namespace ColorVision.Copilot.Tests;

public sealed class CopilotConversationHistoryWindowTests
{
    [Theory]
    [InlineData('a', 1)]
    [InlineData('中', 4)]
    public void WeightReductionKeepsTheFirstGoalAndLatestTurnWhileRemovingOlderPairs(char character, int characterWeight)
    {
        var content = new string(character, 4);
        CopilotRequestMessage[] history =
        [
            new("user", content + "0"),
            new("assistant", content + "1"),
            new("user", content + "2"),
            new("assistant", content + "3"),
            new("user", content + "4") { IsSteering = true },
            new("assistant", content + "5"),
        ];
        var budget = 3 * (4 * characterWeight + 1);

        var selected = CopilotConversationHistoryWindow.SelectWithDiagnostics(history, 6, budget, budget);

        Assert.Equal(new[] { history[0], history[4], history[5] }, selected.Messages);
        Assert.Equal(6, selected.SourceMessageCount);
        Assert.Equal(30, selected.SourceCharacters);
        Assert.Equal(15, selected.RetainedCharacters);
        Assert.True(selected.WasReduced);
        Assert.True(history[4].IsSteering);
        Assert.Equal(6, history.Length);
    }

    [Theory]
    [InlineData("assistant,assistant,assistant", 4, "2")]
    [InlineData("user,assistant,assistant", 8, "0,2")]
    [InlineData("user,user,user,assistant", 12, "0,2,3")]
    public void WeightReductionPreservesTheProtectedMessagesForUnevenRoleSequences(string roles, int budget, string retainedIndexes)
    {
        var history = roles.Split(',')
            .Select((role, index) => new CopilotRequestMessage(role, new string((char)('a' + index), 4)))
            .ToArray();
        var expected = retainedIndexes.Split(',').Select(index => history[int.Parse(index)]).ToArray();

        var selected = CopilotConversationHistoryWindow.SelectWithDiagnostics(history, history.Length, budget, budget);

        Assert.Equal(expected, selected.Messages);
        Assert.Equal(history.Length, selected.SourceMessageCount);
        Assert.Equal(4 * expected.Length, selected.RetainedCharacters);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedBudgetFallbackPreservesTerminalEvidenceOrRejectsAnInsufficientShare(bool fitsExactly)
    {
        var marker = CopilotChatMessage.ResponseInterruptionModelMarker;
        var markerWeight = checked((int)CopilotTokenEstimator.EstimateTextWeight(marker));
        var perMessageBudget = markerWeight - (fitsExactly ? 0 : 1);
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, new string('a', markerWeight + 40));
        assistant.MarkResponseInterrupted();
        CopilotRequestMessage[] history =
        [
            new("user", new string('g', markerWeight + 40)),
            new("user", new string('s', markerWeight + 40)) { IsSteering = true },
            new("assistant", assistant.ModelContent),
        ];
        var originalAssistantContent = history[2].Content;
        var budget = 3 * perMessageBudget;

        if (fitsExactly)
        {
            var selected = CopilotConversationHistoryWindow.SelectWithDiagnostics(history, 3, budget, budget);

            Assert.Equal(new[] { "user", "user", "assistant" }, selected.Messages.Select(message => message.Role));
            Assert.True(selected.Messages[1].IsSteering);
            Assert.Equal(marker, selected.Messages[2].Content);
            Assert.All(selected.Messages, message => Assert.True(CopilotTokenEstimator.EstimateTextWeight(message.Content) <= perMessageBudget));
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => CopilotConversationHistoryWindow.SelectWithDiagnostics(history, 3, budget, budget));
        }

        Assert.Equal(originalAssistantContent, history[2].Content);
        Assert.True(assistant.WasResponseInterrupted);
    }
}
