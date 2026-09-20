using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed partial class CopilotWorkspacePatchStore
    {
        private const int MaxChangeSetFiles = 8;
        private const int MaxChangeSets = 8;
        private readonly Dictionary<string, WorkspaceChangeSetRecord> _changeSets = new(StringComparer.Ordinal);

        private CopilotToolResult CreateChangeSetPreview(
            CopilotAgentRequest request,
            string[] previewIds,
            string toolName,
            int minimumFiles)
        {
            EnsureCheckpointRecordsLoaded();
            if (previewIds.Length < minimumFiles || previewIds.Length > MaxChangeSetFiles)
            {
                return Failure(toolName, CopilotToolFailureKind.Validation,
                    "The workspace change-set preview list is invalid.",
                    $"The operation list must contain {minimumFiles}-{MaxChangeSetFiles} workspace preview identifiers.");
            }

            var now = DateTimeOffset.UtcNow;
            WorkspaceChangeSetRecord changeSet;
            WorkspacePatchRecord[] records;
            lock (_syncRoot)
            {
                RemoveExpiredEntries(now);
                records = previewIds
                    .Select(previewId => _records.TryGetValue(previewId, out var record) ? record : null)
                    .Where(record => record != null)
                    .Cast<WorkspacePatchRecord>()
                    .ToArray();
                if (records.Length != previewIds.Length)
                {
                    return Failure(toolName, CopilotToolFailureKind.NotFound,
                        "One or more workspace previews are unavailable or expired.",
                        "Create fresh workspace operation previews before preparing the guarded change set.");
                }
                if (records.Any(record => record.State != WorkspacePatchState.Previewed))
                {
                    return Failure(toolName, CopilotToolFailureKind.Conflict,
                        "Every workspace preview must be unused before it can enter a change set.",
                        "At least one preview has already been applied, rolled back, or invalidated.");
                }
                if (records.Any(record => !string.IsNullOrWhiteSpace(record.ChangeSetId)))
                {
                    return Failure(toolName, CopilotToolFailureKind.Conflict,
                        "A workspace preview is already reserved by another change set.",
                        "Create fresh previews or use the existing matching change-set identifier.");
                }
                if (records.Select(record => record.FullPath).Distinct(StringComparer.OrdinalIgnoreCase).Count() != records.Length)
                {
                    return Failure(toolName, CopilotToolFailureKind.Validation,
                        "A workspace change set cannot target the same path more than once.",
                        "Combine replacements for one file into a single update operation before grouping files.");
                }

                changeSet = new WorkspaceChangeSetRecord
                {
                    ChangeSetId = "workspace-change-set:" + Guid.NewGuid().ToString("N"),
                    PreviewIds = previewIds,
                    ConversationId = request.ConversationId?.Trim() ?? string.Empty,
                    WorkspacePath = TryNormalizeWorkspaceIdentity(request.WorkspacePath, out var workspacePath)
                        ? workspacePath
                        : string.Empty,
                    CreatedAtUtc = now,
                    ExpiresAtUtc = records.Min(record => record.ExpiresAtUtc),
                    State = WorkspaceChangeSetState.Previewed,
                };
                foreach (var record in records)
                    record.ChangeSetId = changeSet.ChangeSetId;
                StoreChangeSet(changeSet);
            }

            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = true,
                Summary = $"Prepared one conflict-checked workspace change set for {records.Length} file(s).",
                Content = BuildChangeSetContent(changeSet, records, preview: true),
            };
        }

        public Task<CopilotToolResult> ApplyPatchEnvelopeAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            return MutateChangeSetAsync(request, input, rollback: false, "ApplyWorkspacePatchEnvelope", cancellationToken);
        }

        public Task<CopilotToolResult> RollbackPatchEnvelopeAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            return MutateChangeSetAsync(request, input, rollback: true, "RollbackWorkspacePatchEnvelope", cancellationToken);
        }

        public static string GetChangeSetConcurrencyKey(CopilotAgentToolInput input, string fallbackToolName)
        {
            return TryGetChangeSetId(input, out var changeSetId)
                ? changeSetId
                : "tool:" + fallbackToolName;
        }

        public CopilotToolApprovalPresentation CreateChangeSetApprovalPresentation(
            CopilotAgentToolInput input,
            bool rollback)
        {
            EnsureCheckpointRecordsLoaded();
            if (!TryGetChangeSetId(input, out var changeSetId))
            {
                return new CopilotToolApprovalPresentation(
                    rollback ? "Approve workspace change-set rollback" : "Approve workspace change set",
                    "The workspace change-set identifier is missing or invalid.");
            }

            lock (_syncRoot)
            {
                RemoveExpiredEntries(DateTimeOffset.UtcNow);
                if (!_changeSets.TryGetValue(changeSetId, out var changeSet)
                    || !TryResolveChangeSetRecords(changeSet, out var records))
                {
                    return new CopilotToolApprovalPresentation(
                        rollback ? "Approve workspace change-set rollback" : "Approve workspace change set",
                        $"The referenced workspace change set {changeSetId} is no longer available.");
                }

                var builder = new StringBuilder();
                builder.Append(rollback ? "Restore" : "Apply").Append(' ')
                    .Append(records.Length).AppendLine(" workspace files as one guarded change set.");
                builder.AppendLine("Every file is revalidated before the first write. If a later write fails, completed writes are compensated.");
                foreach (var record in records)
                {
                    builder.Append("- ").Append(record.Operation).Append(": ");
                    CopilotApprovalReviewTextEncoder.Append(builder, record.FullPath);
                    if (record.Operation == WorkspacePatchOperation.Replace)
                        builder.Append(" (").Append(record.Replacements.Length)
                            .Append(record.Replacements.Length == 1 ? " replacement)" : " replacements)");
                    builder.AppendLine();
                    builder.Append("  SHA-256: ")
                        .Append(rollback ? record.AfterSha256 : record.BeforeSha256)
                        .Append(" -> ")
                        .AppendLine(rollback ? record.BeforeSha256 : record.AfterSha256);
                }
                var reviewDetails = builder.ToString().TrimEnd();
                return new CopilotToolApprovalPresentation(
                    rollback
                        ? $"Rollback {records.Length}-file workspace change set"
                        : $"Apply {records.Length}-file workspace change set",
                    "Review every target path and SHA-256 transition in the complete execution details before approving.",
                    ImpactSummary: rollback
                        ? $"将把当前工作区中的 {records.Length} 个文件恢复到该变更集应用前的内容。"
                        : $"将以一个受保护变更集写入当前工作区中的 {records.Length} 个文件。",
                    Reversibility: rollback
                        ? CopilotApprovalReversibility.ManualOnly
                        : CopilotApprovalReversibility.AutomaticUntilExpiry,
                    ReversibilitySummary: rollback
                        ? "回滚本身不会再生成自动恢复点；如需撤销回滚，需要后续手动修改。"
                        : $"可在 {changeSet.ExpiresAtUtc.ToLocalTime():HH:mm:ss} 前通过该变更集整体回滚。")
                {
                    ReviewDetails = reviewDetails,
                };
            }
        }

        private async Task<CopilotToolResult> MutateChangeSetAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            bool rollback,
            string toolName,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            input ??= CopilotAgentToolInput.Empty;
            if (!TryGetChangeSetId(input, out var changeSetId))
            {
                return Failure(toolName, CopilotToolFailureKind.Validation,
                    "The workspace change-set identifier is missing.", "changeSetId is required.");
            }

            EnsureCheckpointRecordsLoaded();
            WorkspaceChangeSetRecord changeSet;
            WorkspacePatchRecord[] records;
            lock (_syncRoot)
            {
                RemoveExpiredEntries(DateTimeOffset.UtcNow);
                if (!_changeSets.TryGetValue(changeSetId, out changeSet!)
                    || !TryResolveChangeSetRecords(changeSet, out records))
                {
                    return Failure(toolName, CopilotToolFailureKind.NotFound,
                        "The workspace change set is unavailable or expired.",
                        "Prepare a fresh multi-file change-set preview before trying again.");
                }
                if (!MatchesCheckpointBinding(request, changeSet))
                {
                    return Failure(toolName, CopilotToolFailureKind.Authorization,
                        "The workspace change set belongs to a different conversation or workspace.",
                        "Use the original Copilot conversation and exact workspace that created this change set.");
                }

                var expectedChangeSetState = rollback ? WorkspaceChangeSetState.Applied : WorkspaceChangeSetState.Previewed;
                var expectedPatchState = rollback ? WorkspacePatchState.Applied : WorkspacePatchState.Previewed;
                if (changeSet.State != expectedChangeSetState
                    || records.Any(record => record.State != expectedPatchState
                        || !string.Equals(record.ChangeSetId, changeSetId, StringComparison.Ordinal)))
                {
                    return Failure(toolName, CopilotToolFailureKind.Conflict,
                        rollback ? "The workspace change set is not fully applied." : "The workspace change set has already been consumed or invalidated.",
                        $"Current change-set state: {changeSet.State}. Every child preview must be {expectedPatchState}.");
                }
                changeSet.State = rollback ? WorkspaceChangeSetState.RollingBack : WorkspaceChangeSetState.Applying;
            }

            CopilotToolResult? validationFailure;
            try
            {
                validationFailure = await ValidateChangeSetAsync(request, records, rollback, toolName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CompleteChangeSet(
                    changeSet,
                    rollback ? WorkspaceChangeSetState.Applied : WorkspaceChangeSetState.Previewed,
                    releaseReservations: false,
                    records);
                throw;
            }
            if (validationFailure != null)
            {
                CompleteChangeSet(changeSet, WorkspaceChangeSetState.Invalidated, releaseReservations: true, records);
                return validationFailure;
            }

            var completed = new List<WorkspacePatchRecord>();
            var orderedRecords = rollback ? records.Reverse().ToArray() : records;
            try
            {
                foreach (var record in orderedRecords)
                {
                    var result = await MutateChangeSetChildAsync(request, record, rollback, changeSetId, cancellationToken);
                    if (!result.Success)
                    {
                        return rollback
                            ? await HandleRollbackFailureAsync(request, changeSet, records, completed, record, result, toolName)
                            : await HandleApplyFailureAsync(request, changeSet, records, completed, record, result, toolName);
                    }
                    completed.Add(record);
                }
            }
            catch (OperationCanceledException)
            {
                if (rollback)
                {
                    var restored = await RestoreAppliedChildrenAsync(request, completed, changeSetId);
                    CompleteChangeSet(changeSet,
                        restored.Success && AreAllChildrenInState(records, WorkspacePatchState.Applied)
                            ? WorkspaceChangeSetState.Applied
                            : WorkspaceChangeSetState.Invalidated,
                        releaseReservations: !restored.Success,
                        records);
                }
                else
                {
                    var compensated = await RollbackAppliedChildrenAsync(request, completed, changeSetId);
                    CompleteChangeSet(changeSet,
                        compensated.Success ? WorkspaceChangeSetState.RolledBack : WorkspaceChangeSetState.Invalidated,
                        releaseReservations: true,
                        records);
                }
                throw;
            }

            CompleteChangeSet(changeSet,
                rollback ? WorkspaceChangeSetState.RolledBack : WorkspaceChangeSetState.Applied,
                releaseReservations: false,
                records);
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = true,
                Summary = rollback
                    ? $"Rolled back the approved {records.Length}-file workspace change set."
                    : $"Applied the approved {records.Length}-file workspace change set.",
                Content = BuildChangeSetContent(changeSet, records, preview: false),
                WorkspaceMutation = CreateWorkspaceMutationSnapshot(records, rollback),
            };
        }

        private static async Task<CopilotToolResult?> ValidateChangeSetAsync(
            CopilotAgentRequest request,
            IEnumerable<WorkspacePatchRecord> records,
            bool rollback,
            string toolName,
            CancellationToken cancellationToken)
        {
            foreach (var record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if ((!rollback && record.Operation == WorkspacePatchOperation.Create)
                    || (rollback && record.Operation == WorkspacePatchOperation.Delete))
                {
                    if (!CopilotWorkspacePatchScope.TryResolveNewFile(request, record.FullPath, out _, out _, out var createError))
                    {
                        var createPath = SafeFullPath(record.FullPath);
                        var conflict = File.Exists(createPath) || Directory.Exists(createPath);
                        return Failure(toolName,
                            conflict ? CopilotToolFailureKind.Conflict : CopilotToolFailureKind.Authorization,
                            conflict
                                ? rollback
                                    ? "A deleted-file path was recreated after apply; no files were changed."
                                    : "A change-set creation target appeared after preview; no files were changed."
                                : "A missing-file target is outside the writable workspace; no files were changed.",
                            createError);
                    }
                    continue;
                }

                if (!CopilotWorkspacePatchScope.TryResolve(request, record.FullPath, MaxFileBytes, out var fullPath, out var scopeError))
                {
                    return Failure(toolName, CopilotToolFailureKind.Authorization,
                        "A change-set target is no longer inside the writable workspace; no files were changed.", scopeError);
                }

                byte[] currentBytes;
                try
                {
                    currentBytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return Failure(toolName, CopilotToolFailureKind.Internal,
                        "A change-set target could not be read during whole-set validation; no files were changed.", ex.Message);
                }

                var expectedHash = rollback ? record.AfterSha256 : record.BeforeSha256;
                var currentHash = Hash(currentBytes);
                if (!string.Equals(currentHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                {
                    return Failure(toolName, CopilotToolFailureKind.Conflict,
                        "A workspace file changed after the change-set preview; no files were changed.",
                        $"Path: {fullPath}. Expected SHA-256 {expectedHash}, current SHA-256 {currentHash}.");
                }
            }
            return null;
        }

        private Task<CopilotToolResult> MutateChangeSetChildAsync(
            CopilotAgentRequest request,
            WorkspacePatchRecord record,
            bool rollback,
            string changeSetId,
            CancellationToken cancellationToken)
        {
            var input = new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?> { ["previewId"] = record.PreviewId },
            };
            return MutateAsync(
                request,
                input,
                rollback,
                rollback ? null : record.Operation,
                changeSetId,
                rollback ? "RollbackWorkspacePatchEnvelope" : "ApplyWorkspacePatchEnvelope",
                cancellationToken);
        }

        private async Task<CopilotToolResult> HandleApplyFailureAsync(
            CopilotAgentRequest request,
            WorkspaceChangeSetRecord changeSet,
            WorkspacePatchRecord[] records,
            IReadOnlyList<WorkspacePatchRecord> appliedRecords,
            WorkspacePatchRecord failedRecord,
            CopilotToolResult failure,
            string toolName)
        {
            var compensated = await RollbackAppliedChildrenAsync(request, appliedRecords, changeSet.ChangeSetId);
            CompleteChangeSet(changeSet,
                compensated.Success ? WorkspaceChangeSetState.RolledBack : WorkspaceChangeSetState.Invalidated,
                releaseReservations: true,
                records);
            return CreateChangeSetFailureResult(toolName, changeSet, failedRecord, failure, compensated,
                compensated.Success
                    ? "The workspace change set was not applied; earlier writes were rolled back."
                    : "The workspace change set failed and compensation could not fully restore earlier files.",
                rollback: false);
        }

        private async Task<CopilotToolResult> HandleRollbackFailureAsync(
            CopilotAgentRequest request,
            WorkspaceChangeSetRecord changeSet,
            WorkspacePatchRecord[] records,
            IReadOnlyList<WorkspacePatchRecord> rolledBackRecords,
            WorkspacePatchRecord failedRecord,
            CopilotToolResult failure,
            string toolName)
        {
            var restored = await RestoreAppliedChildrenAsync(request, rolledBackRecords, changeSet.ChangeSetId);
            var fullyApplied = restored.Success && AreAllChildrenInState(records, WorkspacePatchState.Applied);
            CompleteChangeSet(changeSet,
                fullyApplied ? WorkspaceChangeSetState.Applied : WorkspaceChangeSetState.Invalidated,
                releaseReservations: !fullyApplied,
                records);
            return CreateChangeSetFailureResult(toolName, changeSet, failedRecord, failure, restored,
                fullyApplied
                    ? "The workspace change-set rollback failed; already restored files were reapplied so the set remains applied."
                    : "The workspace change-set rollback failed and the prior applied state could not be fully restored.",
                rollback: true);
        }

        private async Task<WorkspaceRecoveryResult> RollbackAppliedChildrenAsync(
            CopilotAgentRequest request,
            IEnumerable<WorkspacePatchRecord> records,
            string changeSetId)
        {
            var attempts = new List<WorkspaceRecoveryAttempt>();
            foreach (var record in records.Reverse())
            {
                try
                {
                    var result = await MutateChangeSetChildAsync(request, record, rollback: true, changeSetId, CancellationToken.None);
                    attempts.Add(new(record.FullPath, result.Success, result.FailureKind, result.ErrorMessage));
                }
                catch (Exception ex)
                {
                    attempts.Add(new(record.FullPath, false, CopilotToolFailureClassifier.Classify(ex), CopilotUserFacingErrorFormatter.Sanitize(ex.Message)));
                }
            }
            return new(attempts);
        }

        private async Task<WorkspaceRecoveryResult> RestoreAppliedChildrenAsync(
            CopilotAgentRequest request,
            IEnumerable<WorkspacePatchRecord> records,
            string changeSetId)
        {
            var attempts = new List<WorkspaceRecoveryAttempt>();
            foreach (var record in records.Reverse())
            {
                lock (_syncRoot)
                {
                    if (record.State == WorkspacePatchState.RolledBack)
                        record.State = WorkspacePatchState.Previewed;
                }
                try
                {
                    var result = await MutateChangeSetChildAsync(request, record, rollback: false, changeSetId, CancellationToken.None);
                    attempts.Add(new(record.FullPath, result.Success, result.FailureKind, result.ErrorMessage));
                }
                catch (Exception ex)
                {
                    attempts.Add(new(record.FullPath, false, CopilotToolFailureClassifier.Classify(ex), CopilotUserFacingErrorFormatter.Sanitize(ex.Message)));
                }
            }
            return new(attempts);
        }

        internal IReadOnlyList<string> GetChangeSetRecheckPaths(CopilotAgentRequest request, CopilotAgentToolInput input, bool rollback)
        {
            EnsureCheckpointRecordsLoaded();
            if (!TryGetChangeSetId(input, out var changeSetId))
                return Array.Empty<string>();
            lock (_syncRoot)
            {
                if (!_changeSets.TryGetValue(changeSetId, out var changeSet)
                    || changeSet.ExpiresAtUtc <= DateTimeOffset.UtcNow
                    || !MatchesCheckpointBinding(request, changeSet)
                    || changeSet.State != (rollback ? WorkspaceChangeSetState.Applied : WorkspaceChangeSetState.Previewed)
                    || !TryResolveChangeSetRecords(changeSet, out var records))
                    return Array.Empty<string>();
                return Array.AsReadOnly(records.Select(record => record.FullPath).ToArray());
            }
        }

        private static CopilotToolResult CreateChangeSetFailureResult(
            string toolName,
            WorkspaceChangeSetRecord changeSet,
            WorkspacePatchRecord failedRecord,
            CopilotToolResult failure,
            WorkspaceRecoveryResult recovery,
            string summary,
            bool rollback)
        {
            var builder = new StringBuilder();
            builder.AppendLine("[Workspace Change Set Recovery]");
            builder.AppendLine($"change_set_id: {changeSet.ChangeSetId}");
            builder.AppendLine($"state: {changeSet.State}");
            builder.AppendLine($"failed_operation: {(rollback ? "rollback" : "apply")}");
            builder.AppendLine($"failed_path: {failedRecord.FullPath}");
            builder.AppendLine($"failure: {CopilotUserFacingErrorFormatter.Sanitize(failure.ErrorMessage)}");
            builder.AppendLine($"recovery_action: {(rollback ? "restore_applied_state" : "undo_completed_writes")}");
            builder.AppendLine($"recovery_attempt_count: {recovery.Attempts.Count}");
            foreach (var attempt in recovery.Attempts)
            {
                builder.AppendLine();
                builder.AppendLine($"path: {attempt.FullPath}");
                builder.AppendLine($"recovery_result: {(attempt.Success ? "succeeded" : "failed")}");
                if (!attempt.Success)
                {
                    builder.AppendLine($"failure_kind: {attempt.FailureKind}");
                    builder.AppendLine($"error: {CopilotUserFacingErrorFormatter.Sanitize(attempt.ErrorMessage)}");
                }
            }
            builder.AppendLine();
            builder.AppendLine("Re-read the attempted paths before describing the current workspace. Recovery results record individual attempts, not a fresh filesystem snapshot. Do not repeat the failed write automatically.");
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = false,
                Summary = summary,
                ErrorMessage = failure.ErrorMessage,
                FailureKind = failure.FailureKind == CopilotToolFailureKind.None ? CopilotToolFailureKind.Internal : failure.FailureKind,
                Content = builder.ToString().TrimEnd(),
                WorkspaceRecheckPaths = recovery.Attempts.Select(attempt => attempt.FullPath).Append(failedRecord.FullPath)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            };
        }

        private sealed record WorkspaceRecoveryAttempt(string FullPath, bool Success, CopilotToolFailureKind FailureKind, string ErrorMessage);

        private sealed record WorkspaceRecoveryResult(IReadOnlyList<WorkspaceRecoveryAttempt> Attempts)
        {
            public bool Success => Attempts.All(attempt => attempt.Success);
        }

        private bool TryResolveChangeSetRecords(WorkspaceChangeSetRecord changeSet, out WorkspacePatchRecord[] records)
        {
            records = changeSet.PreviewIds
                .Select(previewId => _records.TryGetValue(previewId, out var record) ? record : null)
                .Where(record => record != null)
                .Cast<WorkspacePatchRecord>()
                .ToArray();
            return records.Length == changeSet.PreviewIds.Length;
        }

        private void CompleteChangeSet(
            WorkspaceChangeSetRecord changeSet,
            WorkspaceChangeSetState state,
            bool releaseReservations,
            IEnumerable<WorkspacePatchRecord> records)
        {
            var recordArray = records.ToArray();
            lock (_syncRoot)
            {
                changeSet.State = state;
                if (releaseReservations)
                {
                    foreach (var record in recordArray.Where(record => string.Equals(record.ChangeSetId, changeSet.ChangeSetId, StringComparison.Ordinal)))
                        record.ChangeSetId = string.Empty;
                }
            }
            if (state == WorkspaceChangeSetState.Applied)
                PersistCheckpoint(changeSet, recordArray);
            else if (state is WorkspaceChangeSetState.RolledBack or WorkspaceChangeSetState.Invalidated)
                DeleteCheckpoint(changeSet.ChangeSetId);
        }

        private bool AreAllChildrenInState(IEnumerable<WorkspacePatchRecord> records, WorkspacePatchState state)
        {
            lock (_syncRoot)
                return records.All(record => record.State == state);
        }

        private void StoreChangeSet(WorkspaceChangeSetRecord changeSet)
        {
            if (_changeSets.Count >= MaxChangeSets)
            {
                var oldest = _changeSets.Values
                    .Where(item => item.State is not WorkspaceChangeSetState.Applying and not WorkspaceChangeSetState.RollingBack)
                    .OrderBy(item => item.State == WorkspaceChangeSetState.Applied ? 1 : 0)
                    .ThenBy(item => item.CreatedAtUtc)
                    .FirstOrDefault();
                if (oldest != null)
                {
                    ReleaseReservations(oldest);
                    _changeSets.Remove(oldest.ChangeSetId);
                    DeleteCheckpoint(oldest.ChangeSetId);
                }
            }
            _changeSets[changeSet.ChangeSetId] = changeSet;
        }

        private void RemoveExpiredChangeSets(DateTimeOffset now)
        {
            foreach (var changeSet in _changeSets.Values.Where(item => item.ExpiresAtUtc <= now).ToArray())
            {
                ReleaseReservations(changeSet);
                _changeSets.Remove(changeSet.ChangeSetId);
                DeleteCheckpoint(changeSet.ChangeSetId);
            }
        }

        private void ReleaseReservations(WorkspaceChangeSetRecord changeSet)
        {
            foreach (var previewId in changeSet.PreviewIds)
            {
                if (_records.TryGetValue(previewId, out var record)
                    && string.Equals(record.ChangeSetId, changeSet.ChangeSetId, StringComparison.Ordinal))
                {
                    record.ChangeSetId = string.Empty;
                }
            }
        }

        private static bool TryGetChangeSetId(CopilotAgentToolInput input, out string changeSetId)
        {
            return TryGetTextArgument(input, "changeSetId", out changeSetId)
                && changeSetId.StartsWith("workspace-change-set:", StringComparison.Ordinal)
                && changeSetId.Length == "workspace-change-set:".Length + 32;
        }

        private static string BuildChangeSetContent(
            WorkspaceChangeSetRecord changeSet,
            IReadOnlyList<WorkspacePatchRecord> records,
            bool preview)
        {
            var builder = new StringBuilder();
            builder.AppendLine(preview ? "[Workspace Change Set Preview]" : "[Workspace Change Set Result]");
            builder.AppendLine($"change_set_id: {changeSet.ChangeSetId}");
            builder.AppendLine($"file_count: {records.Count}");
            builder.AppendLine($"state: {changeSet.State}");
            builder.AppendLine($"expires_at_utc: {changeSet.ExpiresAtUtc:O}");
            for (var index = 0; index < records.Count; index++)
            {
                var record = records[index];
                builder.AppendLine($"file_{index + 1}_operation: {record.Operation}");
                builder.AppendLine($"file_{index + 1}_path: {record.FullPath}");
                if (record.Operation == WorkspacePatchOperation.Replace)
                    builder.AppendLine($"file_{index + 1}_replacement_count: {record.Replacements.Length}");
                builder.AppendLine($"file_{index + 1}_before_sha256: {record.BeforeSha256}");
                builder.AppendLine($"file_{index + 1}_after_sha256: {record.AfterSha256}");
            }
            return builder.ToString().TrimEnd();
        }

        private sealed class WorkspaceChangeSetRecord
        {
            public string ChangeSetId { get; init; } = string.Empty;
            public string[] PreviewIds { get; init; } = Array.Empty<string>();
            public string ConversationId { get; init; } = string.Empty;
            public string WorkspacePath { get; init; } = string.Empty;
            public DateTimeOffset CreatedAtUtc { get; init; }
            public DateTimeOffset ExpiresAtUtc { get; init; }
            public WorkspaceChangeSetState State { get; set; }
        }

        private enum WorkspaceChangeSetState
        {
            Previewed,
            Applying,
            Applied,
            RollingBack,
            RolledBack,
            Invalidated,
        }
    }
}
