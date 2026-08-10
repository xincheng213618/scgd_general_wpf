using ColorVision.Copilot;
using ColorVision.FloatingBall;

namespace ColorVision.Copilot.Tests;

public sealed class DesktopPetCopilotActivityTrackerTests
{
    [Fact]
    public void ActivitiesFollowCodexNeedsInputBlockedReadyRunningPriority()
    {
        var tracker = new DesktopPetCopilotActivityTracker();
        var now = DateTimeOffset.Parse("2026-07-25T12:00:00Z");
        tracker.RecordCompletion("ready-chat", CopilotConversationActivityState.Ready, now.AddMinutes(1));
        tracker.RecordCompletion("blocked-chat", CopilotConversationActivityState.Blocked, now);
        tracker.ReconcileActive("running-chat", needsInput: false, now.AddMinutes(2));

        Assert.Equal(
            ["blocked-chat", "ready-chat", "running-chat"],
            tracker.Snapshot().Select(activity => activity.ConversationId));

        tracker.ReconcileActive("running-chat", needsInput: true, now.AddMinutes(3));

        Assert.Equal(
            ["running-chat", "blocked-chat", "ready-chat"],
            tracker.Snapshot().Select(activity => activity.ConversationId));
        Assert.Equal(DesktopPetActivityState.Waiting, tracker.Snapshot()[0].PetState);
    }

    [Fact]
    public void CancellingAQueuedConversationDoesNotReplaceTheActiveConversation()
    {
        var tracker = new DesktopPetCopilotActivityTracker();
        tracker.ReconcileActive("active-chat", needsInput: false);

        tracker.RecordCompletion("cancelled-queued-chat", CopilotConversationActivityState.None);
        tracker.ReconcileActive("active-chat", needsInput: false);

        var primary = Assert.Single(tracker.Snapshot());
        Assert.Equal("active-chat", primary.ConversationId);
        Assert.Equal(DesktopPetCopilotActivityKind.Running, primary.Kind);
    }

    [Fact]
    public void OpeningATerminalActivityRevealsTheRunningConversation()
    {
        var tracker = new DesktopPetCopilotActivityTracker();
        tracker.ReconcileActive("running-chat", needsInput: false);
        tracker.RecordCompletion("ready-chat", CopilotConversationActivityState.Ready);

        Assert.Equal("ready-chat", tracker.Snapshot()[0].ConversationId);
        Assert.True(tracker.MarkViewed("ready-chat"));

        var remaining = Assert.Single(tracker.Snapshot());
        Assert.Equal("running-chat", remaining.ConversationId);
        Assert.False(tracker.MarkViewed("running-chat"));
    }

    [Fact]
    public void PausedConversationRemainsVisibleWithoutAnActiveRun()
    {
        var tracker = new DesktopPetCopilotActivityTracker();
        tracker.RecordCompletion("paused-chat", CopilotConversationActivityState.NeedsInput);

        tracker.ReconcileActive(null, needsInput: false);

        var activity = Assert.Single(tracker.Snapshot());
        Assert.Equal(DesktopPetCopilotActivityKind.NeedsInput, activity.Kind);
        Assert.Equal("需要输入", activity.StatusLabel);
    }

    [Theory]
    [InlineData(false, false, CopilotAgentControlIntent.None, CopilotConversationActivityState.Ready)]
    [InlineData(true, false, CopilotAgentControlIntent.None, CopilotConversationActivityState.Blocked)]
    [InlineData(false, true, CopilotAgentControlIntent.Cancel, CopilotConversationActivityState.None)]
    [InlineData(false, true, CopilotAgentControlIntent.Pause, CopilotConversationActivityState.NeedsInput)]
    public void CompletedRunMapsToPetActivity(
        bool faulted,
        bool cancelled,
        CopilotAgentControlIntent controlIntent,
        CopilotConversationActivityState expected)
    {
        var run = new CopilotHostedAgentRun("conversation", CopilotAgentMode.Auto);
        Assert.True(run.TryStart());
        if (controlIntent == CopilotAgentControlIntent.Pause)
        {
            Assert.True(run.TryMarkCheckpointReady());
            Assert.True(run.TryRequestPause());
        }
        else if (controlIntent == CopilotAgentControlIntent.Cancel)
        {
            Assert.True(run.TryRequestCancel());
        }

        run.Complete(faulted ? new InvalidOperationException("boom") : null);
        if (cancelled)
            Assert.True(run.Completion.IsCanceled);

        Assert.Equal(
            expected,
            CopilotAgentRunActivityPolicy.ResolveCompletionState(run));
    }

    [Theory]
    [InlineData(CopilotAgentStopReason.None, CopilotConversationActivityState.Ready)]
    [InlineData(CopilotAgentStopReason.Completed, CopilotConversationActivityState.Ready)]
    [InlineData(CopilotAgentStopReason.AwaitingUser, CopilotConversationActivityState.NeedsInput)]
    [InlineData(CopilotAgentStopReason.Paused, CopilotConversationActivityState.NeedsInput)]
    [InlineData(CopilotAgentStopReason.Cancelled, CopilotConversationActivityState.None)]
    [InlineData(CopilotAgentStopReason.ApprovalDenied, CopilotConversationActivityState.Blocked)]
    [InlineData(CopilotAgentStopReason.BudgetExhausted, CopilotConversationActivityState.Blocked)]
    [InlineData(CopilotAgentStopReason.TaskPassLimit, CopilotConversationActivityState.Blocked)]
    [InlineData(CopilotAgentStopReason.Blocked, CopilotConversationActivityState.Blocked)]
    [InlineData(CopilotAgentStopReason.IncompleteOutput, CopilotConversationActivityState.Blocked)]
    [InlineData(CopilotAgentStopReason.ProviderFailure, CopilotConversationActivityState.Blocked)]
    [InlineData(CopilotAgentStopReason.Interrupted, CopilotConversationActivityState.Blocked)]
    public void StructuredAgentStopReasonMapsToPetActivity(
        CopilotAgentStopReason stopReason,
        CopilotConversationActivityState expected)
    {
        var run = new CopilotHostedAgentRun("conversation", CopilotAgentMode.Auto);
        Assert.True(run.TryStart());

        run.SetAgentStopReason(stopReason);
        run.Complete(error: null);

        Assert.Equal(stopReason, run.AgentStopReason);
        Assert.Equal(
            expected,
            CopilotAgentRunActivityPolicy.ResolveCompletionState(run));
    }

    [Fact]
    public void InterruptedFinalAnswerIsBlockedEvenWhenTheRunReportedCompleted()
    {
        var run = new CopilotHostedAgentRun("conversation", CopilotAgentMode.Auto);
        Assert.True(run.TryStart());
        run.SetAgentStopReason(CopilotAgentStopReason.Completed);
        run.Complete(error: null);
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial result")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
            WasResponseInterrupted = true,
        };

        Assert.True(assistant.HasRecoverableFinalAnswer);
        Assert.Equal(
            CopilotConversationActivityState.Blocked,
            CopilotAgentRunActivityPolicy.ResolveCompletionState(run, assistant));
    }
}
