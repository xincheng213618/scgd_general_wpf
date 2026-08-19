#pragma warning disable CA1001 // The semaphore lifetime matches the owning Agent run; its WaitHandle is never requested.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotAgentCheckpointPublicationGate
    {
        private readonly SemaphoreSlim _gate = new(1, 1);

        public async ValueTask<bool> TryRunAsync(
            Func<CancellationToken, ValueTask<bool>> publish,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(publish);
            if (!await _gate.WaitAsync(
                    millisecondsTimeout: 0,
                    cancellationToken).ConfigureAwait(false))
            {
                return false;
            }

            try
            {
                return await publish(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        public async ValueTask<bool> RunRequiredAsync(
            Func<CancellationToken, ValueTask<bool>> publish,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(publish);
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                return await publish(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
