namespace ColorVision.Copilot.Tests;

public sealed class CopilotQueuedFollowUpReviewTargetTests
{
    [Theory]
    [InlineData("single")]
    [InlineData("different_revision")]
    [InlineData("different_target")]
    public void QueuedReviewTargetSurvivesExecutionEditingAndRecoverySnapshots(string recoveryCase)
    {
        var capturedRevision = recoveryCase == "different_target" ? "abcdef1" : "origin/develop";
        var target = new CopilotWorkspaceReviewTargetContext
        {
            Target = CopilotWorkspaceReviewTarget.BaseBranch,
            Revision = capturedRevision,
        };
        Assert.True(target.IsStructurallyValid());
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
        Assert.Equal(capturedRevision, executionTarget.Revision);
        Assert.Equal(CopilotAgentMode.Review, composerState.RequestMode);
        Assert.Equal(CopilotWorkspaceReviewTarget.BaseBranch, composerState.WorkspaceReviewTarget?.Target);
        Assert.Equal(capturedRevision, composerState.WorkspaceReviewTarget?.Revision);

        executionTarget.Revision = "mutated-snapshot";
        Assert.Equal(
            capturedRevision,
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
        if (recoveryCase != "single")
        {
            var conflictingTarget = new CopilotWorkspaceReviewTargetContext
            {
                Target = recoveryCase == "different_target"
                    ? CopilotWorkspaceReviewTarget.Commit
                    : CopilotWorkspaceReviewTarget.BaseBranch,
                Revision = recoveryCase == "different_revision" ? "origin/Develop" : capturedRevision,
            };
            Assert.True(conflictingTarget.IsStructurallyValid());
            state.QueuedFollowUpRecoveries.Add(new CopilotQueuedFollowUpRecoveryRecord
            {
                RunId = "queued-review-second",
                ConversationId = conversation.Id,
                ComposerState = CopilotComposerStash.Capture("review the second request", 0,
                    CopilotAgentMode.Review, [], conflictingTarget),
            });
        }

        Assert.True(CopilotQueuedFollowUpRecovery.RestoreToDrafts(state));
        Assert.Equal(CopilotAgentMode.Review, conversation.DraftRequestMode);
        if (recoveryCase == "single")
        {
            Assert.Equal(CopilotWorkspaceReviewTarget.BaseBranch, conversation.DraftWorkspaceReviewTarget?.Target);
            Assert.Equal(capturedRevision, conversation.DraftWorkspaceReviewTarget?.Revision);
        }
        else
        {
            Assert.Null(conversation.DraftWorkspaceReviewTarget);
        }
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
