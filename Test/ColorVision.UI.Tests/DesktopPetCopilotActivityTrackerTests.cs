using ColorVision.Copilot;
using ColorVision.FloatingBall;

namespace ColorVision.UI.Tests;

public sealed class DesktopPetCopilotActivityTrackerTests
{
    [Fact]
    public void ActivitiesFollowCodexNeedsInputBlockedReadyRunningPriority()
    {
        var tracker = new DesktopPetCopilotActivityTracker();
        var now = DateTimeOffset.Parse("2026-07-25T12:00:00Z");
        tracker.RecordCompletion("ready-chat", DesktopPetCopilotCompletionKind.Ready, now.AddMinutes(1));
        tracker.RecordCompletion("blocked-chat", DesktopPetCopilotCompletionKind.Blocked, now);
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

        tracker.RecordCompletion("cancelled-queued-chat", DesktopPetCopilotCompletionKind.Cancelled);
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
        tracker.RecordCompletion("ready-chat", DesktopPetCopilotCompletionKind.Ready);

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
        tracker.RecordCompletion("paused-chat", DesktopPetCopilotCompletionKind.Paused);

        tracker.ReconcileActive(null, needsInput: false);

        var activity = Assert.Single(tracker.Snapshot());
        Assert.Equal(DesktopPetCopilotActivityKind.NeedsInput, activity.Kind);
        Assert.Equal("需要输入", activity.StatusLabel);
    }

    [Theory]
    [InlineData(false, false, CopilotAgentControlIntent.None, 0)]
    [InlineData(true, false, CopilotAgentControlIntent.None, 1)]
    [InlineData(false, true, CopilotAgentControlIntent.Cancel, 3)]
    [InlineData(false, true, CopilotAgentControlIntent.Pause, 2)]
    public void CompletedRunMapsToPetActivity(
        bool faulted,
        bool cancelled,
        CopilotAgentControlIntent controlIntent,
        int expectedValue)
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
            (DesktopPetCopilotCompletionKind)expectedValue,
            DesktopPetCopilotBridge.ResolveCompletionKind(run));
    }

    [Theory]
    [InlineData(CopilotAgentStopReason.None, DesktopPetCopilotCompletionKind.Ready)]
    [InlineData(CopilotAgentStopReason.Completed, DesktopPetCopilotCompletionKind.Ready)]
    [InlineData(CopilotAgentStopReason.AwaitingUser, DesktopPetCopilotCompletionKind.Paused)]
    [InlineData(CopilotAgentStopReason.Paused, DesktopPetCopilotCompletionKind.Paused)]
    [InlineData(CopilotAgentStopReason.Cancelled, DesktopPetCopilotCompletionKind.Cancelled)]
    [InlineData(CopilotAgentStopReason.ApprovalDenied, DesktopPetCopilotCompletionKind.Blocked)]
    [InlineData(CopilotAgentStopReason.BudgetExhausted, DesktopPetCopilotCompletionKind.Blocked)]
    [InlineData(CopilotAgentStopReason.TaskPassLimit, DesktopPetCopilotCompletionKind.Blocked)]
    [InlineData(CopilotAgentStopReason.Blocked, DesktopPetCopilotCompletionKind.Blocked)]
    [InlineData(CopilotAgentStopReason.IncompleteOutput, DesktopPetCopilotCompletionKind.Blocked)]
    [InlineData(CopilotAgentStopReason.ProviderFailure, DesktopPetCopilotCompletionKind.Blocked)]
    [InlineData(CopilotAgentStopReason.Interrupted, DesktopPetCopilotCompletionKind.Blocked)]
    public void StructuredAgentStopReasonMapsToPetActivity(
        CopilotAgentStopReason stopReason,
        DesktopPetCopilotCompletionKind expected)
    {
        var run = new CopilotHostedAgentRun("conversation", CopilotAgentMode.Auto);
        Assert.True(run.TryStart());

        run.SetAgentStopReason(stopReason);
        run.Complete(error: null);

        Assert.Equal(stopReason, run.AgentStopReason);
        Assert.Equal(expected, DesktopPetCopilotBridge.ResolveCompletionKind(run));
    }
}
