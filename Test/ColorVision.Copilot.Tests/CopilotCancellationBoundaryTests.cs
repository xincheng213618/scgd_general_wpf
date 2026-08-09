using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCancellationBoundaryTests
{
    [Fact]
    public async Task AsyncOperationReturnsCancellationWhileSynchronousPrefixRemainsBlocked()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var completed = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var guardedTask = CopilotCancellationBoundary.RunTaskAsync(
            ignoredCancellationToken =>
            {
                started.Set();
                try
                {
                    release.Wait(CancellationToken.None);
                    return Task.FromResult(42);
                }
                finally
                {
                    completed.Set();
                }
            },
            cancellation.Token);

        try
        {
            Assert.True(started.Wait(TimeSpan.FromSeconds(1)));
            cancellation.Cancel();

            var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => guardedTask.WaitAsync(TimeSpan.FromSeconds(1)));

            Assert.Equal(cancellation.Token, exception.CancellationToken);
            Assert.False(release.IsSet);
        }
        finally
        {
            release.Set();
            _ = completed.Wait(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task SynchronousOperationReturnsCancellationWhileWorkerRemainsBlocked()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var completed = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var guardedTask = CopilotCancellationBoundary.RunSynchronousAsync(
            ignoredCancellationToken =>
            {
                started.Set();
                try
                {
                    release.Wait(CancellationToken.None);
                    return 42;
                }
                finally
                {
                    completed.Set();
                }
            },
            cancellation.Token);

        try
        {
            Assert.True(started.Wait(TimeSpan.FromSeconds(1)));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => guardedTask.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.False(release.IsSet);
        }
        finally
        {
            release.Set();
            _ = completed.Wait(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task CompletedOperationPreservesItsResult()
    {
        var result = await CopilotCancellationBoundary.RunTaskAsync(
            _ => Task.FromResult(42),
            CancellationToken.None);

        Assert.Equal(42, result);
    }
}
