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
                FileInfo? latest = new DirectoryInfo(_logDirectory).EnumerateFiles("*.txt", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(file => file.LastWriteTimeUtc).FirstOrDefault();
                if (latest == null)
                    return [];

                DateTimeOffset fallbackTime = new(latest.LastWriteTimeUtc, TimeSpan.Zero);
                HashSet<string> seen = new(StringComparer.Ordinal);
                List<OperationsAlert> alerts = [];
                List<string> lines = ReadTailLines(latest.FullName);
                for (int index = lines.Count - 1; index >= 0; index--)
                {
                    string line = lines[index];
                    string? severity = ResolveSeverity(line);
                    if (severity == null)
                        continue;
                    string summary = Redact(ExtractMessage(line));
                    if (summary.Length == 0)
                        continue;
                    string alertId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(line)))[..24].ToLowerInvariant();
                    if (!seen.Add(alertId))
                        continue;
                    alerts.Add(new OperationsAlert
                    {
                        AlertId = alertId,
                        Severity = severity,
                        Summary = summary,
                        OccurredAt = ParseTimestamp(line) ?? fallbackTime,
                    });
                    if (alerts.Count == boundedCount)
                        break;
                }
                return alerts;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or DirectoryNotFoundException)
            {
                return [];
            }
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

        private static string? ResolveSeverity(string line)
        {
            if (line.Contains(" FATAL ", StringComparison.OrdinalIgnoreCase))
                return "critical";
            if (line.Contains(" ERROR ", StringComparison.OrdinalIgnoreCase))
                return "error";
            if (line.Contains(" WARN ", StringComparison.OrdinalIgnoreCase))
                return "warning";
            return null;
        }

        private static string ExtractMessage(string line)
        {
            int separator = line.IndexOf(" - ", StringComparison.Ordinal);
            string value = separator >= 0 ? line[(separator + 3)..] : line;
            return value.Trim();
        }

        private static string Redact(string value)
        {
            string redacted = SecretAssignmentRegex().Replace(value, "$1=[redacted]");
            redacted = SecretQueryRegex().Replace(redacted, "$1[redacted]");
            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(userProfile))
                redacted = redacted.Replace(userProfile, "<user-profile>", StringComparison.OrdinalIgnoreCase);
            return redacted.Length <= 300 ? redacted : redacted[..300];
        }

        private static DateTimeOffset? ParseTimestamp(string line)
        {
            if (line.Length < 23 || !DateTime.TryParseExact(line[..23], "yyyy-MM-dd HH:mm:ss,fff",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime value))
                return null;
            return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Local));
        }

        [GeneratedRegex(@"(?i)\b(token|password|secret|authorization|api[-_]?key)\s*[:=]\s*[^\s,;]+")]
        private static partial Regex SecretAssignmentRegex();

        [GeneratedRegex(@"(?i)([?&](?:token|access_token|api[-_]?key|signature)=)[^&\s]+")]
        private static partial Regex SecretQueryRegex();
    }
}
