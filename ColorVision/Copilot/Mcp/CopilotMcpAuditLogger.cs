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

        public string ActionId { get; init; } = string.Empty;
    }

    public static class CopilotMcpAuditLogger
    {
        private const int MaxEntries = 200;
        private static readonly ILog Log = LogManager.GetLogger("ColorVision.Copilot.McpAudit");
        private static readonly object SyncRoot = new();
        private static readonly List<CopilotMcpAuditEntry> RecentEntries = new();
        private static readonly AsyncLocal<CopilotMcpAuditScope?> CurrentScope = new();
        private static readonly Regex SensitiveInlineRegex = new(
            "(?<name>[\"']?(?:password|passwd|pwd|secret|token|api[_-]?key|apikey|access[_-]?key|private[_-]?key|authorization|bearer)[\"']?\\s*[:=]\\s*)[\"']?[^,;\\s\"'}]+[\"']?",
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

        public static void AuthenticationFailed(string? callerSource, string reason)
        {
            var entry = new CopilotMcpAuditEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                ToolName = "authentication",
                ArgumentSummary = "{}",
                CallerSource = Sanitize(callerSource),
                Success = false,
                DurationMs = 0,
                ErrorMessage = Sanitize(reason),
            };

            lock (SyncRoot)
            {
                RecentEntries.Add(entry);
                if (RecentEntries.Count > MaxEntries)
                    RecentEntries.RemoveRange(0, RecentEntries.Count - MaxEntries);
            }

            Log.Warn($"MCP authentication failed. TimestampUtc={entry.TimestampUtc:O} Reason={entry.ErrorMessage} Caller={EmptyLabel(entry.CallerSource)}");
        }

        public static void ActionCreated(ConfirmableAction action) => RecordActionEvent("action_created", action, true, "Created pending confirmable action.");

        public static void ActionApproved(ConfirmableAction action) => RecordActionEvent("action_approved", action, true, "Approved by ColorVision user.");

        public static void ActionRejected(ConfirmableAction action) => RecordActionEvent("action_rejected", action, false, "Rejected by ColorVision user.");

        public static void ActionExpired(ConfirmableAction action) => RecordActionEvent("action_expired", action, false, "The confirmable action expired.");

        public static void ActionExecuted(ConfirmableAction action, bool success, string message) => RecordActionEvent("action_executed", action, success, message);

        private static void RecordActionEvent(string eventName, ConfirmableAction action, bool success, string message)
        {
            var entry = new CopilotMcpAuditEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                ToolName = Sanitize(eventName),
                ArgumentSummary = Sanitize(Redact(action.ArgumentsSummary)),
                Success = success,
                DurationMs = 0,
                ErrorMessage = success ? string.Empty : Sanitize(message),
                CallerSource = "colorvision-ui",
                ActionId = Sanitize(action.ActionId),
            };

            lock (SyncRoot)
            {
                RecentEntries.Add(entry);
                if (RecentEntries.Count > MaxEntries)
                    RecentEntries.RemoveRange(0, RecentEntries.Count - MaxEntries);
            }

            Log.Info($"MCP action event. TimestampUtc={entry.TimestampUtc:O} Event={entry.ToolName} ActionId={entry.ActionId} Tool={action.ToolName} Success={entry.Success} Message={EmptyLabel(entry.ErrorMessage)}");
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
                return RecentEntries.LastOrDefault(IsRealFailureEntry);
            }
        }

        public static bool IsRealFailureEntry(CopilotMcpAuditEntry entry)
        {
            return !entry.Success && !IsApprovalFlowEntry(entry);
        }

        public static bool IsApprovalFlowEntry(CopilotMcpAuditEntry entry)
        {
            if (entry.Success)
                return false;

            var toolName = entry.ToolName ?? string.Empty;
            if (string.Equals(toolName, "action_rejected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "action_expired", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var error = entry.ErrorMessage ?? string.Empty;
            return error.Contains("confirmation_required", StringComparison.OrdinalIgnoreCase)
                || error.Contains("pending_user_confirmation", StringComparison.OrdinalIgnoreCase)
                || error.Contains("risk_level: confirmation-required", StringComparison.OrdinalIgnoreCase)
                || error.Contains("risk_level=confirmation-required", StringComparison.OrdinalIgnoreCase)
                || error.Contains("action_pending", StringComparison.OrdinalIgnoreCase)
                || error.Contains("action_not_approved", StringComparison.OrdinalIgnoreCase)
                || error.Contains("action_rejected", StringComparison.OrdinalIgnoreCase)
                || error.Contains("action_expired", StringComparison.OrdinalIgnoreCase);
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

        public static string RedactText(string? value) => Redact(value);

        private static string Redact(string? value)
        {
            var text = value ?? string.Empty;
            text = BearerRegex.Replace(text, "Bearer <redacted>");
            return SensitiveInlineRegex.Replace(text, match => $"{match.Groups["name"].Value}<redacted>");
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
