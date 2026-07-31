using System;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotConversationContextUsagePresentation(
        string Label,
        string ToolTip,
        bool IsUnderPressure);

    internal static class CopilotConversationContextUsagePresenter
    {
        private const int WarningLeadPercent = 10;
        private const int DisabledWarningPercent = 75;

        public static CopilotConversationContextUsagePresentation Create(
            CopilotConversationContextUsage usage,
            bool autoCompactionEnabled,
            int autoCompactThresholdPercent,
            int customInstructionsCharacters = 0)
        {
            var threshold = Math.Clamp(
                autoCompactThresholdPercent,
                CopilotAgentDefaultsConfig.MinimumAutoCompactThresholdPercent,
                CopilotAgentDefaultsConfig.MaximumAutoCompactThresholdPercent);
            var estimatedTokens = CopilotTokenEstimator.WeightToTokenEstimate(usage.ActiveWeight);
            var maximumTokens = CopilotTokenEstimator.WeightToTokenEstimate(usage.MaximumWeight);
            var toolTip = $"活动历史约占 {usage.UsagePercent:N0}%："
                + $"{estimatedTokens:N0}/{maximumTokens:N0} Token，"
                + $"{usage.ActiveMessageCount:N0}/{usage.MaximumMessages:N0} 条消息。"
                + Environment.NewLine
                + BuildAutoCompactionStatus(usage.UsagePercent, autoCompactionEnabled, threshold)
                + Environment.NewLine
                + (customInstructionsCharacters > 0
                    ? $"自动压缩会额外保留 {customInstructionsCharacters:N0} 字符的自定义长期重点。"
                    : "自动压缩使用内置默认保留重点。")
                + Environment.NewLine
                + "点击查看完整上下文、附件、项目指令与 Agent 预算诊断。";
            var warningThreshold = autoCompactionEnabled
                ? Math.Max(1, threshold - WarningLeadPercent)
                : DisabledWarningPercent;
            return new CopilotConversationContextUsagePresentation(
                $"历史 {usage.UsagePercent:N0}%",
                toolTip,
                usage.UsagePercent >= warningThreshold);
        }

        private static string BuildAutoCompactionStatus(
            int usagePercent,
            bool autoCompactionEnabled,
            int threshold)
        {
            if (!autoCompactionEnabled)
                return "自动压缩已关闭；可在 Copilot 设置的 Agent 页启用。";
            if (usagePercent >= threshold)
            {
                return $"已达到 {threshold:N0}% 自动压缩阈值；空闲且有完整新对话时，会在发送前尝试压缩。";
            }

            return $"自动压缩阈值为 {threshold:N0}%，当前还剩 {threshold - usagePercent:N0} 个百分点。";
        }
    }
}
