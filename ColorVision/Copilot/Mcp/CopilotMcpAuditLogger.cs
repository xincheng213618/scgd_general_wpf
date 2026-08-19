using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace ColorVision.Copilot.Mcp
{
    internal sealed class CopilotMcpAuditEntry
    {
        public DateTimeOffset TimestampUtc { get; init; }

        public string ToolName { get; init; } = string.Empty;

        public string ArgumentSummary { get; init; } = string.Empty;

        public bool Success { get; init; }

        public long DurationMs { get; init; }

        public string ErrorMessage { get; init; } = string.Empty;

        public string CallerSource { get; init; } = string.Empty;

        public string ActionId { get; init; } = string.Empty;

        public string ConversationId { get; init; } = string.Empty;

        public string TaskId { get; init; } = string.Empty;

        public string WorkspaceIdentity { get; init; } = string.Empty;

        public string SessionIdentity { get; init; } = string.Empty;

        public string RunId { get; init; } = string.Empty;

        public string ParentRunId { get; init; } = string.Empty;

        public string TraceId { get; init; } = string.Empty;

        public string ScopeId { get; init; } = string.Empty;

        public string ProviderCallId { get; init; } = string.Empty;

        public string ArgumentsSignature { get; init; } = string.Empty;

        public string ApprovalDecisionSource { get; init; } = string.Empty;

        public string ApprovalDecisionReason { get; init; } = string.Empty;
    }

    internal static class CopilotMcpAuditLogger
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
            "(?i:\\bBearer)[ \\t]+[A-Za-z0-9._~+/-]{16,}=*",
            RegexOptions.Compiled);

        private static readonly Regex OpenAiApiKeyRegex = new(
            "sk-[A-Za-z0-9]{20,}",
            RegexOptions.Compiled);

        private static readonly Regex AwsAccessKeyIdRegex = new(
            "\\bAKIA[0-9A-Z]{16}\\b",
            RegexOptions.Compiled);

        public static void ToolCallStarted(string toolName, string argumentSummary, string? callerSource = null)
        {
            ToolCallStarted(
                toolName,
                argumentSummary,
                CopilotExecutionScope.ForInProcess(callerSource ?? string.Empty));
        }

        public static void ToolCallStarted(
            string toolName,
            string argumentSummary,
            CopilotExecutionScope executionScope)
        {
            executionScope ??= CopilotExecutionScope.Empty;
            var scope = new CopilotMcpAuditScope
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                ToolName = Sanitize(toolName),
                ArgumentSummary = SanitizeRedacted(argumentSummary),
                ExecutionScope = executionScope,
            };

            CurrentScope.Value = scope;
            CopilotNonAuthoritativeDiagnosticBoundary.TryWrite(() =>
                Log.Info($"MCP tool call started. TimestampUtc={scope.TimestampUtc:O} Tool={scope.ToolName} Arguments={scope.ArgumentSummary} Caller={EmptyLabel(executionScope.CallerIdentity)} ScopeId={executionScope.ScopeId}"));
        }

        public static void ToolCallCompleted(string toolName, bool success, TimeSpan elapsed, string failureCode)
        {
            var scope = CurrentScope.Value;
            var entry = new CopilotMcpAuditEntry
            {
                TimestampUtc = scope?.TimestampUtc ?? DateTimeOffset.UtcNow,
                ToolName = Sanitize(scope?.ToolName ?? toolName),
                ArgumentSummary = Sanitize(scope?.ArgumentSummary),
                Success = success,
                DurationMs = (long)elapsed.TotalMilliseconds,
                ErrorMessage = success ? string.Empty : SanitizeRedacted(failureCode),
            };
            entry = WithExecutionScope(entry, scope?.ExecutionScope);

            lock (SyncRoot)
            {
                RecentEntries.Add(entry);
                if (RecentEntries.Count > MaxEntries)
                    RecentEntries.RemoveRange(0, RecentEntries.Count - MaxEntries);
            }

            CurrentScope.Value = null;
            CopilotNonAuthoritativeDiagnosticBoundary.TryWrite(() =>
                Log.Info($"MCP tool call completed. TimestampUtc={DateTimeOffset.UtcNow:O} Tool={entry.ToolName} Arguments={entry.ArgumentSummary} Success={entry.Success} DurationMs={entry.DurationMs} Error={EmptyLabel(entry.ErrorMessage)} Caller={EmptyLabel(entry.CallerSource)} ScopeId={EmptyLabel(entry.ScopeId)}"));
        }

        public static void AuthenticationFailed(string? callerSource, string reason)
        {
            var entry = new CopilotMcpAuditEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                ToolName = "authentication",
                ArgumentSummary = "{}",
                CallerSource = SanitizeRedacted(callerSource),
                Success = false,
                DurationMs = 0,
                ErrorMessage = SanitizeRedacted(reason),
            };

            lock (SyncRoot)
            {
                RecentEntries.Add(entry);
                if (RecentEntries.Count > MaxEntries)
                    RecentEntries.RemoveRange(0, RecentEntries.Count - MaxEntries);
            }

            CopilotNonAuthoritativeDiagnosticBoundary.TryWrite(() =>
                Log.Warn($"MCP authentication failed. TimestampUtc={entry.TimestampUtc:O} Reason={entry.ErrorMessage} Caller={EmptyLabel(entry.CallerSource)}"));
        }

        public static void ActionCreated(ConfirmableAction action) => RecordActionEvent("action_created", action, true, "Created pending confirmable action.");

        public static void ActionApproved(ConfirmableAction action) => RecordActionEvent(
            "action_approved",
            action,
            true,
            string.Equals(action.ApprovalDecisionSource, "automatic-review", StringComparison.Ordinal)
                ? "Approved by the automatic permission reviewer."
                : "Approved by the ColorVision user.");

        public static void ActionRejected(ConfirmableAction action) => RecordActionEvent(
            "action_rejected",
            action,
            false,
            string.Equals(action.ApprovalDecisionSource, "automatic-review-unavailable", StringComparison.Ordinal)
                ? "automatic_review_unavailable"
                : string.Equals(action.ApprovalDecisionSource, "automatic-review", StringComparison.Ordinal)
                    ? "automatic_review_denied"
                    : "action_rejected");

        public static void ActionCancelled(ConfirmableAction action) => RecordActionEvent("action_cancelled", action, false, "action_cancelled");

        public static void ActionExpired(ConfirmableAction action) => RecordActionEvent("action_expired", action, false, "action_expired");

        public static void ActionExecuted(ConfirmableAction action, bool success, string failureCode) => RecordActionEvent("action_executed", action, success, failureCode);

        private static void RecordActionEvent(string eventName, ConfirmableAction action, bool success, string failureCode)
        {
            var executionScope = action.RequestContext.ResolveExecutionScope();
            var entry = WithExecutionScope(new CopilotMcpAuditEntry
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                ToolName = Sanitize(eventName),
                ArgumentSummary = ProjectDetailPresence(action.ArgumentsSummary),
                Success = success,
                DurationMs = 0,
                ErrorMessage = success ? string.Empty : SanitizeRedacted(failureCode),
                CallerSource = SanitizeRedacted(string.IsNullOrWhiteSpace(action.RequestContext.RequestSource)
                    ? "colorvision-ui"
                    : action.RequestContext.RequestSource),
                ActionId = SanitizeRedacted(action.ActionId),
                ConversationId = SanitizeRedacted(action.RequestContext.ConversationId),
                TaskId = SanitizeRedacted(action.RequestContext.TaskId),
                ApprovalDecisionSource = SanitizeRedacted(action.ApprovalDecisionSource),
                ApprovalDecisionReason = ProjectDetailPresence(action.ApprovalDecisionReason),
            }, executionScope);

            lock (SyncRoot)
            {
                RecentEntries.Add(entry);
                if (RecentEntries.Count > MaxEntries)
                    RecentEntries.RemoveRange(0, RecentEntries.Count - MaxEntries);
            }

            CopilotNonAuthoritativeDiagnosticBoundary.TryWrite(() =>
                Log.Info($"MCP action event. TimestampUtc={entry.TimestampUtc:O} Event={entry.ToolName} ActionId={entry.ActionId} Tool={action.ToolName} Conversation={EmptyLabel(entry.ConversationId)} Task={EmptyLabel(entry.TaskId)} Run={EmptyLabel(entry.RunId)} WorkspaceIdentity={EmptyLabel(entry.WorkspaceIdentity)} Caller={EmptyLabel(entry.CallerSource)} ScopeId={EmptyLabel(entry.ScopeId)} ApprovalSource={EmptyLabel(entry.ApprovalDecisionSource)} ApprovalReason={EmptyLabel(entry.ApprovalDecisionReason)} Success={entry.Success} Message={EmptyLabel(entry.ErrorMessage)}"));
        }

        private static CopilotMcpAuditEntry WithExecutionScope(
            CopilotMcpAuditEntry entry,
            CopilotExecutionScope? executionScope)
        {
            executionScope ??= CopilotExecutionScope.Empty;
            return new CopilotMcpAuditEntry
            {
                TimestampUtc = entry.TimestampUtc,
                ToolName = entry.ToolName,
                ArgumentSummary = entry.ArgumentSummary,
                Success = entry.Success,
                DurationMs = entry.DurationMs,
                ErrorMessage = entry.ErrorMessage,
                CallerSource = SanitizeRedacted(FirstNonEmpty(entry.CallerSource, executionScope.CallerIdentity)),
                ActionId = entry.ActionId,
                ConversationId = SanitizeRedacted(FirstNonEmpty(entry.ConversationId, executionScope.ConversationId)),
                TaskId = SanitizeRedacted(FirstNonEmpty(entry.TaskId, executionScope.TaskId)),
                WorkspaceIdentity = SanitizeRedacted(FirstNonEmpty(entry.WorkspaceIdentity, executionScope.WorkspaceIdentity)),
                SessionIdentity = SanitizeRedacted(executionScope.SessionIdentity),
                RunId = SanitizeRedacted(executionScope.RunId),
                ParentRunId = SanitizeRedacted(executionScope.ParentRunId),
                TraceId = SanitizeRedacted(executionScope.TraceId),
                ScopeId = SanitizeRedacted(executionScope.ScopeId),
                ProviderCallId = SanitizeRedacted(executionScope.ProviderCallId),
                ArgumentsSignature = SanitizeRedacted(executionScope.ArgumentsSignature),
                ApprovalDecisionSource = SanitizeRedacted(entry.ApprovalDecisionSource),
                ApprovalDecisionReason = SanitizeRedacted(entry.ApprovalDecisionReason),
            };
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
                || string.Equals(toolName, "action_cancelled", StringComparison.OrdinalIgnoreCase)
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
                || error.Contains("action_cancelled", StringComparison.OrdinalIgnoreCase)
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

        internal static bool IsSensitiveArgumentName(string? name) => IsSensitiveKey(name);

        private static string Redact(string? value)
        {
            var text = value ?? string.Empty;
            text = BearerRegex.Replace(text, "Bearer <redacted>");
            text = OpenAiApiKeyRegex.Replace(text, "<redacted>");
            text = AwsAccessKeyIdRegex.Replace(text, "<redacted>");
            return SensitiveInlineRegex.Replace(text, match => $"{match.Groups["name"].Value}<redacted>");
        }

        private static bool IsSensitiveKey(string? key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return false;

            var normalized = key.Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Replace(" ", string.Empty, StringComparison.Ordinal);
            return normalized.Contains("password", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("passwd", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("pwd", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("token", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("authorization", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("bearer", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("apikey", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("accesskey", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("privatekey", StringComparison.OrdinalIgnoreCase);
        }

        private static string Sanitize(string? value)
        {
            var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 800 ? text : text[..800] + "...";
        }

        private static string SanitizeRedacted(string? value) => Sanitize(Redact(value));

        private static string ProjectDetailPresence(string? value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : "details-withheld";

        private static string EmptyLabel(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private sealed class CopilotMcpAuditScope
        {
            public DateTimeOffset TimestampUtc { get; init; }

            public string ToolName { get; init; } = string.Empty;

            public string ArgumentSummary { get; init; } = string.Empty;

            public CopilotExecutionScope ExecutionScope { get; init; } = CopilotExecutionScope.Empty;
        }
    }
}
