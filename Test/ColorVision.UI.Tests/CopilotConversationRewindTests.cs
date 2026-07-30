using ColorVision.Copilot;
using System.Collections.ObjectModel;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationRewindTests
{
    [Fact]
    public void RewindCommandAcceptsAnOptionalLatestFirstOrdinal()
    {
        var catalog = CopilotLocalCommandCatalog.Parse("/rewind");
        var selected = CopilotLocalCommandCatalog.Parse("/rewind 2");

        Assert.NotNull(catalog);
        Assert.Equal(CopilotLocalCommandKind.RewindConversation, catalog.Command.Kind);
        Assert.Empty(catalog.Arguments);
        Assert.False(catalog.Command.AvailableWhileAgentRuns);
        Assert.NotNull(selected);
        Assert.Same(catalog.Command, selected.Command);
        Assert.Equal("2", selected.Arguments);
    }

    [Fact]
    public void RewindPointsAreLatestFirstAndExposeOnlyVisiblePromptPreviews()
    {
        var conversation = CreateConversation();
        conversation.Messages[2].AttachmentSnapshotCaptured = true;
        conversation.Messages[2].Attachments = new ObservableCollection<CopilotAttachmentItem>
        {
            CopilotAttachmentItem.CreateContext("private attachment body", "Context"),
        };

        var points = CopilotConversationRewindService.GetPoints(conversation);
        var report = CopilotConversationRewindService.Format(conversation);

        Assert.Equal(3, points.Count);
        Assert.Equal("Third request", points[0].Preview);
        Assert.Equal("Second request", points[1].Preview);
        Assert.Equal(1, points[1].AttachmentCount);
        Assert.Equal("First request", points[2].Preview);
        Assert.True(CopilotConversationRewindService.TryResolve(conversation, "2", out var selected));
        Assert.Same(conversation.Messages[2], selected.UserMessage);
        Assert.False(CopilotConversationRewindService.TryResolve(conversation, "0", out _));
        Assert.False(CopilotConversationRewindService.TryResolve(conversation, "missing", out _));
        Assert.Contains("1 · Third request", report, StringComparison.Ordinal);
        Assert.Contains("2 · Second request · 附件 1", report, StringComparison.Ordinal);
        Assert.DoesNotContain("private attachment body", report, StringComparison.Ordinal);
        Assert.Contains("当前文件和外部操作保持不变", report, StringComparison.Ordinal);
    }

    [Fact]
    public void RewindBranchCopiesOnlyHistoryBeforeSelectedRequest()
    {
        var source = CreateConversation();
        source.Id = "source";
        source.Title = "Original";
        source.ProfileId = "profile";
        source.ProfileDisplayName = "Profile";
        source.ResponsePersonality = CopilotResponsePersonality.Friendly;
        source.AgentSessionCheckpoint = new CopilotAgentSessionCheckpoint();
        source.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
            Summary = "First turn summary.",
            ThroughMessageId = source.Messages[1].Id,
            SourceMessageCount = 2,
            SourceCharacters = 32,
        };
        var selectedUser = source.Messages[2];

        var branch = CopilotConversationBranchService.CreateRewindBranch(source, selectedUser);

        Assert.Equal(["First request", "First answer"], branch.Messages.Select(message => message.Content));
        Assert.Equal(source.ProfileId, branch.ProfileId);
        Assert.Equal(source.ResponsePersonality, branch.ResponsePersonality);
        Assert.Empty(branch.Attachments);
        Assert.Empty(branch.DraftText);
        Assert.Null(branch.AgentSessionCheckpoint);
        Assert.NotNull(branch.BranchOrigin);
        Assert.Equal(source.Id, branch.BranchOrigin.ParentConversationId);
        Assert.Equal(selectedUser.Id, branch.BranchOrigin.ThroughMessageId);
        Assert.True(branch.HasBranchOrigin);
        Assert.NotNull(branch.Compaction);
        Assert.Equal(branch.Messages[1].Id, branch.Compaction.ThroughMessageId);
        Assert.Equal(6, source.Messages.Count);
        Assert.NotNull(source.AgentSessionCheckpoint);
    }

    [Fact]
    public void RewindingFirstRequestCreatesEmptyBranchWithDurableOrigin()
    {
        var source = CreateConversation();
        source.Id = "source";
        var firstUser = source.Messages[0];

        var branch = CopilotConversationBranchService.CreateRewindBranch(source, firstUser, "Fresh approach");

        Assert.Empty(branch.Messages);
        Assert.Equal("Fresh approach", branch.Title);
        Assert.NotNull(branch.BranchOrigin);
        Assert.Equal(firstUser.Id, branch.BranchOrigin.ThroughMessageId);
        Assert.True(branch.BranchOrigin.IsStructurallyValid(branch.Id));
        Assert.Same(
            source,
            CopilotConversationBranchService.FindBranchOriginTarget([source, branch], branch));
    }

    private static CopilotConversationRecord CreateConversation()
    {
        var conversation = new CopilotConversationRecord();
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "First request"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "First answer"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Second request")
        {
            RequestMode = CopilotAgentMode.Code,
        });
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Second answer"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Third request"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Third answer"));
        return conversation;
    }
}
