using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.ClientModel;

namespace ColorVision.Copilot
{
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

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    return await base.GetResponseAsync(materializedMessages, options, cancellationToken);
                }
                catch (Exception ex) when (TryCreateRetry(ex, attempt, out var retry, cancellationToken))
                {
                    _onRetry?.Invoke(retry);
                    await _delayAsync(retry.Delay, cancellationToken);
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
                CopilotStreamingAttempt? streamingAttempt;
                try
                {
                    streamingAttempt = await OpenStreamingAttemptAsync(
                        materializedMessages,
                        options,
                        cancellationToken);
                }
                catch (Exception ex) when (TryCreateRetry(ex, attempt, out var retry, cancellationToken))
                {
                    _onRetry?.Invoke(retry);
                    await _delayAsync(retry.Delay, cancellationToken);
                    continue;
                }

                if (streamingAttempt == null)
                    yield break;

                await using (streamingAttempt)
                {
                    foreach (var update in streamingAttempt.BufferedUpdates)
                        yield return update;

                    var enumerator = streamingAttempt.Enumerator;
                    if (enumerator != null)
                    {
                        while (await enumerator.MoveNextAsync())
                            yield return enumerator.Current;
                    }
                }
                yield break;
            }
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
                        CopilotProviderResponseContent.HasAny(update.Contents);
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
            catch
            {
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

            public ValueTask DisposeAsync() =>
                Enumerator?.DisposeAsync() ?? ValueTask.CompletedTask;
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
                || !TryClassifyTransientFailure(exception, cancellationToken, out _, out _))
            {
                return false;
            }

            retry = CreateRetry(exception, failedAttempt);
            return true;
        }

        internal static bool IsProviderInterruption(Exception exception, CancellationToken cancellationToken)
        {
            if (exception == null || cancellationToken.IsCancellationRequested)
                return false;

            return EnumerateExceptionChain(exception).Any(candidate => candidate is ClientResultException
                or HttpRequestException
                or TimeoutException
                or IOException
                or SocketException
                or OperationCanceledException);
        }

        private CopilotProviderRetryInfo CreateRetry(Exception exception, int failedAttempt)
        {
            _ = TryClassifyTransientFailure(exception, CancellationToken.None, out var failureKind, out var statusCode);
            return new CopilotProviderRetryInfo(
                failedAttempt,
                failedAttempt + 1,
                _maximumAttempts,
                ResolveRetryDelay(exception, _delayFactory(failedAttempt)),
                failureKind,
                statusCode,
                CopilotProviderRequestId.Find(exception));
        }

        internal static void PreserveRetryAfter(HttpResponseMessage response, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentNullException.ThrowIfNull(exception);
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
                : TimeSpan.FromMilliseconds(Math.Min(
                    MaximumServerRetryDelay.TotalMilliseconds,
                    requestedDelay.TotalMilliseconds));
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
                if (candidate is ClientResultException { Status: > 0 } clientResultException)
                {
                    statusCode = clientResultException.Status;
                    failureKind = "HTTP " + statusCode.Value;
                    return IsTransientStatusCode(statusCode.Value);
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
