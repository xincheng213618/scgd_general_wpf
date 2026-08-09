using System;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotTurnAnswerLifecycleState(
        string Text,
        bool IsTruncated)
    {
        public static CopilotTurnAnswerLifecycleState Empty => new(string.Empty, false);

        public CopilotTurnAnswerLifecycleState Observe(CopilotAgentEvent agentEvent)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            if (agentEvent.Type == CopilotAgentEventType.AnswerReset)
                return Empty;
            if (agentEvent.Type != CopilotAgentEventType.AnswerDelta || IsTruncated)
                return this;

            var currentText = Text ?? string.Empty;
            var boundedText = CopilotChatMessage.BoundAssistantDelta(
                currentText.Length,
                agentEvent.Text,
                CopilotChatMessage.ResponseTruncationMarker,
                out var truncated);
            return new CopilotTurnAnswerLifecycleState(currentText + boundedText, truncated);
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
    }
}
