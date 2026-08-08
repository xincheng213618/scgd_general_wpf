using System;

namespace ColorVision.Copilot
{
    internal static class CopilotReasoningVisibility
    {
        public static CopilotTurnEvent? FilterForPresentation(
            CopilotTurnEvent turnEvent,
            bool hideAgentReasoning)
        {
            ArgumentNullException.ThrowIfNull(turnEvent);
            if (!hideAgentReasoning)
                return turnEvent;

            return turnEvent switch
            {
                CopilotTurnChatDeltaEvent chatDelta when chatDelta.Delta.HasContent =>
                    new CopilotTurnChatDeltaEvent(
                        new CopilotStreamDelta(string.Empty, chatDelta.Delta.Content)),
                CopilotTurnChatDeltaEvent => null,
                CopilotTurnAgentEvent { Event.Type: CopilotAgentEventType.ReasoningDelta } => null,
                _ => turnEvent,
            };
        }
    }
}
