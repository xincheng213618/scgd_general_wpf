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

            if (assistantMessage.RequestMode == CopilotAgentMode.Chat
                && !string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                assistantMessage.MarkResponseInterrupted("回答已停止；已保留现有内容，但回答可能不完整。");
            }

            CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, controlIntent == CopilotAgentControlIntent.Pause
                ? "Agent 任务已暂停；可从最近一次可用 checkpoint 继续。"
                : assistantMessage.RequestMode == CopilotAgentMode.Chat
                    ? "当前回答已停止。"
                    : "Agent 任务已取消；本轮新 checkpoint 已丢弃。");
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
            if (!string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                assistantMessage.MarkResponseInterrupted(
                    $"回复生成过程中发生错误；已保留现有内容，但回答可能不完整。错误：{normalizedError}");
            }
            else
            {
                CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, $"请求失败：{normalizedError}");
            }
            conversation.ClearLastUsage();
        }

        public static void CompleteBeforeStartCancellation(CopilotChatMessage assistantMessage)
        {
            ArgumentNullException.ThrowIfNull(assistantMessage);

            CompleteThinking(assistantMessage);
            if (assistantMessage.RequestMode != CopilotAgentMode.Chat)
                assistantMessage.AgentStopReason = CopilotAgentStopReason.Cancelled;
            CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, assistantMessage.RequestMode == CopilotAgentMode.Chat
                ? "请求已取消，尚未调用模型。"
                : "排队的 Agent 任务已取消，未调用模型或工具。");
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
