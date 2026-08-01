using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    internal enum CopilotConversationRetentionBlocker
    {
        None,
        ScheduledRun,
        PendingApproval,
        QueuedFollowUp,
        ActiveGoal,
        RecoverableAgentTask,
        MessageEdit,
    }

    internal static class CopilotConversationRetentionPolicy
    {
        internal static CopilotConversationRetentionBlocker Evaluate(
            CopilotConversationRecord conversation,
            bool hasScheduledRun,
            bool hasPendingApproval,
            bool hasQueuedFollowUp,
            bool isEditingMessage)
        {
            ArgumentNullException.ThrowIfNull(conversation);

            if (hasScheduledRun)
                return CopilotConversationRetentionBlocker.ScheduledRun;
            if (hasPendingApproval)
                return CopilotConversationRetentionBlocker.PendingApproval;
            if (hasQueuedFollowUp)
                return CopilotConversationRetentionBlocker.QueuedFollowUp;
            if (conversation.Goal?.IsActive == true)
                return CopilotConversationRetentionBlocker.ActiveGoal;
            if (conversation.AgentSessionCheckpoint != null
                || CopilotAgentTaskIndex.Build([conversation]).Count > 0)
            {
                return CopilotConversationRetentionBlocker.RecoverableAgentTask;
            }
            if (isEditingMessage)
                return CopilotConversationRetentionBlocker.MessageEdit;

            return CopilotConversationRetentionBlocker.None;
        }

        internal static string Describe(CopilotConversationRetentionBlocker blocker) => blocker switch
        {
            CopilotConversationRetentionBlocker.ScheduledRun => "仍有正在运行或排队的 Agent 请求",
            CopilotConversationRetentionBlocker.PendingApproval => "仍有待确认操作",
            CopilotConversationRetentionBlocker.QueuedFollowUp => "仍有排队后续请求",
            CopilotConversationRetentionBlocker.ActiveGoal => "持续目标仍在运行",
            CopilotConversationRetentionBlocker.RecoverableAgentTask => "仍有可恢复 Agent 任务",
            CopilotConversationRetentionBlocker.MessageEdit => "正在编辑该会话中的历史请求",
            _ => string.Empty,
        };

        internal static CopilotConversationRecord? FindNearestActive(
            IReadOnlyList<CopilotConversationRecord> conversations,
            int preferredIndex)
        {
            ArgumentNullException.ThrowIfNull(conversations);
            if (conversations.Count == 0)
                return null;

            var startIndex = Math.Clamp(preferredIndex, 0, conversations.Count - 1);
            for (var distance = 0; distance < conversations.Count; distance++)
            {
                var nextIndex = startIndex + distance;
                if (nextIndex < conversations.Count && !conversations[nextIndex].IsArchived)
                    return conversations[nextIndex];

                var previousIndex = startIndex - distance;
                if (distance > 0
                    && previousIndex >= 0
                    && !conversations[previousIndex].IsArchived)
                {
                    return conversations[previousIndex];
                }
            }

            return null;
        }
    }
}
