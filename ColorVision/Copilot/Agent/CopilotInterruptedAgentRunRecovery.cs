using System;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotInterruptedAgentRunRecovery
    {
        public static bool Normalize(CopilotConversationRecord conversation, CopilotChatMessage? assistantMessage)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            var checkpoint = conversation.AgentSessionCheckpoint;
            if (assistantMessage == null
                || assistantMessage.IsUser
                || assistantMessage.RequestMode == CopilotAgentMode.Chat
                || !assistantMessage.IsThinkingInProgress
                || checkpoint?.IsStructurallyValid() != true)
            {
                return false;
            }

            var journal = checkpoint.TaskEventJournal;
            var started = journal.Events.LastOrDefault(item => item.Type == CopilotAgentTaskEventType.RunStarted);
            if (started == null || journal.Events.Any(item => item.Sequence > started.Sequence
                && item.Type == CopilotAgentTaskEventType.RunStopped
                && string.Equals(item.RunId, started.RunId, StringComparison.Ordinal)))
            {
                return false;
            }

            var builder = new CopilotAgentTaskEventJournalBuilder(journal, started.RunId);
            assistantMessage.AgentTaskLedger.EnsureValid();
            builder.RecordTaskLedger(assistantMessage.AgentTaskLedger, "interrupted");
            builder.RecordStop(CopilotAgentStopReason.Interrupted);
            var updatedCheckpoint = checkpoint.CopyWithTaskEventJournal(builder.Snapshot());
            if (updatedCheckpoint == null)
                return false;

            conversation.AgentSessionCheckpoint = updatedCheckpoint;
            assistantMessage.AgentStopReason = CopilotAgentStopReason.Interrupted;
            assistantMessage.IsExecutionInProgress = false;
            assistantMessage.IsReasoningInProgress = false;
            assistantMessage.MarkThinkingCompleted();
            if (string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                const string interruptedMessage = "Agent 任务因应用退出而中断；最近的安全进度已经保存，可以继续。";
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
