using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal enum CopilotProviderInactivityPhase
    {
        FirstResponse,
        StreamingUpdate,
    }

    internal sealed class CopilotProviderInactivityException : TimeoutException
    {
        public CopilotProviderInactivityException(
            CopilotProviderInactivityPhase phase,
            TimeSpan timeout)
            : base(BuildMessage(phase, timeout))
        {
            Phase = phase;
            TimeoutDuration = timeout;
        }

        public CopilotProviderInactivityPhase Phase { get; }

        public TimeSpan TimeoutDuration { get; }

        public static bool TryFind(
            Exception? exception,
            out CopilotProviderInactivityException inactivity)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is not CopilotProviderInactivityException candidate)
                    continue;

                inactivity = candidate;
                return true;
            }

            inactivity = null!;
            return false;
        }

        private static string BuildMessage(
            CopilotProviderInactivityPhase phase,
            TimeSpan timeout)
        {
            var duration = timeout.TotalMinutes >= 1
                ? $"{timeout.TotalMinutes:0.#} minute(s)"
                : $"{Math.Max(0, timeout.TotalSeconds):0.#} second(s)";
            return phase == CopilotProviderInactivityPhase.FirstResponse
                ? $"The provider returned no content within {duration}; the attempt was stopped before any content or tool call could be replayed."
                : $"The provider stream returned no new content for {duration}; the stalled stream was stopped without replaying partial content or tool calls.";
        }
    }

    internal static class CopilotProviderInactivityPolicy
    {
        public static readonly TimeSpan DefaultFirstResponseTimeout =
            TimeSpan.FromSeconds(CopilotProfileConfig.DefaultFirstContentTimeoutSeconds);

        public static readonly TimeSpan DefaultStreamingUpdateTimeout =
            TimeSpan.FromSeconds(CopilotProfileConfig.DefaultStreamingInactivityTimeoutSeconds);

        public static CopilotProviderInactivityTimeouts Resolve(
            CopilotProfileConfig profile,
            TimeSpan? firstResponseTimeoutOverride = null,
            TimeSpan? streamingUpdateTimeoutOverride = null)
        {
            ArgumentNullException.ThrowIfNull(profile);
            var firstResponseTimeout =
                firstResponseTimeoutOverride
                ?? profile.EffectiveFirstContentTimeout;
            var streamingUpdateTimeout =
                streamingUpdateTimeoutOverride
                ?? profile.EffectiveStreamingInactivityTimeout;
            ValidateTimeout(firstResponseTimeout, nameof(firstResponseTimeoutOverride));
            ValidateTimeout(streamingUpdateTimeout, nameof(streamingUpdateTimeoutOverride));
            return new CopilotProviderInactivityTimeouts(
                firstResponseTimeout,
                streamingUpdateTimeout);
        }

        public static void ValidateTimeout(TimeSpan timeout, string parameterName)
        {
            if (timeout <= TimeSpan.Zero || timeout == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "Provider inactivity timeouts must be finite and positive.");
            }
        }
    }

    internal readonly record struct CopilotProviderInactivityTimeouts(
        TimeSpan FirstResponseTimeout,
        TimeSpan StreamingUpdateTimeout)
    {
        public TimeSpan GetTimeout(CopilotProviderInactivityPhase phase)
        {
            return phase == CopilotProviderInactivityPhase.FirstResponse
                ? FirstResponseTimeout
                : StreamingUpdateTimeout;
        }
    }

    internal static class CopilotProviderResponseContent
    {
        public static bool HasAny(IEnumerable<AIContent>? contents)
        {
            return (contents ?? Enumerable.Empty<AIContent>()).Any(content => content switch
            {
                UsageContent => false,
                TextContent text => !string.IsNullOrEmpty(text.Text),
                TextReasoningContent reasoning =>
                    !string.IsNullOrEmpty(reasoning.Text)
                    || !string.IsNullOrEmpty(reasoning.ProtectedData),
                _ => true,
            });
        }
    }

    internal sealed class CopilotProviderInactivityChatClient : DelegatingChatClient
    {
        private readonly TimeSpan _firstResponseTimeout;
        private readonly TimeSpan _streamingUpdateTimeout;

        public CopilotProviderInactivityChatClient(
            IChatClient innerClient,
            TimeSpan? firstResponseTimeout = null,
            TimeSpan? streamingUpdateTimeout = null)
            : base(innerClient)
        {
            _firstResponseTimeout =
                firstResponseTimeout
                ?? CopilotProviderInactivityPolicy.DefaultFirstResponseTimeout;
            _streamingUpdateTimeout =
                streamingUpdateTimeout
                ?? CopilotProviderInactivityPolicy.DefaultStreamingUpdateTimeout;
            CopilotProviderInactivityPolicy.ValidateTimeout(
                _firstResponseTimeout,
                nameof(firstResponseTimeout));
            CopilotProviderInactivityPolicy.ValidateTimeout(
                _streamingUpdateTimeout,
                nameof(streamingUpdateTimeout));
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var timeoutCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCancellation.CancelAfter(_firstResponseTimeout);
            try
            {
                var response = await base.GetResponseAsync(
                    messages,
                    options,
                    timeoutCancellation.Token).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (timeoutCancellation.IsCancellationRequested)
                {
                    throw new CopilotProviderInactivityException(
                        CopilotProviderInactivityPhase.FirstResponse,
                        _firstResponseTimeout);
                }
                return response;
            }
            catch (Exception exception)
                when ((exception is OperationCanceledException
                        or ObjectDisposedException
                        or IOException
                        or HttpRequestException)
                    && !cancellationToken.IsCancellationRequested
                    && timeoutCancellation.IsCancellationRequested)
            {
                throw new CopilotProviderInactivityException(
                    CopilotProviderInactivityPhase.FirstResponse,
                    _firstResponseTimeout);
            }
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var timeoutCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var enumerator = base.GetStreamingResponseAsync(
                    messages,
                    options,
                    timeoutCancellation.Token)
                .GetAsyncEnumerator(timeoutCancellation.Token);
            await using (enumerator)
            {
                var receivedContent = false;
                var remaining = _firstResponseTimeout;
                while (true)
                {
                    if (remaining <= TimeSpan.Zero)
                    {
                        timeoutCancellation.Cancel();
                        throw CreateTimeout(receivedContent);
                    }

                    timeoutCancellation.CancelAfter(remaining);
                    var stopwatch = Stopwatch.StartNew();
                    bool hasNext;
                    try
                    {
                        hasNext = await enumerator.MoveNextAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception)
                        when ((exception is OperationCanceledException
                                or ObjectDisposedException
                                or IOException
                                or HttpRequestException)
                            && !cancellationToken.IsCancellationRequested
                            && timeoutCancellation.IsCancellationRequested)
                    {
                        throw CreateTimeout(receivedContent);
                    }
                    finally
                    {
                        if (!timeoutCancellation.IsCancellationRequested)
                            timeoutCancellation.CancelAfter(Timeout.InfiniteTimeSpan);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    if (timeoutCancellation.IsCancellationRequested)
                        throw CreateTimeout(receivedContent);
                    if (!hasNext)
                        yield break;

                    var update = enumerator.Current;
                    if (CopilotProviderResponseContent.HasAny(update.Contents))
                    {
                        receivedContent = true;
                        remaining = _streamingUpdateTimeout;
                    }
                    else
                    {
                        remaining = remaining > stopwatch.Elapsed
                            ? remaining - stopwatch.Elapsed
                            : TimeSpan.Zero;
                    }
                    yield return update;
                }
            }
        }

        private CopilotProviderInactivityException CreateTimeout(bool receivedContent)
        {
            return receivedContent
                ? new CopilotProviderInactivityException(
                    CopilotProviderInactivityPhase.StreamingUpdate,
                    _streamingUpdateTimeout)
                : new CopilotProviderInactivityException(
                    CopilotProviderInactivityPhase.FirstResponse,
                    _firstResponseTimeout);
        }
    }
}
