using System;
using System.Linq;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotProviderEndpointValidation(
        Uri? Endpoint,
        bool IsInsecureHttp,
        string ErrorMessage)
    {
        public bool IsValid => Endpoint != null && string.IsNullOrWhiteSpace(ErrorMessage);
    }

    internal static class CopilotProviderEndpoint
    {
        public static CopilotProviderEndpointValidation Validate(CopilotProfileConfig profile)
        {
            ArgumentNullException.ThrowIfNull(profile);
            return Validate(profile.BaseUrl, profile.ProviderType, profile.AllowInsecureHttp);
        }

        public static CopilotProviderEndpointValidation Validate(
            string? baseUrl,
            CopilotProviderType providerType,
            bool allowInsecureHttp)
        {
            var normalizedBaseUrl = (baseUrl ?? string.Empty).Trim();
            if (normalizedBaseUrl.Length == 0)
                return Invalid("Base URL is required.");
            if (!Uri.TryCreate(normalizedBaseUrl, UriKind.Absolute, out var baseUri)
                || string.IsNullOrWhiteSpace(baseUri.Host))
            {
                return Invalid("Base URL must be an absolute HTTP or HTTPS URL.");
            }
            if (!string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
            {
                return Invalid("Base URL must use HTTP or HTTPS.");
            }
            if (!string.IsNullOrWhiteSpace(baseUri.UserInfo))
                return Invalid("Base URL must not contain a user name or password.");
            if (!string.IsNullOrWhiteSpace(baseUri.Fragment))
                return Invalid("Base URL must not contain a URL fragment.");
            if (!string.IsNullOrWhiteSpace(baseUri.Query))
                return Invalid("Base URL must not contain query parameters.");

            var isInsecureHttp = string.Equals(baseUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !baseUri.IsLoopback;
            if (isInsecureHttp && !allowInsecureHttp)
            {
                return Invalid(
                    "Remote HTTP would send the API key without transport encryption. Use HTTPS, a loopback URL, or explicitly allow insecure HTTP for this profile.");
            }

            try
            {
                var builder = new UriBuilder(baseUri)
                {
                    Path = BuildEndpointPath(baseUri.AbsolutePath, providerType),
                    Query = string.Empty,
                    Fragment = string.Empty,
                };
                return new CopilotProviderEndpointValidation(builder.Uri, isInsecureHttp, string.Empty);
            }
            catch (UriFormatException)
            {
                return Invalid("Base URL could not be converted into a provider endpoint.");
            }
        }

        public static Uri Build(CopilotProfileConfig profile)
        {
            var validation = Validate(profile);
            if (!validation.IsValid)
                throw new InvalidOperationException(validation.ErrorMessage);
            return validation.Endpoint!;
        }

        private static string BuildEndpointPath(string absolutePath, CopilotProviderType providerType)
        {
            var path = "/" + (absolutePath ?? string.Empty).Trim('/');
            if (path == "/")
            {
                return providerType == CopilotProviderType.AnthropicCompatible
                    ? "/v1/messages"
                    : "/v1/chat/completions";
            }

            if (providerType == CopilotProviderType.AnthropicCompatible)
            {
                if (path.EndsWith("/messages", StringComparison.OrdinalIgnoreCase))
                    return path;
                if (path.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                    return path + "/messages";
                return path + "/v1/messages";
            }

            if (path.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
                return path;
            return IsOpenAiApiRoot(path)
                ? path + "/chat/completions"
                : path + "/v1/chat/completions";
        }

        private static bool IsOpenAiApiRoot(string path)
        {
            var lastSegment = path[(path.LastIndexOf('/') + 1)..];
            if (string.Equals(lastSegment, "openai", StringComparison.OrdinalIgnoreCase))
                return true;
            return lastSegment.Length > 1
                && (lastSegment[0] is 'v' or 'V')
                && lastSegment[1..].Any(char.IsDigit);
        }

        private static CopilotProviderEndpointValidation Invalid(string message) =>
            new(null, false, message);
    }
}
