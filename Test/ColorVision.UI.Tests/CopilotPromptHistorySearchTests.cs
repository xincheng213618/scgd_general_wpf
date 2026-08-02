using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotPromptHistorySearchTests
{
    [Fact]
    public void HistoryCommandIsIdleOnlyAndRejectsArguments()
    {
        var command = CopilotLocalCommandCatalog.Parse("/history");

        Assert.NotNull(command);
        Assert.Equal(CopilotLocalCommandKind.SearchPromptHistory, command.Command.Kind);
        Assert.False(command.Command.AvailableWhileAgentRuns);
        Assert.False(command.Command.AcceptsArguments);
        Assert.Null(CopilotLocalCommandCatalog.Parse("/history camera"));
        Assert.False(CopilotLocalCommandAvailabilityPolicy.CanExecute(
            command.Command,
            CopilotLocalCommandComposerContext.ActiveRun));
    }

    [Fact]
    public void EmptyQueryReturnsRecentUniqueVisibleUserPromptsOnly()
    {
        var hidden = new CopilotChatMessage(CopilotChatRole.User, "Visible request")
        {
            RequestContent = "hidden request with private attachment body",
        };
        var messages = new[]
        {
            new CopilotChatMessage(CopilotChatRole.User, "First request"),
            new CopilotChatMessage(CopilotChatRole.Assistant, "Assistant answer"),
            new CopilotChatMessage(CopilotChatRole.User, "Repeated request"),
            hidden,
            new CopilotChatMessage(CopilotChatRole.User, "Repeated request"),
        };

        var results = CopilotPromptHistorySearch.Search(messages, null);

        Assert.Equal(
            ["Repeated request", "Visible request", "First request"],
            results.Select(item => item.Text));
        Assert.DoesNotContain(results, item => item.Text.Contains("hidden request", StringComparison.Ordinal));
        Assert.DoesNotContain(results, item => item.Preview.Contains("private attachment", StringComparison.Ordinal));
        Assert.DoesNotContain(results, item => item.Text.Contains("Assistant answer", StringComparison.Ordinal));
        Assert.All(results, item => Assert.False(item.HasSourceSummary));
    }

    [Theory]
    [InlineData("camera workflow")]
    [InlineData("ccw")]
    [InlineData("CAMERA")]
    public void SearchSupportsTermsSubsequencesAndCaseInsensitiveMatches(string query)
    {
        var messages = new[]
        {
            new CopilotChatMessage(CopilotChatRole.User, "Unrelated diagnostics"),
            new CopilotChatMessage(CopilotChatRole.User, "Camera calibration workflow"),
        };

        var result = Assert.Single(CopilotPromptHistorySearch.Search(messages, query));

        Assert.Equal("Camera calibration workflow", result.Text);
    }

    [Fact]
    public void SearchBoundsResultsAndSingleLinePreviewsWithoutChangingRestoredText()
    {
        var longPrompt = "Line one\r\n" + new string('x', 200);
        var messages = Enumerable.Range(0, CopilotPromptHistorySearch.MaximumResults + 4)
            .Select(index => new CopilotChatMessage(
                CopilotChatRole.User,
                index == 0 ? longPrompt : $"Prompt {index}"))
            .ToArray();

        var all = CopilotPromptHistorySearch.Search(messages, string.Empty);
        var longResult = Assert.Single(CopilotPromptHistorySearch.Search(messages, "Line one"));

        Assert.Equal(CopilotPromptHistorySearch.MaximumResults, all.Count);
        Assert.Equal(longPrompt, longResult.Text);
        Assert.DoesNotContain('\r', longResult.Preview);
        Assert.DoesNotContain('\n', longResult.Preview);
        Assert.EndsWith("…", longResult.Preview, StringComparison.Ordinal);
        Assert.True(longResult.Preview.Length <= CopilotPromptHistorySearch.MaximumPreviewCharacters + 1);
    }

    [Fact]
    public void AllConversationSearchUsesVisiblePromptsAndKeepsNewestDuplicate()
    {
        var firstConversation = new CopilotConversationRecord
        {
            Title = "Camera calibration",
        };
        firstConversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Shared request")
        {
            CreatedAt = new DateTime(2026, 7, 29, 10, 0, 0),
            RequestContent = "hidden first request",
        });
        firstConversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Older request")
        {
            CreatedAt = new DateTime(2026, 7, 29, 11, 0, 0),
        });

        var secondConversation = new CopilotConversationRecord
        {
            Title = "Flow diagnostics",
        };
        secondConversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Assistant answer")
        {
            CreatedAt = new DateTime(2026, 7, 30, 9, 0, 0),
        });
        secondConversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Shared request")
        {
            CreatedAt = new DateTime(2026, 7, 30, 10, 0, 0),
            RequestContent = "hidden newest request",
        });

        var results = CopilotPromptHistorySearch.SearchAll(
            [firstConversation, secondConversation],
            string.Empty);

        Assert.Equal(["Shared request", "Older request"], results.Select(item => item.Text));
        Assert.Equal("Flow diagnostics · 2026-07-30 10:00", results[0].SourceSummary);
        Assert.Equal("Camera calibration · 2026-07-29 11:00", results[1].SourceSummary);
        Assert.All(results, item => Assert.True(item.HasSourceSummary));
        Assert.DoesNotContain(results, item => item.Text.Contains("hidden", StringComparison.Ordinal));
        Assert.DoesNotContain(results, item => item.Text.Contains("Assistant", StringComparison.Ordinal));
    }

    [Fact]
    public void AllConversationSourceTitleIsBoundedWithoutSplittingSurrogatePairs()
    {
        var conversation = new CopilotConversationRecord
        {
            Title = new string('x', CopilotPromptHistorySearch.MaximumSourceTitleCharacters - 1) + "😀tail",
        };
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Inspect result")
        {
            CreatedAt = new DateTime(2026, 7, 31, 8, 30, 0),
        });

        var result = Assert.Single(CopilotPromptHistorySearch.SearchAll([conversation], string.Empty));
        var title = result.SourceSummary.Split(" · ", StringSplitOptions.None)[0];

        Assert.Equal(CopilotPromptHistorySearch.MaximumSourceTitleCharacters - 1, title.Length);
        Assert.DoesNotContain('�', title);
        Assert.EndsWith("x", title, StringComparison.Ordinal);
    }
}
