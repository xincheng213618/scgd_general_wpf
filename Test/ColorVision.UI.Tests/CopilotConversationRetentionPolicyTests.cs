using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationRetentionPolicyTests
{
    [Theory]
    [InlineData(true, false, false, false, (int)CopilotConversationRetentionBlocker.ScheduledRun)]
    [InlineData(false, true, false, false, (int)CopilotConversationRetentionBlocker.PendingApproval)]
    [InlineData(false, false, true, false, (int)CopilotConversationRetentionBlocker.QueuedFollowUp)]
    [InlineData(false, false, false, true, (int)CopilotConversationRetentionBlocker.MessageEdit)]
    [InlineData(false, false, false, false, (int)CopilotConversationRetentionBlocker.None)]
    public void TransientStateProtectsConversationRetention(
        bool hasScheduledRun,
        bool hasPendingApproval,
        bool hasQueuedFollowUp,
        bool isEditingMessage,
        int expectedValue)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");

        Assert.Equal(
            (CopilotConversationRetentionBlocker)expectedValue,
            CopilotConversationRetentionPolicy.Evaluate(
                conversation,
                hasScheduledRun,
                hasPendingApproval,
                hasQueuedFollowUp,
                isEditingMessage));
    }

    [Fact]
    public void ActiveGoalAndCheckpointRequireExplicitResolution()
    {
        var goalConversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        goalConversation.Goal = CopilotConversationGoal.Create("finish the task", DateTimeOffset.UtcNow);
        var checkpointConversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        checkpointConversation.AgentSessionCheckpoint = new CopilotAgentSessionCheckpoint();

        Assert.Equal(
            CopilotConversationRetentionBlocker.ActiveGoal,
            CopilotConversationRetentionPolicy.Evaluate(
                goalConversation,
                hasScheduledRun: false,
                hasPendingApproval: false,
                hasQueuedFollowUp: false,
                isEditingMessage: false));
        Assert.Equal(
            CopilotConversationRetentionBlocker.RecoverableAgentTask,
            CopilotConversationRetentionPolicy.Evaluate(
                checkpointConversation,
                hasScheduledRun: false,
                hasPendingApproval: false,
                hasQueuedFollowUp: false,
                isEditingMessage: false));
    }

    [Fact]
    public void NearestActiveReplacementSkipsArchivedConversations()
    {
        var previous = CreateConversation("previous", archived: false);
        var archived = CreateConversation("archived", archived: true);
        var next = CreateConversation("next", archived: false);

        Assert.Same(
            next,
            CopilotConversationRetentionPolicy.FindNearestActive(
                [previous, archived, next],
                preferredIndex: 1));
        Assert.Same(
            previous,
            CopilotConversationRetentionPolicy.FindNearestActive(
                [previous, archived],
                preferredIndex: 1));
        Assert.Null(CopilotConversationRetentionPolicy.FindNearestActive(
            [archived],
            preferredIndex: 0));
    }

    [Fact]
    public void BlockerDescriptionsAreActionable()
    {
        foreach (var blocker in Enum.GetValues<CopilotConversationRetentionBlocker>()
                     .Where(item => item != CopilotConversationRetentionBlocker.None))
        {
            Assert.False(string.IsNullOrWhiteSpace(
                CopilotConversationRetentionPolicy.Describe(blocker)));
        }
    }

    private static CopilotConversationRecord CreateConversation(string id, bool archived)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Id = id;
        conversation.IsArchived = archived;
        return conversation;
    }
}
