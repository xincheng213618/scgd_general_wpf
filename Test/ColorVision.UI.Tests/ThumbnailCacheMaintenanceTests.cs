using ColorVision.Solution.MultiImageViewer;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Reflection;

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
