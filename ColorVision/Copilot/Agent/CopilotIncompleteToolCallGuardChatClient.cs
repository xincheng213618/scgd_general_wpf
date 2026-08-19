using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    /// <summary>
    /// Prevents locally invocable function calls from being dispatched when the
    /// provider says the response ended incompletely. The original call content
    /// remains in the response as informational evidence.
    /// </summary>
    internal sealed class CopilotIncompleteToolCallGuardChatClient : DelegatingChatClient
    {
        private readonly Action<int, ChatFinishReason>? _onCallsSuppressed;

        public CopilotIncompleteToolCallGuardChatClient(
            IChatClient innerClient,
            Action<int, ChatFinishReason>? onCallsSuppressed = null)
            : base(innerClient)
        {
            _onCallsSuppressed = onCallsSuppressed;
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);
            SuppressIncompleteCalls(
                response.Messages.SelectMany(message => message.Contents),
                response.FinishReason);
            return response;
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            List<ChatResponseUpdate>? bufferedUpdates = null;
            var providerHandledCallIds = new HashSet<string>(StringComparer.Ordinal);
            ChatFinishReason? finishReason = null;
            await foreach (var update in base.GetStreamingResponseAsync(messages, options, cancellationToken))
            {
                foreach (var result in update.Contents.OfType<FunctionResultContent>())
                {
                    if (!string.IsNullOrWhiteSpace(result.CallId))
                        providerHandledCallIds.Add(result.CallId.Trim());
                }
                if (update.FinishReason.HasValue)
                    finishReason = update.FinishReason;

                if (bufferedUpdates != null)
                {
                    bufferedUpdates.Add(update);
                    continue;
                }
                if (update.Contents.OfType<FunctionCallContent>().Any(call => !call.InformationalOnly))
                {
                    bufferedUpdates = [update];
                    continue;
                }

                yield return update;
            }

            if (bufferedUpdates == null)
                yield break;

            SuppressIncompleteCalls(
                bufferedUpdates.SelectMany(update => update.Contents),
                finishReason,
                providerHandledCallIds);
            foreach (var update in bufferedUpdates)
                yield return update;
        }

        private void SuppressIncompleteCalls(
            IEnumerable<AIContent> contents,
            ChatFinishReason? finishReason,
            IReadOnlySet<string>? knownProviderHandledCallIds = null)
        {
            if (!finishReason.HasValue || !IsUnsafeForToolDispatch(finishReason.Value))
                return;

            var materializedContents = contents.ToArray();
            var providerHandledCallIds = knownProviderHandledCallIds == null
                ? materializedContents
                    .OfType<FunctionResultContent>()
                    .Where(result => !string.IsNullOrWhiteSpace(result.CallId))
                    .Select(result => result.CallId.Trim())
                    .ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(knownProviderHandledCallIds, StringComparer.Ordinal);
            var suppressedCalls = new HashSet<string>(StringComparer.Ordinal);
            foreach (var call in materializedContents.OfType<FunctionCallContent>())
            {
                var callId = (call.CallId ?? string.Empty).Trim();
                if (call.InformationalOnly
                    || callId.Length > 0 && providerHandledCallIds.Contains(callId))
                {
                    continue;
                }

                call.InformationalOnly = true;
                suppressedCalls.Add(callId.Length > 0
                    ? "call:" + callId
                    : $"instance:{RuntimeHelpers.GetHashCode(call)}");
            }

            if (suppressedCalls.Count == 0 || _onCallsSuppressed == null)
                return;
            try
            {
                _onCallsSuppressed(suppressedCalls.Count, finishReason.Value);
            }
            catch
            {
                // Diagnostics are best effort; failure must not re-enable a
                // provider call that was already classified as incomplete.
            }
        }

        internal static bool IsUnsafeForToolDispatch(ChatFinishReason finishReason)
        {
            return CopilotProviderFinishReasonClassifier.Classify(finishReason.Value)
                is CopilotChatFinishKind.LengthLimit
                    or CopilotChatFinishKind.ContentFiltered
                    or CopilotChatFinishKind.Other;
        }
    }
}
