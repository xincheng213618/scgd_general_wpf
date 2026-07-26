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

    [Fact]
    public void ReplanRecoveryRetainsOriginalTaskIntentForToolSelection()
    {
        const string originalTask = "只读审计 C:\\workspace\\ColorVision\\Copilot，列出至少 30 条可验证的问题；不要修改任何文件。";
        var checkpoint = new CopilotAgentSessionCheckpoint
        {
            ConversationMemory =
            [
                new CopilotRequestMessage("user", originalTask),
                new CopilotRequestMessage("user", CopilotAgentRecoveryPolicy.ReplanUserMessage),
            ],
        };
        var recovery = new CopilotAgentRecoveryRequest
        {
            Mode = CopilotAgentRecoveryMode.Replan,
            PreviousStopReason = CopilotAgentStopReason.Paused,
        };

        var context = CopilotAgentRecoveryTaskContext.Resolve(
            CopilotAgentRecoveryPolicy.ReplanUserMessage,
            recovery,
            checkpoint);
        var request = new CopilotAgentRequest
        {
            UserText = context.EffectiveUserText,
            Mode = CopilotAgentMode.Auto,
            SearchRootPaths = [@"C:\workspace"],
        };

        Assert.Equal(originalTask, context.TaskIntentText);
        Assert.Contains(originalTask, context.EffectiveUserText, StringComparison.Ordinal);
        Assert.Contains(CopilotAgentRecoveryPolicy.ReplanUserMessage, context.EffectiveUserText, StringComparison.Ordinal);
        Assert.True(CopilotToolIntentPolicy.NeedsLocalEvidence(request));
        Assert.True(new CopilotSearchFilesTool().IsAvailable(request));
    }

    [Fact]
    public void PersistedTaskIntentWinsAfterRepeatedRecoveryMessages()
    {
        const string originalTask = "检查当前项目并验证构建";
        var checkpoint = new CopilotAgentSessionCheckpoint
        {
            TaskIntentText = originalTask,
            ConversationMemory =
            [
                new CopilotRequestMessage("user", "Earlier unrelated request"),
                new CopilotRequestMessage("user", CopilotAgentRecoveryPolicy.ResumeUserMessage),
                new CopilotRequestMessage("user", CopilotAgentRecoveryPolicy.ReplanUserMessage),
            ],
        };

        var context = CopilotAgentRecoveryTaskContext.Resolve(
            CopilotAgentRecoveryPolicy.ReplanUserMessage,
            new CopilotAgentRecoveryRequest
            {
                Mode = CopilotAgentRecoveryMode.Replan,
                PreviousStopReason = CopilotAgentStopReason.Paused,
            },
            checkpoint);

        Assert.Equal(originalTask, context.TaskIntentText);
        Assert.StartsWith("# Original task to continue", context.EffectiveUserText, StringComparison.Ordinal);
    }

    [Fact]
    public void FinalAnswerRecoveryDoesNotReopenOriginalToolIntent()
    {
        const string originalTask = "检查当前项目并修复构建";
        var context = CopilotAgentRecoveryTaskContext.Resolve(
            CopilotAgentRecoveryPolicy.FinalizeUserMessage,
            new CopilotAgentRecoveryRequest
            {
                Mode = CopilotAgentRecoveryMode.Finalize,
                PreviousStopReason = CopilotAgentStopReason.IncompleteOutput,
            },
            new CopilotAgentSessionCheckpoint { TaskIntentText = originalTask });

        Assert.Equal(originalTask, context.TaskIntentText);
        Assert.Equal(CopilotAgentRecoveryPolicy.FinalizeUserMessage, context.EffectiveUserText);
        Assert.False(CopilotToolIntentPolicy.NeedsLocalEvidence(new CopilotAgentRequest
        {
            UserText = context.EffectiveUserText,
            Mode = CopilotAgentMode.Auto,
        }));
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
