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
            int customInstructionsCharacters = 0,
            int? modelAutoCompactTokenLimit = null,
            CopilotModelAutoCompactTokenLimitScope modelAutoCompactTokenLimitScope =
                CopilotModelAutoCompactTokenLimitScope.Total)
        {
            var threshold = Math.Clamp(
                autoCompactThresholdPercent,
                CopilotAgentDefaultsConfig.MinimumAutoCompactThresholdPercent,
                CopilotAgentDefaultsConfig.MaximumAutoCompactThresholdPercent);
            var estimatedTokens = CopilotTokenEstimator.WeightToTokenEstimate(usage.ActiveWeight);
            var maximumTokens = CopilotTokenEstimator.WeightToTokenEstimate(usage.MaximumWeight);
            var configuredTokenLimit = Math.Max(0, modelAutoCompactTokenLimit ?? 0);
            var effectiveScope = modelAutoCompactTokenLimitScope ==
                CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix
                    ? CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix
                    : CopilotModelAutoCompactTokenLimitScope.Total;
            var evaluatedWeight = effectiveScope == CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix
                ? usage.BodyAfterPrefixWeight
                : usage.ActiveWeight;
            var evaluatedTokens = evaluatedWeight <= 0
                ? 0
                : CopilotTokenEstimator.WeightToTokenEstimate(evaluatedWeight);
            var toolTip = $"活动历史约占 {usage.UsagePercent:N0}%："
                + $"{estimatedTokens:N0}/{maximumTokens:N0} Token，"
                + $"{usage.ActiveMessageCount:N0}/{usage.MaximumMessages:N0} 条消息。"
                + Environment.NewLine
                + BuildAutoCompactionStatus(
                    usage,
                    autoCompactionEnabled,
                    threshold,
                    configuredTokenLimit,
                    effectiveScope,
                    evaluatedTokens)
                + Environment.NewLine
                + (customInstructionsCharacters > 0
                    ? $"自动压缩会额外保留 {customInstructionsCharacters:N0} 字符的自定义长期重点。"
                    : "自动压缩使用内置默认保留重点。")
                + Environment.NewLine
                + "点击查看完整上下文、附件、项目指令与 Agent 预算诊断。";
            var isNearConfiguredTokenLimit = configuredTokenLimit > 0
                && evaluatedTokens * 100L >= configuredTokenLimit * (100 - WarningLeadPercent);
            var warningThreshold = autoCompactionEnabled
                ? Math.Max(1, threshold - WarningLeadPercent)
                : DisabledWarningPercent;
            var isUnderPressure = !autoCompactionEnabled
                ? usage.UsagePercent >= warningThreshold
                : configuredTokenLimit > 0
                    ? isNearConfiguredTokenLimit || usage.MessageUsagePercent >= warningThreshold
                    : usage.UsagePercent >= warningThreshold;
            return new CopilotConversationContextUsagePresentation(
                $"历史 {usage.UsagePercent:N0}%",
                toolTip,
                isUnderPressure);
        }

        private static string BuildAutoCompactionStatus(
            CopilotConversationContextUsage usage,
            bool autoCompactionEnabled,
            int threshold,
            int configuredTokenLimit,
            CopilotModelAutoCompactTokenLimitScope scope,
            int evaluatedTokens)
        {
            if (!autoCompactionEnabled)
                return "自动压缩已关闭；可在 Copilot 设置的 Agent 页启用。";
            if (configuredTokenLimit > 0)
            {
                var scopeToken = CopilotModelAutoCompactTokenLimitScopeSelection.GetConfigToken(scope);
                var state = evaluatedTokens >= configuredTokenLimit
                    ? "已达到"
                    : $"还差 {configuredTokenLimit - evaluatedTokens:N0} Token 达到";
                return $"Codex {scopeToken} 自动压缩计量为 {evaluatedTokens:N0}/{configuredTokenLimit:N0} Token，{state}阈值；"
                    + $"消息数仍保留 {threshold:N0}% 安全阈值。";
            }

            if (usage.UsagePercent >= threshold)
                return $"已达到 {threshold:N0}% 自动压缩阈值；空闲且有完整新对话时，会在发送前尝试压缩。";

            return $"自动压缩阈值为 {threshold:N0}%，当前还剩 {threshold - usage.UsagePercent:N0} 个百分点。";
        }
    }
}
