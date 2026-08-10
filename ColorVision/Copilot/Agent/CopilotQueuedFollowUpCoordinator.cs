using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed record CopilotQueuedFollowUpRequest(
        string ConversationId,
        string ConversationTitle,
        string Prompt,
        CopilotAgentMode Mode,
        CopilotProfileConfig Profile,
        CopilotAgentHostContextSnapshot SubmissionContext,
        CopilotAgentSkillReference? AgentSkillReference,
        CopilotTurnRuntimeConfigSnapshot RuntimeConfigSnapshot,
        CopilotWorkspaceReviewTargetContext? WorkspaceReviewTarget,
        string GoalId = "",
        DateTimeOffset? QueuedAtUtc = null)
    {
        public CopilotQueuedFollowUp Create(string runId) => new(
            runId,
            ConversationId,
            ConversationTitle,
            Prompt,
            Mode,
            Profile,
            SubmissionContext,
            GoalId,
            AgentSkillReference,
            RuntimeConfigSnapshot,
            WorkspaceReviewTarget,
            QueuedAtUtc);
    }

    internal readonly record struct CopilotQueuedFollowUpRemovalResult(
        bool QueueChanged,
        bool RecoveryChanged)
    {
        public bool Changed => QueueChanged || RecoveryChanged;
    }

    internal sealed class CopilotQueuedFollowUpCoordinator
    {
        private readonly CopilotChatState _state;
        private readonly CopilotAgentTaskHost _taskHost;
        private readonly Dictionary<string, CopilotQueuedFollowUp> _itemsByRunId =
            new(StringComparer.Ordinal);
        private bool _isShuttingDown;

        public CopilotQueuedFollowUpCoordinator(
            CopilotChatState state,
            CopilotAgentTaskHost taskHost)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _taskHost = taskHost ?? throw new ArgumentNullException(nameof(taskHost));
            _state.QueuedFollowUpRecoveries ??= new ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>();
        }

        public event EventHandler? Changed;

        public ObservableCollection<CopilotQueuedFollowUp> Items { get; } = new();

        public int MaxQueuedRuns => _taskHost.MaxQueuedRuns;

        public IReadOnlyList<CopilotHostedAgentRun> ScheduledRuns => _taskHost.ScheduledRuns;

        public bool HasConversation(string conversationId) => _itemsByRunId.Values.Any(item =>
            string.Equals(item.ConversationId, conversationId, StringComparison.Ordinal));

        public bool TryGet(string runId, out CopilotQueuedFollowUp? item) =>
            _itemsByRunId.TryGetValue(runId, out item);

        public IReadOnlyList<CopilotQueuedFollowUpRecoveryRecord> GetResumableRecoveries() =>
            _state.QueuedFollowUpRecoveries
                .Where(record => record?.ResumeAfterRestart == true)
                .Take(MaxQueuedRuns)
                .ToArray();

        public IEnumerable<CopilotAttachmentItem> EnumerateReferencedAttachments() =>
            _state.QueuedFollowUpRecoveries
                .Where(recovery => recovery != null)
                .SelectMany(recovery => recovery.EnumerateReferencedAttachments())
                .Concat(Items
                    .Where(item => item != null)
                    .SelectMany(item => item.SubmissionContext.Attachments));

        public bool RemoveRecoveriesForConversation(string conversationId)
        {
            var changed = false;
            for (var index = _state.QueuedFollowUpRecoveries.Count - 1; index >= 0; index--)
            {
                if (!string.Equals(
                    _state.QueuedFollowUpRecoveries[index]?.ConversationId,
                    conversationId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                _state.QueuedFollowUpRecoveries.RemoveAt(index);
                changed = true;
            }
            return changed;
        }

        public void RecordStartupRecovery(int resumedCount, int restoredDraftCount)
        {
            _state.ResumedQueuedFollowUpCount = Math.Max(0, resumedCount);
            _state.RecoveredQueuedFollowUpCount += Math.Max(0, restoredDraftCount);
        }

        public CopilotRequestAdmissionResult EvaluateAdmission(
            string conversationId,
            CopilotAgentMode mode) =>
            _taskHost.EvaluateFollowUpAdmission(conversationId, mode);

        public bool TrySchedule(
            CopilotQueuedFollowUpRequest request,
            bool runNext,
            Func<CopilotHostedAgentRun, CopilotQueuedFollowUp, Task> executeAsync,
            out CopilotQueuedFollowUp? item,
            out CopilotRequestAdmissionResult admission)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(executeAsync);

            var itemReady = new TaskCompletionSource<CopilotQueuedFollowUp>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            async Task ExecuteAsync(CopilotHostedAgentRun run)
            {
                var queuedItem = await itemReady.Task.ConfigureAwait(false);
                await executeAsync(run, queuedItem).ConfigureAwait(false);
            }

            CopilotHostedAgentRun? queuedRun;
            var scheduled = runNext
                ? _taskHost.TryScheduleFollowUpNext(
                    request.ConversationId,
                    request.Mode,
                    ExecuteAsync,
                    out queuedRun,
                    out admission)
                : _taskHost.TryScheduleFollowUp(
                    request.ConversationId,
                    request.Mode,
                    ExecuteAsync,
                    out queuedRun,
                    out admission);
            if (!scheduled || queuedRun == null)
            {
                item = null;
                return false;
            }

            item = request.Create(queuedRun.Id);
            try
            {
                Register(item, addRecoveryRecord: true);
            }
            catch
            {
                itemReady.TrySetCanceled();
                _taskHost.RequestCancel(queuedRun.Id);
                throw;
            }
            itemReady.SetResult(item);
            if (runNext)
                SynchronizeRecoveryOrder();
            return true;
        }

        public bool TryRestore(
            CopilotQueuedFollowUp item,
            Func<CopilotHostedAgentRun, CopilotQueuedFollowUp, Task> executeAsync)
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentNullException.ThrowIfNull(executeAsync);
            if (_itemsByRunId.ContainsKey(item.RunId))
                return false;

            var itemReady = new TaskCompletionSource<CopilotQueuedFollowUp>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_taskHost.TryRestoreQueuedFollowUp(
                item.RunId,
                item.ConversationId,
                item.Mode,
                item.QueuedAtUtc,
                async run =>
                {
                    var queuedItem = await itemReady.Task.ConfigureAwait(false);
                    await executeAsync(run, queuedItem).ConfigureAwait(false);
                },
                out var restoredRun))
            {
                return false;
            }

            try
            {
                Register(item, addRecoveryRecord: false);
            }
            catch
            {
                itemReady.TrySetCanceled();
                if (restoredRun != null)
                    _taskHost.RequestCancel(restoredRun.Id);
                throw;
            }
            itemReady.SetResult(item);
            return true;
        }

        public bool TryPromote(string runId)
        {
            return _taskHost.PromoteQueuedRun(runId);
        }

        public bool TryMove(string runId, int offset)
        {
            return _taskHost.MoveQueuedRun(runId, offset);
        }

        public bool TryStart(string runId) => _taskHost.TryStartQueuedRun(runId);

        public bool RequestCancel(string runId) => _taskHost.RequestCancel(runId);

        public int GetQueuePosition(string runId) => _taskHost.GetQueuePosition(runId);

        public CopilotQueuedFollowUpRemovalResult Remove(
            string runId,
            bool removeRecoveryRecord)
        {
            var queueChanged = false;
            if (_itemsByRunId.Remove(runId, out var item))
            {
                Items.Remove(item);
                queueChanged = true;
            }

            var recoveryChanged = removeRecoveryRecord
                && !_isShuttingDown
                && RemoveRecovery(runId);
            if (queueChanged)
                RefreshPositions();
            return new CopilotQueuedFollowUpRemovalResult(queueChanged, recoveryChanged);
        }

        public CopilotQueuedFollowUpRemovalResult HandleTaskHostChanged(
            CopilotAgentTaskHostChangedEventArgs e)
        {
            ArgumentNullException.ThrowIfNull(e);

            var result = e.Kind switch
            {
                CopilotAgentTaskHostChangeKind.Started => Remove(
                    e.Run.Id,
                    removeRecoveryRecord: false),
                CopilotAgentTaskHostChangeKind.Completed => Remove(
                    e.Run.Id,
                    removeRecoveryRecord: true),
                _ => default,
            };
            if (e.Kind is not (CopilotAgentTaskHostChangeKind.QueueChanged
                or CopilotAgentTaskHostChangeKind.Started
                or CopilotAgentTaskHostChangeKind.Completed))
            {
                return result;
            }

            var recoveryChanged = result.RecoveryChanged | SynchronizeRecoveryOrder();
            if (!result.QueueChanged)
                RefreshPositions();
            return new CopilotQueuedFollowUpRemovalResult(
                result.QueueChanged,
                recoveryChanged);
        }

        public bool RestoreRecoveryToDraft(string runId) =>
            CopilotQueuedFollowUpRecovery.RestoreRecordToDraft(_state, runId);

        public bool RemoveRecovery(string runId)
        {
            var changed = false;
            for (var index = _state.QueuedFollowUpRecoveries.Count - 1; index >= 0; index--)
            {
                if (!string.Equals(
                    _state.QueuedFollowUpRecoveries[index]?.RunId,
                    runId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                _state.QueuedFollowUpRecoveries.RemoveAt(index);
                changed = true;
            }
            return changed;
        }

        public bool PreserveForRestart()
        {
            var knownRunIds = _state.QueuedFollowUpRecoveries
                .Where(record => record != null)
                .Select(record => record.RunId)
                .ToHashSet(StringComparer.Ordinal);
            var changed = false;
            foreach (var item in Items.OrderBy(candidate => candidate.QueuePosition))
            {
                if (!knownRunIds.Add(item.RunId))
                    continue;

                AddRecovery(item);
                changed = true;
            }
            return changed;
        }

        public bool BeginShutdown()
        {
            _isShuttingDown = true;
            var changed = PreserveForRestart();
            changed |= SynchronizeRecoveryOrder();
            return changed;
        }

        public bool SynchronizeRecoveryOrder()
        {
            if (_state.QueuedFollowUpRecoveries.Count < 2)
                return false;

            var positions = _taskHost.ScheduledRuns
                .Select((run, index) => new { run.Id, Position = index })
                .ToDictionary(item => item.Id, item => item.Position, StringComparer.Ordinal);
            var ordered = _state.QueuedFollowUpRecoveries
                .Select((record, index) => new { Record = record, OriginalPosition = index })
                .OrderBy(item => positions.TryGetValue(item.Record.RunId, out var position)
                    ? position
                    : int.MaxValue)
                .ThenBy(item => item.OriginalPosition)
                .Select(item => item.Record)
                .ToArray();
            if (ordered.SequenceEqual(_state.QueuedFollowUpRecoveries))
                return false;

            _state.QueuedFollowUpRecoveries.Clear();
            foreach (var record in ordered)
                _state.QueuedFollowUpRecoveries.Add(record);
            return true;
        }

        public void RefreshPositions()
        {
            var queuedRuns = _taskHost.QueuedRuns;
            var positions = queuedRuns
                .Select((run, index) => new { run.Id, Position = index + 1 })
                .ToDictionary(item => item.Id, item => item.Position, StringComparer.Ordinal);
            var ordered = Items
                .Where(item => positions.ContainsKey(item.RunId))
                .OrderBy(item => positions[item.RunId])
                .ToArray();

            for (var targetIndex = 0; targetIndex < ordered.Length; targetIndex++)
            {
                var currentIndex = Items.IndexOf(ordered[targetIndex]);
                if (currentIndex != targetIndex)
                    Items.Move(currentIndex, targetIndex);
            }
            foreach (var item in ordered)
                item.UpdateQueuePosition(positions[item.RunId], queuedRuns.Count);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private void Register(CopilotQueuedFollowUp item, bool addRecoveryRecord)
        {
            if (!_itemsByRunId.TryAdd(item.RunId, item))
                throw new InvalidOperationException($"A queued follow-up with run ID '{item.RunId}' is already registered.");

            Items.Add(item);
            if (addRecoveryRecord)
                AddRecovery(item);
            RefreshPositions();
        }

        private void AddRecovery(CopilotQueuedFollowUp item)
        {
            _state.QueuedFollowUpRecoveries.Add(new CopilotQueuedFollowUpRecoveryRecord
            {
                RunId = item.RunId,
                ConversationId = item.ConversationId,
                Prompt = item.Prompt,
                ComposerState = item.CreateComposerState(),
                ProfileId = item.Profile.Id,
                QueuedAtUtc = item.QueuedAtUtc,
                ResumeAfterRestart = !item.IsAutomaticGoalContinuation,
            });
        }
    }
}
