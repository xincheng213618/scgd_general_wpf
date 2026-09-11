using System.ComponentModel;

namespace ColorVision.UI.Tests;

public sealed class SingleInstanceStartupCoordinatorTests
{
    [Fact]
    public async Task StartsByForcingAndContinuesAfterConfirmedExit()
    {
        int attempts = 0;
        var coordinator = new SingleInstanceStartupCoordinator(_ => { attempts++; return Task.CompletedTask; });
        Assert.Equal(SingleInstanceStartupResult.ClosedEarlierInstances, await coordinator.RunAsync());
        Assert.Equal(1, attempts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedTerminationStaysOpenWithReasonAndAllowsRetry(bool accessDenied)
    {
        int attempts = 0;
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinator = new SingleInstanceStartupCoordinator(_ => ++attempts == 1
            ? Task.FromException(accessDenied
                ? new InvalidOperationException("PID 123", new Win32Exception(5))
                : new TimeoutException("PID 123 仍未退出")) : Task.CompletedTask);
        coordinator.StateChanged += () => { if (!coordinator.IsForcing) failed.TrySetResult(); };
        Task<SingleInstanceStartupResult> run = coordinator.RunAsync();
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(run.IsCompleted);
        Assert.Contains("123", coordinator.Detail);
        Assert.Contains(accessDenied ? "系统仍拒绝访问" : "重启 Windows", coordinator.Detail);
        coordinator.Choose(SingleInstanceStartupChoice.ForceClose);
        Assert.Equal(SingleInstanceStartupResult.ClosedEarlierInstances, await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(2, attempts);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChoiceDuringTerminationWaitsForCancellationCleanup(bool openAdditional)
    {
        SingleInstanceStartupChoice choice = openAdditional ? SingleInstanceStartupChoice.OpenAdditional : SingleInstanceStartupChoice.Cancel;
        SingleInstanceStartupResult expected = openAdditional ? SingleInstanceStartupResult.OpenAdditional : SingleInstanceStartupResult.Cancel;
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool cleanupCompleted = false;
        var coordinator = new SingleInstanceStartupCoordinator(async token =>
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally
            {
                cancellationObserved.SetResult();
                await cleanup.Task;
                cleanupCompleted = true;
            }
        });
        Task<SingleInstanceStartupResult> run = coordinator.RunAsync();
        coordinator.Choose(choice);
        await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(run.IsCompleted);
        cleanup.SetResult();
        Assert.Equal(expected, await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.True(cleanupCompleted);
    }

    [Fact]
    public async Task DirectOpenAfterFailureDoesNotRetryTermination()
    {
        int attempts = 0;
        var coordinator = new SingleInstanceStartupCoordinator(_ => { attempts++; throw new TimeoutException("PID 123"); });
        Task<SingleInstanceStartupResult> run = coordinator.RunAsync();
        Assert.False(run.IsCompleted);
        coordinator.Choose(SingleInstanceStartupChoice.OpenAdditional);
        Assert.Equal(SingleInstanceStartupResult.OpenAdditional, await run.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, attempts);
    }
}
