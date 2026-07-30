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
            assistantMessage.CompleteActiveAgentTraces(
                CopilotToolExecutionState.Interrupted,
                CopilotToolFailureKind.Internal,
                "tool_terminal_event_missing",
                "Execution was interrupted when the application exited before an authoritative tool result was saved.");
            assistantMessage.MarkThinkingCompleted();
            assistantMessage.WasResponseInterrupted = true;
            if (string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                const string interruptedMessage = "回答因应用退出而中断，未收到可显示内容；可以重试本轮请求。";
                CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, interruptedMessage);
            }

            conversation.Touch();
            return true;
        }
    }
}
