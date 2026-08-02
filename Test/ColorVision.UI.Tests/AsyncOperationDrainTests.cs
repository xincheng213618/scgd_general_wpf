using ColorVision.Engine.FlowProcessing;

namespace ColorVision.UI.Tests;

public class AsyncOperationDrainTests
{
    [Fact]
    public async Task WaitCompletesAfterEveryTrackedOperationEnds()
    {
        var drain = new AsyncOperationDrain();
        Assert.True(drain.Begin());
        Assert.True(drain.Begin());

        Task<bool> wait = drain.WaitAsync(TimeSpan.FromSeconds(1));
        drain.Complete();
        Assert.False(wait.IsCompleted);

        drain.Complete();

        Assert.True(await wait);
    }

    [Fact]
    public async Task ResetReleasesOldWaitersAndIgnoresLateCompletion()
    {
        var drain = new AsyncOperationDrain();
        drain.Reset(1);
        Assert.True(drain.Begin(1));
        Task<bool> oldWait = drain.WaitAsync(
            TimeSpan.FromSeconds(1),
            1);

        drain.Reset(2);
        Assert.True(await oldWait);
        drain.Complete(1);

        Assert.True(drain.Begin(2));
        Task<bool> currentWait = drain.WaitAsync(
            TimeSpan.FromSeconds(1),
            2);
        Assert.False(currentWait.IsCompleted);
        drain.Complete(2);
        Assert.True(await currentWait);
    }

    [Fact]
    public async Task WaitReportsTimeoutWithoutChangingPendingState()
    {
        var drain = new AsyncOperationDrain();
        Assert.True(drain.Begin());

        Assert.False(await drain.WaitAsync(TimeSpan.FromMilliseconds(10)));

        drain.Complete();
        Assert.True(await drain.WaitAsync(TimeSpan.Zero));
    }
}
