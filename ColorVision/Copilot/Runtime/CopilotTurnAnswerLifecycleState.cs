using System;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotTurnAnswerLifecycleState
    {
        private readonly AnswerChunk? _lastChunk;
        private readonly int _length;

        private CopilotTurnAnswerLifecycleState(AnswerChunk lastChunk, int length, bool isTruncated)
        {
            _lastChunk = lastChunk;
            _length = length;
            IsTruncated = isTruncated;
        }

        public static CopilotTurnAnswerLifecycleState Empty => default;

        public bool IsTruncated { get; }

        public string Text
        {
            get
            {
                if (_lastChunk == null)
                    return string.Empty;
                if (_lastChunk.Previous == null)
                    return _lastChunk.Text;

                return string.Create(_length, _lastChunk, static (destination, chunk) =>
                {
                    var offset = destination.Length;
                    for (AnswerChunk? current = chunk; current != null; current = current.Previous)
                    {
                        offset -= current.Text.Length;
                        current.Text.AsSpan().CopyTo(destination[offset..]);
                    }
                });
            }
        }

        public CopilotTurnAnswerLifecycleState Observe(CopilotAgentEvent agentEvent)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            if (agentEvent.Type == CopilotAgentEventType.AnswerReset)
                return Empty;
            if (agentEvent.Type != CopilotAgentEventType.AnswerDelta || IsTruncated)
                return this;

            var boundedText = CopilotChatMessage.BoundAssistantDelta(
                _length,
                agentEvent.Text,
                CopilotChatMessage.ResponseTruncationMarker,
                out var truncated);
            return boundedText.Length == 0 ? this : new CopilotTurnAnswerLifecycleState(
                new AnswerChunk(boundedText, _lastChunk), _length + boundedText.Length, truncated);
        }

        public void ValidateSnapshot(string? text, bool isTruncated)
        {
            if (!IsValidSnapshot(text, isTruncated))
                throw new InvalidOperationException("Copilot Review emitted an invalid final review text snapshot.");
            if (IsTruncated != isTruncated
                || !string.Equals(Text, text, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Copilot Review final text snapshot did not match its streamed answer lifecycle.");
            }
        }

        public static bool IsValidSnapshot(string? text, bool isTruncated)
        {
            if (text == null || text.Length > CopilotChatMessage.MaximumAssistantTextCharacters)
                return false;

            return !isTruncated
                || text.EndsWith(
                    CopilotChatMessage.ResponseTruncationMarker,
                    StringComparison.Ordinal);
        }

        // Reducer snapshots share immutable chunks, never a mutable append buffer.
        private sealed class AnswerChunk(string text, AnswerChunk? previous)
        {
            public string Text { get; } = text;
            public AnswerChunk? Previous { get; } = previous;
        }
    }
}
