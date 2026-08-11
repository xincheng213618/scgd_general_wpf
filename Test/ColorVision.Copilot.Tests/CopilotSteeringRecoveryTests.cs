using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotSteeringRecoveryTests
{
    [Fact]
    public void FinalCheckpointRequiresANewMatchingSteeringOccurrence()
    {
        var message = new CopilotSteeringMessageSnapshot("steering:2", "keep the constraint");
        var previousCheckpoint = CreateCheckpoint(
        [
            CreateSteeringMessage("keep the constraint"),
        ]);

        Assert.False(CopilotSteeringRecovery.AreNewMessagesIncludedInCheckpoint(
            previousCheckpoint,
            CreateCheckpoint(
            [
                CreateSteeringMessage("keep the constraint"),
            ]),
            [message]));
        Assert.True(CopilotSteeringRecovery.AreNewMessagesIncludedInCheckpoint(
            previousCheckpoint,
            CreateCheckpoint(
            [
                CreateSteeringMessage("keep the constraint"),
                CreateSteeringMessage("keep the constraint"),
            ]),
            [message]));
    }

    [Fact]
    public void UncheckpointedDeliveredMessagesReturnToDraftAndLeaveOtherPendingMessages()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var first = new CopilotSteeringMessageSnapshot("steering:1", "first constraint");
        var second = new CopilotSteeringMessageSnapshot("steering:2", "second constraint");
        Assert.True(CopilotSteeringRecovery.TrackPending(
            conversation,
            "run:1",
            first,
            DateTimeOffset.UtcNow));
        Assert.True(CopilotSteeringRecovery.TrackPending(
            conversation,
            "run:1",
            second,
            DateTimeOffset.UtcNow));

        Assert.True(CopilotSteeringRecovery.RestorePendingMessagesToDraft(
            conversation,
            [first]));

        Assert.Equal("first constraint", conversation.DraftText);
        var pending = Assert.Single(conversation.PendingSteeringRecoveries);
        Assert.Equal(second.MessageId, pending.MessageId);
    }

    private static CopilotRequestMessage CreateSteeringMessage(string content) =>
        new("user", content)
        {
            IsSteering = true,
        };

    private static CopilotAgentSessionCheckpoint CreateCheckpoint(
        IReadOnlyList<CopilotRequestMessage> conversationMemory) => new()
        {
            ProfileKey = "profile|model",
            SerializedSessionJson = "{}",
            ConversationMemory = conversationMemory,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
}
