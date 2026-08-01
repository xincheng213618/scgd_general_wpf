using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationFindNavigatorTests
{
    [Fact]
    public void FindCommandAcceptsOptionalTextDuringAnActiveRun()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/find target text");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.FindInConversation, invocation.Command.Kind);
        Assert.Equal("target text", invocation.Arguments);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
    }

    [Fact]
    public void RefreshMatchesVisibleConversationTextAndMetadata()
    {
        var hiddenOnly = new CopilotChatMessage(CopilotChatRole.User, "visible")
        {
            RequestContent = "hidden request target",
        };
        var content = new CopilotChatMessage(CopilotChatRole.Assistant, "TARGET in answer");
        var attachment = new CopilotChatMessage(CopilotChatRole.User, "attachment")
        {
            Attachments =
            {
                new CopilotAttachmentItem
                {
                    Title = "target-log.txt",
                    Type = CopilotAttachmentType.File,
                },
            },
        };
        var trace = new CopilotChatMessage(CopilotChatRole.Assistant, "tool")
        {
            AgentTraceEntries =
            {
                new CopilotAgentTraceEntry
                {
                    ToolName = "SearchTarget",
                },
            },
        };
        var navigator = new CopilotConversationFindNavigator();

        navigator.Refresh([hiddenOnly, content, attachment, trace], "target");

        Assert.Equal([content, attachment, trace], navigator.Matches);
        Assert.Same(content, navigator.Current);
        Assert.Equal(0, navigator.SelectedIndex);
    }

    [Fact]
    public void NavigationWrapsAndKeepsTheCurrentMatchAcrossRefresh()
    {
        var first = new CopilotChatMessage(CopilotChatRole.User, "match one");
        var second = new CopilotChatMessage(CopilotChatRole.Assistant, "match two");
        var third = new CopilotChatMessage(CopilotChatRole.User, "match three");
        var navigator = new CopilotConversationFindNavigator();

        navigator.Refresh([first, second], "MATCH");
        Assert.True(navigator.Move(previous: false));
        Assert.Same(second, navigator.Current);

        navigator.Refresh([first, second, third], "match");
        Assert.Same(second, navigator.Current);
        Assert.True(navigator.Move(previous: false));
        Assert.Same(third, navigator.Current);
        Assert.True(navigator.Move(previous: false));
        Assert.Same(first, navigator.Current);
        Assert.True(navigator.Move(previous: true));
        Assert.Same(third, navigator.Current);
    }

    [Fact]
    public void EmptyOrOverlongQueriesAreNormalizedSafely()
    {
        var navigator = new CopilotConversationFindNavigator();
        var message = new CopilotChatMessage(CopilotChatRole.User, "text");

        navigator.Refresh([message], "   ");
        Assert.Empty(navigator.Matches);
        Assert.Null(navigator.Current);
        Assert.False(navigator.Move(previous: false));

        var query = new string('a', CopilotConversationFindNavigator.MaximumQueryCharacters - 1) + "😀tail";
        var normalized = CopilotConversationFindNavigator.NormalizeQuery(query);
        Assert.True(normalized.Length <= CopilotConversationFindNavigator.MaximumQueryCharacters);
        Assert.False(char.IsHighSurrogate(normalized[^1]));
    }
}
