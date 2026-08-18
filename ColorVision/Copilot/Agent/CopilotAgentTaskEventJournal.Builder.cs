using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentTaskEventJournalBuilder
    {
        private readonly object _syncRoot = new();
        private readonly List<CopilotAgentTaskEvent> _events = new();
        private long _nextSequence;

        public CopilotAgentTaskEventJournalBuilder(CopilotAgentTaskEventJournalSnapshot? previous = null, string? runId = null)
        {
            if (previous?.IsStructurallyValid() == true)
                _events.AddRange(previous.Events.TakeLast(CopilotAgentTaskEventJournal.MaxEvents));
            _nextSequence = _events.Count == 0 ? 1 : _events.Max(item => item.Sequence) + 1;
            RunId = CopilotAgentTaskEventIds.IsKey(runId, "run", 32) ? runId! : CopilotAgentTaskEventIds.CreateRunId();
        }

        public string RunId { get; }

        public void RecordRunStarted()
        {
            RecordRunStarted(
                Array.Empty<CopilotBackgroundShellCommandSnapshot>());
        }

        internal void RecordRunStarted(
            IReadOnlyList<CopilotBackgroundShellCommandSnapshot>?
                activeBackgroundCommands)
        {
            var relatedIds = CreateActiveBackgroundCommandRelatedIds(
                activeBackgroundCommands,
                nameof(activeBackgroundCommands));
            lock (_syncRoot)
            {
                var existingStart = _events.LastOrDefault(item =>
                    item.Type == CopilotAgentTaskEventType.RunStarted
                    && string.Equals(item.RunId, RunId, StringComparison.Ordinal));
                if (existingStart != null)
                {
                    if (existingStart.RelatedIds.SequenceEqual(relatedIds, StringComparer.Ordinal))
                        return;
                    throw new InvalidOperationException(
                        $"Agent run {RunId} already started with different background command evidence.");
                }

                Append(
                    CopilotAgentTaskEventType.RunStarted,
                    RunId,
                    "running",
                    relatedIds.Length == 0
                        ? "Agent run started."
                        : $"Agent run started with {relatedIds.Length} active application-managed background command(s).",
                    relatedIds: relatedIds);
            }
        }

        internal void RecordValidationBackgroundCommandSnapshot(
            string callId,
            IReadOnlyList<CopilotBackgroundShellCommandSnapshot>?
                activeBackgroundCommands)
        {
            if (string.IsNullOrWhiteSpace(callId))
                throw new ArgumentException("Validation call ID cannot be empty.", nameof(callId));

            var relatedIds = CreateActiveBackgroundCommandRelatedIds(
                activeBackgroundCommands,
                nameof(activeBackgroundCommands));
            Append(
                CopilotAgentTaskEventType.EvidenceCaptured,
                CopilotAgentTaskEventIds.ForCall(callId),
                CopilotAgentTaskEventJournal.ValidationBackgroundSnapshotState,
                relatedIds.Length == 0
                    ? "No active application-managed background commands were observed when workspace validation started."
                    : $"Workspace validation started with {relatedIds.Length} active application-managed background command(s).",
                "RunWorkspaceValidation",
                relatedIds);
        }

        public void RecordRecovery(CopilotAgentRecoveryRequest recovery)
        {
            ArgumentNullException.ThrowIfNull(recovery);
            if (!recovery.IsStructurallyValid())
                throw new ArgumentException("Agent recovery request is not structurally valid.", nameof(recovery));

            var subjectId = recovery.Mode == CopilotAgentRecoveryMode.RetryRead ? recovery.SourceCallKey : RunId;
            Append(
                CopilotAgentTaskEventType.RecoveryRequested,
                subjectId,
                recovery.Mode.ToString(),
                recovery.Mode switch
                {
                    CopilotAgentRecoveryMode.Finalize => "Recovery retries only the final answer with every tool disabled.",
                    CopilotAgentRecoveryMode.RetryRead => "Recovery may re-evaluate one retry-eligible read failure without replaying stored arguments.",
                    CopilotAgentRecoveryMode.Replan => "Recovery requires a fresh plan against current capabilities.",
                    _ => "Recovery resumes the incomplete task ledger from its checkpoint.",
                },
                recovery.ToolName);
        }

        public void RecordSessionResumed()
        {
            Append(CopilotAgentTaskEventType.SessionResumed, RunId, "resumed", "Agent session and task state resumed from checkpoint.");
        }

        public void RecordReplanRequired(CopilotAgentCheckpointCompatibilityKind reason)
        {
            Append(CopilotAgentTaskEventType.ReplanRequired, RunId, reason.ToString(), "Persisted task state was discarded and must be replanned.");
        }

        public void RecordTaskLedger(CopilotAgentTaskLedgerSnapshot ledger, string phase)
        {
            ArgumentNullException.ThrowIfNull(ledger);
            var items = ledger.Items ?? Array.Empty<CopilotAgentTaskItem>();
            var completedCount = items.Count(item => item?.IsComplete == true);
            var relatedIds = items.Where(item => item != null).Select(item => $"task:{Math.Max(0, item.Id)}");
            Append(
                CopilotAgentTaskEventType.TaskLedgerCaptured,
                RunId,
                phase,
                $"Task ledger {completedCount}/{items.Count} complete in {ledger.Mode} mode.",
                relatedIds: relatedIds);
        }

        public void RecordApprovalDecision(CopilotToolExecutionInfo execution, bool approved)
        {
            ArgumentNullException.ThrowIfNull(execution);
            RecordApprovalDecision(execution.ToolName, execution.CallId, execution.ApprovalActionId, approved);
        }

        public void RecordApprovalDecision(
            string toolName,
            string callId,
            string approvalActionId,
            bool approved,
            string decisionSource = "")
        {
            var approvalId = CopilotAgentTaskEventIds.ForApproval(approvalActionId);
            var source = (decisionSource ?? string.Empty).Trim();
            AppendUnique(
                approved ? CopilotAgentTaskEventType.ApprovalApproved : CopilotAgentTaskEventType.ApprovalDenied,
                approvalId,
                approved
                    ? string.IsNullOrWhiteSpace(source) ? "approved" : "approved:" + source
                    : "denied",
                approved
                    ? string.Equals(source, nameof(CopilotFrameworkApprovalDecisionSource.AutomaticReview), StringComparison.Ordinal)
                        ? "Protected tool call was approved by automatic permission review."
                        : string.Equals(source, nameof(CopilotFrameworkApprovalDecisionSource.TemporaryGrant), StringComparison.Ordinal)
                            ? "Protected tool call was approved by the temporary task grant."
                            : "Protected tool call was approved by the user."
                    : "Protected tool call was denied or expired.",
                toolName,
                [CopilotAgentTaskEventIds.ForCall(callId)],
                uniqueTypes:
                [
                    CopilotAgentTaskEventType.ApprovalApproved,
                    CopilotAgentTaskEventType.ApprovalDenied,
                ]);
        }

        public void RecordSteering(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Steering message cannot be empty.", nameof(message));
            Append(
                CopilotAgentTaskEventType.SteeringQueued,
                CopilotAgentTaskEventIds.ForSteering(message),
                "queued",
                "A user steering instruction was queued for the active Agent session.");
        }

        public void RecordSteeringDelivered(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                throw new ArgumentException("Steering message cannot be empty.", nameof(message));
            Append(
                CopilotAgentTaskEventType.SteeringDelivered,
                CopilotAgentTaskEventIds.ForSteering(message),
                "delivered",
                "A queued user steering instruction was delivered to the Agent provider.");
        }

        internal void RecordBackgroundShellCommandCompletion(
            CopilotBackgroundShellCommandSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var evidence =
                CopilotBackgroundShellCommandEvidence.FromSnapshot(snapshot);
            if (!evidence.IsStructurallyValid() || !evidence.IsTerminal)
            {
                throw new ArgumentException(
                    "A terminal background command snapshot is required.",
                    nameof(snapshot));
            }

            RecordBackgroundShellCommandCompletion(evidence);
        }

        internal void RecordBackgroundShellCommandOutput(
            CopilotBackgroundShellOutputMonitorEventArgs eventArgs)
        {
            ArgumentNullException.ThrowIfNull(eventArgs);
            var monitor = eventArgs.Monitor;
            if (!monitor.IsActive
                || string.IsNullOrWhiteSpace(monitor.Id)
                || string.IsNullOrWhiteSpace(monitor.BackgroundId)
                || string.IsNullOrWhiteSpace(eventArgs.Content))
            {
                throw new ArgumentException(
                    "An active background output monitor event is required.",
                    nameof(eventArgs));
            }

            var stream =
                monitor.Stream
                    == CopilotBackgroundShellOutputStream.StandardError
                    ? "stderr"
                    : "stdout";
            Append(
                CopilotAgentTaskEventType.BackgroundCommandOutputObserved,
                CopilotAgentTaskEventIds.ForBackgroundOutputMonitor(
                    monitor.Id),
                stream,
                eventArgs.SuppressedEvents > 0
                    ? $"A bounded redacted background {stream} monitor event was queued after suppressing {eventArgs.SuppressedEvents} earlier event(s)."
                    : $"A bounded redacted background {stream} monitor event was queued.",
                relatedIds:
                [
                    CopilotAgentTaskEventIds.ForBackgroundCommand(
                        monitor.BackgroundId),
                ]);
        }

        public void RecordEvidence(CopilotAgentEvidenceArtifact artifact)
        {
            ArgumentNullException.ThrowIfNull(artifact);
            if (!artifact.IsStructurallyValid())
                throw new ArgumentException("Evidence artifact is not structurally valid.", nameof(artifact));
            var related = new[] { artifact.SourceCallKey, artifact.ResourceKey }
                .Where(value => !string.IsNullOrWhiteSpace(value));
            Append(
                CopilotAgentTaskEventType.EvidenceCaptured,
                artifact.Id,
                "captured",
                artifact.Summary,
                artifact.ToolName,
                related,
                artifact.CapturedAtUtc);
        }

        public void RecordStop(CopilotAgentStopReason reason)
        {
            if (!Enum.IsDefined(reason) || reason == CopilotAgentStopReason.None)
                throw new ArgumentOutOfRangeException(nameof(reason));
            lock (_syncRoot)
            {
                var existingStop = _events.LastOrDefault(item =>
                    item.Type == CopilotAgentTaskEventType.RunStopped
                    && string.Equals(item.RunId, RunId, StringComparison.Ordinal));
                if (existingStop != null)
                {
                    if (string.Equals(existingStop.State, reason.ToString(), StringComparison.Ordinal))
                        return;
                    throw new InvalidOperationException(
                        $"Agent run {RunId} already stopped with reason {existingStop.State}.");
                }
                var control = _events.LastOrDefault(item =>
                    (item.Type is CopilotAgentTaskEventType.PauseRequested
                        or CopilotAgentTaskEventType.CancelRequested)
                    && string.Equals(item.RunId, RunId, StringComparison.Ordinal));
                var expectedReason = control?.Type switch
                {
                    CopilotAgentTaskEventType.PauseRequested => CopilotAgentStopReason.Paused,
                    CopilotAgentTaskEventType.CancelRequested => CopilotAgentStopReason.Cancelled,
                    _ => (CopilotAgentStopReason?)null,
                };
                if (expectedReason.HasValue && reason != expectedReason.Value)
                {
                    throw new InvalidOperationException(
                        $"Agent run {RunId} control event {control!.Type} requires stop reason {expectedReason.Value}, not {reason}.");
                }
                CloseDanglingToolExecutions(reason);
                CloseDanglingUserQuestions(reason);
                Append(CopilotAgentTaskEventType.RunStopped, RunId, reason.ToString(), $"Agent run stopped with reason {reason}.");
            }
        }

        public void RecordBlocker(CopilotAgentBlockerSnapshot blocker)
        {
            ArgumentNullException.ThrowIfNull(blocker);
            if (!blocker.IsStructurallyValid())
                throw new ArgumentException("Agent blocker is not structurally valid.", nameof(blocker));

            Append(
                CopilotAgentTaskEventType.BlockerDetected,
                string.IsNullOrWhiteSpace(blocker.SourceCallKey) ? RunId : blocker.SourceCallKey,
                blocker.Code,
                blocker.Summary,
                blocker.ToolName);
        }

        public void RecordControl(CopilotAgentControlIntent intent)
        {
            if (intent is not (CopilotAgentControlIntent.Pause or CopilotAgentControlIntent.Cancel))
                throw new ArgumentOutOfRangeException(nameof(intent));
            AppendUnique(
                intent == CopilotAgentControlIntent.Pause ? CopilotAgentTaskEventType.PauseRequested : CopilotAgentTaskEventType.CancelRequested,
                RunId,
                intent.ToString(),
                intent == CopilotAgentControlIntent.Pause
                    ? "The user paused the active Agent run at a cancellation boundary."
                    : "The user cancelled the active Agent run and discarded its new checkpoint.",
                uniqueTypes:
                [
                    CopilotAgentTaskEventType.PauseRequested,
                    CopilotAgentTaskEventType.CancelRequested,
                ]);
        }

        public void Observe(CopilotAgentEvent agentEvent)
        {
            if (agentEvent == null)
                return;

            var execution = agentEvent.ToolExecution;
            if (agentEvent.Type == CopilotAgentEventType.ToolStarted && execution != null)
            {
                AppendUnique(
                    CopilotAgentTaskEventType.ToolStarted,
                    CopilotAgentTaskEventIds.ForCall(execution.CallId),
                    execution.State.ToString(),
                    "Tool execution started.",
                    execution.ToolName,
                    occurredAtUtc: execution.StartedAtUtc == default ? null : execution.StartedAtUtc);
                return;
            }

            if (agentEvent.Type == CopilotAgentEventType.ToolResult && execution != null)
            {
                var type = execution.State switch
                {
                    CopilotToolExecutionState.AwaitingApproval => CopilotAgentTaskEventType.ApprovalRequested,
                    CopilotToolExecutionState.Denied => CopilotAgentTaskEventType.ApprovalDenied,
                    _ => CopilotAgentTaskEventType.ToolCompleted,
                };
                var callId = CopilotAgentTaskEventIds.ForCall(execution.CallId);
                var subjectId = type is CopilotAgentTaskEventType.ApprovalRequested or CopilotAgentTaskEventType.ApprovalDenied
                    ? CopilotAgentTaskEventIds.ForApproval(execution.ApprovalActionId)
                    : callId;
                var backgroundCommands = SelectBackgroundShellCommandEvidence(
                    execution,
                    agentEvent.ToolResult);
                var related = (subjectId == callId
                        ? Array.Empty<string>()
                        : [callId])
                    .Concat(backgroundCommands.Select(item =>
                        CopilotAgentTaskEventIds.ForBackgroundCommand(item.Id)));
                AppendUnique(
                    type,
                    subjectId,
                    execution.State.ToString(),
                    agentEvent.ToolResult?.Summary ?? agentEvent.Text,
                    execution.ToolName,
                    related,
                    execution.CompletedAtUtc ?? (execution.StartedAtUtc == default ? null : execution.StartedAtUtc),
                    agentEvent.ToolResult?.Success == false ? agentEvent.ToolResult.FailureCode : string.Empty,
                    uniqueTypes: type == CopilotAgentTaskEventType.ApprovalDenied
                        ? [
                            CopilotAgentTaskEventType.ApprovalApproved,
                            CopilotAgentTaskEventType.ApprovalDenied,
                        ]
                        : null);
                foreach (var backgroundCommand in backgroundCommands.Where(item => item.IsTerminal))
                    RecordBackgroundShellCommandCompletion(backgroundCommand);
                return;
            }

            if (agentEvent.Type == CopilotAgentEventType.Error)
            {
                Append(CopilotAgentTaskEventType.RuntimeError, RunId, "error", agentEvent.Text);
                return;
            }

            if ((agentEvent.Type is CopilotAgentEventType.UserQuestionRequested
                    or CopilotAgentEventType.UserQuestionResolved)
                && agentEvent.UserQuestion?.IsStructurallyValid() == true)
            {
                var question = agentEvent.UserQuestion;
                var requested = agentEvent.Type == CopilotAgentEventType.UserQuestionRequested;
                AppendUnique(
                    requested
                        ? CopilotAgentTaskEventType.UserQuestionRequested
                        : CopilotAgentTaskEventType.UserQuestionResolved,
                    CopilotAgentTaskEventIds.ForUserQuestion(question.RequestId),
                    requested ? "pending" : question.Resolution.ToString(),
                    requested
                        ? "The Agent requested one structured user clarification."
                        : question.Resolution == CopilotUserQuestionResolution.Answered
                            ? "The structured user clarification was answered."
                            : "The structured user clarification was cancelled.");
            }
        }

        public CopilotAgentTaskEventJournalSnapshot Snapshot()
        {
            lock (_syncRoot)
            {
                return new CopilotAgentTaskEventJournalSnapshot
                {
                    Events = _events.ToArray(),
                };
            }
        }

        private void CloseDanglingToolExecutions(CopilotAgentStopReason stopReason)
        {
            var latestStarts = _events
                .Where(item => item.Type == CopilotAgentTaskEventType.ToolStarted
                    && string.Equals(item.RunId, RunId, StringComparison.Ordinal))
                .GroupBy(item => item.SubjectId, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(item => item.Sequence).First())
                .Where(start => !_events.Any(item =>
                    string.Equals(item.RunId, RunId, StringComparison.Ordinal)
                    && item.Sequence > start.Sequence
                    && IsTerminalToolEvent(item, start.SubjectId)))
                .OrderBy(item => item.Sequence)
                .ToArray();
            if (latestStarts.Length == 0)
                return;

            var cancelled = stopReason == CopilotAgentStopReason.Cancelled;
            var state = cancelled
                ? CopilotToolExecutionState.Cancelled.ToString()
                : CopilotToolExecutionState.Interrupted.ToString();
            var failureCode = cancelled
                ? "tool_execution_cancelled"
                : CopilotToolFailureCode.OutcomeUnknown;
            var summary = cancelled
                ? "Tool execution was cancelled before a terminal result was recorded."
                : "Tool execution started but was interrupted before a terminal result was recorded; its external outcome is unknown.";
            foreach (var start in latestStarts)
            {
                Append(
                    CopilotAgentTaskEventType.ToolCompleted,
                    start.SubjectId,
                    state,
                    summary,
                    start.ToolName,
                    failureCode: failureCode);
            }
        }

        private static bool IsTerminalToolEvent(CopilotAgentTaskEvent item, string callSubjectId)
        {
            return (item.Type == CopilotAgentTaskEventType.ToolCompleted
                    && string.Equals(item.SubjectId, callSubjectId, StringComparison.Ordinal))
                || ((item.Type is CopilotAgentTaskEventType.ApprovalRequested
                        or CopilotAgentTaskEventType.ApprovalDenied)
                    && (string.Equals(item.SubjectId, callSubjectId, StringComparison.Ordinal)
                        || item.RelatedIds.Contains(callSubjectId, StringComparer.Ordinal)));
        }

        private void CloseDanglingUserQuestions(CopilotAgentStopReason stopReason)
        {
            var pendingRequests = _events
                .Where(item => item.Type == CopilotAgentTaskEventType.UserQuestionRequested
                    && string.Equals(item.RunId, RunId, StringComparison.Ordinal))
                .GroupBy(item => item.SubjectId, StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(item => item.Sequence).First())
                .Where(request => !_events.Any(item =>
                    item.Type == CopilotAgentTaskEventType.UserQuestionResolved
                    && string.Equals(item.RunId, RunId, StringComparison.Ordinal)
                    && string.Equals(item.SubjectId, request.SubjectId, StringComparison.Ordinal)
                    && item.Sequence > request.Sequence))
                .OrderBy(item => item.Sequence)
                .ToArray();
            if (pendingRequests.Length == 0)
                return;

            var summary = stopReason == CopilotAgentStopReason.Cancelled
                ? "The structured user clarification was cancelled before a terminal response was recorded."
                : "The structured user clarification was interrupted before a terminal response was recorded; the pending request was closed without an answer.";
            foreach (var request in pendingRequests)
            {
                Append(
                    CopilotAgentTaskEventType.UserQuestionResolved,
                    request.SubjectId,
                    CopilotUserQuestionResolution.Cancelled.ToString(),
                    summary);
            }
        }

        private void RecordBackgroundShellCommandCompletion(
            CopilotBackgroundShellCommandEvidence evidence)
        {
            AppendUnique(
                CopilotAgentTaskEventType.BackgroundCommandCompleted,
                CopilotAgentTaskEventIds.ForBackgroundCommand(evidence.Id),
                evidence.State.ToString().ToLowerInvariant(),
                evidence.ExitCode.HasValue
                    ? $"An application-managed background command reached a terminal state with exit code {evidence.ExitCode.Value}."
                    : "An application-managed background command reached a terminal state.",
                exitCode: evidence.ExitCode);
        }

        private static CopilotBackgroundShellCommandEvidence[]
            SelectBackgroundShellCommandEvidence(
                CopilotToolExecutionInfo execution,
                CopilotToolResult? result)
        {
            if (execution.State != CopilotToolExecutionState.Completed
                || result?.Success != true
                || !string.Equals(
                    execution.ToolName,
                    result.ToolName,
                    StringComparison.Ordinal))
            {
                return Array.Empty<CopilotBackgroundShellCommandEvidence>();
            }

            return (result.BackgroundShellCommands
                    ?? Array.Empty<CopilotBackgroundShellCommandEvidence>())
                .Where(item => item.IsStructurallyValid())
                .GroupBy(item => item.Id, StringComparer.Ordinal)
                .Select(group => group.Last())
                .Take(CopilotBackgroundShellCommandRegistry.MaximumRetainedCommands)
                .ToArray();
        }

        private void Append(
            CopilotAgentTaskEventType type,
            string subjectId,
            string state,
            string summary,
            string toolName = "",
            IEnumerable<string>? relatedIds = null,
            DateTimeOffset? occurredAtUtc = null,
            string failureCode = "",
            int? exitCode = null)
        {
            lock (_syncRoot)
            {
                if (type != CopilotAgentTaskEventType.RunStopped
                    && _events.Any(item =>
                        item.Type == CopilotAgentTaskEventType.RunStopped
                        && string.Equals(item.RunId, RunId, StringComparison.Ordinal)))
                {
                    throw new InvalidOperationException(
                        $"Agent run {RunId} is already stopped and cannot accept more events.");
                }

                var timestamp = occurredAtUtc ?? DateTimeOffset.UtcNow;
                var sequence = _nextSequence++;
                var item = new CopilotAgentTaskEvent
                {
                    Sequence = sequence,
                    Id = CopilotAgentTaskEventIds.CreateEventId(sequence, RunId, type, timestamp),
                    Type = type,
                    OccurredAtUtc = timestamp,
                    RunId = RunId,
                    SubjectId = NormalizeIdentifier(subjectId, RunId),
                    RelatedIds = (relatedIds ?? Array.Empty<string>())
                        .Select(value => NormalizeIdentifier(value, string.Empty))
                        .Where(value => value.Length > 0)
                        .Distinct(StringComparer.Ordinal)
                        .Take(CopilotAgentTaskEventJournal.MaxRelatedIds)
                        .ToArray(),
                    ToolName = SanitizeText(toolName, CopilotAgentTaskEventJournal.MaxToolNameLength, collapseWhitespace: true),
                    State = SanitizeText(state, CopilotAgentTaskEventJournal.MaxStateLength, collapseWhitespace: true),
                    FailureCode = CopilotToolFailureCode.Normalize(failureCode),
                    ExitCode = exitCode,
                    Summary = SanitizeText(summary, CopilotAgentTaskEventJournal.MaxSummaryLength, collapseWhitespace: true),
                };
                if (!item.IsStructurallyValid())
                    throw new InvalidOperationException("Agent task event could not be normalized into a valid journal entry.");
                _events.Add(item);
                TrimToCapacity();
            }
        }

        private void AppendUnique(
            CopilotAgentTaskEventType type,
            string subjectId,
            string state,
            string summary,
            string toolName = "",
            IEnumerable<string>? relatedIds = null,
            DateTimeOffset? occurredAtUtc = null,
            string failureCode = "",
            int? exitCode = null,
            IReadOnlyCollection<CopilotAgentTaskEventType>? uniqueTypes = null)
        {
            lock (_syncRoot)
            {
                var normalizedSubjectId = NormalizeIdentifier(subjectId, RunId);
                var normalizedRelatedIds = (relatedIds ?? Array.Empty<string>())
                    .Select(value => NormalizeIdentifier(value, string.Empty))
                    .Where(value => value.Length > 0)
                    .Distinct(StringComparer.Ordinal)
                    .Take(CopilotAgentTaskEventJournal.MaxRelatedIds)
                    .ToArray();
                var normalizedState = SanitizeText(
                    state,
                    CopilotAgentTaskEventJournal.MaxStateLength,
                    collapseWhitespace: true);
                var normalizedSummary = SanitizeText(
                    summary,
                    CopilotAgentTaskEventJournal.MaxSummaryLength,
                    collapseWhitespace: true);
                var normalizedToolName = SanitizeText(
                    toolName,
                    CopilotAgentTaskEventJournal.MaxToolNameLength,
                    collapseWhitespace: true);
                var normalizedFailureCode = CopilotToolFailureCode.Normalize(
                    failureCode);
                var eventTypes = uniqueTypes ?? [type];
                var existing = _events.LastOrDefault(item =>
                    eventTypes.Contains(item.Type)
                    && string.Equals(item.RunId, RunId, StringComparison.Ordinal)
                    && string.Equals(
                        item.SubjectId,
                        normalizedSubjectId,
                        StringComparison.Ordinal));
                if (existing != null)
                {
                    if (existing.Type == type
                        && string.Equals(existing.State, normalizedState, StringComparison.Ordinal)
                        && string.Equals(existing.Summary, normalizedSummary, StringComparison.Ordinal)
                        && string.Equals(existing.ToolName, normalizedToolName, StringComparison.Ordinal)
                        && string.Equals(existing.FailureCode, normalizedFailureCode, StringComparison.Ordinal)
                        && existing.ExitCode == exitCode
                        && existing.RelatedIds.SequenceEqual(
                            normalizedRelatedIds,
                            StringComparer.Ordinal))
                    {
                        return;
                    }

                    throw new InvalidOperationException(
                        $"Agent run {RunId} already recorded a conflicting {existing.Type} event for {normalizedSubjectId}.");
                }

                Append(
                    type,
                    normalizedSubjectId,
                    normalizedState,
                    normalizedSummary,
                    normalizedToolName,
                    normalizedRelatedIds,
                    occurredAtUtc,
                    normalizedFailureCode,
                    exitCode);
            }
        }

        private void TrimToCapacity()
        {
            while (_events.Count > CopilotAgentTaskEventJournal.MaxEvents)
            {
                var index = _events.FindIndex(item =>
                    item.Type
                        == CopilotAgentTaskEventType.BackgroundCommandOutputObserved);
                if (index < 0)
                    index = FindOldestNonAuditSpineEvent();
                _events.RemoveAt(index < 0 ? 0 : index);
            }
        }

        private int FindOldestNonAuditSpineEvent()
        {
            var latestValidationSnapshot = _events.LastOrDefault(item =>
                string.Equals(item.RunId, RunId, StringComparison.Ordinal)
                && item.Type == CopilotAgentTaskEventType.EvidenceCaptured
                && string.Equals(
                    item.State,
                    CopilotAgentTaskEventJournal.ValidationBackgroundSnapshotState,
                    StringComparison.Ordinal)
                && string.Equals(
                    item.ToolName,
                    "RunWorkspaceValidation",
                    StringComparison.Ordinal));
            var latestValidationStart = latestValidationSnapshot == null
                ? null
                : _events.FirstOrDefault(item =>
                    item.Sequence == latestValidationSnapshot.Sequence + 1
                    && string.Equals(item.RunId, RunId, StringComparison.Ordinal)
                    && item.Type == CopilotAgentTaskEventType.ToolStarted
                    && string.Equals(
                        item.SubjectId,
                        latestValidationSnapshot.SubjectId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        item.ToolName,
                        "RunWorkspaceValidation",
                        StringComparison.Ordinal));
            var latestValidationCompletion = latestValidationStart == null
                ? null
                : _events.FirstOrDefault(item =>
                    item.Sequence > latestValidationStart.Sequence
                    && string.Equals(item.RunId, RunId, StringComparison.Ordinal)
                    && item.Type == CopilotAgentTaskEventType.ToolCompleted
                    && string.Equals(
                        item.SubjectId,
                        latestValidationStart.SubjectId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        item.ToolName,
                        "RunWorkspaceValidation",
                        StringComparison.Ordinal));
            for (var index = 0; index < _events.Count; index++)
            {
                var item = _events[index];
                if (item.Type == CopilotAgentTaskEventType.RunStarted
                    && string.Equals(item.RunId, RunId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (ReferenceEquals(item, latestValidationSnapshot)
                    || ReferenceEquals(item, latestValidationStart)
                    || ReferenceEquals(item, latestValidationCompletion))
                {
                    continue;
                }

                return index;
            }

            return -1;
        }

        private static string[] CreateActiveBackgroundCommandRelatedIds(
            IReadOnlyList<CopilotBackgroundShellCommandSnapshot>?
                activeBackgroundCommands,
            string parameterName)
        {
            var snapshots = (activeBackgroundCommands
                    ?? Array.Empty<CopilotBackgroundShellCommandSnapshot>())
                .ToArray();
            if (snapshots.Any(snapshot => snapshot?.IsActive != true))
            {
                throw new ArgumentException(
                    "Only active background command snapshots can be captured.",
                    parameterName);
            }

            var evidence = snapshots
                .Select(CopilotBackgroundShellCommandEvidence.FromSnapshot)
                .ToArray();
            if (evidence.Any(item => !item.IsStructurallyValid()))
            {
                throw new ArgumentException(
                    "Every active background command snapshot must be structurally valid.",
                    parameterName);
            }

            var relatedIds = evidence
                .Select(item =>
                    CopilotAgentTaskEventIds.ForBackgroundCommand(item.Id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (relatedIds.Length > CopilotAgentTaskEventJournal.MaxRelatedIds)
            {
                throw new ArgumentException(
                    "Too many active background commands were captured for one task event.",
                    parameterName);
            }

            return relatedIds;
        }

        private static string NormalizeIdentifier(string? value, string fallback)
        {
            var normalized = new string((value ?? string.Empty)
                .Where(character => char.IsLetterOrDigit(character) || character is ':' or '-' or '_' or '.')
                .Take(CopilotAgentTaskEventJournal.MaxIdentifierLength)
                .ToArray());
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        private static string SanitizeText(string? value, int maximumLength, bool collapseWhitespace)
        {
            var sanitized = CopilotMcpAuditLogger.RedactText(value ?? string.Empty).Replace("\0", string.Empty, StringComparison.Ordinal);
            if (collapseWhitespace)
                sanitized = string.Join(" ", sanitized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (sanitized.Length <= maximumLength)
                return sanitized;
            return maximumLength <= 3 ? sanitized[..maximumLength] : sanitized[..(maximumLength - 3)] + "...";
        }
    }
}
