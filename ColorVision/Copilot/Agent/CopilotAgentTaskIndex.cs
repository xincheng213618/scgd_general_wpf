using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotAgentTaskAttentionKind
    {
        Paused,
        AwaitingUser,
        ApprovalDenied,
        Blocked,
        BudgetExhausted,
        TaskPassLimit,
        IncompleteOutput,
        ProviderFailure,
    }

    public sealed class CopilotAgentTaskSummary
    {
        internal CopilotAgentTaskSummary(
            CopilotConversationRecord conversation,
            CopilotChatMessage message,
            CopilotAgentTaskAttentionKind attentionKind)
        {
            Conversation = conversation;
            Message = message;
            AttentionKind = attentionKind;
        }

        public CopilotConversationRecord Conversation { get; }

        public CopilotChatMessage Message { get; }

        public CopilotAgentTaskAttentionKind AttentionKind { get; }

        public string ConversationId => Conversation.Id;

        public string Title => Conversation.Title;

        public string UpdatedLabel => Conversation.UpdatedLabel;

        public int RemainingCount => Message.AgentTaskLedger.RemainingCount;

        public bool CanResume => Conversation.AgentSessionCheckpoint != null && Message.HasRecoverableAgentTasks;

        public string StatusLabel => AttentionKind switch
        {
            CopilotAgentTaskAttentionKind.Paused => "已暂停",
            CopilotAgentTaskAttentionKind.AwaitingUser => "等待回复",
            CopilotAgentTaskAttentionKind.ApprovalDenied => "审批未通过",
            CopilotAgentTaskAttentionKind.Blocked => "任务受阻",
            CopilotAgentTaskAttentionKind.BudgetExhausted => "预算耗尽",
            CopilotAgentTaskAttentionKind.TaskPassLimit => "达到轮次上限",
            CopilotAgentTaskAttentionKind.IncompleteOutput => "等待最终回答",
            CopilotAgentTaskAttentionKind.ProviderFailure => "模型连接中断",
            _ => string.Empty,
        };

        public string DetailLabel
        {
            get
            {
                var blocker = Message.AgentBlockers.FirstOrDefault(item => item != null && item.IsStructurallyValid());
                if (blocker?.Kind == CopilotAgentBlockerKind.ProviderOutput)
                    return blocker.Code == "provider_interrupted"
                        ? Conversation.AgentSessionCheckpoint == null ? "恢复点未能保存，请重新发送请求" : "已保存当前进度，可安全恢复"
                        : "模型未返回最终回答";
                if (blocker != null && !string.IsNullOrWhiteSpace(blocker.Summary))
                    return blocker.Summary;

                return RemainingCount > 0 ? $"剩余 {RemainingCount} 项" : StatusLabel;
            }
        }
    }

    public static class CopilotAgentTaskIndex
    {
        public static IReadOnlyList<CopilotAgentTaskSummary> Build(IEnumerable<CopilotConversationRecord>? conversations)
        {
            return (conversations ?? Array.Empty<CopilotConversationRecord>())
                .Where(conversation => conversation != null)
                .Select(CreateSummary)
                .Where(summary => summary != null)
                .Cast<CopilotAgentTaskSummary>()
                .OrderByDescending(summary => summary.Conversation.UpdatedAt)
                .ToArray();
        }

        public static bool Dismiss(CopilotAgentTaskSummary? task)
        {
            if (task == null)
                return false;

            task.Conversation.AgentSessionCheckpoint = null;
            task.Message.AgentStopReason = CopilotAgentStopReason.Cancelled;
            task.Message.AgentBlockers = Array.Empty<CopilotAgentBlockerSnapshot>();
            task.Conversation.Touch();
            task.Conversation.RefreshSummary();
            return true;
        }

        private static CopilotAgentTaskSummary? CreateSummary(CopilotConversationRecord conversation)
        {
            var message = conversation.Messages.LastOrDefault(candidate => candidate != null && !candidate.IsUser);
            if (message == null
                || (message.AgentTaskLedger.RemainingCount <= 0 && !message.HasRecoverableAgentTasks))
                return null;

            var attentionKind = message.AgentStopReason switch
            {
                CopilotAgentStopReason.Paused => CopilotAgentTaskAttentionKind.Paused,
                CopilotAgentStopReason.AwaitingUser => CopilotAgentTaskAttentionKind.AwaitingUser,
                CopilotAgentStopReason.ApprovalDenied => CopilotAgentTaskAttentionKind.ApprovalDenied,
                CopilotAgentStopReason.Blocked => CopilotAgentTaskAttentionKind.Blocked,
                CopilotAgentStopReason.BudgetExhausted => CopilotAgentTaskAttentionKind.BudgetExhausted,
                CopilotAgentStopReason.TaskPassLimit => CopilotAgentTaskAttentionKind.TaskPassLimit,
                CopilotAgentStopReason.IncompleteOutput => CopilotAgentTaskAttentionKind.IncompleteOutput,
                CopilotAgentStopReason.ProviderFailure => CopilotAgentTaskAttentionKind.ProviderFailure,
                _ => (CopilotAgentTaskAttentionKind?)null,
            };

            return attentionKind == null ? null : new CopilotAgentTaskSummary(conversation, message, attentionKind.Value);
        }
    }
}
