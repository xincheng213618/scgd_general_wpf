using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public enum CopilotHostedRunState
    {
        Running,
        PauseRequested,
        CancelRequested,
        Completed,
    }

    public enum CopilotAgentTaskHostChangeKind
    {
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
        private int _state = (int)CopilotHostedRunState.Running;

        internal CopilotHostedAgentRun(string conversationId, CopilotAgentMode mode)
        {
            Id = "run:" + Guid.NewGuid().ToString("N");
            ConversationId = conversationId;
            Mode = mode;
            StartedAtUtc = DateTimeOffset.UtcNow;
            RunControl = IsAgent ? new CopilotAgentRunControl() : null;
            _cancellationToken = _cancellation.Token;
        }

        public string Id { get; }

        public string ConversationId { get; }

        public CopilotAgentMode Mode { get; }

        public DateTimeOffset StartedAtUtc { get; }

        public bool IsAgent => Mode != CopilotAgentMode.Chat;

        public bool IsCheckpointReady => Volatile.Read(ref _checkpointReady) == 1;

        public CopilotHostedRunState State => (CopilotHostedRunState)Volatile.Read(ref _state);

        public CopilotAgentRunControl? RunControl { get; }

        public CancellationToken CancellationToken => _cancellationToken;

        public Task Completion => _completion.Task;

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
        private static readonly Lazy<CopilotAgentTaskHost> SharedInstance = new(() => new CopilotAgentTaskHost());
        private readonly object _gate = new();
        private CopilotHostedAgentRun? _activeRun;

        public static CopilotAgentTaskHost Shared => SharedInstance.Value;

        public event EventHandler<CopilotAgentTaskHostChangedEventArgs>? Changed;

        public CopilotHostedAgentRun? ActiveRun
        {
            get
            {
                lock (_gate)
                    return _activeRun;
            }
        }

        public bool IsActive => ActiveRun != null;

        public CopilotHostedAgentRun Start(
            string conversationId,
            CopilotAgentMode mode,
            Func<CopilotHostedAgentRun, Task> operation)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                throw new ArgumentException("A conversation ID is required.", nameof(conversationId));
            ArgumentNullException.ThrowIfNull(operation);

            CopilotHostedAgentRun run;
            lock (_gate)
            {
                if (_activeRun != null)
                    throw new InvalidOperationException("Another Copilot run is already active.");

                run = new CopilotHostedAgentRun(conversationId.Trim(), mode);
                _activeRun = run;
            }

            Publish(CopilotAgentTaskHostChangeKind.Started, run);
            _ = ExecuteAsync(run, operation);
            return run;
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
            var run = GetActiveRun(runId);
            if (run?.TryRequestCancel() != true)
                return false;

            Publish(CopilotAgentTaskHostChangeKind.ControlRequested, run);
            return true;
        }

        private CopilotHostedAgentRun? GetActiveRun(string? runId)
        {
            lock (_gate)
            {
                if (_activeRun == null)
                    return null;
                return string.IsNullOrWhiteSpace(runId) || string.Equals(_activeRun.Id, runId, StringComparison.Ordinal)
                    ? _activeRun
                    : null;
            }
        }

        private async Task ExecuteAsync(CopilotHostedAgentRun run, Func<CopilotHostedAgentRun, Task> operation)
        {
            Exception? error = null;
            try
            {
                await operation(run);
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                lock (_gate)
                {
                    if (ReferenceEquals(_activeRun, run))
                        _activeRun = null;
                }

                run.Complete(error);
                Publish(CopilotAgentTaskHostChangeKind.Completed, run);
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
    }
}
