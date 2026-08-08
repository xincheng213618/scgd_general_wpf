namespace ColorVision.Copilot
{
    internal enum CopilotCodexReasoningEffort
    {
        Unspecified,
        None,
        Minimal,
        Low,
        Medium,
        High,
        XHigh,
    }

    internal static class CopilotCodexReasoningEffortSelection
    {
        public static bool TryParse(string? value, out CopilotCodexReasoningEffort effort)
        {
            effort = (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "minimal" => CopilotCodexReasoningEffort.Minimal,
                "low" => CopilotCodexReasoningEffort.Low,
                "medium" => CopilotCodexReasoningEffort.Medium,
                "high" => CopilotCodexReasoningEffort.High,
                "xhigh" => CopilotCodexReasoningEffort.XHigh,
                _ => CopilotCodexReasoningEffort.Unspecified,
            };
            return effort != CopilotCodexReasoningEffort.Unspecified;
        }

        public static bool TryParsePlanMode(string? value, out CopilotCodexReasoningEffort effort)
        {
            if (string.Equals(
                (value ?? string.Empty).Trim(),
                "none",
                System.StringComparison.OrdinalIgnoreCase))
            {
                effort = CopilotCodexReasoningEffort.None;
                return true;
            }
            return TryParse(value, out effort);
        }

        public static string GetConfigToken(CopilotCodexReasoningEffort effort) => effort switch
        {
            CopilotCodexReasoningEffort.None => "none",
            CopilotCodexReasoningEffort.Minimal => "minimal",
            CopilotCodexReasoningEffort.Low => "low",
            CopilotCodexReasoningEffort.Medium => "medium",
            CopilotCodexReasoningEffort.High => "high",
            CopilotCodexReasoningEffort.XHigh => "xhigh",
            _ => "未配置",
        };
    }

    internal enum CopilotCodexReasoningSummary
    {
        Unspecified,
        Auto,
        Concise,
        Detailed,
        None,
    }

    internal static class CopilotCodexReasoningSummarySelection
    {
        public static bool TryParse(string? value, out CopilotCodexReasoningSummary summary)
        {
            summary = (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "auto" => CopilotCodexReasoningSummary.Auto,
                "concise" => CopilotCodexReasoningSummary.Concise,
                "detailed" => CopilotCodexReasoningSummary.Detailed,
                "none" => CopilotCodexReasoningSummary.None,
                _ => CopilotCodexReasoningSummary.Unspecified,
            };
            return summary != CopilotCodexReasoningSummary.Unspecified;
        }

        public static string GetConfigToken(CopilotCodexReasoningSummary summary) => summary switch
        {
            CopilotCodexReasoningSummary.Auto => "auto",
            CopilotCodexReasoningSummary.Concise => "concise",
            CopilotCodexReasoningSummary.Detailed => "detailed",
            CopilotCodexReasoningSummary.None => "none",
            _ => "未配置",
        };
    }

    internal static class CopilotCodexReasoningSummarySupportSelection
    {
        public static string GetConfigToken(bool? supportsReasoningSummaries) =>
            supportsReasoningSummaries switch
            {
                true => "true",
                false => "false",
                null => "未配置",
            };

        public static CopilotCodexReasoningSummary ResolveSummary(
            bool? supportsReasoningSummaries,
            CopilotCodexReasoningSummary configuredSummary)
        {
            if (supportsReasoningSummaries == false)
                return CopilotCodexReasoningSummary.Unspecified;
            if (supportsReasoningSummaries == true
                && configuredSummary == CopilotCodexReasoningSummary.Unspecified)
            {
                return CopilotCodexReasoningSummary.Auto;
            }
            return configuredSummary;
        }
    }
}
