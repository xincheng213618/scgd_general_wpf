#pragma warning disable CA1707
using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTaskContinuityPolicyTests
{
    [Fact]
    public void HasAvailableStructuredRecovery_RequiresLatestAssistantAndMatchingCheckpoint()
    {
        var profile = CreateProfile();
        var capabilities = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{\"state\":{}}",
            capabilities,
            taskEventJournal: journal.Snapshot());
        Assert.NotNull(checkpoint);

        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        var pausedMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial result")
        {
            AgentStopReason = CopilotAgentStopReason.Paused,
            AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items = [new CopilotAgentTaskItem { Id = 1, Title = "Finish the task" }],
            },
        };
        conversation.Messages.Add(pausedMessage);
        conversation.AgentSessionCheckpoint = checkpoint;

        Assert.True(CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(conversation, profile, capabilities));
        Assert.True(CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(conversation, pausedMessage, profile, capabilities));

        var laterMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Later answer");
        conversation.Messages.Add(laterMessage);

        Assert.False(CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(conversation, profile, capabilities));
        Assert.False(CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(conversation, pausedMessage, profile, capabilities));
    }

    [Fact]
    public void HasAvailableStructuredRecovery_FailsClosedWithoutUsableCheckpointOrProfile()
    {
        var profile = CreateProfile();
        var capabilities = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        var pausedMessage = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial result")
        {
            AgentStopReason = CopilotAgentStopReason.Paused,
            AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items = [new CopilotAgentTaskItem { Id = 1, Title = "Finish the task" }],
            },
        };
        conversation.Messages.Add(pausedMessage);

        Assert.False(CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(conversation, profile, capabilities));
        Assert.False(CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(conversation, pausedMessage, null, capabilities));
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "secret-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 256,
        };
    }
}
