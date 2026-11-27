using Newtonsoft.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotConversationStatisticsTests
{
    [Fact]
    public void BranchUsageUsesTheFirstMatchingParentAndFallsBackToForkTime()
    {
        var forkedAt = new DateTimeOffset(2026, 8, 9, 10, 0, 0, TimeSpan.Zero);
        DateTime forkTime = forkedAt.LocalDateTime;
        var question = new CopilotChatMessage(CopilotChatRole.User, "question")
        {
            CreatedAt = forkTime.AddMinutes(-1),
        };
        CopilotChatMessage through = Response("through", 10, forkTime.AddMinutes(1));
        var parent = new CopilotConversationRecord { Id = "parent", Messages = [question, through] };
        var duplicate = new CopilotConversationRecord { Id = parent.Id, Messages = [through] };
        var branch = new CopilotConversationRecord
        {
            Id = "branch",
            BranchOrigin = Origin(parent.Id, through.Id, forkedAt),
            Messages = [question, through, Response("continued", 20, forkTime.AddMinutes(2))],
        };
        CopilotChatMessage inherited = Response("inherited", 100, forkTime);
        var missingParent = new CopilotConversationRecord
        {
            Id = "missing-parent-branch",
            BranchOrigin = Origin("deleted-parent", through.Id, forkedAt),
            Messages = [question, inherited, Response("after-parent-deleted", 30, forkTime.AddMinutes(2))],
        };
        var missingMessage = new CopilotConversationRecord
        {
            Id = "missing-message-branch",
            BranchOrigin = Origin(parent.Id, "deleted-message", forkedAt),
            Messages = [question, inherited, Response("after-message-deleted", 40, forkTime.AddMinutes(2))],
        };

        var legacy = JsonConvert.DeserializeObject<CopilotConversationRecord>("""{"Id":null}""");
        Assert.NotNull(legacy);
        CopilotConversationStatisticsSnapshot snapshot = CopilotConversationStatistics.Capture(
            [legacy, parent, duplicate, branch, missingParent, missingMessage],
            forkedAt.ToLocalTime().AddMinutes(3),
            CopilotConversationStatisticsWindow.All);

        Assert.Equal(6, snapshot.StoredConversations);
        Assert.Equal(5, snapshot.ActiveConversations);
        Assert.Equal(1, snapshot.UserTurns);
        Assert.Equal(5, snapshot.TrackedResponses);
        Assert.Equal(110, snapshot.Usage.InputTokens);
    }

    private static CopilotConversationBranchOrigin Origin(string parentId, string throughId, DateTimeOffset forkedAt) => new()
    {
        ParentConversationId = parentId,
        RootConversationId = parentId,
        ThroughMessageId = throughId,
        ForkedAtUtc = forkedAt,
    };

    private static CopilotChatMessage Response(string id, int inputTokens, DateTime createdAt)
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, id) { Id = id, CreatedAt = createdAt };
        message.SetReportedUsage(new CopilotTokenUsage(inputTokens, 0, inputTokens));
        return message;
    }
}
