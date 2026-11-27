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
            string? entryStart = null;
            StringBuilder? multilineEntry = null;

            foreach (var line in lines)
            {
                if (IsTimestampedLogLine(line))
                {
                    if (entryStart != null)
                    {
                        entries.Add(FromText(multilineEntry?.ToString() ?? entryStart));
                        multilineEntry = null;
                    }
                    entryStart = line;
                    continue;
                }

                if (entryStart == null)
                {
                    entries.Add(FromText(line));
                    continue;
                }

                multilineEntry ??= new StringBuilder(entryStart);
                multilineEntry.AppendLine();
                multilineEntry.Append(line);
            }

            if (entryStart != null)
            {
                entries.Add(FromText(multilineEntry?.ToString() ?? entryStart));
            }
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
            if (line == null || line.Length < LogConstants.LogTimestampLength)
            {
                return false;
            }

            ReadOnlySpan<char> timestamp = line.AsSpan(0, LogConstants.LogTimestampLength);
            DateTimeFormatInfo format = DateTimeFormatInfo.CurrentInfo;
            // The rolling log uses a fixed ASCII layout. Keep culture-specific calendars and separators on the general parser.
            if (format.Calendar.GetType() == typeof(GregorianCalendar) && format.TimeSeparator == ":"
                && TryParseGregorianTimestamp(timestamp, out logTime))
                return true;

            return DateTime.TryParseExact(timestamp, LogConstants.LogTimestampFormat, format, DateTimeStyles.None, out logTime);
        }

        private static bool TryParseGregorianTimestamp(ReadOnlySpan<char> timestamp, out DateTime logTime)
        {
            logTime = default;
            if (timestamp[4] != '-' || timestamp[7] != '-' || timestamp[10] != ' '
                || timestamp[13] != ':' || timestamp[16] != ':' || timestamp[19] != ',')
                return false;

            int year = ReadDigits(timestamp[..4]);
            int month = ReadDigits(timestamp.Slice(5, 2));
            int day = ReadDigits(timestamp.Slice(8, 2));
            int hour = ReadDigits(timestamp.Slice(11, 2));
            int minute = ReadDigits(timestamp.Slice(14, 2));
            int second = ReadDigits(timestamp.Slice(17, 2));
            int millisecond = ReadDigits(timestamp.Slice(20, 3));
            if (year <= 0 || (uint)(month - 1) >= 12 || day <= 0 || day > DateTime.DaysInMonth(year, month)
                || (uint)hour >= 24 || (uint)minute >= 60 || (uint)second >= 60 || millisecond < 0)
                return false;

            logTime = new DateTime(year, month, day, hour, minute, second, millisecond);
            return true;
        }

        private static int ReadDigits(ReadOnlySpan<char> digits)
        {
            int value = 0;
            foreach (char digit in digits)
            {
                int number = digit - '0';
                if ((uint)number > 9) return -1;
                value = value * 10 + number;
            }
            return value;
        }

        private static bool IsTimestampedLogLine(string line)
        {
            return TryParseLogTimestamp(line, out _);
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
