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
            var entries = ReadEntryTexts(reader, logLoadState, reverse, maxChars, today, startupTime);
            if (entries.Length == 0)
            {
                return string.Empty;
            }

            return TrimDisplayText(string.Join(Environment.NewLine, entries), reverse, maxChars);
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
            string[] texts = ReadEntryTexts(reader, logLoadState, reverse, maxChars, today, startupTime);
            if (texts.Length == 0) return Array.Empty<LogEntry>();

            var entries = new LogEntry[texts.Length];
            for (int i = 0; i < entries.Length; i++)
                entries[i] = LogEntryParser.FromText(texts[i]);
            return entries;
        }

        private static string[] ReadEntryTexts(
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
                return Array.Empty<string>();
            }

            var matchingEntries = new Queue<string>();
            var totalChars = 0;
            StringBuilder? currentEntry = null;
            string? currentLine = null;

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (LogEntryParser.TryParseLogTimestamp(line, out DateTime logTime))
                {
                    if (currentLine != null)
                    {
                        AddEntry(matchingEntries, ref totalChars, currentEntry?.ToString() ?? currentLine, maxChars);
                        currentEntry = null;
                    }

                    currentLine = ShouldIncludeLogEntry(logLoadState, today, startupTime, logTime) ? line : null;
                    continue;
                }

                if (currentLine != null)
                {
                    currentEntry ??= new StringBuilder(currentLine);
                    currentEntry.AppendLine();
                    currentEntry.Append(line);
                }
            }

            if (currentLine != null)
            {
                AddEntry(matchingEntries, ref totalChars, currentEntry?.ToString() ?? currentLine, maxChars);
            }

            if (matchingEntries.Count == 0)
            {
                return Array.Empty<string>();
            }

            var entries = matchingEntries.ToArray();
            if (reverse)
            {
                Array.Reverse(entries);
            }

            return entries;
        }

        private static void AddEntry(Queue<string> entries, ref int totalChars, string entryText, int maxChars)
        {
            int length = entryText.AsSpan().TrimEnd("\r\n").Length;
            entries.Enqueue(length == entryText.Length ? entryText : entryText[..length]);
            totalChars += length;
            TrimOldEntries(entries, ref totalChars, maxChars);
        }

        private static void TrimOldEntries(Queue<string> entries, ref int totalChars, int maxChars)
        {
            if (!HasMaxCharLimit(maxChars))
            {
                return;
            }

            while (entries.Count > 1 && GetJoinedLength(totalChars, entries.Count) > maxChars)
            {
                totalChars -= entries.Dequeue().Length;
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
