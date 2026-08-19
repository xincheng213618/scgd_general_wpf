using System;

namespace ColorVision.Copilot
{
    internal static class CopilotInterruptedResponseRecovery
    {
        public static bool Normalize(CopilotConversationRecord conversation, CopilotChatMessage? assistantMessage)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            if (assistantMessage == null
                || assistantMessage.IsUser
                || (!assistantMessage.IsResponsePending && !assistantMessage.IsThinkingInProgress))
            {
                return false;
            }

            assistantMessage.IsExecutionInProgress = false;
            assistantMessage.IsReasoningInProgress = false;
            var hasUnknownToolOutcome = assistantMessage.CompleteActiveAgentTracesAfterUnexpectedTurnEnd(
                "The application exited");
            assistantMessage.MarkThinkingCompleted();
            assistantMessage.WasResponseInterrupted = true;
            if (string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                var interruptedMessage = hasUnknownToolOutcome
                    ? "回答因应用退出而中断，且已启动工具没有保存权威终态；其外部结果未知。若涉及写入或非幂等操作，请先核对当前状态，不要直接重试。"
                    : "回答因应用退出而中断，未收到可显示内容；可以重试本轮请求。";
                CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, interruptedMessage);
            }

            conversation.Touch();
            return true;
        }
    }
}
