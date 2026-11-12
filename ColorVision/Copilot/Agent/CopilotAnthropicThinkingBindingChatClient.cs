using Anthropic.Exceptions;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    // A thinking signature records the system prompt, tools and messages that produced it, so context
    // recovery, compaction or a resumed session can make a replayed block invalid, and the API then
    // rejects the request. Only the rejected blocks are given up: reasoning produced after this recovery
    // still replays, and rejected blocks stay out of later requests instead of failing each one again.
    internal sealed class CopilotAnthropicThinkingBindingChatClient(IChatClient inner, Action<string>? onRecovery = null) : DelegatingChatClient(inner)
    {
        private readonly ConcurrentDictionary<string, byte> _rejectedSignatures = new(StringComparer.Ordinal);

        internal static IChatClient WrapIfSupported(IChatClient client, CopilotProfileConfig profile, Action<string>? onRecovery = null)
        {
            return profile.ProviderType == CopilotProviderType.AnthropicCompatible
                ? new CopilotAnthropicThinkingBindingChatClient(client, onRecovery)
                : client;
        }

        public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var history = messages as IReadOnlyCollection<ChatMessage> ?? messages.ToArray();
            try
            {
                return await base.GetResponseAsync(WithoutRejectedThinking(history), options, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (TryRejectReplayedThinking(history, exception))
            {
                return await base.GetResponseAsync(WithoutRejectedThinking(history), options, cancellationToken).ConfigureAwait(false);
            }
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var history = messages as IReadOnlyCollection<ChatMessage> ?? messages.ToArray();
            await using var stream = await OpenStreamAsync(history, options, cancellationToken).ConfigureAwait(false);
            while (stream.HasCurrent)
            {
                yield return stream.Enumerator.Current;
                stream.HasCurrent = await stream.Enumerator.MoveNextAsync().ConfigureAwait(false);
            }
        }

        // The API decides this rejection before any output, so nothing has reached the caller yet.
        private async Task<OpenedStream> OpenStreamAsync(IReadOnlyCollection<ChatMessage> history, ChatOptions? options, CancellationToken cancellationToken)
        {
            try
            {
                return await OpenAttemptAsync(history, options, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (TryRejectReplayedThinking(history, exception))
            {
                return await OpenAttemptAsync(history, options, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task<OpenedStream> OpenAttemptAsync(IReadOnlyCollection<ChatMessage> history, ChatOptions? options, CancellationToken cancellationToken)
        {
            var enumerator = base.GetStreamingResponseAsync(WithoutRejectedThinking(history), options, cancellationToken).GetAsyncEnumerator(cancellationToken);
            try
            {
                return new OpenedStream(enumerator, await enumerator.MoveNextAsync().ConfigureAwait(false));
            }
            catch
            {
                await enumerator.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }

        private bool TryRejectReplayedThinking(IEnumerable<ChatMessage> history, Exception exception)
        {
            if (!IsThinkingSignatureRejection(exception))
                return false;

            var rejected = history
                .Where(message => message.Role == ChatRole.Assistant)
                .SelectMany(message => message.Contents)
                .OfType<TextReasoningContent>()
                .Select(content => content.ProtectedData)
                .OfType<string>()
                .Where(signature => _rejectedSignatures.TryAdd(signature, 0))
                .Count();
            // No newly removable block means a resend would be the same refused body.
            if (rejected == 0)
                return false;

            onRecovery?.Invoke($"The provider rejected replayed thinking bound to an earlier conversation state; removed {rejected} block(s) and resubmitted once. Text, tool calls and tool results were kept.");
            return true;
        }

        private static bool IsThinkingSignatureRejection(Exception exception)
        {
            return exception is AnthropicApiException { StatusCode: HttpStatusCode.BadRequest }
                && exception.Message.Contains("signature", StringComparison.OrdinalIgnoreCase)
                && exception.Message.Contains("thinking", StringComparison.OrdinalIgnoreCase);
        }

        private IEnumerable<ChatMessage> WithoutRejectedThinking(IEnumerable<ChatMessage> history)
        {
            if (_rejectedSignatures.IsEmpty)
                return history;

            return history
                .Select(message =>
                {
                    if (message.Role != ChatRole.Assistant || !message.Contents.Any(IsRejected))
                        return message;

                    var prepared = message.Clone();
                    prepared.Contents = message.Contents.Where(content => !IsRejected(content)).ToList();
                    return prepared;
                })
                .Where(message => message.Contents.Count > 0)
                .ToArray();
        }

        private bool IsRejected(AIContent content)
            => content is TextReasoningContent { ProtectedData: { } signature } && _rejectedSignatures.ContainsKey(signature);

        private sealed class OpenedStream(IAsyncEnumerator<ChatResponseUpdate> enumerator, bool hasCurrent) : IAsyncDisposable
        {
            public IAsyncEnumerator<ChatResponseUpdate> Enumerator { get; } = enumerator;

            public bool HasCurrent { get; set; } = hasCurrent;

            public ValueTask DisposeAsync() => Enumerator.DisposeAsync();
        }
    }
}
