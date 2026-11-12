using System;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Video;

internal enum LocalVideoRecoveryAction { None, Restart, Stop }

internal sealed class LocalVideoFrameWatchdog
{
    private long lastFrameAt;
    private long? stalledFrameAt;
    private long restartAt;

    internal bool IsRecovering => stalledFrameAt.HasValue;

    internal void Start(long now)
    {
        MarkFrame(now);
        stalledFrameAt = null;
    }

    // Called by the native callback. All recovery decisions run on the dispatcher.
    internal void MarkFrame(long now) => Volatile.Write(ref lastFrameAt, now);

    internal bool HasNewFrame(long observedFrameAt) => Volatile.Read(ref lastFrameAt) != observedFrameAt;

    internal void RestartCompleted(long now) => restartAt = now;

    internal LocalVideoRecoveryAction Check(long now, double exposureMs, out long observedFrameAt)
    {
        observedFrameAt = Volatile.Read(ref lastFrameAt);
        if (stalledFrameAt != observedFrameAt) stalledFrameAt = null;

        // Allow for exposure, native capture timeout and full-frame conversion.
        double timeoutMs = Math.Max(6000, double.IsFinite(exposureMs) ? exposureMs + 4000 : 0);
        if (now - (stalledFrameAt.HasValue ? restartAt : observedFrameAt) < timeoutMs)
            return LocalVideoRecoveryAction.None;
        if (stalledFrameAt.HasValue) return LocalVideoRecoveryAction.Stop;

        stalledFrameAt = observedFrameAt;
        restartAt = now;
        return LocalVideoRecoveryAction.Restart;
    }
}
