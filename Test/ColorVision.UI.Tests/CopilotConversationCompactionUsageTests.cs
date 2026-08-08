using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationCompactionUsageTests
{
    [Fact]
    public void CompactionUsageAccumulatesAndNormalizesProviderMetadata()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var firstCompletedAt = DateTimeOffset.Parse("2026-08-08T01:02:03+08:00");
        var secondCompletedAt = firstCompletedAt.AddMinutes(5);

        conversation.RecordCompactionUsage(
            new CopilotTokenUsage(100, 20, 0, 150),
            firstCompletedAt);
        conversation.RecordCompactionUsage(
            new CopilotTokenUsage(50, 10, 1),
            secondCompletedAt);

        var usage = Assert.IsType<CopilotConversationAuxiliaryUsage>(conversation.CompactionUsage);
        Assert.Equal(2, usage.RequestCount);
        Assert.Equal(150, usage.Usage.InputTokens);
        Assert.Equal(30, usage.Usage.OutputTokens);
        Assert.Equal(180, usage.Usage.EffectiveTotalTokens);
        Assert.Equal(100, usage.Usage.EffectiveCachedInputTokens);
        Assert.Equal(secondCompletedAt.ToUniversalTime(), usage.LastRequestAtUtc);
    }

    [Fact]
    public void CompactionUsageSurvivesConversationRoundTrip()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.RecordCompactionUsage(
            new CopilotTokenUsage(64, 16, 80, 32),
            DateTimeOffset.Parse("2026-08-08T02:03:04Z"));

        var json = JsonConvert.SerializeObject(conversation);
        var restored = JsonConvert.DeserializeObject<CopilotConversationRecord>(json);

        Assert.NotNull(restored);
        Assert.Contains(nameof(CopilotConversationRecord.CompactionUsage), json, StringComparison.Ordinal);
        var usage = Assert.IsType<CopilotConversationAuxiliaryUsage>(restored.CompactionUsage);
        Assert.Equal(1, usage.RequestCount);
        Assert.Equal(new CopilotTokenUsage(64, 16, 80, 32), usage.Usage);
        Assert.Equal(DateTimeOffset.Parse("2026-08-08T02:03:04Z"), usage.LastRequestAtUtc);
    }

    [Fact]
    public void SessionUsageIncludesCompactionWithoutReplacingLastAnswer()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Answer");
        assistant.SetReportedUsage(new CopilotTokenUsage(100, 25, 125, 40));
        conversation.Messages.Add(assistant);
        conversation.RecordCompactionUsage(
            new CopilotTokenUsage(30, 10, 40, 20),
            DateTimeOffset.UtcNow);

        var snapshot = CopilotConversationUsageDiagnostics.Capture(conversation);
        var report = CopilotConversationUsageDiagnostics.Format(conversation);

        Assert.Equal(new CopilotTokenUsage(130, 35, 165, 60), snapshot.TotalUsage);
        Assert.Equal(new CopilotTokenUsage(100, 25, 125, 40), snapshot.LastUsage);
        Assert.Equal(new CopilotTokenUsage(30, 10, 40, 20), snapshot.CompactionUsage);
        Assert.Equal(1, snapshot.CompactionRequests);
        Assert.Contains("压缩模型调用：1 次", report, StringComparison.Ordinal);
        Assert.Contains("最近一轮回答：输入 100", report, StringComparison.Ordinal);
    }

    [Fact]
    public void BranchCopiesUsageWhenItCopiesTheActiveCompaction()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Question"));
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Answer");
        conversation.Messages.Add(assistant);
        conversation.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = "Earlier question and answer.",
            ThroughMessageId = assistant.Id,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            SourceMessageCount = 2,
            SourceCharacters = 32,
        };
        conversation.RecordCompactionUsage(
            new CopilotTokenUsage(80, 20, 100, 40),
            DateTimeOffset.UtcNow);

        var branch = CopilotConversationBranchService.CreateBranch(conversation, assistant);

        Assert.NotSame(conversation.CompactionUsage, branch.CompactionUsage);
        var usage = Assert.IsType<CopilotConversationAuxiliaryUsage>(branch.CompactionUsage);
        Assert.Equal(1, usage.RequestCount);
        Assert.Equal(new CopilotTokenUsage(80, 20, 100, 40), usage.Usage);
    }

    [Fact]
    public void ValidationRepairsPersistedUsageAndInfersMissingRequestCount()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.CompactionUsage = new CopilotConversationAuxiliaryUsage
        {
            RequestCount = 0,
            InputTokens = 20,
            OutputTokens = 5,
            TotalTokens = 1,
            CachedInputTokens = 99,
        };

        Assert.True(conversation.EnsureValid());

        var usage = Assert.IsType<CopilotConversationAuxiliaryUsage>(conversation.CompactionUsage);
        Assert.Equal(1, usage.RequestCount);
        Assert.Equal(25, usage.TotalTokens);
        Assert.Equal(20, usage.CachedInputTokens);
        Assert.False(conversation.EnsureValid());
    }
}
