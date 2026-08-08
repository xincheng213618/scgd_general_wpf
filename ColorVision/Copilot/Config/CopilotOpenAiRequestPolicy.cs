using System;

namespace ColorVision.Copilot
{
    internal static class CopilotOpenAiRequestPolicy
    {
        public const string ResponsesAgentSessionTransportVersion = "openai-responses-stateless-v2";

        public static string GetMaximumOutputTokensPropertyName(
            CopilotProfileConfig profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            return UsesOfficialOpenAiApi(profile)
                ? "max_completion_tokens"
                : "max_tokens";
        }

        public static string GetInstructionRole(CopilotProfileConfig profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            return IsOfficialOpenAiReasoningModel(profile)
                ? "developer"
                : "system";
        }

        public static bool SupportsTemperature(CopilotProfileConfig profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            return !IsOfficialOpenAiReasoningModel(profile);
        }

        public static bool UsesResponsesApi(CopilotProfileConfig profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            return UsesOfficialOpenAiApi(profile);
        }

        public static string GetAgentSessionTransportVersion(
            CopilotProfileConfig profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            return UsesResponsesApi(profile)
                ? ResponsesAgentSessionTransportVersion
                : string.Empty;
        }

        private static bool UsesOfficialOpenAiApi(
            CopilotProfileConfig profile)
        {
            if (profile.VendorType != CopilotVendorType.OpenAI
                || profile.ProviderType != CopilotProviderType.OpenAICompatible
                || !Uri.TryCreate(
                    profile.BaseUrl,
                    UriKind.Absolute,
                    out var baseUri))
            {
                return false;
            }

            var host = baseUri.Host.TrimEnd('.');
            return string.Equals(
                    host,
                    "api.openai.com",
                    StringComparison.OrdinalIgnoreCase)
                || host.EndsWith(
                    ".api.openai.com",
                    StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsOfficialOpenAiReasoningModel(
            CopilotProfileConfig profile)
        {
            if (!UsesOfficialOpenAiApi(profile))
                return false;

            var model = profile.Model?.Trim() ?? string.Empty;
            if (model.Length == 0)
                return false;
            if (model.Contains("codex", StringComparison.OrdinalIgnoreCase))
                return true;
            if (model.Length > 1
                && model[0] is 'o' or 'O'
                && model[1] is >= '0' and <= '9')
            {
                return true;
            }
            if (!model.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase))
                return false;

            var index = 4;
            var majorVersion = 0;
            var hasDigit = false;
            while (index < model.Length
                && model[index] is >= '0' and <= '9')
            {
                hasDigit = true;
                majorVersion = Math.Min(
                    1_000,
                    majorVersion * 10 + model[index] - '0');
                index++;
            }
            return hasDigit && majorVersion >= 5;
        }
    }
}
