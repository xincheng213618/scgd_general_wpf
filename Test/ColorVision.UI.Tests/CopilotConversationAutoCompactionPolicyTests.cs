using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationAutoCompactionPolicyTests
{
    [Fact]
    public void ConfigurationDefaultsAreEnabledAndCloned()
    {
        var config = new CopilotAgentDefaultsConfig();

        Assert.True(config.AutoCompactConversationHistory);
        Assert.Equal(85, config.AutoCompactThresholdPercent);

        config.AutoCompactConversationHistory = false;
        config.AutoCompactThresholdPercent = 73;
        var clone = config.Clone();

        Assert.False(clone.AutoCompactConversationHistory);
        Assert.Equal(73, clone.AutoCompactThresholdPercent);
    }

    [Fact]
    public void RecommendsCompactionAtTheConfiguredWeightThreshold()
    {
        var conversation = CreateConversation(
            new string('a', 425),
            new string('b', 425));

        var decision = CopilotConversationAutoCompactionPolicy.Evaluate(
            conversation,
            new CopilotConversationHistoryLimits(100, 1_000, 1_000),
            pendingPrompt: string.Empty,
            enabled: true,
            thresholdPercent: 85);

        Assert.True(decision.ShouldCompact);
        Assert.Equal(CopilotConversationAutoCompactionTrigger.HistoryWeight, decision.Trigger);
        Assert.Equal(85, decision.UsagePercent);
    }

    [Fact]
    public void IncludesPendingPromptAndWeightedTextInTheThreshold()
    {
        var conversation = CreateConversation(
            new string('a', 200),
            new string('界', 100));

        var decision = CopilotConversationAutoCompactionPolicy.Evaluate(
            conversation,
            new CopilotConversationHistoryLimits(100, 1_000, 1_000),
            pendingPrompt: new string('b', 250),
            enabled: true,
            thresholdPercent: 85);

        Assert.True(decision.ShouldCompact);
        Assert.Equal(85, decision.UsagePercent);
        Assert.Equal(850, decision.ActiveWeight);
    }

    [Fact]
    public void MessageCountCanTriggerBeforeTheWeightLimit()
    {
        var conversation = CreateConversation(
            "u1", "a1",
            "u2", "a2",
            "u3", "a3",
            "u4", "a4");

        var decision = CopilotConversationAutoCompactionPolicy.Evaluate(
            conversation,
            new CopilotConversationHistoryLimits(10, 10_000, 1_000),
            pendingPrompt: "next",
            enabled: true,
            thresholdPercent: 85);

        Assert.True(decision.ShouldCompact);
        Assert.Equal(CopilotConversationAutoCompactionTrigger.MessageCount, decision.Trigger);
        Assert.Equal(90, decision.UsagePercent);
    }

    [Fact]
    public void ExistingSummaryRequiresOneCompleteNewTurn()
    {
        var conversation = CreateConversation(
            "old user",
            "old assistant");
        conversation.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = "existing summary",
            ThroughMessageId = conversation.Messages[1].Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            SourceMessageCount = 2,
            SourceCharacters = 21,
        };

        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, new string('a', 900)));
        var decision = CopilotConversationAutoCompactionPolicy.Evaluate(
            conversation,
            new CopilotConversationHistoryLimits(100, 1_000, 1_000),
            pendingPrompt: string.Empty,
            enabled: true,
            thresholdPercent: 85);

        Assert.False(decision.ShouldCompact);

        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "next assistant"));

        decision = CopilotConversationAutoCompactionPolicy.Evaluate(
            conversation,
            new CopilotConversationHistoryLimits(100, 1_000, 1_000),
            pendingPrompt: string.Empty,
            enabled: true,
            thresholdPercent: 85);

        Assert.True(decision.ShouldCompact);
    }

    [Fact]
    public void DisabledPolicyNeverRecommendsCompaction()
    {
        var conversation = CreateConversation(
            new string('a', 500),
            new string('b', 500));

        var decision = CopilotConversationAutoCompactionPolicy.Evaluate(
            conversation,
            new CopilotConversationHistoryLimits(100, 1_000, 1_000),
            pendingPrompt: string.Empty,
            enabled: false,
            thresholdPercent: 85);

        Assert.False(decision.ShouldCompact);
    }

    private static CopilotConversationRecord CreateConversation(params string[] messages)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Test");
        for (var index = 0; index < messages.Length; index++)
        {
            var role = index % 2 == 0 ? CopilotChatRole.User : CopilotChatRole.Assistant;
            conversation.Messages.Add(new CopilotChatMessage(role, messages[index]));
        }
        return conversation;
    }
}
