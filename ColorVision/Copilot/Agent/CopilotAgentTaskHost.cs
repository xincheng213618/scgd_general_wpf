using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        Started,
        CheckpointReady,
        ControlRequested,
        Completed,
    }

    public sealed class CopilotAgentTaskHostChangedEventArgs : EventArgs
    {
        public CopilotAgentTaskHostChangedEventArgs(CopilotAgentTaskHostChangeKind kind, CopilotHostedAgentRun run)
        {
            Kind = kind;
            Run = run;
        }

        public CopilotAgentTaskHostChangeKind Kind { get; }

        public CopilotHostedAgentRun Run { get; }
    }

    public sealed class CopilotHostedAgentRun : IDisposable
    {
        private readonly CancellationTokenSource _cancellation = new();
        private readonly TaskCompletionSource<object?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationToken _cancellationToken;
        private int _checkpointReady;
        private int _disposed;
        private int _state = (int)CopilotHostedRunState.Queued;

        internal CopilotHostedAgentRun(string conversationId, CopilotAgentMode mode)
        {
            Id = "run:" + Guid.NewGuid().ToString("N");
            ConversationId = conversationId;
            Mode = mode;
            EnqueuedAtUtc = DateTimeOffset.UtcNow;
            RunControl = IsAgent ? new CopilotAgentRunControl() : null;
            _cancellationToken = _cancellation.Token;
        }

        public string Id { get; }

        public string ConversationId { get; }

        public CopilotAgentMode Mode { get; }

        public DateTimeOffset EnqueuedAtUtc { get; }

        public DateTimeOffset? StartedAtUtc { get; private set; }

        public bool HasStarted => StartedAtUtc.HasValue;

        public bool IsAgent => Mode != CopilotAgentMode.Chat;

        public bool IsCheckpointReady => Volatile.Read(ref _checkpointReady) == 1;

        public CopilotHostedRunState State => (CopilotHostedRunState)Volatile.Read(ref _state);

        public CopilotAgentRunControl? RunControl { get; }

        public CancellationToken CancellationToken => _cancellationToken;

        public Task Completion => _completion.Task;

        internal bool TryStart()
        {
            StartedAtUtc = DateTimeOffset.UtcNow;
            if (Interlocked.CompareExchange(ref _state, (int)CopilotHostedRunState.Running, (int)CopilotHostedRunState.Queued)
                == (int)CopilotHostedRunState.Queued)
            {
                return true;
            }

            StartedAtUtc = null;
            return false;
        }

        internal bool TryMarkCheckpointReady()
        {
            return IsAgent
                && State == CopilotHostedRunState.Running
                && Interlocked.CompareExchange(ref _checkpointReady, 1, 0) == 0;
        }

        internal bool TryRequestPause()
        {
            if (!IsAgent || !IsCheckpointReady || State != CopilotHostedRunState.Running)
                return false;
            if (Interlocked.CompareExchange(ref _state, (int)CopilotHostedRunState.PauseRequested, (int)CopilotHostedRunState.Running)
                != (int)CopilotHostedRunState.Running)
            {
                return false;
            }

            RunControl!.RequestPause();
            _cancellation.Cancel();
            return true;
        }

        internal bool TryRequestCancel()
        {
            while (true)
            {
                var state = State;
                if (state is CopilotHostedRunState.CancelRequested or CopilotHostedRunState.Completed)
                    return false;
                if (Interlocked.CompareExchange(ref _state, (int)CopilotHostedRunState.CancelRequested, (int)state) == (int)state)
                    break;
            }

            RunControl?.RequestCancel();
            _cancellation.Cancel();
            return true;
        }

        internal void Complete(Exception? error)
        {
            Interlocked.Exchange(ref _state, (int)CopilotHostedRunState.Completed);
            if (error == null)
                _completion.TrySetResult(null);
            else
                _completion.TrySetException(error);
            Dispose();
        }

        public void Dispose()
        {
            if (State != CopilotHostedRunState.Completed || Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            _cancellation.Dispose();
        }
    }

    public sealed class CopilotAgentTaskHost
    {
        public const int DefaultMaxQueuedRuns = 3;
        public const int MaximumQueuedRuns = 16;

        private static readonly Lazy<CopilotAgentTaskHost> SharedInstance = new(() => new CopilotAgentTaskHost());
        private readonly object _gate = new();
        private readonly LinkedList<HostedRunWorkItem> _queuedWorkItems = new();
        private HostedRunWorkItem? _activeWorkItem;

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
                    return _activeWorkItem == null || _queuedWorkItems.Count < MaxQueuedRuns;
            }
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
                if (_activeWorkItem != null)
                    throw new InvalidOperationException("Another Copilot run is already active.");

                var run = new CopilotHostedAgentRun(conversationId.Trim(), mode);
                if (!run.TryStart())
                    throw new InvalidOperationException("The Copilot run could not enter the active state.");

                workItem = new HostedRunWorkItem(run, operation);
                _activeWorkItem = workItem;
            }

            Publish(CopilotAgentTaskHostChangeKind.Started, workItem.Run);
            BeginExecution(workItem);
            return workItem.Run;
        }

        public bool TrySchedule(
            string conversationId,
            CopilotAgentMode mode,
            Func<CopilotHostedAgentRun, Task> operation,
            out CopilotHostedAgentRun? run)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("A conversation ID is required.", nameof(conversationId));
            ArgumentNullException.ThrowIfNull(operation);

            HostedRunWorkItem workItem;
            var startsImmediately = false;
            lock (_gate)
            {
                if (_activeWorkItem != null && _queuedWorkItems.Count >= MaxQueuedRuns)
                {
                    run = null;
                    return false;
                }

                run = new CopilotHostedAgentRun(conversationId.Trim(), mode);
                workItem = new HostedRunWorkItem(run, operation);
                if (_activeWorkItem == null)
                {
                    if (!run.TryStart())
                        throw new InvalidOperationException("The Copilot run could not enter the active state.");
                    _activeWorkItem = workItem;
                    startsImmediately = true;
                }
                else
                {
                    _queuedWorkItems.AddLast(workItem);
                }
            }

            Publish(startsImmediately ? CopilotAgentTaskHostChangeKind.Started : CopilotAgentTaskHostChangeKind.Queued, run);
            if (startsImmediately)
                BeginExecution(workItem);
            return true;
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

        public bool RequestCancel(string? runId = null)
        {
            CopilotHostedAgentRun? run;
            var wasQueued = false;
            lock (_gate)
            {
                run = MatchRun(_activeWorkItem?.Run, runId) ? _activeWorkItem?.Run : null;
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

        private void BeginExecution(HostedRunWorkItem workItem)
        {
            _ = ExecuteAsync(workItem);
        }

        private async Task ExecuteAsync(HostedRunWorkItem workItem)
        {
            Exception? error = null;
            try
            {
                await workItem.Operation(workItem.Run);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                HostedRunWorkItem? nextWorkItem = null;
                lock (_gate)
                {
                    if (ReferenceEquals(_activeWorkItem, workItem))
                    {
                        _activeWorkItem = null;
                        while (_queuedWorkItems.First != null)
                        {
                            var candidate = _queuedWorkItems.First.Value;
                            _queuedWorkItems.RemoveFirst();
                            if (!candidate.Run.TryStart())
                                continue;

                            _activeWorkItem = candidate;
                            nextWorkItem = candidate;
                            break;
                        }
                    }
                }

                workItem.Run.Complete(error);
                Publish(CopilotAgentTaskHostChangeKind.Completed, workItem.Run);
                if (nextWorkItem != null)
                {
                    Publish(CopilotAgentTaskHostChangeKind.Started, nextWorkItem.Run);
                    BeginExecution(nextWorkItem);
                }
            }
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

        private sealed record HostedRunWorkItem(
            CopilotHostedAgentRun Run,
            Func<CopilotHostedAgentRun, Task> Operation);
    }
}
