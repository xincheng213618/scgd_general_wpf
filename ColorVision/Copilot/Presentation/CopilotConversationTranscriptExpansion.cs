using System;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed record CopilotConversationTranscriptExpansionResult(
        string Report,
        int EligibleMessageCount,
        int ChangedMessageCount,
        bool? IsExpanded);

    internal static class CopilotConversationTranscriptExpansion
    {
        internal const string Usage = "用法：/transcript [expand|collapse]";

        public static CopilotConversationTranscriptExpansionResult Execute(
            CopilotConversationRecord? conversation,
            string? arguments)
        {
            var traceMessages = conversation?.Messages
                .Where(message => message?.HasThinkingTrace == true)
                .ToArray()
                ?? [];
            if (!TryResolveTarget(arguments, traceMessages, out var isExpanded))
            {
                return new CopilotConversationTranscriptExpansionResult(
                    Usage + "。省略参数时会在全部展开与全部收起之间切换。",
                    traceMessages.Length,
                    0,
                    null);
            }

            if (traceMessages.Length == 0)
            {
                return new CopilotConversationTranscriptExpansionResult(
                    "当前会话没有可展开的推理或工具活动。\n\n"
                    + "该命令只改变本地消息卡显示，不调用模型或工具。",
                    0,
                    0,
                    isExpanded);
            }

            var changedCount = 0;
            foreach (var message in traceMessages)
            {
                if (message.IsThinkingExpanded == isExpanded)
                    continue;

                message.IsThinkingExpanded = isExpanded;
                changedCount++;
            }

            var stateLabel = isExpanded ? "展开" : "收起";
            return new CopilotConversationTranscriptExpansionResult(
                $"当前会话 {traceMessages.Length} 条消息的推理与工具活动已{stateLabel}，"
                + $"其中 {changedCount} 条状态发生变化。\n\n"
                + "只改变本地消息卡显示；不调用模型或工具，不写入聊天历史，"
                + "也不读取隐藏请求或附件正文。",
                traceMessages.Length,
                changedCount,
                isExpanded);
        }

        private static bool TryResolveTarget(
            string? arguments,
            CopilotChatMessage[] traceMessages,
            out bool isExpanded)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                isExpanded = traceMessages.Any(message => !message.IsThinkingExpanded);
                return true;
            }

            if (string.Equals(normalized, "expand", StringComparison.OrdinalIgnoreCase))
            {
                isExpanded = true;
                return true;
            }

            if (string.Equals(normalized, "collapse", StringComparison.OrdinalIgnoreCase))
            {
                isExpanded = false;
                return true;
            }

            isExpanded = false;
            return false;
        }
    }
}
