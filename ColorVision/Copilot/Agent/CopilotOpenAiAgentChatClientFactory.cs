#pragma warning disable OPENAI001
#pragma warning disable MAAI001
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Responses;
using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http;

namespace ColorVision.Copilot
{
    internal static class CopilotOpenAiAgentChatClientFactory
    {
        public static IChatClient Create(
            CopilotProfileConfig profile,
            HttpClient httpClient)
        {
            ArgumentNullException.ThrowIfNull(profile);
            ArgumentNullException.ThrowIfNull(httpClient);

            var options = new OpenAIClientOptions
            {
                Endpoint = NormalizeEndpoint(profile.BaseUrl),
                Transport = new HttpClientPipelineTransport(httpClient),
            };
            var credential = new ApiKeyCredential(profile.ApiKey);
            if (CopilotOpenAiRequestPolicy.UsesResponsesApi(profile))
            {
                var client = new OpenAIClient(credential, options)
                    .GetResponsesClient();
                return new CopilotStatelessResponsesHistoryChatClient(
                    client.AsIChatClientWithStoredOutputDisabled(
                        profile.Model,
                        includeReasoningEncryptedContent: true));
            }

            return new ChatClient(profile.Model, credential, options)
                .AsIChatClient();
        }

        internal static Uri NormalizeEndpoint(string baseUrl)
        {
            var value = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
            foreach (var suffix in new[] { "/chat/completions", "/responses" })
            {
                if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    value = value[..^suffix.Length];
                    break;
                }
            }

            var endpoint = new Uri(value, UriKind.Absolute);
            if (string.IsNullOrWhiteSpace(endpoint.AbsolutePath)
                || endpoint.AbsolutePath == "/")
            {
                value = value.TrimEnd('/') + "/v1";
            }

            return new Uri(value, UriKind.Absolute);
        }
    }
}
