#pragma warning disable CA1707
using ColorVision.Copilot;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTaskIndexTests
{
    [Fact]
    public void Build_IndexesOnlyUnfinishedTasksThatNeedAttention()
    {
        var completed = CreateConversation("completed", CopilotAgentStopReason.Completed, DateTime.Now.AddMinutes(-1));
        var awaitingUser = CreateConversation("awaiting", CopilotAgentStopReason.AwaitingUser, DateTime.Now);
        var paused = CreateConversation("paused", CopilotAgentStopReason.Paused, DateTime.Now.AddMinutes(1), withCheckpoint: true);

        var tasks = CopilotAgentTaskIndex.Build(new[] { completed, awaitingUser, paused });

        Assert.Equal(2, tasks.Count);
        Assert.Equal("paused", tasks[0].Title);
        Assert.Equal(CopilotAgentTaskAttentionKind.Paused, tasks[0].AttentionKind);
        Assert.True(tasks[0].CanResume);
        Assert.Equal("awaiting", tasks[1].Title);
        Assert.Equal(CopilotAgentTaskAttentionKind.AwaitingUser, tasks[1].AttentionKind);
        Assert.False(tasks[1].CanResume);
    }

    [Fact]
    public void Build_UsesCompactBlockerSummaryWithoutIndexingCancelledWork()
    {
        var blocked = CreateConversation("blocked", CopilotAgentStopReason.Blocked, DateTime.Now, withCheckpoint: true);
        blocked.Messages[0].AgentBlockers =
        [
            new CopilotAgentBlockerSnapshot
            {
                Kind = CopilotAgentBlockerKind.ToolFailure,
                Code = "permanent_tool_failure",
                Summary = "The device capability is unavailable.",
                ToolName = "InspectDevice",
            },
        ];
        var cancelled = CreateConversation("cancelled", CopilotAgentStopReason.Cancelled, DateTime.Now.AddMinutes(1));

        var task = Assert.Single(CopilotAgentTaskIndex.Build(new[] { blocked, cancelled }));

        Assert.Equal("blocked", task.Title);
        Assert.Equal("任务受阻", task.StatusLabel);
        Assert.Equal("The device capability is unavailable.", task.DetailLabel);
        Assert.Equal(1, task.RemainingCount);

        Assert.True(CopilotAgentTaskIndex.Dismiss(task));
        Assert.Equal(CopilotAgentStopReason.Cancelled, task.Message.AgentStopReason);
        Assert.Empty(task.Message.AgentBlockers);
        Assert.Null(task.Conversation.AgentSessionCheckpoint);
        Assert.Empty(CopilotAgentTaskIndex.Build(new[] { blocked }));
    }

    [Fact]
    public void StateRoundTrip_RebuildsPausedTaskIndex()
    {
        var root = Path.Combine(Path.GetTempPath(), "ColorVision", "CopilotAgentTaskIndexTests", Guid.NewGuid().ToString("N"));
        try
        {
            var state = new CopilotChatState();
            state.Conversations.Add(CreateConversation("persistent", CopilotAgentStopReason.Paused, DateTime.Now, withCheckpoint: true));
            var store = new CopilotChatStateStore(root);

            store.Save(state);
            var task = Assert.Single(CopilotAgentTaskIndex.Build(store.Load().Conversations));

            Assert.Equal("persistent", task.Title);
            Assert.Equal(CopilotAgentTaskAttentionKind.Paused, task.AttentionKind);
            Assert.True(task.CanResume);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static CopilotConversationRecord CreateConversation(
        string title,
        CopilotAgentStopReason stopReason,
        DateTime updatedAt,
        bool withCheckpoint = false)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Model");
        conversation.SetCustomTitle(title);
        conversation.UpdatedAt = updatedAt;
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Agent result")
        {
            AgentStopReason = stopReason,
            AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items =
                [
                    new CopilotAgentTaskItem
                    {
                        Id = 1,
                        Title = "Finish task",
                        Description = "Complete the remaining work.",
                    },
                ],
            },
        });

        if (withCheckpoint)
        {
            conversation.AgentSessionCheckpoint = new CopilotAgentSessionCheckpoint
            {
                ProfileKey = "profile-key",
                SerializedSessionJson = "{}",
                CapabilityCatalogRevision = 1,
                Capabilities =
                [
                    new CopilotAgentCheckpointCapability
                    {
                        Id = "builtin:test",
                        Revision = 1,
                        Fingerprint = new string('A', 64),
                    },
                ],
            };
        }

        return conversation;
    }
}
