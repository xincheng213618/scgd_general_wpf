using System;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentRunBudgetOverride
    {
        public int? RequestTokenBudget { get; init; }

        public int? MaxToolCalls { get; init; }

        public int? MaxAgentPasses { get; init; }

        public TimeSpan? TotalDuration { get; init; }
    }

    public sealed class CopilotAgentRunBudget
    {
        public const int MinimumRequestTokenBudget = 4096;
        public const int MaximumRequestTokenBudget = 1_048_576;
        public const int MinimumToolCalls = 1;
        public const int MaximumToolCalls = 64;
        public const int MinimumAgentPasses = 1;
        public const int MaximumAgentPasses = 16;
        public static readonly TimeSpan MinimumTotalDuration = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan MaximumTotalDuration = TimeSpan.FromHours(1);

        public int RequestTokenBudget { get; init; }

        public int MaxToolCalls { get; init; }

        public int MaxAgentPasses { get; init; }

        public TimeSpan TotalDuration { get; init; }

        public static CopilotAgentRunBudget Resolve(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var profile = request.Profile;
            var requestOverride = request.RunBudgetOverride;
            return new CopilotAgentRunBudget
            {
                RequestTokenBudget = Clamp(
                    requestOverride?.RequestTokenBudget ?? profile?.AgentRequestTokenBudget ?? CopilotProfileConfig.DefaultAgentRequestTokenBudget,
                    MinimumRequestTokenBudget,
                    MaximumRequestTokenBudget),
                MaxToolCalls = Clamp(
                    requestOverride?.MaxToolCalls ?? profile?.MaxToolRounds ?? CopilotProfileConfig.DefaultMaxToolRounds,
                    MinimumToolCalls,
                    MaximumToolCalls),
                MaxAgentPasses = Clamp(
                    requestOverride?.MaxAgentPasses ?? profile?.MaxAgentPasses ?? CopilotProfileConfig.DefaultMaxAgentPasses,
                    MinimumAgentPasses,
                    MaximumAgentPasses),
                TotalDuration = Clamp(
                    requestOverride?.TotalDuration ?? TimeSpan.FromSeconds(profile?.AgentTimeoutSeconds ?? CopilotProfileConfig.DefaultAgentTimeoutSeconds),
                    MinimumTotalDuration,
                    MaximumTotalDuration),
            };
        }

        public CopilotAgentBudgetSnapshot CreateSnapshot(
            CopilotAgentBudgetSnapshot? tokenSnapshot,
            TimeSpan elapsed,
            int toolCalls,
            bool timeBudgetExhausted)
        {
            tokenSnapshot ??= new CopilotAgentBudgetSnapshot();
            return new CopilotAgentBudgetSnapshot
            {
                CompactionEnabled = tokenSnapshot.CompactionEnabled,
                ContextWindowTokens = tokenSnapshot.ContextWindowTokens,
                InputBudgetTokens = tokenSnapshot.InputBudgetTokens,
                RequestTokenBudget = RequestTokenBudget,
                ConsumedTokens = tokenSnapshot.ConsumedTokens,
                ProviderCalls = tokenSnapshot.ProviderCalls,
                UsedEstimatedUsage = tokenSnapshot.UsedEstimatedUsage,
                BudgetExhausted = tokenSnapshot.BudgetExhausted || timeBudgetExhausted,
                MaxToolCalls = MaxToolCalls,
                ToolCalls = Math.Clamp(toolCalls, 0, MaxToolCalls),
                MaxAgentPasses = MaxAgentPasses,
                TotalDurationMs = Math.Max(1, (long)TotalDuration.TotalMilliseconds),
                ElapsedMs = Math.Max(0, (long)elapsed.TotalMilliseconds),
                TimeBudgetExhausted = timeBudgetExhausted,
            };
        }

        private static int Clamp(int value, int minimum, int maximum) => Math.Clamp(value, minimum, maximum);

        private static TimeSpan Clamp(TimeSpan value, TimeSpan minimum, TimeSpan maximum)
        {
            if (value < minimum)
                return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
