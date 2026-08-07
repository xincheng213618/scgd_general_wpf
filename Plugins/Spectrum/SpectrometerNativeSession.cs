namespace Spectrum;

internal enum SpectrometerNativeSessionOwner
{
    None,
    Main,
    Direct
}

/// <summary>
/// Prevents the CM and SA APIs from owning the same native driver at once.
/// Ownership spans the complete connection lifetime, not an individual call.
/// </summary>
internal static class SpectrometerNativeSession
{
    private static readonly object SyncRoot = new();
    private static SpectrometerNativeSessionOwner currentOwner;
    private static bool isQuarantined;

    public static bool TryAcquire(SpectrometerNativeSessionOwner owner)
    {
        lock (SyncRoot)
        {
            if (currentOwner != SpectrometerNativeSessionOwner.None)
                return false;

            currentOwner = owner;
            isQuarantined = false;
            return true;
        }
    }

    public static void Release(SpectrometerNativeSessionOwner owner)
    {
        lock (SyncRoot)
        {
            if (currentOwner == owner && !isQuarantined)
                currentOwner = SpectrometerNativeSessionOwner.None;
        }
    }

    public static void Quarantine(SpectrometerNativeSessionOwner owner)
    {
        lock (SyncRoot)
        {
            if (currentOwner == owner)
                isQuarantined = true;
        }
    }
}
