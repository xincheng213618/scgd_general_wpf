using System.Threading;

namespace Conoscope.MVS;

/// <summary>
/// Coalesces frame UI work without allowing an older capture generation to
/// suppress or clear the pending callback owned by a newer generation.
/// </summary>
internal sealed class MvsFrameUiUpdateGate
{
    private long nextGeneration;
    private long pendingGeneration;

    internal long BeginGeneration()
    {
        return Interlocked.Increment(ref nextGeneration);
    }

    internal bool TryQueue(long generation)
    {
        while (true)
        {
            long pending = Volatile.Read(ref pendingGeneration);
            if (pending >= generation)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref pendingGeneration, generation, pending) == pending)
            {
                return true;
            }
        }
    }

    internal bool TryComplete(long generation)
    {
        return Interlocked.CompareExchange(ref pendingGeneration, 0, generation) == generation;
    }
}
