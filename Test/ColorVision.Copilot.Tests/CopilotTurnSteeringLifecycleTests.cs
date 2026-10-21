namespace ColorVision.Copilot.Tests;

public sealed class CopilotTurnSteeringLifecycleTests
{

    [Fact]
    public void ReducerAcceptsDistinctDeliveredAndRecoveredMessages()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.SteeringDelivered(
            [
                new CopilotSteeringMessageSnapshot("message:1", "use the current file"),
            ]));
        state = Observe(
            state,
            CopilotAgentEvent.SteeringRecovery(
            [
                new CopilotSteeringMessageSnapshot("message:2", "also run the focused tests"),
            ]));

        Assert.Equal(1, state.SteeringLifecycle.DeliveredCount);
        Assert.Equal(1, state.SteeringLifecycle.RecoveredCount);
    }

    [Fact]
    public void ReducerRejectsDeliveredMessageThatIsLaterRecovered()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.SteeringDelivered(
            [
                new CopilotSteeringMessageSnapshot("message:1", "continue"),
            ]));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.SteeringRecovery(
                [
                    new CopilotSteeringMessageSnapshot("message:1", "continue"),
                ])));

        Assert.Contains("both delivered and recovered", exception.Message, StringComparison.Ordinal);
    }

    private static CopilotTurnEventState CreateStartedState() =>
        CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Auto),
            new CopilotTurnStartedEvent(CopilotAgentMode.Auto));

    private static CopilotTurnEventState Observe(
        CopilotTurnEventState state,
        CopilotAgentEvent agentEvent) =>
        CopilotTurnEventReducer.Reduce(state, new CopilotTurnAgentEvent(agentEvent));
}
