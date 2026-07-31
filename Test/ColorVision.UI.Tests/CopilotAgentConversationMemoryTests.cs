using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentConversationMemoryTests
{
    [Fact]
    public void MergePreservesRepeatedVisibleTurnsOnTheFirstCheckpoint()
    {
        var visibleHistory = new[]
        {
            new CopilotRequestMessage("user", "continue"),
            new CopilotRequestMessage("assistant", "done"),
            new CopilotRequestMessage("user", "continue"),
            new CopilotRequestMessage("assistant", "done"),
        };

        var memory = CopilotAgentConversationMemory.Merge(
            null,
            visibleHistory,
            string.Empty,
            string.Empty);

        Assert.Collection(
            memory,
            item => AssertMessage(item, "user", "continue"),
            item => AssertMessage(item, "assistant", "done"),
            item => AssertMessage(item, "user", "continue"),
            item => AssertMessage(item, "assistant", "done"));
    }

    [Fact]
    public void MergeSkipsOnlyRepeatedTurnsAlreadyRepresentedByTheCheckpoint()
    {
        var previousMemory = new[]
        {
            new CopilotRequestMessage("user", "continue"),
            new CopilotRequestMessage("assistant", "done"),
        };
        var visibleHistory = new[]
        {
            new CopilotRequestMessage("user", "continue"),
            new CopilotRequestMessage("assistant", "done"),
            new CopilotRequestMessage("user", "continue"),
            new CopilotRequestMessage("assistant", "done"),
        };

        var memory = CopilotAgentConversationMemory.Merge(
            previousMemory,
            visibleHistory,
            string.Empty,
            string.Empty);

        Assert.Collection(
            memory,
            item => AssertMessage(item, "user", "continue"),
            item => AssertMessage(item, "assistant", "done"),
            item => AssertMessage(item, "user", "continue"),
            item => AssertMessage(item, "assistant", "done"));
    }

    [Fact]
    public void MergePlacesRepeatedUserFollowUpsBeforeAssistantInOrder()
    {
        var memory = CopilotAgentConversationMemory.Merge(
            null,
            null,
            "initial request",
            "final answer",
            ["same steering", "same steering", "third steering"]);

        Assert.Collection(
            memory,
            item => AssertMessage(item, "user", "initial request"),
            item => AssertMessage(item, "user", "same steering"),
            item => AssertMessage(item, "user", "same steering"),
            item => AssertMessage(item, "user", "third steering"),
            item => AssertMessage(item, "assistant", "final answer"));
    }

    [Fact]
    public void MergeIntoPreparedPromptRestoresFollowUpsBeforeCurrentRequest()
    {
        var memory = CopilotAgentConversationMemory.Merge(
            null,
            null,
            "initial request",
            "final answer",
            ["delivered steering"]);

        var prompt = CopilotAgentConversationMemory.MergeIntoPreparedPrompt(
            memory,
            [new CopilotRequestMessage("user", "retry request")]);

        Assert.Collection(
            prompt,
            item => AssertMessage(item, "user", "initial request"),
            item => AssertMessage(item, "user", "delivered steering"),
            item => AssertMessage(item, "assistant", "final answer"),
            item => AssertMessage(item, "user", "retry request"));
    }

    [Fact]
    public void MergeBoundsEachFollowUpContent()
    {
        var memory = CopilotAgentConversationMemory.Merge(
            null,
            null,
            "initial request",
            "final answer",
            [new string(
                'x',
                CopilotAgentSessionCheckpoint.MaxConversationMemoryContentLength + 1)]);

        var followUp = Assert.Single(
            memory,
            item => item.Role == "user"
                && item.Content.Contains(
                    "<conversation memory truncated>",
                    StringComparison.Ordinal));
        Assert.Equal(
            CopilotAgentSessionCheckpoint.MaxConversationMemoryContentLength,
            followUp.Content.Length);
    }

    [Fact]
    public void MergeBoundsFollowUpCountWhilePreservingInitialRequest()
    {
        var memory = CopilotAgentConversationMemory.Merge(
            null,
            null,
            "initial request",
            "final answer",
            Enumerable.Range(1, 20).Select(index => $"steering {index}"));

        Assert.True(
            memory.Count
                <= CopilotAgentSessionCheckpoint.MaxConversationMemoryMessages);
        Assert.Equal("initial request", memory[0].Content);
        Assert.Equal("final answer", memory[^1].Content);
        Assert.Contains(memory, item => item.Content == "steering 20");
    }

    [Fact]
    public void SelectBoundedUserFollowUpsCapsTheActiveRecoveryCache()
    {
        var followUps = CopilotAgentConversationMemory
            .SelectBoundedUserFollowUps(
                Enumerable.Range(1, 20)
                    .Select(index => $"{index}:" + new string('x', 8_000)));

        Assert.True(
            followUps.Count
                <= CopilotAgentSessionCheckpoint.MaxConversationMemoryMessages);
        Assert.True(
            followUps.Sum(content => content.Length)
                <= CopilotAgentSessionCheckpoint.MaxConversationMemoryCharacters);
        Assert.All(
            followUps,
            content => Assert.True(
                content.Length
                    <= CopilotAgentSessionCheckpoint.MaxConversationMemoryContentLength));
        Assert.Contains(followUps, content => content.StartsWith("20:", StringComparison.Ordinal));
    }

    private static void AssertMessage(
        CopilotRequestMessage message,
        string role,
        string content)
    {
        Assert.Equal(role, message.Role);
        Assert.Equal(content, message.Content);
    }
}
