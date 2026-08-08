using System;
using System.Linq;

namespace ColorVision.Copilot
{
    internal enum CopilotConversationAutoCompactionTrigger
    {
        None,
        HistoryWeight,
        ConfiguredTokenLimit,
        MessageCount,
    }

    internal readonly record struct CopilotConversationAutoCompactionOptions(
        bool Enabled,
        int ThresholdPercent,
        int? ModelTokenLimit,
        CopilotModelAutoCompactTokenLimitScope ModelTokenLimitScope);

    internal readonly record struct CopilotConversationAutoCompactionDecision(
        bool ShouldCompact,
        CopilotConversationAutoCompactionTrigger Trigger,
        int UsagePercent,
        int ActiveMessageCount,
        long ActiveWeight,
        int EvaluatedTokens,
        int ThresholdTokens,
        CopilotModelAutoCompactTokenLimitScope TokenLimitScope);

    internal readonly record struct CopilotConversationContextUsage(
        int UsagePercent,
        int WeightUsagePercent,
        int MessageUsagePercent,
        int ActiveMessageCount,
        long ActiveWeight,
        long CarriedPrefixWeight,
        long BodyAfterPrefixWeight,
        int MaximumMessages,
        long MaximumWeight);

    internal static class CopilotConversationAutoCompactionPolicy
    {
        private const int MinimumNewMessages = 2;

        public static CopilotConversationContextUsage Measure(
            CopilotConversationRecord? conversation,
            CopilotConversationHistoryLimits limits,
            string? pendingPrompt)
        {
            if (limits.MaximumMessages <= 0 || limits.MaximumCharacters <= 0)
                return default;

            var history = conversation == null
                ? Array.Empty<CopilotRequestMessage>()
                : CopilotConversationCompactionContext.Build(
                    conversation,
                    stopBeforeMessage: null,
                    useModelContent: true);
            var prompt = (pendingPrompt ?? string.Empty).Trim();
            var activeMessageCount = history.Count + (prompt.Length == 0 ? 0 : 1);
            var activeWeight = history.Sum(message => CopilotTokenEstimator.EstimateTextWeight(message.Content));
            activeWeight = SaturatingAdd(activeWeight, CopilotTokenEstimator.EstimateTextWeight(prompt));
            var carriedPrefixWeight = conversation == null
                ? 0
                : Math.Min(
                    activeWeight,
                    CopilotConversationCompactionContext.EstimateCarriedPrefixWeight(conversation));
            var bodyAfterPrefixWeight = Math.Max(0, activeWeight - carriedPrefixWeight);
            var weightUsagePercent = ResolveUsagePercent(activeWeight, limits.MaximumCharacters);
            var messageUsagePercent = ResolveUsagePercent(activeMessageCount, limits.MaximumMessages);
            return new CopilotConversationContextUsage(
                Math.Max(weightUsagePercent, messageUsagePercent),
                weightUsagePercent,
                messageUsagePercent,
                activeMessageCount,
                activeWeight,
                carriedPrefixWeight,
                bodyAfterPrefixWeight,
                limits.MaximumMessages,
                limits.MaximumCharacters);
        }

        public static CopilotConversationAutoCompactionDecision Evaluate(
            CopilotConversationRecord? conversation,
            CopilotConversationHistoryLimits limits,
            string? pendingPrompt,
            CopilotConversationAutoCompactionOptions options)
        {
            if (!options.Enabled
                || conversation == null
                || limits.MaximumMessages <= 0
                || limits.MaximumCharacters <= 0)
            {
                return default;
            }

            var usage = Measure(conversation, limits, pendingPrompt);
            var tokenLimitScope = options.ModelTokenLimitScope ==
                CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix
                    ? CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix
                    : CopilotModelAutoCompactTokenLimitScope.Total;
            var evaluatedWeight = tokenLimitScope == CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix
                ? usage.BodyAfterPrefixWeight
                : usage.ActiveWeight;
            var evaluatedTokens = EstimateTokens(evaluatedWeight);
            var newMessageCount = CopilotConversationCompactionContext.CountMessagesAfterBoundary(conversation);
            if (newMessageCount < MinimumNewMessages)
            {
                return new CopilotConversationAutoCompactionDecision(
                    false,
                    CopilotConversationAutoCompactionTrigger.None,
                    usage.UsagePercent,
                    usage.ActiveMessageCount,
                    usage.ActiveWeight,
                    evaluatedTokens,
                    Math.Max(0, options.ModelTokenLimit ?? 0),
                    tokenLimitScope);
            }

            var normalizedThreshold = Math.Clamp(
                options.ThresholdPercent,
                CopilotAgentDefaultsConfig.MinimumAutoCompactThresholdPercent,
                CopilotAgentDefaultsConfig.MaximumAutoCompactThresholdPercent);

            var weightThreshold = ResolveThreshold(limits.MaximumCharacters, normalizedThreshold);
            var messageThreshold = ResolveThreshold(limits.MaximumMessages, normalizedThreshold);
            var configuredTokenLimit = Math.Max(0, options.ModelTokenLimit ?? 0);
            var trigger = configuredTokenLimit > 0 && evaluatedTokens >= configuredTokenLimit
                ? CopilotConversationAutoCompactionTrigger.ConfiguredTokenLimit
                : configuredTokenLimit == 0 && usage.ActiveWeight >= weightThreshold
                    ? CopilotConversationAutoCompactionTrigger.HistoryWeight
                    : usage.ActiveMessageCount >= messageThreshold
                        ? CopilotConversationAutoCompactionTrigger.MessageCount
                        : CopilotConversationAutoCompactionTrigger.None;
            return new CopilotConversationAutoCompactionDecision(
                trigger != CopilotConversationAutoCompactionTrigger.None,
                trigger,
                usage.UsagePercent,
                usage.ActiveMessageCount,
                usage.ActiveWeight,
                evaluatedTokens,
                configuredTokenLimit > 0
                    ? configuredTokenLimit
                    : EstimateTokens(weightThreshold),
                tokenLimitScope);
        }

        private static long ResolveThreshold(long maximum, int percent) =>
            Math.Max(1, (maximum * percent + 99) / 100);

        private static int ResolveUsagePercent(long used, long maximum)
        {
            if (used <= 0 || maximum <= 0)
                return 0;
            if (used >= maximum * 10)
                return 999;

            return (int)(used * 100 / maximum);
        }

        private static int EstimateTokens(long weight) => weight <= 0
            ? 0
            : CopilotTokenEstimator.WeightToTokenEstimate(weight);

        private static long SaturatingAdd(long left, long right) =>
            long.MaxValue - left < right ? long.MaxValue : left + right;
    }
}
