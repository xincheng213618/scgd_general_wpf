using ColorVision.Engine.Services.Storage;
using ColorVision.UI.Maintenance;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class EngineStorageUsageProviderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ColorVisionEngineStorageTests-").FullName;

    [Fact]
    public void DiscoveryHasStableCategoriesWithoutPreparingOrOpeningAnyStorage()
    {
        int preparations = 0;
        var provider = new EngineStorageUsageProvider(() => { preparations++; return []; }, () => false);
        FileMaintenanceCategory[] cleanup = provider.GetCategories().ToArray();
        StorageUsageCategory[] usage = provider.GetStorageCategories().ToArray();

        Assert.Equal(0, preparations);
        Assert.All(usage, category => { Assert.NotEqual(category.Id, category.Title); Assert.False(string.IsNullOrWhiteSpace(category.Description)); });
        Assert.Equal(3, cleanup.Length);
        Assert.All(cleanup, category => { Assert.False(category.DefaultSelected); Assert.Empty(category.SourceDirectories); });
        StorageUsageCategory resources = Assert.Single(usage, category => category.Id == EngineStorageUsageProvider.CameraResources);
        Assert.True(resources.IsProtected);
        Assert.Null(resources.ManagementCategoryId);
        Assert.NotNull(resources.OpenManager); // Reading the delegate never opens its window.
    }

    [Fact]
    public void WholeUsageAndRetentionCandidatesStaySeparateAndResourcesRemainProtectedAcrossOverlaps()
    {
        string resource = CreateFile("camera/cfg/calibration.cvcie", -90);
        string old = CreateFile("camera/Data/old.cvraw", -60);
        string recent = CreateFile("camera/Data/recent.cvraw", -1);
        var provider = new EngineStorageUsageProvider(() => [
            new(EngineStorageUsageProvider.CameraResources, new(Path.GetDirectoryName(resource)!, Recursive: true)),
            new(EngineStorageUsageProvider.CameraImages, new(Path.GetDirectoryName(old)!, Recursive: true)),
            new(EngineStorageUsageProvider.PreProcessFiles, new(Path.Combine(_root, "camera"), "*.cv*", true))], () => false);
        provider.PrepareStorageScan();

        StorageUsageCategory images = Assert.Single(provider.GetStorageCategories(), category => category.Id == EngineStorageUsageProvider.CameraImages);
        Assert.Equal(2, images.Scan(CancellationToken.None).Count);
        FileMaintenanceCategory cleanup = Assert.Single(provider.GetCategories(), category => category.Id == EngineStorageUsageProvider.PreProcessFiles);
        FileMaintenanceSnapshot candidates = cleanup.Scan(30, null, CancellationToken.None);
        Assert.Equal(old, Assert.Single(candidates.Files).FullPath);
        FileMaintenanceCleanupResult result = cleanup.Cleanup(candidates, CancellationToken.None);
        Assert.Equal(1, result.DeletedCount);
        Assert.False(File.Exists(old));
        Assert.True(File.Exists(resource));
        Assert.True(File.Exists(recent));
    }

    [Fact]
    public void StartingAnOperationOrChangingAConfirmedFilePreventsItsDeletion()
    {
        string image = CreateFile("Data/old.cvraw", -60);
        bool active = false;
        var provider = new EngineStorageUsageProvider(() => [new(EngineStorageUsageProvider.CameraImages, new(Path.GetDirectoryName(image)!, Recursive: true))], () => active);
        provider.PrepareScan();
        FileMaintenanceCategory category = Assert.Single(provider.GetCategories(), category => category.Id == EngineStorageUsageProvider.CameraImages);
        FileMaintenanceSnapshot confirmed = category.Scan(30, null, CancellationToken.None);
        active = true;
        Assert.Equal(1, category.Cleanup(confirmed, CancellationToken.None).SkippedCount);
        Assert.True(File.Exists(image));
        active = false;
        File.AppendAllText(image, "changed since confirmation");
        Assert.Equal(1, category.Cleanup(confirmed, CancellationToken.None).SkippedCount);
        Assert.True(File.Exists(image));
    }

    [Fact]
    public void ReadOnlyContributionsDoNotBecomeCleanupAllowLists()
    {
        string original = CreateFile("custom/output.png", -60);
        var provider = new EngineStorageUsageProvider(() => [
            new(EngineStorageUsageProvider.PreProcessFiles, new(Path.GetDirectoryName(original)!, Recursive: true), AllowCleanup: false)], () => false);
        provider.PrepareStorageScan();
        StorageUsageCategory usage = Assert.Single(provider.GetStorageCategories(), category => category.Id == EngineStorageUsageProvider.PreProcessFiles);
        Assert.Equal(original, Assert.Single(usage.Scan(CancellationToken.None).Files).FullPath);
        Assert.Null(usage.ManagementCategoryId);
        FileMaintenanceCategory cleanup = Assert.Single(provider.GetCategories(), category => category.Id == EngineStorageUsageProvider.PreProcessFiles);
        Assert.Empty(cleanup.SourceDirectories);
        Assert.Empty(cleanup.Scan(0, null, CancellationToken.None).Files);
        Assert.True(File.Exists(original));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("other/camera")]
    [InlineData("")]
    public void OwnedDeviceRootsCannotEscapeThroughADeviceIdentifier(string code)
        => Assert.Null(EngineStorageUsageProvider.OwnedDeviceDirectory(_root, code, "Data"));

    private string CreateFile(string name, int days)
    {
        string path = Path.GetFullPath(Path.Combine(_root, name));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "isolated test file");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(days));
        return path;
    }

    public void Dispose()
    {
        string root = Path.GetFullPath(_root);
        string temporary = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(root).StartsWith("ColorVisionEngineStorageTests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected test directory.");
        Directory.Delete(root, true);
    }
}
