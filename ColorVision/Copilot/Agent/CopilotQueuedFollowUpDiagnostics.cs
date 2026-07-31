using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal enum CopilotQueuedFollowUpCommandAction
    {
        List,
        SendNow,
        Edit,
        MoveUp,
        MoveDown,
        Delete,
        Invalid,
    }

    internal sealed record CopilotQueuedFollowUpCommandRequest(
        CopilotQueuedFollowUpCommandAction Action,
        int QueuePosition);

    internal static class CopilotQueuedFollowUpDiagnostics
    {
        private const int MaximumListedItems = CopilotAgentTaskHost.MaximumQueuedRuns;
        private const int MaximumPreviewCharacters = 120;
        public const string Usage = "用法：/queue [send|edit|up|down|delete] N。N 是 /queue 列表显示的全局 #N；省略参数时只查看当前会话队列。";

        public static CopilotQueuedFollowUpCommandRequest ParseCommand(string? arguments)
        {
            var parts = (arguments ?? string.Empty).Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 0)
                return new CopilotQueuedFollowUpCommandRequest(CopilotQueuedFollowUpCommandAction.List, 0);
            if (parts.Length != 2
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var queuePosition)
                || queuePosition <= 0)
            {
                return new CopilotQueuedFollowUpCommandRequest(CopilotQueuedFollowUpCommandAction.Invalid, 0);
            }

            var action = parts[0].ToLowerInvariant() switch
            {
                "send" or "now" => CopilotQueuedFollowUpCommandAction.SendNow,
                "edit" => CopilotQueuedFollowUpCommandAction.Edit,
                "up" => CopilotQueuedFollowUpCommandAction.MoveUp,
                "down" => CopilotQueuedFollowUpCommandAction.MoveDown,
                "delete" or "remove" or "cancel" => CopilotQueuedFollowUpCommandAction.Delete,
                _ => CopilotQueuedFollowUpCommandAction.Invalid,
            };
            return new CopilotQueuedFollowUpCommandRequest(action, queuePosition);
        }

        public static CopilotQueuedFollowUp? FindByPosition(
            IEnumerable<CopilotQueuedFollowUp>? queuedFollowUps,
            string? conversationId,
            int queuePosition)
        {
            if (string.IsNullOrWhiteSpace(conversationId) || queuePosition <= 0)
                return null;

            return (queuedFollowUps ?? Array.Empty<CopilotQueuedFollowUp>())
                .FirstOrDefault(item => item != null
                    && item.QueuePosition == queuePosition
                    && string.Equals(item.ConversationId, conversationId, StringComparison.Ordinal));
        }

        public static string Format(
            IEnumerable<CopilotQueuedFollowUp>? queuedFollowUps,
            string? conversationId)
        {
            if (string.IsNullOrWhiteSpace(conversationId))
                return "当前没有可用于查看排队请求的会话。";

            var items = (queuedFollowUps ?? Array.Empty<CopilotQueuedFollowUp>())
                .Where(item => item != null
                    && string.Equals(item.ConversationId, conversationId, StringComparison.Ordinal))
                .OrderBy(item => item.QueuePosition > 0 ? item.QueuePosition : int.MaxValue)
                .ThenBy(item => item.QueuedAtUtc)
                .ToArray();
            if (items.Length == 0)
                return "当前会话没有排队的后续请求。";

            var builder = new StringBuilder();
            builder.Append("当前会话排队 · ")
                .Append(items.Length.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine();
            builder.AppendLine();
            builder.AppendLine("这些请求会在前序任务结束后按全局队列顺序开始；列表本身只读，使用 /queue 动作 N 显式调整。");
            foreach (var item in items.Take(MaximumListedItems))
            {
                builder.Append(item.PositionLabel)
                    .Append(" · ")
                    .Append(FormatMode(item.Mode))
                    .Append(" · ")
                    .Append(BuildPreview(item.Prompt));
                if (item.SubmissionContext.Attachments.Count > 0)
                {
                    builder.Append(" · 附件 ")
                        .Append(item.SubmissionContext.Attachments.Count.ToString("N0", CultureInfo.CurrentCulture));
                }
                if (item.IsAutomaticGoalContinuation)
                    builder.Append(" · 持续目标");
                builder.AppendLine();
            }
            if (items.Length > MaximumListedItems)
            {
                builder.Append("…另有 ")
                    .Append((items.Length - MaximumListedItems).ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 条请求未显示。");
            }

            builder.AppendLine();
            builder.Append("可用动作：send、edit、up、down、delete。报告不包含内部任务 ID、附件正文、活动文档或工作区路径。");
            return builder.ToString();
        }

        private static string FormatMode(CopilotAgentMode mode)
        {
            return mode switch
            {
                CopilotAgentMode.Chat => "Chat",
                CopilotAgentMode.Auto => "Auto",
                CopilotAgentMode.Explain => "Explain",
                CopilotAgentMode.Web => "Web",
                CopilotAgentMode.Code => "Code",
                CopilotAgentMode.Review => "Review",
                CopilotAgentMode.Diagnose => "Diagnose",
                CopilotAgentMode.Plan => "Plan",
                _ => "Chat",
            };
        }

        private static string BuildPreview(string? prompt)
        {
            var normalized = string.Join(
                " ",
                (prompt ?? string.Empty).Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            if (normalized.Length == 0)
                return "（空请求）";
            return normalized.Length <= MaximumPreviewCharacters
                ? normalized
                : normalized[..MaximumPreviewCharacters].TrimEnd() + "…";
        }
    }
}
