using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationFindSessionTests
{
    [Fact]
    public void OpenProjectsMatchesAndCurrentHighlight()
    {
        var first = new CopilotChatMessage(CopilotChatRole.User, "target one");
        var unrelated = new CopilotChatMessage(CopilotChatRole.Assistant, "other");
        var second = new CopilotChatMessage(CopilotChatRole.Assistant, "target two");
        var session = new CopilotConversationFindSession();

        Assert.True(session.Open([first, unrelated, second], "target"));

        Assert.True(session.IsOpen);
        Assert.True(session.HasQuery);
        Assert.True(session.HasMatches);
        Assert.Equal("1 / 2", session.StatusText);
        Assert.Same(first, session.Current);
        Assert.True(first.IsConversationFindMatch);
        Assert.True(first.IsCurrentConversationFindMatch);
        Assert.False(unrelated.IsConversationFindMatch);
        Assert.True(second.IsConversationFindMatch);
        Assert.False(second.IsCurrentConversationFindMatch);
    }

    [Fact]
    public void MoveWrapsAndKeepsOnlyOneCurrentHighlight()
    {
        var first = new CopilotChatMessage(CopilotChatRole.User, "match one");
        var second = new CopilotChatMessage(CopilotChatRole.Assistant, "match two");
        var messages = new[] { first, second };
        var session = new CopilotConversationFindSession();
        session.Open(messages, "match");

        Assert.True(session.Move(messages, previous: false));
        Assert.Same(second, session.Current);
        Assert.False(first.IsCurrentConversationFindMatch);
        Assert.True(second.IsCurrentConversationFindMatch);

        Assert.True(session.Move(messages, previous: false));
        Assert.Same(first, session.Current);
        Assert.True(first.IsCurrentConversationFindMatch);
        Assert.False(second.IsCurrentConversationFindMatch);
    }

    [Fact]
    public void RefreshPreservesAStillMatchingCurrentMessage()
    {
        var first = new CopilotChatMessage(CopilotChatRole.User, "match one");
        var second = new CopilotChatMessage(CopilotChatRole.Assistant, "match two");
        var third = new CopilotChatMessage(CopilotChatRole.User, "match three");
        var session = new CopilotConversationFindSession();
        session.Open([first, second], "match");
        session.Move([first, second], previous: false);

        Assert.True(session.Refresh([first, second, third]));

        Assert.Same(second, session.Current);
        Assert.False(first.IsCurrentConversationFindMatch);
        Assert.True(second.IsCurrentConversationFindMatch);
        Assert.False(third.IsCurrentConversationFindMatch);
        Assert.True(third.IsConversationFindMatch);
        Assert.Equal("2 / 3", session.StatusText);
    }

    [Fact]
    public void QueryChangeClearsStaleHighlights()
    {
        var oldMatch = new CopilotChatMessage(CopilotChatRole.User, "old target");
        var newMatch = new CopilotChatMessage(CopilotChatRole.Assistant, "new result");
        var messages = new[] { oldMatch, newMatch };
        var session = new CopilotConversationFindSession();
        session.Open(messages, "old");

        Assert.True(session.SetQuery(messages, "new"));

        Assert.False(oldMatch.IsConversationFindMatch);
        Assert.False(oldMatch.IsCurrentConversationFindMatch);
        Assert.True(newMatch.IsConversationFindMatch);
        Assert.True(newMatch.IsCurrentConversationFindMatch);
        Assert.Same(newMatch, session.Current);
    }

    [Fact]
    public void ConversationSwitchClearsOldHighlightsAndAppliesTheSameQueryToNewMessages()
    {
        var oldMessage = new CopilotChatMessage(CopilotChatRole.User, "shared old");
        var newMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "shared new");
        var session = new CopilotConversationFindSession();
        session.Open([oldMessage], "shared");

        CopilotConversationFindSession.ClearHighlights([oldMessage]);
        Assert.True(session.Refresh([newMessage]));

        Assert.False(oldMessage.IsConversationFindMatch);
        Assert.False(oldMessage.IsCurrentConversationFindMatch);
        Assert.True(newMessage.IsConversationFindMatch);
        Assert.True(newMessage.IsCurrentConversationFindMatch);
        Assert.Same(newMessage, session.Current);
        Assert.Equal("shared", session.Query);
    }

    [Fact]
    public void CloseClearsHighlightsButRetainsTheQuery()
    {
        var message = new CopilotChatMessage(CopilotChatRole.User, "target");
        var session = new CopilotConversationFindSession();
        session.Open([message], "target");

        Assert.True(session.Close([message]));

        Assert.False(session.IsOpen);
        Assert.False(session.HasMatches);
        Assert.Null(session.Current);
        Assert.Equal("target", session.Query);
        Assert.True(session.HasQuery);
        Assert.False(message.IsConversationFindMatch);
        Assert.False(message.IsCurrentConversationFindMatch);
        Assert.False(session.Close([message]));
    }

    [Fact]
    public void QueryNormalizationHandlesEmptyAndUnicodeBoundaries()
    {
        var session = new CopilotConversationFindSession();

        Assert.False(session.SetQuery([], "   "));
        Assert.Equal(string.Empty, session.Query);
        Assert.False(session.HasQuery);

        var query = new string('a', CopilotConversationFindNavigator.MaximumQueryCharacters - 1) + "😀tail";
        Assert.True(session.SetQuery([], query));
        Assert.True(session.Query.Length <= CopilotConversationFindNavigator.MaximumQueryCharacters);
        Assert.False(char.IsHighSurrogate(session.Query[^1]));
    }
}
