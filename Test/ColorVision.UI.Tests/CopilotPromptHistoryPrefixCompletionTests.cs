using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotPromptHistoryPrefixCompletionTests
{
    [Fact]
    public void MostRecentMatchingUserPromptWins()
    {
        CopilotChatMessage[] messages =
        [
            new(CopilotChatRole.User, "run the focused tests"),
            new(CopilotChatRole.Assistant, "done"),
            new(CopilotChatRole.User, "run the full Copilot tests"),
        ];

        var resolved = CopilotPromptHistoryPrefixCompletionResolver.TryResolve(
            messages,
            "run the ",
            out var completion);

        Assert.True(resolved);
        Assert.Equal("run the full Copilot tests", completion.FullText);
        Assert.Equal("full Copilot tests", completion.Suffix);
    }

    [Fact]
    public void ExactAndAssistantMessagesAreNotSuggested()
    {
        CopilotChatMessage[] messages =
        [
            new(CopilotChatRole.User, "commit this"),
            new(CopilotChatRole.Assistant, "commit this after tests"),
        ];

        Assert.False(CopilotPromptHistoryPrefixCompletionResolver.TryResolve(
            messages,
            "commit this",
            out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" leading")]
    [InlineData("/rev")]
    [InlineData("$skill")]
    [InlineData("@file")]
    public void EmptyCommandAndReferencePrefixesAreIgnored(string input)
    {
        var messages = new[]
        {
            new CopilotChatMessage(CopilotChatRole.User, input + " completion"),
        };

        Assert.False(CopilotPromptHistoryPrefixCompletionResolver.TryResolve(
            messages,
            input,
            out _));
    }

    [Fact]
    public void MatchingIsCaseSensitiveToPreserveTypedPrefix()
    {
        var messages = new[]
        {
            new CopilotChatMessage(CopilotChatRole.User, "Run the tests"),
        };

        Assert.False(CopilotPromptHistoryPrefixCompletionResolver.TryResolve(
            messages,
            "run",
            out _));
    }
}
