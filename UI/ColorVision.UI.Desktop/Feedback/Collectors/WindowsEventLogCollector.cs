using System.Diagnostics;
using System.IO;
using System.Text;

namespace ColorVision.UI.Desktop.Feedback.Collectors
{
    /// <summary>
    /// Adds recent Windows Application and System warnings/errors to feedback packages.
    /// </summary>
    public sealed class WindowsEventLogCollector : IFeedbackLogCollector
    {
        private const int MaxEntriesPerLog = 200;

        public string Name => "Windows Event Logs";

        public string Description => "Recent Application and System warnings/errors";

        public int Order => 40;

        public IEnumerable<(string EntryPath, string FilePath)> CollectFiles()
        {
            var results = new List<(string, string)>();
            foreach (string logName in new[] { "Application", "System" })
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
                if (entries.Count == 0) return null;

                var output = new StringBuilder();
                output.AppendLine($"=== Windows {logName} Event Log ===");
                output.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                output.AppendLine($"Entries: {entries.Count}");
                output.AppendLine();

                foreach (EventLogEntry entry in entries)
                {
                    output.AppendLine($"[{entry.TimeGenerated:yyyy-MM-dd HH:mm:ss}] {entry.EntryType}");
                    output.AppendLine($"Source: {entry.Source}");
                    output.AppendLine($"EventId: {entry.InstanceId}");
                    output.AppendLine($"Category: {entry.Category}");
                    output.AppendLine("Message:");
                    output.AppendLine(entry.Message);
                    output.AppendLine(new string('-', 80));
                }

                string tempPath = Path.Combine(Path.GetTempPath(), $"ColorVision_WindowsEvent_{logName}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.txt");
                File.WriteAllText(tempPath, output.ToString(), Encoding.UTF8);
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
