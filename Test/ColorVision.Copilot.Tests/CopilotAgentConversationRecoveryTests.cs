using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAgentConversationRecoveryTests
{
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
