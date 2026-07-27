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

        public static async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            Func<CopilotTurnEventSink, CancellationToken, Task<CopilotTurnResult>> runTurn,
            [EnumeratorCancellation] CancellationToken cancellationToken,
            TimeSpan? producerShutdownTimeout = null,
            int maximumPendingEvents = DefaultMaximumPendingEvents)
        {
            ArgumentNullException.ThrowIfNull(runTurn);
            var shutdownTimeout = producerShutdownTimeout ?? DefaultProducerShutdownTimeout;
            if (shutdownTimeout <= TimeSpan.Zero || shutdownTimeout == Timeout.InfiniteTimeSpan)
                throw new ArgumentOutOfRangeException(nameof(producerShutdownTimeout), "Producer shutdown timeout must be finite and positive.");
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumPendingEvents, 1);

            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var cancellationDrainGuard = new CancellationTokenSource();
            var eventBuffer = new CopilotTurnEventBuffer(maximumPendingEvents);
            var sink = new CopilotTurnEventSink(turnEvent => eventBuffer.TryWrite(turnEvent));
            // Async delegates execute synchronously until their first incomplete await. Start the
            // entire turn on the thread pool so provider setup and extension code cannot occupy
            // the WPF thread before yielding.
            var producer = Task.Run(
                () => ProduceAsync(runTurn, sink, eventBuffer, lifetime.Token),
                CancellationToken.None);
            var cancellationDrain = EnforceCancellationDeadlineAsync(
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
                eventBuffer.TryComplete();
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
                eventBuffer.TryComplete(new OperationCanceledException(
                    "Copilot turn producer did not stop within the cancellation grace period.",
                    callerCancellationToken));
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
            Func<CopilotTurnEventSink, CancellationToken, Task<CopilotTurnResult>> runTurn,
            CopilotTurnEventSink sink,
            CopilotTurnEventBuffer eventBuffer,
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await runTurn(sink, cancellationToken).ConfigureAwait(false);
                if (!eventBuffer.TryWrite(new CopilotTurnCompletedEvent(result)))
                    throw new InvalidOperationException("Copilot turn event stream closed before completion.");
                eventBuffer.TryComplete();
            }
            catch (Exception ex)
            {
                eventBuffer.TryComplete(ex);
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
        private readonly int _streamCoalescingThreshold;
        private readonly LinkedList<PendingEvent> _pendingEvents = new();
        private ExceptionDispatchInfo? _completionError;
        private bool _completed;
        private TaskCompletionSource _stateChanged = CreateStateChangedSignal();

        public CopilotTurnEventBuffer(int maximumPendingEvents)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumPendingEvents, 1);
            _maximumPendingEvents = maximumPendingEvents;
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

        public bool TryWrite(CopilotTurnEvent turnEvent)
        {
            ArgumentNullException.ThrowIfNull(turnEvent);
            TaskCompletionSource stateChanged;
            lock (_gate)
            {
                if (_completed)
                    return false;
                if (_pendingEvents.Count >= _streamCoalescingThreshold
                    && _pendingEvents.Last?.Value.TryCoalesce(turnEvent) == true)
                {
                    return true;
                }
                if (_pendingEvents.Count >= _maximumPendingEvents)
                {
                    throw new InvalidOperationException(
                        $"Copilot turn event backlog exceeded the {_maximumPendingEvents:N0}-event safety limit. The turn was stopped before unbounded UI memory growth.");
                }

                _pendingEvents.AddLast(new PendingEvent(turnEvent));
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

        private sealed class PendingEvent
        {
            private readonly CopilotTurnEvent _turnEvent;
            private readonly StringBuilder? _streamText;
            private readonly PendingStreamKind _streamKind;

            public PendingEvent(CopilotTurnEvent turnEvent)
            {
                _turnEvent = turnEvent;
                (_streamKind, _streamText) = CreateStreamAccumulator(turnEvent);
            }

            public bool TryCoalesce(CopilotTurnEvent next)
            {
                if (_streamText != null && TryGetCompatibleStreamText(next, _streamKind, out var text))
                {
                    _streamText.Append(text);
                    return true;
                }

                return false;
            }

            public CopilotTurnEvent ToTurnEvent()
            {
                if (_streamText == null)
                    return _turnEvent;

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
                    _ => _turnEvent,
                };
            }

            private static (PendingStreamKind Kind, StringBuilder? Text) CreateStreamAccumulator(
                CopilotTurnEvent turnEvent)
            {
                if (turnEvent is CopilotTurnChatDeltaEvent chatDelta)
                {
                    if (chatDelta.Delta.HasReasoning && !chatDelta.Delta.HasContent)
                    {
                        return (
                            PendingStreamKind.ChatReasoning,
                            new StringBuilder(chatDelta.Delta.ReasoningContent));
                    }
                    if (chatDelta.Delta.HasContent && !chatDelta.Delta.HasReasoning)
                    {
                        return (
                            PendingStreamKind.ChatContent,
                            new StringBuilder(chatDelta.Delta.Content));
                    }
                }
                if (turnEvent is CopilotTurnAgentEvent agentEvent)
                {
                    if (agentEvent.Event.Type == CopilotAgentEventType.ReasoningDelta)
                    {
                        return (
                            PendingStreamKind.AgentReasoning,
                            new StringBuilder(agentEvent.Event.Text));
                    }
                    if (agentEvent.Event.Type == CopilotAgentEventType.AnswerDelta)
                    {
                        return (
                            PendingStreamKind.AgentAnswer,
                            new StringBuilder(agentEvent.Event.Text));
                    }
                }

                return (PendingStreamKind.None, null);
            }

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
                            && !chatReasoning.Delta.HasContent:
                        text = chatReasoning.Delta.ReasoningContent;
                        return true;
                    case PendingStreamKind.ChatContent
                        when turnEvent is CopilotTurnChatDeltaEvent chatContent
                            && chatContent.Delta.HasContent
                            && !chatContent.Delta.HasReasoning:
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
