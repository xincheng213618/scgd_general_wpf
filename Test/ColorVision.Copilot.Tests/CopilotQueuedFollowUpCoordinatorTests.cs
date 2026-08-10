using ColorVision.Copilot;
using Newtonsoft.Json.Linq;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotQueuedFollowUpCoordinatorTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task NewFollowUpCreatesOneDurableRecoveryRecord()
    {
        var state = new CopilotChatState();
        var busyHost = await StartBusyHostAsync("conversation-1");
        var queue = new CopilotQueuedFollowUpCoordinator(state, busyHost.Host);
        ForwardHostChanges(busyHost.Host, queue);
        CopilotQueuedFollowUp? queuedItem = null;

        try
        {
            var request = CreateRequest("conversation-1", "continue once");

            Assert.True(queue.TrySchedule(
                request,
                runNext: false,
                static (_, _) => Task.CompletedTask,
                out queuedItem,
                out var admission));
            Assert.True(admission.IsAllowed);

            Assert.Same(queuedItem, Assert.Single(queue.Items));
            var recovery = Assert.Single(state.QueuedFollowUpRecoveries);
            Assert.Equal(queuedItem!.RunId, recovery.RunId);
            Assert.Equal(request.ConversationId, recovery.ConversationId);
            Assert.Equal(request.Prompt, Assert.IsType<CopilotComposerStash>(recovery.ComposerState).Text);
            Assert.Equal(request.Profile.Id, recovery.ProfileId);
            Assert.True(recovery.ResumeAfterRestart);

            Assert.False(queue.PreserveForRestart());
            Assert.Single(state.QueuedFollowUpRecoveries);
        }
        finally
        {
            if (queuedItem != null)
                busyHost.Host.RequestCancel(queuedItem.RunId);
            await busyHost.CompleteAsync();
        }
    }

    [Fact]
    public async Task AutomaticGoalContinuationPersistsItsGoalIdentity()
    {
        var state = new CopilotChatState();
        var busyHost = await StartBusyHostAsync("conversation-1");
        var queue = new CopilotQueuedFollowUpCoordinator(state, busyHost.Host);
        ForwardHostChanges(busyHost.Host, queue);
        CopilotQueuedFollowUp? queuedItem = null;

        try
        {
            Assert.True(queue.TrySchedule(
                CreateRequest("conversation-1", "continue goal", goalId: "goal-1"),
                runNext: false,
                static (_, _) => Task.CompletedTask,
                out queuedItem,
                out _));

            var recovery = Assert.Single(state.QueuedFollowUpRecoveries);
            var recoveryDocument = JObject.FromObject(recovery);
            Assert.Equal("goal-1", recoveryDocument[nameof(CopilotQueuedFollowUp.GoalId)]?.Value<string>());
            Assert.False(recovery.ResumeAfterRestart);
        }
        finally
        {
            if (queuedItem != null)
                busyHost.Host.RequestCancel(queuedItem.RunId);
            await busyHost.CompleteAsync();
        }
    }

    [Fact]
    public async Task GoalContinuationLookupMatchesGoalIdentityAndKeepsUserFollowUpsEligible()
    {
        var state = new CopilotChatState();
        var busyHost = await StartBusyHostAsync("conversation-1");
        var queue = new CopilotQueuedFollowUpCoordinator(state, busyHost.Host);
        ForwardHostChanges(busyHost.Host, queue);

        try
        {
            Assert.True(queue.TrySchedule(
                CreateRequest("conversation-1", "continue old goal", goalId: "goal-old"),
                runNext: false,
                static (_, _) => Task.CompletedTask,
                out _,
                out _));

            Assert.True(queue.HasContinuationForGoal("conversation-1", "goal-old"));
            Assert.False(queue.HasContinuationForGoal("conversation-1", "goal-new"));

            Assert.True(queue.TrySchedule(
                CreateRequest("conversation-1", "user queued follow-up"),
                runNext: false,
                static (_, _) => Task.CompletedTask,
                out _,
                out _));

            Assert.True(queue.HasContinuationForGoal("conversation-1", "goal-new"));
        }
        finally
        {
            foreach (var queuedItem in queue.Items.ToArray())
                busyHost.Host.RequestCancel(queuedItem.RunId);
            await busyHost.CompleteAsync();
        }
    }

    [Fact]
    public async Task CancellingAutomaticGoalContinuationsPreservesUserAndOtherConversationWork()
    {
        var state = new CopilotChatState();
        var busyHost = await StartBusyHostAsync("conversation-1");
        var queue = new CopilotQueuedFollowUpCoordinator(state, busyHost.Host);
        ForwardHostChanges(busyHost.Host, queue);

        try
        {
            Assert.True(queue.TrySchedule(
                CreateRequest("conversation-1", "automatic target", goalId: "goal-1"),
                runNext: false,
                static (_, _) => Task.CompletedTask,
                out var automaticTarget,
                out _));
            Assert.True(queue.TrySchedule(
                CreateRequest("conversation-1", "user target"),
                runNext: false,
                static (_, _) => Task.CompletedTask,
                out var userTarget,
                out _));
            var automaticOther = CreateRequest(
                "conversation-2",
                "automatic other",
                goalId: "goal-2").Create("restored-run-other");
            var otherRecovery = CreateRecovery(automaticOther.RunId, automaticOther.ConversationId);
            otherRecovery.GoalId = automaticOther.GoalId;
            state.QueuedFollowUpRecoveries.Add(otherRecovery);
            Assert.True(queue.TryRestore(
                automaticOther,
                static (_, _) => Task.CompletedTask));

            Assert.Equal(1, queue.CancelAutomaticGoalContinuations("conversation-1"));

            Assert.Equal(
                [userTarget!.RunId, automaticOther.RunId],
                queue.Items.Select(item => item.RunId));
            Assert.Equal(
                [userTarget.RunId, automaticOther.RunId],
                state.QueuedFollowUpRecoveries.Select(record => record.RunId));
            Assert.DoesNotContain(queue.Items, item => item.RunId == automaticTarget!.RunId);
        }
        finally
        {
            foreach (var queuedItem in queue.Items.ToArray())
                busyHost.Host.RequestCancel(queuedItem.RunId);
            await busyHost.CompleteAsync();
        }
    }

    [Fact]
    public async Task MovingFollowUpKeepsHostProjectionAndRecoveryInTheSameOrder()
    {
        var state = new CopilotChatState();
        var busyHost = await StartBusyHostAsync("conversation-1");
        var queue = new CopilotQueuedFollowUpCoordinator(state, busyHost.Host);
        ForwardHostChanges(busyHost.Host, queue);
        CopilotQueuedFollowUp? first = null;
        CopilotQueuedFollowUp? second = null;

        try
        {
            Assert.True(queue.TrySchedule(
                CreateRequest("conversation-1", "first"),
                runNext: false,
                static (_, _) => Task.CompletedTask,
                out first,
                out _));
            Assert.True(queue.TrySchedule(
                CreateRequest("conversation-1", "second"),
                runNext: false,
                static (_, _) => Task.CompletedTask,
                out second,
                out _));

            Assert.True(queue.TryMove(second!.RunId, offset: -1));

            var expectedOrder = new[] { second.RunId, first!.RunId };
            Assert.Equal(expectedOrder, busyHost.Host.QueuedRuns.Select(run => run.Id));
            Assert.Equal(expectedOrder, queue.Items.Select(item => item.RunId));
            Assert.Equal(expectedOrder, state.QueuedFollowUpRecoveries.Select(record => record.RunId));
            Assert.Equal([1, 2], queue.Items.Select(item => item.QueuePosition));
        }
        finally
        {
            if (first != null)
                busyHost.Host.RequestCancel(first.RunId);
            if (second != null)
                busyHost.Host.RequestCancel(second.RunId);
            await busyHost.CompleteAsync();
        }
    }

    [Fact]
    public async Task StartedFollowUpLeavesRecoveryUntilExecutionCommitsIt()
    {
        var state = new CopilotChatState();
        var host = new CopilotAgentTaskHost();
        var queue = new CopilotQueuedFollowUpCoordinator(state, host);
        var activeStarted = NewSignal();
        var releaseActive = NewSignal();
        var followUpStarted = NewSignal();
        var releaseFollowUp = NewSignal();
        CopilotHostedAgentRun? followUpRun = null;

        ForwardHostChanges(host, queue);
        var activeRun = host.Start(
            "conversation-1",
            CopilotAgentMode.Auto,
            async _ =>
            {
                activeStarted.TrySetResult();
                await releaseActive.Task;
            });
        await activeStarted.Task.WaitAsync(TestTimeout);

        Assert.True(queue.TrySchedule(
            CreateRequest("conversation-1", "start later"),
            runNext: false,
            async (run, _) =>
            {
                followUpRun = run;
                followUpStarted.TrySetResult();
                await releaseFollowUp.Task;
            },
            out var queuedItem,
            out _));

        try
        {
            releaseActive.TrySetResult();
            await followUpStarted.Task.WaitAsync(TestTimeout);
            await activeRun.Completion.WaitAsync(TestTimeout);

            Assert.Empty(queue.Items);
            Assert.False(queue.TryGet(queuedItem!.RunId, out _));
            Assert.Equal(queuedItem.RunId, Assert.Single(state.QueuedFollowUpRecoveries).RunId);
        }
        finally
        {
            releaseActive.TrySetResult();
            releaseFollowUp.TrySetResult();
            if (!activeRun.Completion.IsCompleted)
                await activeRun.Completion.WaitAsync(TestTimeout);
            if (followUpRun != null)
                await followUpRun.Completion.WaitAsync(TestTimeout);
        }
    }

    [Fact]
    public async Task ShutdownCompletionRemovesProjectionButPreservesRecovery()
    {
        var state = new CopilotChatState();
        var host = new CopilotAgentTaskHost();
        var queue = new CopilotQueuedFollowUpCoordinator(state, host);
        var activeStarted = NewSignal();
        ForwardHostChanges(host, queue);
        var activeRun = host.Start(
            "conversation-1",
            CopilotAgentMode.Auto,
            async run =>
            {
                activeStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, run.CancellationToken);
            });
        await activeStarted.Task.WaitAsync(TestTimeout);

        Assert.True(queue.TrySchedule(
            CreateRequest("conversation-1", "resume after restart"),
            runNext: false,
            static (_, _) => Task.CompletedTask,
            out var queuedItem,
            out _));
        var queuedRun = Assert.Single(host.QueuedRuns);

        queue.BeginShutdown();
        host.Shutdown();

        Assert.Empty(queue.Items);
        Assert.Equal(queuedItem!.RunId, Assert.Single(state.QueuedFollowUpRecoveries).RunId);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await queuedRun.Completion.WaitAsync(TestTimeout));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await activeRun.Completion.WaitAsync(TestTimeout));
    }

    [Fact]
    public void RemovingConversationRecoveriesDoesNotAffectOtherConversations()
    {
        var retained = CreateRecovery("run-b", "conversation-b");
        var state = new CopilotChatState
        {
            QueuedFollowUpRecoveries =
            [
                CreateRecovery("run-a-1", "conversation-a"),
                retained,
                CreateRecovery("run-a-2", "conversation-a"),
            ],
        };
        var queue = new CopilotQueuedFollowUpCoordinator(state, new CopilotAgentTaskHost());

        Assert.True(queue.RemoveRecoveriesForConversation("conversation-a"));

        Assert.Same(retained, Assert.Single(state.QueuedFollowUpRecoveries));
        Assert.False(queue.RemoveRecoveriesForConversation("conversation-a"));
    }

    private static CopilotQueuedFollowUpRequest CreateRequest(
        string conversationId,
        string prompt,
        string goalId = "") => new(
        conversationId,
        "Conversation",
        prompt,
        CopilotAgentMode.Auto,
        CopilotProfileConfig.CreateDefault(),
        new CopilotAgentHostContextSnapshot("", "", []),
        AgentSkillReference: null,
        new CopilotTurnRuntimeConfigSnapshot(new CopilotAgentDefaultsConfig(), []),
        WorkspaceReviewTarget: null,
        GoalId: goalId);

    private static CopilotQueuedFollowUpRecoveryRecord CreateRecovery(
        string runId,
        string conversationId) => new()
        {
            RunId = runId,
            ConversationId = conversationId,
            ComposerState = CopilotComposerStash.Capture(
                "prompt",
                "prompt".Length,
                CopilotAgentMode.Auto,
                []),
        };

    private static async Task<BusyHost> StartBusyHostAsync(string conversationId)
    {
        var host = new CopilotAgentTaskHost();
        var started = NewSignal();
        var release = NewSignal();
        var activeRun = host.Start(
            conversationId,
            CopilotAgentMode.Auto,
            async _ =>
            {
                started.TrySetResult();
                await release.Task;
            });
        await started.Task.WaitAsync(TestTimeout);
        return new BusyHost(host, activeRun, release);
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static void ForwardHostChanges(
        CopilotAgentTaskHost host,
        CopilotQueuedFollowUpCoordinator queue)
    {
        host.Changed += (_, args) => queue.HandleTaskHostChanged(args);
    }

    private sealed record BusyHost(
        CopilotAgentTaskHost Host,
        CopilotHostedAgentRun ActiveRun,
        TaskCompletionSource Release)
    {
        public async Task CompleteAsync()
        {
            Release.TrySetResult();
            await ActiveRun.Completion.WaitAsync(TestTimeout);
        }
    }
}
