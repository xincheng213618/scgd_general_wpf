using Microsoft.Extensions.AI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotOpenAiRequestIdChatClient : DelegatingChatClient
    {
        private static readonly string[] RequestIdHeaders = ["x-request-id", "request-id", "x-amzn-requestid"];
        private readonly string _apiKey;

        public CopilotOpenAiRequestIdChatClient(IChatClient innerClient, string apiKey)
            : base(innerClient)
        {
            _apiKey = apiKey;
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await base.GetResponseAsync(messages, options, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                PreserveRequestId(exception);
                throw;
            }
        }

        public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => new ResponseEnumerable(base.GetStreamingResponseAsync(messages, options, cancellationToken), this);

        private void PreserveRequestId(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is not ClientResultException sdkException)
                    continue;

                try
                {
                    var headers = sdkException.GetRawResponse()?.Headers;
                    if (headers == null)
                        continue;

                    foreach (var header in RequestIdHeaders)
                    {
                        if (!headers.TryGetValue(header, out var value))
                            continue;

                        // Remove the complete credential before normalization can truncate a long header.
                        var requestId = string.IsNullOrEmpty(_apiKey)
                            ? value
                            : value.Replace(_apiKey, "redacted", StringComparison.Ordinal);
                        requestId = CopilotProviderRequestId.Redact(requestId, _apiKey);
                        if (requestId.Length == 0)
                            continue;

                        CopilotProviderRequestId.Preserve(current, requestId);
                        break;
                    }
                }
                catch
                {
                    // Optional header metadata must never replace the original provider failure.
                }
            }
        }

        private sealed class ResponseEnumerable(
            IAsyncEnumerable<ChatResponseUpdate> updates,
            CopilotOpenAiRequestIdChatClient owner) : IAsyncEnumerable<ChatResponseUpdate>
        {
            public IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(CancellationToken cancellationToken = default)
                => new ResponseEnumerator(updates.GetAsyncEnumerator(cancellationToken), owner);
        }

        private sealed class ResponseEnumerator(
            IAsyncEnumerator<ChatResponseUpdate> inner,
            CopilotOpenAiRequestIdChatClient owner) : IAsyncEnumerator<ChatResponseUpdate>
        {
            public ChatResponseUpdate Current => inner.Current;

            public async ValueTask<bool> MoveNextAsync()
            {
                try
                {
                    return await inner.MoveNextAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    owner.PreserveRequestId(exception);
                    throw;
                }
            }

            // Preserve the outer cancellation guard's ownership and primary-error cleanup policy.
            public ValueTask DisposeAsync() => inner.DisposeAsync();
        }
    }
}
