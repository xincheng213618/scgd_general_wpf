using System;
using System.Linq;
using System.Text.RegularExpressions;

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
        private const int NarrowEvidenceRequestTokenBudget = 512 * 1024;
        private const int NarrowEvidenceBaseToolCalls = 12;
        private const int NarrowEvidenceToolCallsPerResult = 4;
        private const int NarrowEvidenceMaxAgentPasses = 8;
        private static readonly TimeSpan NarrowEvidenceTotalDuration = TimeSpan.FromMinutes(15);
        private static readonly Regex ChineseNarrowResultRegex = new(
            @"(?:列出|给出|找出|指出|报告|返回|展示)\s*(?:至少|最多|至多)?\s*(?<count>[1-3一二三])\s*(?:条|个|项|处|点)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex EnglishNarrowResultRegex = new(
            @"\b(?:list|give|find|identify|report|show)\s+(?:only\s+|at\s+least\s+|up\s+to\s+)?(?<count>[1-3]|one|two|three)\s+(?:[a-z-]+\s+){0,3}(?:issues?|findings?|problems?|risks?|examples?|items?)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex ChineseBroadCountRegex = new(
            @"(?<count>\d{1,4})\s*(?:个|条|项|处)\s*(?:相关)?\s*(?:代码)?\s*(?:文件|位置|目录|模块|组件|页面|节点|调用|结果)?",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex EnglishBroadCountRegex = new(
            @"\b(?<count>\d{1,4})\s+(?:files?|locations?|directories|modules?|components?|pages?|nodes?|calls?|results?)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly string[] ExhaustiveScopeMarkers =
        {
            "全面", "全量", "所有", "全部", "整个", "逐一", "每个", "完整审计",
            "comprehensive", "exhaustive", "all files", "all locations", "entire", "every file",
        };

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

        public int NarrowEvidenceResultLimit { get; init; }

        public static CopilotAgentRunBudget Resolve(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var defaults = request.RunBudgetDefaults;
            var requestOverride = request.RunBudgetOverride;
            var resolved = new CopilotAgentRunBudget
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
            if (requestOverride != null || !TryGetNarrowEvidenceResultLimit(request, out var resultLimit))
                return resolved;

            return new CopilotAgentRunBudget
            {
                ContextWindowTokens = resolved.ContextWindowTokens,
                RequestTokenBudget = Math.Min(resolved.RequestTokenBudget, NarrowEvidenceRequestTokenBudget),
                MaxToolCalls = Math.Min(
                    resolved.MaxToolCalls,
                    NarrowEvidenceBaseToolCalls + NarrowEvidenceToolCallsPerResult * resultLimit),
                MaxAgentPasses = Math.Min(resolved.MaxAgentPasses, NarrowEvidenceMaxAgentPasses),
                TotalDuration = resolved.TotalDuration < NarrowEvidenceTotalDuration
                    ? resolved.TotalDuration
                    : NarrowEvidenceTotalDuration,
                NarrowEvidenceResultLimit = resultLimit,
            };
        }

        internal static bool TryGetNarrowEvidenceResultLimit(CopilotAgentRequest? request, out int resultLimit)
        {
            resultLimit = 0;
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || !CopilotToolIntentPolicy.ExplicitlyDisallowsWriteAccess(request)
                || !CopilotToolIntentPolicy.NeedsLocalEvidence(request)
                || ContainsExhaustiveScope(request.UserText)
                || ContainsBroadCount(request.UserText))
            {
                return false;
            }

            var match = ChineseNarrowResultRegex.Match(request.UserText ?? string.Empty);
            if (!match.Success)
                match = EnglishNarrowResultRegex.Match(request.UserText ?? string.Empty);
            if (!match.Success || !TryParseSmallCount(match.Groups["count"].Value, out resultLimit))
            {
                resultLimit = 0;
                return false;
            }

            return true;
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
                RequestTokenBudgetExhausted = tokenSnapshot.BudgetExhausted || tokenSnapshot.RequestTokenBudgetExhausted,
                MaxToolCalls = MaxToolCalls,
                ToolCalls = Math.Clamp(toolCalls, 0, MaxToolCalls),
                ToolBudgetExhausted = toolBudgetExhausted,
                NarrowEvidenceResultLimit = NarrowEvidenceResultLimit,
                MaxAgentPasses = MaxAgentPasses,
                TotalDurationMs = Math.Max(1, (long)TotalDuration.TotalMilliseconds),
                ElapsedMs = Math.Max(0, (long)elapsed.TotalMilliseconds),
                TimeBudgetExhausted = timeBudgetExhausted,
            };
        }

        private static int Clamp(int value, int minimum, int maximum) => Math.Clamp(value, minimum, maximum);

        private static bool ContainsExhaustiveScope(string? text)
        {
            var source = text ?? string.Empty;
            return ExhaustiveScopeMarkers.Any(marker => source.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsBroadCount(string? text)
        {
            var source = text ?? string.Empty;
            return ChineseBroadCountRegex.Matches(source)
                .Concat(EnglishBroadCountRegex.Matches(source))
                .Any(match => int.TryParse(match.Groups["count"].Value, out var count) && count > 3);
        }

        private static bool TryParseSmallCount(string value, out int count)
        {
            count = value.ToLowerInvariant() switch
            {
                "1" or "一" or "one" => 1,
                "2" or "二" or "two" => 2,
                "3" or "三" or "three" => 3,
                _ => 0,
            };
            return count > 0;
        }

        private static TimeSpan Clamp(TimeSpan value, TimeSpan minimum, TimeSpan maximum)
        {
            if (value < minimum)
                return minimum;
            return value > maximum ? maximum : value;
        }
    }
}
