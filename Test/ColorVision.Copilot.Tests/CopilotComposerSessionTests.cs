using ColorVision.Copilot;
using System.Collections.Generic;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotComposerSessionTests
{
    [Fact]
    public void LoadRestoresNormalizedCopiesWithoutTakingAttachmentOwnership()
    {
        var reviewTarget = CopilotWorkspaceReviewTargetContext.WorkingTree();
        var skillReference = CreateSkillReference();
        var conversation = CreateConversation(
            "conversation-one",
            "$sample-skill inspect this",
            CopilotAgentMode.Review,
            reviewTarget,
            skillReference);
        conversation.Attachments.Add(new CopilotAttachmentItem());
        var session = new CopilotComposerSession();

        session.Load(conversation);

        Assert.Equal("$sample-skill inspect this", session.Text);
        Assert.Equal(CopilotAgentMode.Review, session.RequestMode);
        Assert.Equal(CopilotWorkspaceReviewTarget.WorkingTree, session.WorkspaceReviewTarget?.Target);
        Assert.Equal(skillReference.Name, session.AgentSkillReference?.Name);
        Assert.NotSame(reviewTarget, session.WorkspaceReviewTarget);
        Assert.NotSame(skillReference, session.AgentSkillReference);
        Assert.Equal(1, session.Version);
        Assert.Null(typeof(CopilotComposerSession).GetProperty("Attachments"));
        Assert.Null(typeof(CopilotComposerCaptureSnapshot).GetProperty("Attachments"));

        reviewTarget.Revision = "changed outside the session";
        skillReference.Name = "changed-outside";

        Assert.Equal(string.Empty, session.WorkspaceReviewTarget?.Revision);
        Assert.Equal("sample-skill", session.AgentSkillReference?.Name);
    }

    [Fact]
    public void LoadDropsDraftMetadataThatDoesNotMatchTheDraft()
    {
        var conversation = CreateConversation(
            "conversation-one",
            "ordinary text",
            CopilotAgentMode.Auto,
            CopilotWorkspaceReviewTargetContext.WorkingTree(),
            CreateSkillReference());
        var session = new CopilotComposerSession();

        session.Load(conversation);

        Assert.Equal("ordinary text", session.Text);
        Assert.Equal(CopilotAgentMode.Auto, session.RequestMode);
        Assert.Null(session.WorkspaceReviewTarget);
        Assert.Null(session.AgentSkillReference);
    }

    [Fact]
    public void SetTextClearsAReferenceWhenItsInvocationIsRemoved()
    {
        var conversation = CreateConversation(
            "conversation-one",
            "$sample-skill inspect this",
            CopilotAgentMode.Auto,
            workspaceReviewTarget: null,
            CreateSkillReference());
        var session = new CopilotComposerSession();
        session.Load(conversation);
        var loadedVersion = session.Version;

        var changed = session.SetText("inspect this");
        var changedAgain = session.SetText("inspect this");

        Assert.True(changed);
        Assert.False(changedAgain);
        Assert.Equal("inspect this", session.Text);
        Assert.Null(session.AgentSkillReference);
        Assert.Equal(loadedVersion + 1, session.Version);
        Assert.Equal("$sample-skill inspect this", conversation.DraftText);
    }

    [Fact]
    public void LoadAndSetTextPreserveContentBeyondTheSubmissionLimit()
    {
        var persistedText = new string('p',
            CopilotConversationHistoryWindow.MaximumContentCharacterLimit + 1);
        var editedText = new string('e',
            CopilotConversationHistoryWindow.MaximumContentCharacterLimit + 2);
        var session = new CopilotComposerSession();

        session.Load(CreateConversation("conversation-one", persistedText));
        Assert.Equal(persistedText, session.Text);

        var changed = session.SetText(editedText);

        Assert.True(changed);
        Assert.Equal(editedText, session.Text);
    }

    [Fact]
    public void SetRequestModeMaintainsTheReviewTargetInvariant()
    {
        var session = new CopilotComposerSession();
        session.Load(CreateConversation("conversation-one", string.Empty));
        var workingTree = CopilotWorkspaceReviewTargetContext.WorkingTree();

        Assert.False(session.SetWorkspaceReviewTarget(workingTree));
        Assert.True(session.SetRequestMode(CopilotAgentMode.Review));
        Assert.True(session.SetWorkspaceReviewTarget(workingTree));
        var reviewVersion = session.Version;

        Assert.False(session.SetWorkspaceReviewTarget(
            CopilotWorkspaceReviewTargetContext.WorkingTree()));
        Assert.Equal(reviewVersion, session.Version);
        Assert.NotNull(session.WorkspaceReviewTarget);

        Assert.True(session.SetRequestMode(CopilotAgentMode.Code));
        Assert.Equal(CopilotAgentMode.Code, session.RequestMode);
        Assert.Null(session.WorkspaceReviewTarget);
    }

    [Fact]
    public void SetAgentSkillReferenceRequiresAnExplicitInvocationAndCopiesTheValue()
    {
        var session = new CopilotComposerSession();
        session.Load(CreateConversation("conversation-one", "ordinary text"));
        var reference = CreateSkillReference();

        Assert.False(session.SetAgentSkillReference(reference));
        Assert.Null(session.AgentSkillReference);

        Assert.True(session.SetText("Use $sample-skill for this task"));
        Assert.True(session.SetAgentSkillReference(reference));
        var selectedVersion = session.Version;
        reference.Name = "changed-outside";

        Assert.Equal("sample-skill", session.AgentSkillReference?.Name);
        Assert.False(session.SetAgentSkillReference(CreateSkillReference()));
        Assert.Equal(selectedVersion, session.Version);
    }

    [Fact]
    public void CaptureReturnsAnIsolatedSnapshotWithoutClearingTheComposer()
    {
        var conversation = CreateConversation(
            "conversation-one",
            "$sample-skill inspect this",
            CopilotAgentMode.Review,
            CopilotWorkspaceReviewTargetContext.WorkingTree(),
            CreateSkillReference());
        var session = new CopilotComposerSession();
        session.Load(conversation);

        var capture = session.Capture();

        Assert.Equal("conversation-one", capture.ConversationId);
        Assert.Equal(session.Version, capture.Version);
        Assert.Equal(capture.ConversationId, capture.Token.ConversationId);
        Assert.Equal(capture.Version, capture.Token.Version);
        Assert.Equal(session.Text, capture.Text);
        Assert.Equal(session.RequestMode, capture.RequestMode);
        Assert.NotSame(session.WorkspaceReviewTarget, capture.WorkspaceReviewTarget);
        Assert.NotSame(session.AgentSkillReference, capture.AgentSkillReference);

        capture.WorkspaceReviewTarget!.Revision = "changed capture";
        capture.AgentSkillReference!.Name = "changed-capture";

        Assert.Equal(string.Empty, session.WorkspaceReviewTarget?.Revision);
        Assert.Equal("sample-skill", session.AgentSkillReference?.Name);
        Assert.Equal("$sample-skill inspect this", session.Text);
    }

    [Fact]
    public void CommitScheduledClearsOnlyTheMatchingCaptureAndConsumesItsToken()
    {
        var conversation = CreateConversation(
            "conversation-one",
            "$sample-skill inspect this",
            CopilotAgentMode.Review,
            CopilotWorkspaceReviewTargetContext.WorkingTree(),
            CreateSkillReference());
        var session = new CopilotComposerSession();
        session.Load(conversation);
        var capture = session.Capture();

        var committed = session.CommitScheduled(capture.Token);
        var committedAgain = session.CommitScheduled(capture.Token);

        Assert.True(committed);
        Assert.False(committedAgain);
        Assert.Equal(string.Empty, session.Text);
        Assert.Equal(CopilotAgentMode.Auto, session.RequestMode);
        Assert.Null(session.WorkspaceReviewTarget);
        Assert.Null(session.AgentSkillReference);
        Assert.Equal(capture.Version + 1, session.Version);
        Assert.Equal("$sample-skill inspect this", conversation.DraftText);
    }

    [Fact]
    public void CommitScheduledLeavesNewerEditsUntouched()
    {
        var session = new CopilotComposerSession();
        session.Load(CreateConversation(
            "conversation-one",
            "original",
            CopilotAgentMode.Review,
            CopilotWorkspaceReviewTargetContext.WorkingTree()));
        var capture = session.Capture();
        session.SetText("newer edit");
        var currentVersion = session.Version;

        var committed = session.CommitScheduled(capture.Token);
        var foreignCommitted = session.CommitScheduled(
            new CopilotComposerCaptureToken("another-conversation", currentVersion));

        Assert.False(committed);
        Assert.False(foreignCommitted);
        Assert.Equal("newer edit", session.Text);
        Assert.Equal(CopilotAgentMode.Review, session.RequestMode);
        Assert.NotNull(session.WorkspaceReviewTarget);
        Assert.Equal(currentVersion, session.Version);
    }

    [Fact]
    public void LoadingAnotherConversationInvalidatesAnOldTokenEvenAfterSwitchingBack()
    {
        var first = CreateConversation("first", "first draft");
        var second = CreateConversation("second", "second draft");
        var session = new CopilotComposerSession();
        session.Load(first);
        var firstCapture = session.Capture();

        session.Load(second);
        session.Load(first);
        var reloadedVersion = session.Version;

        Assert.False(session.CommitScheduled(firstCapture.Token));
        Assert.Equal("first draft", session.Text);
        Assert.Equal(reloadedVersion, session.Version);
        Assert.NotEqual(firstCapture.Version, session.Version);
    }

    [Fact]
    public void CapturedAttachmentCommitRemovesOnlyTheOriginalObjects()
    {
        var captured = new CopilotAttachmentItem();
        var replacement = captured.CreateSnapshot();
        var addedWhileScheduling = new CopilotAttachmentItem();
        var current = new List<CopilotAttachmentItem>
        {
            replacement,
            captured,
            addedWhileScheduling,
        };

        var removed = CopilotComposerAttachmentService.RemoveCapturedByReference(
            current,
            [captured]);

        Assert.Equal(1, removed);
        Assert.Equal([replacement, addedWhileScheduling], current);
    }

    private static CopilotConversationRecord CreateConversation(
        string id,
        string text,
        CopilotAgentMode requestMode = CopilotAgentMode.Auto,
        CopilotWorkspaceReviewTargetContext? workspaceReviewTarget = null,
        CopilotAgentSkillReference? agentSkillReference = null)
    {
        var conversation = CopilotConversationRecord.CreateEmpty(string.Empty, string.Empty);
        conversation.Id = id;
        conversation.DraftText = text;
        conversation.DraftRequestMode = requestMode;
        conversation.DraftWorkspaceReviewTarget = workspaceReviewTarget;
        conversation.DraftAgentSkillReference = agentSkillReference;
        return conversation;
    }

    private static CopilotAgentSkillReference CreateSkillReference() => new()
    {
        Name = "sample-skill",
        SkillFilePath = Path.GetFullPath(Path.Combine("skills", "sample-skill", "SKILL.md")),
    };
}
