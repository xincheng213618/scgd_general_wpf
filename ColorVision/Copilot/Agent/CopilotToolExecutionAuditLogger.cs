using ColorVision.Copilot.Mcp;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    internal sealed class CopilotToolExecutionAuditEntry
    {
        public string CallId { get; init; } = string.Empty;

        public CopilotExecutionSourceKind SourceKind { get; init; }

        public CopilotExecutionAuthorizationChannel AuthorizationChannel { get; init; }

        public string ScopeId { get; init; } = string.Empty;

        public string AuthorizationScopeId { get; init; } = string.Empty;

        public string OperationBindingId { get; init; } = string.Empty;

        public string TraceId { get; init; } = string.Empty;

        public string SessionIdentity { get; init; } = string.Empty;

        public string CallerIdentity { get; init; } = string.Empty;

        public string ConversationId { get; init; } = string.Empty;

        public string TaskId { get; init; } = string.Empty;

        public string RunId { get; init; } = string.Empty;

        public string ParentRunId { get; init; } = string.Empty;

        public string WorkspaceIdentity { get; init; } = string.Empty;

        public string WorkspaceSnapshotId { get; init; } = string.Empty;

        public long CapabilityRevision { get; init; }

        public string ScopeToolName { get; init; } = string.Empty;

        public string ProviderCallId { get; init; } = string.Empty;

        public string ArgumentsSignature { get; init; } = string.Empty;

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

        public IReadOnlyList<CopilotToolExecutionHookRun> HookRuns { get; init; } =
            Array.Empty<CopilotToolExecutionHookRun>();

        public string HookSummary { get; init; } = string.Empty;

        public string ArgumentSummary { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;
    }

    internal static class CopilotToolExecutionAuditLogger
    {
        private const int MaxEntries = 200;
        private const int MaxHookRuns = (CopilotToolExecutionHookRegistry.MaxRegistrations + 1) * 3;
        private const int MaxHookSummaryCharacters = 4_000;
        private static readonly ILog Log = LogManager.GetLogger("ColorVision.Copilot.AgentToolAudit");
        private static readonly object SyncRoot = new();
        private static readonly List<CopilotToolExecutionAuditEntry> RecentEntries = new();

        public static void Record(CopilotToolExecutionOutcome outcome)
        {
            ArgumentNullException.ThrowIfNull(outcome);
            var execution = outcome.Execution;
            var executionScope = ResolveExecutionScope(outcome);
            var hookRuns = (outcome.HookRuns ?? Array.Empty<CopilotToolExecutionHookRun>())
                .Where(item => item?.IsStructurallyValid() == true)
                .Take(MaxHookRuns)
                .ToArray();
            var entry = new CopilotToolExecutionAuditEntry
            {
                CallId = Sanitize(execution.CallId),
                SourceKind = executionScope.SourceKind,
                AuthorizationChannel = executionScope.AuthorizationChannel,
                ScopeId = ProjectScopeValue(executionScope.ScopeId),
                AuthorizationScopeId = ProjectScopeValue(executionScope.AuthorizationScopeId),
                OperationBindingId = ProjectScopeValue(executionScope.OperationBindingId),
                TraceId = ProjectScopeValue(executionScope.TraceId),
                SessionIdentity = ProjectScopeValue(executionScope.SessionIdentity),
                CallerIdentity = ProjectScopeValue(executionScope.CallerIdentity),
                ConversationId = ProjectScopeValue(executionScope.ConversationId),
                TaskId = ProjectScopeValue(executionScope.TaskId),
                RunId = ProjectScopeValue(executionScope.RunId),
                ParentRunId = ProjectScopeValue(executionScope.ParentRunId),
                WorkspaceIdentity = ProjectScopeValue(executionScope.WorkspaceIdentity),
                WorkspaceSnapshotId = ProjectScopeValue(executionScope.WorkspaceSnapshotId),
                CapabilityRevision = executionScope.CapabilityRevision,
                ScopeToolName = ProjectScopeValue(executionScope.ToolName),
                ProviderCallId = ProjectScopeValue(executionScope.ProviderCallId),
                ArgumentsSignature = ProjectArgumentsSignature(executionScope.ArgumentsSignature),
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
                HookRuns = hookRuns,
                HookSummary = CreateHookSummary(hookRuns),
                ArgumentSummary = execution.ArgumentSummary,
                ErrorMessage = outcome.Result.Success ? string.Empty : Sanitize(CopilotMcpAuditLogger.RedactText(outcome.Result.ErrorMessage)),
            };

            lock (SyncRoot)
            {
                RecentEntries.Add(entry);
                if (RecentEntries.Count > MaxEntries)
                    RecentEntries.RemoveRange(0, RecentEntries.Count - MaxEntries);
            }

            Log.Info($"Agent tool completed. CallId={entry.CallId} ProviderCallId={EmptyLabel(entry.ProviderCallId)} Runtime={entry.RuntimeName} Round={entry.Round} Attempt={entry.Attempt}/{entry.MaxAttempts} Tool={entry.ToolName} ScopeTool={EmptyLabel(entry.ScopeToolName)} Source={entry.SourceKind} AuthorizationChannel={entry.AuthorizationChannel} ScopeId={EmptyLabel(entry.ScopeId)} AuthorizationScopeId={EmptyLabel(entry.AuthorizationScopeId)} OperationBindingId={EmptyLabel(entry.OperationBindingId)} TraceId={EmptyLabel(entry.TraceId)} Session={EmptyLabel(entry.SessionIdentity)} Caller={EmptyLabel(entry.CallerIdentity)} Conversation={EmptyLabel(entry.ConversationId)} Task={EmptyLabel(entry.TaskId)} Run={EmptyLabel(entry.RunId)} ParentRun={EmptyLabel(entry.ParentRunId)} Workspace={EmptyLabel(entry.WorkspaceIdentity)} WorkspaceSnapshot={EmptyLabel(entry.WorkspaceSnapshotId)} CapabilityRevision={entry.CapabilityRevision} ArgumentsSignature={EmptyLabel(entry.ArgumentsSignature)} Access={entry.Access} Risk={entry.RiskLevel} Approval={entry.ApprovalMode} Idempotency={entry.Idempotency} Concurrency={entry.ConcurrencyMode} ConcurrencyKey={EmptyLabel(entry.ConcurrencyKey)} QueueMs={entry.QueueDurationMs} State={entry.State} FailureKind={entry.FailureKind} FailureCode={EmptyLabel(entry.FailureCode)} RetryEligible={entry.RetryEligible} ApprovalActionId={EmptyLabel(entry.ApprovalActionId)} DurationMs={entry.DurationMs} Hooks={EmptyLabel(entry.HookSummary)} Arguments={entry.ArgumentSummary} Error={EmptyLabel(entry.ErrorMessage)}");
        }

        private static string CreateHookSummary(IReadOnlyList<CopilotToolExecutionHookRun> hookRuns)
        {
            if (hookRuns.Count == 0)
                return string.Empty;

            var builder = new StringBuilder();
            foreach (var hookRun in hookRuns)
            {
                var mode = hookRun.ExecutionMode == CopilotToolExecutionHookMode.Async
                    ? "@async"
                    : string.Empty;
                var item = $"{FormatHookPhase(hookRun.Phase)}:{hookRun.SourceId}{mode}={FormatHookState(hookRun.State)}/{hookRun.DurationMs}ms";
                if (!string.IsNullOrWhiteSpace(hookRun.FailureCode))
                    item += "/" + hookRun.FailureCode;
                if (builder.Length > 0)
                    item = "," + item;
                if (builder.Length + item.Length > MaxHookSummaryCharacters)
                {
                    builder.Append(",...");
                    break;
                }
                builder.Append(item);
            }
            return builder.ToString();
        }

        private static string FormatHookPhase(CopilotToolExecutionHookPhase phase) => phase switch
        {
            CopilotToolExecutionHookPhase.PermissionRequest => "permission",
            CopilotToolExecutionHookPhase.BeforeExecute => "before",
            CopilotToolExecutionHookPhase.AfterExecute => "after",
            _ => "unknown",
        };

        private static string FormatHookState(CopilotToolExecutionHookState state) => state switch
        {
            CopilotToolExecutionHookState.Scheduled => "scheduled",
            CopilotToolExecutionHookState.Completed => "completed",
            CopilotToolExecutionHookState.Denied => "denied",
            CopilotToolExecutionHookState.Failed => "failed",
            CopilotToolExecutionHookState.TimedOut => "timed_out",
            CopilotToolExecutionHookState.Cancelled => "cancelled",
            CopilotToolExecutionHookState.Skipped => "skipped",
            _ => "unknown",
        };

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
            if (!string.IsNullOrWhiteSpace(input.Cursor))
                parts.Add("cursor=" + input.Cursor.Trim());
            if (input.StartLine.HasValue)
                parts.Add("startLine=" + input.StartLine.Value);
            if (input.StartColumn.HasValue)
                parts.Add("startColumn=" + input.StartColumn.Value);
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
            if (!string.IsNullOrWhiteSpace(input.Cursor))
                names.Add("cursor");
            if (input.StartLine.HasValue)
                names.Add("startLine");
            if (input.StartColumn.HasValue)
                names.Add("startColumn");
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
                || string.Equals(name, "cursor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "startLine", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "startColumn", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "endLine", StringComparison.OrdinalIgnoreCase);
        }

        private static CopilotExecutionScope ResolveExecutionScope(CopilotToolExecutionOutcome outcome)
        {
            var invocation = outcome.Invocation;
            if (invocation == null)
                return CopilotExecutionScope.Empty;
            if (!invocation.ExecutionScope.IsEmpty)
                return invocation.ExecutionScope;
            if (invocation.AgentRequest == null)
                return CopilotExecutionScope.Empty;

            var executionScope = CopilotExecutionScope.ForAgentRequest(invocation.AgentRequest);
            try
            {
                return executionScope.BindToolCall(
                    outcome.Execution.ToolName,
                    outcome.Execution.CallId,
                    CopilotAgentToolInputExactBinding.CreateExecutionSignature(
                        outcome.Execution.ToolName,
                        invocation.ToolInput));
            }
            catch
            {
                return executionScope;
            }
        }

        private static string Sanitize(string? value)
        {
            var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 800 ? text : text[..800] + "...";
        }

        private static string ProjectScopeValue(string? value)
            => Sanitize(CopilotMcpAuditLogger.RedactText(value));

        private static string ProjectArgumentsSignature(string? value)
        {
            var signature = ProjectScopeValue(value);
            if (signature.Length == 0)
                return string.Empty;
            if (signature.Length == 64 && signature.All(Uri.IsHexDigit))
                return signature.ToLowerInvariant();

            return "sha256:" + Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(signature))).ToLowerInvariant();
        }

        private static string EmptyLabel(string? value) => string.IsNullOrWhiteSpace(value) ? "(none)" : value;
    }
}
