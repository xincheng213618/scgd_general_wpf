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
            return UsesResponsesApi(profile)
                ? "max_output_tokens"
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
            return UsesOfficialOpenAiApi(profile)
                || IsExplicitResponsesEndpoint(profile.BaseUrl, profile.ProviderType);
        }

        internal static bool IsExplicitResponsesEndpoint(string? baseUrl, CopilotProviderType providerType) =>
            providerType == CopilotProviderType.OpenAICompatible
            && Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var endpoint)
            && endpoint.AbsolutePath.TrimEnd('/').EndsWith("/responses", StringComparison.OrdinalIgnoreCase);

        internal static bool CanRequestPromptCacheDiagnostics(CopilotProfileConfig profile)
        {
            if (!UsesOfficialOpenAiApi(profile))
                return false;
            var model = profile.Model?.Trim() ?? string.Empty;
            if (!model.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase))
                return false;
            var version = model[4..].Split('-')[0].Split('.');
            if (version.Length is < 1 or > 2 || !int.TryParse(version[0], out var major))
                return false;
            var minor = 0;
            if (version.Length == 2 && !int.TryParse(version[1], out minor))
                return false;
            return major > 5 || major == 5 && minor >= 6;
        }

        public static string GetAgentSessionTransportVersion(
            CopilotProfileConfig profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            return UsesResponsesApi(profile)
                ? ResponsesAgentSessionTransportVersion
                : string.Empty;
        }

        internal static bool UsesOfficialOpenAiApi(
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

        internal static bool IsOfficialOpenAiReasoningModel(
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

        internal static bool IsGpt6Astra(CopilotProfileConfig profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            return UsesOfficialOpenAiApi(profile)
                && string.Equals(
                    profile.Model?.Trim(),
                    "gpt-6-astra",
                    StringComparison.OrdinalIgnoreCase);
        }
    }
}
