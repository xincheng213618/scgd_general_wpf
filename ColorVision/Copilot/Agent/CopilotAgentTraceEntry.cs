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
        public const int CurrentSchemaVersion = 14;
        private const int MaxSummaryLength = 800;
        private const int MaxDelegatedAnswerLength = 20_000;
        internal const int MaxPersistedHookRuns = 64;
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

        public string DelegatedModel { get; set; } = string.Empty;

        public string DelegatedReasoningEffort { get; set; } = string.Empty;

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

        public bool ShouldSerializeDelegatedModel() => !string.IsNullOrEmpty(DelegatedModel);

        public bool ShouldSerializeDelegatedReasoningEffort() => !string.IsNullOrEmpty(DelegatedReasoningEffort);

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
                entry.DelegatedModel = Sanitize(reportedProgress.DelegatedRun.Model);
                entry.DelegatedReasoningEffort = SanitizeIdentifier(reportedProgress.DelegatedRun.ReasoningEffort);
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
                    entry.DelegatedModel = Sanitize(result.DelegatedRunUsage.Model);
                    entry.DelegatedReasoningEffort = SanitizeIdentifier(result.DelegatedRunUsage.ReasoningEffort);
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
