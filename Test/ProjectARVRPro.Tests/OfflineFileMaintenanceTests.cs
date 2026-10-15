using ProjectARVRPro.Offline;
using System.IO;
using System.Text.Json;

namespace ProjectARVRPro.Tests;

public sealed class OfflineFileMaintenanceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "ColorVisionOfflineMaintenanceTests-" + Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(7)]
    [InlineData(0)]
    public void OnlyExpiredGuidCopiesAreScannedAndLiveSessionsRemainProtected(int retentionDays)
    {
        string old = CreateCopy(Guid.NewGuid().ToString("N"));
        string active = CreateCopy(Guid.NewGuid().ToString("N"), active: true);
        string unknown = CreateCopy("external-feedback");
        string original = Path.Combine(_root, "original.zip");
        File.WriteAllText(original, "original data");
        var category = Assert.Single(new OfflineFileMaintenanceProvider(_root).GetCategories());
        Assert.Equal(_root, Assert.Single(category.SourceDirectories));
        if (retentionDays == 0)
        {
            Directory.SetCreationTimeUtc(old, DateTime.UtcNow.AddDays(-1));
            File.SetLastWriteTimeUtc(Path.Combine(old, "ProjectARVRPro.db"), DateTime.UtcNow.AddDays(-1));
        }

        var scan = category.Scan(retentionDays, null, CancellationToken.None);
        Assert.False(category.DefaultSelected);
        Assert.Equal(Path.Combine(old, "ProjectARVRPro.db"), Assert.Single(scan.Files).FullPath);
        Assert.Equal(1, category.Scan(retentionDays, scan, CancellationToken.None).ReusedFileCount);
        var result = category.Cleanup(scan, CancellationToken.None);

        Assert.Equal(1, result.DeletedCount);
        Assert.True(File.Exists(Path.Combine(active, "ProjectARVRPro.db")));
        Assert.True(File.Exists(Path.Combine(unknown, "ProjectARVRPro.db")));
        Assert.True(File.Exists(original));
    }

    [Fact]
    public void OwnershipIsRecheckedWhenTheConfirmedFilesAreCleaned()
    {
        string directory = CreateCopy(Guid.NewGuid().ToString("N"));
        var category = Assert.Single(new OfflineFileMaintenanceProvider(_root).GetCategories());
        var scan = category.Scan(7, null, CancellationToken.None);
        OfflineFileMaintenanceProvider.CreateImportDirectory(directory);
        var result = category.Cleanup(scan, CancellationToken.None);
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.True(File.Exists(Path.Combine(directory, "ProjectARVRPro.db")));
    }

    private string CreateCopy(string name, bool active = false)
    {
        string directory = Path.Combine(_root, name);
        if (active) OfflineFileMaintenanceProvider.CreateImportDirectory(directory);
        else
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(directory + ".owner.json", JsonSerializer.Serialize(new { ProcessId = int.MaxValue, StartTimeUtcTicks = 1L }));
        }
        File.WriteAllText(Path.Combine(directory, "ProjectARVRPro.db"), "isolated database file");
        File.SetLastWriteTimeUtc(Path.Combine(directory, "ProjectARVRPro.db"), DateTime.UtcNow.AddDays(-30));
        Directory.SetCreationTimeUtc(directory, DateTime.UtcNow.AddDays(-30));
        return directory;
    }

    public void Dispose()
    {
        string root = Path.GetFullPath(_root), temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("ColorVisionOfflineMaintenanceTests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Invalid isolated test root.");
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
