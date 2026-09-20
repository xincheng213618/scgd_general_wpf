using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    // DeepSeek emits an empty thinking block with a signature even without visible
    // reasoning. The Anthropic adapter replays that as redacted_thinking, which
    // DeepSeek rejects with HTTP 422. This compatibility rule is endpoint-specific:
    // real Anthropic redacted thinking must remain intact.
    internal sealed class CopilotDeepSeekAnthropicChatClient(IChatClient inner) : DelegatingChatClient(inner)
    {
        internal static IChatClient WrapIfRequired(IChatClient client, CopilotProfileConfig profile)
        {
            return profile.ProviderType == CopilotProviderType.AnthropicCompatible
                && Uri.TryCreate(profile.BaseUrl, UriKind.Absolute, out var endpoint)
                && string.Equals(endpoint.Host, "api.deepseek.com", StringComparison.OrdinalIgnoreCase)
                && string.Equals(endpoint.AbsolutePath.TrimEnd('/'), "/anthropic", StringComparison.Ordinal)
                ? new CopilotDeepSeekAnthropicChatClient(client)
                : client;
        }

        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => base.GetResponseAsync(PrepareMessages(messages), options, cancellationToken);

        public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => base.GetStreamingResponseAsync(PrepareMessages(messages), options, cancellationToken);

        private static IEnumerable<ChatMessage> PrepareMessages(IEnumerable<ChatMessage> messages)
        {
            foreach (var message in messages)
            {
                if (message.Role != ChatRole.Assistant || !message.Contents.Any(IsEmptySignedReasoning))
                {
                    yield return message;
                    continue;
                }
                var contents = message.Contents.Where(content => !IsEmptySignedReasoning(content)).ToList();
                if (contents.Count == 0)
                    continue;
                var prepared = message.Clone();
                prepared.Contents = contents;
                yield return prepared;
            }
        }

        private static bool IsEmptySignedReasoning(AIContent content) => content is TextReasoningContent reasoning
            && string.IsNullOrEmpty(reasoning.Text) && !string.IsNullOrEmpty(reasoning.ProtectedData);
    }
}
