using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotCancellationGuardChatClient : DelegatingChatClient
    {
        internal static readonly TimeSpan DefaultEnumeratorDisposalTimeout = TimeSpan.FromSeconds(1);

        private readonly TimeSpan _enumeratorDisposalTimeout;

        public CopilotCancellationGuardChatClient(
            IChatClient innerClient,
            TimeSpan? enumeratorDisposalTimeout = null)
            : base(innerClient)
        {
            _enumeratorDisposalTimeout = enumeratorDisposalTimeout ?? DefaultEnumeratorDisposalTimeout;
            if (_enumeratorDisposalTimeout <= TimeSpan.Zero
                || _enumeratorDisposalTimeout == Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(enumeratorDisposalTimeout),
                    "Enumerator disposal timeout must be finite and positive.");
            }
        }

        public override async Task<ChatResponse> GetResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var responseTask = base.GetResponseAsync(messages, options, cancellationToken);
            try
            {
                return await responseTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CopilotCancellationBoundary.ObserveLateFault(responseTask);
                throw;
            }
        }

        public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var enumerator = base.GetStreamingResponseAsync(messages, options, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
            await using var lease = new CancellationGuardEnumerator(
                enumerator,
                _enumeratorDisposalTimeout);
            while (await lease.MoveNextAsync(cancellationToken).ConfigureAwait(false))
                yield return lease.Current;
        }

        private sealed class CancellationGuardEnumerator : IAsyncDisposable
        {
            private readonly TimeSpan _disposalTimeout;
            private IAsyncEnumerator<ChatResponseUpdate>? _enumerator;
            private Task<bool>? _pendingMove;
            private bool _cancellationInterrupted;

            public CancellationGuardEnumerator(
                IAsyncEnumerator<ChatResponseUpdate> enumerator,
                TimeSpan disposalTimeout)
            {
                _enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
                _disposalTimeout = disposalTimeout;
            }

            public ChatResponseUpdate Current =>
                _enumerator?.Current
                ?? throw new ObjectDisposedException(nameof(CancellationGuardEnumerator));

            public async ValueTask<bool> MoveNextAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var enumerator = _enumerator
                    ?? throw new ObjectDisposedException(nameof(CancellationGuardEnumerator));
                var pendingMove = enumerator.MoveNextAsync().AsTask();
                _pendingMove = pendingMove;
                try
                {
                    return await pendingMove.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _cancellationInterrupted = true;
                    CopilotCancellationBoundary.ObserveLateFault(pendingMove);
                    throw;
                }
                finally
                {
                    if (pendingMove.IsCompleted)
                        _pendingMove = null;
                }
            }

            public ValueTask DisposeAsync()
            {
                var enumerator = Interlocked.Exchange(ref _enumerator, null);
                if (enumerator == null)
                    return ValueTask.CompletedTask;

                var pendingMove = Interlocked.Exchange(ref _pendingMove, null);
                if (pendingMove is { IsCompleted: false })
                {
                    CopilotCancellationBoundary.ObserveLateFault(pendingMove);
                    _ = DisposeAfterMoveCompletesAsync(pendingMove, enumerator, _disposalTimeout);
                    return ValueTask.CompletedTask;
                }

                return new ValueTask(DisposeBoundedAsync(
                    enumerator,
                    _disposalTimeout,
                    suppressFailure: _cancellationInterrupted));
            }

            private static async Task DisposeAfterMoveCompletesAsync(
                Task<bool> pendingMove,
                IAsyncEnumerator<ChatResponseUpdate> enumerator,
                TimeSpan disposalTimeout)
            {
                try
                {
                    await pendingMove.ConfigureAwait(false);
                }
                catch
                {
                }

                await DisposeBoundedAsync(
                    enumerator,
                    disposalTimeout,
                    suppressFailure: true).ConfigureAwait(false);
            }

            private static async Task DisposeBoundedAsync(
                IAsyncEnumerator<ChatResponseUpdate> enumerator,
                TimeSpan disposalTimeout,
                bool suppressFailure)
            {
                Task disposeTask;
                try
                {
                    disposeTask = enumerator.DisposeAsync().AsTask();
                }
                catch (Exception ex) when (suppressFailure)
                {
                    Trace.TraceWarning(
                        "Copilot provider stream disposal failed after cancellation: {0}",
                        CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
                    return;
                }

                try
                {
                    await disposeTask.WaitAsync(disposalTimeout).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    CopilotCancellationBoundary.ObserveLateFault(disposeTask);
                    Trace.TraceWarning(
                        "Copilot provider stream disposal exceeded {0}; detaching the late cleanup.",
                        disposalTimeout);
                }
                catch (Exception ex) when (suppressFailure)
                {
                    Trace.TraceWarning(
                        "Copilot provider stream disposal failed after cancellation: {0}",
                        CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
                }
            }
        }
    }
}
