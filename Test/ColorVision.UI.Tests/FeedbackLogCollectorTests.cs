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
    }
}
