using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotTurnBudgetLifecycleTests
{
    [Fact]
    public void ReducerAcceptsMonotonicBudgetUpdatesAndFinalReconciliation()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.BudgetUpdated(CreateBudget(100, 1, 0, 100)));
        state = Observe(
            state,
            CopilotAgentEvent.BudgetUpdated(CreateBudget(200, 2, 1, 200)));

        state.BudgetLifecycle.ValidateCompletion(new CopilotAgentRunResult
        {
            Budget = CreateBudget(300, 3, 2, 300),
        });

        Assert.Equal(200, state.BudgetLifecycle.Latest!.ConsumedTokens);
    }

    [Fact]
    public void ReducerRejectsStructurallyInvalidBudgetUpdate()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                CreateStartedState(),
                CopilotAgentEvent.BudgetUpdated(new CopilotAgentBudgetSnapshot())));

        Assert.Contains("invalid budget snapshot", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsBudgetCounterRegression()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.BudgetUpdated(CreateBudget(200, 2, 1, 200)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.BudgetUpdated(CreateBudget(100, 1, 0, 100))));

        Assert.Contains("moved backwards", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsBudgetLimitChangesDuringTurn()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.BudgetUpdated(CreateBudget(100, 1, 0, 100)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.BudgetUpdated(CreateBudget(
                    200,
                    2,
                    1,
                    200,
                    requestTokenBudget: 200_000))));

        Assert.Contains("moved backwards", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsBudgetFlagsThatBecomeFalse()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.BudgetUpdated(CreateBudget(
                100,
                1,
                0,
                100,
                usedEstimatedUsage: true)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.BudgetUpdated(CreateBudget(200, 2, 1, 200))));

        Assert.Contains("moved backwards", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsFinalBudgetBehindLatestUpdate()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.BudgetUpdated(CreateBudget(200, 2, 1, 200)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            state.BudgetLifecycle.ValidateCompletion(new CopilotAgentRunResult
            {
                Budget = CreateBudget(100, 1, 0, 100),
            }));

        Assert.Contains("did not cover", exception.Message, StringComparison.Ordinal);
    }

    private static CopilotTurnEventState CreateStartedState() =>
        CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Auto),
            new CopilotTurnStartedEvent(CopilotAgentMode.Auto));

    private static CopilotTurnEventState Observe(
        CopilotTurnEventState state,
        CopilotAgentEvent agentEvent) =>
        CopilotTurnEventReducer.Reduce(state, new CopilotTurnAgentEvent(agentEvent));

    private static CopilotAgentBudgetSnapshot CreateBudget(
        int consumedTokens,
        int providerCalls,
        int toolCalls,
        long elapsedMs,
        int requestTokenBudget = 100_000,
        bool usedEstimatedUsage = false)
    {
        var reportedInputTokens = consumedTokens * 3 / 4;
        var reportedOutputTokens = consumedTokens - reportedInputTokens;
        var interChunkCount = Math.Max(0, providerCalls - 1);
        return new CopilotAgentBudgetSnapshot
        {
            CompactionEnabled = true,
            ContextWindowTokens = 131_072,
            InputBudgetTokens = 122_880,
            RequestTokenBudget = requestTokenBudget,
            ConsumedTokens = consumedTokens,
            ProviderCalls = providerCalls,
            PeakEstimatedInputTokens = 1_000,
            ProviderResponseCount = providerCalls,
            ProviderFirstResponseLatencyTotalMs = providerCalls * 100L,
            ProviderFirstResponseLatencyMaxMs = providerCalls > 0 ? 100 : 0,
            ProviderCallDurationTotalMs = providerCalls * 150L,
            ProviderStreamChunkCount = providerCalls,
            ProviderStreamInterChunkLatencyCount = interChunkCount,
            ProviderStreamInterChunkLatencyTotalMs = interChunkCount * 20L,
            ProviderStreamInterChunkLatencyMaxMs = interChunkCount > 0 ? 20 : 0,
            ReportedInputTokens = reportedInputTokens,
            ReportedOutputTokens = reportedOutputTokens,
            ReportedTotalTokens = consumedTokens,
            UsedEstimatedUsage = usedEstimatedUsage,
            MaxToolCalls = 10,
            ToolCalls = toolCalls,
            RegisteredToolCount = 8,
            AvailableToolCount = 6,
            AvailableToolDefinitionCharacters = 2_000,
            HarnessInstructionCharacters = 1_000,
            MaxAgentPasses = 8,
            TotalDurationMs = 60_000,
            ElapsedMs = elapsedMs,
        };
    }
}
