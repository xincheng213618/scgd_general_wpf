using System.Diagnostics;
using System.IO;
using System.Text;
using ColorVision.UI.LogImp.Models;

namespace ColorVision.UI.LogImp
{
    public static class LogHistoryReader
    {
        public static string ReadDisplayText(TextReader reader, LogLoadState logLoadState, bool reverse, int maxChars)
        {
            return ReadDisplayText(
                reader,
                logLoadState,
                reverse,
                maxChars,
                DateTime.Today,
                Process.GetCurrentProcess().StartTime);
        }

        public static string ReadDisplayText(
            TextReader reader,
            LogLoadState logLoadState,
            bool reverse,
            int maxChars,
            DateTime today,
            DateTime startupTime)
        {
            var entries = ReadEntries(reader, logLoadState, reverse, maxChars, today, startupTime);
            if (entries.Count == 0)
            {
                return string.Empty;
            }

            return TrimDisplayText(string.Join(Environment.NewLine, entries.Select(entry => entry.Text)), reverse, maxChars);
        }

        public static IReadOnlyList<LogEntry> ReadEntries(TextReader reader, LogLoadState logLoadState, bool reverse, int maxChars)
        {
            return ReadEntries(
                reader,
                logLoadState,
                reverse,
                maxChars,
                DateTime.Today,
                Process.GetCurrentProcess().StartTime);
        }

        public static IReadOnlyList<LogEntry> ReadEntries(
            TextReader reader,
            LogLoadState logLoadState,
            bool reverse,
            int maxChars,
            DateTime today,
            DateTime startupTime)
        {
            ArgumentNullException.ThrowIfNull(reader);

            if (logLoadState == LogLoadState.None)
            {
                return Array.Empty<LogEntry>();
            }

            var matchingEntries = new Queue<LogEntry>();
            var totalChars = 0;
            StringBuilder? currentEntry = null;
            bool currentEntryIncluded = false;

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (LogEntryParser.TryParseLogTimestamp(line, out DateTime logTime))
                {
                    if (currentEntryIncluded && currentEntry != null)
                    {
                        AddEntry(matchingEntries, ref totalChars, currentEntry.ToString(), maxChars);
                    }

                    currentEntryIncluded = ShouldIncludeLogEntry(logLoadState, today, startupTime, logTime);
                    currentEntry = currentEntryIncluded ? new StringBuilder(line) : null;
                    continue;
                }

                if (currentEntryIncluded && currentEntry != null)
                {
                    currentEntry.AppendLine();
                    currentEntry.Append(line);
                }
            }

            if (currentEntryIncluded && currentEntry != null)
            {
                AddEntry(matchingEntries, ref totalChars, currentEntry.ToString(), maxChars);
            }

            if (matchingEntries.Count == 0)
            {
                return Array.Empty<LogEntry>();
            }

            var entries = matchingEntries.ToArray();
            if (reverse)
            {
                Array.Reverse(entries);
            }

            return entries;
        }

        private static void AddEntry(Queue<LogEntry> entries, ref int totalChars, string entryText, int maxChars)
        {
            var entry = LogEntryParser.FromText(entryText);
            entries.Enqueue(entry);
            totalChars += entry.Text.Length;
            TrimOldEntries(entries, ref totalChars, maxChars);
        }

        private static void TrimOldEntries(Queue<LogEntry> entries, ref int totalChars, int maxChars)
        {
            if (!HasMaxCharLimit(maxChars))
            {
                return;
            }

            while (entries.Count > 1 && GetJoinedLength(totalChars, entries.Count) > maxChars)
            {
                totalChars -= entries.Dequeue().Text.Length;
            }
        }

        private static int GetJoinedLength(int totalChars, int entryCount)
        {
            if (entryCount == 0)
            {
                return 0;
            }

            return totalChars + (Environment.NewLine.Length * (entryCount - 1));
        }

        private static string TrimDisplayText(string displayText, bool reverse, int maxChars)
        {
            if (!HasMaxCharLimit(maxChars) || displayText.Length <= maxChars)
            {
                return displayText;
            }

            return reverse ? displayText[..maxChars] : displayText[^maxChars..];
        }

        private static bool HasMaxCharLimit(int maxChars)
        {
            return maxChars > LogConstants.MinMaxCharsForTrimming;
        }

        private static bool ShouldIncludeLogEntry(LogLoadState logLoadState, DateTime today, DateTime startupTime, DateTime logTime)
        {
            if (logLoadState == LogLoadState.AllToday)
            {
                return logTime.Date == today;
            }

            if (logLoadState == LogLoadState.SinceStartup)
            {
                return logTime >= startupTime;
            }

            return true;
        }
    }
}
