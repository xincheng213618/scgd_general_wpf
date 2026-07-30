using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotQueuedFollowUpDiagnosticsTests
{
    [Fact]
    public void QueueCommandIsReadOnlyAndAvailableDuringAgentRuns()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/queue");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Queue, invocation.Command.Kind);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.False(invocation.Command.AcceptsArguments);
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
        Assert.Contains("报告只读", report, StringComparison.Ordinal);
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
