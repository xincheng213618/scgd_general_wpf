#pragma warning disable CA1707
using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTaskHostTests
{
    [Fact]
    public async Task Host_PausesOnlyAfterCheckpointBoundaryAndClearsCompletedRun()
    {
        var host = new CopilotAgentTaskHost();
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var changes = new List<CopilotAgentTaskHostChangeKind>();
        host.Changed += (_, _) => throw new InvalidOperationException("Subscriber failures must be isolated.");
        host.Changed += (_, args) => changes.Add(args.Kind);

        var run = host.Start("conversation-1", CopilotAgentMode.Auto, _ => release.Task);

        Assert.Same(run, host.ActiveRun);
        Assert.StartsWith("run:", run.Id);
        Assert.False(run.CanRequestPause);
        Assert.True(run.CanRequestCancel);
        Assert.False(host.MarkCheckpointReady("run:stale"));
        Assert.False(host.RequestCancel("run:stale"));
        Assert.False(host.RequestPause(run.Id));
        Assert.True(host.MarkCheckpointReady(run.Id));
        Assert.True(run.CanRequestPause);
        Assert.True(host.RequestPause(run.Id));
        Assert.False(run.CanRequestPause);
        Assert.True(run.CanRequestCancel);
        Assert.True(run.CancellationToken.IsCancellationRequested);
        Assert.Equal(CopilotAgentControlIntent.Pause, run.RunControl?.Intent);

        release.TrySetResult(null);
        await run.Completion;

        Assert.False(host.IsActive);
        Assert.Equal(CopilotHostedRunState.Completed, run.State);
        Assert.False(run.CanRequestPause);
        Assert.False(run.CanRequestCancel);
        Assert.Equal(
            new[]
            {
                CopilotAgentTaskHostChangeKind.Started,
                CopilotAgentTaskHostChangeKind.CheckpointReady,
                CopilotAgentTaskHostChangeKind.ControlRequested,
                CopilotAgentTaskHostChangeKind.Completed,
            },
            changes);
    }

    [Fact]
    public async Task Host_CancelOverridesPauseAndRejectsConcurrentRun()
    {
        var host = new CopilotAgentTaskHost();
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = host.Start("conversation-1", CopilotAgentMode.Auto, _ => release.Task);

        Assert.Throws<InvalidOperationException>(() => host.Start("conversation-2", CopilotAgentMode.Chat, _ => Task.CompletedTask));
        Assert.True(host.MarkCheckpointReady(run.Id));
        Assert.True(host.RequestPause(run.Id));
        Assert.True(host.RequestCancel(run.Id));
        Assert.Equal(CopilotAgentControlIntent.Cancel, run.RunControl?.Intent);
        Assert.Equal(CopilotHostedRunState.CancelRequested, run.State);
        Assert.False(run.CanRequestPause);
        Assert.False(run.CanRequestCancel);

        release.TrySetResult(null);
        await run.Completion;

        Assert.False(host.IsActive);
    }

    [Fact]
    public async Task Host_ContainsThrowingCancellationCallbackAndStillPublishesControlChange()
    {
        var host = new CopilotAgentTaskHost();
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var changes = new List<CopilotAgentTaskHostChangeKind>();
        host.Changed += (_, args) => changes.Add(args.Kind);
        var run = host.Start("conversation-1", CopilotAgentMode.Auto, _ => release.Task);
        using var registration = run.CancellationToken.Register(() => throw new InvalidOperationException("callback failure"));

        Assert.True(host.RequestCancel(run.Id));

        Assert.True(run.CancellationToken.IsCancellationRequested);
        Assert.Equal(CopilotHostedRunState.CancelRequested, run.State);
        Assert.Contains(CopilotAgentTaskHostChangeKind.ControlRequested, changes);
        release.TrySetResult(null);
        await run.Completion;
    }

    [Fact]
    public async Task Host_PropagatesOperationFailureAfterReleasingActiveSlot()
    {
        var host = new CopilotAgentTaskHost();
        var run = host.Start("conversation-1", CopilotAgentMode.Chat, _ => Task.FromException(new InvalidOperationException("boom")));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => run.Completion);

        Assert.Equal("boom", error.Message);
        Assert.False(host.IsActive);
        Assert.Equal(CopilotHostedRunState.Completed, run.State);
    }

    [Fact]
    public async Task Host_SchedulesBoundedRunsInFifoOrder()
    {
        var host = new CopilotAgentTaskHost(maxQueuedRuns: 2);
        var releaseActive = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var executionOrder = new List<string>();
        var active = host.Start("conversation-1", CopilotAgentMode.Auto, _ => releaseActive.Task);

        Assert.True(host.TrySchedule(
            "conversation-2",
            CopilotAgentMode.Auto,
            _ =>
            {
                executionOrder.Add("conversation-2");
                return Task.CompletedTask;
            },
            out var second));
        Assert.True(host.TrySchedule(
            "conversation-3",
            CopilotAgentMode.Diagnose,
            _ =>
            {
                executionOrder.Add("conversation-3");
                return Task.CompletedTask;
            },
            out var third));

        Assert.NotNull(second);
        Assert.NotNull(third);
        Assert.Equal(CopilotHostedRunState.Queued, second.State);
        Assert.Equal(CopilotHostedRunState.Queued, third.State);
        Assert.Equal(2, host.QueuedCount);
        Assert.Equal(1, host.GetQueuePosition(second.Id));
        Assert.Equal(2, host.GetQueuePosition(third.Id));
        Assert.Equal(new[] { second, third }, host.QueuedRuns);
        Assert.Same(third, host.FindRunByConversationId("conversation-3"));

        releaseActive.TrySetResult(null);
        await Task.WhenAll(active.Completion, second.Completion, third.Completion).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(new[] { "conversation-2", "conversation-3" }, executionOrder);
        Assert.True(second.HasStarted);
        Assert.True(third.HasStarted);
        Assert.False(host.IsActive);
        Assert.Equal(0, host.QueuedCount);
    }

    [Fact]
    public async Task Host_RejectsDuplicateConversationAcrossActiveAndQueuedRuns()
    {
        var host = new CopilotAgentTaskHost(maxQueuedRuns: 2);
        var releaseActive = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = host.Start("conversation-1", CopilotAgentMode.Auto, _ => releaseActive.Task);

        Assert.False(host.TrySchedule("conversation-1", CopilotAgentMode.Auto, _ => Task.CompletedTask, out var duplicateActive));
        Assert.Null(duplicateActive);
        Assert.True(host.TrySchedule("conversation-2", CopilotAgentMode.Auto, _ => Task.CompletedTask, out var queued));
        Assert.False(host.TrySchedule("conversation-2", CopilotAgentMode.Diagnose, _ => Task.CompletedTask, out var duplicateQueued));
        Assert.Null(duplicateQueued);

        releaseActive.TrySetResult(null);
        await Task.WhenAll(active.Completion, queued!.Completion).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Host_CancelsQueuedRunWithoutStartingAndReleasesCapacity()
    {
        var host = new CopilotAgentTaskHost(maxQueuedRuns: 1);
        var releaseActive = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queuedExecutionCount = 0;
        var active = host.Start("conversation-1", CopilotAgentMode.Auto, _ => releaseActive.Task);

        Assert.True(host.TrySchedule(
            "conversation-2",
            CopilotAgentMode.Auto,
            _ =>
            {
                queuedExecutionCount++;
                return Task.CompletedTask;
            },
            out var queued));
        Assert.False(host.TrySchedule("conversation-full", CopilotAgentMode.Auto, _ => Task.CompletedTask, out var rejected));
        Assert.Null(rejected);

        Assert.NotNull(queued);
        Assert.True(host.RequestCancel(queued.Id));
        await queued.Completion;

        Assert.False(queued.HasStarted);
        Assert.Equal(CopilotAgentControlIntent.Cancel, queued.RunControl?.Intent);
        Assert.Equal(CopilotHostedRunState.Completed, queued.State);
        Assert.Equal(0, queuedExecutionCount);
        Assert.Equal(0, host.QueuedCount);
        Assert.True(host.TrySchedule("conversation-3", CopilotAgentMode.Auto, _ => Task.CompletedTask, out var replacement));

        releaseActive.TrySetResult(null);
        await Task.WhenAll(active.Completion, replacement!.Completion).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Host_PromotesNextRunAfterQueuedOperationFails()
    {
        var host = new CopilotAgentTaskHost(maxQueuedRuns: 2);
        var releaseActive = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var active = host.Start("conversation-1", CopilotAgentMode.Auto, _ => releaseActive.Task);
        Assert.True(host.TrySchedule(
            "conversation-2",
            CopilotAgentMode.Auto,
            _ => Task.FromException(new InvalidOperationException("queued failure")),
            out var failed));
        Assert.True(host.TrySchedule("conversation-3", CopilotAgentMode.Auto, _ => Task.CompletedTask, out var successor));

        releaseActive.TrySetResult(null);
        await active.Completion;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => failed!.Completion);
        await successor!.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal("queued failure", error.Message);
        Assert.True(successor.HasStarted);
        Assert.False(host.IsActive);
    }
}
