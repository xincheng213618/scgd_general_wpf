using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotTurnAnswerLifecycleTests
{
    [Fact]
    public void ReviewAcceptsFinalSnapshotThatMatchesDeltasAfterReset()
    {
        var state = CreateEnteredReviewState();
        state = Observe(state, CopilotAgentEvent.AnswerDelta("unsupported draft"));
        state = Observe(state, CopilotAgentEvent.AnswerReset());
        state = Observe(state, CopilotAgentEvent.AnswerDelta("verified "));
        state = Observe(state, CopilotAgentEvent.AnswerDelta("finding"));
        state = Observe(state, CopilotAgentEvent.Completed());

        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnReviewExitedEvent(
                CopilotWorkspaceReviewTargetContext.WorkingTree(),
                "verified finding",
                false));

        Assert.True(state.ReviewExited);
        Assert.Equal("verified finding", state.AnswerLifecycle.Text);
    }

    [Fact]
    public void ReviewRejectsFinalSnapshotThatDiffersFromStreamedAnswer()
    {
        var state = CreateEnteredReviewState();
        state = Observe(state, CopilotAgentEvent.AnswerDelta("streamed finding"));
        state = Observe(state, CopilotAgentEvent.Completed());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotTurnEventReducer.Reduce(
                state,
                new CopilotTurnReviewExitedEvent(
                    CopilotWorkspaceReviewTargetContext.WorkingTree(),
                    "different finding",
                    false)));

        Assert.Contains("did not match its streamed answer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnswerLifecyclePreservesBoundedTruncationSnapshot()
    {
        var state = CopilotTurnAnswerLifecycleState.Empty.Observe(
            CopilotAgentEvent.AnswerDelta(
                new string('x', CopilotChatMessage.MaximumAssistantTextCharacters + 1)));

        state.ValidateSnapshot(state.Text, isTruncated: true);

        Assert.True(state.IsTruncated);
        Assert.EndsWith(
            CopilotChatMessage.ResponseTruncationMarker,
            state.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    public void AnswerLifecycleRejectsIncorrectTruncationFlag()
    {
        var state = CopilotTurnAnswerLifecycleState.Empty.Observe(
            CopilotAgentEvent.AnswerDelta(
                new string('x', CopilotChatMessage.MaximumAssistantTextCharacters + 1)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            state.ValidateSnapshot(state.Text, isTruncated: false));

        Assert.Contains("did not match its streamed answer", exception.Message, StringComparison.Ordinal);
    }

    private static CopilotTurnEventState CreateEnteredReviewState()
    {
        var state = CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Review),
            new CopilotTurnStartedEvent(CopilotAgentMode.Review));
        return CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnReviewEnteredEvent(
                CopilotWorkspaceReviewTargetContext.WorkingTree()));
    }

    private static CopilotTurnEventState Observe(
        CopilotTurnEventState state,
        CopilotAgentEvent agentEvent) =>
        CopilotTurnEventReducer.Reduce(state, new CopilotTurnAgentEvent(agentEvent));
}
