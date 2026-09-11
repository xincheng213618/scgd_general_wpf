using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotConversationSurfaceProjectionTests
{
    [Theory]
    [InlineData(0, false, 0)]
    [InlineData(1, false, 1)]
    [InlineData(2, true, 1)]
    [InlineData(3, true, 2)]
    [InlineData(-1, true, 2)]
    public void HistoryBoundsKeepStopBeforeSemanticsAcrossTheCompactionBoundary(int stopIndex, bool hasSummary, int expectedCount)
    {
        var conversation = new CopilotConversationRecord();
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "First"));
        var boundary = new CopilotChatMessage(CopilotChatRole.Assistant, "Boundary");
        conversation.Messages.Add(boundary);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Recent visible") { RequestContent = "Recent model" });
        conversation.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = "Carried history",
            ThroughMessageId = boundary.Id,
        };
        var stopBefore = stopIndex < 0
            ? new CopilotChatMessage(CopilotChatRole.User, "Not a member")
            : stopIndex < conversation.Messages.Count ? conversation.Messages[stopIndex] : null;

        var surface = CopilotConversationCompactionContext.CaptureSurface(conversation, stopBefore);
        var model = CopilotConversationCompactionContext.Build(conversation, stopBefore, useModelContent: true);
        var visible = CopilotConversationCompactionContext.Build(conversation, stopBefore, useModelContent: false);

        Assert.Equal(hasSummary, surface.HasCompactionSummary);
        Assert.Equal(expectedCount, model.Count);
        Assert.Equal(expectedCount, visible.Count);
        if (hasSummary)
            Assert.Contains("Carried history", model[0].Content, StringComparison.Ordinal);
        if (expectedCount == 2)
        {
            Assert.Equal("Recent model", model[1].Content);
            Assert.Equal("Recent visible", visible[1].Content);
        }
        if (!hasSummary && expectedCount == 1)
            Assert.Equal("First", model[0].Content);
    }

    [Fact]
    public void ProjectionSeparatesCurrentShadowedAndLogOnlyMessages()
    {
        var conversation = new CopilotConversationRecord();
        var firstUser = new CopilotChatMessage(
            CopilotChatRole.User,
            "First request");
        var firstAssistant = new CopilotChatMessage(
            CopilotChatRole.Assistant,
            "First answer");
        var displayOnly = new CopilotChatMessage(
            CopilotChatRole.Assistant,
            "Local diagnostic")
        {
            IsContentDisplayOnly = true,
        };
        var recentUser = new CopilotChatMessage(
            CopilotChatRole.User,
            "Recent request");
        var recentAssistant = new CopilotChatMessage(
            CopilotChatRole.Assistant,
            "Recent answer");
        conversation.Messages.Add(firstUser);
        conversation.Messages.Add(firstAssistant);
        conversation.Messages.Add(displayOnly);
        conversation.Messages.Add(recentUser);
        conversation.Messages.Add(recentAssistant);
        conversation.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion =
                CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = "Earlier work was completed.",
            ThroughMessageId = firstAssistant.Id,
            SourceMessageCount = 2,
            SourceCharacters = 25,
        };

        var surface =
            CopilotConversationCompactionContext.CaptureSurface(
                conversation);
        var modelHistory = CopilotConversationCompactionContext.Build(
            conversation,
            stopBeforeMessage: null,
            useModelContent: true);

        Assert.Equal(2, surface.CurrentMessages);
        Assert.Equal(2, surface.ShadowedMessages);
        Assert.Equal(1, surface.LogOnlyMessages);
        Assert.True(surface.HasCompactionSummary);
        Assert.Equal(3, modelHistory.Count);
        Assert.Contains(
            "Earlier conversation summary",
            modelHistory[0].Content,
            StringComparison.Ordinal);
        Assert.Equal("Recent request", modelHistory[1].Content);
        Assert.Equal("Recent answer", modelHistory[2].Content);
    }

    [Fact]
    public void MissingCompactionBoundaryCannotShadowCurrentMessages()
    {
        var conversation = new CopilotConversationRecord();
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.User,
            "Still current"));
        conversation.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion =
                CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = "Orphan summary",
            ThroughMessageId = "missing-message",
            SourceMessageCount = 1,
            SourceCharacters = 13,
        };

        var surface =
            CopilotConversationCompactionContext.CaptureSurface(
                conversation);
        var modelHistory = CopilotConversationCompactionContext.Build(
            conversation,
            stopBeforeMessage: null,
            useModelContent: true);

        Assert.Equal(1, surface.CurrentMessages);
        Assert.Equal(0, surface.ShadowedMessages);
        Assert.False(surface.HasCompactionSummary);
        Assert.Equal("Still current", Assert.Single(modelHistory).Content);
    }
}
