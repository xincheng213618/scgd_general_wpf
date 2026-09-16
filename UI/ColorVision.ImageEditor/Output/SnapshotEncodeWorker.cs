using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Output
{
    /// <summary>Bounds large snapshot encoding and disk writes without creating dedicated threads per export.</summary>
    internal static class SnapshotEncodeWorker
    {
        internal const int MaxConcurrency = 2;
        private static readonly SemaphoreSlim Slots = new(MaxConcurrency, MaxConcurrency);

        internal static async Task<Reservation> ReserveAsync(CancellationToken cancellationToken)
        {
            await Slots.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new Reservation();
        }

        internal sealed class Reservation : IDisposable
        {
            private int disposed;

            internal Task RunAsync(Action action, CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(action);
                ObjectDisposedException.ThrowIf(
                    Volatile.Read(ref disposed) != 0,
                    this);
                return Task.Run(action, cancellationToken);
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                    Slots.Release();
            }
        }
    }
}
