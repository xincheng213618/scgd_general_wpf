using ColorVision.UI.LogImp.Models;
using System.Globalization;
using System.Text;

namespace ColorVision.UI.LogImp
{
    public static class LogEntryParser
    {
        private static readonly char[] NewLineChars = { '\r', '\n' };

        private static readonly (string Token, LogEntryLevel Level)[] LevelTokens =
        {
            ("FATAL", LogEntryLevel.Fatal),
            ("ERROR", LogEntryLevel.Error),
            ("WARNING", LogEntryLevel.Warning),
            ("WARN", LogEntryLevel.Warning),
            ("DEBUG", LogEntryLevel.Debug),
            ("TRACE", LogEntryLevel.Trace),
            ("INFO", LogEntryLevel.Info)
        };

        public static LogEntry FromText(string text)
        {
            return new LogEntry(text, DetectLevel(text));
        }

        public static IReadOnlyList<LogEntry> FromRenderedMessage(string renderedMessage)
        {
            if (string.IsNullOrEmpty(renderedMessage))
            {
                return Array.Empty<LogEntry>();
            }

            return new[] { FromText(renderedMessage) };
        }

        public static List<LogEntry> FromLines(IEnumerable<string> lines)
        {
            ArgumentNullException.ThrowIfNull(lines);

            var entries = new List<LogEntry>();
            StringBuilder? currentEntry = null;

            foreach (var line in lines)
            {
                if (IsTimestampedLogLine(line))
                {
                    FlushCurrentEntry(entries, ref currentEntry);
                    currentEntry = new StringBuilder(line);
                    continue;
                }

                if (currentEntry == null)
                {
                    entries.Add(FromText(line));
                    continue;
                }

                currentEntry.AppendLine();
                currentEntry.Append(line);
            }

            FlushCurrentEntry(entries, ref currentEntry);
            return entries;
        }

        public static LogEntryLevel DetectLevel(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return LogEntryLevel.Unknown;
            }

            var firstLineLength = text.IndexOfAny(NewLineChars);
            var firstLine = firstLineLength >= 0 ? text.AsSpan(0, firstLineLength) : text.AsSpan();

            foreach (var (token, level) in LevelTokens)
            {
                if (ContainsToken(firstLine, token))
                {
                    return level;
                }
            }

            return LogEntryLevel.Unknown;
        }

        public static bool TryParseLogTimestamp(string line, out DateTime logTime)
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

        private static bool IsTimestampedLogLine(string line)
        {
            return TryParseLogTimestamp(line, out _);
        }

        private static void FlushCurrentEntry(List<LogEntry> entries, ref StringBuilder? currentEntry)
        {
            if (currentEntry == null)
            {
                return;
            }

            entries.Add(FromText(currentEntry.ToString()));
            currentEntry = null;
        }

        private static bool ContainsToken(ReadOnlySpan<char> text, string token)
        {
            var startIndex = 0;
            while (startIndex < text.Length)
            {
                var index = text[startIndex..].IndexOf(token.AsSpan(), StringComparison.OrdinalIgnoreCase);
                if (index < 0)
                {
                    return false;
                }

                var absoluteIndex = startIndex + index;
                if (HasTokenBoundary(text, absoluteIndex, token.Length))
                {
                    return true;
                }

                startIndex = absoluteIndex + token.Length;
            }

            return false;
        }

        private static bool HasTokenBoundary(ReadOnlySpan<char> text, int index, int length)
        {
            var leftBoundary = index == 0 || IsTokenBoundary(text[index - 1]);
            var rightIndex = index + length;
            var rightBoundary = rightIndex >= text.Length || IsTokenBoundary(text[rightIndex]);
            return leftBoundary && rightBoundary;
        }

        private static bool IsTokenBoundary(char value)
        {
            return !char.IsLetterOrDigit(value) && value != '_';
        }
    }
}
