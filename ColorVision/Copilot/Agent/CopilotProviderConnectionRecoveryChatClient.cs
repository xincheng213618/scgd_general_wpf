using Microsoft.Extensions.AI;
using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotProviderConnectionRecoveryCancelledException : OperationCanceledException
    {
        public CopilotProviderConnectionRecoveryCancelledException(
            OperationCanceledException innerException,
            CancellationToken cancellationToken)
            : base(
                "Provider connection recovery was cancelled while waiting for the network.",
                innerException,
                cancellationToken)
        {
        }
    }

    internal sealed record CopilotProviderConnectionRecoveryInfo(
        int RecoveryAttempt,
        TimeSpan Delay,
        string FailureKind,
        string RequestId = "")
    {
        public string ToDiagnosticText()
        {
            var delay = Delay.TotalSeconds >= 1
                ? $"{Delay.TotalSeconds:0.#}s"
                : $"{Math.Max(0, Delay.TotalMilliseconds):0}ms";
            var normalizedRequestId = CopilotProviderRequestId.Normalize(RequestId);
            var request = normalizedRequestId.Length == 0
                ? string.Empty
                : $" · request {normalizedRequestId}";
            return $"Provider connection unavailable · recovery attempt {RecoveryAttempt} · {FailureKind}{request} · waiting {delay}; the ordinary request-retry budget and token accounting remain unchanged. Cancel the turn to stop waiting.";
        }
    }

    internal static class CopilotProviderConnectionRecoveryProtocol
    {
        public static void Validate(CopilotProviderConnectionRecoveryInfo recovery)
        {
            ArgumentNullException.ThrowIfNull(recovery);
            if (recovery.RecoveryAttempt < 1
                || recovery.Delay < TimeSpan.Zero
                || string.IsNullOrWhiteSpace(recovery.FailureKind)
                || recovery.FailureKind.Length > 96
                || recovery.FailureKind.Any(char.IsControl)
                || !string.Equals(
                    recovery.RequestId,
                    CopilotProviderRequestId.Normalize(recovery.RequestId),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Copilot provider connection-recovery event has invalid metadata.");
            }
        }

        public static void ValidateDiagnostic(
            CopilotProviderConnectionRecoveryInfo recovery,
            string diagnosticText)
        {
            Validate(recovery);
            if (!string.Equals(
                diagnosticText,
                recovery.ToDiagnosticText(),
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Copilot Agent provider connection-recovery diagnostic has mismatched metadata.");
            }
        }
    }

    internal sealed class CopilotProviderConnectionRecoveryState(
        TimeSpan initialDelay,
        TimeSpan maximumDelay)
    {
        private int _attempt;
        private TimeSpan _nextDelay = initialDelay;

        public CopilotProviderConnectionRecoveryInfo Next(string requestId)
        {
            _attempt = _attempt == int.MaxValue ? int.MaxValue : _attempt + 1;
            var recovery = new CopilotProviderConnectionRecoveryInfo(
                _attempt,
                _nextDelay,
                "connection failure",
                CopilotProviderRequestId.Normalize(requestId));
            _nextDelay = TimeSpan.FromTicks(Math.Min(
                maximumDelay.Ticks,
                _nextDelay.Ticks > long.MaxValue / 2
                    ? long.MaxValue
                    : _nextDelay.Ticks * 2));
            return recovery;
        }
    }

    internal sealed class CopilotProviderConnectionRecoveryChatClient : DelegatingChatClient
    {
        internal static readonly TimeSpan DefaultInitialDelay = TimeSpan.FromSeconds(5);
        internal static readonly TimeSpan DefaultMaximumDelay = TimeSpan.FromMinutes(1);

        private const int MaximumBufferedPreambleUpdates = 64;
        private readonly Action<CopilotProviderConnectionRecoveryInfo>? _onRecovery;
        private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
        private readonly TimeSpan _initialDelay;
        private readonly TimeSpan _maximumDelay;

        internal static bool IsEligibleRootRequest(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return request.RuntimePurpose == CopilotAgentRuntimePurpose.Standard
                && string.IsNullOrWhiteSpace(request.RuntimeExecutionScope.ParentRunId);
        }

        public CopilotProviderConnectionRecoveryChatClient(
            IChatClient innerClient,
            Action<CopilotProviderConnectionRecoveryInfo>? onRecovery = null,
            Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
            TimeSpan? initialDelay = null,
            TimeSpan? maximumDelay = null)
            : base(innerClient)
        {
            _initialDelay = initialDelay ?? DefaultInitialDelay;
            _maximumDelay = maximumDelay ?? DefaultMaximumDelay;
            if (_initialDelay <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(initialDelay));
            if (_maximumDelay < _initialDelay)
                throw new ArgumentOutOfRangeException(nameof(maximumDelay));

            _onRecovery = onRecovery;
            _delayAsync = delayAsync ?? Task.Delay;
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var materializedMessages = messages is Microsoft.Extensions.AI.ChatMessage[] array
                ? array
                : messages?.ToArray() ?? Array.Empty<Microsoft.Extensions.AI.ChatMessage>();
            var state = new CopilotProviderConnectionRecoveryState(_initialDelay, _maximumDelay);

            while (true)
            {
                try
                {
                    return await base.GetResponseAsync(
                        materializedMessages,
                        options,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (TryCreateRecovery(
                    exception,
                    state,
                    cancellationToken,
                    out var recovery))
                {
                    _onRecovery?.Invoke(recovery);
                    await WaitForRecoveryDelayAsync(recovery.Delay, cancellationToken).ConfigureAwait(false);
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
            var state = new CopilotProviderConnectionRecoveryState(_initialDelay, _maximumDelay);

            while (true)
            {
                CopilotStreamingAttempt? streamingAttempt;
                try
                {
                    streamingAttempt = await OpenStreamingAttemptAsync(
                        materializedMessages,
                        options,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (Exception exception) when (TryCreateRecovery(
                    exception,
                    state,
                    cancellationToken,
                    out var recovery))
                {
                    _onRecovery?.Invoke(recovery);
                    await WaitForRecoveryDelayAsync(recovery.Delay, cancellationToken).ConfigureAwait(false);
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
                        while (await enumerator.MoveNextAsync().ConfigureAwait(false))
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
                while (await enumerator.MoveNextAsync().ConfigureAwait(false))
                {
                    var update = enumerator.Current;
                    var hasResponseContent = CopilotProviderResponseContent.HasAny(update.Contents);
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

                await enumerator.DisposeAsync().ConfigureAwait(false);
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
                        await enumerator.DisposeAsync().ConfigureAwait(false);
                    }
                    catch
                    {
                        // Preserve the connection failure that controls recovery.
                    }
                }
                throw;
            }
        }

        internal static bool TryCreateRecovery(
            Exception exception,
            CopilotProviderConnectionRecoveryState state,
            CancellationToken cancellationToken,
            out CopilotProviderConnectionRecoveryInfo recovery)
        {
            recovery = null!;
            if (!IsConnectionFailure(exception, cancellationToken))
                return false;

            recovery = state.Next(CopilotProviderRequestId.Find(exception));
            return true;
        }

        private async Task WaitForRecoveryDelayAsync(
            TimeSpan delay,
            CancellationToken cancellationToken)
        {
            try
            {
                await _delayAsync(delay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException exception) when (cancellationToken.IsCancellationRequested)
            {
                throw new CopilotProviderConnectionRecoveryCancelledException(
                    exception,
                    cancellationToken);
            }
        }

        internal static bool IsConnectionFailure(
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception == null || cancellationToken.IsCancellationRequested)
                return false;

            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is ClientResultException { Status: <= 0 }
                    or HttpRequestException { StatusCode: null }
                    or SocketException)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class CopilotStreamingAttempt(
            IAsyncEnumerator<ChatResponseUpdate>? enumerator,
            IReadOnlyList<ChatResponseUpdate> bufferedUpdates) : IAsyncDisposable
        {
            public IAsyncEnumerator<ChatResponseUpdate>? Enumerator { get; } = enumerator;

            public IReadOnlyList<ChatResponseUpdate> BufferedUpdates { get; } = bufferedUpdates;

            public ValueTask DisposeAsync() =>
                Enumerator?.DisposeAsync() ?? ValueTask.CompletedTask;
        }
    }
}
