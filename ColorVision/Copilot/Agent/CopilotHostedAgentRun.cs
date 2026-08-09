using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotHostedAgentRun : IDisposable
    {
        private readonly CopilotNonBlockingCancellationSource _cancellation = new();
        private readonly TaskCompletionSource<object?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CancellationToken _cancellationToken;
        private int _agentStopReason;
        private int _automaticFollowUpDispatchSuppressed;
        private int _checkpointReady;
        private int _disposed;
        private int _state = (int)CopilotHostedRunState.Queued;
        private long _startedTimestamp = long.MinValue;
        private long _completedTimestamp = long.MinValue;
        private CopilotHostedProviderRetrySnapshot _providerRetrySnapshot =
            CopilotHostedProviderRetrySnapshot.Empty;

        internal CopilotHostedAgentRun(
            string conversationId,
            CopilotAgentMode mode,
            string? runId = null,
            DateTimeOffset? enqueuedAtUtc = null)
        {
            var normalizedRunId = (runId ?? string.Empty).Trim();
            Id = normalizedRunId.Length == 0
                ? "run:" + Guid.NewGuid().ToString("N")
                : normalizedRunId;
            ConversationId = conversationId;
            Mode = mode;
            EnqueuedAtUtc = enqueuedAtUtc ?? DateTimeOffset.UtcNow;
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

        public CopilotAgentStopReason AgentStopReason =>
            (CopilotAgentStopReason)Volatile.Read(ref _agentStopReason);

        public CopilotHostedRunState State => (CopilotHostedRunState)Volatile.Read(ref _state);

        public bool CanRequestPause => IsAgent && IsCheckpointReady && State == CopilotHostedRunState.Running;

        public bool CanRequestCancel => State is CopilotHostedRunState.Queued
            or CopilotHostedRunState.Running
            or CopilotHostedRunState.PauseRequested;

        public CopilotAgentRunControl? RunControl { get; }

        public CancellationToken CancellationToken => _cancellationToken;

        public Task Completion => _completion.Task;

        internal CopilotHostedProviderRetrySnapshot ProviderRetrySnapshot =>
            Volatile.Read(ref _providerRetrySnapshot);

        internal bool AllowsAutomaticFollowUpDispatch =>
            Volatile.Read(ref _automaticFollowUpDispatchSuppressed) == 0;

        internal long ElapsedSeconds
        {
            get
            {
                var startedTimestamp = Volatile.Read(ref _startedTimestamp);
                if (startedTimestamp == long.MinValue)
                    return 0;

                var completedTimestamp = Volatile.Read(ref _completedTimestamp);
                var endTimestamp = completedTimestamp == long.MinValue
                    ? Stopwatch.GetTimestamp()
                    : completedTimestamp;
                return Math.Max(0, (long)Stopwatch.GetElapsedTime(startedTimestamp, endTimestamp).TotalSeconds);
            }
        }

        internal bool TryStart()
        {
            if (Interlocked.CompareExchange(ref _state, (int)CopilotHostedRunState.Running, (int)CopilotHostedRunState.Queued)
                != (int)CopilotHostedRunState.Queued)
                return false;

            StartedAtUtc = DateTimeOffset.UtcNow;
            Volatile.Write(ref _startedTimestamp, Stopwatch.GetTimestamp());
            return true;
        }

        internal bool TryMarkCheckpointReady()
        {
            return IsAgent
                && State == CopilotHostedRunState.Running
                && Interlocked.CompareExchange(ref _checkpointReady, 1, 0) == 0;
        }

        internal void SetAgentStopReason(CopilotAgentStopReason stopReason)
        {
            if (!IsAgent || !Enum.IsDefined(stopReason))
                return;

            Volatile.Write(ref _agentStopReason, (int)stopReason);
        }

        internal void SuppressAutomaticFollowUpDispatch()
        {
            Interlocked.Exchange(ref _automaticFollowUpDispatchSuppressed, 1);
        }

        internal void RecordProviderRetry(CopilotProviderRetryInfo retry)
        {
            ArgumentNullException.ThrowIfNull(retry);
            while (true)
            {
                var current = Volatile.Read(ref _providerRetrySnapshot);
                var updated = new CopilotHostedProviderRetrySnapshot(
                    current.Count == int.MaxValue ? int.MaxValue : current.Count + 1,
                    retry);
                if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _providerRetrySnapshot, updated, current),
                    current))
                {
                    return;
                }
            }
        }

        internal bool TryRequestPause()
        {
            if (!CanRequestPause)
                return false;
            if (Interlocked.CompareExchange(ref _state, (int)CopilotHostedRunState.PauseRequested, (int)CopilotHostedRunState.Running)
                != (int)CopilotHostedRunState.Running)
            {
                return false;
            }

            RunControl!.RequestPause();
            CancelExecutionToken();
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
            CancelExecutionToken();
            return true;
        }

        internal bool TryRequestShutdown()
        {
            while (true)
            {
                var state = State;
                if (state is CopilotHostedRunState.CancelRequested or CopilotHostedRunState.Completed)
                    return false;
                if (Interlocked.CompareExchange(ref _state, (int)CopilotHostedRunState.CancelRequested, (int)state) == (int)state)
                    break;
            }

            CancelExecutionToken();
            return true;
        }

        private void CancelExecutionToken()
        {
            _cancellation.RequestCancellation();
        }

        internal void Complete(Exception? error)
        {
            if (Volatile.Read(ref _startedTimestamp) != long.MinValue)
            {
                Interlocked.CompareExchange(
                    ref _completedTimestamp,
                    Stopwatch.GetTimestamp(),
                    long.MinValue);
            }
            var previousState = (CopilotHostedRunState)Interlocked.Exchange(ref _state, (int)CopilotHostedRunState.Completed);
            if (previousState is CopilotHostedRunState.PauseRequested or CopilotHostedRunState.CancelRequested
                || _cancellationToken.IsCancellationRequested)
            {
                _completion.TrySetCanceled(_cancellationToken);
            }
            else if (error == null)
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
}
