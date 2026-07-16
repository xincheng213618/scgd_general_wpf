using ColorVision.Copilot.Mcp;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Copilot
{
    public sealed class CopilotToolExecutionAuditEntry
    {
        public string CallId { get; init; } = string.Empty;

        public int Round { get; init; }

        public int Attempt { get; init; }

        public int MaxAttempts { get; init; }

        public string RuntimeName { get; init; } = string.Empty;

        public string ToolName { get; init; } = string.Empty;

        public CopilotToolAccess Access { get; init; }

        public CopilotToolRiskLevel RiskLevel { get; init; }

        public CopilotToolApprovalMode ApprovalMode { get; init; }

        public CopilotToolIdempotency Idempotency { get; init; }

        public CopilotToolConcurrencyMode ConcurrencyMode { get; init; }

        public string ConcurrencyKey { get; init; } = string.Empty;

        public string ApprovalActionId { get; init; } = string.Empty;

        public CopilotToolExecutionState State { get; init; }

        public CopilotToolFailureKind FailureKind { get; init; }

        public string FailureCode { get; init; } = string.Empty;

        public bool RetryEligible { get; init; }

        public DateTimeOffset StartedAtUtc { get; init; }

        public DateTimeOffset? CompletedAtUtc { get; init; }

        public long DurationMs { get; init; }

        public long QueueDurationMs { get; init; }

        public string ArgumentSummary { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;
    }

    public static class CopilotToolExecutionAuditLogger
    {
        private const int MaxEntries = 200;
        private static readonly ILog Log = LogManager.GetLogger("ColorVision.Copilot.AgentToolAudit");
        private static readonly object SyncRoot = new();
        private static readonly List<CopilotToolExecutionAuditEntry> RecentEntries = new();

        public static void Record(CopilotToolExecutionOutcome outcome)
        {
            ArgumentNullException.ThrowIfNull(outcome);
            var execution = outcome.Execution;
            var entry = new CopilotToolExecutionAuditEntry
            {
                CallId = Sanitize(execution.CallId),
                Round = execution.Round,
                Attempt = execution.Attempt,
                MaxAttempts = execution.MaxAttempts,
                RuntimeName = Sanitize(execution.RuntimeName),
                ToolName = Sanitize(execution.ToolName),
                Access = execution.Access,
                RiskLevel = execution.RiskLevel,
                ApprovalMode = execution.ApprovalMode,
                Idempotency = execution.Idempotency,
                ConcurrencyMode = execution.ConcurrencyMode,
                ConcurrencyKey = Sanitize(execution.ConcurrencyKey),
                ApprovalActionId = Sanitize(execution.ApprovalActionId),
                State = execution.State,
                FailureKind = execution.FailureKind,
                FailureCode = outcome.Result.Success ? string.Empty : CopilotToolFailureCode.Normalize(outcome.Result.FailureCode),
                RetryEligible = execution.RetryEligible,
                StartedAtUtc = execution.StartedAtUtc,
                CompletedAtUtc = execution.CompletedAtUtc,
                DurationMs = execution.DurationMs,
                QueueDurationMs = execution.QueueDurationMs,
                ArgumentSummary = execution.ArgumentSummary,
                ErrorMessage = outcome.Result.Success ? string.Empty : Sanitize(CopilotMcpAuditLogger.RedactText(outcome.Result.ErrorMessage)),
            };

            lock (SyncRoot)
            {
                RecentEntries.Add(entry);
                if (RecentEntries.Count > MaxEntries)
                    RecentEntries.RemoveRange(0, RecentEntries.Count - MaxEntries);
            }

            Log.Info($"Agent tool completed. CallId={entry.CallId} Runtime={entry.RuntimeName} Round={entry.Round} Attempt={entry.Attempt}/{entry.MaxAttempts} Tool={entry.ToolName} Access={entry.Access} Risk={entry.RiskLevel} Approval={entry.ApprovalMode} Idempotency={entry.Idempotency} Concurrency={entry.ConcurrencyMode} ConcurrencyKey={EmptyLabel(entry.ConcurrencyKey)} QueueMs={entry.QueueDurationMs} State={entry.State} FailureKind={entry.FailureKind} FailureCode={EmptyLabel(entry.FailureCode)} RetryEligible={entry.RetryEligible} ApprovalActionId={EmptyLabel(entry.ApprovalActionId)} DurationMs={entry.DurationMs} Arguments={entry.ArgumentSummary} Error={EmptyLabel(entry.ErrorMessage)}");
        }

        public static IReadOnlyList<CopilotToolExecutionAuditEntry> GetRecentEntries(int maxEntries = 50)
        {
            var count = Math.Clamp(maxEntries, 1, MaxEntries);
            lock (SyncRoot)
                return RecentEntries.Skip(Math.Max(0, RecentEntries.Count - count)).ToArray();
        }

        public static void ClearForTests()
        {
            lock (SyncRoot)
                RecentEntries.Clear();
        }

        public static string CreateArgumentSummary(CopilotAgentToolInput input)
            => CreateRedactedArgumentSummary(input);

        public static string CreateArgumentSummary(ICopilotTool tool, CopilotAgentToolInput input)
        {
            ArgumentNullException.ThrowIfNull(tool);
            return tool.Capability.AuditArgumentMode == CopilotToolAuditArgumentMode.NamesOnly
                ? CreateArgumentNames(input)
                : CreateRedactedArgumentSummary(input);
        }

        private static string CreateRedactedArgumentSummary(CopilotAgentToolInput input)
        {
            input ??= CopilotAgentToolInput.Empty;
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(input.Query))
                parts.Add("query=" + input.Query.Trim());
            if (!string.IsNullOrWhiteSpace(input.Path))
                parts.Add("path=" + input.Path.Trim());
            if (input.StartLine.HasValue)
                parts.Add("startLine=" + input.StartLine.Value);
            if (input.EndLine.HasValue)
                parts.Add("endLine=" + input.EndLine.Value);

            foreach (var pair in input.Arguments
                .Where(pair => !IsStandardArgumentName(pair.Key))
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                string value;
                try
                {
                    value = pair.Value is string text ? text : JsonSerializer.Serialize(pair.Value);
                }
                catch
                {
                    value = "<unserializable>";
                }
                parts.Add(pair.Key + "=" + CopilotMcpAuditLogger.RedactArgument(pair.Key, value));
            }

            var summary = parts.Count == 0 ? "(none)" : string.Join("; ", parts);
            return Sanitize(CopilotMcpAuditLogger.RedactText(summary));
        }

        private static string CreateArgumentNames(CopilotAgentToolInput input)
        {
            input ??= CopilotAgentToolInput.Empty;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(input.Query))
                names.Add("query");
            if (!string.IsNullOrWhiteSpace(input.Path))
                names.Add("path");
            if (input.StartLine.HasValue)
                names.Add("startLine");
            if (input.EndLine.HasValue)
                names.Add("endLine");
            foreach (var name in input.Arguments.Keys.Where(name => !string.IsNullOrWhiteSpace(name)))
                names.Add(name.Trim());

            return names.Count == 0
                ? "(none)"
                : Sanitize("fields=" + string.Join(",", names.OrderBy(name => name, StringComparer.OrdinalIgnoreCase)));
        }

        private static bool IsStandardArgumentName(string name)
        {
            return string.Equals(name, "query", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "path", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "startLine", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "endLine", StringComparison.OrdinalIgnoreCase);
        }

        private static string Sanitize(string? value)
        {
            var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 800 ? text : text[..800] + "...";
        }

        private static string EmptyLabel(string? value) => string.IsNullOrWhiteSpace(value) ? "(none)" : value;
    }
}
