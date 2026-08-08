using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotTurnTokenUsageUpdateTests
{
    [Fact]
    public void AgentBudgetMappingExcludesEstimatedConsumedTokens()
    {
        var usage = CopilotTurnRuntime.GetReportedTokenUsage(new CopilotAgentBudgetSnapshot
        {
            ConsumedTokens = 50_000,
            UsedEstimatedUsage = true,
            ReportedInputTokens = 120,
            ReportedOutputTokens = 30,
            ReportedTotalTokens = 150,
            ReportedCachedInputTokens = 80,
        });

        Assert.Equal(new CopilotTokenUsage(120, 30, 150, 80), usage);
    }

    [Fact]
    public void EventSinkPublishesOnlyChangedNormalizedUsageSnapshots()
    {
        var emitted = new List<CopilotTurnEvent>();
        var sink = new CopilotTurnEventSink(emitted.Add);

        sink.OnTokenUsageUpdated(CopilotTokenUsage.Empty);
        sink.OnTokenUsageUpdated(new CopilotTokenUsage(10, 2, 1, 20));
        sink.OnTokenUsageUpdated(new CopilotTokenUsage(10, 2, 12, 10));
        sink.OnTokenUsageUpdated(new CopilotTokenUsage(12, 3, 15, 11));

        Assert.Collection(
            emitted,
            item => Assert.Equal(
                new CopilotTokenUsage(10, 2, 12, 10),
                Assert.IsType<CopilotTurnTokenUsageUpdatedEvent>(item).Usage),
            item => Assert.Equal(
                new CopilotTokenUsage(12, 3, 15, 11),
                Assert.IsType<CopilotTurnTokenUsageUpdatedEvent>(item).Usage));
    }

    [Fact]
    public void ReducerAcceptsMonotonicUsageThatMatchesCompletion()
    {
        var state = CreatePreparedChatState();
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnTokenUsageUpdatedEvent(new CopilotTokenUsage(10, 2, 12, 4)));
        var finalUsage = new CopilotTokenUsage(12, 3, 15, 5);
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnTokenUsageUpdatedEvent(finalUsage));
        var result = CreateChatResult(finalUsage);

        state = CopilotTurnEventReducer.Reduce(state, new CopilotTurnCompletedEvent(result));

        Assert.Equal(finalUsage, state.TokenUsage);
        Assert.Same(result, CopilotTurnEventReducer.RequireCompletion(state));
    }

    [Fact]
    public void ReducerRejectsUsageThatMovesBackwards()
    {
        var state = CreatePreparedChatState();
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnTokenUsageUpdatedEvent(new CopilotTokenUsage(10, 4, 14, 3)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotTurnEventReducer.Reduce(
                state,
                new CopilotTurnTokenUsageUpdatedEvent(new CopilotTokenUsage(9, 5, 14, 3))));

        Assert.Contains("moved backwards", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsCompletionThatDisagreesWithLatestUsage()
    {
        var state = CreatePreparedChatState();
        state = CopilotTurnEventReducer.Reduce(
            state,
            new CopilotTurnTokenUsageUpdatedEvent(new CopilotTokenUsage(10, 2, 12)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotTurnEventReducer.Reduce(
                state,
                new CopilotTurnCompletedEvent(
                    CreateChatResult(new CopilotTokenUsage(10, 3, 13)))));

        Assert.Contains("did not match its latest update", exception.Message, StringComparison.Ordinal);
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

    private static CopilotTurnResult CreateChatResult(CopilotTokenUsage usage) =>
        CopilotTurnResult.FromChat(
            usage,
            "prepared",
            chatAttachmentContextCaptured: false,
            new CopilotChatStreamResult(
                usage,
                CopilotChatFinishKind.Complete,
                "stop"));
}
