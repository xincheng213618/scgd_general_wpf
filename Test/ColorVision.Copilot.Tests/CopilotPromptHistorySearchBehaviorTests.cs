using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotPromptHistorySearchBehaviorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n ")]
    public void EmptyQueryReturnsTheLatestTwelveDistinctVisibleUserRequests(string? query)
    {
        var messages = new List<CopilotChatMessage>();
        for (var index = 0; index < 20; index++)
        {
            messages.Add(new CopilotChatMessage(CopilotChatRole.User, $" request {index} "));
            messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, $"assistant {index}"));
            messages.Add(new CopilotChatMessage(CopilotChatRole.User, " \t\n "));
        }
        messages.Add(new CopilotChatMessage(CopilotChatRole.User, "\nrequest 10\t"));

        var results = CopilotPromptHistorySearch.Search(messages, query);

        Assert.Equal(12, results.Count);
        Assert.Equal(new[] { 10, 19, 18, 17, 16, 15, 14, 13, 12, 11, 9, 8 }.Select(index => $"request {index}"), results.Select(item => item.Text));
        Assert.All(results, item => Assert.False(item.HasSourceSummary));
    }

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
    public void CrossConversationDeduplicationUsesLatestTimestampThenStableSourceOrder()
    {
        var sameTime = new DateTime(2026, 9, 8, 9, 30, 0);
        var latest = CreateConversation(" Latest \n title ",
            new CopilotChatMessage(CopilotChatRole.User, " shared request ") { CreatedAt = sameTime.AddMinutes(1) });
        var earlier = CreateConversation("Earlier",
            new CopilotChatMessage(CopilotChatRole.User, "shared request") { CreatedAt = sameTime },
            new CopilotChatMessage(CopilotChatRole.User, "first tied request") { CreatedAt = sameTime },
            new CopilotChatMessage(CopilotChatRole.Assistant, "Ignored newest assistant") { CreatedAt = sameTime.AddDays(1) });
        var last = CreateConversation("Last",
            new CopilotChatMessage(CopilotChatRole.User, "second tied request") { CreatedAt = sameTime },
            new CopilotChatMessage(CopilotChatRole.User, "third tied request") { CreatedAt = sameTime });

        var results = CopilotPromptHistorySearch.SearchAll([latest, earlier, last], "");

        Assert.Equal(new[] { "shared request", "third tied request", "second tied request", "first tied request" }, results.Select(item => item.Text));
        Assert.Equal("Latest title · 2026-09-08 09:31", results[0].SourceSummary);
        Assert.Equal("Last · 2026-09-08 09:30", results[1].SourceSummary);

        latest.Messages[0].CreatedAt = sameTime;
        var sameTimestamp = CopilotPromptHistorySearch.SearchAll([latest, earlier, last], "shared request");
        Assert.Equal("Earlier · 2026-09-08 09:30", Assert.Single(sameTimestamp).SourceSummary);
    }

    [Fact]
    public void CurrentConversationKeepsCollectionRecencyRegardlessOfMessageTimestamps()
    {
        var messages = new[]
        {
            new CopilotChatMessage(CopilotChatRole.User, "request first") { CreatedAt = new DateTime(2030, 1, 1) },
            new CopilotChatMessage(CopilotChatRole.User, "request later") { CreatedAt = default },
        };

        Assert.Equal(new[] { "request later", "request first" }, CopilotPromptHistorySearch.Search(messages, "request").Select(item => item.Text));
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

    [Fact]
    public void QueryLimitDoesNotSplitASurrogatePairAndEmptySourcesReturnNoResults()
    {
        var prefix = new string('a', CopilotPromptHistorySearch.MaximumQueryCharacters - 1);
        var message = new CopilotChatMessage(CopilotChatRole.User, prefix + " matches");

        Assert.Equal(message.Content, Assert.Single(CopilotPromptHistorySearch.Search([message], prefix + "😀ignored")).Text);
        Assert.Empty(CopilotPromptHistorySearch.Search(null, "request"));
        Assert.Empty(CopilotPromptHistorySearch.SearchAll(null, "request"));
        Assert.Empty(CopilotPromptHistorySearch.Search([message], "zzzzzz"));
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
