using ColorVision.Common.MVVM;
using ColorVision.Copilot.Mcp;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotAgentTraceEntry : ViewModelBase
    {
        public const int CurrentSchemaVersion = 13;
        private const int MaxSummaryLength = 800;
        private const int MaxDelegatedAnswerLength = 20_000;
        private const int MaxPersistedHookRuns = 64;
        private static readonly TimeSpan MaximumWorkspaceRollbackLifetime = TimeSpan.FromMinutes(31);

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public string CallId { get; set; } = string.Empty;

        public int Round { get; set; }

        public int Attempt { get; set; } = 1;

        public int MaxAttempts { get; set; } = 1;

        public string RuntimeName { get; set; } = string.Empty;

        public string ToolName { get; set; } = string.Empty;

        public CopilotToolAccess Access { get; set; }

        public CopilotToolRiskLevel RiskLevel { get; set; }

        public CopilotToolApprovalMode ApprovalMode { get; set; }

        public CopilotToolIdempotency Idempotency { get; set; }

        public CopilotToolConcurrencyMode ConcurrencyMode { get; set; }

        public string ConcurrencyKey { get; set; } = string.Empty;

        public string ApprovalActionId { get; set; } = string.Empty;

        public CopilotToolExecutionState State { get; set; } = CopilotToolExecutionState.Pending;

        public CopilotToolFailureKind FailureKind { get; set; }

        public string FailureCode { get; set; } = string.Empty;

        public bool RetryEligible { get; set; }

        public DateTimeOffset StartedAtUtc { get; set; }

        public DateTimeOffset? CompletedAtUtc { get; set; }

        public long DurationMs { get; set; }

        public long QueueDurationMs { get; set; }

        public long TimeoutMs { get; set; }

        public List<CopilotToolExecutionHookRun> HookRuns { get; set; } = new();

        public string ProgressMessage { get; set; } = string.Empty;

        public long? ProgressCompleted { get; set; }

        public long? ProgressTotal { get; set; }

        public string ProgressUnit { get; set; } = string.Empty;

        public string ArgumentSummary { get; set; } = string.Empty;

        public string ResultSummary { get; set; } = string.Empty;

        public string ErrorMessage { get; set; } = string.Empty;

        public string DelegatedRunId { get; set; } = string.Empty;

        public string DelegatedResumeFromRunId { get; set; } = string.Empty;

        public string DelegatedRoleId { get; set; } = string.Empty;

        public CopilotAgentStopReason DelegatedStopReason { get; set; }

        public int DelegatedRequestTokenBudget { get; set; }

        public long DelegatedConsumedTokens { get; set; }

        public int DelegatedProviderCalls { get; set; }

        public int DelegatedToolCalls { get; set; }

        public int DelegatedDeliveredSteeringCount { get; set; }

        public int DelegatedUndeliveredSteeringCount { get; set; }

        public int DelegatedRegisteredToolCount { get; set; }

        public int DelegatedAvailableToolCount { get; set; }

        public int DelegatedAvailableToolDefinitionCharacters { get; set; }

        public int DelegatedHarnessInstructionCharacters { get; set; }

        public long DelegatedQueueDurationMs { get; set; }

        public string DelegatedAnswerText { get; set; } = string.Empty;

        public bool DelegatedAnswerHasSuccessfulEvidence { get; set; }

        public bool DelegatedAnswerWasTruncated { get; set; }

        public bool DelegatedRunClosed { get; set; }

        [JsonProperty]
        public string WorkspaceChangeSetId { get; private set; } = string.Empty;

        [JsonProperty]
        public DateTimeOffset? WorkspaceChangeSetExpiresAtUtc { get; private set; }

        public bool WorkspaceChangeSetRolledBack { get; private set; }

        public List<CopilotWorkspaceChangeFile> WorkspaceChangedFiles { get; set; } = new();

        public bool ShouldSerializeCallId() => !string.IsNullOrEmpty(CallId);

        public bool ShouldSerializeAttempt() => Attempt != 1;

        public bool ShouldSerializeMaxAttempts() => MaxAttempts != 1;

        public bool ShouldSerializeRuntimeName() => !string.IsNullOrEmpty(RuntimeName);

        public bool ShouldSerializeToolName() => !string.IsNullOrEmpty(ToolName);

        public bool ShouldSerializeAccess() => Access != CopilotToolAccess.ReadOnly;

        public bool ShouldSerializeRiskLevel() => RiskLevel != CopilotToolRiskLevel.Low;

        public bool ShouldSerializeApprovalMode() => ApprovalMode != CopilotToolApprovalMode.Never;

        public bool ShouldSerializeIdempotency() => Idempotency != CopilotToolIdempotency.Unknown;

        public bool ShouldSerializeConcurrencyMode() => ConcurrencyMode != CopilotToolConcurrencyMode.SharedRead;

        public bool ShouldSerializeConcurrencyKey() => !string.IsNullOrEmpty(ConcurrencyKey);

        public bool ShouldSerializeApprovalActionId() => !string.IsNullOrEmpty(ApprovalActionId);

        public bool ShouldSerializeState() => State != CopilotToolExecutionState.Pending;

        public bool ShouldSerializeFailureKind() => FailureKind != CopilotToolFailureKind.None;

        public bool ShouldSerializeWorkspaceChangeSetRolledBack() => WorkspaceChangeSetRolledBack;

        public bool ShouldSerializeWorkspaceChangeSetId() => !string.IsNullOrWhiteSpace(WorkspaceChangeSetId);

        public bool ShouldSerializeWorkspaceChangeSetExpiresAtUtc() => WorkspaceChangeSetExpiresAtUtc.HasValue;

        public bool ShouldSerializeWorkspaceChangedFiles() => WorkspaceChangedFiles?.Count > 0;

        public bool ShouldSerializeFailureCode() => !string.IsNullOrWhiteSpace(FailureCode);

        public bool ShouldSerializeRetryEligible() => RetryEligible;

        public bool ShouldSerializeStartedAtUtc() => StartedAtUtc != default;

        public bool ShouldSerializeDurationMs() => DurationMs != 0;

        public bool ShouldSerializeQueueDurationMs() => QueueDurationMs != 0;

        public bool ShouldSerializeTimeoutMs() => TimeoutMs != 0;

        public bool ShouldSerializeHookRuns() => HookRuns?.Count > 0;

        public bool ShouldSerializeProgressMessage() => !string.IsNullOrWhiteSpace(ProgressMessage);

        public bool ShouldSerializeProgressCompleted() => ProgressCompleted.HasValue;

        public bool ShouldSerializeProgressTotal() => ProgressTotal.HasValue;

        public bool ShouldSerializeProgressUnit() => !string.IsNullOrWhiteSpace(ProgressUnit);

        public bool ShouldSerializeArgumentSummary() => !string.IsNullOrEmpty(ArgumentSummary);

        public bool ShouldSerializeResultSummary() => !string.IsNullOrEmpty(ResultSummary);

        public bool ShouldSerializeErrorMessage() => !string.IsNullOrEmpty(ErrorMessage);

        public bool ShouldSerializeDelegatedRunId() => !string.IsNullOrEmpty(DelegatedRunId);

        public bool ShouldSerializeDelegatedResumeFromRunId() => !string.IsNullOrEmpty(DelegatedResumeFromRunId);

        public bool ShouldSerializeDelegatedRoleId() => !string.IsNullOrEmpty(DelegatedRoleId);

        public bool ShouldSerializeDelegatedStopReason() => DelegatedStopReason != CopilotAgentStopReason.None;

        public bool ShouldSerializeDelegatedRequestTokenBudget() => DelegatedRequestTokenBudget != 0;

        public bool ShouldSerializeDelegatedConsumedTokens() => DelegatedConsumedTokens != 0;

        public bool ShouldSerializeDelegatedProviderCalls() => DelegatedProviderCalls != 0;

        public bool ShouldSerializeDelegatedToolCalls() => DelegatedToolCalls != 0;

        public bool ShouldSerializeDelegatedDeliveredSteeringCount() => DelegatedDeliveredSteeringCount != 0;

        public bool ShouldSerializeDelegatedUndeliveredSteeringCount() => DelegatedUndeliveredSteeringCount != 0;

        public bool ShouldSerializeDelegatedRegisteredToolCount() => DelegatedRegisteredToolCount != 0;

        public bool ShouldSerializeDelegatedAvailableToolCount() => DelegatedAvailableToolCount != 0;

        public bool ShouldSerializeDelegatedAvailableToolDefinitionCharacters() => DelegatedAvailableToolDefinitionCharacters != 0;

        public bool ShouldSerializeDelegatedHarnessInstructionCharacters() => DelegatedHarnessInstructionCharacters != 0;

        public bool ShouldSerializeDelegatedQueueDurationMs() => DelegatedQueueDurationMs != 0;

        public bool ShouldSerializeDelegatedAnswerText() => !string.IsNullOrWhiteSpace(DelegatedAnswerText);

        public bool ShouldSerializeDelegatedAnswerHasSuccessfulEvidence() => DelegatedAnswerHasSuccessfulEvidence;

        public bool ShouldSerializeDelegatedAnswerWasTruncated() => DelegatedAnswerWasTruncated;

        public bool ShouldSerializeDelegatedRunClosed() => DelegatedRunClosed;

        [JsonIgnore]
        public bool HasWorkspaceChangedFiles => WorkspaceChangedFiles?.Count > 0;

        [JsonIgnore]
        public bool CanRequestWorkspaceRollback => string.Equals(ToolName, "ApplyWorkspacePatchEnvelope", StringComparison.Ordinal)
            && State == CopilotToolExecutionState.Completed
            && !WorkspaceChangeSetRolledBack
            && !string.IsNullOrWhiteSpace(WorkspaceChangeSetId)
            && WorkspaceChangeSetExpiresAtUtc > DateTimeOffset.UtcNow;

        [JsonIgnore]
        internal bool IsCompletedWorkspaceRollback => string.Equals(ToolName, "RollbackWorkspacePatchEnvelope", StringComparison.Ordinal)
            && State == CopilotToolExecutionState.Completed
            && !string.IsNullOrWhiteSpace(WorkspaceChangeSetId);

        [JsonIgnore]
        public bool IsFailure => State is CopilotToolExecutionState.Failed
            or CopilotToolExecutionState.TimedOut
            or CopilotToolExecutionState.Denied
            or CopilotToolExecutionState.Cancelled
            or CopilotToolExecutionState.Interrupted;

        [JsonIgnore]
        public bool IsVisibleInActivity => !IsFailedSearchAttempt();

        [JsonIgnore]
        public string ActivityGlyph => State switch
        {
            CopilotToolExecutionState.Completed => "✓",
            CopilotToolExecutionState.Failed or CopilotToolExecutionState.TimedOut => "!",
            CopilotToolExecutionState.Denied or CopilotToolExecutionState.Cancelled or CopilotToolExecutionState.Interrupted => "×",
            CopilotToolExecutionState.AwaitingApproval => "?",
            _ => "·",
        };

        [JsonIgnore]
        public string ActivityLabel => BuildActivityLabel();

        [JsonIgnore]
        public string ActivityDurationLabel => DurationMs > 0
            && (CompletedAtUtc != null || State is CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running)
                ? FormatDuration(DurationMs)
                : string.Empty;

        [JsonIgnore]
        public string ActivityProgressLabel
        {
            get
            {
                if (State is not (CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running))
                    return string.Empty;

                var count = ProgressCompleted.HasValue && ProgressTotal.HasValue
                    ? $"{ProgressCompleted.Value}/{ProgressTotal.Value}"
                    : ProgressCompleted?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(count) && !string.IsNullOrWhiteSpace(ProgressUnit))
                    count += " " + FormatProgressUnit(ProgressUnit);
                return !string.IsNullOrWhiteSpace(count)
                    ? count
                    : TrimForActivity(ProgressMessage, 48);
            }
        }

        [JsonIgnore]
        public string ActivityDescription
        {
            get
            {
                var text = IsFailure ? BuildFriendlyFailureSummary() : BuildFriendlySuccessSummary();
                return TrimForActivity(text, 180);
            }
        }

        [JsonIgnore]
        public string DiagnosticDetails
        {
            get
            {
                var builder = new StringBuilder();
                builder.Append("[Round ").Append(Math.Max(1, Round)).Append(" · ").Append(ToolName);
                if (Attempt > 1 || RetryEligible)
                    builder.Append(" · Attempt ").Append(Attempt).Append('/').Append(MaxAttempts);
                builder.Append("] ").Append(FormatDiagnosticState(State));
                if (DurationMs > 0
                    && (CompletedAtUtc != null || State is CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running))
                    builder.Append(" · ").Append(FormatDuration(DurationMs));
                if (QueueDurationMs > 0)
                    builder.Append(" · queued ").Append(FormatDuration(QueueDurationMs));
                if (!string.IsNullOrWhiteSpace(ActivityProgressLabel))
                {
                    builder.AppendLine().Append("Progress: ").Append(ActivityProgressLabel);
                    if (!string.IsNullOrWhiteSpace(ProgressMessage))
                        builder.Append(" · ").Append(ProgressMessage);
                }
                if (!string.IsNullOrWhiteSpace(RuntimeName))
                    builder.AppendLine().Append("Runtime: ").Append(RuntimeName)
                        .Append(" · Access: ").Append(Access)
                        .Append(" · Risk: ").Append(RiskLevel)
                        .Append(" · Approval: ").Append(ApprovalMode)
                        .Append(" · Idempotency: ").Append(Idempotency)
                        .Append(" · Concurrency: ").Append(ConcurrencyMode);
                if (!string.IsNullOrWhiteSpace(ConcurrencyKey))
                    builder.AppendLine().Append("Resource: ").Append(ConcurrencyKey);
                if (!string.IsNullOrWhiteSpace(DelegatedRunId))
                {
                    builder.AppendLine().Append("Child run: ").Append(DelegatedRunId);
                    if (!string.IsNullOrWhiteSpace(DelegatedRoleId))
                        builder.Append(" · role: ").Append(DelegatedRoleId);
                    if (!string.IsNullOrWhiteSpace(DelegatedResumeFromRunId))
                        builder.Append(" · resumed from: ").Append(DelegatedResumeFromRunId);
                    if (DelegatedRunClosed)
                        builder.Append(" · closed");
                    builder.Append(" · stop: ").Append(DelegatedStopReason)
                        .Append(" · provider calls: ").Append(DelegatedProviderCalls)
                        .Append(" · tool calls: ").Append(DelegatedToolCalls);
                    builder.AppendLine().Append("Child budget: ").Append(DelegatedConsumedTokens)
                        .Append('/').Append(DelegatedRequestTokenBudget).Append(" tokens");
                    if (DelegatedQueueDurationMs > 0)
                        builder.Append(" · queued ").Append(FormatDuration(DelegatedQueueDurationMs));
                    if (DelegatedRegisteredToolCount > 0
                        || DelegatedAvailableToolCount > 0
                        || DelegatedAvailableToolDefinitionCharacters > 0
                        || DelegatedHarnessInstructionCharacters > 0)
                    {
                        builder.AppendLine().Append("Child prompt surface: ")
                            .Append(DelegatedAvailableToolCount)
                            .Append('/')
                            .Append(DelegatedRegisteredToolCount)
                            .Append(" tools");
                        if (DelegatedAvailableToolDefinitionCharacters > 0)
                            builder.Append(" · definitions ").Append(DelegatedAvailableToolDefinitionCharacters).Append(" chars");
                        if (DelegatedHarnessInstructionCharacters > 0)
                            builder.Append(" · harness ").Append(DelegatedHarnessInstructionCharacters).Append(" chars");
                    }
                }
                if (FailureKind != CopilotToolFailureKind.None)
                {
                    builder.AppendLine().Append("Failure: ").Append(FailureKind);
                    if (!string.IsNullOrWhiteSpace(FailureCode))
                        builder.Append(" · Code: ").Append(FailureCode);
                    builder.Append(" · Retry eligible: ").Append(RetryEligible ? "yes" : "no");
                }
                else if (!string.IsNullOrWhiteSpace(FailureCode))
                {
                    builder.AppendLine().Append("Failure code: ").Append(FailureCode);
                }
                if (HookRuns?.Count > 0)
                {
                    builder.AppendLine().Append("Hooks:");
                    foreach (var hookRun in HookRuns)
                    {
                        builder.AppendLine()
                            .Append("- ")
                            .Append(FormatHookPhase(hookRun.Phase))
                            .Append(' ')
                            .Append(hookRun.SourceId)
                            .Append(" · ")
                            .Append(FormatHookState(hookRun.State))
                            .Append(" · ")
                            .Append(FormatDuration(hookRun.DurationMs));
                        if (!string.IsNullOrWhiteSpace(hookRun.FailureCode))
                            builder.Append(" · ").Append(hookRun.FailureCode);
                    }
                }
                if (!string.IsNullOrWhiteSpace(ApprovalActionId))
                    builder.AppendLine().Append("Approval action: ").Append(ApprovalActionId);
                if (!string.IsNullOrWhiteSpace(ArgumentSummary) && ArgumentSummary != "(none)")
                    builder.AppendLine().Append("Arguments: ").Append(ArgumentSummary);
                if (!string.IsNullOrWhiteSpace(ResultSummary))
                    builder.AppendLine().Append(ResultSummary);
                if (!string.IsNullOrWhiteSpace(ErrorMessage))
                    builder.AppendLine().Append("Error: ").Append(ErrorMessage);
                return builder.ToString().TrimEnd();
            }
        }

        public static CopilotAgentTraceEntry FromStarted(CopilotToolExecutionInfo execution)
        {
            ArgumentNullException.ThrowIfNull(execution);
            return FromExecution(execution);
        }

        public static CopilotAgentTraceEntry FromProgress(
            CopilotToolExecutionInfo execution,
            string? progress,
            CopilotToolProgressUpdate? reportedProgress = null)
        {
            ArgumentNullException.ThrowIfNull(execution);
            var entry = FromExecution(execution);
            entry.ProgressMessage = Sanitize(reportedProgress?.Message);
            entry.ProgressCompleted = NormalizeProgressCount(reportedProgress?.Completed);
            entry.ProgressTotal = NormalizeProgressCount(reportedProgress?.Total);
            if (entry.ProgressCompleted.HasValue && entry.ProgressTotal.HasValue)
                entry.ProgressCompleted = Math.Min(entry.ProgressCompleted.Value, entry.ProgressTotal.Value);
            entry.ProgressUnit = SanitizeProgressUnit(reportedProgress?.Unit);
            if (reportedProgress?.DelegatedRun != null)
            {
                entry.DelegatedRoleId = SanitizeIdentifier(reportedProgress.DelegatedRun.RoleId);
                entry.DelegatedRunId = SanitizeIdentifier(reportedProgress.DelegatedRun.RunId);
                entry.DelegatedResumeFromRunId = SanitizeIdentifier(reportedProgress.DelegatedRun.ResumeFromRunId);
                entry.DelegatedRequestTokenBudget = Math.Max(0, reportedProgress.DelegatedRun.RequestTokenBudget);
                entry.DelegatedQueueDurationMs = Math.Max(0, reportedProgress.DelegatedRun.QueueDurationMs);
                entry.DelegatedConsumedTokens = Math.Max(0, reportedProgress.DelegatedRun.ConsumedTokens);
                entry.DelegatedProviderCalls = Math.Max(0, reportedProgress.DelegatedRun.ProviderCalls);
                entry.DelegatedToolCalls = Math.Max(0, reportedProgress.DelegatedRun.ToolCalls);
            }
            entry.ResultSummary = !string.IsNullOrWhiteSpace(entry.ProgressMessage)
                ? entry.ProgressMessage
                : Sanitize(progress);
            return entry;
        }

        public static CopilotAgentTraceEntry FromResult(
            CopilotToolExecutionInfo execution,
            CopilotToolResult? result,
            IReadOnlyList<CopilotToolExecutionHookRun>? hookRuns = null)
        {
            ArgumentNullException.ThrowIfNull(execution);
            var entry = FromExecution(execution);
            if (hookRuns != null)
            {
                foreach (var hookRun in hookRuns)
                {
                    if (entry.HookRuns.Count >= MaxPersistedHookRuns)
                        break;
                    if (hookRun?.IsStructurallyValid() == true)
                        entry.HookRuns.Add(hookRun);
                }
            }
            if (result != null)
            {
                var summary = !string.IsNullOrWhiteSpace(result.Summary) ? result.Summary : result.Content;
                entry.ResultSummary = Sanitize(summary);
                entry.ErrorMessage = result.Success ? string.Empty : Sanitize(result.ErrorMessage);
                entry.FailureCode = result.Success ? string.Empty : CopilotToolFailureCode.Normalize(result.FailureCode);
                if (result.DelegatedRunUsage != null)
                {
                    entry.DelegatedRoleId = SanitizeIdentifier(result.DelegatedRunUsage.RoleId);
                    entry.DelegatedRunId = SanitizeIdentifier(result.DelegatedRunUsage.RunId);
                    entry.DelegatedResumeFromRunId = SanitizeIdentifier(result.DelegatedRunUsage.ResumeFromRunId);
                    entry.DelegatedStopReason = result.DelegatedRunUsage.StopReason;
                    entry.DelegatedRequestTokenBudget = Math.Max(0, result.DelegatedRunUsage.RequestTokenBudget);
                    entry.DelegatedConsumedTokens = Math.Max(0, result.DelegatedRunUsage.ConsumedTokens);
                    entry.DelegatedProviderCalls = Math.Max(0, result.DelegatedRunUsage.ProviderCalls);
                    entry.DelegatedToolCalls = Math.Max(0, result.DelegatedRunUsage.ToolCalls);
                    entry.DelegatedDeliveredSteeringCount = Math.Max(0, result.DelegatedRunUsage.DeliveredSteeringCount);
                    entry.DelegatedUndeliveredSteeringCount = Math.Max(0, result.DelegatedRunUsage.UndeliveredSteeringCount);
                    entry.DelegatedRegisteredToolCount = Math.Max(0, result.DelegatedRunUsage.RegisteredToolCount);
                    entry.DelegatedAvailableToolCount = Math.Clamp(
                        result.DelegatedRunUsage.AvailableToolCount,
                        0,
                        entry.DelegatedRegisteredToolCount);
                    entry.DelegatedAvailableToolDefinitionCharacters = Math.Max(
                        0,
                        result.DelegatedRunUsage.AvailableToolDefinitionCharacters);
                    entry.DelegatedHarnessInstructionCharacters = Math.Max(
                        0,
                        result.DelegatedRunUsage.HarnessInstructionCharacters);
                    entry.DelegatedQueueDurationMs = Math.Max(0, result.DelegatedRunUsage.QueueDurationMs);
                }
                if (result.DelegatedAnswer != null)
                {
                    entry.DelegatedAnswerText = SanitizeDelegatedAnswer(
                        result.DelegatedAnswer.Text,
                        out var answerWasTruncated);
                    entry.DelegatedAnswerHasSuccessfulEvidence =
                        entry.DelegatedAnswerText.Length > 0
                        && result.DelegatedAnswer.HasSuccessfulEvidence;
                    entry.DelegatedAnswerWasTruncated =
                        entry.DelegatedAnswerText.Length > 0
                        && (result.DelegatedAnswer.WasTruncated || answerWasTruncated);
                }
                entry.CaptureWorkspaceChangeSetMetadata(result.Content);
            }

            return entry;
        }


        private static CopilotAgentTraceEntry FromExecution(CopilotToolExecutionInfo execution)
        {
            return new CopilotAgentTraceEntry
            {
                CallId = SanitizeIdentifier(execution.CallId),
                Round = Math.Max(1, execution.Round),
                Attempt = Math.Max(1, execution.Attempt),
                MaxAttempts = Math.Max(Math.Max(1, execution.Attempt), execution.MaxAttempts),
                RuntimeName = SanitizeIdentifier(execution.RuntimeName),
                ToolName = SanitizeIdentifier(execution.ToolName),
                Access = execution.Access,
                RiskLevel = execution.RiskLevel,
                ApprovalMode = execution.ApprovalMode,
                Idempotency = execution.Idempotency,
                ConcurrencyMode = execution.ConcurrencyMode,
                ConcurrencyKey = SanitizeIdentifier(execution.ConcurrencyKey),
                ApprovalActionId = SanitizeIdentifier(execution.ApprovalActionId),
                State = execution.State,
                FailureKind = execution.FailureKind,
                RetryEligible = execution.RetryEligible,
                StartedAtUtc = execution.StartedAtUtc,
                CompletedAtUtc = execution.CompletedAtUtc,
                DurationMs = Math.Max(0, execution.DurationMs),
                QueueDurationMs = Math.Max(0, execution.QueueDurationMs),
                TimeoutMs = Math.Max(0, execution.TimeoutMs),
                ArgumentSummary = Sanitize(execution.ArgumentSummary),
            };
        }

        internal static string Sanitize(string? value)
        {
            var redacted = CopilotMcpAuditLogger.RedactText(value ?? string.Empty).Trim();
            return redacted.Length <= MaxSummaryLength ? redacted : redacted[..MaxSummaryLength] + "...";
        }

        private static string SanitizeDelegatedAnswer(string? value, out bool wasTruncated)
        {
            var redacted = CopilotMcpAuditLogger.RedactText(value ?? string.Empty).Trim();
            wasTruncated = redacted.Length > MaxDelegatedAnswerLength;
            return wasTruncated
                ? redacted[..MaxDelegatedAnswerLength].TrimEnd() + "\n...<子代理结果预览已截断>"
                : redacted;
        }

        private static string SanitizeIdentifier(string? value)
        {
            var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 120 ? text : text[..120];
        }

        private static long? NormalizeProgressCount(long? value)
        {
            return value.HasValue ? Math.Clamp(value.Value, 0, 1_000_000_000) : null;
        }

        private static string SanitizeProgressUnit(string? value)
        {
            var text = string.Join(" ", SanitizeIdentifier(value)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return text.Length <= 24 ? text : text[..24];
        }

        private static string FormatProgressUnit(string unit)
        {
            return unit switch
            {
                "files" => "个文件",
                "items" => "项",
                _ => unit,
            };
        }

        private string BuildActivityLabel()
        {
            var (running, completed) = ToolName switch
            {
                "FetchUrl" => ("正在读取网页", "读取了网页"),
                "WebSearch" => ("正在搜索网页", "搜索了网页"),
                "ReadLocalFile" or "ReadAttachedFile" => ("正在读取文件", "读取了文件"),
                "ListDirectory" or "SearchFiles" or "GrepText" or "SearchDocs" => ("正在搜索文件", "搜索了文件"),
                "DelegateExplore" => ("正在委派代码探索", "委派了代码探索"),
                "DelegateScout" => ("正在查阅外部资料", "查阅了外部资料"),
                _ when ToolName.StartsWith("Delegate", StringComparison.Ordinal) => ("正在委派子任务", "委派了子任务"),
                "GetRecentLog" => ("正在读取日志", "读取了日志"),
                "QueryFlowExecutionStats" or "QueryDatabaseSql" => ("正在查询数据库", "查询了数据库"),
                "ExecuteDatabaseSql" => ("正在执行数据库 SQL", "执行了数据库 SQL"),
                "InspectWindowsSystem" => ("正在检查系统", "检查了系统"),
                "InspectWindowsProcesses" => ("正在检查进程", "检查了进程"),
                "InspectWindowsServices" => ("正在检查服务", "检查了服务"),
                "InspectTcpPort" => ("正在检查端口", "检查了端口"),
                "InspectGitWorkingTree" => ("正在检查工作树", "检查了工作树"),
                "InspectGitDiff" => ("正在读取 Git 差异", "读取了 Git 差异"),
                "RunShellCommand" => ("正在运行命令", "运行了命令"),
                "ReadShellCommandOutput" => ("正在读取命令输出", "读取了命令输出"),
                "StartBackgroundShellCommand" => ("正在启动后台命令", "启动了后台命令"),
                "InspectBackgroundShellCommands" => ("正在检查后台命令", "检查了后台命令"),
                "ReadBackgroundShellCommandOutput" => ("正在读取后台输出", "读取了后台输出"),
                "MonitorBackgroundShellCommandOutput" => ("正在监控后台输出", "监控了后台输出"),
                "StopBackgroundShellCommandOutputMonitor" => ("正在停止后台输出监控", "停止了后台输出监控"),
                "WaitForBackgroundShellCommand" => ("正在等待后台命令", "等待了后台命令"),
                "WaitForBackgroundShellCommands" => ("正在等待多个后台命令", "等待了多个后台命令"),
                "StopBackgroundShellCommand" => ("正在停止后台命令", "停止了后台命令"),
                "ConvertBatchImages" => ("正在转换图像", "转换了图像"),
                "PreviewWorkspacePatchEnvelope" => ("正在准备修改", "准备了修改"),
                "ApplyWorkspacePatchEnvelope" => ("正在修改文件", "修改了文件"),
                "RollbackWorkspacePatchEnvelope" => ("正在回滚修改", "回滚了修改"),
                "CreateFlow" => ("正在创建流程", "创建了流程"),
                "ApplyTemplatePatch" or "TemplatePatch" => ("正在修改模板", "修改了模板"),
                "ExecuteMenu" => ("正在执行应用操作", "执行了应用操作"),
                "SetLanguage" or "SetTheme" => ("正在修改应用设置", "修改了应用设置"),
                _ => ($"正在运行 {ToolName}", $"运行了 {ToolName}"),
            };

            if (State == CopilotToolExecutionState.Completed
                && WorkspaceChangeSetRolledBack
                && string.Equals(ToolName, "ApplyWorkspacePatchEnvelope", StringComparison.Ordinal))
            {
                return completed + " · 已撤销";
            }

            return State switch
            {
                CopilotToolExecutionState.Pending => BuildWaitingActivityLabel(running),
                CopilotToolExecutionState.Running => running,
                CopilotToolExecutionState.AwaitingApproval => completed + " · 等待批准",
                CopilotToolExecutionState.Failed or CopilotToolExecutionState.TimedOut => completed + " · 失败",
                CopilotToolExecutionState.Denied => completed + " · 未批准",
                CopilotToolExecutionState.Cancelled => completed + " · 已取消",
                CopilotToolExecutionState.Interrupted => completed + " · 已中断",
                _ => completed,
            };
        }

        private static string BuildWaitingActivityLabel(string runningLabel)
        {
            const string runningPrefix = "正在";
            return runningLabel.StartsWith(runningPrefix, StringComparison.Ordinal)
                ? "等待" + runningLabel[runningPrefix.Length..]
                : "等待运行";
        }

        private bool IsFailedSearchAttempt()
        {
            if (!IsFailure)
                return false;

            return string.Equals(ToolName, "SearchFiles", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ToolName, "GrepText", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ToolName, "SearchDocs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(ToolName, "WebSearch", StringComparison.OrdinalIgnoreCase);
        }

        private string BuildFriendlyFailureSummary()
        {
            return FailureKind switch
            {
                CopilotToolFailureKind.NotFound => "没有找到可用结果。",
                CopilotToolFailureKind.Validation => "工具输入不符合要求。",
                CopilotToolFailureKind.Authorization => "当前操作没有获得授权。",
                CopilotToolFailureKind.Transient => "暂时无法完成，Agent 可以重试。",
                CopilotToolFailureKind.Cancelled => "操作已取消。",
                _ => !string.IsNullOrWhiteSpace(ResultSummary) ? ResultSummary : ErrorMessage,
            };
        }

        private string BuildFriendlySuccessSummary()
        {
            if (State is CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running)
                return ResultSummary;

            return ToolName switch
            {
                "FetchUrl" => "已读取网页正文。",
                "WebSearch" => "已获得网页搜索结果。",
                "ReadLocalFile" or "ReadAttachedFile" => "已读取文件内容。",
                "ListDirectory" or "SearchFiles" or "GrepText" or "SearchDocs" => "已完成文件搜索。",
                "DelegateExplore" => "只读 Explore 子 Agent 已返回结果。",
                "DelegateScout" => "只读 Scout 子 Agent 已返回外部资料。",
                _ when ToolName.StartsWith("Delegate", StringComparison.Ordinal) => ResultSummary,
                "GetRecentLog" => "已读取最近日志。",
                "QueryFlowExecutionStats" or "QueryDatabaseSql" => "已获得数据库查询结果。",
                "ExecuteDatabaseSql" => "数据库 SQL 已执行。",
                "InspectWindowsSystem" => "Windows 系统信息检查完成。",
                "InspectWindowsProcesses" => "Windows 进程检查完成。",
                "InspectWindowsServices" => "Windows 服务检查完成。",
                "InspectTcpPort" => "端口检查完成。",
                "InspectGitWorkingTree" => "Git 工作树检查完成。",
                "InspectGitDiff" => "Git 差异读取完成。",
                "RunShellCommand" => "命令已执行。",
                "ReadShellCommandOutput" => "命令输出读取完成。",
                "StartBackgroundShellCommand" => ResultSummary,
                "InspectBackgroundShellCommands" => "后台命令状态检查完成。",
                "ReadBackgroundShellCommandOutput" => "后台命令输出读取完成。",
                "MonitorBackgroundShellCommandOutput" => ResultSummary,
                "StopBackgroundShellCommandOutputMonitor" => ResultSummary,
                "WaitForBackgroundShellCommand" => ResultSummary,
                "WaitForBackgroundShellCommands" => ResultSummary,
                "StopBackgroundShellCommand" => ResultSummary,
                "PreviewWorkspacePatchEnvelope" => "文件修改预览已准备。",
                "ApplyWorkspacePatchEnvelope" => WorkspaceChangeSetRolledBack
                    ? "这组文件修改已撤销。"
                    : WorkspaceChangedFiles.Count > 0
                        ? $"已完成 {WorkspaceChangedFiles.Count} 个文件的修改，可逐个打开核对。"
                        : "文件修改已完成。",
                "RollbackWorkspacePatchEnvelope" => "文件修改已回滚。",
                "CreateFlow" => "流程已创建。",
                "ApplyTemplatePatch" or "TemplatePatch" => "模板修改已完成。",
                "ExecuteMenu" => "应用操作已执行。",
                "SetLanguage" or "SetTheme" => "应用设置已更新。",
                _ => ResultSummary,
            };
        }

        private static string TrimForActivity(string? value, int maxLength)
        {
            var normalized = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            while (normalized.Contains("  ", StringComparison.Ordinal))
                normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
            return normalized.Length <= maxLength ? normalized : normalized[..maxLength] + "...";
        }

        private static string FormatDuration(long durationMs)
        {
            return durationMs < 1000 ? $"{Math.Max(0, durationMs)}ms" : $"{durationMs / 1000d:0.#}s";
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
            CopilotToolExecutionHookState.Completed => "completed",
            CopilotToolExecutionHookState.Denied => "denied",
            CopilotToolExecutionHookState.Failed => "failed",
            CopilotToolExecutionHookState.TimedOut => "timed out",
            CopilotToolExecutionHookState.Cancelled => "cancelled",
            CopilotToolExecutionHookState.Skipped => "skipped",
            _ => "unknown",
        };

        private static string FormatDiagnosticState(CopilotToolExecutionState state) => state switch
        {
            CopilotToolExecutionState.Pending => "Pending",
            CopilotToolExecutionState.Running => "Running...",
            CopilotToolExecutionState.Completed => "Completed",
            CopilotToolExecutionState.Failed => "Failed",
            CopilotToolExecutionState.TimedOut => "Timed out",
            CopilotToolExecutionState.Denied => "Denied",
            CopilotToolExecutionState.Cancelled => "Cancelled",
            CopilotToolExecutionState.Interrupted => "Interrupted",
            CopilotToolExecutionState.AwaitingApproval => "Awaiting approval",
            _ => "Unknown",
        };
    }

    public sealed class CopilotWorkspaceChangeFile
    {
        private const int MaximumPathCharacters = 4096;

        public string Operation { get; set; } = string.Empty;

        public string FilePath { get; set; } = string.Empty;

        [JsonIgnore]
        public string DisplayLabel => $"{OperationLabel}  {GetFileName(FilePath)}";

        [JsonIgnore]
        public string OperationLabel => Operation switch
        {
            "Create" => "新建",
            "Update" => "更新",
            "Delete" => "删除",
            _ => "修改",
        };

        internal bool EnsureValid(out bool changed)
        {
            var originalOperation = Operation;
            var originalFilePath = FilePath;
            Operation = (Operation ?? string.Empty).Trim() switch
            {
                "Create" => "Create",
                "Update" => "Update",
                "Delete" => "Delete",
                _ => string.Empty,
            };
            FilePath = (FilePath ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            changed = !string.Equals(originalOperation, Operation, StringComparison.Ordinal)
                || !string.Equals(originalFilePath, FilePath, StringComparison.Ordinal);
            return Operation.Length > 0 && FilePath.Length is > 0 and <= MaximumPathCharacters;
        }

        private static string GetFileName(string filePath)
        {
            try
            {
                return Path.GetFileName(filePath);
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException)
            {
                return filePath;
            }
        }
    }
}
