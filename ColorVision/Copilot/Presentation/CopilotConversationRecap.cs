using System;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal static class CopilotConversationRecap
    {
        internal const int MaximumPreviewCharacters = 240;
        internal const int MaximumGoalCharacters = 320;

        internal static string Format(
            CopilotConversationRecord? conversation,
            int queuedFollowUpCount)
        {
            if (conversation == null)
            {
                return "当前没有可回顾的会话。\n\n"
                    + "这是纯本地只读回顾，不调用模型、工具或外部服务。";
            }

            var messages = (conversation.Messages ?? [])
                .Where(message => message != null)
                .ToArray();
            var latestUserIndex = Array.FindLastIndex(messages, message => message.IsUser);
            var latestUser = latestUserIndex >= 0 ? messages[latestUserIndex] : null;
            var latestAssistant = latestUserIndex >= 0
                ? messages.Skip(latestUserIndex + 1).LastOrDefault(message => !message.IsUser)
                : null;

            var builder = new StringBuilder();
            builder.AppendLine("会话回顾");
            builder.AppendLine();
            builder.Append("会话：")
                .Append(Preview(conversation.Title, CopilotConversationRecord.MaximumTitleCharacters))
                .Append(" · 更新于 ")
                .AppendLine(FormatTime(conversation.UpdatedAt));
            builder.Append("可见历史：")
                .Append(messages.Count(message => message.IsUser))
                .Append(" 轮请求 · ")
                .Append(messages.Length)
                .AppendLine(" 条消息");
            builder.Append("持续目标：")
                .AppendLine(FormatGoal(conversation.Goal));
            builder.Append("最近请求：")
                .AppendLine(latestUser == null
                    ? "尚无可见请求"
                    : Preview(latestUser.Content, MaximumPreviewCharacters));
            builder.Append("最近回答：")
                .AppendLine(latestAssistant == null || string.IsNullOrWhiteSpace(latestAssistant.Content)
                    ? "尚无可见回答"
                    : Preview(latestAssistant.Content, MaximumPreviewCharacters));
            builder.Append("本轮状态：")
                .AppendLine(FormatTurnStatus(latestUser, latestAssistant));
            builder.Append("待执行：")
                .Append(Math.Max(0, queuedFollowUpCount))
                .AppendLine(" 条排队后续");
            builder.Append("输入区：草稿 ")
                .Append((conversation.DraftText ?? string.Empty).Length)
                .Append(" 字符 · ")
                .Append(conversation.Attachments?.Count ?? 0)
                .Append(" 个附件 · ")
                .AppendLine(conversation.HasComposerStash ? "有暂存草稿" : "无暂存草稿");
            builder.AppendLine();
            builder.Append("纯本地只读回顾；不调用模型或工具，不读取隐藏请求、附件正文、执行轨迹或路径。");
            return builder.ToString();
        }

        private static string FormatGoal(CopilotConversationGoal? goal)
        {
            if (goal == null)
                return "未设置";

            var state = CopilotConversationGoalStateText.Format(goal.State);
            return $"{state} · {Preview(goal.Objective, MaximumGoalCharacters)}";
        }

        private static string FormatTurnStatus(
            CopilotChatMessage? user,
            CopilotChatMessage? assistant)
        {
            if (user == null)
                return "尚未开始";
            if (assistant == null)
                return "等待回答";
            if (assistant.IsResponsePending
                || assistant.IsThinkingInProgress
                || assistant.IsExecutionInProgress)
            {
                return "正在进行";
            }

            var parts = new System.Collections.Generic.List<string>();
            if (assistant.HasAgentTaskLedger)
                parts.Add(assistant.AgentTaskProgressLabel);
            if (assistant.AgentStopReason != CopilotAgentStopReason.None
                || assistant.HasAgentTaskState)
            {
                parts.Add(assistant.AgentStopReasonLabel);
            }
            else
            {
                parts.Add("回答已记录");
            }
            if (assistant.WasResponseInterrupted)
                parts.Add("回答不完整");
            if (assistant.HasAgentBlockers)
                parts.Add(assistant.AgentBlockerLabel);
            if (assistant.HasRecoverableAgentTasks)
                parts.Add("可操作：" + assistant.AgentRecoveryActionLabel);
            return string.Join(" · ", parts.Distinct(StringComparer.Ordinal));
        }

        private static string FormatTime(DateTime value)
        {
            return value == default
                ? "未知时间"
                : value.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Preview(string? value, int maximumCharacters)
        {
            var normalized = string.Join(
                " ",
                (value ?? string.Empty).Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (normalized.Length == 0)
                return "（空）";
            if (normalized.Length <= maximumCharacters)
                return normalized;

            var retainedLength = Math.Max(1, maximumCharacters - 1);
            if (char.IsHighSurrogate(normalized[retainedLength - 1])
                && char.IsLowSurrogate(normalized[retainedLength]))
            {
                retainedLength--;
            }
            return normalized[..retainedLength].TrimEnd() + "…";
        }
    }
}
