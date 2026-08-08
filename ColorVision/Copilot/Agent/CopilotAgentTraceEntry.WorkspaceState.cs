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
        internal bool CompleteActiveExecution(
            CopilotToolExecutionState terminalState,
            CopilotToolFailureKind failureKind,
            string failureCode,
            string errorMessage,
            DateTimeOffset completedAtUtc)
        {
            if (State is not (CopilotToolExecutionState.Pending
                or CopilotToolExecutionState.Running
                or CopilotToolExecutionState.AwaitingApproval))
            {
                return false;
            }
            if (terminalState is not (CopilotToolExecutionState.Cancelled
                or CopilotToolExecutionState.Interrupted))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(terminalState),
                    terminalState,
                    "An active trace can only be closed as cancelled or interrupted without an authoritative tool result.");
            }

            State = terminalState;
            FailureKind = failureKind;
            FailureCode = CopilotToolFailureCode.Normalize(failureCode);
            RetryEligible = false;
            CompletedAtUtc = completedAtUtc;
            if (StartedAtUtc != default)
            {
                DurationMs = Math.Max(
                    DurationMs,
                    (long)Math.Max(0, (completedAtUtc - StartedAtUtc).TotalMilliseconds));
            }
            ErrorMessage = Sanitize(errorMessage);

            OnPropertyChanged(nameof(State));
            OnPropertyChanged(nameof(FailureKind));
            OnPropertyChanged(nameof(FailureCode));
            OnPropertyChanged(nameof(RetryEligible));
            OnPropertyChanged(nameof(CompletedAtUtc));
            OnPropertyChanged(nameof(DurationMs));
            OnPropertyChanged(nameof(ErrorMessage));
            OnPropertyChanged(nameof(IsFailure));
            OnPropertyChanged(nameof(ActivityGlyph));
            OnPropertyChanged(nameof(ActivityLabel));
            OnPropertyChanged(nameof(ActivityDurationLabel));
            OnPropertyChanged(nameof(ActivityProgressLabel));
            OnPropertyChanged(nameof(ActivityDescription));
            OnPropertyChanged(nameof(DiagnosticDetails));
            OnPropertyChanged(nameof(CanRequestWorkspaceRollback));
            return true;
        }

        internal bool MarkWorkspaceChangeSetRolledBack(string changeSetId)
        {
            if (WorkspaceChangeSetRolledBack
                || !string.Equals(WorkspaceChangeSetId, changeSetId, StringComparison.Ordinal))
            {
                return false;
            }

            WorkspaceChangeSetRolledBack = true;
            OnPropertyChanged(nameof(WorkspaceChangeSetRolledBack));
            OnPropertyChanged(nameof(CanRequestWorkspaceRollback));
            OnPropertyChanged(nameof(ActivityLabel));
            OnPropertyChanged(nameof(ActivityDescription));
            return true;
        }

        internal void DiscardWorkspaceRollbackAuthority()
        {
            if (string.IsNullOrWhiteSpace(WorkspaceChangeSetId)
                && !WorkspaceChangeSetExpiresAtUtc.HasValue)
            {
                return;
            }

            WorkspaceChangeSetId = string.Empty;
            WorkspaceChangeSetExpiresAtUtc = null;
            OnPropertyChanged(nameof(WorkspaceChangeSetId));
            OnPropertyChanged(nameof(WorkspaceChangeSetExpiresAtUtc));
            OnPropertyChanged(nameof(CanRequestWorkspaceRollback));
        }

        private void CaptureWorkspaceChangeSetMetadata(string? content)
        {
            if (!string.Equals(ToolName, "ApplyWorkspacePatchEnvelope", StringComparison.Ordinal)
                && !string.Equals(ToolName, "RollbackWorkspacePatchEnvelope", StringComparison.Ordinal))
            {
                return;
            }

            var changeSetId = ReadMetadataValue(content, "change_set_id");
            const string changeSetPrefix = "workspace-change-set:";
            if (!changeSetId.StartsWith(changeSetPrefix, StringComparison.Ordinal)
                || !Guid.TryParseExact(changeSetId[changeSetPrefix.Length..], "N", out _))
            {
                return;
            }

            WorkspaceChangeSetId = changeSetId;
            var expiresAt = ReadMetadataValue(content, "expires_at_utc");
            if (DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedExpiresAt))
                WorkspaceChangeSetExpiresAtUtc = parsedExpiresAt;

            if (!string.Equals(ToolName, "ApplyWorkspacePatchEnvelope", StringComparison.Ordinal)
                || !int.TryParse(ReadMetadataValue(content, "file_count"), NumberStyles.None, CultureInfo.InvariantCulture, out var fileCount))
            {
                return;
            }

            WorkspaceChangedFiles.Clear();
            for (var index = 1; index <= Math.Min(8, fileCount); index++)
            {
                var file = new CopilotWorkspaceChangeFile
                {
                    Operation = ReadMetadataValue(content, $"file_{index}_operation"),
                    FilePath = ReadMetadataValue(content, $"file_{index}_path"),
                };
                if (file.EnsureValid(out _) && !WorkspaceChangedFiles.Exists(item =>
                    string.Equals(item.FilePath, file.FilePath, StringComparison.OrdinalIgnoreCase)))
                {
                    WorkspaceChangedFiles.Add(file);
                }
            }
        }

        private static string ReadMetadataValue(string? content, string key)
        {
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            foreach (var line in content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = line.IndexOf(':');
                if (separator <= 0 || !string.Equals(line[..separator].Trim(), key, StringComparison.OrdinalIgnoreCase))
                    continue;

                return line[(separator + 1)..].Trim();
            }

            return string.Empty;
        }

        public bool EnsureValid(DateTimeOffset recoveredAtUtc)
        {
            var changed = false;
            HookRuns ??= new List<CopilotToolExecutionHookRun>();
            var seenHookRuns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < HookRuns.Count; index++)
            {
                var hookRun = HookRuns[index];
                var identity = hookRun == null
                    ? string.Empty
                    : $"{(int)hookRun.Phase}:{hookRun.SourceId}";
                if (hookRun?.IsStructurallyValid() != true
                    || !seenHookRuns.Add(identity)
                    || index >= MaxPersistedHookRuns)
                {
                    HookRuns.RemoveAt(index--);
                    changed = true;
                }
            }
            WorkspaceChangedFiles ??= new List<CopilotWorkspaceChangeFile>();
            var seenWorkspacePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < WorkspaceChangedFiles.Count; index++)
            {
                var file = WorkspaceChangedFiles[index];
                if (file == null)
                {
                    WorkspaceChangedFiles.RemoveAt(index--);
                    changed = true;
                    continue;
                }

                var isValid = file.EnsureValid(out var fileChanged);
                changed |= fileChanged;
                if (!isValid || !seenWorkspacePaths.Add(file.FilePath) || index >= 8)
                {
                    WorkspaceChangedFiles.RemoveAt(index--);
                    changed = true;
                }
            }
            if (!string.Equals(ToolName, "ApplyWorkspacePatchEnvelope", StringComparison.Ordinal)
                && (WorkspaceChangeSetRolledBack || WorkspaceChangedFiles.Count > 0))
            {
                WorkspaceChangeSetRolledBack = false;
                WorkspaceChangedFiles.Clear();
                changed = true;
            }
            var originalSchemaVersion = SchemaVersion;
            var originalCallId = CallId;
            var originalRuntimeName = RuntimeName;
            var originalToolName = ToolName;
            var originalArgumentSummary = ArgumentSummary;
            var originalApprovalActionId = ApprovalActionId;
            var originalConcurrencyKey = ConcurrencyKey;
            var originalResultSummary = ResultSummary;
            var originalErrorMessage = ErrorMessage;
            var originalFailureCode = FailureCode;
            var originalDelegatedRoleId = DelegatedRoleId;
            var originalDelegatedRunId = DelegatedRunId;
            var originalDelegatedResumeFromRunId = DelegatedResumeFromRunId;
            var originalDelegatedModel = DelegatedModel;
            var originalDelegatedReasoningEffort = DelegatedReasoningEffort;
            var originalDelegatedStopReason = DelegatedStopReason;
            var originalDelegatedRequestTokenBudget = DelegatedRequestTokenBudget;
            var originalDelegatedConsumedTokens = DelegatedConsumedTokens;
            var originalDelegatedProviderCalls = DelegatedProviderCalls;
            var originalDelegatedToolCalls = DelegatedToolCalls;
            var originalDelegatedDeliveredSteeringCount = DelegatedDeliveredSteeringCount;
            var originalDelegatedUndeliveredSteeringCount = DelegatedUndeliveredSteeringCount;
            var originalDelegatedRegisteredToolCount = DelegatedRegisteredToolCount;
            var originalDelegatedAvailableToolCount = DelegatedAvailableToolCount;
            var originalDelegatedAvailableToolDefinitionCharacters = DelegatedAvailableToolDefinitionCharacters;
            var originalDelegatedHarnessInstructionCharacters = DelegatedHarnessInstructionCharacters;
            var originalDelegatedQueueDurationMs = DelegatedQueueDurationMs;
            var originalDelegatedAnswerText = DelegatedAnswerText;
            var originalDelegatedAnswerHasSuccessfulEvidence = DelegatedAnswerHasSuccessfulEvidence;
            var originalDelegatedAnswerWasTruncated = DelegatedAnswerWasTruncated;
            var originalRound = Round;
            var originalAttempt = Attempt;
            var originalMaxAttempts = MaxAttempts;
            var originalDurationMs = DurationMs;
            var originalQueueDurationMs = QueueDurationMs;
            var originalTimeoutMs = TimeoutMs;
            var originalProgressMessage = ProgressMessage;
            var originalProgressCompleted = ProgressCompleted;
            var originalProgressTotal = ProgressTotal;
            var originalProgressUnit = ProgressUnit;
            SchemaVersion = CurrentSchemaVersion;
            CallId = SanitizeIdentifier(CallId);
            RuntimeName = SanitizeIdentifier(RuntimeName);
            ToolName = SanitizeIdentifier(ToolName);
            ArgumentSummary = Sanitize(ArgumentSummary);
            ApprovalActionId = SanitizeIdentifier(ApprovalActionId);
            ConcurrencyKey = SanitizeIdentifier(ConcurrencyKey);
            ResultSummary = Sanitize(ResultSummary);
            ErrorMessage = Sanitize(ErrorMessage);
            FailureCode = State == CopilotToolExecutionState.Completed
                ? string.Empty
                : CopilotToolFailureCode.Normalize(FailureCode);
            DelegatedRoleId = SanitizeIdentifier(DelegatedRoleId);
            DelegatedRunId = SanitizeIdentifier(DelegatedRunId);
            DelegatedResumeFromRunId = SanitizeIdentifier(DelegatedResumeFromRunId);
            DelegatedModel = CopilotConfiguredModelSelection.TryNormalize(DelegatedModel, out var delegatedModel)
                ? delegatedModel
                : string.Empty;
            DelegatedReasoningEffort = NormalizeDelegatedReasoningEffort(DelegatedReasoningEffort);
            DelegatedRequestTokenBudget = Math.Max(0, DelegatedRequestTokenBudget);
            DelegatedConsumedTokens = Math.Max(0, DelegatedConsumedTokens);
            DelegatedProviderCalls = Math.Max(0, DelegatedProviderCalls);
            DelegatedToolCalls = Math.Max(0, DelegatedToolCalls);
            DelegatedDeliveredSteeringCount = Math.Max(0, DelegatedDeliveredSteeringCount);
            DelegatedUndeliveredSteeringCount = Math.Max(0, DelegatedUndeliveredSteeringCount);
            DelegatedRegisteredToolCount = Math.Max(0, DelegatedRegisteredToolCount);
            DelegatedAvailableToolCount = Math.Clamp(
                DelegatedAvailableToolCount,
                0,
                DelegatedRegisteredToolCount);
            DelegatedAvailableToolDefinitionCharacters = Math.Max(0, DelegatedAvailableToolDefinitionCharacters);
            DelegatedHarnessInstructionCharacters = Math.Max(0, DelegatedHarnessInstructionCharacters);
            DelegatedQueueDurationMs = Math.Max(0, DelegatedQueueDurationMs);
            DelegatedAnswerText = SanitizeDelegatedAnswer(
                DelegatedAnswerText,
                out var delegatedAnswerWasTruncatedByPersistence);
            DelegatedAnswerHasSuccessfulEvidence =
                DelegatedAnswerText.Length > 0
                && DelegatedAnswerHasSuccessfulEvidence;
            DelegatedAnswerWasTruncated =
                DelegatedAnswerText.Length > 0
                && (DelegatedAnswerWasTruncated || delegatedAnswerWasTruncatedByPersistence);
            Round = Math.Max(1, Round);
            Attempt = Math.Max(1, Attempt);
            MaxAttempts = Math.Max(Attempt, MaxAttempts);
            DurationMs = Math.Max(0, DurationMs);
            QueueDurationMs = Math.Max(0, QueueDurationMs);
            TimeoutMs = Math.Max(0, TimeoutMs);
            ProgressMessage = Sanitize(ProgressMessage);
            ProgressCompleted = NormalizeProgressCount(ProgressCompleted);
            ProgressTotal = NormalizeProgressCount(ProgressTotal);
            if (ProgressCompleted.HasValue && ProgressTotal.HasValue)
                ProgressCompleted = Math.Min(ProgressCompleted.Value, ProgressTotal.Value);
            ProgressUnit = SanitizeProgressUnit(ProgressUnit);
            if (originalSchemaVersion < 4)
            {
                ConcurrencyMode = Access == CopilotToolAccess.Write || Idempotency != CopilotToolIdempotency.Idempotent
                    ? CopilotToolConcurrencyMode.Exclusive
                    : CopilotToolConcurrencyMode.SharedRead;
                if (string.IsNullOrWhiteSpace(ConcurrencyKey) && !string.IsNullOrWhiteSpace(ToolName))
                    ConcurrencyKey = "legacy:" + ToolName;
            }
            changed |= originalSchemaVersion != SchemaVersion
                || !string.Equals(originalCallId, CallId, StringComparison.Ordinal)
                || !string.Equals(originalRuntimeName, RuntimeName, StringComparison.Ordinal)
                || !string.Equals(originalToolName, ToolName, StringComparison.Ordinal)
                || !string.Equals(originalArgumentSummary, ArgumentSummary, StringComparison.Ordinal)
                || !string.Equals(originalApprovalActionId, ApprovalActionId, StringComparison.Ordinal)
                || !string.Equals(originalConcurrencyKey, ConcurrencyKey, StringComparison.Ordinal)
                || !string.Equals(originalResultSummary, ResultSummary, StringComparison.Ordinal)
                || !string.Equals(originalErrorMessage, ErrorMessage, StringComparison.Ordinal)
                || !string.Equals(originalFailureCode, FailureCode, StringComparison.Ordinal)
                || !string.Equals(originalDelegatedRoleId, DelegatedRoleId, StringComparison.Ordinal)
                || !string.Equals(originalDelegatedRunId, DelegatedRunId, StringComparison.Ordinal)
                || !string.Equals(originalDelegatedResumeFromRunId, DelegatedResumeFromRunId, StringComparison.Ordinal)
                || !string.Equals(originalDelegatedModel, DelegatedModel, StringComparison.Ordinal)
                || !string.Equals(originalDelegatedReasoningEffort, DelegatedReasoningEffort, StringComparison.Ordinal)
                || originalDelegatedStopReason != DelegatedStopReason
                || originalDelegatedRequestTokenBudget != DelegatedRequestTokenBudget
                || originalDelegatedConsumedTokens != DelegatedConsumedTokens
                || originalDelegatedProviderCalls != DelegatedProviderCalls
                || originalDelegatedToolCalls != DelegatedToolCalls
                || originalDelegatedDeliveredSteeringCount != DelegatedDeliveredSteeringCount
                || originalDelegatedUndeliveredSteeringCount != DelegatedUndeliveredSteeringCount
                || originalDelegatedRegisteredToolCount != DelegatedRegisteredToolCount
                || originalDelegatedAvailableToolCount != DelegatedAvailableToolCount
                || originalDelegatedAvailableToolDefinitionCharacters != DelegatedAvailableToolDefinitionCharacters
                || originalDelegatedHarnessInstructionCharacters != DelegatedHarnessInstructionCharacters
                || originalDelegatedQueueDurationMs != DelegatedQueueDurationMs
                || !string.Equals(originalDelegatedAnswerText, DelegatedAnswerText, StringComparison.Ordinal)
                || originalDelegatedAnswerHasSuccessfulEvidence != DelegatedAnswerHasSuccessfulEvidence
                || originalDelegatedAnswerWasTruncated != DelegatedAnswerWasTruncated
                || originalRound != Round
                || originalAttempt != Attempt
                || originalMaxAttempts != MaxAttempts
                || originalDurationMs != DurationMs
                || originalQueueDurationMs != QueueDurationMs
                || originalTimeoutMs != TimeoutMs
                || !string.Equals(originalProgressMessage, ProgressMessage, StringComparison.Ordinal)
                || originalProgressCompleted != ProgressCompleted
                || originalProgressTotal != ProgressTotal
                || !string.Equals(originalProgressUnit, ProgressUnit, StringComparison.Ordinal);

            if (!Enum.IsDefined(State))
            {
                State = CopilotToolExecutionState.Failed;
                changed = true;
            }

            if (!Enum.IsDefined(RiskLevel))
            {
                RiskLevel = CopilotToolRiskLevel.Low;
                changed = true;
            }

            if (!Enum.IsDefined(ApprovalMode))
            {
                ApprovalMode = CopilotToolApprovalMode.Never;
                changed = true;
            }

            if (!Enum.IsDefined(Idempotency))
            {
                Idempotency = CopilotToolIdempotency.Unknown;
                changed = true;
            }

            if (!Enum.IsDefined(ConcurrencyMode))
            {
                ConcurrencyMode = Access == CopilotToolAccess.Write ? CopilotToolConcurrencyMode.Exclusive : CopilotToolConcurrencyMode.SharedRead;
                changed = true;
            }

            if (!Enum.IsDefined(FailureKind))
            {
                FailureKind = CopilotToolFailureKind.Unspecified;
                changed = true;
            }

            if (!Enum.IsDefined(DelegatedStopReason))
            {
                DelegatedStopReason = CopilotAgentStopReason.None;
                changed = true;
            }

            if (State is CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running or CopilotToolExecutionState.AwaitingApproval)
            {
                var wasAwaitingApproval = State == CopilotToolExecutionState.AwaitingApproval;
                State = CopilotToolExecutionState.Interrupted;
                CompletedAtUtc = recoveredAtUtc;
                if (StartedAtUtc != default)
                    DurationMs = Math.Max(DurationMs, (long)Math.Max(0, (recoveredAtUtc - StartedAtUtc).TotalMilliseconds));
                ErrorMessage = wasAwaitingApproval
                    ? "Approval was interrupted before a decision was recorded. Submit the request again to create a fresh approval."
                    : "Execution was interrupted before completion.";
                changed = true;
            }

            changed |= NormalizeWorkspaceRollbackAuthority(recoveredAtUtc);
            return changed;
        }

        private static string NormalizeDelegatedReasoningEffort(string? value)
        {
            var normalized = (value ?? string.Empty).Trim();
            if (string.Equals(normalized, "model_default", StringComparison.Ordinal))
                return normalized;
            return CopilotCodexReasoningEffortSelection.TryParse(normalized, out var effort)
                ? CopilotCodexReasoningEffortSelection.GetConfigToken(effort)
                : string.Empty;
        }

        private bool NormalizeWorkspaceRollbackAuthority(DateTimeOffset recoveredAtUtc)
        {
            var changeSetId = WorkspaceChangeSetId?.Trim() ?? string.Empty;
            const string changeSetPrefix = "workspace-change-set:";
            var validChangeSetId = changeSetId.StartsWith(changeSetPrefix, StringComparison.Ordinal)
                && changeSetId.Length == changeSetPrefix.Length + 32
                && Guid.TryParseExact(changeSetId[changeSetPrefix.Length..], "N", out _);
            var expiresAtUtc = WorkspaceChangeSetExpiresAtUtc;
            var retainsAuthority = string.Equals(ToolName, "ApplyWorkspacePatchEnvelope", StringComparison.Ordinal)
                && State == CopilotToolExecutionState.Completed
                && !WorkspaceChangeSetRolledBack
                && validChangeSetId
                && expiresAtUtc > recoveredAtUtc
                && expiresAtUtc <= recoveredAtUtc.Add(MaximumWorkspaceRollbackLifetime);
            if (retainsAuthority)
            {
                if (string.Equals(WorkspaceChangeSetId, changeSetId, StringComparison.Ordinal))
                    return false;
                WorkspaceChangeSetId = changeSetId;
                return true;
            }

            if (string.IsNullOrEmpty(WorkspaceChangeSetId) && !WorkspaceChangeSetExpiresAtUtc.HasValue)
                return false;
            WorkspaceChangeSetId = string.Empty;
            WorkspaceChangeSetExpiresAtUtc = null;
            return true;
        }
    }
}
