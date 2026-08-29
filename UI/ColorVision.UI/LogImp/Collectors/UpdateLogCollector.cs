using log4net;
using System.IO;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// Collects per-installation updater logs so feedback packages retain failures that happen
    /// while the main application is not running.
    /// </summary>
    public sealed class UpdateLogCollector : IFeedbackLogCollector, IFeedbackLogTimeRangeCollector
    {
        private const long MaxFileBytes = 10L * 1024 * 1024;
        private static readonly ILog log = LogManager.GetLogger(typeof(UpdateLogCollector));

        public string Name => "Update Logs";
        public string Description => "Main application and plugin update handoff logs";
        public int Order => 5;
        public int RecentDays { get; set; } = 7;
        public string? LogDirectory => GetUpdateStateRoot();

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            var results = new List<(string, string)>();
            string? stateRoot = LogDirectory;
            if (string.IsNullOrWhiteSpace(stateRoot) || !Directory.Exists(stateRoot))
                return results;

            foreach (FileInfo file in GetRecentUpdateLogs(stateRoot, RecentDays, DateTime.UtcNow))
            {
                try
                {
                    string tempCopy = Path.Combine(Path.GetTempPath(), $"ColorVision_UpdateLog_{Guid.NewGuid():N}.log");
                    file.CopyTo(tempCopy, true);
                    string relativePath = Path.GetRelativePath(stateRoot, file.FullName);
                    results.Add(($"UpdateLogs/{relativePath}", tempCopy));
                }
                catch (Exception ex)
                {
                    log.Debug($"Could not collect update log {file.FullName}: {ex.Message}");
                }
            }

            return results;
        }

        internal static IReadOnlyList<FileInfo> GetRecentUpdateLogs(string stateRoot, int recentDays, DateTime utcNow)
        {
            try
            {
                DateTime cutoffUtc = utcNow.AddDays(-Math.Max(1, recentDays));
                return new DirectoryInfo(stateRoot)
                    .EnumerateFiles("update.log", SearchOption.AllDirectories)
                    .Where(file => file.Length <= MaxFileBytes && file.LastWriteTimeUtc >= cutoffUtc)
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ToList();
            }
            catch (Exception ex)
            {
                log.Debug($"Could not enumerate update logs in {stateRoot}: {ex.Message}");
                return Array.Empty<FileInfo>();
            }
        }

        private static string GetUpdateStateRoot() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ColorVision",
            "UpdateState");
    }
}
