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
            assistantMessage.MarkThinkingCompleted();
            assistantMessage.WasResponseInterrupted = true;
            if (string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                const string interruptedMessage = "回答因应用退出而中断，未收到可显示内容；可以重试本轮请求。";
                if (assistantMessage.UsesResponseTimeline)
                    assistantMessage.AppendResponseTimelineText(interruptedMessage);
                else
                    assistantMessage.Content = interruptedMessage;
            }

            conversation.Touch();
            return true;
        }
    }
}
