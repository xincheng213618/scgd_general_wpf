using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotWorkspaceReviewRequestTests
{
    [Fact]
    public void PersistedReviewTargetsAreDetachedFromCallers()
    {
        var source = new CopilotWorkspaceReviewTargetContext
        {
            Target = CopilotWorkspaceReviewTarget.BaseBranch,
            Revision = "origin/develop",
        };
        var message = new CopilotChatMessage(CopilotChatRole.User, "Review the branch")
        {
            RequestMode = CopilotAgentMode.Review,
            WorkspaceReviewTarget = source,
        };
        var conversation = CopilotConversationRecord.CreateEmpty(string.Empty, string.Empty);
        conversation.DraftWorkspaceReviewTarget = source;

        source.Revision = "changed-source";
        var messageSnapshot = message.WorkspaceReviewTarget!;
        var draftSnapshot = conversation.DraftWorkspaceReviewTarget!;
        messageSnapshot.Revision = "changed-message-snapshot";
        draftSnapshot.Revision = "changed-draft-snapshot";

        Assert.Equal("origin/develop", message.WorkspaceReviewTarget?.Revision);
        Assert.Equal("origin/develop", conversation.DraftWorkspaceReviewTarget?.Revision);
        Assert.NotSame(messageSnapshot, message.WorkspaceReviewTarget);
        Assert.NotSame(draftSnapshot, conversation.DraftWorkspaceReviewTarget);
    }

    [Fact]
    public void PlainFocusKeepsWorkingTreeReviewCompatibility()
    {
        Assert.True(CopilotWorkspaceReviewRequest.TryParse(
            "prioritize cancellation races",
            out var request,
            out var error), error);

        Assert.Equal(CopilotWorkspaceReviewTarget.WorkingTree, request.Target);
        Assert.Equal("prioritize cancellation races", request.Focus);
        Assert.Contains("target working_tree and scope both", request.BuildPrompt(), StringComparison.Ordinal);
    }

    [Fact]
    public void BaseBranchBuildsStructuredReviewPrompt()
    {
        Assert.True(CopilotWorkspaceReviewRequest.TryParse(
            "--base origin/develop focus on regressions",
            out var request,
            out var error), error);

        Assert.Equal(CopilotWorkspaceReviewTarget.BaseBranch, request.Target);
        Assert.Equal("origin/develop", request.Revision);
        Assert.Equal("focus on regressions", request.Focus);
        Assert.Contains("target base_branch and revision 'origin/develop'", request.BuildPrompt(), StringComparison.Ordinal);
    }

    [Fact]
    public void CommitBuildsStructuredReviewPrompt()
    {
        Assert.True(CopilotWorkspaceReviewRequest.TryParse(
            "--commit abcdef1 security",
            out var request,
            out var error), error);

        Assert.Equal(CopilotWorkspaceReviewTarget.Commit, request.Target);
        Assert.Equal("abcdef1", request.Revision);
        Assert.Equal("security", request.Focus);
        Assert.Contains("target commit and revision 'abcdef1'", request.BuildPrompt(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--base")]
    [InlineData("--base -dangerous")]
    [InlineData("--base main..feature")]
    [InlineData("--base topic.lock/child")]
    [InlineData("--commit not-a-sha")]
    [InlineData("--unknown anything")]
    public void InvalidStructuredTargetIsRejected(string arguments)
    {
        Assert.False(CopilotWorkspaceReviewRequest.TryParse(arguments, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
