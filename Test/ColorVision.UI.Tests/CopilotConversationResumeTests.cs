using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationResumeTests
{
    [Fact]
    public void ResumeCommandAcceptsAnOptionalSearchAndCanRunDuringAnAgentTask()
    {
        var withoutQuery = CopilotLocalCommandCatalog.Parse("/resume");
        var withQuery = CopilotLocalCommandCatalog.Parse("/resume camera calibration");

        Assert.NotNull(withoutQuery);
        Assert.Equal(CopilotLocalCommandKind.ResumeConversation, withoutQuery.Command.Kind);
        Assert.Empty(withoutQuery.Arguments);
        Assert.True(withoutQuery.Command.AvailableWhileAgentRuns);
        Assert.NotNull(withQuery);
        Assert.Equal("camera calibration", withQuery.Arguments);
        Assert.True(withQuery.Command.AvailableWhileAgentRuns);
    }

    [Fact]
    public void UniqueResumeTargetPrefersIdAndRejectsAmbiguousTitles()
    {
        var byId = new CopilotConversationRecord
        {
            Id = "conversation-id",
            Title = "Primary",
        };
        var titleLooksLikeId = new CopilotConversationRecord
        {
            Id = "another-id",
            Title = "conversation-id",
        };
        var duplicateTitle1 = new CopilotConversationRecord
        {
            Id = "duplicate-1",
            Title = "Shared title",
        };
        var duplicateTitle2 = new CopilotConversationRecord
        {
            Id = "duplicate-2",
            Title = "shared TITLE",
        };
        var uniqueTitle = new CopilotConversationRecord
        {
            Id = "unique-id",
            Title = "Camera calibration",
        };
        CopilotConversationRecord[] conversations =
        [
            titleLooksLikeId,
            duplicateTitle1,
            uniqueTitle,
            byId,
            duplicateTitle2,
        ];

        Assert.Same(
            byId,
            CopilotConversationService.FindUniqueResumeTarget(conversations, "conversation-id"));
        Assert.Same(
            uniqueTitle,
            CopilotConversationService.FindUniqueResumeTarget(conversations, " camera CALIBRATION "));
        Assert.Null(CopilotConversationService.FindUniqueResumeTarget(conversations, "shared title"));
        Assert.Null(CopilotConversationService.FindUniqueResumeTarget(conversations, "missing"));
        Assert.Null(CopilotConversationService.FindUniqueResumeTarget(conversations, "   "));
    }
}
