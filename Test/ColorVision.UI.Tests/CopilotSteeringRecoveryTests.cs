using ColorVision.Copilot;
using Newtonsoft.Json.Linq;
using System.IO;

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
            .Select(index => new CopilotSteeringMessageSnapshot(
                $"message-{index}",
                $"steering {index}"))
            .ToList();

        var agentEvent = CopilotAgentEvent.SteeringRecovery(messages);
        messages[0] = new CopilotSteeringMessageSnapshot("mutated", "mutated");

        Assert.Equal(CopilotAgentEventType.SteeringRecovery, agentEvent.Type);
        Assert.Equal(8, agentEvent.SteeringMessages.Count);
        Assert.Equal("message-1", agentEvent.SteeringMessages[0].MessageId);
        Assert.Equal("steering 1", agentEvent.SteeringMessages[0].Text);
    }

    [Fact]
    public void DeliveryAcknowledgmentRemovesOnlyMatchingPendingMessageId()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var acceptedAtUtc = DateTimeOffset.Parse("2026-07-31T10:00:00Z");
        var first = new CopilotSteeringMessageSnapshot("message-1", "same text");
        var second = new CopilotSteeringMessageSnapshot("message-2", "same text");
        Assert.True(CopilotSteeringRecovery.TrackPending(conversation, "task", first, acceptedAtUtc));
        Assert.True(CopilotSteeringRecovery.TrackPending(conversation, "task", second, acceptedAtUtc));

        Assert.True(CopilotSteeringRecovery.RemovePending(conversation, [first]));

        var remaining = Assert.Single(conversation.PendingSteeringRecoveries);
        Assert.Equal("message-2", remaining.MessageId);
        Assert.Equal("same text", remaining.Text);
    }

    [Fact]
    public void ProcessRestartRestoresEveryUnacknowledgedOccurrenceAndClearsLedger()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.DraftText = "new draft";
        var acceptedAtUtc = DateTimeOffset.Parse("2026-07-31T10:00:00Z");
        Assert.True(CopilotSteeringRecovery.TrackPending(
            conversation,
            "task",
            new CopilotSteeringMessageSnapshot("message-1", "same text"),
            acceptedAtUtc));
        Assert.True(CopilotSteeringRecovery.TrackPending(
            conversation,
            "task",
            new CopilotSteeringMessageSnapshot("message-2", "same text"),
            acceptedAtUtc.AddSeconds(1)));
        var state = new CopilotChatState
        {
            Conversations = new System.Collections.ObjectModel.ObservableCollection<CopilotConversationRecord>
            {
                conversation,
            },
        };

        Assert.True(CopilotSteeringRecovery.RestorePendingToDrafts(state));

        Assert.Equal(2, state.RecoveredSteeringCount);
        Assert.Empty(conversation.PendingSteeringRecoveries);
        Assert.Contains("new draft", conversation.DraftText, StringComparison.Ordinal);
        Assert.Contains("1. same text", conversation.DraftText, StringComparison.Ordinal);
        Assert.Contains("2. same text", conversation.DraftText, StringComparison.Ordinal);
    }

    [Fact]
    public void StateStoreRoundTripsPendingSteeringRecoveryLedger()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
            var acceptedAtUtc = DateTimeOffset.Parse("2026-07-31T10:00:00Z");
            Assert.True(CopilotSteeringRecovery.TrackPending(
                conversation,
                "task-1",
                new CopilotSteeringMessageSnapshot("message-1", "recover me"),
                acceptedAtUtc));
            var state = new CopilotChatState
            {
                Conversations = new System.Collections.ObjectModel.ObservableCollection<CopilotConversationRecord>
                {
                    conversation,
                },
            };
            var store = new CopilotChatStateStore(root);

            store.Save(state);
            var restored = new CopilotChatStateStore(root).Load();

            var restoredRecord = Assert.Single(Assert.Single(restored.Conversations).PendingSteeringRecoveries);
            Assert.Equal("message-1", restoredRecord.MessageId);
            Assert.Equal("task-1", restoredRecord.TaskId);
            Assert.Equal("recover me", restoredRecord.Text);
            Assert.Equal(acceptedAtUtc, restoredRecord.AcceptedAtUtc);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void StateStoreRejectsMalformedPendingSteeringRecoveryLedger()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CopilotChatStateStore(root);
            Directory.CreateDirectory(store.StateDirectoryPath);
            var document = new JObject
            {
                [nameof(CopilotChatState.SchemaVersion)] = CopilotChatState.CurrentSchemaVersion,
                [nameof(CopilotChatState.ActiveConversationId)] = "conversation",
                [nameof(CopilotChatState.ActiveProfileId)] = "profile",
                [nameof(CopilotChatState.Conversations)] = new JArray
                {
                    new JObject
                    {
                        [nameof(CopilotConversationRecord.Messages)] = new JArray(),
                        [nameof(CopilotConversationRecord.Attachments)] = new JArray(),
                        [nameof(CopilotConversationRecord.PendingSteeringRecoveries)] = new JObject
                        {
                            [nameof(CopilotPendingSteeringRecoveryRecord.MessageId)] = "invalid-container",
                        },
                    },
                },
            };
            File.WriteAllText(store.StateFilePath, document.ToString());

            var restored = store.Load();

            Assert.Equal(CopilotChatStateLoadSource.Unrecoverable, store.LastLoadStatus.Source);
            Assert.Empty(restored.Conversations);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
