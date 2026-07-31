using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotSteeringRecoveryTests
{
    [Fact]
    public void EmptyDraftRestoresSingleMessageExactly()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");

        Assert.True(CopilotSteeringRecovery.RestoreToDraft(
            conversation,
            ["  keep this instruction  "]));

        Assert.Equal("keep this instruction", conversation.DraftText);
    }

    [Fact]
    public void ExistingDraftIsPreservedBeforeNumberedRecoveryNotice()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.DraftText = "正在编辑的新草稿";

        Assert.True(CopilotSteeringRecovery.RestoreToDraft(
            conversation,
            ["先检查状态", "再继续修复"]));

        Assert.StartsWith("正在编辑的新草稿", conversation.DraftText, StringComparison.Ordinal);
        Assert.Contains("以下运行中指令尚未送达，请检查后重新发送：", conversation.DraftText, StringComparison.Ordinal);
        Assert.Contains("1. 先检查状态", conversation.DraftText, StringComparison.Ordinal);
        Assert.Contains("2. 再继续修复", conversation.DraftText, StringComparison.Ordinal);
    }

    [Fact]
    public void SameTextDraftAndRecoveryRemainDistinctOccurrences()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.DraftText = "重复指令";

        Assert.True(CopilotSteeringRecovery.RestoreToDraft(
            conversation,
            ["重复指令"]));

        Assert.StartsWith("重复指令", conversation.DraftText, StringComparison.Ordinal);
        Assert.Contains("以下运行中指令尚未送达，请检查后重新发送：", conversation.DraftText, StringComparison.Ordinal);
        Assert.EndsWith("1. 重复指令", conversation.DraftText, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryEventBoundsAndCopiesMessages()
    {
        var messages = Enumerable.Range(1, 10)
            .Select(index => $"steering {index}")
            .ToList();

        var agentEvent = CopilotAgentEvent.SteeringRecovery(messages);
        messages[0] = "mutated";

        Assert.Equal(CopilotAgentEventType.SteeringRecovery, agentEvent.Type);
        Assert.Equal(8, agentEvent.SteeringMessages.Count);
        Assert.Equal("steering 1", agentEvent.SteeringMessages[0]);
    }
}
