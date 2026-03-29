using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// Collects the local log4net application log files for the feedback system.
    /// </summary>
    public class AppLogCollector : IFeedbackLogCollector
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AppLogCollector));

        public string Name => "Application Logs";
        public int Order => 0;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            var results = new List<(string, string)>();

            string? logDir = GetLogDirectory();
            if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir))
                return results;

            foreach (var file in Directory.GetFiles(logDir, "*", SearchOption.TopDirectoryOnly).Take(20))
            {
                try
                {
                    string tempCopy = Path.Combine(Path.GetTempPath(), $"logcopy_{Path.GetFileName(file)}");
                    File.Copy(file, tempCopy, true);
                    results.Add(($"AppLogs/{Path.GetFileName(file)}", tempCopy));
                }
                catch (Exception ex)
                {
                    log.Debug($"Could not collect app log file {file}: {ex.Message}");
                }
            }

            return results;
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
