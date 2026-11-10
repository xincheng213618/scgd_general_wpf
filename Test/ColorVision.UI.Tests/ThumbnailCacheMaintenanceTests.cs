using ColorVision.UI;
using ColorVision.Solution.MultiImageViewer;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
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
        Execute("INSERT INTO ThumbnailCache SELECT 2, 'ORIGINAL.png', FileLastModified, ThumbnailData, ThumbnailWidth, ThumbnailHeight, 640, 480, FileSize, CreateDate, RequestedThumbnailSize FROM ThumbnailCache WHERE Id = 1;");
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
        File.SetLastWriteTime(first, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local));
        DateTime lastModified = File.GetLastWriteTime(first);
        using (SqliteConnection connection = Open())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE ThumbnailCache SET FilePath = $first, FileLastModified = $time, FileSize = $size; INSERT INTO ThumbnailCache SELECT 2, $removed, FileLastModified, ThumbnailData, ThumbnailWidth, ThumbnailHeight, 640, 480, FileSize, CreateDate, RequestedThumbnailSize FROM ThumbnailCache WHERE Id = 1;";
            command.Parameters.AddWithValue("$first", first);
            command.Parameters.AddWithValue("$removed", removed);
            command.Parameters.AddWithValue("$time", lastModified);
            command.Parameters.AddWithValue("$size", new FileInfo(first).Length);
            command.ExecuteNonQuery();
        }
        using var database = CreateReaderDatabase();
        ThumbnailCacheManager reader = CreateReader(database);
        int queries = 0;
        database.Aop.OnLogExecuting = (_, _) => queries++;
        string[] paths = [first, removed, first, first.ToUpperInvariant()];
        IReadOnlyDictionary<string, ThumbnailCacheEntry> metadata = ReadCachedInfos(reader, paths);
        Assert.Equal(1, queries);
        Assert.Equal(1024, CreateItem(first).ImageWidth);
        File.Delete(removed);
        File.WriteAllText(first, "updated source content");
        File.SetLastWriteTime(first, lastModified);

        ImageFileInfo[] items = paths.Select(CreateItem).ToArray();
        Assert.Equal(paths, items.Select(item => item.FilePath));
        Assert.NotSame(items[0], items[2]);
        Assert.Equal((0, 0), (items[0].ImageWidth, items[2].ImageWidth));
        Assert.False(items[1].FileExists);
        Assert.Equal(0, items[1].ImageWidth);
        Assert.True(items[3].FileExists);
        Assert.Equal(0, items[3].ImageWidth);
        Assert.Equal(new FileInfo(first).Length, items[0].FileSize);
        Assert.Equal(lastModified, items[0].LastModified);
        Assert.Equal(1, queries);

        ImageFileInfo CreateItem(string path)
        {
            metadata.TryGetValue(path, out ThumbnailCacheEntry? cachedInfo);
            return (ImageFileInfo)Activator.CreateInstance(typeof(ImageFileInfo), BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, args: [path, cachedInfo], culture: null)!;
        }
    }

    // The dimensions belong to the returned image, not to a later, independently queried cache row.
    [Theory]
    [InlineData("none")]
    [InlineData("time")]
    [InlineData("length")]
    [InlineData("bad PNG")]
    [InlineData("PNG dimensions")]
    public async Task ThumbnailResultKeepsOriginalDimensionsAndRebuildsInvalidCache(string difference)
    {
        CreateDatabase();
        string source = Path.Combine(_root, "source.png");
        byte[] png = EncodeGrayPng(2, 2);
        File.WriteAllBytes(source, png);
        DateTime storedTime = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);
        File.SetLastWriteTime(source, difference == "time" ? storedTime.AddDays(1) : storedTime);
        using (SqliteConnection connection = Open())
        using (SqliteCommand command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE ThumbnailCache SET FilePath = $path, FileLastModified = $time, FileSize = $size, ThumbnailData = $png, ThumbnailWidth = $width, ThumbnailHeight = 2, RequestedThumbnailSize = 120;";
            command.Parameters.AddWithValue("$path", source);
            command.Parameters.AddWithValue("$time", storedTime);
            command.Parameters.AddWithValue("$size", difference == "length" ? png.LongLength + 1 : png.LongLength);
            command.Parameters.AddWithValue("$png", difference == "bad PNG" ? new byte[] { 1, 2, 3 } : png);
            command.Parameters.AddWithValue("$width", difference == "PNG dimensions" ? 120 : 2);
            command.ExecuteNonQuery();
        }
        using var database = CreateReaderDatabase();
        ThumbnailCacheManager reader = CreateReader(database);
        object? result = await WpfTestHost.Invoke(() => LoadWithDimensionsAsync(reader, source));
        Assert.NotNull(result);
        var thumbnail = Assert.IsAssignableFrom<BitmapSource>(result.GetType().GetProperty("Thumbnail")!.GetValue(result));
        Assert.Equal((2, 2), (thumbnail.PixelWidth, thumbnail.PixelHeight));
        int expectedOriginalSize = difference == "none" ? 1024 : 2;
        Assert.Equal(expectedOriginalSize, result.GetType().GetProperty("OriginalWidth")!.GetValue(result));
        Assert.Equal(expectedOriginalSize, result.GetType().GetProperty("OriginalHeight")!.GetValue(result));
        Assert.True(thumbnail.IsFrozen);
        if (difference != "none")
        {
            ThumbnailCacheEntry stored = Assert.IsType<ThumbnailCacheEntry>(database.Queryable<ThumbnailCacheEntry>().First(row => row.FilePath == source));
            Assert.Equal(File.GetLastWriteTime(source), stored.FileLastModified);
            Assert.Equal(png.LongLength, stored.FileSize);
            Assert.Equal(120, stored.RequestedThumbnailSize);
            Assert.Equal(png[..8], stored.ThumbnailData![..8]);
        }
    }

    [Fact]
    public async Task ChangingRequestedThumbnailSizeRegeneratesTheCachedImage()
    {
        CreateDatabase();
        string source = Path.Combine(_root, "resize.png");
        File.WriteAllBytes(source, EncodeGrayPng(256, 64));
        File.SetLastWriteTime(source, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local));
        using var database = CreateReaderDatabase();
        ThumbnailCacheManager reader = CreateReader(database);

        var initial = Assert.IsAssignableFrom<BitmapSource>(await reader.GetOrCreateThumbnailAsync(source, 120));
        Assert.Equal((120, 30), (initial.PixelWidth, initial.PixelHeight));
        var resized = Assert.IsAssignableFrom<BitmapSource>(await reader.GetOrCreateThumbnailAsync(source, 64));
        Assert.Equal((64, 16), (resized.PixelWidth, resized.PixelHeight));
        ThumbnailCacheEntry stored = Assert.IsType<ThumbnailCacheEntry>(database.Queryable<ThumbnailCacheEntry>().First(row => row.FilePath == source));
        Assert.Equal(64, stored.RequestedThumbnailSize);
        Assert.Equal((256, 64), (stored.OriginalWidth, stored.OriginalHeight));
    }

    [Fact]
    public async Task UnchangedSourceWithSubsecondTimestampReusesTheCachedThumbnail()
    {
        CreateDatabase();
        string path = Path.Combine(_root, "fractional-timestamp.cv-test-thumbnail");
        File.WriteAllText(path, "provider fixture");
        File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddTicks(1_234_567));
        Assert.NotEqual(0, File.GetLastWriteTimeUtc(path).Ticks % TimeSpan.TicksPerSecond);
        using var database = CreateReaderDatabase();
        ThumbnailCacheManager reader = CreateReader(database);
        using var provider = new FixtureThumbnailProvider(path);
        ThumbnailProviderFactory.RegisterProvider(provider);

        Assert.NotNull(await WpfTestHost.Invoke(() => reader.GetOrCreateThumbnailAsync(path)));
        Assert.NotNull(await WpfTestHost.Invoke(() => reader.GetOrCreateThumbnailAsync(path)));

        Assert.Equal(1, provider.GenerateCount);
    }

    [Fact]
    public async Task LegacyCacheSchemaMigratesWithoutLosingRowsAndRebuildsAnUnsizedThumbnail()
    {
        string source = Path.Combine(_root, "legacy.png");
        byte[] png = EncodeGrayPng(256, 64);
        File.WriteAllBytes(source, png);
        DateTime storedTime = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);
        File.SetLastWriteTime(source, storedTime);
        try
        {
            // Keep the real CodeFirst schema and index definitions, then remove only the field absent in the old version.
            using (OpenCacheDatabase()) { }
            Execute("ALTER TABLE ThumbnailCache DROP COLUMN RequestedThumbnailSize;");
            using (SqliteConnection connection = Open())
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO ThumbnailCache (FilePath, FileLastModified, FileSize, ThumbnailData, ThumbnailWidth, ThumbnailHeight, OriginalWidth, OriginalHeight, CreateDate) VALUES ($path, $time, $size, $png, 256, 64, 256, 64, $time);";
                command.Parameters.AddWithValue("$path", source);
                command.Parameters.AddWithValue("$time", storedTime);
                command.Parameters.AddWithValue("$size", png.LongLength);
                command.Parameters.AddWithValue("$png", png);
                command.ExecuteNonQuery();
            }

            using var database = OpenCacheDatabase();
            ThumbnailCacheManager reader = CreateReader(database);
            ThumbnailCacheEntry legacy = Assert.IsType<ThumbnailCacheEntry>(database.Queryable<ThumbnailCacheEntry>().First(row => row.FilePath == source));
            Assert.Equal(1, legacy.Id);
            Assert.Equal(png, legacy.ThumbnailData);
            Assert.Null(legacy.RequestedThumbnailSize);

            var rebuilt = Assert.IsAssignableFrom<BitmapSource>(await reader.GetOrCreateThumbnailAsync(source, 120));
            Assert.Equal((120, 30), (rebuilt.PixelWidth, rebuilt.PixelHeight));
            ThumbnailCacheEntry stored = Assert.IsType<ThumbnailCacheEntry>(database.Queryable<ThumbnailCacheEntry>().First(row => row.FilePath == source));
            Assert.Equal(120, stored.RequestedThumbnailSize);
            Assert.Equal(1, CountRows());
        }
        finally
        {
            using var pooledConnection = new SqliteConnection($"Data Source={DatabasePath}");
            SqliteConnection.ClearPool(pooledConnection);
        }

        SqlSugar.SqlSugarClient OpenCacheDatabase() => (SqlSugar.SqlSugarClient)typeof(ThumbnailCacheManager)
            .GetMethod("OpenDatabase", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [DatabasePath])!;
    }

    [Fact]
    public async Task SourceChangedDuringGenerationDoesNotPublishOrWriteItsOldThumbnail()
    {
        CreateDatabase();
        string path = Path.Combine(_root, "changing-source.cv-test-thumbnail");
        File.WriteAllText(path, "first source");
        DateTime storedTime = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Local);
        File.SetLastWriteTime(path, storedTime);
        using var database = CreateReaderDatabase();
        ThumbnailCacheManager reader = CreateReader(database);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var provider = new FixtureThumbnailProvider(path, release.Task);
        ThumbnailProviderFactory.RegisterProvider(provider);

        Task<BitmapSource?> pending = WpfTestHost.Invoke(() => reader.GetOrCreateThumbnailAsync(path));
        try
        {
            await provider.Entered.WaitAsync(TimeSpan.FromSeconds(10));
            File.WriteAllText(path, "a replacement with different length");
            File.SetLastWriteTime(path, storedTime);
        }
        finally
        {
            release.TrySetResult();
        }
        Assert.Null(await pending.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Null(reader.GetCachedInfo(path));

        Assert.NotNull(await WpfTestHost.Invoke(() => reader.GetOrCreateThumbnailAsync(path)));
        Assert.Equal(new FileInfo(path).Length, reader.GetCachedInfo(path)!.FileSize);
    }

    [Theory]
    [InlineData(1024, 1, 120, 1)]
    [InlineData(1, 1024, 1, 120)]
    public async Task StandardThumbnailsGenerateAndLoadFromCacheWithTheUiDispatcherBlocked(int width, int height, int thumbWidth, int thumbHeight)
    {
        CreateDatabase();
        string source = Path.Combine(_root, "strip.png");
        var dispatcher = WpfTestHost.Invoke(() =>
        {
            byte[] pixels = Enumerable.Repeat((byte)37, width * height).ToArray();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width)));
            using var output = File.Create(source);
            encoder.Save(output);
            return Application.Current.Dispatcher;
        });
        File.SetLastWriteTime(source, new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Local));
        using var database = CreateReaderDatabase();
        ThumbnailCacheManager reader = CreateReader(database);
        using ManualResetEventSlim release = new();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task blocked = dispatcher.InvokeAsync(() =>
        {
            entered.SetResult();
            // Safety timeout. The test releases the dispatcher as soon as generation and cache read finish.
            if (!release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("UI dispatcher gate was not released.");
        }).Task;

        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var generated = Assert.IsAssignableFrom<BitmapSource>(await reader.GetOrCreateThumbnailAsync(source).WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal((thumbWidth, thumbHeight), (generated.PixelWidth, generated.PixelHeight));
            Assert.True(generated.IsFrozen);
            Assert.Equal((width, height), (reader.GetCachedInfo(source)!.OriginalWidth, reader.GetCachedInfo(source)!.OriginalHeight));

            // The second request must decode the cached PNG even though the UI thread is still gated.
            var cached = Assert.IsAssignableFrom<BitmapSource>(await reader.GetOrCreateThumbnailAsync(source).WaitAsync(TimeSpan.FromSeconds(10)));
            Assert.Equal((thumbWidth, thumbHeight), (cached.PixelWidth, cached.PixelHeight));
            Assert.True(cached.IsFrozen);
            File.Delete(source);
            byte[] output = new byte[thumbWidth * thumbHeight * 4];
            generated.CopyPixels(output, thumbWidth * 4, 0);
            Assert.Equal(new byte[] { 37, 37, 37, 255 }, output[..4]);
        }
        finally
        {
            release.Set();
            await blocked;
        }
    }

    [Fact]
    public async Task DisablingCacheStillUsesTheMatchingThumbnailProvider()
    {
        string path = Path.Combine(_root, "fixture.cv-test-thumbnail");
        File.WriteAllText(path, "provider fixture");
        using var provider = new FixtureThumbnailProvider(path);
        ThumbnailProviderFactory.RegisterProvider(provider);
        var item = (ImageFileInfo)Activator.CreateInstance(typeof(ImageFileInfo), BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null, args: [path, null], culture: null)!;
        await WpfTestHost.Invoke(() => item.LoadThumbnailAsync(useCache: false));
        Assert.Equal((512, 128), (item.ImageWidth, item.ImageHeight));
        var bitmap = Assert.IsAssignableFrom<BitmapSource>(item.Thumbnail);
        Assert.Equal((4, 1), (bitmap.PixelWidth, bitmap.PixelHeight));
        Assert.True(bitmap.IsFrozen);
        WpfTestHost.Invoke(() => Assert.False(provider.Bitmap!.IsFrozen));
    }

    private sealed class FixtureThumbnailProvider(string path, Task? release = null) : IThumbnailProvider, IDisposable
    {
        private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public BitmapSource? Bitmap { get; private set; }
        public Task Entered => _entered.Task;
        public int GenerateCount { get; private set; }
        public int Order => 0;
        public bool CanHandle(string filePath) => filePath == path;
        public (int width, int height) GetImageDimensions(string filePath) => (512, 128);
        public async Task<BitmapSource?> GenerateThumbnailAsync(string filePath, int maxSize)
        {
            Bitmap = new WriteableBitmap(4, 1, 96, 96, PixelFormats.Gray8, null);
            GenerateCount++;
            _entered.TrySetResult();
            if (release != null) await release;
            return Bitmap;
        }

        public void Dispose()
        {
            var providers = (List<IThumbnailProvider>)typeof(ThumbnailProviderFactory).GetField("_providers", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            providers.Remove(this);
        }
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
        Execute("INSERT INTO ThumbnailCache SELECT 2, 'new.png', FileLastModified, ThumbnailData, ThumbnailWidth, ThumbnailHeight, OriginalWidth, OriginalHeight, FileSize, CreateDate, RequestedThumbnailSize FROM ThumbnailCache WHERE Id = 1;");

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
            .Invoke(reader, [source, 120, CancellationToken.None])!;
        await pending;
        return pending.GetType().GetProperty("Result")!.GetValue(pending);
    }

    private static byte[] EncodeGrayPng(int width, int height) => WpfTestHost.Invoke(() =>
    {
        var encoder = new PngBitmapEncoder();
        byte[] pixels = Enumerable.Repeat((byte)37, width * height).ToArray();
        encoder.Frames.Add(BitmapFrame.Create(BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray8, null, pixels, width)));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    });

    private void CreateDatabase()
    {
        Execute("CREATE TABLE ThumbnailCache (Id INTEGER PRIMARY KEY, FilePath TEXT, FileLastModified TEXT, ThumbnailData BLOB, ThumbnailWidth INTEGER, ThumbnailHeight INTEGER, OriginalWidth INTEGER, OriginalHeight INTEGER, FileSize INTEGER, CreateDate TEXT, RequestedThumbnailSize INTEGER NULL);");
        Execute("INSERT INTO ThumbnailCache VALUES (1, 'original.png', '2026-01-01', zeroblob(16384), 120, 120, 1024, 1024, 100000, '2026-01-01', NULL);");
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
