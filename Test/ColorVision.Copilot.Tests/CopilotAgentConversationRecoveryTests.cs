using Newtonsoft.Json;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAgentConversationRecoveryTests
{
    [Theory]
    [InlineData("中", 3_000)]
    [InlineData("😀", 4_500)]
    public void PersistedUnicodeMemoryKeepsTerminalEvidenceAcrossRepeatedRecovery(string fragment, int repetitions)
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant,
            "Partial answer " + string.Concat(Enumerable.Repeat(fragment, repetitions)))
        {
            RequestMode = CopilotAgentMode.Auto,
            AgentStopReason = CopilotAgentStopReason.Paused,
        };
        assistant.MarkResponseInterrupted();
        var modelContent = assistant.ModelContent;
        var terminalSuffix = modelContent[assistant.Content.Length..].Trim();
        var visibleHistory = new[]
        {
            Message("user", "initial goal"),
            Message("assistant", modelContent),
        };

        var normalized = CopilotAgentConversationMemory.SelectUnseenVisibleTail([], visibleHistory);
        Assert.EndsWith(terminalSuffix, normalized[^1].Content, StringComparison.Ordinal);
        _ = new UTF8Encoding(false, true).GetBytes(normalized[^1].Content);
        var checkpointMemory = CopilotAgentConversationMemory.Merge(
            [], visibleHistory, string.Empty, string.Empty);
        var persistedMemory = JsonConvert.DeserializeObject<CopilotRequestMessage[]>(
            JsonConvert.SerializeObject(checkpointMemory))!;

        Assert.EndsWith(terminalSuffix, persistedMemory[^1].Content, StringComparison.Ordinal);
        Assert.Empty(CopilotAgentConversationMemory.SelectUnseenVisibleTail(persistedMemory, visibleHistory));
        Assert.Equal(persistedMemory, CopilotAgentConversationMemory.Merge(
            persistedMemory, visibleHistory, string.Empty, string.Empty));
        Assert.Equal(modelContent, assistant.ModelContent);
    }

    [Theory]
    [InlineData("中", 3_000)]
    [InlineData("😀", 1_500)]
    public void SelectUnseenVisibleTailDoesNotReplayLongUnicodeCheckpointHistory(string fragment, int repetitions)
    {
        var visibleHistory = new[]
        {
            Message("user", "initial goal"),
            Message("assistant", string.Concat(Enumerable.Repeat(fragment, repetitions))),
        };
        var checkpointMemory = CopilotAgentConversationMemory.Merge(
            [], visibleHistory, string.Empty, string.Empty);

        Assert.True(checkpointMemory[^1].Content.Length < visibleHistory[^1].Content.Length);
        Assert.Empty(CopilotAgentConversationMemory.SelectUnseenVisibleTail(checkpointMemory, visibleHistory));
        Assert.Equal(checkpointMemory, CopilotAgentConversationMemory.Merge(
            checkpointMemory, visibleHistory, string.Empty, string.Empty));
    }

    [Theory]
    [InlineData("中", 3_000)]
    [InlineData("😀", 1_500)]
    public void SelectUnseenVisibleTailReturnsOnlyCompleteNewUnicodeTail(string fragment, int repetitions)
    {
        var longContent = string.Concat(Enumerable.Repeat(fragment, repetitions));
        var seenHistory = new[]
        {
            Message("user", "initial goal"),
            Message("assistant", longContent),
        };
        var checkpointMemory = CopilotAgentConversationMemory.Merge(
            [], seenHistory, string.Empty, string.Empty);
        var newMessage = new CopilotRequestMessage("user", longContent + " preserve this new instruction")
        {
            IsSteering = true,
        };

        var unseen = CopilotAgentConversationMemory.SelectUnseenVisibleTail(
            checkpointMemory, seenHistory.Append(newMessage));

        Assert.Equal(newMessage, Assert.Single(unseen));
    }

    [Theory]
    [InlineData("中", 2_500)]
    [InlineData("😀", 1_250)]
    [InlineData("x", 7_500)]
    public void CompleteCheckpointMessagesWithDifferentConclusionsRemainDistinct(string fragment, int repetitions)
    {
        var prefix = string.Concat(Enumerable.Repeat(fragment, repetitions));
        // Legacy checkpoints accept complete content within the character limit,
        // including Unicode text exceeding the current weighted memory limit.
        var checkpointMemory = new[]
        {
            Message("user", "initial goal"),
            Message("assistant", prefix + " previous conclusion"),
        };
        var revisedMessage = Message("assistant", prefix + " revised conclusion");
        var visibleHistory = new[] { checkpointMemory[0], revisedMessage };

        Assert.Equal(revisedMessage, Assert.Single(
            CopilotAgentConversationMemory.SelectUnseenVisibleTail(checkpointMemory, visibleHistory)));
        var merged = CopilotAgentConversationMemory.Merge(
            checkpointMemory, visibleHistory, string.Empty, string.Empty);

        Assert.Equal(3, merged.Count);
        Assert.Equal(CopilotAgentConversationMemory.Merge(
            [], checkpointMemory.Append(revisedMessage), string.Empty, string.Empty), merged);
        Assert.Equal(prefix + " previous conclusion", checkpointMemory[^1].Content);
    }

    [Fact]
    public void SelectUnseenVisibleTailAlignsBoundedCheckpointWithLongerHistory()
    {
        var checkpointMemory = new[]
        {
            Message("user", "initial goal"),
            Message("assistant", "third answer"),
            Message("user", "fourth request"),
            Message("assistant", "fourth answer"),
        };
        var visibleHistory = new[]
        {
            Message("user", "initial goal"),
            Message("assistant", "first answer"),
            Message("user", "second request"),
            Message("assistant", "second answer"),
            Message("assistant", "third answer"),
            Message("user", "fourth request"),
            Message("assistant", "fourth answer"),
            Message("user", "fifth request"),
        };

        var unseen = CopilotAgentConversationMemory.SelectUnseenVisibleTail(
            checkpointMemory,
            visibleHistory);

        Assert.Collection(
            unseen,
            message => AssertMessage(message, "user", "fifth request"));
    }

    [Fact]
    public void SelectUnseenVisibleTailPreservesRepeatedTurnAfterExactCheckpointPrefix()
    {
        var checkpointMemory = new[]
        {
            Message("user", "initial goal"),
            Message("user", "continue"),
            Message("assistant", "done"),
        };
        var visibleHistory = new[]
        {
            Message("user", "initial goal"),
            Message("user", "continue"),
            Message("assistant", "done"),
            Message("user", "continue"),
            Message("assistant", "done"),
        };

        var unseen = CopilotAgentConversationMemory.SelectUnseenVisibleTail(
            checkpointMemory,
            visibleHistory);

        Assert.Collection(
            unseen,
            message => AssertMessage(message, "user", "continue"),
            message => AssertMessage(message, "assistant", "done"));
    }

    [Fact]
    public void SelectUnseenVisibleTailDoesNotReplayAnOlderVisiblePrefix()
    {
        var checkpointMemory = new[]
        {
            Message("user", "initial goal"),
            Message("assistant", "first answer"),
            Message("user", "second request"),
        };
        var visibleHistory = new[]
        {
            Message("user", "initial goal"),
            Message("assistant", "first answer"),
        };

        var unseen = CopilotAgentConversationMemory.SelectUnseenVisibleTail(
            checkpointMemory,
            visibleHistory);

        Assert.Empty(unseen);
    }

    private static CopilotRequestMessage Message(string role, string content) => new(role, content);

    private static void AssertMessage(CopilotRequestMessage message, string role, string content)
    {
        Assert.Equal(role, message.Role);
        Assert.Equal(content, message.Content);
    }
}
