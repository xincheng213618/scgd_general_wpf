using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentRunStatusSynchronizerTests
{
    [Fact]
    public void Refresh_LabelsActiveAndQueuedConversationsAndClearsStaleStatus()
    {
        var active = CreateConversation("active");
        var firstQueued = CreateConversation("queued-1");
        var secondQueued = CreateConversation("queued-2");
        var idle = CreateConversation("idle");
        CopilotAgentRunStatusSynchronizer.Refresh(
            [active, firstQueued, secondQueued, idle],
            idle.Id,
            CopilotHostedRunState.Running,
            []);

        CopilotAgentRunStatusSynchronizer.Refresh(
            [active, firstQueued, secondQueued, idle],
            active.Id,
            CopilotHostedRunState.Running,
            [firstQueued.Id, secondQueued.Id]);

        Assert.Equal("运行中", active.AgentRunStatusLabel);
        Assert.Equal("排队 1", firstQueued.AgentRunStatusLabel);
        Assert.Equal("排队 2", secondQueued.AgentRunStatusLabel);
        Assert.Empty(idle.AgentRunStatusLabel);
        Assert.False(idle.HasAgentRunStatus);
    }

    [Theory]
    [InlineData(CopilotHostedRunState.Running, "运行中")]
    [InlineData(CopilotHostedRunState.PauseRequested, "暂停中")]
    [InlineData(CopilotHostedRunState.CancelRequested, "取消中")]
    [InlineData(CopilotHostedRunState.Queued, "")]
    [InlineData(CopilotHostedRunState.Completed, "")]
    public void FormatActiveState_UsesOnlyActionableLiveStates(CopilotHostedRunState state, string expected)
    {
        Assert.Equal(expected, CopilotAgentRunStatusSynchronizer.FormatActiveState(state));
    }

    [Fact]
    public void AgentRunStatus_IsTransientChatState()
    {
        var conversation = CreateConversation("active");
        CopilotAgentRunStatusSynchronizer.Refresh(
            [conversation],
            conversation.Id,
            CopilotHostedRunState.Running,
            []);

        var json = JsonConvert.SerializeObject(conversation);

        Assert.DoesNotContain(nameof(CopilotConversationRecord.AgentRunStatusLabel), json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(CopilotConversationRecord.HasAgentRunStatus), json, StringComparison.Ordinal);
    }

    private static CopilotConversationRecord CreateConversation(string id)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        conversation.Id = id;
        return conversation;
    }
}
