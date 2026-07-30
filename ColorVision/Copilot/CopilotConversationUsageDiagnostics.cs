using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    internal sealed record CopilotConversationUsageSnapshot(
        CopilotTokenUsage TotalUsage,
        CopilotTokenUsage LastUsage,
        int TrackedResponses,
        int UnreportedResponses,
        int ActiveResponses);

    internal static class CopilotConversationUsageDiagnostics
    {
        public static CopilotConversationUsageSnapshot Capture(CopilotConversationRecord? conversation)
        {
            if (conversation == null)
                return new CopilotConversationUsageSnapshot(CopilotTokenUsage.Empty, CopilotTokenUsage.Empty, 0, 0, 0);

            var assistantMessages = conversation.Messages
                .Where(message => message != null && !message.IsUser)
                .ToArray();
            var activeResponses = assistantMessages.Count(message =>
                message.IsResponsePending
                || message.IsThinkingInProgress
                || message.IsExecutionInProgress);
            var completedResponses = assistantMessages
                .Where(message =>
                    !message.IsResponsePending
                    && !message.IsThinkingInProgress
                    && !message.IsExecutionInProgress)
                .ToArray();
            var trackedResponses = completedResponses
                .Where(message => message.ReportedUsage.HasAny)
                .ToArray();
            var totalUsage = trackedResponses.Aggregate(
                CopilotTokenUsage.Empty,
                (total, message) => total.Add(message.ReportedUsage));
            var lastUsage = trackedResponses.LastOrDefault()?.ReportedUsage ?? CopilotTokenUsage.Empty;
            return new CopilotConversationUsageSnapshot(
                totalUsage,
                lastUsage,
                trackedResponses.Length,
                completedResponses.Length - trackedResponses.Length,
                activeResponses);
        }

        public static string Format(CopilotConversationRecord? conversation)
        {
            if (conversation == null)
                return "使用量" + Environment.NewLine + "当前没有可统计的 Copilot 会话。";

            var snapshot = Capture(conversation);
            var title = string.IsNullOrWhiteSpace(conversation.Title)
                ? CopilotUiText.NewConversationTitle
                : conversation.Title.Trim();
            var builder = new StringBuilder()
                .Append("使用量 · ")
                .AppendLine(title);
            if (!snapshot.TotalUsage.HasAny)
            {
                builder.AppendLine("尚未收到可累计的 Provider Token 元数据。");
            }
            else
            {
                builder
                    .Append("已记录回答：")
                    .Append(snapshot.TrackedResponses.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine()
                    .Append("累计输入：")
                    .Append(FormatTokens(snapshot.TotalUsage.InputTokens))
                    .Append(" · 输出：")
                    .Append(FormatTokens(snapshot.TotalUsage.OutputTokens))
                    .Append(" · 总计：")
                    .AppendLine(FormatTokens(snapshot.TotalUsage.EffectiveTotalTokens));
                if (snapshot.TotalUsage.CachedInputTokens.HasValue)
                {
                    builder
                        .Append("累计缓存输入：")
                        .Append(FormatTokens(snapshot.TotalUsage.EffectiveCachedInputTokens))
                        .Append('（')
                        .Append(snapshot.TotalUsage.CachedInputPercentage.ToString("0.#", CultureInfo.CurrentCulture))
                        .AppendLine("%）");
                }
                builder
                    .Append("最近一轮：输入 ")
                    .Append(FormatTokens(snapshot.LastUsage.InputTokens))
                    .Append(" · 输出 ")
                    .Append(FormatTokens(snapshot.LastUsage.OutputTokens))
                    .Append(" · 总计 ")
                    .AppendLine(FormatTokens(snapshot.LastUsage.EffectiveTotalTokens));
            }

            if (snapshot.UnreportedResponses > 0)
            {
                builder
                    .Append("未纳入：")
                    .Append(snapshot.UnreportedResponses.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 条旧回答、失败回答或未返回 Token 元数据的回答。");
            }
            if (snapshot.ActiveResponses > 0)
            {
                builder
                    .Append("进行中：")
                    .Append(snapshot.ActiveResponses.ToString("N0", CultureInfo.CurrentCulture))
                    .AppendLine(" 条回答将在成功完成并收到 Token 元数据后计入。");
            }

            builder.Append("范围：仅统计当前会话消息中由 Provider 返回并由应用保存的 Token；不代表账户账单、套餐余额、费用或速率限制。");
            return builder.ToString();
        }

        private static string FormatTokens(int value)
        {
            return Math.Max(0, value).ToString("N0", CultureInfo.CurrentCulture);
        }
    }
}
