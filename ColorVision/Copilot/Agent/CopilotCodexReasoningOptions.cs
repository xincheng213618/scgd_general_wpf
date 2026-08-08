namespace ColorVision.Copilot
{
    internal enum CopilotCodexReasoningEffort
    {
        Unspecified,
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

        public static string GetConfigToken(CopilotCodexReasoningEffort effort) => effort switch
        {
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
}
