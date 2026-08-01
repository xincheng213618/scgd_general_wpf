using System;
using System.Linq;

namespace ColorVision.Copilot
{
    internal enum CopilotConversationAutoCompactionTrigger
    {
        None,
        HistoryWeight,
        MessageCount,
    }

    internal readonly record struct CopilotConversationAutoCompactionDecision(
        bool ShouldCompact,
        CopilotConversationAutoCompactionTrigger Trigger,
        int UsagePercent,
        int ActiveMessageCount,
        long ActiveWeight);

    internal readonly record struct CopilotConversationContextUsage(
        int UsagePercent,
        int WeightUsagePercent,
        int MessageUsagePercent,
        int ActiveMessageCount,
        long ActiveWeight,
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
            var weightUsagePercent = ResolveUsagePercent(activeWeight, limits.MaximumCharacters);
            var messageUsagePercent = ResolveUsagePercent(activeMessageCount, limits.MaximumMessages);
            return new CopilotConversationContextUsage(
                Math.Max(weightUsagePercent, messageUsagePercent),
                weightUsagePercent,
                messageUsagePercent,
                activeMessageCount,
                activeWeight,
                limits.MaximumMessages,
                limits.MaximumCharacters);
        }

        public static CopilotConversationAutoCompactionDecision Evaluate(
            CopilotConversationRecord? conversation,
            CopilotConversationHistoryLimits limits,
            string? pendingPrompt,
            bool enabled,
            int thresholdPercent)
        {
            if (!enabled
                || conversation == null
                || limits.MaximumMessages <= 0
                || limits.MaximumCharacters <= 0)
            {
                return default;
            }

            var usage = Measure(conversation, limits, pendingPrompt);
            var newMessageCount = CopilotConversationCompactionContext.CountMessagesAfterBoundary(conversation);
            if (newMessageCount < MinimumNewMessages)
            {
                return new CopilotConversationAutoCompactionDecision(
                    false,
                    CopilotConversationAutoCompactionTrigger.None,
                    usage.UsagePercent,
                    usage.ActiveMessageCount,
                    usage.ActiveWeight);
            }

            var normalizedThreshold = Math.Clamp(
                thresholdPercent,
                CopilotAgentDefaultsConfig.MinimumAutoCompactThresholdPercent,
                CopilotAgentDefaultsConfig.MaximumAutoCompactThresholdPercent);

            var weightThreshold = ResolveThreshold(limits.MaximumCharacters, normalizedThreshold);
            var messageThreshold = ResolveThreshold(limits.MaximumMessages, normalizedThreshold);
            var trigger = usage.ActiveWeight >= weightThreshold
                ? CopilotConversationAutoCompactionTrigger.HistoryWeight
                : usage.ActiveMessageCount >= messageThreshold
                    ? CopilotConversationAutoCompactionTrigger.MessageCount
                    : CopilotConversationAutoCompactionTrigger.None;
            return new CopilotConversationAutoCompactionDecision(
                trigger != CopilotConversationAutoCompactionTrigger.None,
                trigger,
                usage.UsagePercent,
                usage.ActiveMessageCount,
                usage.ActiveWeight);
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

        private static long SaturatingAdd(long left, long right) =>
            long.MaxValue - left < right ? long.MaxValue : left + right;
    }
}
