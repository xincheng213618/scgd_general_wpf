using ColorVision.Copilot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentRunMetricsTests
{
    [Fact]
    public void ToolSurfaceMetricsCaptureDefinitionsAndFlowIntoRunSnapshot()
    {
        ICopilotTool tool = new CopilotSearchDocsTool();
        var metrics = CopilotAgentToolSurfaceMetrics.Capture(
            48,
            [tool],
            "rules");
        var expectedDefinitionCharacters = tool.Name.Length
            + tool.Description.Length
            + tool.InputSchema.JsonSchema.GetRawText().Length;
        var budget = new CopilotAgentRunBudget
        {
            RequestTokenBudget = 32_000,
            ContextWindowTokens = 64_000,
            MaxToolCalls = 16,
            MaxAgentPasses = 4,
            TotalDuration = TimeSpan.FromMinutes(2),
        };

        var snapshot = budget.CreateSnapshot(
            new CopilotAgentBudgetSnapshot(),
            TimeSpan.FromSeconds(1),
            toolCalls: 0,
            timeBudgetExhausted: false,
            toolSurface: metrics);

        Assert.Equal(48, metrics.RegisteredToolCount);
        Assert.Equal(1, metrics.AvailableToolCount);
        Assert.Equal(expectedDefinitionCharacters, metrics.AvailableToolDefinitionCharacters);
        Assert.Equal(5, metrics.HarnessInstructionCharacters);
        Assert.Equal(metrics.RegisteredToolCount, snapshot.RegisteredToolCount);
        Assert.Equal(metrics.AvailableToolCount, snapshot.AvailableToolCount);
        Assert.Equal(metrics.AvailableToolDefinitionCharacters, snapshot.AvailableToolDefinitionCharacters);
        Assert.Equal(metrics.HarnessInstructionCharacters, snapshot.HarnessInstructionCharacters);
    }

    [Fact]
    public void DelegatedRunMetricsSeparateParentAndChildCallsAndRoundTrip()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Grounded answer.")
        {
            RequestMode = CopilotAgentMode.Auto,
            ThinkingStartedAt = new DateTime(2026, 7, 26, 20, 16, 3),
            ThinkingCompletedAt = new DateTime(2026, 7, 26, 20, 16, 28),
        };
        assistant.UpsertAgentTrace(new CopilotAgentTraceEntry
        {
            CallId = "call-delegate",
            Round = 1,
            RuntimeName = "agent-framework",
            ToolName = "DelegateExplore",
            State = CopilotToolExecutionState.Completed,
            StartedAtUtc = DateTimeOffset.Parse("2026-07-26T12:16:12+00:00"),
            CompletedAtUtc = DateTimeOffset.Parse("2026-07-26T12:16:28+00:00"),
            DelegatedRunId = "explore-test",
            DelegatedProviderCalls = 1,
            DelegatedConsumedTokens = 6_982,
            DelegatedToolCalls = 1,
            DelegatedRegisteredToolCount = 48,
            DelegatedAvailableToolCount = 5,
            DelegatedAvailableToolDefinitionCharacters = 4_396,
            DelegatedHarnessInstructionCharacters = 7_228,
        });
        assistant.AgentRunBudget = new CopilotAgentBudgetSnapshot
        {
            RequestTokenBudget = 512 * 1024,
            ConsumedTokens = 11_185,
            ProviderCalls = 3,
            InputBudgetTokens = 1_000_000,
            PeakEstimatedInputTokens = 8_192,
            ProviderRetryCount = 2,
            ProviderRateLimitRetryCount = 1,
            ProviderRetryDelayMs = 1_500,
            ProviderResponseCount = 2,
            ProviderFirstResponseLatencyTotalMs = 1_250,
            ProviderFirstResponseLatencyMaxMs = 800,
            ProviderCallDurationTotalMs = 4_200,
            ContextRecoveryCount = 2,
            ContextRecoveryEstimatedInputTokensBefore = 50_000,
            ContextRecoveryEstimatedInputTokensAfter = 22_000,
            ReportedInputTokens = 10_000,
            ReportedOutputTokens = 1_185,
            ReportedTotalTokens = 11_185,
            ReportedCachedInputTokens = 7_500,
            UsedDelegatedDirectAnswer = true,
            MaxToolCalls = 16,
            ToolCalls = 1,
            RegisteredToolCount = 48,
            AvailableToolCount = 2,
            AvailableToolDefinitionCharacters = 1_284,
            HarnessInstructionCharacters = 6_394,
            TotalDurationMs = 120_000,
            ElapsedMs = 25_395,
        };
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(assistant);
        var state = new CopilotChatState
        {
            ActiveConversationId = conversation.Id,
            ActiveProfileId = "profile",
            Conversations = new ObservableCollection<CopilotConversationRecord> { conversation },
        };
        var store = new CopilotChatStateStore(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));

        Assert.Equal("父 2 / 子 1 · 11.2k tokens · 委派直返", assistant.AgentRunCompactLabel);
        Assert.Contains("已处理 25s · 父 2 / 子 1 · 11.2k tokens · 委派直返", assistant.ThinkingHeader, StringComparison.Ordinal);
        Assert.Contains("模型调用：3（父 2 / 子 1）", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains("令牌：11,185（父 4,203 / 子 6,982）", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains("提供商用量：输入 10,000 · 输出 1,185 · 总计 11,185 · 缓存输入 7,500（75%）", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains("提供商重试：2 次 · 计划等待 1.5s · 限流 1 次", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains("模型延迟：首响应平均 625ms · 最慢 800ms · 有效响应 2 / 3 · 调用累计 4.2s", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains("峰值输入（估算）：8,192 / 1,000,000", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains("窗口恢复：2 次 · 累计输入（估算）50,000 → 22,000 tokens（缩减 56%）", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains("工具调用：父 1 / 16 · 子 1", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains("父工具面：2 / 48 · 定义 1,284 字符", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains("子工具面（峰值）：5 / 48 · 定义 4,396 字符", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains("父运行指令：6,394 字符", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains("子运行指令（峰值）：7,228 字符", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);
        Assert.Contains("委派直返：是（省略第二次父级模型调用）", assistant.AgentRunMetricsToolTip, StringComparison.Ordinal);

        var serialized = store.Serialize(state);
        var document = JObject.Parse(serialized);
        var budgetDocument = Assert.IsType<JObject>(
            document[nameof(CopilotChatState.Conversations)]![0]!
                [nameof(CopilotConversationRecord.Messages)]![0]!
                [nameof(CopilotChatMessage.AgentRunBudget)]);
        var restored = JsonConvert.DeserializeObject<CopilotChatState>(serialized);

        Assert.Equal(CopilotChatState.CurrentSchemaVersion, document[nameof(CopilotChatState.SchemaVersion)]!.Value<int>());
        Assert.True(budgetDocument[nameof(CopilotAgentBudgetSnapshot.UsedDelegatedDirectAnswer)]!.Value<bool>());
        Assert.NotNull(restored);
        var restoredConversation = Assert.Single(restored.Conversations);
        restoredConversation.EnsureValid();
        var restoredMessage = Assert.Single(restoredConversation.Messages);
        Assert.Equal(3, restoredMessage.AgentRunBudget.ProviderCalls);
        Assert.Equal(11_185, restoredMessage.AgentRunBudget.ConsumedTokens);
        Assert.Equal(8_192, restoredMessage.AgentRunBudget.PeakEstimatedInputTokens);
        Assert.Equal(2, restoredMessage.AgentRunBudget.ProviderRetryCount);
        Assert.Equal(1, restoredMessage.AgentRunBudget.ProviderRateLimitRetryCount);
        Assert.Equal(1_500, restoredMessage.AgentRunBudget.ProviderRetryDelayMs);
        Assert.Equal(2, restoredMessage.AgentRunBudget.ProviderResponseCount);
        Assert.Equal(1_250, restoredMessage.AgentRunBudget.ProviderFirstResponseLatencyTotalMs);
        Assert.Equal(800, restoredMessage.AgentRunBudget.ProviderFirstResponseLatencyMaxMs);
        Assert.Equal(4_200, restoredMessage.AgentRunBudget.ProviderCallDurationTotalMs);
        Assert.Equal(2, restoredMessage.AgentRunBudget.ContextRecoveryCount);
        Assert.Equal(50_000, restoredMessage.AgentRunBudget.ContextRecoveryEstimatedInputTokensBefore);
        Assert.Equal(22_000, restoredMessage.AgentRunBudget.ContextRecoveryEstimatedInputTokensAfter);
        Assert.Equal(10_000, restoredMessage.AgentRunBudget.ReportedInputTokens);
        Assert.Equal(1_185, restoredMessage.AgentRunBudget.ReportedOutputTokens);
        Assert.Equal(11_185, restoredMessage.AgentRunBudget.ReportedTotalTokens);
        Assert.Equal(7_500, restoredMessage.AgentRunBudget.ReportedCachedInputTokens);
        Assert.True(restoredMessage.AgentRunBudget.UsedDelegatedDirectAnswer);
        Assert.Equal(48, restoredMessage.AgentRunBudget.RegisteredToolCount);
        Assert.Equal(2, restoredMessage.AgentRunBudget.AvailableToolCount);
        Assert.Equal(1_284, restoredMessage.AgentRunBudget.AvailableToolDefinitionCharacters);
        Assert.Equal(6_394, restoredMessage.AgentRunBudget.HarnessInstructionCharacters);
        var restoredTrace = Assert.Single(restoredMessage.AgentTraceEntries);
        Assert.Equal(CopilotAgentTraceEntry.CurrentSchemaVersion, restoredTrace.SchemaVersion);
        Assert.Equal(48, restoredTrace.DelegatedRegisteredToolCount);
        Assert.Equal(5, restoredTrace.DelegatedAvailableToolCount);
        Assert.Equal(4_396, restoredTrace.DelegatedAvailableToolDefinitionCharacters);
        Assert.Equal(7_228, restoredTrace.DelegatedHarnessInstructionCharacters);
        Assert.Equal(assistant.AgentRunCompactLabel, restoredMessage.AgentRunCompactLabel);
        Assert.Equal(assistant.AgentRunMetricsToolTip, restoredMessage.AgentRunMetricsToolTip);
    }

    [Fact]
    public void EmptyOrInvalidRunMetricsAreNormalizedAndOmitted()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Answer.")
        {
            AgentRunBudget = new CopilotAgentBudgetSnapshot
            {
                ConsumedTokens = -1,
                ProviderCalls = -2,
                PeakEstimatedInputTokens = -8,
                ProviderRetryCount = -4,
                ProviderRateLimitRetryCount = 8,
                ProviderRetryDelayMs = -250,
                ProviderResponseCount = 99,
                ProviderFirstResponseLatencyTotalMs = 1_500,
                ProviderFirstResponseLatencyMaxMs = 2_000,
                ProviderCallDurationTotalMs = 3_000,
                ContextRecoveryCount = -3,
                ContextRecoveryEstimatedInputTokensBefore = -12,
                ContextRecoveryEstimatedInputTokensAfter = 99,
                ReportedInputTokens = -9,
                ReportedOutputTokens = -10,
                ReportedTotalTokens = -11,
                ReportedCachedInputTokens = 99,
                MaxToolCalls = -1,
                ToolCalls = -3,
                RegisteredToolCount = -5,
                AvailableToolCount = 99,
                AvailableToolDefinitionCharacters = -6,
                HarnessInstructionCharacters = -7,
                ElapsedMs = -4,
            },
        };

        var serialized = JsonConvert.SerializeObject(assistant, Formatting.None);
        var document = JObject.Parse(serialized);

        Assert.False(assistant.HasAgentRunMetrics);
        Assert.Equal(0, assistant.AgentRunBudget.ConsumedTokens);
        Assert.Equal(0, assistant.AgentRunBudget.ProviderCalls);
        Assert.Equal(0, assistant.AgentRunBudget.PeakEstimatedInputTokens);
        Assert.Equal(0, assistant.AgentRunBudget.ProviderRetryCount);
        Assert.Equal(0, assistant.AgentRunBudget.ProviderRateLimitRetryCount);
        Assert.Equal(0, assistant.AgentRunBudget.ProviderRetryDelayMs);
        Assert.Equal(0, assistant.AgentRunBudget.ProviderResponseCount);
        Assert.Equal(0, assistant.AgentRunBudget.ProviderFirstResponseLatencyTotalMs);
        Assert.Equal(0, assistant.AgentRunBudget.ProviderFirstResponseLatencyMaxMs);
        Assert.Equal(0, assistant.AgentRunBudget.ProviderCallDurationTotalMs);
        Assert.Equal(0, assistant.AgentRunBudget.ContextRecoveryCount);
        Assert.Equal(0, assistant.AgentRunBudget.ContextRecoveryEstimatedInputTokensBefore);
        Assert.Equal(0, assistant.AgentRunBudget.ContextRecoveryEstimatedInputTokensAfter);
        Assert.Equal(0, assistant.AgentRunBudget.ReportedInputTokens);
        Assert.Equal(0, assistant.AgentRunBudget.ReportedOutputTokens);
        Assert.Equal(0, assistant.AgentRunBudget.ReportedTotalTokens);
        Assert.Null(assistant.AgentRunBudget.ReportedCachedInputTokens);
        Assert.Equal(0, assistant.AgentRunBudget.ToolCalls);
        Assert.Equal(0, assistant.AgentRunBudget.RegisteredToolCount);
        Assert.Equal(0, assistant.AgentRunBudget.AvailableToolCount);
        Assert.Equal(0, assistant.AgentRunBudget.AvailableToolDefinitionCharacters);
        Assert.Equal(0, assistant.AgentRunBudget.HarnessInstructionCharacters);
        Assert.Equal(0, assistant.AgentRunBudget.ElapsedMs);
        Assert.Null(document[nameof(CopilotChatMessage.AgentRunBudget)]);

        var delegatedTrace = new CopilotAgentTraceEntry
        {
            Round = 1,
            DelegatedRegisteredToolCount = -5,
            DelegatedAvailableToolCount = 99,
            DelegatedAvailableToolDefinitionCharacters = -6,
            DelegatedHarnessInstructionCharacters = -7,
        };

        Assert.True(delegatedTrace.EnsureValid(DateTimeOffset.UtcNow));
        Assert.Equal(0, delegatedTrace.DelegatedRegisteredToolCount);
        Assert.Equal(0, delegatedTrace.DelegatedAvailableToolCount);
        Assert.Equal(0, delegatedTrace.DelegatedAvailableToolDefinitionCharacters);
        Assert.Equal(0, delegatedTrace.DelegatedHarnessInstructionCharacters);
    }
}
