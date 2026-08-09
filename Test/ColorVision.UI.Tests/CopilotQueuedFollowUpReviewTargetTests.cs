using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotQueuedFollowUpReviewTargetTests
{
    [Fact]
    public void QueuedReviewTargetSurvivesExecutionEditingAndRecoverySnapshots()
    {
        var target = new CopilotWorkspaceReviewTargetContext
        {
            Target = CopilotWorkspaceReviewTarget.BaseBranch,
            Revision = "origin/develop",
        };
        var queued = new CopilotQueuedFollowUp(
            "queued-review",
            "conversation-1",
            "Review",
            "review the selected branch",
            CopilotAgentMode.Review,
            CopilotProfileConfig.CreateDefault(),
            new CopilotAgentHostContextSnapshot("", "", []),
            workspaceReviewTarget: target);

        target.Revision = "mutated-after-queue";
        var executionTarget = Assert.IsType<CopilotWorkspaceReviewTargetContext>(
            queued.CreateWorkspaceReviewTargetSnapshot());
        var composerState = queued.CreateComposerState();

        Assert.Equal(CopilotWorkspaceReviewTarget.BaseBranch, executionTarget.Target);
        Assert.Equal("origin/develop", executionTarget.Revision);
        Assert.Equal(CopilotAgentMode.Review, composerState.RequestMode);
        Assert.Equal(CopilotWorkspaceReviewTarget.BaseBranch, composerState.WorkspaceReviewTarget?.Target);
        Assert.Equal("origin/develop", composerState.WorkspaceReviewTarget?.Revision);

        executionTarget.Revision = "mutated-snapshot";
        Assert.Equal(
            "origin/develop",
            queued.CreateWorkspaceReviewTargetSnapshot()?.Revision);

        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var state = new CopilotChatState
        {
            Conversations = [conversation],
            QueuedFollowUpRecoveries =
            [
                new CopilotQueuedFollowUpRecoveryRecord
                {
                    RunId = queued.RunId,
                    ConversationId = conversation.Id,
                    ComposerState = queued.CreateComposerState(),
                },
            ],
        };

        Assert.True(CopilotQueuedFollowUpRecovery.RestoreToDrafts(state));
        Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
        Assert.Equal(CopilotWorkspaceReviewTarget.BaseBranch, conversation.DraftWorkspaceReviewTarget?.Target);
        Assert.Equal("origin/develop", conversation.DraftWorkspaceReviewTarget?.Revision);
    }

    [Fact]
    public void QueuedReviewTargetIsCapturedFromTheActiveUserTurnOnly()
    {
        var conversation = new CopilotConversationRecord();
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "older")
        {
            RequestMode = CopilotAgentMode.Review,
            WorkspaceReviewTarget = new CopilotWorkspaceReviewTargetContext
            {
                Target = CopilotWorkspaceReviewTarget.Commit,
                Revision = "older-commit",
            },
        });
        var activeTarget = new CopilotWorkspaceReviewTargetContext
        {
            Target = CopilotWorkspaceReviewTarget.BaseBranch,
            Revision = "origin/main",
        };
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "active")
        {
            RequestMode = CopilotAgentMode.Review,
            WorkspaceReviewTarget = activeTarget,
        });
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "running"));

        var captured = Assert.IsType<CopilotWorkspaceReviewTargetContext>(
            CopilotChatViewModel.ResolveQueuedFollowUpReviewTarget(
                conversation,
                CopilotAgentMode.Review));

        activeTarget.Revision = "mutated";
        Assert.Equal(CopilotWorkspaceReviewTarget.BaseBranch, captured.Target);
        Assert.Equal("origin/main", captured.Revision);
        Assert.Null(CopilotChatViewModel.ResolveQueuedFollowUpReviewTarget(
            conversation,
            CopilotAgentMode.Auto));
    }
}
