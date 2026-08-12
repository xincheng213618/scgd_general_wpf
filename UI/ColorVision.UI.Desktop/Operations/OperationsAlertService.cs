using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ColorVision.UI.Desktop.Operations
{
    public sealed class OperationsAlert
    {
        public string AlertId { get; init; } = string.Empty;
        public string Severity { get; init; } = string.Empty;
        public string Source { get; init; } = "application-log";
        public string Summary { get; init; } = string.Empty;
        public DateTimeOffset OccurredAt { get; init; }
    }

    public sealed class OperationsLogCategoryCount
    {
        public string Category { get; init; } = string.Empty;
        public int Count { get; init; }
    }

    public sealed class OperationsLogDigest
    {
        public bool Available { get; init; }
        public int ScannedLineCount { get; init; }
        public int ParsedEventCount { get; init; }
        public int InfoCount { get; init; }
        public int WarningCount { get; init; }
        public int ErrorCount { get; init; }
        public int CriticalCount { get; init; }
        public bool TailWasBounded { get; init; }
        public DateTimeOffset? LogLastUpdatedAt { get; init; }
        public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.UtcNow;
        public IReadOnlyList<OperationsLogCategoryCount> Categories { get; init; } = [];
        public IReadOnlyList<OperationsAlert> RecentEvents { get; init; } = [];
        public string PrivacyNotice { get; init; } = "仅返回有界聚合与脱敏事件；不返回日志文件名、路径、完整日志或凭据。";
    }

    public sealed partial class OperationsAlertService
    {
        private const int MaxReadBytes = 256 * 1024;
        private const int MaxCandidateLines = 500;
        private readonly string _logDirectory;

        public OperationsAlertService(string? logDirectory = null)
        {
            _logDirectory = logDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
        }

        public IReadOnlyList<OperationsAlert> GetRecent(int count = 50)
        {
            int boundedCount = Math.Clamp(count, 1, 100);
            try
            {
                LogReadResult result = ReadLatestLog();
                return result.Available ? BuildAlerts(result, boundedCount) : [];
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                return [];
            }
        }

        public OperationsLogDigest GetDigest(int eventCount = 12)
        {
            int boundedEventCount = Math.Clamp(eventCount, 1, 30);
            try
            {
                LogReadResult result = ReadLatestLog();
                if (!result.Available)
                    return new OperationsLogDigest();

                IReadOnlyList<OperationsLogCategoryCount> categories = result.Entries
                    .GroupBy(entry => entry.Category, StringComparer.Ordinal)
                    .Select(group => new OperationsLogCategoryCount { Category = group.Key, Count = group.Count() })
                    .OrderByDescending(item => item.Count)
                    .ThenBy(item => item.Category, StringComparer.Ordinal)
                    .Take(6)
                    .ToArray();
                return new OperationsLogDigest
                {
                    Available = true,
                    ScannedLineCount = result.Lines.Count,
                    ParsedEventCount = result.Entries.Count,
                    InfoCount = result.Entries.Count(entry => entry.Severity == "info"),
                    WarningCount = result.Entries.Count(entry => entry.Severity == "warning"),
                    ErrorCount = result.Entries.Count(entry => entry.Severity == "error"),
                    CriticalCount = result.Entries.Count(entry => entry.Severity == "critical"),
                    TailWasBounded = result.TailWasBounded,
                    LogLastUpdatedAt = result.FallbackTime,
                    Categories = categories,
                    RecentEvents = BuildAlerts(result, boundedEventCount),
                };
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                return new OperationsLogDigest();
            }
        }

        private LogReadResult ReadLatestLog()
        {
            FileInfo? latest = new DirectoryInfo(_logDirectory).EnumerateFiles("*.txt", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault();
            if (latest == null)
                return LogReadResult.Missing;

            List<string> lines = ReadTailLines(latest.FullName);
            List<ParsedLogEntry> entries = [];
            foreach (string line in lines)
            {
                ParsedLogEntry? entry = ParseEntry(line);
                if (entry != null)
                    entries.Add(entry);
            }
            return new LogReadResult(
                true,
                lines,
                entries,
                new DateTimeOffset(latest.LastWriteTimeUtc, TimeSpan.Zero),
                latest.Length > MaxReadBytes || lines.Count == MaxCandidateLines);
        }

        private static IReadOnlyList<OperationsAlert> BuildAlerts(LogReadResult result, int count)
        {
            HashSet<string> seen = new(StringComparer.Ordinal);
            List<OperationsAlert> alerts = [];
            for (int index = result.Entries.Count - 1; index >= 0; index--)
            {
                ParsedLogEntry entry = result.Entries[index];
                if (entry.Severity is not ("warning" or "error" or "critical"))
                    continue;
                string summary = Redact(entry.Message);
                if (summary.Length == 0)
                    continue;
                string alertId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(entry.RawLine)))[..24].ToLowerInvariant();
                if (!seen.Add(alertId))
                    continue;
                alerts.Add(new OperationsAlert
                {
                    AlertId = alertId,
                    Severity = entry.Severity,
                    Source = entry.Category,
                    Summary = summary,
                    OccurredAt = entry.OccurredAt ?? result.FallbackTime,
                });
                if (alerts.Count == count)
                    break;
            }
            return alerts;
        }

        private static List<string> ReadTailLines(string path)
        {
            using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            long start = Math.Max(0, stream.Length - MaxReadBytes);
            stream.Seek(start, SeekOrigin.Begin);
            using StreamReader reader = new(stream, Encoding.UTF8, true, leaveOpen: false);
            if (start > 0)
                reader.ReadLine();
            Queue<string> lines = new(MaxCandidateLines);
            while (reader.ReadLine() is string line)
            {
                if (lines.Count == MaxCandidateLines)
                    lines.Dequeue();
                lines.Enqueue(line);
            }
            return lines.ToList();
        }

        private static ParsedLogEntry? ParseEntry(string line)
        {
            Match match = LogEntryRegex().Match(line);
            if (!match.Success)
                return null;
            string severity = match.Groups["level"].Value.ToUpperInvariant() switch
            {
                "FATAL" => "critical",
                "ERROR" => "error",
                "WARN" => "warning",
                _ => "info",
            };
            string source = match.Groups["source"].Value.Trim();
            return new ParsedLogEntry(
                line,
                severity,
                NormalizeSource(source),
                match.Groups["message"].Value.Trim(),
                ParseTimestamp(match.Groups["timestamp"].Value));
        }

        private static string NormalizeSource(string value)
        {
            string source = value.ToLowerInvariant();
            if (source.Contains("operations") || source.Contains("lanremote"))
                return "安全运维";
            if (source.Contains("mqtt") || source.Contains("broker"))
                return "消息服务";
            if (source.Contains("camera") || source.Contains("image") || source.Contains("device"))
                return "设备与图像";
            if (source.Contains("flow") || source.Contains("template"))
                return "流程";
            if (source.Contains("update") || source.Contains("download") || source.Contains("marketplace"))
                return "更新与下载";
            if (source.Contains("copilot") || source.Contains("mcp"))
                return "Copilot";
            if (source.Contains("service"))
                return "服务";
            return "应用";
        }

        private static string Redact(string value)
        {
            string redacted = SecretAssignmentRegex().Replace(value, "$1=[redacted]");
            redacted = SecretQueryRegex().Replace(redacted, "$1[redacted]");
            redacted = UrlRegex().Replace(redacted, "<url>");
            redacted = WindowsPathRegex().Replace(redacted, "<file-path>");
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
                redacted = redacted.Replace(userProfile, "<user-profile>", StringComparison.OrdinalIgnoreCase);
            redacted = UserProfileRemainderRegex().Replace(redacted, "<file-path>");
            redacted = EmailRegex().Replace(redacted, "<email>");
            redacted = GuidRegex().Replace(redacted, "<identifier>");
            redacted = LongHexRegex().Replace(redacted, "<identifier>");
            redacted = Ipv4Regex().Replace(redacted, "<ip-address>");
            return redacted.Length <= 300 ? redacted : redacted[..300];
        }

        private static DateTimeOffset? ParseTimestamp(string text)
        {
            if (!DateTime.TryParseExact(text, "yyyy-MM-dd HH:mm:ss,fff",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timestamp))
                return null;
            return new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Local));
        }

        private sealed record ParsedLogEntry(
            string RawLine,
            string Severity,
            string Category,
            string Message,
            DateTimeOffset? OccurredAt);

        private sealed record LogReadResult(
            bool Available,
            IReadOnlyList<string> Lines,
            IReadOnlyList<ParsedLogEntry> Entries,
            DateTimeOffset FallbackTime,
            bool TailWasBounded)
        {
            public static LogReadResult Missing { get; } = new(false, [], [], DateTimeOffset.UtcNow, false);
        }

        [GeneratedRegex(@"^(?<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},\d{3})\s+\[[^\]]*\]\s+(?<level>FATAL|ERROR|WARN|INFO|DEBUG)\s+(?<source>.+?)\s+-\s+(?<message>.*)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex LogEntryRegex();

        [GeneratedRegex(@"(?i)\b(token|password|secret|authorization|api[-_]?key)\s*[:=]\s*[^\s,;]+")]
        private static partial Regex SecretAssignmentRegex();

        [GeneratedRegex(@"(?i)([?&](?:token|access_token|api[-_]?key|signature)=)[^&\s]+")]
        private static partial Regex SecretQueryRegex();

        [GeneratedRegex(@"(?i)(?:[a-z]:\\|\\\\)[^\r\n,;]+")]
        private static partial Regex WindowsPathRegex();

        [GeneratedRegex(@"(?i)<user-profile>\\[^\s""'<>|]+")]
        private static partial Regex UserProfileRemainderRegex();

        [GeneratedRegex(@"(?i)\bhttps?://[^\s""'<>]+")]
        private static partial Regex UrlRegex();

        [GeneratedRegex(@"(?i)\b[a-z0-9._%+-]+@[a-z0-9.-]+\.[a-z]{2,}\b")]
        private static partial Regex EmailRegex();

        [GeneratedRegex(@"(?i)\b[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}\b")]
        private static partial Regex GuidRegex();

        [GeneratedRegex(@"(?i)\b[0-9a-f]{24,}\b")]
        private static partial Regex LongHexRegex();

        [GeneratedRegex(@"(?<![\d.])(?:\d{1,3}\.){3}\d{1,3}(?::\d{1,5})?")]
        private static partial Regex Ipv4Regex();
    }
}
