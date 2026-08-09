using System;

namespace ColorVision.Copilot
{
    internal enum CopilotCodexModelVerbosity
    {
        Unspecified,
        Low,
        Medium,
        High,
    }

    internal static class CopilotCodexModelVerbositySelection
    {
        public static bool TryParse(string? value, out CopilotCodexModelVerbosity verbosity)
        {
            verbosity = (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "low" => CopilotCodexModelVerbosity.Low,
                "medium" => CopilotCodexModelVerbosity.Medium,
                "high" => CopilotCodexModelVerbosity.High,
                _ => CopilotCodexModelVerbosity.Unspecified,
            };
            return verbosity != CopilotCodexModelVerbosity.Unspecified;
        }

        public static string GetConfigToken(CopilotCodexModelVerbosity verbosity) => verbosity switch
        {
            CopilotCodexModelVerbosity.Low => "low",
            CopilotCodexModelVerbosity.Medium => "medium",
            CopilotCodexModelVerbosity.High => "high",
            _ => "未配置",
        };
    }

    internal static class CopilotCodexServiceTierSelection
    {
        internal const int MaximumCharacters = 64;

        public static bool TryNormalize(string? value, out string serviceTier)
        {
            serviceTier = (value ?? string.Empty).Trim().ToLowerInvariant();
            if (serviceTier.Length is 0 or > MaximumCharacters
                || !IsAlphaNumeric(serviceTier[0]))
            {
                serviceTier = string.Empty;
                return false;
            }

            for (var index = 1; index < serviceTier.Length; index++)
            {
                if (!IsAlphaNumeric(serviceTier[index])
                    && serviceTier[index] is not ('-' or '_' or '.'))
                {
                    serviceTier = string.Empty;
                    return false;
                }
            }
            return true;
        }

        public static string GetRequestToken(string? configuredServiceTier)
        {
            return TryNormalize(configuredServiceTier, out var serviceTier)
                ? string.Equals(serviceTier, "fast", StringComparison.Ordinal)
                    ? "priority"
                    : serviceTier
                : string.Empty;
        }

        private static bool IsAlphaNumeric(char value) =>
            value is >= 'a' and <= 'z'
            or >= '0' and <= '9';
    }
}
