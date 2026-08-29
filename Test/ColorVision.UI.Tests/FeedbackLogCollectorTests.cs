using ColorVision.Engine.Services;
using ColorVision.UI.LogImp;
using System.IO;

namespace ColorVision.UI.Tests
{
    public sealed class FeedbackLogCollectorTests
    {
        [Fact]
        public void AppLogCollectorDefaultsToSevenDays()
        {
            var collector = new AppLogCollector();

            Assert.IsAssignableFrom<IFeedbackLogTimeRangeCollector>(collector);
            Assert.Equal(7, collector.RecentDays);
        }

        [Fact]
        public void AppLogCollectorFiltersFilesBySelectedDayRange()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"ColorVision_AppLogCollectorTests_{Guid.NewGuid():N}");
            string fullTempDirectory = Path.GetFullPath(tempDirectory);
            Directory.CreateDirectory(fullTempDirectory);

            try
            {
                DateTime utcNow = new(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
                string recentPath = Path.Combine(fullTempDirectory, "recent.log");
                string oldPath = Path.Combine(fullTempDirectory, "old.log");
                File.WriteAllText(recentPath, "recent");
                File.WriteAllText(oldPath, "old");
                File.SetLastWriteTimeUtc(recentPath, utcNow.AddDays(-6));
                File.SetLastWriteTimeUtc(oldPath, utcNow.AddDays(-8));

                IReadOnlyList<FileInfo> files = AppLogCollector.GetRecentLogFiles(fullTempDirectory, 7, utcNow);

                FileInfo file = Assert.Single(files);
                Assert.Equal(recentPath, file.FullName, ignoreCase: true);
            }
            finally
            {
                if (Directory.Exists(fullTempDirectory)
                    && fullTempDirectory.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                {
                    Directory.Delete(fullTempDirectory, true);
                }
            }
        }

        [Fact]
        public void UpdateLogCollectorIncludesRecentPerInstallationLogs()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"ColorVision_UpdateLogCollectorTests_{Guid.NewGuid():N}");
            string fullTempDirectory = Path.GetFullPath(tempDirectory);
            Directory.CreateDirectory(fullTempDirectory);

            try
            {
                DateTime utcNow = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
                string recentDirectory = Path.Combine(fullTempDirectory, "CURRENTINSTALL01");
                string oldDirectory = Path.Combine(fullTempDirectory, "OLDINSTALL000001");
                Directory.CreateDirectory(recentDirectory);
                Directory.CreateDirectory(oldDirectory);
                string recentPath = Path.Combine(recentDirectory, "update.log");
                string oldPath = Path.Combine(oldDirectory, "update.log");
                File.WriteAllText(recentPath, "recent updater failure");
                File.WriteAllText(oldPath, "old updater failure");
                File.SetLastWriteTimeUtc(recentPath, utcNow.AddDays(-1));
                File.SetLastWriteTimeUtc(oldPath, utcNow.AddDays(-8));

                IReadOnlyList<FileInfo> files = UpdateLogCollector.GetRecentUpdateLogs(fullTempDirectory, 7, utcNow);

                FileInfo file = Assert.Single(files);
                Assert.Equal(recentPath, file.FullName, ignoreCase: true);
                Assert.IsAssignableFrom<IFeedbackLogCollector>(new UpdateLogCollector());
            }
            finally
            {
                if (Directory.Exists(fullTempDirectory)
                    && fullTempDirectory.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                {
                    Directory.Delete(fullTempDirectory, true);
                }
            }
        }

        [Fact]
        public void ServiceLogCollectorDefaultsToSevenDays()
        {
            var collector = new ServiceLogCollector();

            Assert.IsAssignableFrom<IFeedbackLogTimeRangeCollector>(collector);
            Assert.Equal(7, collector.RecentDays);
        }

        [Fact]
        public void ServiceLogCollectorIncludesAllRecentModuleLogs()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"ColorVision_ServiceLogCollectorTests_{Guid.NewGuid():N}");
            string fullTempDirectory = Path.GetFullPath(tempDirectory);
            Directory.CreateDirectory(fullTempDirectory);

            try
            {
                DateTime utcNow = new(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
                string[] moduleNames = ["camera", "Algorithm", "CVOLED", "Spectrum", "Sensor", "CustomModule"];
                foreach (string moduleName in moduleNames)
                {
                    string moduleDirectory = Path.Combine(fullTempDirectory, moduleName);
                    Directory.CreateDirectory(moduleDirectory);
                    for (int index = 0; index < 2; index++)
                    {
                        string path = Path.Combine(moduleDirectory, $"{moduleName}_{index}.log");
                        File.WriteAllText(path, moduleName);
                        File.SetLastWriteTimeUtc(path, utcNow.AddDays(-6));
                    }
                }

                string nonLogExtensionPath = Path.Combine(fullTempDirectory, "CustomModule.trace");
                File.WriteAllText(nonLogExtensionPath, "custom");
                File.SetLastWriteTimeUtc(nonLogExtensionPath, utcNow.AddDays(-1));

                string oldPath = Path.Combine(fullTempDirectory, "old.log");
                File.WriteAllText(oldPath, "old");
                File.SetLastWriteTimeUtc(oldPath, utcNow.AddDays(-8));

                IReadOnlyList<FileInfo> files = ServiceLogCollector.GetRecentLogFiles(fullTempDirectory, 7, utcNow);

                Assert.Equal(13, files.Count);
                Assert.Contains(files, file => string.Equals(file.FullName, nonLogExtensionPath, StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(files, file => string.Equals(file.FullName, oldPath, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(fullTempDirectory)
                    && fullTempDirectory.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase))
                {
                    Directory.Delete(fullTempDirectory, true);
                }
            }
        }
    }
}
