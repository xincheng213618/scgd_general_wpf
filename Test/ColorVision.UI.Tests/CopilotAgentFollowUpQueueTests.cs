using ColorVision.Copilot;
using System.Collections.Concurrent;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentFollowUpQueueTests
{
    [Fact]
    public async Task SameConversationFollowUpStartsOnlyAfterActiveRunCompletes()
    {
        var host = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new ConcurrentQueue<string>();
        var activeRun = host.Start("conversation", CopilotAgentMode.Auto, async _ =>
        {
            events.Enqueue("active-start");
            activeStarted.SetResult();
            await releaseActive.Task;
            events.Enqueue("active-end");
        });
        await activeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(host.TryScheduleFollowUp(
            "conversation",
            CopilotAgentMode.Auto,
            _ =>
            {
                events.Enqueue("follow-up-start");
                return Task.CompletedTask;
            },
            out var followUp,
            out var admission));
        Assert.True(admission.IsAllowed);
        Assert.NotNull(followUp);
        Assert.Equal(CopilotHostedRunState.Queued, followUp.State);
        Assert.Equal(["active-start"], events);

        releaseActive.SetResult();
        await activeRun.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        await followUp.Completion.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(["active-start", "active-end", "follow-up-start"], events);
    }

    [Fact]
    public async Task NormalSchedulingStillRejectsDuplicateConversation()
    {
        var host = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeRun = host.Start("conversation", CopilotAgentMode.Auto, async _ =>
        {
            activeStarted.SetResult();
            await releaseActive.Task;
        });
        await activeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(host.TrySchedule(
            "conversation",
            CopilotAgentMode.Auto,
            _ => Task.CompletedTask,
            out var duplicate,
            out var admission));
        Assert.Null(duplicate);
        Assert.Equal(CopilotRequestAdmissionReason.ConversationAlreadyScheduled, admission.Reason);

        releaseActive.SetResult();
        await activeRun.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FollowUpsCanBeReorderedWithoutStartingEarly()
    {
        var host = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeRun = host.Start("conversation", CopilotAgentMode.Auto, async _ =>
        {
            activeStarted.SetResult();
            await releaseActive.Task;
        });
        await activeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(host.TryScheduleFollowUp("conversation", CopilotAgentMode.Auto, _ => Task.CompletedTask, out var first, out _));
        Assert.True(host.TryScheduleFollowUp("conversation", CopilotAgentMode.Auto, _ => Task.CompletedTask, out var second, out _));
        Assert.True(host.TryScheduleFollowUp("conversation", CopilotAgentMode.Auto, _ => Task.CompletedTask, out var third, out _));
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotNull(third);

        Assert.True(host.MoveQueuedRun(third.Id, -1));
        Assert.Equal([first.Id, third.Id, second.Id], host.QueuedRuns.Select(run => run.Id));
        Assert.Equal(2, host.GetQueuePosition(third.Id));
        Assert.Equal(CopilotHostedRunState.Running, activeRun.State);

        releaseActive.SetResult();
        await Task.WhenAll(
            activeRun.Completion,
            first.Completion,
            second.Completion,
            third.Completion).WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task CancellingQueuedFollowUpNeverInvokesItsOperation()
    {
        var host = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        var activeRun = host.Start("conversation", CopilotAgentMode.Auto, async _ =>
        {
            activeStarted.SetResult();
            await releaseActive.Task;
        });
        await activeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(host.TryScheduleFollowUp(
            "conversation",
            CopilotAgentMode.Auto,
            _ =>
            {
                Interlocked.Increment(ref invocationCount);
                return Task.CompletedTask;
            },
            out var followUp,
            out _));
        Assert.NotNull(followUp);
        Assert.True(host.RequestCancel(followUp.Id));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => followUp.Completion.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(0, Volatile.Read(ref invocationCount));
        Assert.Equal(CopilotHostedRunState.Running, activeRun.State);

        releaseActive.SetResult();
        await activeRun.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("other-conversation", CopilotAgentMode.Auto, "FollowUpConversationMismatch")]
    [InlineData("conversation", CopilotAgentMode.Chat, "ChatCannotQueue")]
    public async Task FollowUpAdmissionFailsClosed(
        string conversationId,
        CopilotAgentMode mode,
        string expectedReason)
    {
        var host = new CopilotAgentTaskHost();
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeRun = host.Start("conversation", CopilotAgentMode.Auto, async _ =>
        {
            activeStarted.SetResult();
            await releaseActive.Task;
        });
        await activeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(
            expectedReason,
            host.EvaluateFollowUpAdmission(conversationId, mode).Reason.ToString());
        Assert.False(host.TryScheduleFollowUp(
            conversationId,
            mode,
            _ => Task.CompletedTask,
            out var followUp,
            out var admission));
        Assert.Null(followUp);
        Assert.Equal(expectedReason, admission.Reason.ToString());

        releaseActive.SetResult();
        await activeRun.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task FollowUpPreflightReportsQueueCapacityBeforeScheduling()
    {
        var host = new CopilotAgentTaskHost(maxQueuedRuns: 1);
        var activeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseActive = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var activeRun = host.Start("conversation", CopilotAgentMode.Auto, async _ =>
        {
            activeStarted.SetResult();
            await releaseActive.Task;
        });
        await activeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(
            CopilotRequestAdmissionReason.Allowed,
            host.EvaluateFollowUpAdmission("conversation", CopilotAgentMode.Auto).Reason);
        Assert.True(host.TryScheduleFollowUp(
            "conversation",
            CopilotAgentMode.Auto,
            _ => Task.CompletedTask,
            out var queued,
            out _));
        Assert.NotNull(queued);
        Assert.Equal(
            CopilotRequestAdmissionReason.QueueFull,
            host.EvaluateFollowUpAdmission("conversation", CopilotAgentMode.Auto).Reason);

        releaseActive.SetResult();
        await Task.WhenAll(activeRun.Completion, queued.Completion)
            .WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void FollowUpPreflightRejectsMissingConversationWithoutAnActiveRun()
    {
        var host = new CopilotAgentTaskHost();

        Assert.Equal(
            CopilotRequestAdmissionReason.MissingConversation,
            host.EvaluateFollowUpAdmission(string.Empty, CopilotAgentMode.Auto).Reason);
        Assert.Equal(
            CopilotRequestAdmissionReason.NoActiveRun,
            host.EvaluateFollowUpAdmission("conversation", CopilotAgentMode.Auto).Reason);
    }
}
