using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using System.Collections.ObjectModel;

namespace ColorVision.UI.Tests;

public sealed class FlowTemplateWorkspaceControllerTests
{

    [Fact]
    public async Task RefreshGateSerializesAndSkipsSupersededRefresh()
    {
        var gate = new FlowTemplateRefreshGate();
        long firstGeneration = gate.Advance();
        var firstEntered = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var applied = new List<string>();

        Task first = gate.ExecuteLatestAsync(
            firstGeneration,
            async isCurrent =>
            {
                firstEntered.SetResult();
                await releaseFirst.Task;
                if (isCurrent())
                    applied.Add("first");
            });
        await firstEntered.Task;
        Assert.True(gate.IsRefreshing);

        long secondGeneration = gate.Advance();
        Task second = gate.ExecuteLatestAsync(
            secondGeneration,
            isCurrent =>
            {
                if (isCurrent())
                    applied.Add("second");
                return Task.CompletedTask;
            });

        releaseFirst.SetResult();
        await Task.WhenAll(first, second);

        Assert.False(gate.IsRefreshing);
        Assert.Equal(["second"], applied);
        Assert.True(gate.WaitUntilIdleAsync().IsCompletedSuccessfully);
    }

    [Fact]
    public async Task IndependentRefreshGatesDoNotSupersedeEachOther()
    {
        var firstGate = new FlowTemplateRefreshGate();
        var secondGate = new FlowTemplateRefreshGate();
        long firstGeneration = firstGate.Advance();
        long secondGeneration = secondGate.Advance();
        int applied = 0;

        await Task.WhenAll(
            firstGate.ExecuteLatestAsync(
                firstGeneration,
                isCurrent =>
                {
                    if (isCurrent())
                        Interlocked.Increment(ref applied);
                    return Task.CompletedTask;
                }),
            secondGate.ExecuteLatestAsync(
                secondGeneration,
                isCurrent =>
                {
                    if (isCurrent())
                        Interlocked.Increment(ref applied);
                    return Task.CompletedTask;
                }));

        Assert.Equal(2, applied);
    }

    [Fact]
    public async Task PendingWorkspaceWaitsBeforeDebouncedRefreshStarts()
    {
        using var state = new FlowTemplateWorkspaceState();
        state.BeginRequest(1, 17);

        Task<FlowTemplateWorkspaceSettlement> wait =
            state.WaitForCurrentSettlementAsync();

        Assert.False(wait.IsCompleted);
        Assert.True(state.TryMarkLoading(1));
        Assert.True(state.TryCompleteLoaded(1, 17));
        FlowTemplateWorkspaceSettlement settlement = await wait;
        Assert.Equal(
            FlowTemplateWorkspaceStatus.Loaded,
            settlement.Status);
        Assert.Equal(17, settlement.TemplateId);
    }

    [Fact]
    public async Task SupersededRequestNeverReturnsOldLoadedIdentity()
    {
        using var state = new FlowTemplateWorkspaceState();
        state.BeginRequest(1, 17);
        Task<FlowTemplateWorkspaceSettlement> wait =
            state.WaitForCurrentSettlementAsync();

        state.BeginRequest(2, 42);
        Assert.False(state.TryCompleteLoaded(1, 17));
        Assert.False(wait.IsCompleted);

        Assert.True(state.TryMarkLoading(2));
        Assert.True(state.TryCompleteLoaded(2, 42));
        FlowTemplateWorkspaceSettlement settlement = await wait;
        Assert.Equal(2, settlement.Generation);
        Assert.Equal(42, settlement.TemplateId);
    }

    [Fact]
    public async Task FailedReplacementBlocksPreviousLoadedSnapshot()
    {
        using var state = new FlowTemplateWorkspaceState();
        state.BeginRequest(1, 17);
        Assert.True(state.TryMarkLoading(1));
        Assert.True(state.TryCompleteLoaded(1, 17));
        Assert.True(state.IsCurrentLoaded(1, 17));

        state.BeginRequest(2, 42);
        Assert.True(state.TryMarkLoading(2));
        Assert.True(state.TryCompleteFailed(2, "invalid canvas"));

        FlowTemplateWorkspaceSettlement settlement =
            await state.WaitForCurrentSettlementAsync();
        Assert.Equal(
            FlowTemplateWorkspaceStatus.Failed,
            settlement.Status);
        Assert.False(state.IsCurrentLoaded(1, 17));
        Assert.False(state.IsCurrentLoaded(2, 42));
    }

    [Fact]
    public async Task DisposingWorkspaceCompletesPendingWaiter()
    {
        var state = new FlowTemplateWorkspaceState();
        state.BeginRequest(1, 17);
        Task<FlowTemplateWorkspaceSettlement> wait =
            state.WaitForCurrentSettlementAsync();

        state.Dispose();

        FlowTemplateWorkspaceSettlement settlement = await wait;
        Assert.Equal(
            FlowTemplateWorkspaceStatus.Disposed,
            settlement.Status);
        Assert.Equal(
            FlowTemplateWorkspaceStatus.Disposed,
            (await state.WaitForCurrentSettlementAsync()).Status);
    }

    [Fact]
    public void RunLifecycleRejectsConcurrentStartAndCancelsExactRun()
    {
        var lifecycle = new FlowRunLifecycleGate();
        using var first = new CancellationTokenSource();
        using var second = new CancellationTokenSource();

        Assert.True(lifecycle.TryBegin("SN-1", first, engineIsRunning: false));
        Assert.False(lifecycle.TryBegin("SN-2", second, engineIsRunning: false));
        Assert.True(lifecycle.CanContinue("SN-1"));
        Assert.False(lifecycle.CanContinue("SN-2"));

        Assert.Same(first, lifecycle.RequestCancellation());
        Assert.False(lifecycle.CanContinue("SN-1"));
        first.Cancel();
        lifecycle.DetachCancellationSource("SN-1", first);
        lifecycle.Complete("SN-2");
        Assert.True(lifecycle.IsActive);

        lifecycle.Complete("SN-1");
        Assert.False(lifecycle.IsActive);
        Assert.True(lifecycle.TryBegin("SN-2", second, engineIsRunning: false));
    }
}
