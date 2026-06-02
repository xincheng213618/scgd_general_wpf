using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

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
            ArgumentNullException.ThrowIfNull(reader);

            if (logLoadState == LogLoadState.None)
            {
                return string.Empty;
            }

            var matchingEntries = new Queue<string>();
            var totalChars = 0;
            StringBuilder? currentEntry = null;
            bool currentEntryIncluded = false;

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (TryParseLogTimestamp(line, out DateTime logTime))
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
                return string.Empty;
            }

            var entries = matchingEntries.ToArray();
            if (reverse)
            {
                Array.Reverse(entries);
            }

            return TrimDisplayText(string.Join(Environment.NewLine, entries), reverse, maxChars);
        }

        private static void AddEntry(Queue<string> entries, ref int totalChars, string entry, int maxChars)
        {
            entries.Enqueue(entry);
            totalChars += entry.Length;
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

        private static bool TryParseLogTimestamp(string line, out DateTime logTime)
        {
            logTime = default;
            if (string.IsNullOrWhiteSpace(line) || line.Length < LogConstants.LogTimestampLength)
            {
                return false;
            }

            return DateTime.TryParseExact(
                line.AsSpan(0, LogConstants.LogTimestampLength),
                LogConstants.LogTimestampFormat,
                null,
                DateTimeStyles.None,
                out logTime);
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
