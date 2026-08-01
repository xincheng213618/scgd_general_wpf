using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotCompletionNoticeCenterTests
{
    [Fact]
    public void SelectedConversationTakesPriorityAcrossNoticeKinds()
    {
        var selected = CreateConversation("selected", "Selected");
        var other = CreateConversation("other", "Other");
        var center = new CopilotCompletionNoticeCenter();
        Assert.True(center.CaptureBackgroundCommand(
            CreateBackgroundSnapshot(selected.Id, "bg:selected"),
            selected,
            selected.Id));
        Assert.True(center.CaptureSubagent(
            CreateSubagentSnapshot(other.Id, "other-run"),
            other,
            selected.Id));

        var notice = center.GetCurrent([selected, other], selected.Id);

        Assert.NotNull(notice);
        Assert.Equal(CopilotCompletionNoticeKind.BackgroundCommand, notice.Kind);
        Assert.Equal(selected.Id, notice.ConversationId);
        Assert.Equal("bg:selected", notice.ItemId);
    }

    [Fact]
    public void LatestKindIsShownAndAcknowledgementRevealsThePreviousNotice()
    {
        var conversation = CreateConversation("conversation", "Task");
        var center = new CopilotCompletionNoticeCenter();
        Assert.True(center.CaptureBackgroundCommand(
            CreateBackgroundSnapshot(conversation.Id, "bg:first"),
            conversation,
            conversation.Id));
        Assert.True(center.CaptureSubagent(
            CreateSubagentSnapshot(conversation.Id, "subagent-latest"),
            conversation,
            conversation.Id));

        var latest = center.GetCurrent([conversation], conversation.Id);

        Assert.NotNull(latest);
        Assert.Equal(CopilotCompletionNoticeKind.Subagent, latest.Kind);
        Assert.Equal("subagent-latest", latest.ItemId);
        Assert.True(center.Acknowledge(latest.Kind, latest.ConversationId, latest.ItemId));

        var previous = center.GetCurrent([conversation], conversation.Id);
        Assert.NotNull(previous);
        Assert.Equal(CopilotCompletionNoticeKind.BackgroundCommand, previous.Kind);
        Assert.Equal("bg:first", previous.ItemId);
    }

    [Fact]
    public void ConversationAcknowledgementClearsBothNoticeKinds()
    {
        var conversation = CreateConversation("conversation", "Task");
        var center = new CopilotCompletionNoticeCenter();
        Assert.True(center.CaptureBackgroundCommand(
            CreateBackgroundSnapshot(conversation.Id, "bg:one"),
            conversation,
            conversation.Id));
        Assert.True(center.CaptureSubagent(
            CreateSubagentSnapshot(conversation.Id, "subagent-one"),
            conversation,
            conversation.Id));

        Assert.True(center.AcknowledgeConversation(conversation.Id));
        Assert.Null(center.GetCurrent([conversation], conversation.Id));
        Assert.False(center.AcknowledgeConversation(conversation.Id));
    }

    private static CopilotConversationRecord CreateConversation(string id, string title)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Id = id;
        conversation.Title = title;
        return conversation;
    }

    private static CopilotSubagentCompletionSnapshot CreateSubagentSnapshot(
        string conversationId,
        string runId)
    {
        return new CopilotSubagentCompletionSnapshot(
            conversationId,
            runId,
            "explore",
            CopilotAgentStopReason.Completed);
    }

    private static CopilotBackgroundShellCommandSnapshot CreateBackgroundSnapshot(
        string conversationId,
        string backgroundId)
    {
        return new CopilotBackgroundShellCommandSnapshot(
            backgroundId,
            conversationId,
            "task",
            CopilotShellKind.PowerShell,
            @"C:\workspace",
            "Write-Output done",
            new string('a', 64),
            DateTimeOffset.UtcNow.AddSeconds(-5),
            DateTimeOffset.UtcNow,
            123,
            ProcessTreeContained: true,
            CopilotBackgroundShellCommandState.Completed,
            ExitCode: 0,
            StandardOutput: "done",
            StandardError: string.Empty);
    }
}
