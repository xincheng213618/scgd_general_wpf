using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotQueuedFollowUpRecoveryTests
{
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
