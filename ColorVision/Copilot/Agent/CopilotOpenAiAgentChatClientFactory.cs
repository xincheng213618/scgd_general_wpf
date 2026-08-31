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

            var apiKey = profile.ApiKey;
            var options = new OpenAIClientOptions
            {
                Endpoint = NormalizeEndpoint(profile.BaseUrl),
                Transport = new HttpClientPipelineTransport(httpClient),
                // ColorVision owns bounded retries and accounts for every provider attempt.
                RetryPolicy = new ClientRetryPolicy(0),
            };
            var credential = new ApiKeyCredential(apiKey);
            IChatClient chatClient;
            if (CopilotOpenAiRequestPolicy.UsesResponsesApi(profile))
            {
                var client = new OpenAIClient(credential, options)
                    .GetResponsesClient();
                chatClient = new CopilotStatelessResponsesHistoryChatClient(
                    client.AsIChatClientWithStoredOutputDisabled(
                        profile.Model,
                        includeReasoningEncryptedContent: true));
            }
            else
            {
                chatClient = new ChatClient(profile.Model, credential, options).AsIChatClient();
            }

            return new CopilotOpenAiRequestIdChatClient(chatClient, apiKey);
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
