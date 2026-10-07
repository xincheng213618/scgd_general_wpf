using System;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

internal readonly record struct CameraPreviewParameterChange(bool Exposure, float Value, long Version);

// One worker writes parameters; slider updates replace pending values instead of queuing SDK calls.
internal sealed class CameraPreviewParameterQueue(Action<CameraPreviewParameterChange> apply, Action<Exception> reportError) : IDisposable
{
    private readonly object sync = new();
    private CameraPreviewParameterChange? exposure, gain;
    private long version, exposureVersion, gainVersion;
    private bool running, disposed;
    private Task worker = Task.CompletedTask;

    internal Task WhenIdle { get { lock (sync) return worker; } }

    internal void Enqueue(bool isExposure, float value)
    {
        lock (sync)
        {
            if (disposed) return;
            var change = new CameraPreviewParameterChange(isExposure, value, ++version);
            if (isExposure) { exposure = change; exposureVersion = change.Version; }
            else { gain = change; gainVersion = change.Version; }
            if (!running) { running = true; worker = Task.Run(Process); }
        }
    }

    internal bool IsCurrent(CameraPreviewParameterChange change)
    {
        lock (sync) return !disposed && change.Version == (change.Exposure ? exposureVersion : gainVersion);
    }

    internal void Clear()
    {
        lock (sync) { exposure = gain = null; exposureVersion = gainVersion = ++version; }
    }

    private void Process()
    {
        while (true)
        {
            CameraPreviewParameterChange? nextExposure, nextGain;
            lock (sync)
            {
                nextExposure = exposure; nextGain = gain;
                exposure = gain = null;
                if (nextExposure == null && nextGain == null) { running = false; return; }
            }
            Apply(nextExposure);
            Apply(nextGain);
        }
    }

    private void Apply(CameraPreviewParameterChange? change)
    {
        if (change == null || !IsCurrent(change.Value)) return;
        try { apply(change.Value); }
        catch (Exception ex) { reportError(ex); }
    }

    public void Dispose()
    {
        lock (sync) { disposed = true; exposure = gain = null; }
    }
}
