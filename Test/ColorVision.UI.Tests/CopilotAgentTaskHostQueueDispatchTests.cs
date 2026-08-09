using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTaskHostQueueDispatchTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task CompletedTurnAutomaticallyDispatchesQueuedFollowUp()
    {
        var host = new CopilotAgentTaskHost();
        var activeStarted = NewSignal();
        var releaseActive = NewSignal();
        var secondFollowUpStarted = NewSignal();
        var executionOrder = new List<int>();
        var activeRun = host.Start(
            "conversation",
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        await activeStarted.Task.WaitAsync(TestTimeout);

        Assert.True(host.TryScheduleFollowUp(
            "conversation",
            CopilotAgentMode.Auto,
            _ =>
            {
                executionOrder.Add(1);
                return Task.CompletedTask;
            },
            out var followUpRun,
            out _));
        Assert.True(host.TryScheduleFollowUp(
            "conversation",
            CopilotAgentMode.Auto,
            _ =>
            {
                executionOrder.Add(2);
                secondFollowUpStarted.TrySetResult();
                return Task.CompletedTask;
            },
            out var secondFollowUpRun,
            out _));

        releaseActive.TrySetResult();

        await secondFollowUpStarted.Task.WaitAsync(TestTimeout);
        await activeRun.Completion.WaitAsync(TestTimeout);
        await followUpRun!.Completion.WaitAsync(TestTimeout);
        await secondFollowUpRun!.Completion.WaitAsync(TestTimeout);
        Assert.True(followUpRun.HasStarted);
        Assert.Equal([1, 2], executionOrder);
    }

    [Fact]
    public async Task ReportedFailureLeavesFollowUpQueuedUntilExplicitlyStarted()
    {
        var host = new CopilotAgentTaskHost();
        var activeStarted = NewSignal();
        var releaseActive = NewSignal();
        var followUpStarted = NewSignal();
        var activeRun = host.Start(
            "conversation",
            CopilotAgentMode.Auto,
            async run =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
                run.SuppressAutomaticFollowUpDispatch();
            });
        await activeStarted.Task.WaitAsync(TestTimeout);

        Assert.True(host.TryScheduleFollowUp(
            "conversation",
            CopilotAgentMode.Auto,
            _ =>
            {
                followUpStarted.TrySetResult();
                return Task.CompletedTask;
            },
            out var followUpRun,
            out _));

        releaseActive.TrySetResult();
        await activeRun.Completion.WaitAsync(TestTimeout);

        Assert.False(followUpStarted.Task.IsCompleted);
        Assert.Null(host.ActiveRun);
        Assert.Equal(1, host.GetQueuePosition(followUpRun!.Id));
        Assert.True(host.TryStartQueuedRun(followUpRun.Id));
        await followUpStarted.Task.WaitAsync(TestTimeout);
        await followUpRun.Completion.WaitAsync(TestTimeout);
    }

    [Fact]
    public async Task ThrownFailureLeavesFollowUpQueued()
    {
        var host = new CopilotAgentTaskHost();
        var activeStarted = NewSignal();
        var releaseActive = NewSignal();
        var followUpStarted = NewSignal();
        var activeRun = host.Start(
            "conversation",
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
                throw new InvalidOperationException("failed turn");
            });
        await activeStarted.Task.WaitAsync(TestTimeout);

        Assert.True(host.TryScheduleFollowUp(
            "conversation",
            CopilotAgentMode.Auto,
            _ =>
            {
                followUpStarted.TrySetResult();
                return Task.CompletedTask;
            },
            out var followUpRun,
            out _));

        releaseActive.TrySetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await activeRun.Completion.WaitAsync(TestTimeout));

        Assert.False(followUpStarted.Task.IsCompleted);
        Assert.Equal(1, host.GetQueuePosition(followUpRun!.Id));
        Assert.True(host.RequestCancel(followUpRun.Id));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await followUpRun.Completion.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task BlockedFollowUpDoesNotHoldIndependentConversationQueue()
    {
        var host = new CopilotAgentTaskHost();
        var activeStarted = NewSignal();
        var releaseActive = NewSignal();
        var followUpStarted = NewSignal();
        var independentStarted = NewSignal();
        var activeRun = host.Start(
            "conversation",
            CopilotAgentMode.Auto,
            async run =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
                run.SuppressAutomaticFollowUpDispatch();
            });
        await activeStarted.Task.WaitAsync(TestTimeout);

        Assert.True(host.TryScheduleFollowUp(
            "conversation",
            CopilotAgentMode.Auto,
            _ =>
            {
                followUpStarted.TrySetResult();
                return Task.CompletedTask;
            },
            out var followUpRun,
            out _));
        Assert.True(host.TrySchedule(
            "independent-conversation",
            CopilotAgentMode.Auto,
            _ =>
            {
                independentStarted.TrySetResult();
                return Task.CompletedTask;
            },
            out var independentRun));

        releaseActive.TrySetResult();
        await activeRun.Completion.WaitAsync(TestTimeout);
        await independentStarted.Task.WaitAsync(TestTimeout);
        await independentRun!.Completion.WaitAsync(TestTimeout);

        Assert.False(followUpStarted.Task.IsCompleted);
        Assert.Equal(1, host.GetQueuePosition(followUpRun!.Id));
        Assert.True(host.RequestCancel(followUpRun.Id));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await followUpRun.Completion.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task ImmediateFollowUpStillStartsAfterCancellingActiveTurn()
    {
        var host = new CopilotAgentTaskHost();
        var activeStarted = NewSignal();
        var followUpStarted = NewSignal();
        var activeRun = host.Start(
            "conversation",
            CopilotAgentMode.Auto,
            async run =>
            {
                activeStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, run.CancellationToken);
            });
        await activeStarted.Task.WaitAsync(TestTimeout);

        Assert.True(host.TryScheduleFollowUpNext(
            "conversation",
            CopilotAgentMode.Auto,
            _ =>
            {
                followUpStarted.TrySetResult();
                return Task.CompletedTask;
            },
            out var followUpRun,
            out _));
        Assert.True(host.RequestCancel(activeRun.Id));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await activeRun.Completion.WaitAsync(TestTimeout));
        await followUpStarted.Task.WaitAsync(TestTimeout);
        await followUpRun!.Completion.WaitAsync(TestTimeout);
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
