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
        Interrupted,
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

        public string RecoveryActionLabel => Message.AgentRecoveryActionLabel;

        public string RecoveryToolTip => Message.AgentRecoveryToolTip;

        public string DismissToolTip => Message.HasRecoverableFinalAnswer
            ? "放弃重试最终回答并清除恢复 checkpoint；原任务终态和审计证据仍保留"
            : "放弃任务恢复并清除 checkpoint；原停止原因和审计证据仍保留";

        public string DismissConfirmationText => Message.HasRecoverableFinalAnswer
            ? $"放弃“{Title}”的最终回答恢复项？保存的恢复 checkpoint 会被清除，但已完成任务的终态和审计证据仍会保留。"
            : $"放弃 Agent 任务“{Title}”的恢复项？保存的恢复 checkpoint 会被清除，但原停止原因和审计证据仍会保留。";

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
            CopilotAgentTaskAttentionKind.Interrupted => "应用中断，可继续",
            _ => string.Empty,
        };

        public string DetailLabel
        {
            get
            {
                var blocker = Message.AgentBlockers.FirstOrDefault(item => item != null && item.IsStructurallyValid());
                if (blocker?.Kind == CopilotAgentBlockerKind.ProviderOutput)
                    return blocker.Code switch
                    {
                        "provider_interrupted" => Conversation.AgentSessionCheckpoint == null
                            ? "恢复点未能保存，请重新发送请求"
                            : "已保存当前进度，可安全恢复",
                        "provider_output_length" => "最终回答达到输出上限，已保留部分内容",
                        "provider_content_filtered" => "最终回答被内容策略提前停止",
                        "provider_output_finish_reason" => "最终回答以未确认完成的状态结束",
                        _ => "模型未返回最终回答",
                    };
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
            task.Message.IsAgentRecoveryDismissed = true;
            task.Conversation.ClearAgentActivityForMessage(task.Message.Id);
            task.Conversation.Touch();
            task.Conversation.RefreshSummary();
            return true;
        }

        private static CopilotAgentTaskSummary? CreateSummary(CopilotConversationRecord conversation)
        {
            var message = conversation.Messages.LastOrDefault(candidate => candidate != null && !candidate.IsUser);
            if (message == null
                || message.IsAgentRecoveryDismissed
                || (message.AgentTaskLedger.RemainingCount <= 0 && !message.HasRecoverableAgentTasks))
                return null;

            var attentionKind = message.AgentStopReason switch
            {
                CopilotAgentStopReason.Completed when message.HasRecoverableFinalAnswer =>
                    CopilotAgentTaskAttentionKind.IncompleteOutput,
                CopilotAgentStopReason.Paused => CopilotAgentTaskAttentionKind.Paused,
                CopilotAgentStopReason.AwaitingUser => CopilotAgentTaskAttentionKind.AwaitingUser,
                CopilotAgentStopReason.ApprovalDenied => CopilotAgentTaskAttentionKind.ApprovalDenied,
                CopilotAgentStopReason.Blocked => CopilotAgentTaskAttentionKind.Blocked,
                CopilotAgentStopReason.BudgetExhausted => CopilotAgentTaskAttentionKind.BudgetExhausted,
                CopilotAgentStopReason.TaskPassLimit => CopilotAgentTaskAttentionKind.TaskPassLimit,
                CopilotAgentStopReason.IncompleteOutput => CopilotAgentTaskAttentionKind.IncompleteOutput,
                CopilotAgentStopReason.ProviderFailure => CopilotAgentTaskAttentionKind.ProviderFailure,
                CopilotAgentStopReason.Interrupted => CopilotAgentTaskAttentionKind.Interrupted,
                _ => (CopilotAgentTaskAttentionKind?)null,
            };

            return attentionKind == null ? null : new CopilotAgentTaskSummary(conversation, message, attentionKind.Value);
        }
    }
}
