namespace ColorVision.Copilot.Tests;

public sealed class CopilotTurnAnswerLifecycleTests
{

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
