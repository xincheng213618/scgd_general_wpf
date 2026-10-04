using Anthropic.Exceptions;
using Anthropic.Models;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.ClientModel;

namespace ColorVision.Copilot
{
    internal static class CopilotProviderNotificationObserver
    {
        public static void Notify<T>(Action<T>? observer, T value, string notificationKind)
        {
            try
            {
                observer?.Invoke(value);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(
                    "Copilot provider {0} observer failed: {1}",
                    notificationKind,
                    ex.GetType().Name);
            }
        }
    }

    internal sealed record CopilotProviderRetryInfo(
        int FailedAttempt,
        int NextAttempt,
        int MaximumAttempts,
        TimeSpan Delay,
        string FailureKind,
        int? StatusCode,
        string RequestId = "")
    {
        public string ToDiagnosticText()
        {
            var delay = Delay.TotalSeconds >= 1
                ? $"{Delay.TotalSeconds:0.#}s"
                : $"{Math.Max(0, Delay.TotalMilliseconds):0}ms";
            var normalizedRequestId =
                CopilotProviderRequestId.Normalize(RequestId);
            var request = normalizedRequestId.Length == 0
                ? string.Empty
                : $" · request {normalizedRequestId}";
            return $"Provider request retry {NextAttempt}/{MaximumAttempts} · {FailureKind}{request} before the first content or tool call · waiting {delay}; no content or tool call was replayed.";
        }
    }

    internal sealed class CopilotProviderRetryChatClient : DelegatingChatClient
    {
        internal const int DefaultMaximumAttempts = 3;
        private const int MaximumBufferedPreambleUpdates = 64;
        private const string RetryAfterDataKey = "ColorVision.Copilot.ProviderRetryAfter";
        private const string BufferedAttemptUsageDataKey = "ColorVision.Copilot.ProviderBufferedAttemptUsage";
        internal const string DiscardedAttemptsUsageDataKey = "ColorVision.Copilot.ProviderDiscardedAttemptsUsage";
        private static readonly TimeSpan MaximumServerRetryDelay = TimeSpan.FromMinutes(2);

        private readonly int _maximumAttempts;
        private readonly Func<int, TimeSpan> _delayFactory;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
        private readonly Action<CopilotProviderRetryInfo>? _onRetry;

        public CopilotProviderRetryChatClient(
            IChatClient innerClient,
            Action<CopilotProviderRetryInfo>? onRetry = null,
            int maximumAttempts = DefaultMaximumAttempts,
            Func<int, TimeSpan>? delayFactory = null,
            Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
            : base(innerClient)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumAttempts, 1);

            _maximumAttempts = maximumAttempts;
            _delayFactory = delayFactory ?? CreateDefaultDelay;
            _delayAsync = delayAsync ?? Task.Delay;
            _onRetry = onRetry;
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var materializedMessages = messages is Microsoft.Extensions.AI.ChatMessage[] array
                ? array
                : messages?.ToArray() ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>();
            var discardedUsage = CopilotTokenUsage.Empty;

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    var response = await base.GetResponseAsync(materializedMessages, options, cancellationToken);
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
                catch (Exception ex)
                {
                    if (!TryCreateRetry(ex, attempt, out var retry, cancellationToken))
                    {
                        if (discardedUsage.HasAny)
                            ex.Data[DiscardedAttemptsUsageDataKey] = discardedUsage;
                        throw;
                    }
                    discardedUsage = discardedUsage.Add(ExtractFailureUsage(ex));
                    CopilotProviderNotificationObserver.Notify(_onRetry, retry, "retry");
                    try
                    {
                        await _delayAsync(retry.Delay, cancellationToken);
                    }
                    catch (Exception delayFailure)
                    {
                        if (discardedUsage.HasAny)
                            delayFailure.Data[DiscardedAttemptsUsageDataKey] = discardedUsage;
                        throw;
                    }
                }
            }
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var materializedMessages = messages is Microsoft.Extensions.AI.ChatMessage[] array
                ? array
                : messages?.ToArray() ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>();

