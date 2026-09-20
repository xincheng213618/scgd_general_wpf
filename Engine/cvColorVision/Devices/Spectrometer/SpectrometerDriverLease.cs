using System;

namespace cvColorVision;

/// <summary>One process-wide owner for the CM/SA spectrometer driver, including plugin sessions.</summary>
public sealed class SpectrometerDriverLease : IDisposable
{
    private static readonly object Gate = new();
    private static SpectrometerDriverLease? owner;
    private bool quarantined;

    public static SpectrometerDriverLease? TryAcquire()
    {
        lock (Gate)
        {
            if (owner != null) return null;
            return owner = new SpectrometerDriverLease();
        }
    }

    private SpectrometerDriverLease() { }
    public void Quarantine() { lock (Gate) quarantined = true; }
    public void Dispose() { lock (Gate) { if (ReferenceEquals(owner, this) && !quarantined) owner = null; } }
}
