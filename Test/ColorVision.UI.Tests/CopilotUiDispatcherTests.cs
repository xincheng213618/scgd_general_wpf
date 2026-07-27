using ColorVision.Copilot;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CopilotUiDispatcherTests
{
    [Fact]
    public async Task QueuedInvocationObservesCancellationWithoutRunningLater()
    {
        using var blockedDispatcher = await BlockedDispatcher.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var invocationCount = 0;

        var invocation = CopilotUiDispatcher.InvokeAsync(
            blockedDispatcher.Dispatcher,
            () => Interlocked.Increment(ref invocationCount),
            cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => invocation.WaitAsync(TimeSpan.FromSeconds(1)));

        blockedDispatcher.Dispose();
        Assert.Equal(0, Volatile.Read(ref invocationCount));
    }

    [Fact]
    public async Task BoundedInvocationReturnsFallbackWithoutRunningLater()
    {
        using var blockedDispatcher = await BlockedDispatcher.CreateAsync();
        var invocationCount = 0;

        var result = await Task.Run(() => CopilotUiDispatcher.InvokeBounded(
            blockedDispatcher.Dispatcher,
            () =>
            {
                Interlocked.Increment(ref invocationCount);
                return true;
            },
            fallback: false,
            TimeSpan.FromMilliseconds(100))).WaitAsync(TimeSpan.FromSeconds(1));

        blockedDispatcher.Dispose();
        Assert.False(result);
        Assert.Equal(0, Volatile.Read(ref invocationCount));
    }

    [Fact]
    public async Task CanceledScheduledActionDoesNotRunAfterDispatcherRecovers()
    {
        using var blockedDispatcher = await BlockedDispatcher.CreateAsync();
        using var cancellation = new CancellationTokenSource();
        var invocationCount = 0;

        CopilotUiDispatcher.Schedule(
            blockedDispatcher.Dispatcher,
            () => Interlocked.Increment(ref invocationCount),
            cancellation.Token);
        cancellation.Cancel();

        blockedDispatcher.Dispose();
        Assert.Equal(0, Volatile.Read(ref invocationCount));
    }

    private sealed class BlockedDispatcher : IDisposable
    {
        private readonly ManualResetEventSlim _releaseBlocker = new();
        private readonly Thread _thread;
        private bool _disposed;

        private BlockedDispatcher(Dispatcher dispatcher, Thread thread)
        {
            Dispatcher = dispatcher;
            _thread = thread;
        }

        public Dispatcher Dispatcher { get; }

        public static async Task<BlockedDispatcher> CreateAsync()
        {
            var dispatcherReady = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
            var blockerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
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
            var blockedDispatcher = new BlockedDispatcher(dispatcher, thread);
            _ = dispatcher.BeginInvoke(() =>
            {
                blockerStarted.TrySetResult();
                blockedDispatcher._releaseBlocker.Wait();
            }, DispatcherPriority.Send);
            await blockerStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            return blockedDispatcher;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _releaseBlocker.Set();
            Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            Assert.True(_thread.Join(TimeSpan.FromSeconds(2)));
            _releaseBlocker.Dispose();
        }
    }
}
