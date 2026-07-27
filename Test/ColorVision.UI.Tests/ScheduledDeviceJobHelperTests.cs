using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Camera.Job;
using ColorVision.Engine.Services.Devices.Spectrum.Job;
using ColorVision.Scheduler;
using Quartz;

namespace ColorVision.UI.Tests;

public class ScheduledDeviceJobHelperTests
{
    [Fact]
    public void ScheduledDeviceJobs_DisallowSameJobKeyConcurrency()
    {
        Assert.True(typeof(CameraCaptureJob).IsDefined(typeof(DisallowConcurrentExecutionAttribute), true));
        Assert.True(typeof(SpectrumGetDataJob).IsDefined(typeof(DisallowConcurrentExecutionAttribute), true));
    }

    [Fact]
    public void GetTimeout_ZeroMeansNoTimeout()
    {
        var schedulerInfo = new SchedulerInfo { TimeoutSeconds = 0 };

        Assert.Equal(Timeout.InfiniteTimeSpan, ScheduledDeviceJobHelper.GetTimeout(schedulerInfo));
    }

    [Fact]
    public void GetTimeout_PositiveValueUsesSeconds()
    {
        var schedulerInfo = new SchedulerInfo { TimeoutSeconds = 12 };

        Assert.Equal(TimeSpan.FromSeconds(12), ScheduledDeviceJobHelper.GetTimeout(schedulerInfo));
    }

    [Fact]
    public async Task WaitForTerminalStateAsync_TimesOutWithoutTerminalState()
    {
        var msgRecord = new MsgRecord();

        await Assert.ThrowsAsync<TimeoutException>(() =>
            ScheduledDeviceJobHelper.WaitForTerminalStateAsync(
                msgRecord,
                TimeSpan.FromMilliseconds(20),
                CancellationToken.None));
    }

    [Fact]
    public async Task WaitForTerminalStateAsync_ObservesCancellation()
    {
        var msgRecord = new MsgRecord();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ScheduledDeviceJobHelper.WaitForTerminalStateAsync(
                msgRecord,
                Timeout.InfiniteTimeSpan,
                cancellation.Token));
    }
}
