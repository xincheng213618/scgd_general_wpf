using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotAgentEventBuffer
    {
        public const int DefaultMaximumPendingCharacters = 64 * 1024;
        public const int DefaultMaximumPendingEvents = 128;

        private readonly CopilotUiBatchBuffer<CopilotAgentEvent> _buffer;

        public CopilotAgentEventBuffer(
            SynchronizationContext? targetContext,
            Action<IReadOnlyList<CopilotAgentEvent>> applyBatch,
            int maximumPendingCharacters = DefaultMaximumPendingCharacters,
            int maximumPendingEvents = DefaultMaximumPendingEvents,
            Func<bool>? isOnTargetThread = null)
        {
            _buffer = new CopilotUiBatchBuffer<CopilotAgentEvent>(
                targetContext,
                applyBatch,
                new PendingAgentEventBatch(),
                maximumPendingCharacters,
                maximumPendingEvents,
                postOnTargetThread: true,
                isOnTargetThread);
        }

        public void Enqueue(CopilotAgentEvent agentEvent)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            _buffer.Enqueue(agentEvent);
        }

        public Task CompleteAsync() => _buffer.CompleteAsync();

        private sealed class PendingAgentEventBatch : ICopilotPendingBatch<CopilotAgentEvent>
        {
            private readonly List<PendingEvent> _events = new();

            public int Count => _events.Count;

            public int Size { get; private set; }

            public void Add(CopilotAgentEvent agentEvent)
            {
                Size += Math.Max(1, agentEvent.Text?.Length ?? 0);
                if (_events.Count > 0 && _events[^1].CanAppend(agentEvent))
                {
                    _events[^1].Append(agentEvent.Text);
                    return;
                }

                _events.Add(new PendingEvent(agentEvent));
            }

            public IReadOnlyList<CopilotAgentEvent> Drain()
            {
                var batch = new CopilotAgentEvent[_events.Count];
                for (var index = 0; index < _events.Count; index++)
                    batch[index] = _events[index].ToAgentEvent();
                _events.Clear();
                Size = 0;
                return batch;
            }
        }

        private sealed class PendingEvent
        {
            private readonly CopilotAgentEvent? _original;
            private readonly StringBuilder? _streamedText;

            public PendingEvent(CopilotAgentEvent agentEvent)
            {
                Type = agentEvent.Type;
                if (IsStreamDelta(Type))
                {
                    _streamedText = new StringBuilder(agentEvent.Text ?? string.Empty);
                }
                else
                {
                    _original = agentEvent;
                }
            }

            public CopilotAgentEventType Type { get; }

            public void Append(string? text) => _streamedText!.Append(text ?? string.Empty);

            public bool CanAppend(CopilotAgentEvent agentEvent) => Type == agentEvent.Type && IsStreamDelta(Type);

            public CopilotAgentEvent ToAgentEvent()
            {
                if (_original != null)
                    return _original;
                return Type == CopilotAgentEventType.ReasoningDelta
                    ? CopilotAgentEvent.ReasoningDelta(_streamedText!.ToString())
                    : CopilotAgentEvent.AnswerDelta(_streamedText!.ToString());
            }

            private static bool IsStreamDelta(CopilotAgentEventType type) =>
                type is CopilotAgentEventType.ReasoningDelta or CopilotAgentEventType.AnswerDelta;
        }
    }
}