            for (var attempt = 1; ; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CopilotStreamingAttempt? streamingAttempt = null;
                ExceptionDispatchInfo? openFailure = null;
                try
                {
                    streamingAttempt = await OpenStreamingAttemptAsync(
                        materializedMessages,
                        options,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    openFailure = ExceptionDispatchInfo.Capture(ex);
                }
                if (openFailure != null)
                {
                    var failedUsageUpdate = CreateUsageUpdate(ExtractFailureUsage(openFailure.SourceException));
                    if (failedUsageUpdate != null)
                        yield return failedUsageUpdate;
                    if (TryCreateRetry(openFailure.SourceException, attempt, out var retry, cancellationToken))
                    {
                        CopilotProviderNotificationObserver.Notify(_onRetry, retry, "retry");
                        await _delayAsync(retry.Delay, cancellationToken);
                        continue;
                    }
                    openFailure.Throw();
                }

                if (streamingAttempt?.Enumerator == null)
                    cancellationToken.ThrowIfCancellationRequested();
                if (streamingAttempt?.Enumerator == null
                    && TryCreateEmptyResponseRetry(
                        streamingAttempt?.BufferedUpdates ?? Array.Empty<ChatResponseUpdate>(),
                        attempt,
                        cancellationToken,
                        out var emptyRetry))
                {
                    // Keep only the completed attempt's usage. Its role, response identity and
                    // finish marker must not close the logical response before the next attempt.
                    var usageUpdate = CreateUsageUpdate(CopilotTokenBudgetChatClient.ExtractUsage(
                        streamingAttempt?.BufferedUpdates.SelectMany(update => update.Contents)));
                    if (usageUpdate != null)
                        yield return usageUpdate;
                    CopilotProviderNotificationObserver.Notify(_onRetry, emptyRetry, "retry");
                    await _delayAsync(emptyRetry.Delay, cancellationToken);
                    continue;
                }

                if (streamingAttempt == null)
                    yield break;

                await using (streamingAttempt)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var update in streamingAttempt.BufferedUpdates)
                        yield return update;

                    var enumerator = streamingAttempt.Enumerator;
                    if (enumerator != null)
                    {
                        while (true)
                        {
                            var hasNext = false;
                            try
                            {
                                hasNext = await enumerator.MoveNextAsync();
                            }
                            catch (Exception ex)
                            {
                                streamingAttempt.Failure = ExceptionDispatchInfo.Capture(ex);
                            }
                            if (streamingAttempt.Failure != null)
                            {
                                // Native failed Responses carry terminal usage only on the exception;
                                // that adapter has not published a prior usage snapshot for this attempt.
                                var failedUsageUpdate = CreateUsageUpdate(
                                    CopilotTokenBudgetChatClient.ExtractPayloadFailureUsage(
                                        streamingAttempt.Failure.SourceException));
                                if (failedUsageUpdate != null)
                                    yield return failedUsageUpdate;
                                streamingAttempt.Failure.Throw();
                            }
                            if (!hasNext)
                                break;
                            yield return enumerator.Current;
                        }
                    }
                }
                yield break;
            }
        }

        private bool TryCreateEmptyResponseRetry(
            IReadOnlyList<ChatResponseUpdate> updates,
            int failedAttempt,
            CancellationToken cancellationToken,
            out CopilotProviderRetryInfo retry)
        {
            retry = null!;
            if (failedAttempt >= _maximumAttempts
                || cancellationToken.IsCancellationRequested
                || updates.Any(update => update.FinishReason.HasValue
                    && CopilotProviderFinishReasonClassifier.Classify(update.FinishReason.Value.Value)
                        != CopilotChatFinishKind.Complete))
            {
                return false;
            }

            var delay = _delayFactory(failedAttempt);
            if (delay < TimeSpan.Zero)
                delay = TimeSpan.Zero;
            if (delay > MaximumServerRetryDelay)
                return false;
            retry = new CopilotProviderRetryInfo(
                failedAttempt, failedAttempt + 1, _maximumAttempts,
                delay, "empty response", StatusCode: null);
            return true;
        }

        internal static CopilotTokenUsage ExtractFailureUsage(Exception exception)
        {
            var usage = CopilotTokenBudgetChatClient.ExtractPayloadFailureUsage(exception);
            var discardedUsage = CopilotTokenUsage.Empty;
            foreach (var candidate in EnumerateExceptionChain(exception))
            {
                if (candidate.Data[BufferedAttemptUsageDataKey] is CopilotTokenUsage bufferedUsage)
                    usage = usage.MergeProgress(bufferedUsage);
                if (candidate.Data[DiscardedAttemptsUsageDataKey] is CopilotTokenUsage previousAttempts)
                    discardedUsage = discardedUsage.Add(previousAttempts);
            }
            return usage.Add(discardedUsage);
        }

        internal static ChatResponseUpdate? CreateUsageUpdate(CopilotTokenUsage usage)
        {
            return !usage.HasAny ? null : new ChatResponseUpdate
            {
                Contents = [new UsageContent(new UsageDetails
                {
                    InputTokenCount = usage.InputTokens,
                    OutputTokenCount = usage.OutputTokens,
                    TotalTokenCount = usage.EffectiveTotalTokens,
                    CachedInputTokenCount = usage.CachedInputTokens,
                })],
            };
        }

        private async Task<CopilotStreamingAttempt?> OpenStreamingAttemptAsync(
            IReadOnlyList<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options,
            CancellationToken cancellationToken)
        {
            IAsyncEnumerator<ChatResponseUpdate>? enumerator =
                base.GetStreamingResponseAsync(messages, options, cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);
            var bufferedUpdates = new List<ChatResponseUpdate>();
            try
            {
                while (await enumerator.MoveNextAsync())
                {
                    var update = enumerator.Current;
                    var hasResponseContent =
                        CopilotProviderResponseContent.HasProgress(update);
                    if (!hasResponseContent
                        && bufferedUpdates.Count >= MaximumBufferedPreambleUpdates)
                    {
                        throw new InvalidOperationException(
                            $"The provider returned more than {MaximumBufferedPreambleUpdates} metadata-only stream updates before any content or tool call.");
                    }

                    bufferedUpdates.Add(update);
                    if (hasResponseContent)
                    {
                        var openedAttempt = new CopilotStreamingAttempt(
                            enumerator,
                            bufferedUpdates.ToArray());
                        enumerator = null;
                        return openedAttempt;
                    }
                }

                await enumerator.DisposeAsync();
                enumerator = null;
                return bufferedUpdates.Count == 0
                    ? null
                    : new CopilotStreamingAttempt(
                        enumerator: null,
                        bufferedUpdates.ToArray());
            }
            catch (Exception exception)
            {
                var bufferedUsage = CopilotTokenBudgetChatClient.ExtractUsage(
                    bufferedUpdates.SelectMany(update => update.Contents));
                if (bufferedUsage.HasAny)
                    exception.Data[BufferedAttemptUsageDataKey] = bufferedUsage;
                if (enumerator != null)
                {
                    try
                    {
                        await enumerator.DisposeAsync();
                    }
                    catch
                    {
                        // Preserve the provider failure that determines retry eligibility.
                    }
                }
                throw;
            }
        }

        private sealed class CopilotStreamingAttempt(
            IAsyncEnumerator<ChatResponseUpdate>? enumerator,
            IReadOnlyList<ChatResponseUpdate> bufferedUpdates) : IAsyncDisposable
        {
            public IAsyncEnumerator<ChatResponseUpdate>? Enumerator { get; } = enumerator;

            public IReadOnlyList<ChatResponseUpdate> BufferedUpdates { get; } =
                bufferedUpdates;

            public ExceptionDispatchInfo? Failure { get; set; }

            public async ValueTask DisposeAsync()
            {
                if (Enumerator == null)
                    return;
                try
                {
                    await Enumerator.DisposeAsync();
                }
                catch when (Failure != null)
                {
                    // Enumerator cleanup must not replace the provider failure.
                }
            }
        }

        private bool TryCreateRetry(
            Exception exception,
            int failedAttempt,
            out CopilotProviderRetryInfo retry,
            CancellationToken cancellationToken)
        {
            retry = null!;
            if (failedAttempt >= _maximumAttempts
                || cancellationToken.IsCancellationRequested
                || !TryClassifyTransientFailure(exception, cancellationToken, out var failureKind, out var statusCode)
                || !TryResolveRetryDelay(exception, _delayFactory(failedAttempt), out var delay))
            {
                return false;
            }

            retry = new CopilotProviderRetryInfo(failedAttempt, failedAttempt + 1, _maximumAttempts,
                delay, failureKind, statusCode, CopilotProviderRequestId.Find(exception));
            return true;
        }

        internal static bool IsProviderInterruption(Exception exception, CancellationToken cancellationToken)
        {
            if (exception == null || cancellationToken.IsCancellationRequested)
                return false;

            return EnumerateExceptionChain(exception).Any(candidate => candidate is AnthropicApiException
                or AnthropicSseException
                or CopilotProviderPayloadException
                or ClientResultException
                or HttpRequestException
                or TimeoutException
                or IOException
                or SocketException
                or OperationCanceledException);
        }

        internal static void PreserveRetryAfter(HttpResponseMessage response, Exception exception, bool includeMilliseconds = false)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(exception);
            if (includeMilliseconds
                && response.Headers.TryGetValues("Retry-After-Ms", out var millisecondValues)
                && double.TryParse(millisecondValues.FirstOrDefault(), NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds)
                && double.IsFinite(milliseconds)
                && milliseconds >= 0)
            {
                exception.Data[RetryAfterDataKey] = milliseconds >= TimeSpan.MaxValue.TotalMilliseconds
                    ? TimeSpan.MaxValue
                    : TimeSpan.FromMilliseconds(milliseconds);
                return;
            }
            if (!response.Headers.TryGetValues("Retry-After", out var values))
                return;

            var value = values.FirstOrDefault();
            if (TryParseRetryAfter(value, DateTimeOffset.UtcNow, out var delay))
                exception.Data[RetryAfterDataKey] = delay;
        }

        internal static TimeSpan ResolveRetryDelay(Exception exception, TimeSpan fallbackDelay)
        {
            var normalizedFallback = fallbackDelay < TimeSpan.Zero ? TimeSpan.Zero : fallbackDelay;
            return TryGetRetryAfter(exception, out var retryAfter) && retryAfter > normalizedFallback
                ? retryAfter
                : normalizedFallback;
        }

        internal static bool TryResolveRetryDelay(Exception exception, TimeSpan fallbackDelay, out TimeSpan delay)
        {
            delay = ResolveRetryDelay(exception, fallbackDelay);
            // Keep the automatic wait bounded without retrying before the server permits it.
            return delay <= MaximumServerRetryDelay;
        }

        private static bool TryGetRetryAfter(Exception exception, out TimeSpan delay)
        {
            foreach (var candidate in EnumerateExceptionChain(exception))
            {
                if (candidate.Data[RetryAfterDataKey] is TimeSpan preservedDelay)
                {
                    delay = preservedDelay;
                    return true;
                }

                if (candidate is not ClientResultException clientResultException)
                    continue;

                try
                {
                    var response = clientResultException.GetRawResponse();
                    if (response?.Headers.TryGetValue("Retry-After", out var value) == true
                        && TryParseRetryAfter(value, DateTimeOffset.UtcNow, out delay))
                    {
                        return true;
                    }
                }
                catch
                {
                    // Header metadata is optional; fall back to the local retry schedule.
                }
            }

            delay = TimeSpan.Zero;
            return false;
        }

        private static bool TryParseRetryAfter(string? value, DateTimeOffset now, out TimeSpan delay)
        {
            if (!CopilotProviderRateLimitTimeParser.TryResolveRetryAfterDeadline(
                    value,
                    now,
                    out var retryAt))
            {
                delay = TimeSpan.Zero;
                return false;
            }

            var requestedDelay = retryAt - now;
            delay = requestedDelay <= TimeSpan.Zero
                ? TimeSpan.Zero
                : requestedDelay;
            return true;
        }

        internal static bool TryClassifyTransientFailure(
            Exception exception,
            CancellationToken cancellationToken,
            out string failureKind,
            out int? statusCode)
        {
            failureKind = string.Empty;
            statusCode = null;
            if (cancellationToken.IsCancellationRequested)
                return false;

            var candidates = EnumerateExceptionChain(exception).ToArray();
            foreach (var candidate in candidates)
            {
                if (candidate is CopilotProviderPayloadException payloadException)
                {
                    failureKind = payloadException.ErrorCode;
                    return payloadException.IsTransient;
                }
                if (candidate is AnthropicApiException apiException)
                {
                    statusCode = (int)apiException.StatusCode;
                    return CopilotProviderErrorPolicy.IsTransientHttpFailure(candidate, statusCode.Value, out failureKind);
                }

                if (candidate is AnthropicSseException sseException)
                {
                    failureKind = sseException.ErrorType switch
                    {
                        ErrorType.OverloadedError => "overloaded_error",
                        ErrorType.RateLimitError => "rate_limit_error",
                        ErrorType.ApiError => "api_error",
                        ErrorType.TimeoutError => "timeout_error",
                        _ => string.Empty,
                    };
                    return failureKind.Length > 0;
                }

                if (candidate is ClientResultException { Status: > 0 } clientResultException)
                {
                    statusCode = clientResultException.Status;
                    return CopilotProviderErrorPolicy.IsTransientHttpFailure(candidate, statusCode.Value, out failureKind);
                }

                if (candidate is HttpRequestException { StatusCode: not null } httpRequestException)
                {
                    statusCode = (int)httpRequestException.StatusCode.Value;
                    failureKind = "HTTP " + statusCode.Value;
                    return IsTransientStatusCode(statusCode.Value);
                }
            }

            foreach (var candidate in candidates)
            {
                if (candidate is CopilotProviderInactivityException inactivityException)
                {
                    failureKind = inactivityException.Phase == CopilotProviderInactivityPhase.FirstResponse
                        ? "first-content timeout"
                        : "stream-inactivity timeout";
                    return true;
                }

                if (candidate is ClientResultException or HttpRequestException)
                {
                    failureKind = "connection failure";
                    return true;
                }

                if (candidate is TimeoutException
                    || candidate is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                {
                    failureKind = "timeout";
                    return true;
                }

                if (candidate is IOException or SocketException)
                {
                    failureKind = "I/O interruption";
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<Exception> EnumerateExceptionChain(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
                yield return current;
        }

        internal static bool IsTransientStatusCode(int statusCode)
            => statusCode is (int)HttpStatusCode.RequestTimeout or 429 || statusCode >= 500 && statusCode <= 599;

        internal static TimeSpan CreateDefaultDelay(int failedAttempt)
            => TimeSpan.FromMilliseconds(Math.Min(2_000, 250 * Math.Pow(2, Math.Max(0, failedAttempt - 1))));
    }
}
