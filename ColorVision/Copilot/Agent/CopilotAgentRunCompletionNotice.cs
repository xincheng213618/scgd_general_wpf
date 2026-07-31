using System;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed record CopilotAgentRunCompletionNotice(
        string ConversationId,
        string Text);

    internal static class CopilotAgentRunCompletionNoticePolicy
    {
        private const int MaximumTitleLength = 120;

        public static CopilotAgentRunCompletionNotice? Create(
            CopilotHostedAgentRun? run,
            CopilotConversationRecord? conversation,
            string? selectedConversationId)
        {
            if (run?.IsAgent != true
                || run.State != CopilotHostedRunState.Completed
                || conversation == null
                || !string.Equals(run.ConversationId, conversation.Id, StringComparison.Ordinal)
                || string.Equals(conversation.Id, selectedConversationId, StringComparison.Ordinal))
            {
                return null;
            }

            var assistantMessage = conversation.Messages.LastOrDefault(message => message != null && !message.IsUser);
            var stopReason = assistantMessage?.AgentStopReason is { } messageStopReason
                    && messageStopReason != CopilotAgentStopReason.None
                ? messageStopReason
                : run.AgentStopReason;
            var status = assistantMessage?.HasRecoverableFinalAnswer == true
                ? "等待最终回答"
                : FormatStatus(stopReason);
            var title = NormalizeTitle(conversation.Title);
            return new CopilotAgentRunCompletionNotice(
                conversation.Id,
                $"{title} · {status}");
        }

        private static string FormatStatus(CopilotAgentStopReason stopReason)
        {
            return stopReason switch
            {
                CopilotAgentStopReason.Completed => "已完成",
                CopilotAgentStopReason.AwaitingUser => "等待回复",
                CopilotAgentStopReason.ApprovalDenied => "审批未通过",
                CopilotAgentStopReason.BudgetExhausted => "预算耗尽",
                CopilotAgentStopReason.TaskPassLimit => "达到轮次上限",
                CopilotAgentStopReason.Blocked => "任务受阻",
                CopilotAgentStopReason.Paused => "已暂停，可继续",
                CopilotAgentStopReason.Cancelled => "已取消",
                CopilotAgentStopReason.IncompleteOutput => "等待最终回答",
                CopilotAgentStopReason.ProviderFailure => "模型连接中断",
                CopilotAgentStopReason.Interrupted => "应用中断，可继续",
                _ => "已结束",
            };
        }

        private static string NormalizeTitle(string? title)
        {
            var normalized = string.Join(
                " ",
                (title ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (normalized.Length == 0)
                return CopilotUiText.NewConversationTitle;
            if (normalized.Length <= MaximumTitleLength)
                return normalized;

            var retainedLength = MaximumTitleLength;
            if (char.IsHighSurrogate(normalized[retainedLength - 1])
                && char.IsLowSurrogate(normalized[retainedLength]))
            {
                retainedLength--;
            }
            return normalized[..retainedLength].TrimEnd() + "...";
        }
    }
}
