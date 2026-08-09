using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotTurnProviderRetryLifecycleTests
{
    [Fact]
    public void ReducerAcceptsSequentialChatProviderRetries()
    {
        var state = CreatePreparedChatState();
        state = Observe(state, CreateRetry(1, 2, 3));
        state = Observe(state, CreateRetry(2, 3, 3));

        Assert.Equal(3, state.ProviderRetryLifecycle.Latest!.NextAttempt);
    }

    [Fact]
    public void ReducerRejectsStructurallyInvalidRetryAttempt()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(CreatePreparedChatState(), CreateRetry(1, 3, 3)));

        Assert.Contains("invalid metadata", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsDuplicateRetryAttempt()
    {
        var state = Observe(CreatePreparedChatState(), CreateRetry(1, 2, 3));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(state, CreateRetry(1, 2, 3)));

        Assert.Contains("did not advance in sequence", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsSkippedRetryAttempt()
    {
        var state = Observe(CreatePreparedChatState(), CreateRetry(1, 2, 4));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(state, CreateRetry(3, 4, 4)));

        Assert.Contains("did not advance in sequence", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsMaximumAttemptDrift()
    {
        var state = Observe(CreatePreparedChatState(), CreateRetry(1, 2, 3));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(state, CreateRetry(2, 3, 4)));

        Assert.Contains("did not advance in sequence", exception.Message, StringComparison.Ordinal);
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

    private static CopilotTurnEventState Observe(
        CopilotTurnEventState state,
        CopilotProviderRetryInfo retry) =>
        CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnProviderRetryEvent(retry));

    private static CopilotProviderRetryInfo CreateRetry(
        int failedAttempt,
        int nextAttempt,
        int maximumAttempts) => new(
            failedAttempt,
            nextAttempt,
            maximumAttempts,
            TimeSpan.FromMilliseconds(100),
            "timeout",
            null,
            $"request-{failedAttempt}");
}
