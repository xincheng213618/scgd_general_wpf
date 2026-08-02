using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotBackgroundShellCommandCompletionNoticeTests
{
    [Theory]
    [InlineData(
        (int)CopilotBackgroundShellCommandState.Completed,
        0,
        "后台命令已完成（退出码 0）")]
    [InlineData(
        (int)CopilotBackgroundShellCommandState.Failed,
        7,
        "后台命令失败（退出码 7）")]
    [InlineData(
        (int)CopilotBackgroundShellCommandState.Expired,
        1,
        "后台命令已到期（退出码 1）")]
    public void TerminalStateCreatesSelectedConversationNotice(
        int stateValue,
        int exitCode,
        string expected)
    {
        var state = (CopilotBackgroundShellCommandState)stateValue;
        var conversation = CreateConversation("conversation", "Build monitor");
        var snapshot = CreateSnapshot(conversation.Id, state, exitCode);

        var notice = CopilotBackgroundShellCommandCompletionNoticePolicy.Create(
            snapshot,
            conversation,
            selectedConversationId: conversation.Id);

        Assert.NotNull(notice);
        Assert.Equal(conversation.Id, notice.ConversationId);
        Assert.Equal(snapshot.Id, notice.BackgroundId);
        Assert.Equal(expected, notice.Text);
        Assert.DoesNotContain(snapshot.Id, notice.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(snapshot.CommandPreview, notice.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherConversationNoticeUsesBoundedTitleAndAggregatesPendingCommands()
    {
        var conversation = CreateConversation(
            "conversation-background",
            "  Background   verification  ");
        var snapshot = CreateSnapshot(
            conversation.Id,
            CopilotBackgroundShellCommandState.Completed,
            0);

        var notice = CopilotBackgroundShellCommandCompletionNoticePolicy.Create(
            snapshot,
            conversation,
            selectedConversationId: "conversation-selected",
            pendingCount: 3);

        Assert.NotNull(notice);
        Assert.Equal(
            "Background verification · 3 条后台命令待查看",
            notice.Text);
    }

    [Theory]
    [InlineData((int)CopilotBackgroundShellCommandState.Running)]
    [InlineData((int)CopilotBackgroundShellCommandState.Stopped)]
    public void RunningAndExplicitlyStoppedCommandsDoNotCreateCompletionNotices(
        int stateValue)
    {
        var state = (CopilotBackgroundShellCommandState)stateValue;
        var conversation = CreateConversation("conversation", "Task");

        Assert.Null(CopilotBackgroundShellCommandCompletionNoticePolicy.Create(
            CreateSnapshot(conversation.Id, state, exitCode: null),
            conversation,
            selectedConversationId: conversation.Id));
    }

    [Fact]
    public void MismatchedConversationDoesNotCreateNotice()
    {
        var conversation = CreateConversation("conversation", "Task");

        Assert.Null(CopilotBackgroundShellCommandCompletionNoticePolicy.Create(
            CreateSnapshot(
                "another-conversation",
                CopilotBackgroundShellCommandState.Completed,
                0),
            conversation,
            selectedConversationId: conversation.Id));
    }

    [Fact]
    public void ArchivedConversationDoesNotCreateNotice()
    {
        var conversation = CreateConversation("conversation", "Task");
        conversation.IsArchived = true;

        Assert.Null(CopilotBackgroundShellCommandCompletionNoticePolicy.Create(
            CreateSnapshot(
                conversation.Id,
                CopilotBackgroundShellCommandState.Completed,
                0),
            conversation,
            selectedConversationId: "selected"));
    }

    [Fact]
    public void TrackerPrioritizesSelectedConversationAndAcknowledgesExactOrConversation()
    {
        var first = CreateConversation("conversation-first", "First");
        var second = CreateConversation("conversation-second", "Second");
        var tracker = new CopilotBackgroundShellCommandCompletionNoticeTracker();
        Assert.True(tracker.Capture(
            CreateSnapshot(
                first.Id,
                CopilotBackgroundShellCommandState.Completed,
                0,
                "bg:first-1"),
            first,
            selectedConversationId: second.Id));
        Assert.True(tracker.Capture(
            CreateSnapshot(
                second.Id,
                CopilotBackgroundShellCommandState.Failed,
                7,
                "bg:second"),
            second,
            selectedConversationId: second.Id));
        Assert.True(tracker.Capture(
            CreateSnapshot(
                first.Id,
                CopilotBackgroundShellCommandState.Expired,
                1,
                "bg:first-2"),
            first,
            selectedConversationId: second.Id));

        var selectedNotice = tracker.GetCurrent(
            [first, second],
            selectedConversationId: second.Id);
        Assert.NotNull(selectedNotice);
        Assert.Equal("bg:second", selectedNotice.BackgroundId);
        Assert.Equal("后台命令失败（退出码 7）", selectedNotice.Text);

        Assert.True(tracker.AcknowledgeBackground("bg:second"));
        var aggregated = tracker.GetCurrent(
            [first, second],
            selectedConversationId: second.Id);
        Assert.NotNull(aggregated);
        Assert.Equal("First · 2 条后台命令待查看", aggregated.Text);

        Assert.True(tracker.AcknowledgeConversation(first.Id));
        Assert.Null(tracker.GetCurrent(
            [first, second],
            selectedConversationId: second.Id));
    }

    private static CopilotConversationRecord CreateConversation(
        string id,
        string title)
    {
        var conversation = CopilotConversationRecord.CreateEmpty(
            "profile",
            "Profile");
        conversation.Id = id;
        conversation.Title = title;
        return conversation;
    }

    private static CopilotBackgroundShellCommandSnapshot CreateSnapshot(
        string conversationId,
        CopilotBackgroundShellCommandState state,
        int? exitCode,
        string backgroundId = "bg:completion-secret")
    {
        return new CopilotBackgroundShellCommandSnapshot(
            backgroundId,
            conversationId,
            "task:completion-secret",
            CopilotShellKind.PowerShell,
            @"C:\workspace",
            "Write-Output command-secret",
            new string('a', 64),
            DateTimeOffset.UtcNow.AddSeconds(-5),
            state == CopilotBackgroundShellCommandState.Running
                ? null
                : DateTimeOffset.UtcNow,
            7_654,
            ProcessTreeContained: true,
            state,
            exitCode,
            StandardOutput: "output-secret",
            StandardError: string.Empty);
    }
}
