using ColorVision.Solution.MultiImageViewer;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ThumbnailCacheMaintenanceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ColorVisionThumbnailMaintenanceTests-").FullName;
    private string DatabasePath => Path.Combine(_root, "ThumbnailCache.db");

    [Fact]
    public void ScanningMissingCacheDoesNotCreateDirectoryOrDatabase()
    {
        string path = Path.Combine(_root, "not-created", "ThumbnailCache.db");
        ThumbnailCacheMaintenanceSnapshot scan = Scan(path);

        Assert.False(scan.Exists);
        Assert.False(scan.CanCleanup);
        Assert.Null(scan.Error);
        Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
    }

    // Metadata reads must not materialize the PNG payload or cache stale original dimensions.
    [Fact]
    public void MetadataReadsExcludePngPayloadAndObserveUpdatedDimensions()
    {
        CreateDatabase();
        using var database = CreateReaderDatabase();
        ThumbnailCacheManager reader = CreateReader(database);
        List<string> queries = [];
        database.Aop.OnLogExecuting = (sql, _) => queries.Add(sql);

        ThumbnailCacheEntry first = Assert.IsType<ThumbnailCacheEntry>(reader.GetCachedInfo("original.png"));
        Assert.Null(first.ThumbnailData);
        Assert.Equal((1024, 1024, 120, 120, 100000L),
            (first.OriginalWidth, first.OriginalHeight, first.ThumbnailWidth, first.ThumbnailHeight, first.FileSize));
        Execute("UPDATE ThumbnailCache SET OriginalWidth = 1920, OriginalHeight = 1080;");
        ThumbnailCacheEntry changed = Assert.IsType<ThumbnailCacheEntry>(reader.GetCachedInfo("original.png"));
        Assert.Equal((1920, 1080), (changed.OriginalWidth, changed.OriginalHeight));
        Assert.Equal(2, queries.Count);
        Assert.All(queries, sql => Assert.DoesNotContain("ThumbnailData", sql, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BatchMetadataUsesBoundedQueriesAndPreservesExactPathMatching()
    {
        CreateDatabase();
        Execute("INSERT INTO ThumbnailCache SELECT 2, 'ORIGINAL.png', FileLastModified, ThumbnailData, ThumbnailWidth, ThumbnailHeight, 640, 480, FileSize, CreateDate FROM ThumbnailCache WHERE Id = 1;");
        using var database = CreateReaderDatabase();
        ThumbnailCacheManager reader = CreateReader(database);
        List<string> queries = [];
        database.Aop.OnLogExecuting = (sql, _) => queries.Add(sql);
        string[] paths = ["original.png", "ORIGINAL.png", "original.png", .. Enumerable.Range(0, 499).Select(index => $"missing-{index}.png")];

        IReadOnlyDictionary<string, ThumbnailCacheEntry> metadata = ReadCachedInfos(reader, paths);
        Assert.Equal(2, queries.Count);
        Assert.Equal(2, metadata.Count);
        Assert.Equal(1024, metadata["original.png"].OriginalWidth);
        Assert.Equal(640, metadata["ORIGINAL.png"].OriginalWidth);
        Assert.False(metadata.ContainsKey("Original.png"));
        Assert.All(metadata.Values, entry => Assert.Null(entry.ThumbnailData));
        Assert.All(queries, sql => Assert.DoesNotContain("ThumbnailData", sql, StringComparison.OrdinalIgnoreCase));
        Execute("UPDATE ThumbnailCache SET OriginalWidth = 1920 WHERE Id = 1;");
        Assert.Equal(1920, ReadCachedInfos(reader, ["original.png"])["original.png"].OriginalWidth);
    }

    // Prefetch is local to one load; each item still observes the source file at construction.
    [Fact]
    public void PrefetchedItemsPreserveOrderDuplicatesAndCurrentFileStateWithoutQueries()
    {
        CreateDatabase();
        string first = Path.Combine(_root, "first.png"), removed = Path.Combine(_root, "removed.png");
        File.WriteAllText(first, "first");
        File.WriteAllText(removed, "removed");
        using (SqliteConnection connection = Open())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE ThumbnailCache SET FilePath = $first; INSERT INTO ThumbnailCache SELECT 2, $removed, FileLastModified, ThumbnailData, ThumbnailWidth, ThumbnailHeight, 640, 480, FileSize, CreateDate FROM ThumbnailCache WHERE Id = 1;";
            command.Parameters.AddWithValue("$first", first);
            command.Parameters.AddWithValue("$removed", removed);
            command.ExecuteNonQuery();
        }
        using var database = CreateReaderDatabase();
        ThumbnailCacheManager reader = CreateReader(database);
        int queries = 0;
        database.Aop.OnLogExecuting = (_, _) => queries++;
        string[] paths = [first, removed, first, first.ToUpperInvariant()];
        IReadOnlyDictionary<string, ThumbnailCacheEntry> metadata = ReadCachedInfos(reader, paths);
        Assert.Equal(1, queries);
        File.Delete(removed);
        File.WriteAllText(first, "updated source content");

        ImageFileInfo[] items = paths.Select(path =>
        {
            metadata.TryGetValue(path, out ThumbnailCacheEntry? cachedInfo);
            return (ImageFileInfo)Activator.CreateInstance(typeof(ImageFileInfo), BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, args: [path, cachedInfo], culture: null)!;
        }).ToArray();
        Assert.Equal(paths, items.Select(item => item.FilePath));
        Assert.NotSame(items[0], items[2]);
        Assert.Equal((1024, 1024), (items[0].ImageWidth, items[2].ImageWidth));
        Assert.False(items[1].FileExists);
        Assert.Equal(0, items[1].ImageWidth);
        Assert.True(items[3].FileExists);
        Assert.Equal(0, items[3].ImageWidth);
        Assert.Equal(new FileInfo(first).Length, items[0].FileSize);
        Assert.Equal(File.GetLastWriteTime(first), items[0].LastModified);
        Assert.Equal(1, queries);
    }

    // The dimensions belong to the returned image, not to a later, independently queried cache row.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThumbnailResultKeepsOriginalDimensionsAndRegeneratesChangedSource(bool sourceChanged)
    {
        CreateDatabase();
        string source = Path.Combine(_root, "source.png");
        byte[] png = WpfTestHost.Invoke(() =>
        {
            PngBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(2, 2, 96, 96, PixelFormats.Gray8, null,
                new byte[] { 1, 2, 3, 4 }, 2)));
            using MemoryStream stream = new();
            encoder.Save(stream);
            return stream.ToArray();
        });
        File.WriteAllBytes(source, png);
        DateTime storedTime = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);
        File.SetLastWriteTime(source, sourceChanged ? storedTime.AddDays(1) : storedTime);
        using (SqliteConnection connection = Open())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE ThumbnailCache SET FilePath = $path, FileLastModified = $time, ThumbnailData = $png;";
            command.Parameters.AddWithValue("$path", source);
            command.Parameters.AddWithValue("$time", storedTime);
            command.Parameters.AddWithValue("$png", png);
            command.ExecuteNonQuery();
        }
        using var database = CreateReaderDatabase();
        ThumbnailCacheManager reader = CreateReader(database);
        object? result = await WpfTestHost.Invoke(() => LoadWithDimensionsAsync(reader, source));
        Assert.NotNull(result);
        var thumbnail = Assert.IsAssignableFrom<BitmapSource>(result.GetType().GetProperty("Thumbnail")!.GetValue(result));
        Assert.Equal((2, 2), (thumbnail.PixelWidth, thumbnail.PixelHeight));
        Assert.Equal(sourceChanged ? 2 : 1024, result.GetType().GetProperty("OriginalWidth")!.GetValue(result));
        Assert.Equal(sourceChanged ? 2 : 1024, result.GetType().GetProperty("OriginalHeight")!.GetValue(result));
        Assert.True(thumbnail.IsFrozen);
    }

    [Fact]
    public void ConfirmedCacheClearsRowsAndReportsResultWithoutTouchingOriginalImage()
    {
        CreateDatabase();
        string original = Path.Combine(_root, "original.png");
        File.WriteAllText(original, "original remains");
        ThumbnailCacheMaintenanceSnapshot scan = Scan(DatabasePath);

        Assert.True(scan.CanCleanup, scan.Error);
        Assert.Equal(1, scan.EntryCount);
        Assert.True(scan.SizeBytes > 0);
        ThumbnailCacheMaintenanceResult result = ThumbnailCacheManager.ClearCacheForMaintenance(scan);

        Assert.True(result.Succeeded, result.Message);
        Assert.False(result.RequiresRescan);
        Assert.Equal(1, result.DeletedEntryCount);
        Assert.True(result.ReleasedBytes >= 0);
        Assert.Equal(0, CountRows());
        Assert.Equal("original remains", File.ReadAllText(original));
        Assert.True(File.Exists(DatabasePath));
    }

    [Fact]
    public void NewlyCreatedRowsRequireRescanAndAreNotDeleted()
    {
        CreateDatabase();
        ThumbnailCacheMaintenanceSnapshot scan = Scan(DatabasePath);
        Execute("INSERT INTO ThumbnailCache SELECT 2, 'new.png', FileLastModified, ThumbnailData, ThumbnailWidth, ThumbnailHeight, OriginalWidth, OriginalHeight, FileSize, CreateDate FROM ThumbnailCache WHERE Id = 1;");

        ThumbnailCacheMaintenanceResult result = ThumbnailCacheManager.ClearCacheForMaintenance(scan);

        Assert.False(result.Succeeded);
        Assert.True(result.RequiresRescan);
        Assert.Equal(2, CountRows());
    }

    [Fact]
    public void SameCountMetadataChangesRequireRescan()
    {
        CreateDatabase();
        ThumbnailCacheMaintenanceSnapshot scan = Scan(DatabasePath);
        Execute("UPDATE ThumbnailCache SET FilePath = 'changed.png';");

        ThumbnailCacheMaintenanceResult result = ThumbnailCacheManager.ClearCacheForMaintenance(scan);

        Assert.False(result.Succeeded);
        Assert.True(result.RequiresRescan);
        Assert.Equal(1, CountRows());
    }

    [Fact]
    public void BusyDatabaseReportsFailureWithoutClearingRows()
    {
        CreateDatabase();
        ThumbnailCacheMaintenanceSnapshot scan = Scan(DatabasePath);
        using (SqliteConnection connection = Open())
        using (SqliteTransaction transaction = connection.BeginTransaction())
        {
            ThumbnailCacheMaintenanceResult result = ThumbnailCacheManager.ClearCacheForMaintenance(scan);
            Assert.False(result.Succeeded);
            Assert.Equal(0, result.DeletedEntryCount);
        }
        Assert.Equal(1, CountRows());
    }

    [Fact]
    public void SuccessfulCleanupInvalidatesPreviouslyStartedThumbnailGeneration()
    {
        CreateDatabase();
        FieldInfo generation = typeof(ThumbnailCacheManager).GetField("_cacheMaintenanceGeneration", BindingFlags.Static | BindingFlags.NonPublic)!;
        long before = (long)generation.GetValue(null)!;
        ThumbnailCacheMaintenanceResult result = ThumbnailCacheManager.ClearCacheForMaintenance(Scan(DatabasePath));

        Assert.True(result.Succeeded, result.Message);
        Assert.True((long)generation.GetValue(null)! > before);
    }

    [Fact]
    public void InvalidDatabaseReportsErrorInsteadOfInitializingSchema()
    {
        File.WriteAllText(DatabasePath, "not sqlite");
        ThumbnailCacheMaintenanceSnapshot scan = Scan(DatabasePath);

        Assert.NotNull(scan.Error);
        Assert.False(scan.CanCleanup);
        Assert.False(ThumbnailCacheManager.ClearCacheForMaintenance(scan).Succeeded);
        Assert.Equal("not sqlite", File.ReadAllText(DatabasePath));
    }

    private static ThumbnailCacheMaintenanceSnapshot Scan(string path)
    {
        MethodInfo method = typeof(ThumbnailCacheManager).GetMethod("ScanCacheForMaintenanceAtPath", BindingFlags.Static | BindingFlags.NonPublic)!;
        return (ThumbnailCacheMaintenanceSnapshot)method.Invoke(null, [path])!;
    }

    private SqlSugar.SqlSugarClient CreateReaderDatabase() => new(new SqlSugar.ConnectionConfig
    {
        ConnectionString = $"Data Source={DatabasePath};Pooling=False", DbType = SqlSugar.DbType.Sqlite, IsAutoCloseConnection = true
    });

    private static ThumbnailCacheManager CreateReader(SqlSugar.SqlSugarClient database)
    {
        // Avoid the singleton constructor: this fixture owns only its temporary database.
        var reader = (ThumbnailCacheManager)RuntimeHelpers.GetUninitializedObject(typeof(ThumbnailCacheManager));
        typeof(ThumbnailCacheManager).GetField("_db", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(reader, database);
        return reader;
    }

    private static IReadOnlyDictionary<string, ThumbnailCacheEntry> ReadCachedInfos(ThumbnailCacheManager reader, IReadOnlyList<string> paths) =>
        (IReadOnlyDictionary<string, ThumbnailCacheEntry>)typeof(ThumbnailCacheManager).GetMethod("GetCachedInfos", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(reader, [paths])!;

    private static async Task<object?> LoadWithDimensionsAsync(ThumbnailCacheManager reader, string source)
    {
        Task pending = (Task)typeof(ThumbnailCacheManager).GetMethod("GetOrCreateThumbnailWithDimensionsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(reader, [source, 120])!;
        await pending;
        return pending.GetType().GetProperty("Result")!.GetValue(pending);
    }

    private void CreateDatabase()
    {
        Execute("CREATE TABLE ThumbnailCache (Id INTEGER PRIMARY KEY, FilePath TEXT, FileLastModified TEXT, ThumbnailData BLOB, ThumbnailWidth INTEGER, ThumbnailHeight INTEGER, OriginalWidth INTEGER, OriginalHeight INTEGER, FileSize INTEGER, CreateDate TEXT);");
        Execute("INSERT INTO ThumbnailCache VALUES (1, 'original.png', '2026-01-01', zeroblob(16384), 120, 120, 1024, 1024, 100000, '2026-01-01');");
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private void Execute(string sql)
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private long CountRows()
    {
        using SqliteConnection connection = Open();
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ThumbnailCache;";
        return (long)command.ExecuteScalar()!;
    }

    public void Dispose()
    {
        string root = Path.GetFullPath(_root);
        string temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("ColorVisionThumbnailMaintenanceTests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Refusing to remove an unexpected test directory.");
        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }
}
