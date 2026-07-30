using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationRenameTests
{
    [Fact]
    public void RenameCommandAcceptsAnOptionalTitleAndCanRunDuringAnAgentTask()
    {
        var withoutTitle = CopilotLocalCommandCatalog.Parse("/rename");
        var withTitle = CopilotLocalCommandCatalog.Parse("/rename Camera calibration");

        Assert.NotNull(withoutTitle);
        Assert.Equal(CopilotLocalCommandKind.RenameConversation, withoutTitle.Command.Kind);
        Assert.Empty(withoutTitle.Arguments);
        Assert.True(withoutTitle.Command.AvailableWhileAgentRuns);
        Assert.NotNull(withTitle);
        Assert.Equal("Camera calibration", withTitle.Arguments);
        Assert.True(withTitle.Command.AvailableWhileAgentRuns);
    }

    [Fact]
    public void BareSlashKeepsEveryFixedCommandVisible()
    {
        var suggestions = CopilotLocalCommandCatalog.Suggest("/");

        Assert.Equal(CopilotLocalCommandCatalog.All.Count, suggestions.Count);
        Assert.Contains(suggestions, command => command.Name == "/rename");
        Assert.Contains(suggestions, command => command.Name == "/btw");
    }

    [Fact]
    public void CustomTitleValidationTrimsAndRejectsEmptyOrOversizedValues()
    {
        Assert.False(CopilotConversationRecord.TryNormalizeCustomTitle(null, out _));
        Assert.False(CopilotConversationRecord.TryNormalizeCustomTitle("   ", out _));
        Assert.True(CopilotConversationRecord.TryNormalizeCustomTitle("  Camera calibration  ", out var normalized));
        Assert.Equal("Camera calibration", normalized);
        Assert.True(CopilotConversationRecord.TryNormalizeCustomTitle(
            new string('a', CopilotConversationRecord.MaximumTitleCharacters),
            out _));
        Assert.False(CopilotConversationRecord.TryNormalizeCustomTitle(
            new string('a', CopilotConversationRecord.MaximumTitleCharacters + 1),
            out _));
    }

    [Fact]
    public void ManualTitleSurvivesConversationSummaryRefresh()
    {
        var conversation = new CopilotConversationRecord();

        conversation.SetCustomTitle("  Camera calibration  ");
        conversation.RefreshSummary();

        Assert.True(conversation.HasCustomTitle);
        Assert.Equal("Camera calibration", conversation.Title);
    }
}
