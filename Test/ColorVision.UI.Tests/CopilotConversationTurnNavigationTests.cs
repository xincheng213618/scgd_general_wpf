using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationTurnNavigationTests
{
    [Fact]
    public void TurnCommandAcceptsAnOptionalOrdinalDuringAgentRuns()
    {
        var latest = CopilotLocalCommandCatalog.Parse("/turn");
        var older = CopilotLocalCommandCatalog.Parse("/turn 3");

        Assert.NotNull(latest);
        Assert.Equal(CopilotLocalCommandKind.NavigateTurn, latest.Command.Kind);
        Assert.True(latest.Command.AvailableWhileAgentRuns);
        Assert.NotNull(older);
        Assert.Same(latest.Command, older.Command);
        Assert.Equal("3", older.Arguments);
    }

    [Fact]
    public void EmptyOrdinalTargetsLatestVisibleUserRequest()
    {
        var conversation = CreateConversation();

        var result = CopilotConversationTurnNavigation.Resolve(conversation, string.Empty);

        Assert.NotNull(result.Message);
        Assert.Equal("Third request", result.Message.Content);
        Assert.Empty(result.Report);
    }

    [Fact]
    public void ExplicitOrdinalReusesLatestFirstRewindPointOrdering()
    {
        var conversation = CreateConversation();

        var result = CopilotConversationTurnNavigation.Resolve(conversation, "2");

        Assert.NotNull(result.Message);
        Assert.Equal("Second request", result.Message.Content);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("4")]
    [InlineData("missing")]
    public void InvalidOrdinalDoesNotGuessOrExposeRequestContent(string ordinal)
    {
        var conversation = CreateConversation();

        var result = CopilotConversationTurnNavigation.Resolve(conversation, ordinal);

        Assert.Null(result.Message);
        Assert.Contains("当前会话有 3 条可定位的用户请求", result.Report);
        Assert.Contains("/turn 2", result.Report);
        Assert.DoesNotContain("First request", result.Report, StringComparison.Ordinal);
    }

    [Fact]
    public void EmptyConversationReportsNoNavigationTarget()
    {
        var result = CopilotConversationTurnNavigation.Resolve(null, "1");

        Assert.Null(result.Message);
        Assert.Contains("还没有可定位的用户请求", result.Report);
    }

    private static CopilotConversationRecord CreateConversation()
    {
        var conversation = new CopilotConversationRecord();
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "First request"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "First answer"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Second request"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Second answer"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Third request"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Third answer"));
        return conversation;
    }
}
