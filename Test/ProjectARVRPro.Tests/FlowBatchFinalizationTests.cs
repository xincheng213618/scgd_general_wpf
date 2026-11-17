using System.ComponentModel;
using System.Windows.Threading;

namespace ProjectARVRPro.Tests;

public sealed class FlowBatchFinalizationTests
{
    [Fact]
    public Task DispatcherCanRespondAndDrainThePendingWriteBeforeResumingFinalization() =>
        ArvrDrawingOverlayCompatibilityTests.RunAsyncOnStaThread(async () =>
        {
            var finalization = new FlowBatchFinalization();
            finalization.BeginCompletion();
            TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using ManualResetEventSlim release = new();
            TimeSpan watchdog = TimeSpan.FromSeconds(10);
            int writes = 0;
            Task completed = finalization.RunAsync(() =>
            {
                Interlocked.Increment(ref writes);
                entered.SetResult();
                Assert.True(release.Wait(watchdog));
            });

            try
            {
                await entered.Task.WaitAsync(watchdog);
                Assert.False(completed.IsCompleted);
                Task pending = Assert.IsAssignableFrom<Task>(finalization.PendingWrite);
                bool dispatcherResponded = await Dispatcher.CurrentDispatcher.InvokeAsync(() => true);
                Assert.True(dispatcherResponded);
                await Assert.ThrowsAsync<InvalidOperationException>(() => finalization.RunAsync(() => Interlocked.Increment(ref writes)));

                CancelEventArgs intercepted = new() { Cancel = true };
                finalization.OnClosing(intercepted);
                Assert.False(finalization.CloseRequested);

                CancelEventArgs close = new();
                finalization.OnClosing(close);
                Assert.True(close.Cancel);
                Assert.True(finalization.CloseRequested);

                release.Set();
                // Closing on the dispatcher waits for the command, even though the
                // finalization continuation cannot run until the close handler returns.
                pending.WaitAsync(watchdog).GetAwaiter().GetResult();
                Assert.False(completed.IsCompleted);
            }
            finally
            {
                release.Set();
                await completed;
            }

            Assert.Equal(1, writes);
            Assert.Null(finalization.PendingWrite);
            Assert.True(finalization.IsCompleting);
            CancelEventArgs repeatedClose = new();
            finalization.OnClosing(repeatedClose);
            Assert.True(repeatedClose.Cancel);
            Assert.True(finalization.EndCompletion());
            Assert.False(finalization.EndCompletion());

            CancelEventArgs replay = new();
            finalization.OnClosing(replay);
            Assert.False(replay.Cancel);
            Assert.False(finalization.CloseRequested);
        });
}
