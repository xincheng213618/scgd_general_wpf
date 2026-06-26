using ColorVision.UI;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace EventVWR
{
    /// <summary>
    /// Collects recent Windows Application/System warning and error events for feedback packages.
    /// </summary>
    public class EventLogFeedbackCollector : IFeedbackLogCollector
    {
        private const int MaxEntriesPerLog = 200;

        public string Name => "Windows Event Logs";
        public string Description => "Recent Application and System warnings/errors";
        public int Order => 40;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            var results = new List<(string, string)>();
            foreach (var logName in new[] { "Application", "System" })
            {
                string? tempPath = CollectEventLog(logName);
                if (!string.IsNullOrEmpty(tempPath))
                    results.Add(($"WindowsEvents/{logName}.txt", tempPath));
            }
            return results;
        }

        private static string? CollectEventLog(string logName)
        {
            try
            {
                using var eventLog = new EventLog(logName);
                var entries = eventLog.Entries.Cast<EventLogEntry>()
                    .Where(entry => entry.EntryType is EventLogEntryType.Error or EventLogEntryType.Warning)
                    .OrderByDescending(entry => entry.TimeGenerated)
                    .Take(MaxEntriesPerLog)
                    .ToList();

                if (entries.Count == 0)
                    return null;

                var sb = new StringBuilder();
                sb.AppendLine($"=== Windows {logName} Event Log ===");
                sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"Entries: {entries.Count}");
                sb.AppendLine();

                foreach (var entry in entries)
                {
                    sb.AppendLine($"[{entry.TimeGenerated:yyyy-MM-dd HH:mm:ss}] {entry.EntryType}");
                    sb.AppendLine($"Source: {entry.Source}");
                    sb.AppendLine($"EventId: {entry.InstanceId}");
                    sb.AppendLine($"Category: {entry.Category}");
                    sb.AppendLine("Message:");
                    sb.AppendLine(entry.Message);
                    sb.AppendLine(new string('-', 80));
                }

                string tempPath = Path.Combine(Path.GetTempPath(), $"ColorVision_WindowsEvent_{logName}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.txt");
                File.WriteAllText(tempPath, sb.ToString(), Encoding.UTF8);
                return tempPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not collect Windows {logName} event log: {ex.Message}");
                return null;
            }
        }
    }
}
