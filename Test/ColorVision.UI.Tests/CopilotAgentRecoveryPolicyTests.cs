using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentRecoveryPolicyTests
{
    [Fact]
    public void PausedDirectRunCanResumeWithoutTaskLedgerItems()
    {
        var profile = CreateProfile();
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordTaskLedger(new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "execute",
        }, "final");
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            taskEventJournal: journal.Snapshot());
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Paused")
        {
            AgentStopReason = CopilotAgentStopReason.Paused,
            AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
            },
        };

        var decision = CopilotAgentRecoveryPolicy.Evaluate(
            message,
            checkpoint,
            profile,
            capabilitySnapshot);

        Assert.NotNull(checkpoint);
        Assert.False(message.HasIncompleteAgentTasks);
        Assert.True(message.HasRecoverableAgentTasks);
        Assert.True(decision.IsAvailable);
        Assert.Equal(CopilotAgentRecoveryMode.Resume, decision.Request!.Mode);
        Assert.Equal("继续任务", decision.ActionLabel);
    }

    [Fact]
    public void CompletedDirectRunRemainsNonRecoverable()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Completed")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };

        Assert.False(message.HasIncompleteAgentTasks);
        Assert.False(message.HasRecoverableAgentTasks);
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 4_096,
        };
    }
}
