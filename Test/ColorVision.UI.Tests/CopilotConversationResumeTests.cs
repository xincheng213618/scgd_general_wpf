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

    [Fact]
    public void SearchNavigationStartsAtTheRelevantEdgeAndClampsWithinResults()
    {
        Assert.Equal(-1, CopilotConversationService.ResolveSearchNavigationIndex(0, -1, false, 1));
        Assert.Equal(0, CopilotConversationService.ResolveSearchNavigationIndex(4, 2, false, 1));
        Assert.Equal(3, CopilotConversationService.ResolveSearchNavigationIndex(4, 2, false, -1));
        Assert.Equal(2, CopilotConversationService.ResolveSearchNavigationIndex(4, 1, true, 1));
        Assert.Equal(0, CopilotConversationService.ResolveSearchNavigationIndex(4, 0, true, -1));
        Assert.Equal(3, CopilotConversationService.ResolveSearchNavigationIndex(4, 3, true, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CopilotConversationService.ResolveSearchNavigationIndex(4, 0, true, 0));
    }

    [Fact]
    public void SearchCommitUsesThePreviewOrDefaultsToTheFirstResult()
    {
        Assert.Equal(-1, CopilotConversationService.ResolveSearchCommitIndex(0, -1, false));
        Assert.Equal(0, CopilotConversationService.ResolveSearchCommitIndex(3, 2, false));
        Assert.Equal(2, CopilotConversationService.ResolveSearchCommitIndex(3, 2, true));
        Assert.Equal(0, CopilotConversationService.ResolveSearchCommitIndex(3, 9, true));
    }
}
