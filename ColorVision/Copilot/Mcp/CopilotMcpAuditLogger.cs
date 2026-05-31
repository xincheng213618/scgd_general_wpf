using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace ColorVision.Copilot.Mcp
{
    public sealed class CopilotMcpAuditEntry
    {
        public DateTimeOffset TimestampUtc { get; init; }

        public string ToolName { get; init; } = string.Empty;

        public string ArgumentSummary { get; init; } = string.Empty;

        public bool Success { get; init; }

        public long DurationMs { get; init; }

        public string ErrorMessage { get; init; } = string.Empty;

        public string CallerSource { get; init; } = string.Empty;
    }

    public static class CopilotMcpAuditLogger
    {
        private const int MaxEntries = 200;
        private static readonly ILog Log = LogManager.GetLogger("ColorVision.Copilot.McpAudit");
        private static readonly object SyncRoot = new();
        private static readonly List<CopilotMcpAuditEntry> RecentEntries = new();
        private static readonly AsyncLocal<CopilotMcpAuditScope?> CurrentScope = new();
        private static readonly Regex SensitiveInlineRegex = new(
            "(?<name>password|passwd|pwd|secret|token|api[_-]?key|access[_-]?key|private[_-]?key|authorization|bearer)\\s*[:=]\\s*(?<value>[^,;\\s]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex BearerRegex = new(
            "Bearer\\s+[^,;\\s]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static void ToolCallStarted(string toolName, string argumentSummary, string? callerSource = null)
        {
            var scope = new CopilotMcpAuditScope
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                ToolName = Sanitize(toolName),
                ArgumentSummary = Sanitize(Redact(argumentSummary)),
                CallerSource = Sanitize(callerSource),
            };

            CurrentScope.Value = scope;
            Log.Info($"MCP tool call started. TimestampUtc={scope.TimestampUtc:O} Tool={scope.ToolName} Arguments={scope.ArgumentSummary} Caller={EmptyLabel(scope.CallerSource)}");
        }

        public static void ToolCallCompleted(string toolName, bool success, TimeSpan elapsed, string message)
        {
            var scope = CurrentScope.Value;
            var entry = new CopilotMcpAuditEntry
            {
                TimestampUtc = scope?.TimestampUtc ?? DateTimeOffset.UtcNow,
                ToolName = Sanitize(scope?.ToolName ?? toolName),
                ArgumentSummary = Sanitize(scope?.ArgumentSummary),
                CallerSource = Sanitize(scope?.CallerSource),
                Success = success,
                DurationMs = (long)elapsed.TotalMilliseconds,
                ErrorMessage = success ? string.Empty : Sanitize(message),
            };

            lock (SyncRoot)
            {
                RecentEntries.Add(entry);
                if (RecentEntries.Count > MaxEntries)
                    RecentEntries.RemoveRange(0, RecentEntries.Count - MaxEntries);
            }

            CurrentScope.Value = null;
            Log.Info($"MCP tool call completed. TimestampUtc={DateTimeOffset.UtcNow:O} Tool={entry.ToolName} Arguments={entry.ArgumentSummary} Success={entry.Success} DurationMs={entry.DurationMs} Error={EmptyLabel(entry.ErrorMessage)} Caller={EmptyLabel(entry.CallerSource)}");
        }

        public static IReadOnlyList<CopilotMcpAuditEntry> GetRecentEntries(int maxEntries)
        {
            var count = Math.Clamp(maxEntries, 1, MaxEntries);
            lock (SyncRoot)
            {
                return RecentEntries
                    .Skip(Math.Max(0, RecentEntries.Count - count))
                    .ToArray();
            }
        }

        public static CopilotMcpAuditEntry? GetLastError()
        {
            lock (SyncRoot)
            {
                return RecentEntries.LastOrDefault(entry => !entry.Success);
            }
        }

        public static void ClearForTests()
        {
            lock (SyncRoot)
            {
                RecentEntries.Clear();
            }

            CurrentScope.Value = null;
        }

        public static string RedactArgument(string key, string? value)
        {
            if (IsSensitiveKey(key))
                return "<redacted>";

            return Redact(value);
        }

        private static string Redact(string? value)
        {
            var text = value ?? string.Empty;
            text = BearerRegex.Replace(text, "Bearer <redacted>");
            return SensitiveInlineRegex.Replace(text, match => $"{match.Groups["name"].Value}=<redacted>");
        }

        private static bool IsSensitiveKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            return key.Contains("password", StringComparison.OrdinalIgnoreCase)
                || key.Contains("passwd", StringComparison.OrdinalIgnoreCase)
                || key.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || key.Contains("token", StringComparison.OrdinalIgnoreCase)
                || key.Contains("authorization", StringComparison.OrdinalIgnoreCase)
                || key.Contains("api_key", StringComparison.OrdinalIgnoreCase)
                || key.Contains("apikey", StringComparison.OrdinalIgnoreCase)
                || key.Contains("access_key", StringComparison.OrdinalIgnoreCase)
                || key.Contains("private_key", StringComparison.OrdinalIgnoreCase);
        }

        private static string Sanitize(string? value)
        {
            var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 800 ? text : text[..800] + "...";
        }

        private static string EmptyLabel(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();
        }

        private sealed class CopilotMcpAuditScope
        {
            public DateTimeOffset TimestampUtc { get; init; }

            public string ToolName { get; init; } = string.Empty;

            public string ArgumentSummary { get; init; } = string.Empty;

            public string CallerSource { get; init; } = string.Empty;
        }
    }
}