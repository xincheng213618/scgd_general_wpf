using ColorVision.Engine.Services;
using ColorVision.UI.Maintenance;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class ServiceLogFileMaintenanceTests
{
    [Fact]
    public void HistoricalServiceLogsHonorRetentionAndKeepLatestTodaysAndActiveHostFiles()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ColorVision_ServiceMaintenance_{Guid.NewGuid():N}");
        string module = Path.Combine(root, "service", "log", "Camera");
        Directory.CreateDirectory(module);
        string old = CreateFile(module, "old.log", -40);
        string recent = CreateFile(module, "recent.log", -2);
        string latest = CreateFile(module, "latest.log", -1);
        string host = CreateFile(module, "ColorVisionServiceHost.log", -50);
        string today = CreateFile(root, "today.log", 0);
        try
        {
            var provider = new ServiceLogFileMaintenanceProvider(() => [root]);
            FileMaintenanceCategory category = Assert.Single(provider.GetCategories());
            Assert.False(string.IsNullOrWhiteSpace(category.Title));
            Assert.False(string.IsNullOrWhiteSpace(category.Description));
            Assert.False(category.DefaultSelected);
            provider.PrepareScan();
            Assert.Equal(root, Assert.Single(category.SourceDirectories));
            FileMaintenanceSnapshot retained = category.Scan(30, null, CancellationToken.None);
            Assert.Equal(old, Assert.Single(retained.Files).FullPath);
            FileMaintenanceSnapshot all = category.Scan(0, retained, CancellationToken.None);
            Assert.Equal(2, all.Count);

            File.SetLastWriteTimeUtc(recent, DateTime.UtcNow);
            FileMaintenanceCleanupResult result = category.Cleanup(all, CancellationToken.None);
            Assert.Equal(1, result.DeletedCount);
            Assert.Equal(1, result.SkippedCount);
            Assert.False(File.Exists(old));
            foreach (string path in new[] { recent, latest, host, today }) Assert.True(File.Exists(path));
        }
        finally
        {
            string fullRoot = Path.GetFullPath(root);
            string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(fullRoot).StartsWith("ColorVision_ServiceMaintenance_", StringComparison.Ordinal))
                Directory.Delete(fullRoot, true);
        }
    }

    private static string CreateFile(string root, string name, int days)
    {
        string path = Path.Combine(root, name);
        File.WriteAllText(path, "isolated service log");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(days));
        return path;
    }
}
