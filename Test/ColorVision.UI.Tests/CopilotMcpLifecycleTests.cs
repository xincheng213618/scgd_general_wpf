using ColorVision.Copilot;
using ModelContextProtocol.Protocol;
using System.Collections.Concurrent;

namespace ColorVision.UI.Tests;

public sealed class CopilotMcpLifecycleTests
{
    [Fact]
    public async Task ExternalToolLeaseDisposesResourcesInReverseOrderOnlyOnce()
    {
        var disposalOrder = new ConcurrentQueue<string>();
        var first = new RecordingAsyncDisposable("first", disposalOrder);
        var second = new RecordingAsyncDisposable("second", disposalOrder, delayCompletion: true);
        var lease = new CopilotExternalToolLease(
            tools: null,
            diagnostics: null,
            resources: [first, second],
            disposalTimeout: TimeSpan.FromSeconds(2),
            resourceDisposalTimeout: TimeSpan.FromSeconds(1));

        var firstDisposal = lease.DisposeAsync().AsTask();
        await second.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var concurrentDisposal = lease.DisposeAsync().AsTask();
        second.Release.TrySetResult();
        await Task.WhenAll(firstDisposal, concurrentDisposal).WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(["second", "first"], disposalOrder);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
    }

    [Fact]
    public async Task ExternalToolLeaseBoundsHungCleanupAndStartsRemainingResources()
    {
        var disposalOrder = new ConcurrentQueue<string>();
        var later = new RecordingAsyncDisposable("later", disposalOrder);
        var hung = new RecordingAsyncDisposable("hung", disposalOrder, delayCompletion: true);
        var lease = new CopilotExternalToolLease(
            tools: null,
            diagnostics: null,
            resources: [later, hung],
            disposalTimeout: TimeSpan.FromMilliseconds(200),
            resourceDisposalTimeout: TimeSpan.FromMilliseconds(50));

        var disposal = lease.DisposeAsync().AsTask();
        await hung.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await later.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await disposal.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(["hung", "later"], disposalOrder);
        Assert.False(hung.Completed.Task.IsCompleted);

        hung.Release.TrySetResult();
        await hung.Completed.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ToolDiscoveryReturnsCancellationWhenPageProviderIgnoresToken()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<ListToolsResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var discovery = CopilotMcpToolDiscoveryPaginator.DiscoverAsync(
            (_, _) =>
            {
                started.TrySetResult();
                return new ValueTask<ListToolsResult>(release.Task);
            },
            cancellationToken: cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => discovery.WaitAsync(TimeSpan.FromSeconds(1)));

        release.TrySetResult(new ListToolsResult { Tools = [] });
    }

    private sealed class RecordingAsyncDisposable(
        string name,
        ConcurrentQueue<string> disposalOrder,
        bool delayCompletion = false) : IAsyncDisposable
    {
        private int _disposeCount;

        public int DisposeCount => Volatile.Read(ref _disposeCount);

        public TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Completed { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async ValueTask DisposeAsync()
        {
            Interlocked.Increment(ref _disposeCount);
            disposalOrder.Enqueue(name);
            Started.TrySetResult();
            if (delayCompletion)
                await Release.Task;
            Completed.TrySetResult();
        }
    }
}
