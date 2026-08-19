using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotConversationSurfaceProjectionTests
{
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

    [Fact]
    public void ContextDiagnosticsExposeTheDerivedMessageSurface()
    {
        var report = CopilotContextDiagnostics.Format(
            new CopilotContextDiagnosticSnapshot
            {
                CurrentModelSurfaceMessages = 4,
                ShadowedModelSurfaceMessages = 6,
                LogOnlySurfaceMessages = 2,
                HasCurrentCompactionSummary = true,
            });

        Assert.Contains(
            "模型消息表面：当前 4 + 1 条压缩摘要；已被摘要替代 6；仅本地日志 2。",
            report,
            StringComparison.Ordinal);
    }
}
