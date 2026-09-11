using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexAutoCompactionTests
{
    [Theory]
    [InlineData(false, "  next 中文  ")]
    [InlineData(true, "  next 中文  ")]
    [InlineData(true, "  ")]
    public void SharedHistoryMeasurementMatchesLiveMeasurementIncludingModelOnlyContent(bool compacted, string prompt)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var boundary = new CopilotChatMessage(CopilotChatRole.User, "Earlier request");
        conversation.Messages.Add(boundary);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Local display") { IsContentDisplayOnly = true });
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Visible request") { RequestContent = "实际模型输入" });
        var interrupted = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial answer");
        interrupted.MarkResponseInterrupted();
        conversation.Messages.Add(interrupted);
        if (compacted)
        {
            conversation.Compaction = new CopilotConversationCompaction
            {
                StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
                Summary = "Earlier 中文 summary",
                ThroughMessageId = boundary.Id,
            };
        }
        var limits = new CopilotConversationHistoryLimits(2, 100, 50);
        var history = CopilotConversationCompactionContext.Build(conversation, null, useModelContent: true);

        var shared = CopilotConversationAutoCompactionPolicy.MeasureHistory(
            history, limits, prompt, CopilotConversationCompactionContext.EstimateCarriedPrefixWeight(conversation));
        var live = CopilotConversationAutoCompactionPolicy.Measure(conversation, limits, prompt);

        Assert.Equal(live, shared);
        Assert.Equal(3 + (string.IsNullOrWhiteSpace(prompt) ? 0 : 1), shared.ActiveMessageCount);
        Assert.True(shared.ActiveWeight > limits.MaximumCharacters);
        Assert.Equal(compacted, shared.CarriedPrefixWeight > 0);
        Assert.Equal(default, CopilotConversationAutoCompactionPolicy.MeasureHistory(history, default, prompt, 0));
    }

    [Fact]
    public void AutomaticCompactionRemeasuresChangedMessagesAfterAnEarlierUiMeasurement()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Short request"));
        var response = new CopilotChatMessage(CopilotChatRole.Assistant, "Short answer");
        conversation.Messages.Add(response);
        var limits = new CopilotConversationHistoryLimits(64, 100_000, 50_000);
        var history = CopilotConversationCompactionContext.Build(conversation, null, useModelContent: true);
        var earlierUsage = CopilotConversationAutoCompactionPolicy.MeasureHistory(history, limits, "", 0);
        var options = new CopilotConversationAutoCompactionOptions(true, 85, 200, CopilotModelAutoCompactTokenLimitScope.Total);
        Assert.False(CopilotConversationAutoCompactionPolicy.Evaluate(conversation, limits, "", options).ShouldCompact);

        response.Content = new string('中', 300);
        var current = CopilotConversationAutoCompactionPolicy.Evaluate(conversation, limits, "", options);

        Assert.True(current.ShouldCompact);
        Assert.Equal(CopilotConversationAutoCompactionTrigger.ConfiguredTokenLimit, current.Trigger);
        Assert.True(current.ActiveWeight > earlierUsage.ActiveWeight);
        Assert.Equal("Short answer", history[1].Content);
    }

    [Fact]
    public void BodyAfterPrefixExcludesTheCarriedCompactionSummaryFromTheTokenThreshold()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "First request"));
        var boundary = new CopilotChatMessage(CopilotChatRole.Assistant, "First response");
        conversation.Messages.Add(boundary);
        conversation.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = new string('s', 2_000),
            ThroughMessageId = boundary.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            SourceMessageCount = 2,
            SourceCharacters = 2_100,
        };
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, new string('u', 80)));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, new string('a', 80)));
        var limits = new CopilotConversationHistoryLimits(64, 100_000, 50_000);

        var total = CopilotConversationAutoCompactionPolicy.Evaluate(
            conversation,
            limits,
            pendingPrompt: "continue",
            new CopilotConversationAutoCompactionOptions(
                Enabled: true,
                ThresholdPercent: 85,
                ModelTokenLimit: 200,
                ModelTokenLimitScope: CopilotModelAutoCompactTokenLimitScope.Total));
        var body = CopilotConversationAutoCompactionPolicy.Evaluate(
            conversation,
            limits,
            pendingPrompt: "continue",
            new CopilotConversationAutoCompactionOptions(
                Enabled: true,
                ThresholdPercent: 85,
                ModelTokenLimit: 200,
                ModelTokenLimitScope: CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix));
        var usage = CopilotConversationAutoCompactionPolicy.Measure(conversation, limits, "continue");

        Assert.True(total.ShouldCompact);
        Assert.Equal(CopilotConversationAutoCompactionTrigger.ConfiguredTokenLimit, total.Trigger);
        Assert.True(total.EvaluatedTokens >= total.ThresholdTokens);
        Assert.False(body.ShouldCompact);
        Assert.True(body.EvaluatedTokens < body.ThresholdTokens);
        Assert.True(usage.CarriedPrefixWeight > 0);
        Assert.Equal(usage.ActiveWeight, usage.CarriedPrefixWeight + usage.BodyAfterPrefixWeight);
    }

    [Fact]
    public void ContextUsagePresentationUsesTheConfiguredScopeForPressure()
    {
        var usage = new CopilotConversationContextUsage(
            UsagePercent: 20,
            WeightUsagePercent: 20,
            MessageUsagePercent: 10,
            ActiveMessageCount: 2,
            ActiveWeight: 800,
            CarriedPrefixWeight: 600,
            BodyAfterPrefixWeight: 200,
            MaximumMessages: 20,
            MaximumWeight: 4_000);

        var body = CopilotConversationContextUsagePresenter.Create(
            usage,
            autoCompactionEnabled: true,
            autoCompactThresholdPercent: 85,
            modelAutoCompactTokenLimit: 100,
            modelAutoCompactTokenLimitScope: CopilotModelAutoCompactTokenLimitScope.BodyAfterPrefix);
        var total = CopilotConversationContextUsagePresenter.Create(
            usage,
            autoCompactionEnabled: true,
            autoCompactThresholdPercent: 85,
            modelAutoCompactTokenLimit: 100,
            modelAutoCompactTokenLimitScope: CopilotModelAutoCompactTokenLimitScope.Total);

        Assert.Contains("body_after_prefix 自动压缩计量为 50/100 Token", body.ToolTip, StringComparison.Ordinal);
        Assert.False(body.IsUnderPressure);
        Assert.Contains("total 自动压缩计量为 200/100 Token", total.ToolTip, StringComparison.Ordinal);
        Assert.True(total.IsUnderPressure);
    }
}
