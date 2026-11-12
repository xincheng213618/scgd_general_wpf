using Anthropic.Models.Messages;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    // The Messages API caches nothing unless asked, so without this every Agent round re-bills the
    // tools, instructions and history at the full input price. Top-level cache_control moves the
    // breakpoint with the conversation. A later turn rebuilds its history without tool rounds and
    // context, so it would not reach that breakpoint: the marker before the request-specific part of
    // the instructions is what lets a new turn read the tools and the rest of the instructions, even
    // after the active document or Git HEAD changed. Anthropic-compatible gateways may reject these
    // fields, so they are limited to the official endpoint; the SDK reports cached input inside
    // InputTokenCount, which keeps budgets and statistics unchanged.
    internal sealed class CopilotAnthropicPromptCacheChatClient(IChatClient inner, CopilotProfileConfig profile) : DelegatingChatClient(inner)
    {
        internal static IChatClient WrapIfSupported(IChatClient client, CopilotProfileConfig profile)
        {
            return IsOfficialEndpoint(profile) ? new CopilotAnthropicPromptCacheChatClient(client, profile) : client;
        }

        internal static bool IsOfficialEndpoint(CopilotProfileConfig profile)
        {
            return profile.ProviderType == CopilotProviderType.AnthropicCompatible
                && Uri.TryCreate(profile.BaseUrl?.Trim(), UriKind.Absolute, out var endpoint)
                && string.Equals(endpoint.Host.TrimEnd('.'), "api.anthropic.com", StringComparison.OrdinalIgnoreCase);
        }

        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var history = messages as IReadOnlyCollection<ChatMessage> ?? messages.ToArray();
            return base.GetResponseAsync(history, Prepare(history, options), cancellationToken);
        }

        public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var history = messages as IReadOnlyCollection<ChatMessage> ?? messages.ToArray();
            return base.GetStreamingResponseAsync(history, Prepare(history, options), cancellationToken);
        }

        private ChatOptions Prepare(IReadOnlyCollection<ChatMessage> history, ChatOptions? options)
        {
            if (options?.RawRepresentationFactory != null)
                return options;

            var prepared = options?.Clone() ?? new ChatOptions();
            // The SDK renders raw system blocks before system messages, so moving the instructions
            // past one of those would reorder the prompt.
            var instructions = !string.IsNullOrEmpty(prepared.Instructions) && !history.Any(message => message.Role == ChatRole.System)
                ? prepared.Instructions
                : null;
            if (instructions != null)
                prepared.Instructions = null;
            // The SDK takes model and max_tokens from this object, so it must carry the real values.
            prepared.RawRepresentationFactory = _ =>
            {
                var parameters = new MessageCreateParams
                {
                    CacheControl = new CacheControlEphemeral(),
                    MaxTokens = prepared.MaxOutputTokens ?? profile.MaxTokens,
                    Messages = [],
                    Model = prepared.ModelId ?? profile.Model,
                };
                return instructions == null ? parameters : parameters with { System = BuildSystem(instructions) };
            };
            return prepared;
        }

        private static List<TextBlockParam> BuildSystem(string instructions)
        {
            var volatileStart = instructions.IndexOf(CopilotMicrosoftAgentFrameworkRuntime.RequestSpecificInstructionsStart, StringComparison.Ordinal);
            if (volatileStart <= 0)
                return [new() { Text = instructions, CacheControl = new CacheControlEphemeral() }];

            return
            [
                new() { Text = instructions[..volatileStart], CacheControl = new CacheControlEphemeral() },
                new() { Text = instructions[volatileStart..] },
            ];
        }
    }
}
