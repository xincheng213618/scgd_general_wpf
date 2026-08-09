using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationDelegatedUsageTests
{
    [Fact]
    public void SessionUsageAttributesUniqueDelegatedRunsByActualModel()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var firstAssistant = new CopilotChatMessage(CopilotChatRole.Assistant, "First answer")
        {
            AgentRunBudget = new CopilotAgentBudgetSnapshot
            {
                ProviderCalls = 3,
                ConsumedTokens = 250,
                ReportedInputTokens = 200,
                ReportedOutputTokens = 50,
                ReportedTotalTokens = 250,
            },
        };
        firstAssistant.UpsertAgentTrace(CreateDelegatedTrace(
            "call-a1",
            "run-a1",
            "child-model-a",
            new CopilotTokenUsage(100, 20, 120, 80),
            consumedTokens: 120,
            includesEstimates: false));
        firstAssistant.UpsertAgentTrace(CreateDelegatedTrace(
            "call-a2",
            "run-a2",
            "child-model-a",
            new CopilotTokenUsage(50, 10, 60),
            consumedTokens: 100,
            includesEstimates: true));

        var traceOnlyAssistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Recovered legacy answer");
        traceOnlyAssistant.UpsertAgentTrace(CreateDelegatedTrace(
            "call-b1",
            "run-b1",
            "child-model-b",
            CopilotTokenUsage.Empty,
            consumedTokens: 90,
            includesEstimates: true));
        traceOnlyAssistant.UpsertAgentTrace(CreateDelegatedTrace(
            "call-duplicate",
            "run-a1",
            "duplicate-must-not-count",
            new CopilotTokenUsage(999, 1, 1_000),
            consumedTokens: 1_000,
            includesEstimates: false));
        conversation.Messages.Add(firstAssistant);
        conversation.Messages.Add(traceOnlyAssistant);

        var snapshot = CopilotConversationUsageDiagnostics.Capture(conversation);
        var report = CopilotConversationUsageDiagnostics.Format(conversation);

        Assert.Equal(2, snapshot.AgentUsage.Runs);
        Assert.Equal(3, snapshot.AgentUsage.DelegatedRuns);
        Assert.Equal(2, snapshot.AgentUsage.EstimatedUsageRuns);
        Assert.Equal(2, snapshot.AgentUsage.DelegatedModels.Count);
        var modelA = Assert.Single(
            snapshot.AgentUsage.DelegatedModels,
            item => item.Model == "child-model-a");
        Assert.Equal(2, modelA.Runs);
        Assert.Equal(2, modelA.ReportedUsageRuns);
        Assert.Equal(1, modelA.EstimatedUsageRuns);
        Assert.Equal(220, modelA.ConsumedTokens);
        Assert.Equal(new CopilotTokenUsage(150, 30, 180, 80), modelA.ReportedUsage);
        var modelB = Assert.Single(
            snapshot.AgentUsage.DelegatedModels,
            item => item.Model == "child-model-b");
        Assert.Equal(1, modelB.Runs);
        Assert.Equal(0, modelB.ReportedUsageRuns);
        Assert.Equal(1, modelB.EstimatedUsageRuns);
        Assert.Equal(90, modelB.ConsumedTokens);

        Assert.Contains("子代理模型归因", report, StringComparison.Ordinal);
        Assert.Contains("child-model-a：2 次 · 输入 150 · 输出 30 · 总计 180 · 缓存输入 80 · 预算消耗 220 · 含估算 1 次", report, StringComparison.Ordinal);
        Assert.Contains("child-model-b：1 次 · Provider Token 元数据缺失 · 预算消耗 90 · 未报告 1 次 · 含估算 1 次", report, StringComparison.Ordinal);
        Assert.Contains("不重复累加", report, StringComparison.Ordinal);
    }

    private static CopilotAgentTraceEntry CreateDelegatedTrace(
        string callId,
        string runId,
        string model,
        CopilotTokenUsage usage,
        long consumedTokens,
        bool includesEstimates) => new()
        {
            CallId = callId,
            ToolName = "delegate_explore",
            State = CopilotToolExecutionState.Completed,
            DelegatedRunId = runId,
            DelegatedModel = model,
            DelegatedConsumedTokens = consumedTokens,
            DelegatedUsageIncludesEstimates = includesEstimates,
            DelegatedReportedInputTokens = usage.InputTokens,
            DelegatedReportedOutputTokens = usage.OutputTokens,
            DelegatedReportedTotalTokens = usage.EffectiveTotalTokens,
            DelegatedReportedCachedInputTokens = usage.CachedInputTokens,
        };
}
