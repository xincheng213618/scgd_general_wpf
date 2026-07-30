using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTaskDismissalTests
{
    [Fact]
    public void DismissClearsRecoveryWithoutRewritingCompletedInterruptedOutcome()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.AgentSessionCheckpoint = new CopilotAgentSessionCheckpoint();
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial final answer")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
            WasResponseInterrupted = true,
            AgentBlockers =
            [
                new CopilotAgentBlockerSnapshot
                {
                    Kind = CopilotAgentBlockerKind.ProviderOutput,
                    Code = "provider_output_limit",
                    Summary = "The visible final answer was truncated.",
                },
            ],
        };
        conversation.Messages.Add(message);
        var task = Assert.Single(CopilotAgentTaskIndex.Build([conversation]));

        Assert.True(CopilotAgentTaskIndex.Dismiss(task));

        Assert.Null(conversation.AgentSessionCheckpoint);
        Assert.True(message.IsAgentRecoveryDismissed);
        Assert.Equal(CopilotAgentStopReason.Completed, message.AgentStopReason);
        Assert.True(message.WasResponseInterrupted);
        Assert.Single(message.AgentBlockers);
        Assert.False(message.HasRecoverableFinalAnswer);
        Assert.False(message.HasRecoverableAgentTasks);
        Assert.Contains("<assistant_response_interrupted>", message.ModelContent, StringComparison.Ordinal);
        Assert.Empty(CopilotAgentTaskIndex.Build([conversation]));
    }

    [Fact]
    public void DismissPreservesIncompleteTaskStopReasonAndBlockerEvidence()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.AgentSessionCheckpoint = new CopilotAgentSessionCheckpoint();
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Provider disconnected")
        {
            AgentStopReason = CopilotAgentStopReason.ProviderFailure,
            AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items =
                [
                    new CopilotAgentTaskItem
                    {
                        Id = 1,
                        Title = "Verify the build",
                        IsComplete = false,
                    },
                ],
            },
            AgentBlockers =
            [
                new CopilotAgentBlockerSnapshot
                {
                    Kind = CopilotAgentBlockerKind.ProviderOutput,
                    Code = "provider_interrupted",
                    Summary = "The provider connection ended.",
                },
            ],
        };
        conversation.Messages.Add(message);
        var task = Assert.Single(CopilotAgentTaskIndex.Build([conversation]));

        Assert.True(CopilotAgentTaskIndex.Dismiss(task));

        Assert.Equal(CopilotAgentStopReason.ProviderFailure, message.AgentStopReason);
        Assert.Single(message.AgentBlockers);
        Assert.Equal(1, message.AgentTaskLedger.RemainingCount);
        Assert.False(message.HasRecoverableAgentTasks);
        Assert.Empty(CopilotAgentTaskIndex.Build([conversation]));
    }

    [Fact]
    public void DismissedRecoveryStatePersistsAcrossMessageRoundTrip()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial final answer")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
            WasResponseInterrupted = true,
            IsAgentRecoveryDismissed = true,
        };

        var json = JsonConvert.SerializeObject(message);
        var restored = JsonConvert.DeserializeObject<CopilotChatMessage>(json);

        Assert.NotNull(restored);
        Assert.True(restored.IsAgentRecoveryDismissed);
        Assert.Equal(CopilotAgentStopReason.Completed, restored.AgentStopReason);
        Assert.True(restored.WasResponseInterrupted);
        Assert.False(restored.HasRecoverableAgentTasks);
    }
}
