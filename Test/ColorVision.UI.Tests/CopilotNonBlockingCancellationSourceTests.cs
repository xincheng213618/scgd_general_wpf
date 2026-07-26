using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotNonBlockingCancellationSourceTests
{
    [Fact]
    public async Task RequestAndDisposeReturnWhileCancellationCallbackIsBlocked()
    {
        var source = new CopilotNonBlockingCancellationSource();
        using var callbackStarted = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var callbackCompleted = new ManualResetEventSlim();
        using var registration = source.Token.Register(() =>
        {
            callbackStarted.Set();
            try
            {
                releaseCallback.Wait(CancellationToken.None);
            }
            finally
            {
                callbackCompleted.Set();
            }
        });
        var requestTask = Task.Factory.StartNew(
            source.RequestCancellation,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        try
        {
            Assert.True(callbackStarted.Wait(TimeSpan.FromSeconds(2)));
            await requestTask.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(source.Token.IsCancellationRequested);

            source.Dispose();

            Assert.False(source.DisposalCompletion.IsCompleted);
        }
        finally
        {
            releaseCallback.Set();
            _ = callbackCompleted.Wait(TimeSpan.FromSeconds(2));
            source.Dispose();
        }

        await source.DisposalCompletion.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task HostedRunCancelAndCompleteDoNotWaitForBlockingCallback()
    {
        using var run = new CopilotHostedAgentRun("conversation", CopilotAgentMode.Auto);
        using var callbackStarted = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var callbackCompleted = new ManualResetEventSlim();
        using var registration = run.CancellationToken.Register(() =>
        {
            callbackStarted.Set();
            try
            {
                releaseCallback.Wait(CancellationToken.None);
            }
            finally
            {
                callbackCompleted.Set();
            }
        });
        Assert.True(run.TryStart());
        var cancelTask = Task.Factory.StartNew(
            run.TryRequestCancel,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        try
        {
            Assert.True(callbackStarted.Wait(TimeSpan.FromSeconds(2)));
            Assert.True(await cancelTask.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.Equal(CopilotHostedRunState.CancelRequested, run.State);

            var completeTask = Task.Factory.StartNew(
                () => run.Complete(error: null),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
            await completeTask.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(CopilotHostedRunState.Completed, run.State);
            Assert.True(run.Completion.IsCanceled);
        }
        finally
        {
            releaseCallback.Set();
            _ = callbackCompleted.Wait(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task RepeatedRequestAndDisposeStayNonBlockingWhileCallbackIsRunning()
    {
        var source = new CopilotNonBlockingCancellationSource();
        using var callbackStarted = new ManualResetEventSlim();
        using var releaseCallback = new ManualResetEventSlim();
        using var registration = source.Token.Register(() =>
        {
            callbackStarted.Set();
            releaseCallback.Wait(CancellationToken.None);
        });
        var firstRequest = Task.Factory.StartNew(
            source.RequestCancellation,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        try
        {
            Assert.True(callbackStarted.Wait(TimeSpan.FromSeconds(2)));
            await firstRequest.WaitAsync(TimeSpan.FromSeconds(1));

            source.RequestCancellation();
            source.Dispose();
            source.RequestCancellation();
            source.Dispose();

            Assert.True(source.IsCancellationRequested);
            Assert.False(source.DisposalCompletion.IsCompleted);
        }
        finally
        {
            releaseCallback.Set();
            source.Dispose();
        }

        await source.DisposalCompletion.WaitAsync(TimeSpan.FromSeconds(2));
    }
}
