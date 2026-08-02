using System;
using System.Globalization;

namespace ColorVision.Copilot
{
    internal sealed record CopilotConversationTurnNavigationResult(
        CopilotChatMessage? Message,
        string Report);

    internal static class CopilotConversationTurnNavigation
    {
        public static CopilotConversationTurnNavigationResult Resolve(
            CopilotConversationRecord? conversation,
            string? requestedOrdinal)
        {
            var points = CopilotConversationRewindService.GetPoints(conversation);
            if (points.Count == 0)
            {
                return new CopilotConversationTurnNavigationResult(
                    null,
                    "当前会话还没有可定位的用户请求。");
            }

            var normalized = string.IsNullOrWhiteSpace(requestedOrdinal)
                ? "1"
                : requestedOrdinal.Trim();
            if (CopilotConversationRewindService.TryResolve(
                    conversation,
                    normalized,
                    out var point))
            {
                return new CopilotConversationTurnNavigationResult(
                    point.UserMessage,
                    string.Empty);
            }

            return new CopilotConversationTurnNavigationResult(
                null,
                $"当前会话有 {points.Count.ToString("N0", CultureInfo.CurrentCulture)} 条可定位的用户请求。"
                + "序号必须是大于 0 且不超过该数量的整数；1 表示最近一条，例如 /turn 2。");
        }
    }
}
