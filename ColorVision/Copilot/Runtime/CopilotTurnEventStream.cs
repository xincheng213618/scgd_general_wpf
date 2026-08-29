using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal static class CopilotTurnEventStream
    {
        internal static readonly TimeSpan DefaultProducerShutdownTimeout = TimeSpan.FromSeconds(7);
        internal const int DefaultMaximumPendingEvents = 4096;
        // Match the upstream streaming response ceiling while bounding queued UTF-16 text
        // when the UI consumer is stalled or a provider emits an abnormal stream.
        internal const int DefaultMaximumPendingStreamCharacters = 8 * 1024 * 1024;

        public static async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            string turnId,
            CopilotAgentMode mode,
            Func<CopilotTurnEventSink, CancellationToken, Task<CopilotTurnResult>> runTurn,
            [EnumeratorCancellation] CancellationToken cancellationToken,
            TimeSpan? producerShutdownTimeout = null,
            int maximumPendingEvents = DefaultMaximumPendingEvents,
            int maximumPendingStreamCharacters = DefaultMaximumPendingStreamCharacters)
        {
            turnId = CopilotTurnStartedEvent.NormalizeTurnId(turnId);
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));
            ArgumentNullException.ThrowIfNull(runTurn);
            var shutdownTimeout = producerShutdownTimeout ?? DefaultProducerShutdownTimeout;
            if (shutdownTimeout <= TimeSpan.Zero || shutdownTimeout == Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(producerShutdownTimeout), "Producer shutdown timeout must be finite and positive.");
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumPendingEvents, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumPendingStreamCharacters, 1);

            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var cancellationDrainGuard = new CancellationTokenSource();
            var eventBuffer = new CopilotTurnEventBuffer(
                maximumPendingEvents,
                maximumPendingStreamCharacters);
            var sink = new CopilotTurnEventSink(turnEvent => eventBuffer.TryWrite(turnEvent));
            // Async delegates execute synchronously until their first incomplete await. Start the
            // entire turn on the thread pool so provider setup and extension code cannot occupy
            // the WPF thread before yielding.
            var producer = Task.Run(
                () => ProduceAsync(turnId, mode, runTurn, sink, eventBuffer, lifetime.Token),
                CancellationToken.None);
            var cancellationDrain = EnforceCancellationDeadlineAsync(
                turnId,
                mode,
                producer,
                eventBuffer,
                shutdownTimeout,
                cancellationToken,
                cancellationDrainGuard.Token);

            try
            {
                // Caller cancellation stops the producer, but the reader remains alive for a
                // bounded grace period so Agent pause/cancel can publish its final checkpoint
                // and structured completion event.
                await foreach (var turnEvent in eventBuffer.ReadAllAsync().ConfigureAwait(false))
                    yield return turnEvent;
            }
            finally
            {
                lifetime.Cancel();
                eventBuffer.Abandon();
                cancellationDrainGuard.Cancel();
                var cancellationDrainOutcome = await cancellationDrain.ConfigureAwait(false);
                if (cancellationDrainOutcome == CancellationDrainOutcome.NotRequested
                    && !producer.IsCompleted)
                {
                    await WaitForProducerShutdownAsync(producer, shutdownTimeout).ConfigureAwait(false);
                }
            }
        }

        private static async Task<CancellationDrainOutcome> EnforceCancellationDeadlineAsync(
            string turnId,
            CopilotAgentMode mode,
            Task producer,
            CopilotTurnEventBuffer eventBuffer,
            TimeSpan shutdownTimeout,
            CancellationToken callerCancellationToken,
            CancellationToken guardCancellationToken)
        {
            if (!callerCancellationToken.CanBeCanceled)
                return CancellationDrainOutcome.NotRequested;

            using var cancellationSignal = CancellationTokenSource.CreateLinkedTokenSource(
                callerCancellationToken,
                guardCancellationToken);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationSignal.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            if (!callerCancellationToken.IsCancellationRequested)
                return CancellationDrainOutcome.NotRequested;

            try
            {
                await producer.WaitAsync(shutdownTimeout).ConfigureAwait(false);
                return CancellationDrainOutcome.Completed;
            }
            catch (TimeoutException)
            {
                Trace.TraceWarning(
                    "Copilot turn producer did not stop within {0}; closing the event stream.",
                    shutdownTimeout);
                var cancellationException = new OperationCanceledException(
                    "Copilot turn producer did not stop within the cancellation grace period.",
                    callerCancellationToken);
                eventBuffer.TryWriteTerminalAndComplete(
                    CopilotTurnCompletedEvent.Interrupted(turnId, mode),
                    cancellationException);
                return CancellationDrainOutcome.TimedOut;
            }
        }

        private static async Task WaitForProducerShutdownAsync(Task producer, TimeSpan shutdownTimeout)
        {
            try
            {
                await producer.WaitAsync(shutdownTimeout).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                Trace.TraceWarning(
                    "Copilot turn producer ignored disposal cancellation for {0}; detaching it from the closed event stream.",
                    shutdownTimeout);
            }
        }

        private static async Task ProduceAsync(
            string turnId,
            CopilotAgentMode mode,
            Func<CopilotTurnEventSink, CancellationToken, Task<CopilotTurnResult>> runTurn,
            CopilotTurnEventSink sink,
            CopilotTurnEventBuffer eventBuffer,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!eventBuffer.TryWrite(new CopilotTurnStartedEvent(turnId, mode)))
                    return;
                var result = await runTurn(sink, cancellationToken).ConfigureAwait(false);
                eventBuffer.TryWriteTerminalAndComplete(
                    cancellationToken.IsCancellationRequested
                        ? CopilotTurnCompletedEvent.Interrupted(turnId, result)
                        : CopilotTurnCompletedEvent.Completed(turnId, result));
            }
            catch (OperationCanceledException ex)
            {
                eventBuffer.TryWriteTerminalAndComplete(
                    CopilotTurnCompletedEvent.Interrupted(turnId, mode),
                    ex);
            }
            catch (Exception ex)
            {
                var turnError = CopilotTurnError.FromException(ex);
                eventBuffer.TryWriteFailureAndComplete(
                    new CopilotTurnErrorEvent(turnId, mode, turnError),
                    CopilotTurnCompletedEvent.Failed(turnId, mode, turnError),
                    ex);
            }
        }

        private enum CancellationDrainOutcome
        {
            NotRequested,
            Completed,
            TimedOut,
        }
    }

    internal sealed class CopilotTurnEventBuffer
    {
        private readonly object _gate = new();
        private readonly int _maximumPendingEvents;
        private readonly int _maximumPendingStreamCharacters;
        private readonly int _streamCoalescingThreshold;
        private readonly LinkedList<PendingEvent> _pendingEvents = new();
        private int _pendingStreamCharacters;
        private ExceptionDispatchInfo? _completionError;
        private bool _completed;
        private TaskCompletionSource _stateChanged = CreateStateChangedSignal();

        public CopilotTurnEventBuffer(
            int maximumPendingEvents,
            int maximumPendingStreamCharacters =
                CopilotTurnEventStream.DefaultMaximumPendingStreamCharacters)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumPendingEvents, 1);
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumPendingStreamCharacters, 1);
            _maximumPendingEvents = maximumPendingEvents;
            _maximumPendingStreamCharacters = maximumPendingStreamCharacters;
            _streamCoalescingThreshold = Math.Max(1, maximumPendingEvents / 2);
        }

        internal int PendingCount
        {
            get
            {
                lock (_gate)
                    return _pendingEvents.Count;
            }
        }

        internal int PendingStreamCharacters
        {
            get
            {
                lock (_gate)
                    return _pendingStreamCharacters;
            }
        }

        public bool TryWrite(CopilotTurnEvent turnEvent)
        {
            ArgumentNullException.ThrowIfNull(turnEvent);
            TaskCompletionSource stateChanged;
            lock (_gate)
            {
                if (_completed)
                    return false;
                var lastPendingEvent = _pendingEvents.Last?.Value;
                if (_pendingEvents.Count >= _streamCoalescingThreshold
                    && lastPendingEvent?.TryGetCoalescingText(turnEvent, out var coalescingText) == true)
                {
                    EnsurePendingStreamCapacityUnderLock(coalescingText.Length);
                    lastPendingEvent.AppendStreamText(coalescingText);
                    _pendingStreamCharacters += coalescingText.Length;
                    return true;
                }
                if (_pendingEvents.Count >= _maximumPendingEvents)
                {
                    throw new InvalidOperationException(
                        $"Copilot turn event backlog exceeded the {_maximumPendingEvents:N0}-event safety limit. The turn was stopped before unbounded UI memory growth.");
                }

                var streamCharacters = PendingEvent.GetStreamCharacterCount(turnEvent);
                EnsurePendingStreamCapacityUnderLock(streamCharacters);
                _pendingEvents.AddLast(new PendingEvent(turnEvent));
                _pendingStreamCharacters += streamCharacters;
                stateChanged = _stateChanged;
            }

            stateChanged.TrySetResult();
            return true;
        }

        public bool TryComplete(Exception? error = null)
        {
            TaskCompletionSource stateChanged;
            lock (_gate)
            {
                if (_completed)
                    return false;
                _completed = true;
                if (error != null)
                    _completionError = ExceptionDispatchInfo.Capture(error);
                stateChanged = _stateChanged;
            }

            stateChanged.TrySetResult();
            return true;
        }

        public void Abandon()
        {
            TaskCompletionSource stateChanged;
            lock (_gate)
            {
                _pendingEvents.Clear();
                _pendingStreamCharacters = 0;
                _completionError = null;
                _completed = true;
                stateChanged = _stateChanged;
            }

            stateChanged.TrySetResult();
        }

        public bool TryWriteTerminalAndComplete(
            CopilotTurnCompletedEvent terminalEvent,
            Exception? error = null)
        {
            ArgumentNullException.ThrowIfNull(terminalEvent);
            TaskCompletionSource stateChanged;
            lock (_gate)
            {
                if (_completed)
                    return false;

                // A terminal event is allowed one slot beyond the ordinary backlog limit so
                // consumers always receive exactly one authoritative turn outcome before the
                // original failure or cancellation is rethrown by the async stream.
                _pendingEvents.AddLast(new PendingEvent(terminalEvent));
                _completed = true;
                if (error != null)
                    _completionError = ExceptionDispatchInfo.Capture(error);
                stateChanged = _stateChanged;
            }

            stateChanged.TrySetResult();
            return true;
        }

        public bool TryWriteFailureAndComplete(
            CopilotTurnErrorEvent errorEvent,
            CopilotTurnCompletedEvent terminalEvent,
            Exception error)
        {
            ArgumentNullException.ThrowIfNull(errorEvent);
            ArgumentNullException.ThrowIfNull(terminalEvent);
            ArgumentNullException.ThrowIfNull(error);
            if (terminalEvent.Status != CopilotTurnStatus.Failed
                || !Equals(errorEvent.Error, terminalEvent.Error)
                || !string.Equals(errorEvent.TurnId, terminalEvent.TurnId, StringComparison.Ordinal)
                || errorEvent.Mode != terminalEvent.Mode)
            {
                throw new ArgumentException("Turn error and failed terminal events must describe the same failure.");
            }

            TaskCompletionSource stateChanged;
            lock (_gate)
            {
                if (_completed)
                    return false;

                // Failure is one indivisible protocol transition: consumers must observe the
                // safe error snapshot immediately before the authoritative failed terminal.
                _pendingEvents.AddLast(new PendingEvent(errorEvent));
                _pendingEvents.AddLast(new PendingEvent(terminalEvent));
                _completed = true;
                _completionError = ExceptionDispatchInfo.Capture(error);
                stateChanged = _stateChanged;
            }

            stateChanged.TrySetResult();
            return true;
        }

        public async IAsyncEnumerable<CopilotTurnEvent> ReadAllAsync()
        {
            while (true)
            {
                PendingEvent? pendingEvent = null;
                ExceptionDispatchInfo? completionError = null;
                Task? stateChanged = null;
                var completed = false;
                lock (_gate)
                {
                    if (_pendingEvents.First != null)
                    {
                        pendingEvent = _pendingEvents.First.Value;
                        _pendingEvents.RemoveFirst();
                        _pendingStreamCharacters -= pendingEvent.StreamCharacterCount;
                    }
                    else
                    {
                        completed = _completed;
                        completionError = _completionError;
                        if (!completed)
                        {
                            if (_stateChanged.Task.IsCompleted)
                                _stateChanged = CreateStateChangedSignal();
                            stateChanged = _stateChanged.Task;
                        }
                    }
                }

                if (pendingEvent != null)
                {
                    yield return pendingEvent.ToTurnEvent();
                    continue;
                }
                if (completed)
                {
                    completionError?.Throw();
                    yield break;
                }
                await stateChanged!.ConfigureAwait(false);
            }
        }

        private static TaskCompletionSource CreateStateChangedSignal() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private void EnsurePendingStreamCapacityUnderLock(int additionalCharacters)
        {
            if (additionalCharacters <= _maximumPendingStreamCharacters - _pendingStreamCharacters)
                return;

            throw new InvalidOperationException(
                $"Copilot turn streaming backlog exceeded the {_maximumPendingStreamCharacters:N0}-character safety limit. The turn was stopped before unbounded UI memory growth.");
        }

        private sealed class PendingEvent
        {
            private readonly CopilotTurnEvent? _turnEvent;
            private readonly StringBuilder? _streamText;
            private readonly PendingStreamKind _streamKind;
            private readonly int _nonCoalescedStreamCharacters;

            public PendingEvent(CopilotTurnEvent turnEvent)
            {
                var (streamKind, streamText) = GetStreamDescriptor(turnEvent);
                _streamKind = streamKind;
                if (streamText == null)
                {
                    _turnEvent = turnEvent;
                    _streamText = null;
                    _nonCoalescedStreamCharacters = GetStreamCharacterCount(turnEvent);
                }
                else
                {
                    _turnEvent = null;
                    _streamText = new StringBuilder(streamText);
                    _nonCoalescedStreamCharacters = 0;
                }
            }

            public int StreamCharacterCount =>
                _streamText?.Length ?? _nonCoalescedStreamCharacters;

            public static int GetStreamCharacterCount(CopilotTurnEvent turnEvent)
            {
                var (_, coalescedText) = GetStreamDescriptor(turnEvent);
                if (coalescedText != null)
                    return coalescedText.Length;

                if (turnEvent is CopilotTurnChatDeltaEvent chatDelta)
                {
                    return AddClamped(
                        chatDelta.Delta.ReasoningContent?.Length ?? 0,
                        chatDelta.Delta.Content?.Length ?? 0);
                }
                if (turnEvent is CopilotTurnAgentEvent
                    {
                        Event.Type: CopilotAgentEventType.ReasoningDelta
                            or CopilotAgentEventType.AnswerDelta,
                    } agentEvent)
                {
                    return agentEvent.Event.Text?.Length ?? 0;
                }

                return 0;
            }

            public bool TryGetCoalescingText(CopilotTurnEvent next, out string text)
            {
                if (_streamText != null)
                    return TryGetCompatibleStreamText(next, _streamKind, out text);

                text = string.Empty;
                return false;
            }

            public void AppendStreamText(string text) => _streamText!.Append(text);

            public CopilotTurnEvent ToTurnEvent()
            {
                if (_streamText == null)
                    return _turnEvent!;

                var text = _streamText.ToString();
                return _streamKind switch
                {
                    PendingStreamKind.ChatReasoning =>
                        new CopilotTurnChatDeltaEvent(new CopilotStreamDelta(text, string.Empty)),
                    PendingStreamKind.ChatContent =>
                        new CopilotTurnChatDeltaEvent(new CopilotStreamDelta(string.Empty, text)),
                    PendingStreamKind.AgentReasoning =>
                        new CopilotTurnAgentEvent(CopilotAgentEvent.ReasoningDelta(text)),
                    PendingStreamKind.AgentAnswer =>
                        new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta(text)),
                    _ => _turnEvent!,
                };
            }

            private static (PendingStreamKind Kind, string? Text) GetStreamDescriptor(
                CopilotTurnEvent turnEvent)
            {
                if (turnEvent is CopilotTurnChatDeltaEvent chatDelta)
                {
                    if (chatDelta.Delta.HasReasoning
                        && string.IsNullOrEmpty(chatDelta.Delta.Content))
                    {
                        return (
                            PendingStreamKind.ChatReasoning,
                            chatDelta.Delta.ReasoningContent);
                    }
                    if (chatDelta.Delta.HasContent
                        && string.IsNullOrEmpty(chatDelta.Delta.ReasoningContent))
                    {
                        return (
                            PendingStreamKind.ChatContent,
                            chatDelta.Delta.Content);
                    }
                }
                if (turnEvent is CopilotTurnAgentEvent agentEvent)
                {
                    if (agentEvent.Event.Type == CopilotAgentEventType.ReasoningDelta)
                    {
                        return (
                            PendingStreamKind.AgentReasoning,
                            agentEvent.Event.Text);
                    }
                    if (agentEvent.Event.Type == CopilotAgentEventType.AnswerDelta)
                    {
                        return (
                            PendingStreamKind.AgentAnswer,
                            agentEvent.Event.Text);
                    }
                }

                return (PendingStreamKind.None, null);
            }

            private static int AddClamped(int left, int right) =>
                left > int.MaxValue - right ? int.MaxValue : left + right;

            private static bool TryGetCompatibleStreamText(
                CopilotTurnEvent turnEvent,
                PendingStreamKind streamKind,
                out string text)
            {
                text = string.Empty;
                switch (streamKind)
                {
                    case PendingStreamKind.ChatReasoning
                        when turnEvent is CopilotTurnChatDeltaEvent chatReasoning
                            && chatReasoning.Delta.HasReasoning
                            && string.IsNullOrEmpty(chatReasoning.Delta.Content):
                        text = chatReasoning.Delta.ReasoningContent;
                        return true;
                    case PendingStreamKind.ChatContent
                        when turnEvent is CopilotTurnChatDeltaEvent chatContent
                            && chatContent.Delta.HasContent
                            && string.IsNullOrEmpty(chatContent.Delta.ReasoningContent):
                        text = chatContent.Delta.Content;
                        return true;
                    case PendingStreamKind.AgentReasoning
                        when turnEvent is CopilotTurnAgentEvent { Event.Type: CopilotAgentEventType.ReasoningDelta } agentReasoning:
                        text = agentReasoning.Event.Text;
                        return true;
                    case PendingStreamKind.AgentAnswer
                        when turnEvent is CopilotTurnAgentEvent { Event.Type: CopilotAgentEventType.AnswerDelta } agentAnswer:
                        text = agentAnswer.Event.Text;
                        return true;
                    default:
                        return false;
                }
            }

        }

        private enum PendingStreamKind
        {
            None,
            ChatReasoning,
            ChatContent,
            AgentReasoning,
            AgentAnswer,
        }
    }
}
