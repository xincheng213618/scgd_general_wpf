using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class FlowRunGuardTests
{
    [Fact]
    public void RejectedStartAttemptDoesNotLeaveBusyState()
    {
        var guard = new FlowRunGuard();

        Assert.True(guard.TryBeginStart());
        guard.EndStartAttempt();

        Assert.False(guard.IsBusy);
        Assert.True(guard.TryBeginStart());
    }

    [Fact]
    public void StartedFlowRemainsBusyUntilLifecycleCompletes()
    {
        var guard = new FlowRunGuard();

        Assert.True(guard.TryBeginStart());
        guard.MarkStarted();
        guard.EndStartAttempt();

        Assert.True(guard.IsBusy);
        Assert.False(guard.TryBeginStart());

        guard.Complete();

        Assert.False(guard.IsBusy);
        Assert.True(guard.TryBeginStart());
    }

    [Fact]
    public void PendingStartRejectsConcurrentAttempt()
    {
        var guard = new FlowRunGuard();

        Assert.True(guard.TryBeginStart());
        Assert.False(guard.TryBeginStart());

        guard.Complete();

        Assert.False(guard.IsBusy);
    }
}
