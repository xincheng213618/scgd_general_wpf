using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationCopyTests
{
    [Fact]
    public void CopyCommandAcceptsAnOptionalOrdinalAndCanRunDuringAnAgentTask()
    {
        var latest = CopilotLocalCommandCatalog.Parse("/copy");
        var secondLatest = CopilotLocalCommandCatalog.Parse("/copy 2");

        Assert.NotNull(latest);
        Assert.Equal(CopilotLocalCommandKind.CopyResponse, latest.Command.Kind);
        Assert.Empty(latest.Arguments);
        Assert.True(latest.Command.AvailableWhileAgentRuns);
        Assert.NotNull(secondLatest);
        Assert.Equal("2", secondLatest.Arguments);
        Assert.True(secondLatest.Command.AvailableWhileAgentRuns);
    }

    [Theory]
    [InlineData(null, true, 1)]
    [InlineData("", true, 1)]
    [InlineData(" 2 ", true, 2)]
    [InlineData("0", false, 0)]
    [InlineData("-1", false, -1)]
    [InlineData("second", false, 0)]
    public void CopyOrdinalUsesOneBasedLatestFirstSemantics(
        string? value,
        bool expectedSuccess,
        int expectedOrdinal)
    {
        var success = CopilotConversationService.TryParseAssistantResponseOrdinal(value, out var ordinal);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedOrdinal, ordinal);
    }

    [Fact]
    public void CopySelectionSkipsUsersActiveResponsesAndInterruptedDisplayContent()
    {
        var oldestCompleted = new CopilotChatMessage(CopilotChatRole.Assistant, "oldest completed");
        var interrupted = new CopilotChatMessage(CopilotChatRole.Assistant, "partial")
        {
            WasResponseInterrupted = true,
        };
        var displayOnly = new CopilotChatMessage(CopilotChatRole.Assistant, "status only")
        {
            IsContentDisplayOnly = true,
        };
        var newestCompleted = new CopilotChatMessage(CopilotChatRole.Assistant, "newest completed");
        var active = new CopilotChatMessage(CopilotChatRole.Assistant, "streaming partial");
        active.MarkThinkingStarted();
        var conversation = new CopilotConversationRecord();
        conversation.Messages.Add(oldestCompleted);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "question"));
        conversation.Messages.Add(interrupted);
        conversation.Messages.Add(displayOnly);
        conversation.Messages.Add(newestCompleted);
        conversation.Messages.Add(active);

        Assert.Same(
            newestCompleted,
            CopilotConversationService.FindNthLatestCompletedAssistantResponse(conversation, 1));
        Assert.Same(
            oldestCompleted,
            CopilotConversationService.FindNthLatestCompletedAssistantResponse(conversation, 2));
        Assert.Null(CopilotConversationService.FindNthLatestCompletedAssistantResponse(conversation, 3));
        Assert.Null(CopilotConversationService.FindNthLatestCompletedAssistantResponse(conversation, 0));
    }
}
