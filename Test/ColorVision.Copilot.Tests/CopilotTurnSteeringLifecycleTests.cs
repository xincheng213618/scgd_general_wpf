using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotTurnSteeringLifecycleTests
{
    [Fact]
    public void PublishedSteeringEventOwnsItsMessageCollection()
    {
        var source = new[]
        {
            new CopilotSteeringMessageSnapshot("message:1", "continue"),
        };

        var agentEvent = CopilotAgentEvent.SteeringDelivered(source);
        source[0] = new CopilotSteeringMessageSnapshot("message:2", "rewritten");

        Assert.Equal("message:1", Assert.Single(agentEvent.SteeringMessages).MessageId);
        var messages = Assert.IsAssignableFrom<IList<CopilotSteeringMessageSnapshot>>(
            agentEvent.SteeringMessages);
        Assert.Throws<NotSupportedException>(() => messages[0] = source[0]);
    }

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
    public void ReducerRejectsDuplicateDeliveredMessage()
    {
        var delivered = CopilotAgentEvent.SteeringDelivered(
        [
            new CopilotSteeringMessageSnapshot("message:1", "continue"),
        ]);
        var state = Observe(CreateStartedState(), delivered);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(state, delivered));

        Assert.Contains("delivered more than once", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsDuplicateRecoveredMessage()
    {
        var recovered = CopilotAgentEvent.SteeringRecovery(
        [
            new CopilotSteeringMessageSnapshot("message:1", "continue"),
        ]);
        var state = Observe(CreateStartedState(), recovered);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(state, recovered));

        Assert.Contains("recovered more than once", exception.Message, StringComparison.Ordinal);
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

    [Fact]
    public void ReducerRejectsRecoveredMessageThatIsLaterDelivered()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.SteeringRecovery(
            [
                new CopilotSteeringMessageSnapshot("message:1", "continue"),
            ]));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.SteeringDelivered(
                [
                    new CopilotSteeringMessageSnapshot("message:1", "different text"),
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
