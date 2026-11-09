using ColorVision.Engine.Services.Devices.Camera.Video;

namespace ColorVision.UI.Tests;

public class LocalVideoFrameWatchdogTests
{
    [Theory]
    [InlineData(200, 6000)]
    [InlineData(12000, 16000)]
    public void GivesTheFirstFrameAndTheRestartAnExposureAwareDeadline(double exposureMs, long timeoutMs)
    {
        var watchdog = new LocalVideoFrameWatchdog();
        watchdog.Start(100);

        Assert.Equal(LocalVideoRecoveryAction.None, watchdog.Check(100 + timeoutMs - 1, exposureMs, out _));
        Assert.Equal(LocalVideoRecoveryAction.Restart, watchdog.Check(100 + timeoutMs, exposureMs, out long frameAt));
        Assert.False(watchdog.HasNewFrame(frameAt));

        long restartedAt = 2000 + timeoutMs;
        watchdog.RestartCompleted(restartedAt);
        Assert.Equal(LocalVideoRecoveryAction.None, watchdog.Check(restartedAt + timeoutMs - 1, exposureMs, out _));
        Assert.Equal(LocalVideoRecoveryAction.Stop, watchdog.Check(restartedAt + timeoutMs, exposureMs, out _));
    }

    [Fact]
    public void NewFrameClearsTheOutageAndAllowsRecoveryFromALaterStall()
    {
        var watchdog = new LocalVideoFrameWatchdog();
        watchdog.Start(100);
        Assert.Equal(LocalVideoRecoveryAction.Restart, watchdog.Check(6100, 200, out long frameAt));

        watchdog.MarkFrame(6300);
        watchdog.RestartCompleted(6400);
        Assert.True(watchdog.HasNewFrame(frameAt));
        Assert.Equal(LocalVideoRecoveryAction.None, watchdog.Check(6500, 200, out _));
        Assert.False(watchdog.IsRecovering);
        Assert.Equal(LocalVideoRecoveryAction.Restart, watchdog.Check(12300, 200, out _));
    }
}
