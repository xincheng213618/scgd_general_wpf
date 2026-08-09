using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotTurnProviderConnectionRecoveryLifecycleTests
{
    [Fact]
    public void ReducerAcceptsSequentialRecoveryIndependentOfOrdinaryRetries()
    {
        var state = CreatePreparedChatState();
        state = ObserveRecovery(state, CreateRecovery(1));
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnProviderRetryEvent(
                new CopilotProviderRetryInfo(
                    1,
                    2,
                    3,
                    TimeSpan.FromMilliseconds(100),
                    "HTTP 503",
                    503)));
        state = ObserveRecovery(state, CreateRecovery(2));

        Assert.Equal(2, state.ProviderConnectionRecoveryLifecycle.Latest!.RecoveryAttempt);
        Assert.Equal(2, state.ProviderRetryLifecycle.Latest!.NextAttempt);
    }

    [Fact]
    public void ReducerRejectsDuplicateRecoveryAttempt()
    {
        var state = ObserveRecovery(CreatePreparedChatState(), CreateRecovery(1));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ObserveRecovery(state, CreateRecovery(1)));

        Assert.Contains("did not advance in sequence", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsSkippedRecoveryAttempt()
    {
        var state = ObserveRecovery(CreatePreparedChatState(), CreateRecovery(1));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ObserveRecovery(state, CreateRecovery(3)));

        Assert.Contains("did not advance in sequence", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsStructurallyInvalidRecovery()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ObserveRecovery(
                CreatePreparedChatState(),
                new CopilotProviderConnectionRecoveryInfo(
                    1,
                    TimeSpan.FromSeconds(-1),
                    "connection failure")));

        Assert.Contains("invalid metadata", exception.Message, StringComparison.Ordinal);
    }

    private static CopilotTurnEventState CreatePreparedChatState()
    {
        var state = CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Chat),
            new CopilotTurnStartedEvent(CopilotAgentMode.Chat));
        return CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnRequestPreparedEvent(
                new CopilotPreparedTurnRequest("prepared", false)));
    }

    private static CopilotTurnEventState ObserveRecovery(
        CopilotTurnEventState state,
        CopilotProviderConnectionRecoveryInfo recovery) =>
        CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnProviderConnectionRecoveryEvent(recovery));

    private static CopilotProviderConnectionRecoveryInfo CreateRecovery(int attempt) => new(
        attempt,
        TimeSpan.FromSeconds(Math.Min(60, 5 * Math.Pow(2, attempt - 1))),
        "connection failure",
        $"request-{attempt}");
}
