using ColorVision.Copilot.Mcp;
using System.Diagnostics;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class CopilotMcpWorkspaceSnapshotCaptureTests
{
    [Fact]
    public async Task BusyDispatcherReturnsEmptySnapshotWithoutRunningLateCapture()
    {
        var dispatcherReady = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockerStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseBlocker = new ManualResetEventSlim();
        var captureCount = 0;
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

        var stopwatch = Stopwatch.StartNew();
        var captureTask = Task.Run(() => CopilotMcpWorkspaceSnapshotCapture.Capture(
            dispatcher,
            () =>
            {
                Interlocked.Increment(ref captureCount);
                return new CopilotMcpWorkspaceSnapshot
                {
                    SolutionDirectoryPath = @"C:\workspace",
                    SearchRootPaths = [@"C:\workspace"],
                };
            },
            TimeSpan.FromMilliseconds(100)));

        CopilotMcpWorkspaceSnapshot snapshot = null!;
        try
        {
            snapshot = await captureTask.WaitAsync(TimeSpan.FromSeconds(1));
            stopwatch.Stop();
        }
        finally
        {
            releaseBlocker.Set();
            dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            Assert.True(thread.Join(TimeSpan.FromSeconds(2)));
        }

        Assert.Empty(snapshot.SearchRootPaths);
        Assert.Equal(string.Empty, snapshot.SolutionDirectoryPath);
        Assert.Equal(0, Volatile.Read(ref captureCount));
        Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }
}
