#pragma warning disable MAAI001
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed record CopilotContextWindowRecoveryInfo(
        int OriginalMessageCount,
        int CompactedMessageCount,
        int TargetInputTokens,
        string FailureKind)
    {
        public string ToDiagnosticText()
        {
            return $"Provider context recovery · compacted {OriginalMessageCount} message(s) to {CompactedMessageCount} toward {TargetInputTokens:N0} input tokens and resubmitted once before the first response update; tool-call/result groups remained atomic and no tool execution was replayed.";
        }
    }

    internal sealed class CopilotAgentContextWindowRecoveryExhaustedException : Exception
    {
        public CopilotAgentContextWindowRecoveryExhaustedException(
            int originalMessageCount,
            int compactedMessageCount,
            int targetInputTokens,
            Exception innerException)
            : base("The provider still rejected the Agent context after one bounded compaction retry. Reduce conversation or attachment context, or configure the model's actual context-window size. No tool execution was replayed.", innerException)
        {
            OriginalMessageCount = Math.Max(0, originalMessageCount);
            CompactedMessageCount = Math.Max(0, compactedMessageCount);
            TargetInputTokens = Math.Max(1, targetInputTokens);
        }

        public int OriginalMessageCount { get; }

        public int CompactedMessageCount { get; }

        public int TargetInputTokens { get; }
    }

    internal static class CopilotContextWindowFailureClassifier
    {
        private static readonly string[] ContextWindowMarkers =
        [
            "context_length_exceeded",
            "context length exceeded",
            "maximum context length",
            "maximum context window",
            "context window exceeded",
            "context window is too small",
            "prompt is too long",
            "maximum prompt length",
            "input is too long",
            "too many input tokens",
            "exceeds the maximum number of tokens",
            "request too large for model",
        ];

        public static bool TryClassify(Exception? exception, out string failureKind)
        {
            failureKind = string.Empty;
            if (exception == null)
                return false;

            var chain = EnumerateExceptionChain(exception).ToArray();
            if (chain.Any(candidate => candidate is CopilotAgentContextWindowExceededException))
            {
                failureKind = "local context estimate";
                return true;
            }

            var statusCode = TryGetStatusCode(chain);
            if (statusCode == (int)HttpStatusCode.RequestEntityTooLarge)
            {
                failureKind = "HTTP 413";
                return true;
            }

            if (statusCode is not (null or (int)HttpStatusCode.BadRequest or (int)HttpStatusCode.UnprocessableEntity))
                return false;

            var hasContextMarker = chain
                .Select(candidate => candidate.Message ?? string.Empty)
                .Any(message => ContextWindowMarkers.Any(marker => message.Contains(marker, StringComparison.OrdinalIgnoreCase)));
            if (!hasContextMarker)
                return false;

            failureKind = statusCode.HasValue ? $"HTTP {statusCode.Value} context limit" : "provider context limit";
            return true;
        }

        private static int? TryGetStatusCode(IEnumerable<Exception> exceptions)
        {
            foreach (var exception in exceptions)
            {
                if (exception is ClientResultException { Status: > 0 } clientResultException)
                    return clientResultException.Status;
                if (exception is HttpRequestException { StatusCode: not null } httpRequestException)
                    return (int)httpRequestException.StatusCode.Value;
            }

            return null;
        }

        private static IEnumerable<Exception> EnumerateExceptionChain(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
                yield return current;
        }
    }

    internal sealed class CopilotContextWindowRecoveryChatClient : DelegatingChatClient
    {
        private const int MinimumPreservedGroups = 2;

        private readonly int _maximumTargetInputTokens;
        private readonly Action<CopilotContextWindowRecoveryInfo>? _onRecovery;
        private int _recoveryClaimed;
        private int _originalMessageCount;
        private int _compactedMessageCount;
        private int _recoveryTargetInputTokens;

        public CopilotContextWindowRecoveryChatClient(
            IChatClient innerClient,
            int inputBudgetTokens,
            Action<CopilotContextWindowRecoveryInfo>? onRecovery = null)
            : base(innerClient)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(inputBudgetTokens, 1);
            _maximumTargetInputTokens = Math.Max(1, inputBudgetTokens / 2);
            _onRecovery = onRecovery;
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var requestMessages = Materialize(messages);
            while (true)
            {
                try
                {
                    return await base.GetResponseAsync(requestMessages, options, cancellationToken);
                }
                catch (Exception exception) when (CopilotContextWindowFailureClassifier.TryClassify(exception, out var failureKind))
                {
                    requestMessages = await PrepareRecoveryAsync(requestMessages, exception, failureKind, cancellationToken);
                }
            }
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var requestMessages = Materialize(messages);
            while (true)
            {
                IAsyncEnumerator<ChatResponseUpdate>? enumerator;
                try
                {
                    enumerator = await OpenStreamingAttemptAsync(requestMessages, options, cancellationToken);
                }
                catch (Exception exception) when (CopilotContextWindowFailureClassifier.TryClassify(exception, out var failureKind))
                {
                    requestMessages = await PrepareRecoveryAsync(requestMessages, exception, failureKind, cancellationToken);
                    continue;
                }

                if (enumerator == null)
                    yield break;

                await using (enumerator)
                {
                    yield return enumerator.Current;
                    while (await enumerator.MoveNextAsync())
                        yield return enumerator.Current;
                }
                yield break;
            }
        }

        private async Task<IAsyncEnumerator<ChatResponseUpdate>?> OpenStreamingAttemptAsync(
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options,
            CancellationToken cancellationToken)
        {
            var enumerator = base.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
            try
            {
                if (await enumerator.MoveNextAsync())
                    return enumerator;

                await enumerator.DisposeAsync();
                return null;
            }
            catch
            {
                try
                {
                    await enumerator.DisposeAsync();
                }
                catch
                {
                    // Preserve the context-window failure from the provider.
                }
                throw;
            }
        }

        private async Task<Microsoft.Extensions.AI.ChatMessage[]> PrepareRecoveryAsync(
            Microsoft.Extensions.AI.ChatMessage[] messages,
            Exception exception,
            string failureKind,
            CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref _recoveryClaimed, 1, 0) != 0)
                throw CreateExhaustedException(exception);

            cancellationToken.ThrowIfCancellationRequested();
            _originalMessageCount = messages.Length;
            var estimatedMessageTokens = CopilotTokenBudgetChatClient.EstimateMessageTokens(messages);
            _recoveryTargetInputTokens = Math.Max(
                1,
                Math.Min(_maximumTargetInputTokens, Math.Max(1, estimatedMessageTokens / 2)));
            var reducer = new TruncationCompactionStrategy(
                CompactionTriggers.Always,
                MinimumPreservedGroups,
                CompactionTriggers.TokensBelow(_recoveryTargetInputTokens)).AsChatReducer();
            var compacted = (await reducer.ReduceAsync(messages, cancellationToken)).ToArray();
            _compactedMessageCount = compacted.Length;
            if (compacted.Length >= messages.Length)
                throw CreateExhaustedException(exception);

            _onRecovery?.Invoke(new CopilotContextWindowRecoveryInfo(
                messages.Length,
                compacted.Length,
                _recoveryTargetInputTokens,
                failureKind));
            return compacted;
        }

        private CopilotAgentContextWindowRecoveryExhaustedException CreateExhaustedException(Exception innerException)
        {
            return new CopilotAgentContextWindowRecoveryExhaustedException(
                _originalMessageCount,
                _compactedMessageCount,
                Math.Max(1, _recoveryTargetInputTokens == 0 ? _maximumTargetInputTokens : _recoveryTargetInputTokens),
                innerException);
        }

        private static Microsoft.Extensions.AI.ChatMessage[] Materialize(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage>? messages)
        {
            return messages is Microsoft.Extensions.AI.ChatMessage[] array
                ? array
                : messages?.ToArray() ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>();
        }
    }
}
