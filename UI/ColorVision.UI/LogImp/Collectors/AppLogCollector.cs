using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;
using System.IO;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// Collects the local log4net application log files for the feedback system.
    /// </summary>
    public class AppLogCollector : IFeedbackLogCollector, IFeedbackLogTimeRangeCollector
    {
        private const long MaxFileBytes = 50L * 1024 * 1024;
        private static readonly ILog log = LogManager.GetLogger(typeof(AppLogCollector));

        public string Name => "Application Logs";
        public string Description => "ColorVision UI runtime logs";
        public int Order => 0;
        public int RecentDays { get; set; } = 7;
        public string? LogDirectory => GetLogDirectory();

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            var results = new List<(string, string)>();

            var files = GetRecentApplicationLogFiles(LogDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppDomain.CurrentDomain.BaseDirectory, RecentDays, DateTime.UtcNow);
            foreach (var (entryPath, file) in files)
            {
                try
                {
                    string tempCopy = Path.Combine(Path.GetTempPath(), $"ColorVision_AppLog_{Guid.NewGuid():N}_{file.Name}");
                    file.CopyTo(tempCopy, true);
                    results.Add((entryPath, tempCopy));
                }
                catch (Exception ex)
                {
                    log.Debug($"Could not collect app log file {file.FullName}: {ex.Message}");
                }
            }

            return results;
        }

        internal static IReadOnlyList<(string EntryPath, FileInfo File)> GetRecentApplicationLogFiles(
            string? currentLogDirectory, string applicationDataDirectory, string applicationDirectory, int recentDays, DateTime utcNow)
        {
            (string Source, string? Directory)[] directories =
            [
                ("AppData", Path.Combine(applicationDataDirectory, "ColorVision", "Log")),
                ("Installation", Path.Combine(applicationDirectory, "log")),
                ("Current", currentLogDirectory),
            ];
            var results = new List<(string, FileInfo)>();
            var collectedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (source, directory) in directories)
            {
                if (string.IsNullOrWhiteSpace(directory))
                    continue;

                string fullDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
                if (!collectedDirectories.Add(fullDirectory) || !Directory.Exists(fullDirectory))
                    continue;

                foreach (FileInfo file in GetRecentLogFiles(fullDirectory, recentDays, utcNow))
                    results.Add(($"AppLogs/{source}/{file.Name}", file));
            }

            return results;
        }

        internal static IReadOnlyList<FileInfo> GetRecentLogFiles(string logDir, int recentDays, DateTime utcNow)
        {
            try
            {
                DateTime cutoffUtc = utcNow.AddDays(-Math.Max(1, recentDays));
                return new DirectoryInfo(logDir)
                    .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                    .Where(file => file.Length <= MaxFileBytes && file.LastWriteTimeUtc >= cutoffUtc)
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .ToList();
            }
            catch (Exception ex)
            {
                log.Debug($"Could not enumerate app log directory {logDir}: {ex.Message}");
                return Array.Empty<FileInfo>();
            }
        }

        private static string? GetLogDirectory()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            var fileAppender = hierarchy.Root.Appenders.OfType<FileAppender>().FirstOrDefault();
            if (fileAppender?.File != null)
                return Path.GetDirectoryName(Path.GetFullPath(fileAppender.File));
            return null;
        }
    }
}
