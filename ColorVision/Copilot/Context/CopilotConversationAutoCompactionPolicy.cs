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

    internal static class CopilotConversationAutoCompactionPolicy
    {
        private const int MinimumNewMessages = 2;

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

            var newMessageCount = CopilotConversationCompactionContext.CountMessagesAfterBoundary(conversation);
            if (newMessageCount < MinimumNewMessages)
                return default;

            var normalizedThreshold = Math.Clamp(
                thresholdPercent,
                CopilotAgentDefaultsConfig.MinimumAutoCompactThresholdPercent,
                CopilotAgentDefaultsConfig.MaximumAutoCompactThresholdPercent);
            var history = CopilotConversationCompactionContext.Build(
                conversation,
                stopBeforeMessage: null,
                useModelContent: true);
            var prompt = (pendingPrompt ?? string.Empty).Trim();
            var activeMessageCount = history.Count + (prompt.Length == 0 ? 0 : 1);
            var activeWeight = history.Sum(message => CopilotTokenEstimator.EstimateTextWeight(message.Content));
            activeWeight = SaturatingAdd(activeWeight, CopilotTokenEstimator.EstimateTextWeight(prompt));

            var weightThreshold = ResolveThreshold(limits.MaximumCharacters, normalizedThreshold);
            var messageThreshold = ResolveThreshold(limits.MaximumMessages, normalizedThreshold);
            var trigger = activeWeight >= weightThreshold
                ? CopilotConversationAutoCompactionTrigger.HistoryWeight
                : activeMessageCount >= messageThreshold
                    ? CopilotConversationAutoCompactionTrigger.MessageCount
                    : CopilotConversationAutoCompactionTrigger.None;
            var usagePercent = Math.Max(
                ResolveUsagePercent(activeWeight, limits.MaximumCharacters),
                ResolveUsagePercent(activeMessageCount, limits.MaximumMessages));
            return new CopilotConversationAutoCompactionDecision(
                trigger != CopilotConversationAutoCompactionTrigger.None,
                trigger,
                usagePercent,
                activeMessageCount,
                activeWeight);
        }

        private static long ResolveThreshold(long maximum, int percent) =>
            Math.Max(1, (maximum * percent + 99) / 100);

        private static int ResolveUsagePercent(long used, long maximum)
        {
            if (used <= 0 || maximum <= 0)
                return 0;

            return (int)Math.Min(999, (used * 100 + maximum - 1) / maximum);
        }

        private static long SaturatingAdd(long left, long right) =>
            long.MaxValue - left < right ? long.MaxValue : left + right;
    }
}
