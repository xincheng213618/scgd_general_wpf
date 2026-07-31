using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotQueuedFollowUpDiagnosticsTests
{
    [Fact]
    public void QueueCommandAcceptsExplicitActionsDuringAgentRuns()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/queue");
        var send = CopilotLocalCommandCatalog.Parse("/queue send 3");
        var clear = CopilotLocalCommandCatalog.Parse("/queue clear");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Queue, invocation.Command.Kind);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.True(invocation.Command.AcceptsArguments);
        Assert.NotNull(send);
        Assert.Same(invocation.Command, send.Command);
        Assert.Equal("send 3", send.Arguments);
        Assert.NotNull(clear);
        Assert.Same(invocation.Command, clear.Command);
        Assert.Equal("clear", clear.Arguments);
    }

    [Theory]
    [InlineData("", (int)CopilotQueuedFollowUpCommandAction.List, 0)]
    [InlineData("clear", (int)CopilotQueuedFollowUpCommandAction.Clear, 0)]
    [InlineData("send 3", (int)CopilotQueuedFollowUpCommandAction.SendNow, 3)]
    [InlineData("now 2", (int)CopilotQueuedFollowUpCommandAction.SendNow, 2)]
    [InlineData("edit 4", (int)CopilotQueuedFollowUpCommandAction.Edit, 4)]
    [InlineData("up 5", (int)CopilotQueuedFollowUpCommandAction.MoveUp, 5)]
    [InlineData("down 6", (int)CopilotQueuedFollowUpCommandAction.MoveDown, 6)]
    [InlineData("delete 7", (int)CopilotQueuedFollowUpCommandAction.Delete, 7)]
    [InlineData("cancel 8", (int)CopilotQueuedFollowUpCommandAction.Delete, 8)]
    [InlineData("delete", (int)CopilotQueuedFollowUpCommandAction.Invalid, 0)]
    [InlineData("send zero", (int)CopilotQueuedFollowUpCommandAction.Invalid, 0)]
    [InlineData("unknown 1", (int)CopilotQueuedFollowUpCommandAction.Invalid, 1)]
    public void CommandParserRequiresAnExplicitPositiveGlobalPosition(
        string arguments,
        int expectedAction,
        int expectedPosition)
    {
        var request = CopilotQueuedFollowUpDiagnostics.ParseCommand(arguments);

        Assert.Equal((CopilotQueuedFollowUpCommandAction)expectedAction, request.Action);
        Assert.Equal(expectedPosition, request.QueuePosition);
    }

    [Fact]
    public void ReportListsOnlySelectedConversationWithoutHiddenContext()
    {
        var first = CreateFollowUp(
            "run-private-1",
            "conversation-1",
            "First queued request",
            CopilotAgentMode.Code,
            [CopilotAttachmentItem.CreateContext("private attachment body", "Context")]);
        var second = CreateFollowUp(
            "run-private-2",
            "conversation-1",
            "Second\nqueued\trequest",
            CopilotAgentMode.Plan,
            goalId: "goal-private");
        var foreign = CreateFollowUp(
            "run-private-3",
            "conversation-2",
            "Foreign conversation prompt",
            CopilotAgentMode.Chat);
        first.UpdateQueuePosition(1, 3);
        foreign.UpdateQueuePosition(2, 3);
        second.UpdateQueuePosition(3, 3);

        var report = CopilotQueuedFollowUpDiagnostics.Format(
            [second, foreign, first],
            "conversation-1");

        Assert.Contains("当前会话排队 · 2", report, StringComparison.Ordinal);
        Assert.True(
            report.IndexOf("#1 · Code · First queued request · 附件 1", StringComparison.Ordinal)
            < report.IndexOf("#3 · Plan · Second queued request · 持续目标", StringComparison.Ordinal));
        Assert.DoesNotContain("Foreign conversation prompt", report, StringComparison.Ordinal);
        Assert.DoesNotContain("private attachment body", report, StringComparison.Ordinal);
        Assert.DoesNotContain("run-private", report, StringComparison.Ordinal);
        Assert.DoesNotContain("goal-private", report, StringComparison.Ordinal);
        Assert.Contains("列表本身只读", report, StringComparison.Ordinal);
        Assert.Contains("clear、send、edit、up、down、delete", report, StringComparison.Ordinal);
        Assert.Equal(
            [first, second],
            CopilotQueuedFollowUpDiagnostics.GetItems(
                [second, foreign, first],
                "conversation-1"));
        var confirmation = CopilotQueuedFollowUpDiagnostics.FormatClearConfirmation(
            "Conversation",
            [first, second]);
        Assert.Contains("Conversation", confirmation, StringComparison.Ordinal);
        Assert.Contains("2 条排队后续", confirmation, StringComparison.Ordinal);
        Assert.Contains("其中 1 条是自动续作", confirmation, StringComparison.Ordinal);
        Assert.Contains("其他会话不受影响", confirmation, StringComparison.Ordinal);
        Assert.DoesNotContain("First queued request", confirmation, StringComparison.Ordinal);
        Assert.DoesNotContain("Second", confirmation, StringComparison.Ordinal);
        Assert.DoesNotContain("run-private", confirmation, StringComparison.Ordinal);
        Assert.DoesNotContain("goal-private", confirmation, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\private", confirmation, StringComparison.Ordinal);
        Assert.Same(
            second,
            CopilotQueuedFollowUpDiagnostics.FindByPosition(
                [second, foreign, first],
                "conversation-1",
                3));
        Assert.Null(CopilotQueuedFollowUpDiagnostics.FindByPosition(
            [second, foreign, first],
            "conversation-1",
            2));
    }

    [Fact]
    public void ReportHandlesMissingConversationAndEmptyQueue()
    {
        Assert.Contains(
            "没有可用于查看",
            CopilotQueuedFollowUpDiagnostics.Format([], null),
            StringComparison.Ordinal);
        Assert.Contains(
            "没有排队",
            CopilotQueuedFollowUpDiagnostics.Format([], "conversation-1"),
            StringComparison.Ordinal);
    }

    private static CopilotQueuedFollowUp CreateFollowUp(
        string runId,
        string conversationId,
        string prompt,
        CopilotAgentMode mode,
        IReadOnlyList<CopilotAttachmentItem>? attachments = null,
        string? goalId = null)
    {
        return new CopilotQueuedFollowUp(
            runId,
            conversationId,
            "Conversation",
            prompt,
            mode,
            new CopilotProfileConfig(),
            new CopilotAgentHostContextSnapshot(
                @"C:\private\active.cs",
                @"C:\private\workspace",
                attachments),
            goalId);
    }
}
