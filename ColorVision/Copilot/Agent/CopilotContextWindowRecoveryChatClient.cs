#pragma warning disable MAAI001
using Anthropic.Exceptions;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed record CopilotContextWindowRecoveryInfo(
        int OriginalMessageCount,
        int CompactedMessageCount,
        int EstimatedInputTokensBefore,
        int EstimatedInputTokensAfter,
        int TargetInputTokens,
        string FailureKind)
    {
        public string ToDiagnosticText()
        {
            return $"Provider context recovery ({FailureKind}) · compacted {OriginalMessageCount} message(s) to {CompactedMessageCount}"
                + $" · estimated input {EstimatedInputTokensBefore:N0} → {EstimatedInputTokensAfter:N0} tokens toward {TargetInputTokens:N0}"
                + " · resubmitted once before the first content or tool call; tool-call/result groups remained atomic and no tool execution was replayed.";
        }
    }

    internal sealed class CopilotAgentContextWindowRecoveryExhaustedException : Exception
    {
        public CopilotAgentContextWindowRecoveryExhaustedException(
            CopilotContextWindowRecoveryInfo recovery,
            Exception innerException)
            : base("The provider still rejected the Agent context after one bounded compaction retry for this model turn. Reduce conversation or attachment context, or configure the model's actual context-window size. No tool execution was replayed.", innerException)
        {
            ArgumentNullException.ThrowIfNull(recovery);
            OriginalMessageCount = Math.Max(0, recovery.OriginalMessageCount);
            CompactedMessageCount = Math.Max(0, recovery.CompactedMessageCount);
            EstimatedInputTokensBefore = Math.Max(0, recovery.EstimatedInputTokensBefore);
            EstimatedInputTokensAfter = Math.Max(0, recovery.EstimatedInputTokensAfter);
            TargetInputTokens = Math.Max(1, recovery.TargetInputTokens);
        }

        public int OriginalMessageCount { get; }

        public int CompactedMessageCount { get; }

        public int EstimatedInputTokensBefore { get; }

        public int EstimatedInputTokensAfter { get; }

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
                if (exception is AnthropicApiException apiException)
                    return (int)apiException.StatusCode;
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
            CopilotContextWindowRecoveryInfo? recovery = null;
            var discardedUsage = CopilotTokenUsage.Empty;
            try
            {
                while (true)
                {
                    try
                    {
                        var response = await base.GetResponseAsync(requestMessages, options, cancellationToken);
                        if (discardedUsage.HasAny)
                        {
                            var combinedUsage = CopilotTokenBudgetChatClient.ExtractResponseUsage(response).Add(discardedUsage);
                            response.Usage ??= new UsageDetails();
                            response.Usage.InputTokenCount = combinedUsage.InputTokens;
                            response.Usage.OutputTokenCount = combinedUsage.OutputTokens;
                            response.Usage.TotalTokenCount = combinedUsage.EffectiveTotalTokens;
                            response.Usage.CachedInputTokenCount = combinedUsage.CachedInputTokens;
                        }
                        return response;
                    }
                    catch (Exception exception) when (CopilotContextWindowFailureClassifier.TryClassify(exception, out var failureKind))
                    {
                        try
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (recovery != null)
                                throw CreateExhaustedException(recovery, exception);

                            var attempt = await PrepareRecoveryAsync(requestMessages, exception, failureKind, cancellationToken);
                            discardedUsage = discardedUsage.Add(CopilotProviderRetryChatClient.ExtractFailureUsage(exception));
                            requestMessages = attempt.Messages;
                            recovery = attempt.Recovery;
                        }
                        catch (Exception preparationFailure)
                        {
                            // A cancellation can replace the settled rejection. Wrapped context
                            // failures already retain that attempt's billing through their inner exception.
                            if (!ContainsException(preparationFailure, exception))
                                PreserveDiscardedUsage(preparationFailure, CopilotProviderRetryChatClient.ExtractFailureUsage(exception));
                            throw;
                        }
                    }
                }
            }
            catch (Exception failure)
            {
                PreserveDiscardedUsage(failure, discardedUsage);
                throw;
            }
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var requestMessages = Materialize(messages);
            CopilotContextWindowRecoveryInfo? recovery = null;
            while (true)
            {
                var attempt = await OpenStreamingAttemptAsync(requestMessages, options, cancellationToken);
                if (attempt.Failure != null)
                {
                    var exception = attempt.Failure.SourceException;
                    var bufferedUsage = attempt.BufferedUpdates.Aggregate(CopilotTokenUsage.Empty,
                        (usage, update) => usage.Add(CopilotTokenBudgetChatClient.ExtractUsage(update.Contents)));
                    var usageUpdate = CopilotProviderRetryChatClient.CreateUsageUpdate(
                        bufferedUsage.MergeProgress(CopilotProviderRetryChatClient.ExtractFailureUsage(exception)));
                    if (usageUpdate != null)
                        yield return usageUpdate;
                    if (!CopilotContextWindowFailureClassifier.TryClassify(exception, out var failureKind))
                        attempt.Failure.Throw();
                    cancellationToken.ThrowIfCancellationRequested();
                    if (recovery != null)
                        throw CreateExhaustedException(recovery, exception);

                    var prepared = await PrepareRecoveryAsync(requestMessages, exception, failureKind, cancellationToken);
                    requestMessages = prepared.Messages;
                    recovery = prepared.Recovery;
                    continue;
                }

                var enumerator = attempt.Enumerator;
                if (enumerator == null)
                {
                    foreach (var update in attempt.BufferedUpdates)
                        yield return update;
                    yield break;
                }

                ExceptionDispatchInfo? streamFailure = null;
                try
                {
                    foreach (var update in attempt.BufferedUpdates)
                        yield return update;
                    while (true)
                    {
                        bool hasNext;
                        try
                        {
                            hasNext = await enumerator.MoveNextAsync();
                        }
                        catch (Exception exception)
                        {
                            streamFailure = ExceptionDispatchInfo.Capture(exception);
                            throw;
                        }
                        if (!hasNext)
                            break;
                        yield return enumerator.Current;
                    }
                }
                finally
                {
                    try
                    {
                        await enumerator.DisposeAsync();
                    }
                    catch when (streamFailure != null)
                    {
                        // Cleanup must not replace the failure after content or tool progress.
                    }
                }
                yield break;
            }
        }

        private async Task<(IAsyncEnumerator<ChatResponseUpdate>? Enumerator,
            IReadOnlyList<ChatResponseUpdate> BufferedUpdates, ExceptionDispatchInfo? Failure)> OpenStreamingAttemptAsync(
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options,
            CancellationToken cancellationToken)
        {
            IAsyncEnumerator<ChatResponseUpdate>? enumerator = null;
            var bufferedUpdates = new List<ChatResponseUpdate>();
            try
            {
                enumerator = base.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
                while (await enumerator.MoveNextAsync())
                {
                    var update = enumerator.Current;
                    bufferedUpdates.Add(update);
                    if (CopilotProviderResponseContent.HasProgress(update))
                        return (enumerator, bufferedUpdates, null);
                }

                await enumerator.DisposeAsync();
                return (null, bufferedUpdates, null);
            }
            catch (Exception exception)
            {
                if (enumerator != null)
                {
                    try
                    {
                        await enumerator.DisposeAsync();
                    }
                    catch
                    {
                        // Preserve the context-window failure from the provider.
                    }
                }
                return (null, bufferedUpdates, ExceptionDispatchInfo.Capture(exception));
            }
        }

        private static bool ContainsException(Exception exception, Exception original)
        {
            for (var current = exception; current != null; current = current.InnerException)
                if (ReferenceEquals(current, original))
                    return true;
            return false;
        }

        private static void PreserveDiscardedUsage(Exception exception, CopilotTokenUsage usage)
        {
            if (!usage.HasAny)
                return;
            var previous = exception.Data[CopilotProviderRetryChatClient.DiscardedAttemptsUsageDataKey] is CopilotTokenUsage existing
                ? existing : CopilotTokenUsage.Empty;
            exception.Data[CopilotProviderRetryChatClient.DiscardedAttemptsUsageDataKey] = previous.Add(usage);
        }

        private async Task<(
            Microsoft.Extensions.AI.ChatMessage[] Messages,
            CopilotContextWindowRecoveryInfo Recovery)> PrepareRecoveryAsync(
            Microsoft.Extensions.AI.ChatMessage[] messages,
            Exception exception,
            string failureKind,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var estimatedMessageTokens = CopilotTokenBudgetChatClient.EstimateMessageTokens(messages);
            var recoveryTargetInputTokens = Math.Max(
                1,
                Math.Min(_maximumTargetInputTokens, Math.Max(1, estimatedMessageTokens / 2)));
            var reducer = new TruncationCompactionStrategy(
                CompactionTriggers.Always,
                MinimumPreservedGroups,
                CompactionTriggers.TokensBelow(recoveryTargetInputTokens)).AsChatReducer();
            var compacted = (await reducer.ReduceAsync(messages, cancellationToken)).ToArray();
            var estimatedCompactedTokens = CopilotTokenBudgetChatClient.EstimateMessageTokens(compacted);
            var recovery = new CopilotContextWindowRecoveryInfo(
                messages.Length,
                compacted.Length,
                estimatedMessageTokens,
                estimatedCompactedTokens,
                recoveryTargetInputTokens,
                failureKind);
            if (compacted.Length >= messages.Length)
                throw CreateExhaustedException(recovery, exception);

            CopilotProviderNotificationObserver.Notify(
                _onRecovery,
                recovery,
                "context recovery");
            return (compacted, recovery);
        }

        private static CopilotAgentContextWindowRecoveryExhaustedException CreateExhaustedException(
            CopilotContextWindowRecoveryInfo recovery,
            Exception innerException)
        {
            return new CopilotAgentContextWindowRecoveryExhaustedException(recovery, innerException);
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
