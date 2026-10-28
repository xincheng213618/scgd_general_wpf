namespace ColorVision.Copilot.Tests;

public sealed class CopilotPromptHistorySearchBehaviorTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RankingKeepsExactPrefixSubstringTermsAndSubsequenceOrder(bool allConversations)
    {
        var texts = new[] { "alpha beta", "alpha beta plus", "inspect alpha beta", "beta then alpha", "a_l_p_h_a_ b_e_t_a", "zzzzzz" };
        var conversation = CreateConversation("Ranking", texts.Select(text => new CopilotChatMessage(CopilotChatRole.User, text)).ToArray());

        var results = allConversations
            ? CopilotPromptHistorySearch.SearchAll([conversation], " ALPHA \n BETA ")
            : CopilotPromptHistorySearch.Search(conversation.Messages, " ALPHA \n BETA ");

        Assert.Equal(texts.Take(5), results.Select(item => item.Text));
    }

    [Fact]
    public void VisibleBodyRestoresInFullWhileOnlyPreviewAndSourceAreNormalizedAndBounded()
    {
        var body = new string('x', 139) + "😀" + "\n  line two\t" + new string('y', 200);
        var title = new string('t', 79) + "😀 additional title";
        var message = new CopilotChatMessage(CopilotChatRole.User, " \n" + body + " \t")
        {
            CreatedAt = default,
            RequestContent = "hidden payload",
            IsContentDisplayOnly = true,
        };
        var conversation = CreateConversation(title, message);

        var result = Assert.Single(CopilotPromptHistorySearch.SearchAll([conversation], "line two"));

        Assert.Equal(body, result.Text);
        Assert.Equal(new string('x', 139) + "…", result.Preview);
        Assert.Equal(new string('t', 79), result.SourceSummary);
        Assert.DoesNotContain("hidden", result.Text, StringComparison.Ordinal);
        Assert.Empty(CopilotPromptHistorySearch.SearchAll([conversation], "hidden payload"));
        conversation.Title = " \t\n ";
        Assert.Equal(CopilotUiText.NewConversationTitle, Assert.Single(CopilotPromptHistorySearch.SearchAll([conversation], null)).SourceSummary);
    }

    [Fact]
    public void DeduplicationTrimsEdgesButKeepsCaseAndInternalWhitespaceDistinct()
    {
        var messages = new[] { "alpha beta", "Alpha beta", "alpha  beta", " \nalpha beta\t" }
            .Select(text => new CopilotChatMessage(CopilotChatRole.User, text));

        var results = CopilotPromptHistorySearch.Search(messages, "alpha beta");

        Assert.Equal(new[] { "alpha beta", "alpha  beta", "Alpha beta" }, results.Select(item => item.Text));
        Assert.All(results, item => Assert.Equal("alpha beta", item.Preview, ignoreCase: true));
    }

    private static CopilotConversationRecord CreateConversation(string title, params CopilotChatMessage[] messages)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Title = title;
        foreach (var message in messages)
            conversation.Messages.Add(message);
        return conversation;
    }
}
