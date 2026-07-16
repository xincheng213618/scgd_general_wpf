using System;

namespace ColorVision.Copilot
{
    public static class CopilotHostedTurnCompletion
    {
        public static void CompleteSuccessfully(
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            CopilotTokenUsage usage)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            ArgumentNullException.ThrowIfNull(assistantMessage);

            CopilotAssistantMessagePresenter.FinalizeMessage(assistantMessage);
            SetUsage(conversation, usage);
        }

        public static void CompleteCancellation(
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            CopilotAgentControlIntent controlIntent)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            ArgumentNullException.ThrowIfNull(assistantMessage);

            CompleteThinking(assistantMessage);
            if (controlIntent == CopilotAgentControlIntent.Cancel)
            {
                conversation.AgentSessionCheckpoint = null;
                assistantMessage.AgentStopReason = CopilotAgentStopReason.Cancelled;
            }
            else if (controlIntent == CopilotAgentControlIntent.Pause)
            {
                assistantMessage.AgentStopReason = CopilotAgentStopReason.Paused;
            }

            CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, controlIntent == CopilotAgentControlIntent.Pause
                ? "Agent 任务已暂停；可从最近一次可用 checkpoint 继续。"
                : "The current reply was cancelled.");
            conversation.ClearLastUsage();
        }

        public static void CompleteFailure(
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            string? errorMessage,
            params string?[] sensitiveValues)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            ArgumentNullException.ThrowIfNull(assistantMessage);

            CompleteThinking(assistantMessage);
            var normalizedError = CopilotUserFacingErrorFormatter.Sanitize(errorMessage, sensitiveValues);
            CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, $"Request failed: {normalizedError}");
            conversation.ClearLastUsage();
        }

        public static void CompleteQueuedCancellation(CopilotChatMessage assistantMessage)
        {
            ArgumentNullException.ThrowIfNull(assistantMessage);

            CompleteThinking(assistantMessage);
            assistantMessage.AgentStopReason = CopilotAgentStopReason.Cancelled;
            CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, "排队的 Agent 任务已取消，未调用模型或工具。");
        }

        private static void SetUsage(CopilotConversationRecord conversation, CopilotTokenUsage usage)
        {
            if (usage.HasAny)
                conversation.SetLastUsage(usage);
            else
                conversation.ClearLastUsage();
        }

        private static void CompleteThinking(CopilotChatMessage assistantMessage)
        {
            assistantMessage.IsExecutionInProgress = false;
            assistantMessage.IsReasoningInProgress = false;
            assistantMessage.MarkThinkingCompleted();
        }
    }
}
