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
        int? StatusCode);

    internal sealed class CopilotProviderRetryChatClient : DelegatingChatClient
    {
        internal const int DefaultMaximumAttempts = 3;

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
            if (maximumAttempts < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumAttempts));

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
                catch (Exception ex) when (TryCreateRetry(ex, cancellationToken, attempt, out var retry))
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
                IAsyncEnumerator<ChatResponseUpdate>? enumerator;
                try
                {
                    enumerator = await OpenStreamingAttemptAsync(materializedMessages, options, cancellationToken);
                }
                catch (Exception ex) when (TryCreateRetry(ex, cancellationToken, attempt, out var retry))
                {
                    _onRetry?.Invoke(retry);
                    await _delayAsync(retry.Delay, cancellationToken);
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
                    // Preserve the provider failure that determines retry eligibility.
                }
                throw;
            }
        }

        private bool TryCreateRetry(
            Exception exception,
            CancellationToken cancellationToken,
            int failedAttempt,
            out CopilotProviderRetryInfo retry)
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

        private CopilotProviderRetryInfo CreateRetry(Exception exception, int failedAttempt)
        {
            _ = TryClassifyTransientFailure(exception, CancellationToken.None, out var failureKind, out var statusCode);
            return new CopilotProviderRetryInfo(
                failedAttempt,
                failedAttempt + 1,
                _maximumAttempts,
                _delayFactory(failedAttempt),
                failureKind,
                statusCode);
        }

        private static bool TryClassifyTransientFailure(
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

        private static bool IsTransientStatusCode(int statusCode)
            => statusCode is (int)HttpStatusCode.RequestTimeout or 429 || statusCode >= 500 && statusCode <= 599;

        private static TimeSpan CreateDefaultDelay(int failedAttempt)
            => TimeSpan.FromMilliseconds(Math.Min(2_000, 250 * Math.Pow(2, Math.Max(0, failedAttempt - 1))));
    }
}
