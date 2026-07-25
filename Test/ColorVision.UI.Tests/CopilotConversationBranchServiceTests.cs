using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationBranchServiceTests
{
    [Fact]
    public void CreateBranchCopiesTranscriptButStartsWithFreshRuntimeState()
    {
        var source = new CopilotConversationRecord
        {
            Id = "source-conversation",
            Title = "Inspect camera workflow",
            DraftText = "unsent follow-up",
            ProfileId = "profile-1",
            ProfileDisplayName = "Test Profile",
            AgentSessionCheckpoint = new CopilotAgentSessionCheckpoint(),
        };
        source.SetLastUsage(new CopilotTokenUsage(100, 20, 120));
        source.Attachments.Add(CopilotAttachmentItem.CreateContext("composer context", "Composer"));

        var user = new CopilotChatMessage(CopilotChatRole.User, "Inspect the current workflow.")
        {
            RequestMode = CopilotAgentMode.Auto,
            RecoveryRequest = new CopilotAgentRecoveryRequest
            {
                Mode = CopilotAgentRecoveryMode.Replan,
                PreviousStopReason = CopilotAgentStopReason.Interrupted,
            },
        };
        user.Attachments.Add(CopilotAttachmentItem.CreateContext("captured context", "Captured"));
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "The workflow has three stages.")
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        source.Messages.Add(user);
        source.Messages.Add(assistant);
        source.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = "Earlier context summary.",
            ThroughMessageId = assistant.Id,
            SourceMessageCount = 2,
            SourceCharacters = 64,
        };
        source.RefreshSummary();

        var branch = CopilotConversationBranchService.CreateBranch(source, assistant, "Try another approach");

        Assert.NotEqual(source.Id, branch.Id);
        Assert.Equal("Try another approach", branch.Title);
        Assert.True(branch.HasCustomTitle);
        Assert.False(branch.IsPinned);
        Assert.Equal(source.ProfileId, branch.ProfileId);
        Assert.Equal(source.ProfileDisplayName, branch.ProfileDisplayName);
        Assert.Equal(2, branch.Messages.Count);
        Assert.Equal(source.Messages.Select(message => message.Content), branch.Messages.Select(message => message.Content));
        Assert.DoesNotContain(branch.Messages, message => source.Messages.Any(sourceMessage => sourceMessage.Id == message.Id));
        Assert.Null(branch.Messages[0].RecoveryRequest);
        Assert.NotEqual(user.Attachments[0].Id, branch.Messages[0].Attachments[0].Id);
        Assert.Empty(branch.Attachments);
        Assert.Empty(branch.DraftText);
        Assert.Null(branch.AgentSessionCheckpoint);
        Assert.False(branch.LastUsage.HasAny);
        Assert.NotNull(branch.Compaction);
        Assert.Equal(branch.Messages[1].Id, branch.Compaction.ThroughMessageId);

        Assert.Equal("source-conversation", source.Id);
        Assert.Equal("unsent follow-up", source.DraftText);
        Assert.Single(source.Attachments);
        Assert.NotNull(source.AgentSessionCheckpoint);
        Assert.True(source.LastUsage.HasAny);
        Assert.NotNull(user.RecoveryRequest);
    }

    [Fact]
    public void FindLatestBranchPointIgnoresAnInProgressAssistant()
    {
        var source = new CopilotConversationRecord();
        var complete = new CopilotChatMessage(CopilotChatRole.Assistant, "Completed answer.");
        var pending = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial answer.")
        {
            IsResponsePending = true,
        };
        source.Messages.Add(complete);
        source.Messages.Add(pending);

        var branchPoint = CopilotConversationBranchService.FindLatestBranchPoint(source);

        Assert.Same(complete, branchPoint);
    }

    [Fact]
    public void ForkAndBranchCommandsAcceptAnOptionalConversationName()
    {
        var fork = CopilotLocalCommandCatalog.Parse("/fork alternative approach");
        var branch = CopilotLocalCommandCatalog.Parse("/branch another option");

        Assert.NotNull(fork);
        Assert.Equal(CopilotLocalCommandKind.ForkConversation, fork.Command.Kind);
        Assert.Equal("alternative approach", fork.Arguments);
        Assert.False(fork.Command.AvailableWhileAgentRuns);
        Assert.NotNull(branch);
        Assert.Equal(CopilotLocalCommandKind.ForkConversation, branch.Command.Kind);
        Assert.Equal("another option", branch.Arguments);
        Assert.False(branch.Command.AvailableWhileAgentRuns);
    }
}
