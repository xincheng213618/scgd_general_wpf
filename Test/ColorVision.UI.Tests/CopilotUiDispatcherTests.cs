using ColorVision.Copilot;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CopilotUiDispatcherTests
{
    [Fact]
    public async Task QueuedInvocationObservesCancellationWithoutRunningLater()
    {
        var dispatcherReady = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseBlocker = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var invocationCount = 0;
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            dispatcherReady.TrySetResult(dispatcher);
            Dispatcher.Run();
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var dispatcher = await dispatcherReady.Task.WaitAsync(TimeSpan.FromSeconds(1));
        _ = dispatcher.BeginInvoke(() =>
        {
            blockerStarted.TrySetResult();
            releaseBlocker.Wait();
        }, DispatcherPriority.Send);
        await blockerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        var invocation = CopilotUiDispatcher.InvokeAsync(
            dispatcher,
            () => Interlocked.Increment(ref invocationCount),
            cancellation.Token);
        cancellation.Cancel();

        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => invocation.WaitAsync(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            releaseBlocker.Set();
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            Assert.True(thread.Join(TimeSpan.FromSeconds(2)));
        }

        Assert.Equal(0, Volatile.Read(ref invocationCount));
    }
}
