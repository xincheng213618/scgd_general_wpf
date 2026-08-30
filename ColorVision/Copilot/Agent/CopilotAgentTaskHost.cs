using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public enum CopilotHostedRunState
    {
        Queued,
        Running,
        PauseRequested,
        CancelRequested,
        Completed,
    }

    public enum CopilotAgentTaskHostChangeKind
    {
        Queued,
        QueueChanged,
        Started,
        CheckpointReady,
        ControlRequested,
        Completed,
    }

    internal enum CopilotRequestAdmissionReason
    {
        Allowed,
        MissingConversation,
        HostShutdown,
        ConversationAlreadyScheduled,
        ActiveChatIsExclusive,
        ChatCannotQueue,
        QueueFull,
        NoActiveRun,
        FollowUpConversationMismatch,
    }

    internal readonly record struct CopilotRequestAdmissionResult(CopilotRequestAdmissionReason Reason)
    {
        public bool IsAllowed => Reason == CopilotRequestAdmissionReason.Allowed;
    }

    public sealed class CopilotAgentTaskHostChangedEventArgs : EventArgs
    {
        public CopilotAgentTaskHostChangedEventArgs(CopilotAgentTaskHostChangeKind kind, CopilotHostedAgentRun run)
        {
            ArgumentNullException.ThrowIfNull(run);
            Kind = kind;
            Run = run;
            RunState = run.State;
            RunHadStarted = run.HasStarted;
            RunIsAgent = run.IsAgent;
            ControlIntent = run.RunControl?.Intent ?? CopilotAgentControlIntent.None;
        }

        public CopilotAgentTaskHostChangeKind Kind { get; }

        public CopilotHostedAgentRun Run { get; }

        public CopilotHostedRunState RunState { get; }

        public bool RunHadStarted { get; }

        public bool RunIsAgent { get; }

        public CopilotAgentControlIntent ControlIntent { get; }
    }

    internal sealed record CopilotHostedProviderRetrySnapshot(
        int Count,
        CopilotProviderRetryInfo? Latest)
    {
        public static CopilotHostedProviderRetrySnapshot Empty { get; } = new(0, null);
    }

   public sealed class CopilotAgentTaskHost
    {
        public const int DefaultMaxQueuedRuns = 3;
        public const int MaximumQueuedRuns = 16;

        private static readonly Lazy<CopilotAgentTaskHost> SharedInstance = new(() => new CopilotAgentTaskHost());
        private readonly object _gate = new();
        private readonly LinkedList<HostedRunWorkItem> _queuedWorkItems = new();
        private HostedRunWorkItem? _activeWorkItem;
        private bool _isShutdown;

        public CopilotAgentTaskHost(int maxQueuedRuns = DefaultMaxQueuedRuns)
        {
            if (maxQueuedRuns is < 1 or > MaximumQueuedRuns)
                throw new ArgumentOutOfRangeException(nameof(maxQueuedRuns), $"Queue capacity must be between 1 and {MaximumQueuedRuns}.");

            MaxQueuedRuns = maxQueuedRuns;
        }

        public static CopilotAgentTaskHost Shared => SharedInstance.Value;

        public event EventHandler<CopilotAgentTaskHostChangedEventArgs>? Changed;

        public CopilotHostedAgentRun? ActiveRun
        {
            get
            {
                lock (_gate)
                    return _activeWorkItem?.Run;
            }
        }

        public bool IsActive => ActiveRun != null;

        public bool IsShutdown
        {
            get
            {
                lock (_gate)
                    return _isShutdown;
            }
        }

        public int MaxQueuedRuns { get; }

        public int QueuedCount
        {
            get
            {
                lock (_gate)
                    return _queuedWorkItems.Count;
            }
        }

        public bool CanSchedule
        {
            get
            {
                lock (_gate)
                    return !_isShutdown && (_activeWorkItem == null || _queuedWorkItems.Count < MaxQueuedRuns);
            }
        }

        internal CopilotRequestAdmissionResult EvaluateRequestAdmission(string? conversationId, CopilotAgentMode mode)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.MissingConversation);

            var normalizedConversationId = conversationId.Trim();
            lock (_gate)
                return EvaluateRequestAdmissionNoLock(normalizedConversationId, mode);
        }

        public IReadOnlyList<CopilotHostedAgentRun> QueuedRuns
        {
            get
            {
                lock (_gate)
                {
                    var runs = new List<CopilotHostedAgentRun>(_queuedWorkItems.Count);
                    foreach (var workItem in _queuedWorkItems)
                        runs.Add(workItem.Run);
                    return runs;
                }
            }
        }

        public IReadOnlyList<CopilotHostedAgentRun> ScheduledRuns
        {
            get
            {
                lock (_gate)
                {
                    var runs = new List<CopilotHostedAgentRun>(_queuedWorkItems.Count + 1);
                    if (_activeWorkItem != null)
                        runs.Add(_activeWorkItem.Run);
                    foreach (var workItem in _queuedWorkItems)
                        runs.Add(workItem.Run);
                    return runs;
                }
            }
        }

        public CopilotHostedAgentRun Start(
            string conversationId,
            CopilotAgentMode mode,
            Func<CopilotHostedAgentRun, Task> operation)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("A conversation ID is required.", nameof(conversationId));
            ArgumentNullException.ThrowIfNull(operation);

            HostedRunWorkItem workItem;
            lock (_gate)
            {
                if (_isShutdown)
                    throw new InvalidOperationException("The Copilot task host is shutting down and cannot start new runs.");
                if (_activeWorkItem != null)
                    throw new InvalidOperationException("Another Copilot run is already active.");

                var run = new CopilotHostedAgentRun(conversationId.Trim(), mode);
                workItem = new HostedRunWorkItem(run, operation, QueuedRunDispatchPolicy.Always);
                _activeWorkItem = workItem;
            }

            BeginExecution(workItem);
            return workItem.Run;
        }

        public bool TrySchedule(
            string conversationId,
            CopilotAgentMode mode,
            Func<CopilotHostedAgentRun, Task> operation,
            out CopilotHostedAgentRun? run) =>
            TrySchedule(conversationId, mode, operation, out run, out _);

        internal bool TrySchedule(
            string conversationId,
            CopilotAgentMode mode,
            Func<CopilotHostedAgentRun, Task> operation,
            out CopilotHostedAgentRun? run,
            out CopilotRequestAdmissionResult admission)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("A conversation ID is required.", nameof(conversationId));
            ArgumentNullException.ThrowIfNull(operation);

            var normalizedConversationId = conversationId.Trim();
            HostedRunWorkItem workItem;
            var startsImmediately = false;
            lock (_gate)
            {
                admission = EvaluateRequestAdmissionNoLock(normalizedConversationId, mode);
                if (!admission.IsAllowed)
                {
                    run = null;
                    return false;
                }

                run = new CopilotHostedAgentRun(normalizedConversationId, mode);
                workItem = new HostedRunWorkItem(run, operation, QueuedRunDispatchPolicy.Always);
                if (_activeWorkItem == null)
                {
                    _activeWorkItem = workItem;
                    startsImmediately = true;
                }
                else
                {
                    _queuedWorkItems.AddLast(workItem);
                }
            }

            if (startsImmediately)
                BeginExecution(workItem);
            else
                Publish(CopilotAgentTaskHostChangeKind.Queued, run);
            return true;
        }

        internal bool TryScheduleFollowUp(
            string conversationId,
            CopilotAgentMode mode,
            Func<CopilotHostedAgentRun, Task> operation,
            out CopilotHostedAgentRun? run,
            out CopilotRequestAdmissionResult admission)
        {
            return TryScheduleFollowUp(
                conversationId,
                mode,
                operation,
                runNext: false,
                isLocalCommand: false,
                out run,
                out admission);
        }

        internal bool TryScheduleFollowUpNext(
            string conversationId,
            CopilotAgentMode mode,
            Func<CopilotHostedAgentRun, Task> operation,
            out CopilotHostedAgentRun? run,
            out CopilotRequestAdmissionResult admission)
        {
            return TryScheduleFollowUp(
                conversationId,
                mode,
                operation,
                runNext: true,
                isLocalCommand: false,
                out run,
                out admission);
        }

        internal bool TryScheduleLocalCommandFollowUp(
            string conversationId,
            CopilotAgentMode mode,
            Func<CopilotHostedAgentRun, Task> operation,
            bool runNext,
            out CopilotHostedAgentRun? run,
            out CopilotRequestAdmissionResult admission)
        {
            return TryScheduleFollowUp(
                conversationId,
                mode,
                operation,
                runNext,
                isLocalCommand: true,
                out run,
                out admission);
        }

        internal CopilotRequestAdmissionResult EvaluateQueuedCommandSuccessorAdmission(
            string queuedCommandRunId,
            string? conversationId)
        {
            var normalizedRunId = (queuedCommandRunId ?? string.Empty).Trim();
            var normalizedConversationId = (conversationId ?? string.Empty).Trim();
            if (normalizedConversationId.Length == 0)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.MissingConversation);
            if (normalizedRunId.Length == 0)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.NoActiveRun);

            lock (_gate)
                return EvaluateQueuedCommandSuccessorAdmissionNoLock(normalizedRunId, normalizedConversationId);
        }

        internal bool TryScheduleQueuedCommandSuccessor(
            string queuedCommandRunId,
            string conversationId,
            CopilotAgentMode mode,
            Func<CopilotHostedAgentRun, Task> operation,
            out CopilotHostedAgentRun? run,
            out CopilotRequestAdmissionResult admission)
        {
            var normalizedRunId = (queuedCommandRunId ?? string.Empty).Trim();
            var normalizedConversationId = (conversationId ?? string.Empty).Trim();
            if (normalizedConversationId.Length == 0)
                throw new ArgumentException("A conversation ID is required.", nameof(conversationId));
            ArgumentNullException.ThrowIfNull(operation);

            lock (_gate)
            {
                admission = normalizedRunId.Length == 0
                    ? new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.NoActiveRun)
                    : EvaluateQueuedCommandSuccessorAdmissionNoLock(normalizedRunId, normalizedConversationId);
                if (!admission.IsAllowed)
                {
                    run = null;
                    return false;
                }

                run = new CopilotHostedAgentRun(normalizedConversationId, mode);
                _queuedWorkItems.AddFirst(new HostedRunWorkItem(
                    run,
                    operation,
                    QueuedRunDispatchPolicy.Always,
                    normalizedRunId));
            }

            Publish(CopilotAgentTaskHostChangeKind.Queued, run);
            return true;
        }

        internal bool TryRestoreQueuedFollowUp(
            string runId,
            string conversationId,
            CopilotAgentMode mode,
            DateTimeOffset? enqueuedAtUtc,
            Func<CopilotHostedAgentRun, Task> operation,
            out CopilotHostedAgentRun? run) =>
            TryRestoreQueuedFollowUp(
                runId,
                conversationId,
                mode,
                enqueuedAtUtc,
                operation,
                isLocalCommand: false,
                out run);

        internal bool TryRestoreQueuedLocalCommand(
            string runId,
            string conversationId,
            CopilotAgentMode mode,
            DateTimeOffset? enqueuedAtUtc,
            Func<CopilotHostedAgentRun, Task> operation,
            out CopilotHostedAgentRun? run) =>
            TryRestoreQueuedFollowUp(
                runId,
                conversationId,
                mode,
                enqueuedAtUtc,
                operation,
                isLocalCommand: true,
                out run);

        private bool TryRestoreQueuedFollowUp(
            string runId,
            string conversationId,
            CopilotAgentMode mode,
            DateTimeOffset? enqueuedAtUtc,
            Func<CopilotHostedAgentRun, Task> operation,
            bool isLocalCommand,
            out CopilotHostedAgentRun? run)
        {
            var normalizedRunId = (runId ?? string.Empty).Trim();
            var normalizedConversationId = (conversationId ?? string.Empty).Trim();
            if (normalizedRunId.Length == 0)
                throw new ArgumentException("A run ID is required.", nameof(runId));
            if (normalizedConversationId.Length == 0)
                throw new ArgumentException("A conversation ID is required.", nameof(conversationId));
            ArgumentNullException.ThrowIfNull(operation);

            lock (_gate)
            {
                if (_isShutdown
                    || mode == CopilotAgentMode.Chat
                    || _queuedWorkItems.Count >= MaxQueuedRuns
                    || string.Equals(_activeWorkItem?.Run.Id, normalizedRunId, StringComparison.Ordinal)
                    || _queuedWorkItems.Any(item => string.Equals(item.Run.Id, normalizedRunId, StringComparison.Ordinal)))
                {
                    run = null;
                    return false;
                }

                var hasScheduledConversation = string.Equals(
                        _activeWorkItem?.Run.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal)
                    || _queuedWorkItems.Any(item => string.Equals(
                        item.Run.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal));
                run = new CopilotHostedAgentRun(
                    normalizedConversationId,
                    mode,
                    normalizedRunId,
                    enqueuedAtUtc,
                    isQueuedLocalCommand: isLocalCommand);
                _queuedWorkItems.AddLast(new HostedRunWorkItem(
                    run,
                    operation,
                    hasScheduledConversation
                        ? QueuedRunDispatchPolicy.AfterCompletedTurn
                        : QueuedRunDispatchPolicy.Always));
            }

            Publish(CopilotAgentTaskHostChangeKind.Queued, run);
            return true;
        }

        private bool TryScheduleFollowUp(
            string conversationId,
            CopilotAgentMode mode,
            Func<CopilotHostedAgentRun, Task> operation,
            bool runNext,
            bool isLocalCommand,
            out CopilotHostedAgentRun? run,
            out CopilotRequestAdmissionResult admission)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("A conversation ID is required.", nameof(conversationId));
            ArgumentNullException.ThrowIfNull(operation);

            var normalizedConversationId = conversationId.Trim();
            lock (_gate)
            {
                admission = EvaluateFollowUpAdmissionNoLock(normalizedConversationId, mode);
                if (!admission.IsAllowed)
                {
                    run = null;
                    return false;
                }

                run = new CopilotHostedAgentRun(
                    normalizedConversationId,
                    mode,
                    isQueuedLocalCommand: isLocalCommand);
                var workItem = new HostedRunWorkItem(
                    run,
                    operation,
                    runNext
                        ? QueuedRunDispatchPolicy.AfterAnyTurn
                        : QueuedRunDispatchPolicy.AfterCompletedTurn);
                if (runNext)
                    _queuedWorkItems.AddFirst(workItem);
                else
                    _queuedWorkItems.AddLast(workItem);
            }

            Publish(CopilotAgentTaskHostChangeKind.Queued, run);
            return true;
        }

        internal CopilotRequestAdmissionResult EvaluateFollowUpAdmission(
            string? conversationId,
            CopilotAgentMode mode)
        {
            var normalizedConversationId = (conversationId ?? string.Empty).Trim();
            if (normalizedConversationId.Length == 0)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.MissingConversation);

            lock (_gate)
                return EvaluateFollowUpAdmissionNoLock(normalizedConversationId, mode);
        }

        private CopilotRequestAdmissionResult EvaluateFollowUpAdmissionNoLock(
            string normalizedConversationId,
            CopilotAgentMode mode)
        {
            if (_isShutdown)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.HostShutdown);
            if (_activeWorkItem == null)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.NoActiveRun);
            if (!_activeWorkItem.Run.IsAgent)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.ActiveChatIsExclusive);
            if (!string.Equals(_activeWorkItem.Run.ConversationId, normalizedConversationId, StringComparison.Ordinal))
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.FollowUpConversationMismatch);
            if (mode == CopilotAgentMode.Chat)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.ChatCannotQueue);
            if (_queuedWorkItems.Count >= MaxQueuedRuns)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.QueueFull);
            return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.Allowed);
        }

        private CopilotRequestAdmissionResult EvaluateRequestAdmissionNoLock(string normalizedConversationId, CopilotAgentMode mode)
        {
            if (_isShutdown)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.HostShutdown);
            if (ContainsConversationNoLock(normalizedConversationId))
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.ConversationAlreadyScheduled);
            if (_activeWorkItem == null)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.Allowed);
            if (!_activeWorkItem.Run.IsAgent)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.ActiveChatIsExclusive);
            if (mode == CopilotAgentMode.Chat)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.ChatCannotQueue);
            return new CopilotRequestAdmissionResult(_queuedWorkItems.Count < MaxQueuedRuns
                ? CopilotRequestAdmissionReason.Allowed
                : CopilotRequestAdmissionReason.QueueFull);
        }

        private CopilotRequestAdmissionResult EvaluateQueuedCommandSuccessorAdmissionNoLock(
            string normalizedRunId,
            string normalizedConversationId)
        {
            if (_isShutdown)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.HostShutdown);
            if (_activeWorkItem == null
                || !_activeWorkItem.Run.IsQueuedLocalCommand
                || !string.Equals(_activeWorkItem.Run.Id, normalizedRunId, StringComparison.Ordinal))
            {
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.NoActiveRun);
            }
            if (!string.Equals(
                    _activeWorkItem.Run.ConversationId,
                    normalizedConversationId,
                    StringComparison.Ordinal))
            {
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.FollowUpConversationMismatch);
            }
            if (_queuedWorkItems.Count >= MaxQueuedRuns)
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.QueueFull);
            if (_queuedWorkItems.Any(item => string.Equals(
                    item.QueuedCommandOriginRunId,
                    normalizedRunId,
                    StringComparison.Ordinal)))
            {
                return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.ConversationAlreadyScheduled);
            }
            return new CopilotRequestAdmissionResult(CopilotRequestAdmissionReason.Allowed);
        }

        public int Shutdown()
        {
            CopilotHostedAgentRun? activeRun;
            CopilotHostedAgentRun[] queuedRuns;
            lock (_gate)
            {
                if (_isShutdown)
                    return 0;

                _isShutdown = true;
                activeRun = _activeWorkItem?.Run;
                queuedRuns = _queuedWorkItems.Select(workItem => workItem.Run).ToArray();
                _queuedWorkItems.Clear();
            }

            var cancellationCount = 0;
            if (activeRun?.TryRequestShutdown() == true)
            {
                cancellationCount++;
                Publish(CopilotAgentTaskHostChangeKind.ControlRequested, activeRun);
            }

            foreach (var queuedRun in queuedRuns)
            {
                if (!queuedRun.TryRequestShutdown())
                    continue;

                cancellationCount++;
                Publish(CopilotAgentTaskHostChangeKind.ControlRequested, queuedRun);
                queuedRun.Complete(error: null);
                Publish(CopilotAgentTaskHostChangeKind.Completed, queuedRun);
            }

            return cancellationCount;
        }

        public bool MarkCheckpointReady(string runId)
        {
            var run = GetActiveRun(runId);
            if (run?.TryMarkCheckpointReady() != true)
                return false;

            Publish(CopilotAgentTaskHostChangeKind.CheckpointReady, run);
            return true;
        }

        public bool RequestPause(string? runId = null)
        {
            var run = GetActiveRun(runId);
            if (run?.TryRequestPause() != true)
                return false;

            Publish(CopilotAgentTaskHostChangeKind.ControlRequested, run);
            return true;
        }

        public bool RequestCancel(string? runId = null) => RequestCancelCore(runId, queuedOnly: false);

        // A queue item may have started while its confirmation dialog or UI notification was pending.
        public bool RequestCancelQueued(string runId) => RequestCancelCore(runId, queuedOnly: true);

        private bool RequestCancelCore(string? runId, bool queuedOnly)
        {
            CopilotHostedAgentRun? run;
            var wasQueued = false;
            lock (_gate)
            {
                run = !queuedOnly && MatchRun(_activeWorkItem?.Run, runId) ? _activeWorkItem?.Run : null;
                if (run == null && !string.IsNullOrWhiteSpace(runId))
                {
                    var node = _queuedWorkItems.First;
                    while (node != null)
                    {
                        if (string.Equals(node.Value.Run.Id, runId, StringComparison.Ordinal))
                        {
                            run = node.Value.Run;
                            _queuedWorkItems.Remove(node);
                            wasQueued = true;
                            break;
                        }
                        node = node.Next;
                    }
                }

            }

            if (run?.TryRequestCancel() != true)
                return false;

            Publish(CopilotAgentTaskHostChangeKind.ControlRequested, run);
            if (wasQueued)
            {
                run.Complete(error: null);
                Publish(CopilotAgentTaskHostChangeKind.Completed, run);
            }
            return true;
        }

        public int GetQueuePosition(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId))
                return 0;

            lock (_gate)
            {
                var position = 1;
                foreach (var workItem in _queuedWorkItems)
                {
                    if (string.Equals(workItem.Run.Id, runId, StringComparison.Ordinal))
                        return position;
                    position++;
                }
                return 0;
            }
        }

        public bool MoveQueuedRun(string runId, int offset)
        {
            if (string.IsNullOrWhiteSpace(runId) || offset is not (-1 or 1))
                return false;

            CopilotHostedAgentRun? run = null;
            lock (_gate)
            {
                var node = _queuedWorkItems.First;
                while (node != null && !string.Equals(node.Value.Run.Id, runId, StringComparison.Ordinal))
                    node = node.Next;
                if (node == null)
                    return false;

                if (offset < 0)
                {
                    var previous = node.Previous;
                    if (previous == null)
                        return false;
                    _queuedWorkItems.Remove(node);
                    _queuedWorkItems.AddBefore(previous, node);
                }
                else
                {
                    var next = node.Next;
                    if (next == null)
                        return false;
                    _queuedWorkItems.Remove(node);
                    _queuedWorkItems.AddAfter(next, node);
                }
                run = node.Value.Run;
            }

            Publish(CopilotAgentTaskHostChangeKind.QueueChanged, run);
            return true;
        }

        public bool PromoteQueuedRun(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId))
                return false;

            CopilotHostedAgentRun? run = null;
            var changed = false;
            lock (_gate)
            {
                var node = _queuedWorkItems.First;
                while (node != null && !string.Equals(node.Value.Run.Id, runId, StringComparison.Ordinal))
                    node = node.Next;
                if (node == null)
                    return false;

                run = node.Value.Run;
                if (node.Previous != null)
                {
                    _queuedWorkItems.Remove(node);
                    _queuedWorkItems.AddFirst(node);
                    changed = true;
                }
            }

            if (changed)
                Publish(CopilotAgentTaskHostChangeKind.QueueChanged, run);
            return true;
        }

        internal bool TryStartQueuedRun(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId))
                return false;

            HostedRunWorkItem? workItem = null;
            lock (_gate)
            {
                if (_isShutdown || _activeWorkItem != null)
                    return false;

                var node = _queuedWorkItems.First;
                while (node != null && !string.Equals(node.Value.Run.Id, runId, StringComparison.Ordinal))
                    node = node.Next;
                if (node == null)
                    return false;

                workItem = node.Value;
                _queuedWorkItems.Remove(node);
                _activeWorkItem = workItem;
            }

            Publish(CopilotAgentTaskHostChangeKind.QueueChanged, workItem.Run);
            BeginExecution(workItem);
            return true;
        }

        public CopilotHostedAgentRun? FindRunByConversationId(string? conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return null;

            lock (_gate)
            {
                var activeRun = _activeWorkItem?.Run;
                if (string.Equals(activeRun?.ConversationId, conversationId, StringComparison.Ordinal))
                    return activeRun;

                foreach (var workItem in _queuedWorkItems)
                {
                    if (string.Equals(workItem.Run.ConversationId, conversationId, StringComparison.Ordinal))
                        return workItem.Run;
                }
                return null;
            }
        }

        private CopilotHostedAgentRun? GetActiveRun(string? runId)
        {
            lock (_gate)
            {
                if (_activeWorkItem == null)
                    return null;
                return string.IsNullOrWhiteSpace(runId) || string.Equals(_activeWorkItem.Run.Id, runId, StringComparison.Ordinal)
                    ? _activeWorkItem.Run
                    : null;
            }
        }

        private static bool MatchRun(CopilotHostedAgentRun? run, string? runId)
        {
            return run != null && (string.IsNullOrWhiteSpace(runId) || string.Equals(run.Id, runId, StringComparison.Ordinal));
        }

        private bool ContainsConversationNoLock(string conversationId)
        {
            if (string.Equals(_activeWorkItem?.Run.ConversationId, conversationId, StringComparison.Ordinal))
                return true;

            foreach (var workItem in _queuedWorkItems)
            {
                if (string.Equals(workItem.Run.ConversationId, conversationId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private void BeginExecution(HostedRunWorkItem workItem)
        {
            _ = ExecuteAsync(workItem);
        }

        private async Task ExecuteAsync(HostedRunWorkItem workItem)
        {
            Exception? error = null;
            try
            {
                if (!workItem.Run.TryStart())
                    return;

                Publish(CopilotAgentTaskHostChangeKind.Started, workItem.Run);
                await workItem.Operation(workItem.Run);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                HostedRunWorkItem? nextWorkItem = null;
                var completedNormally = error == null
                    && workItem.Run.AllowsAutomaticFollowUpDispatch
                    && !workItem.Run.CancellationToken.IsCancellationRequested;
                lock (_gate)
                {
                    if (ReferenceEquals(_activeWorkItem, workItem))
                    {
                        _activeWorkItem = null;
                        var nextNode = _queuedWorkItems.First;
                        while (nextNode != null
                            && !CanAutoStartAfter(nextNode.Value, workItem.Run, completedNormally))
                        {
                            nextNode = nextNode.Next;
                        }
                        if (nextNode != null)
                        {
                            var candidate = nextNode.Value;
                            _queuedWorkItems.Remove(nextNode);
                            _activeWorkItem = candidate;
                            nextWorkItem = candidate;
                        }
                    }
                }

                workItem.Run.Complete(error);
                Publish(CopilotAgentTaskHostChangeKind.Completed, workItem.Run);
                if (nextWorkItem != null)
                    BeginExecution(nextWorkItem);
            }
        }

        private static bool CanAutoStartAfter(
            HostedRunWorkItem candidate,
            CopilotHostedAgentRun completedRun,
            bool completedNormally)
        {
            if (candidate.DispatchPolicy == QueuedRunDispatchPolicy.Always)
                return true;
            if (!string.Equals(candidate.Run.ConversationId, completedRun.ConversationId, StringComparison.Ordinal))
                return false;
            return completedNormally || candidate.DispatchPolicy == QueuedRunDispatchPolicy.AfterAnyTurn;
        }

        private void Publish(CopilotAgentTaskHostChangeKind kind, CopilotHostedAgentRun run)
        {
            var handlers = Changed;
            if (handlers == null)
                return;

            var args = new CopilotAgentTaskHostChangedEventArgs(kind, run);
            foreach (EventHandler<CopilotAgentTaskHostChangedEventArgs> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, args);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Copilot task host subscriber failed: {ex.Message}");
                }
            }
        }

        private enum QueuedRunDispatchPolicy
        {
            Always,
            AfterCompletedTurn,
            AfterAnyTurn,
        }

        private sealed record HostedRunWorkItem(
            CopilotHostedAgentRun Run,
            Func<CopilotHostedAgentRun, Task> Operation,
            QueuedRunDispatchPolicy DispatchPolicy,
            string QueuedCommandOriginRunId = "");
    }
}
