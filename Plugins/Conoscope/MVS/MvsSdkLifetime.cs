using System;
using System.Threading;
using MvCamCtrl.NET;

namespace Conoscope.MVS;

internal readonly record struct MvsSdkAcquireResult(MvsSdkLease? Lease, int NativeResult)
{
    internal bool Acquired => Lease != null;
}

/// <summary>
/// Process-wide ownership for the process-global MVS SDK initialization state.
/// A window keeps its lease until all of its native camera work has stopped.
/// </summary>
internal sealed class MvsSdkLifetime
{
    internal static MvsSdkLifetime Shared { get; } = new(
        MyCamera.MV_CC_Initialize_NET,
        MyCamera.MV_CC_Finalize_NET);

    private readonly object gate = new();
    private readonly Func<int> initialize;
    private readonly Func<int> finalize;
    private bool initialized;
    private int activeLeaseCount;
    private long epoch;

    internal MvsSdkLifetime(Func<int> initialize, Func<int> finalize)
    {
        this.initialize = initialize ?? throw new ArgumentNullException(nameof(initialize));
        this.finalize = finalize ?? throw new ArgumentNullException(nameof(finalize));
    }

    internal int ActiveLeaseCount
    {
        get
        {
            lock (gate)
            {
                return activeLeaseCount;
            }
        }
    }

    internal MvsSdkAcquireResult Acquire()
    {
        lock (gate)
        {
            if (!initialized)
            {
                int nativeResult = initialize();
                if (nativeResult != MyCamera.MV_OK)
                {
                    return new MvsSdkAcquireResult(null, nativeResult);
                }

                initialized = true;
                epoch++;
            }

            activeLeaseCount++;
            return new MvsSdkAcquireResult(new MvsSdkLease(this, epoch), MyCamera.MV_OK);
        }
    }

    internal int Release(long leaseEpoch)
    {
        lock (gate)
        {
            if (!initialized || leaseEpoch != epoch || activeLeaseCount <= 0)
            {
                return MyCamera.MV_OK;
            }

            activeLeaseCount--;
            if (activeLeaseCount != 0)
            {
                return MyCamera.MV_OK;
            }

            int nativeResult = finalize();
            if (nativeResult == MyCamera.MV_OK)
            {
                initialized = false;
            }

            return nativeResult;
        }
    }
}

internal sealed class MvsSdkLease : IDisposable
{
    private readonly MvsSdkLifetime lifetime;
    private readonly long epoch;
    private int released;

    internal MvsSdkLease(MvsSdkLifetime lifetime, long epoch)
    {
        this.lifetime = lifetime;
        this.epoch = epoch;
    }

    internal int Release()
    {
        return Interlocked.Exchange(ref released, 1) == 0
            ? lifetime.Release(epoch)
            : MyCamera.MV_OK;
    }

    public void Dispose()
    {
        Release();
    }
}
