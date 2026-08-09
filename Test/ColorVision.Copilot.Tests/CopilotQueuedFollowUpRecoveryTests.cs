using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotQueuedFollowUpRecoveryTests
{
    [Fact]
    public void StartupKeepsDurableQueueItemsAndMigratesLegacyRecordsToDrafts()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var durable = CreateRecovery("queued-run-1", conversation.Id, "resume in order");
        durable.ProfileId = "profile";
        durable.QueuedAtUtc = new DateTimeOffset(2026, 8, 10, 8, 30, 0, TimeSpan.Zero);
        durable.ResumeAfterRestart = true;
        var state = new CopilotChatState
        {
            Conversations = [conversation],
            QueuedFollowUpRecoveries =
            [
                durable,
                CreateRecovery("queued-run-2", conversation.Id, "legacy draft"),
            ],
        };

        Assert.True(CopilotQueuedFollowUpRecovery.PrepareForRestartDispatch(state));

        var retained = Assert.Single(state.QueuedFollowUpRecoveries);
        Assert.Same(durable, retained);
        Assert.Equal("legacy draft", conversation.DraftText);
        Assert.Equal(1, state.RecoveredQueuedFollowUpCount);
        Assert.Equal(0, state.ResumedQueuedFollowUpCount);
    }

    [Fact]
    public void StartupDropsDurableRecordAfterItsUserMessageWasPersisted()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "already persisted")
        {
            Id = "queued-run-1",
        });
        var durable = CreateRecovery("queued-run-1", conversation.Id, "do not replay");
        durable.ProfileId = "profile";
        durable.ResumeAfterRestart = true;
        var state = new CopilotChatState
        {
            Conversations = [conversation],
            QueuedFollowUpRecoveries = [durable],
        };

        Assert.True(CopilotQueuedFollowUpRecovery.PrepareForRestartDispatch(state));

        Assert.Empty(state.QueuedFollowUpRecoveries);
        Assert.Empty(conversation.DraftText);
        Assert.Equal(0, state.RecoveredQueuedFollowUpCount);
    }

    [Fact]
    public void StartupAutoDispatchRequiresAnIdleCompletedConversation()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var completed = new CopilotChatMessage(CopilotChatRole.Assistant, "done")
        {
            AgentStopReason = CopilotAgentStopReason.Completed,
        };
        conversation.Messages.Add(completed);

        Assert.True(CopilotQueuedFollowUpRecovery.CanAutoDispatch(conversation));

        completed.AgentStopReason = CopilotAgentStopReason.ProviderFailure;
        Assert.False(CopilotQueuedFollowUpRecovery.CanAutoDispatch(conversation));

        completed.AgentStopReason = CopilotAgentStopReason.Completed;
        completed.MarkResponseInterrupted("application exited");
        Assert.False(CopilotQueuedFollowUpRecovery.CanAutoDispatch(conversation));
    }

    [Fact]
    public void PersistedQueuedUserMessageIsNotRestoredToTheDraft()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "already persisted")
        {
            Id = "queued-run-1",
        });
        var state = new CopilotChatState
        {
            Conversations = [conversation],
            QueuedFollowUpRecoveries =
            [
                CreateRecovery("queued-run-1", conversation.Id, "do not duplicate"),
            ],
        };

        Assert.True(CopilotQueuedFollowUpRecovery.RestoreToDrafts(state));

        Assert.Empty(conversation.DraftText);
        Assert.Equal(0, state.RecoveredQueuedFollowUpCount);
        Assert.Empty(state.QueuedFollowUpRecoveries);
    }

    [Fact]
    public void RestoreRecordToDraftPreservesOtherQueuedRecoveries()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var reviewTarget = new CopilotWorkspaceReviewTargetContext
        {
            Target = CopilotWorkspaceReviewTarget.WorkingTree,
        };
        var state = new CopilotChatState
        {
            Conversations = [conversation],
            QueuedFollowUpRecoveries =
            [
                CreateRecovery(
                    "queued-run-1",
                    conversation.Id,
                    "restore this review",
                    CopilotAgentMode.Review,
                    reviewTarget),
                CreateRecovery("queued-run-2", conversation.Id, "leave this queued"),
            ],
        };

        Assert.True(CopilotQueuedFollowUpRecovery.RestoreRecordToDraft(state, "queued-run-1"));

        Assert.Equal("restore this review", conversation.DraftText);
        Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
        Assert.Equal(CopilotWorkspaceReviewTarget.WorkingTree, conversation.DraftWorkspaceReviewTarget?.Target);
        var remaining = Assert.Single(state.QueuedFollowUpRecoveries);
        Assert.Equal("queued-run-2", remaining.RunId);
        Assert.Equal(0, state.RecoveredQueuedFollowUpCount);
    }

    private static CopilotQueuedFollowUpRecoveryRecord CreateRecovery(
        string runId,
        string conversationId,
        string prompt,
        CopilotAgentMode mode = CopilotAgentMode.Auto,
        CopilotWorkspaceReviewTargetContext? reviewTarget = null) => new()
        {
            RunId = runId,
            ConversationId = conversationId,
            ComposerState = CopilotComposerStash.Capture(
                prompt,
                prompt.Length,
                mode,
                Array.Empty<CopilotAttachmentItem>(),
                reviewTarget),
        };
}
