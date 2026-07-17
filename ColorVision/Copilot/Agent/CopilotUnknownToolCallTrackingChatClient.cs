using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotUnknownToolCallTrackingChatClient : DelegatingChatClient
    {
        private readonly Action<FunctionCallContent> _onUnknownToolCall;

        public CopilotUnknownToolCallTrackingChatClient(
            IChatClient innerClient,
            Action<FunctionCallContent> onUnknownToolCall)
            : base(innerClient)
        {
            _onUnknownToolCall = onUnknownToolCall ?? throw new ArgumentNullException(nameof(onUnknownToolCall));
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);
            TrackUnknownToolCalls(response.Messages.SelectMany(message => message.Contents), options, new HashSet<string>(StringComparer.Ordinal));
            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var observedCalls = new HashSet<string>(StringComparer.Ordinal);
            var responseContents = new List<AIContent>();
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                responseContents.AddRange(update.Contents);
                yield return update;
            }
            TrackUnknownToolCalls(responseContents, options, observedCalls);
        }

        private void TrackUnknownToolCalls(
            IEnumerable<AIContent>? contents,
            ChatOptions? options,
            HashSet<string> observedCalls)
        {
            var materializedContents = contents?.ToArray() ?? Array.Empty<AIContent>();
            var availableNames = (options?.Tools ?? Array.Empty<AITool>())
                .Where(tool => tool != null && !string.IsNullOrWhiteSpace(tool.Name))
                .Select(tool => tool.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var providerHandledCallIds = materializedContents
                .OfType<FunctionResultContent>()
                .Where(result => !string.IsNullOrWhiteSpace(result.CallId))
                .Select(result => result.CallId.Trim())
                .ToHashSet(StringComparer.Ordinal);
            foreach (var functionCall in materializedContents.OfType<FunctionCallContent>())
            {
                if (functionCall.InformationalOnly
                    || availableNames.Contains(functionCall.Name)
                    || !string.IsNullOrWhiteSpace(functionCall.CallId) && providerHandledCallIds.Contains(functionCall.CallId.Trim()))
                {
                    continue;
                }

                var callKey = string.IsNullOrWhiteSpace(functionCall.CallId)
                    ? $"instance:{RuntimeHelpers.GetHashCode(functionCall)}"
                    : "call:" + functionCall.CallId.Trim();
                if (observedCalls.Add(callKey))
                    _onUnknownToolCall(functionCall);
            }
        }
    }
}
