using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

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
    public void TruncatedCompletedRunOffersFinalAnswerOnlyRecovery()
    {
        var profile = CreateProfile();
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordStop(CopilotAgentStopReason.Completed);
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            taskEventJournal: journal.Snapshot());
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial final answer")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
            WasResponseInterrupted = true,
        };

        var decision = CopilotAgentRecoveryPolicy.Evaluate(
            message,
            checkpoint,
            profile,
            capabilitySnapshot);

        Assert.NotNull(checkpoint);
        Assert.True(message.HasRecoverableFinalAnswer);
        Assert.True(message.HasRecoverableAgentTasks);
        Assert.True(decision.IsAvailable);
        Assert.Equal(CopilotAgentRecoveryMode.Finalize, decision.Request!.Mode);
        Assert.Equal(CopilotAgentStopReason.Completed, decision.Request.PreviousStopReason);
        Assert.True(decision.Request.PreviousResponseWasInterrupted);
        Assert.Equal("重试最终回答", decision.ActionLabel);

        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.AgentSessionCheckpoint = checkpoint;
        conversation.Messages.Add(message);
        var task = Assert.Single(CopilotAgentTaskIndex.Build([conversation]));
        Assert.Equal(CopilotAgentTaskAttentionKind.IncompleteOutput, task.AttentionKind);
        Assert.True(task.CanResume);
        Assert.Equal("重试最终回答", task.RecoveryActionLabel);
        Assert.Equal(message.AgentRecoveryToolTip, task.RecoveryToolTip);
        Assert.Contains("最终回答恢复项", task.DismissConfirmationText, StringComparison.Ordinal);
        Assert.Contains("原任务终态和审计证据仍保留", task.DismissToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void CompletedFinalizeRequestRequiresInterruptedResponseProof()
    {
        Assert.False(new CopilotAgentRecoveryRequest
        {
            Mode = CopilotAgentRecoveryMode.Finalize,
            PreviousStopReason = CopilotAgentStopReason.Completed,
        }.IsStructurallyValid());
        Assert.True(new CopilotAgentRecoveryRequest
        {
            Mode = CopilotAgentRecoveryMode.Finalize,
            PreviousStopReason = CopilotAgentStopReason.Completed,
            PreviousResponseWasInterrupted = true,
        }.IsStructurallyValid());
    }

    [Fact]
    public void InterruptedStateRefreshesFinalAnswerRecoveryBindings()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial final answer")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        var changedProperties = new List<string>();
        message.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName ?? string.Empty);

        message.WasResponseInterrupted = true;

        Assert.True(message.HasRecoverableFinalAnswer);
        Assert.Contains(nameof(CopilotChatMessage.HasRecoverableFinalAnswer), changedProperties);
        Assert.Contains(nameof(CopilotChatMessage.HasRecoverableAgentTasks), changedProperties);
        Assert.Contains(nameof(CopilotChatMessage.AgentRecoveryActionLabel), changedProperties);
        Assert.Contains(nameof(CopilotChatMessage.AgentRecoveryToolTip), changedProperties);
    }

    [Fact]
    public void ProviderFailureWithIncompleteTasksKeepsResumeEntryPoint()
    {
        var profile = CreateProfile();
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var ledger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "execute",
            Items =
            [
                new CopilotAgentTaskItem
                {
                    Id = 1,
                    Title = "继续检查工作区",
                    IsComplete = false,
                },
            ],
        };
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordTaskLedger(ledger, "provider-failure");
        journal.RecordStop(CopilotAgentStopReason.ProviderFailure);
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            taskEventJournal: journal.Snapshot());
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Provider disconnected")
        {
            AgentStopReason = CopilotAgentStopReason.ProviderFailure,
            AgentTaskLedger = ledger,
        };

        var decision = CopilotAgentRecoveryPolicy.Evaluate(
            message,
            checkpoint,
            profile,
            capabilitySnapshot);

        Assert.True(message.HasIncompleteAgentTasks);
        Assert.True(message.HasRecoverableAgentTasks);
        Assert.True(decision.IsAvailable);
        Assert.Equal(CopilotAgentRecoveryMode.Resume, decision.Request!.Mode);
        Assert.Equal("继续任务", decision.ActionLabel);
    }

    [Fact]
    public void HookSurfaceDriftChangesResumeEntryPointToReplan()
    {
        var profile = CreateProfile();
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var registry = new CopilotToolExecutionHookRegistry();
        var executor = new CopilotToolExecutor(registry);
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordStop(CopilotAgentStopReason.Paused);
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            taskEventJournal: journal.Snapshot(),
            hookSurfaceSnapshot: executor.GetHookSurfaceSnapshot());
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Paused")
        {
            AgentStopReason = CopilotAgentStopReason.Paused,
        };
        using var registration = registry.Register(
            "test:recovery-drift",
            new NoOpHook(),
            "^RecoveryProbe$");

        var decision = CopilotAgentRecoveryPolicy.Evaluate(
            message,
            checkpoint,
            profile,
            capabilitySnapshot,
            executor.GetHookSurfaceSnapshot());

        Assert.True(decision.IsAvailable);
        Assert.Equal(CopilotAgentRecoveryMode.Replan, decision.Request!.Mode);
        Assert.Equal("重新规划", decision.ActionLabel);
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

    private sealed class NoOpHook : ICopilotToolExecutionHook
    {
        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(CopilotToolExecutionHookDecision.Proceed);
        }

        public Task AfterExecuteAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
