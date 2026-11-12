using Newtonsoft.Json;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotConversationPromptEditBranchTests
{
    [Fact]
    public void PreparationForksBeforePromptAndRestoresComposerState()
    {
        var source = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        source.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "First prompt"));
        source.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "First answer"));
        var target = new CopilotChatMessage(CopilotChatRole.User, "$review Review the prior change")
        {
            RequestMode = CopilotAgentMode.Review,
            WorkspaceReviewTarget = new CopilotWorkspaceReviewTargetContext
            {
                Target = CopilotWorkspaceReviewTarget.BaseBranch,
                Revision = "origin/develop",
            },
            AgentSkillReference = new CopilotAgentSkillReference
            {
                Name = "review",
                SkillFilePath = Path.GetFullPath(Path.Combine("skills", "review", "SKILL.md")),
            },
            AttachmentSnapshotCaptured = true,
        };
        var attachment = new CopilotAttachmentItem
        {
            Type = CopilotAttachmentType.Context,
            Title = "Selection",
            Value = "Selected evidence",
        };
        target.Attachments.Add(attachment);
        source.Messages.Add(target);
        source.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Second answer"));
        source.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Later prompt"));
        source.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Later answer"));

        var preparation = CopilotConversationBranchService.PreparePromptEditBranch(source, target);
        var branch = preparation.Branch;

        Assert.Equal(6, source.Messages.Count);
        Assert.Equal(2, branch.Messages.Count);
        Assert.Equal("First prompt", branch.Messages[0].Content);
        Assert.Equal("First answer", branch.Messages[1].Content);
        Assert.Equal(target.Id, branch.BranchOrigin?.ThroughMessageId);
        Assert.Equal(target.Content, branch.DraftText);
        Assert.Equal(CopilotAgentMode.Review, branch.DraftRequestMode);
        Assert.Equal(CopilotWorkspaceReviewTarget.BaseBranch, branch.DraftWorkspaceReviewTarget?.Target);
        Assert.Equal("origin/develop", branch.DraftWorkspaceReviewTarget?.Revision);
        Assert.Equal("review", branch.DraftAgentSkillReference?.Name);
        Assert.Single(branch.Attachments);
        Assert.NotSame(attachment, branch.Attachments[0]);
        Assert.Equal("Selected evidence", branch.Attachments[0].Value);
        Assert.Equal(1, preparation.RestoredAttachmentCount);
        Assert.False(preparation.HasUnrestorableAttachments);
    }

    [Fact]
    public void PreparationDoesNotReuseUncapturedAttachments()
    {
        var source = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var target = new CopilotChatMessage(CopilotChatRole.User, "Legacy prompt");
        target.Attachments.Add(new CopilotAttachmentItem
        {
            Type = CopilotAttachmentType.File,
            Title = "Legacy file",
            Value = Path.GetFullPath("legacy.txt"),
        });
        source.Messages.Add(target);
        source.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "Answer"));

        var preparation = CopilotConversationBranchService.PreparePromptEditBranch(source, target);

        Assert.Empty(preparation.Branch.Attachments);
        Assert.Equal(0, preparation.RestoredAttachmentCount);
        Assert.True(preparation.HasUnrestorableAttachments);
        Assert.Single(source.Messages[0].Attachments);
    }

    [Fact]
    public void InitializationRepairsDuplicateMessageIdsBeforeHistoricalPromptEdit()
    {
        var source = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var firstPrompt = new CopilotChatMessage(CopilotChatRole.User, "First prompt");
        var firstAnswer = new CopilotChatMessage(CopilotChatRole.Assistant, "First answer");
        var selectedPrompt = new CopilotChatMessage(CopilotChatRole.User, "Edit this prompt")
        {
            Id = firstPrompt.Id,
        };
        var originalFirstPromptId = firstPrompt.Id;
        source.Messages.Add(firstPrompt);
        source.Messages.Add(firstAnswer);
        source.Messages.Add(selectedPrompt);

        Assert.True(source.EnsureValid());

        Assert.Equal(originalFirstPromptId, firstPrompt.Id);
        Assert.NotEqual(firstPrompt.Id, selectedPrompt.Id);
        var preparation = CopilotConversationBranchService.PreparePromptEditBranch(source, selectedPrompt);

        Assert.Equal(3, source.Messages.Count);
        Assert.Equal(2, preparation.Branch.Messages.Count);
        Assert.Equal("First prompt", preparation.Branch.Messages[0].Content);
        Assert.Equal("First answer", preparation.Branch.Messages[1].Content);
        Assert.Equal(selectedPrompt.Id, preparation.Branch.BranchOrigin?.ThroughMessageId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RestoringCompactionBoundaryIdsDoesNotLeakLaterHistoryIntoAnEarlierBranch(bool duplicateBoundaryId)
    {
        const string futureDetail = "FUTURE_ONLY_DETAILS";
        var source = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var firstAnswer = new CopilotChatMessage(CopilotChatRole.Assistant, "First answer");
        var laterAnswer = new CopilotChatMessage(CopilotChatRole.Assistant, futureDetail);
        if (duplicateBoundaryId)
            laterAnswer.Id = firstAnswer.Id;
        source.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "First prompt"));
        source.Messages.Add(firstAnswer);
        source.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Later prompt"));
        source.Messages.Add(laterAnswer);
        source.Compaction = new CopilotConversationCompaction
        {
            StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
            ThroughMessageId = laterAnswer.Id,
            Summary = "The later request established " + futureDetail,
            SourceMessageCount = source.Messages.Count,
            SourceCharacters = source.Messages.Sum(message => message.ModelContent.Length),
        };
        var compactionUsage = new CopilotTokenUsage(64, 16, 80, 32);
        source.RecordCompactionUsage(compactionUsage, DateTimeOffset.Parse("2026-08-08T02:03:04Z"));
        var sourceContents = source.Messages.Select(message => message.Content).ToArray();
        var restored = Assert.IsType<CopilotConversationRecord>(
            JsonConvert.DeserializeObject<CopilotConversationRecord>(JsonConvert.SerializeObject(source)));

        restored.EnsureValid();
        var branch = CopilotConversationBranchService.CreateBranch(restored, restored.Messages[1]);
        var branchHistory = CopilotConversationRequestBuilder.CaptureHistorySnapshot(branch).ModelMessages;

        Assert.Equal(new[] { "First prompt", "First answer" }, branchHistory.Select(message => message.Content));
        Assert.Equal(sourceContents, restored.Messages.Select(message => message.Content));
        Assert.Equal(compactionUsage, Assert.IsType<CopilotConversationAuxiliaryUsage>(restored.CompactionUsage).Usage);
        var restoredHistory = CopilotConversationRequestBuilder.CaptureHistorySnapshot(restored).ModelMessages;
        if (duplicateBoundaryId)
        {
            Assert.Null(restored.Compaction);
            Assert.Equal(sourceContents, restoredHistory.Select(message => message.Content));
        }
        else
        {
            Assert.NotNull(restored.Compaction);
            Assert.Contains(futureDetail, Assert.Single(restoredHistory).Content, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PreparationRejectsForeignAndOversizedPromptSnapshots()
    {
        var source = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var foreign = new CopilotChatMessage(CopilotChatRole.User, "Foreign prompt");
        Assert.False(CopilotConversationBranchService.CanPreparePromptEditBranch(source, foreign));
        Assert.Throws<InvalidOperationException>(() =>
            CopilotConversationBranchService.PreparePromptEditBranch(source, foreign));

        var target = new CopilotChatMessage(CopilotChatRole.User, "Oversized prompt")
        {
            AttachmentSnapshotCaptured = true,
        };
        for (var index = 0; index <= CopilotComposerAttachmentService.MaximumAttachmentCount; index++)
        {
            target.Attachments.Add(new CopilotAttachmentItem
            {
                Type = CopilotAttachmentType.Context,
                Title = $"Context {index}",
                Value = index.ToString(),
            });
        }
        source.Messages.Add(target);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CopilotConversationBranchService.PreparePromptEditBranch(source, target));
        Assert.Contains(
            CopilotComposerAttachmentService.MaximumAttachmentCount.ToString(),
            exception.Message,
            StringComparison.Ordinal);
    }
}
