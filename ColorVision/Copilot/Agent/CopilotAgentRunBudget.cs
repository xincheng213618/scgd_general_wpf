using System;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentRunBudgetDefaults
    {
        public int ContextWindowTokens { get; init; } = CopilotAgentDefaultsConfig.DefaultContextWindowTokens;

        public int RequestTokenBudget { get; init; } = CopilotAgentDefaultsConfig.DefaultRequestTokenBudget;

        public int MaxToolCalls { get; init; } = CopilotAgentDefaultsConfig.DefaultMaxToolCalls;

        public int MaxAgentPasses { get; init; } = CopilotAgentDefaultsConfig.DefaultMaxAgentPasses;

        public TimeSpan TotalDuration { get; init; } = TimeSpan.FromSeconds(CopilotAgentDefaultsConfig.DefaultTimeoutSeconds);
    }

    public sealed class CopilotAgentRunBudgetOverride
    {
        public int? ContextWindowTokens { get; init; }

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
        public const int MaximumToolCalls = 512;
        public const int MinimumAgentPasses = 1;
        public const int MaximumAgentPasses = 128;
        public static readonly TimeSpan MinimumTotalDuration = TimeSpan.FromSeconds(1);
        public static readonly TimeSpan MaximumTotalDuration = TimeSpan.FromHours(24);

        public int RequestTokenBudget { get; init; }

        public int ContextWindowTokens { get; init; }

        public int MaxToolCalls { get; init; }

        public int MaxAgentPasses { get; init; }

        public TimeSpan TotalDuration { get; init; }

        public static CopilotAgentRunBudget Resolve(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var defaults = request.RunBudgetDefaults;
            var requestOverride = request.RunBudgetOverride;
            return new CopilotAgentRunBudget
            {
                ContextWindowTokens = Clamp(
                    requestOverride?.ContextWindowTokens ?? defaults?.ContextWindowTokens ?? CopilotAgentDefaultsConfig.DefaultContextWindowTokens,
                    CopilotAgentTokenBudget.MinimumContextWindowTokens,
                    CopilotAgentTokenBudget.MaximumContextWindowTokens),
                RequestTokenBudget = Clamp(
                    requestOverride?.RequestTokenBudget ?? defaults?.RequestTokenBudget ?? CopilotAgentDefaultsConfig.DefaultRequestTokenBudget,
                    MinimumRequestTokenBudget,
                    MaximumRequestTokenBudget),
                MaxToolCalls = Clamp(
                    requestOverride?.MaxToolCalls ?? defaults?.MaxToolCalls ?? CopilotAgentDefaultsConfig.DefaultMaxToolCalls,
                    MinimumToolCalls,
                    MaximumToolCalls),
                MaxAgentPasses = Clamp(
                    requestOverride?.MaxAgentPasses ?? defaults?.MaxAgentPasses ?? CopilotAgentDefaultsConfig.DefaultMaxAgentPasses,
                    MinimumAgentPasses,
                    MaximumAgentPasses),
                TotalDuration = Clamp(
                    requestOverride?.TotalDuration ?? defaults?.TotalDuration ?? TimeSpan.FromSeconds(CopilotAgentDefaultsConfig.DefaultTimeoutSeconds),
                    MinimumTotalDuration,
                    MaximumTotalDuration),
            };
        }

        public CopilotAgentBudgetSnapshot CreateSnapshot(
            CopilotAgentBudgetSnapshot? tokenSnapshot,
            TimeSpan elapsed,
            int toolCalls,
            bool timeBudgetExhausted,
            bool toolBudgetExhausted = false)
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
                BudgetExhausted = tokenSnapshot.BudgetExhausted || timeBudgetExhausted || toolBudgetExhausted,
                MaxToolCalls = MaxToolCalls,
                ToolCalls = Math.Clamp(toolCalls, 0, MaxToolCalls),
                ToolBudgetExhausted = toolBudgetExhausted,
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
