using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;
using System.IO;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// Collects the local log4net application log files for the feedback system.
    /// </summary>
    public class AppLogCollector : IFeedbackLogCollector
    {
        private const int MaxFiles = 20;
        private const long MaxFileBytes = 50L * 1024 * 1024;
        private static readonly ILog log = LogManager.GetLogger(typeof(AppLogCollector));

        public string Name => "Application Logs";
        public string Description => "ColorVision UI runtime logs";
        public int Order => 0;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            var results = new List<(string, string)>();

            string? logDir = GetLogDirectory();
            if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir))
                return results;

            foreach (var file in GetRecentLogFiles(logDir))
            {
                try
                {
                    string tempCopy = Path.Combine(Path.GetTempPath(), $"ColorVision_AppLog_{Guid.NewGuid():N}_{file.Name}");
                    file.CopyTo(tempCopy, true);
                    results.Add(($"AppLogs/{file.Name}", tempCopy));
                }
                catch (Exception ex)
                {
                    log.Debug($"Could not collect app log file {file.FullName}: {ex.Message}");
                }
            }

            return results;
        }

        private static IReadOnlyList<FileInfo> GetRecentLogFiles(string logDir)
        {
            try
            {
                return new DirectoryInfo(logDir)
                    .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                    .Where(file => file.Length <= MaxFileBytes)
                    .OrderByDescending(file => file.LastWriteTimeUtc)
                    .Take(MaxFiles)
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
                return Path.GetDirectoryName(fileAppender.File);
            return null;
        }
    }
}
