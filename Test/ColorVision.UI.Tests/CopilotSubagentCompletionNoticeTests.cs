using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotSubagentCompletionNoticeTests
{
    [Fact]
    public void SnapshotComesOnlyFromAValidDelegatedToolResult()
    {
        var ordinary = new CopilotToolResult
        {
            ToolName = "ReadLocalFile",
            Success = true,
            Content = "ordinary-result-secret",
        };
        var delegated = new CopilotToolResult
        {
            ToolName = "DelegateExplore",
            Success = true,
            Content = "delegated-result-secret",
            DelegatedRunUsage = new CopilotDelegatedRunUsage
            {
                RunId = "explore-finished",
                RoleId = "explore",
                StopReason = CopilotAgentStopReason.Completed,
            },
            DelegatedAnswer = new CopilotDelegatedAnswer
            {
                Text = "delegated-answer-secret",
                StopReason = CopilotAgentStopReason.Completed,
            },
        };
        var unsafeDelegated = new CopilotToolResult
        {
            ToolName = "DelegateExplore",
            DelegatedRunUsage = new CopilotDelegatedRunUsage
            {
                RunId = "../unsafe",
                RoleId = "explore",
                StopReason = CopilotAgentStopReason.Completed,
            },
        };
        var oversizedDelegated = new CopilotToolResult
        {
            DelegatedRunUsage = new CopilotDelegatedRunUsage
            {
                RunId = new string('a', 121),
                RoleId = "explore",
                StopReason = CopilotAgentStopReason.Completed,
            },
        };

        Assert.Null(CopilotSubagentCompletionNoticePolicy.CreateSnapshot(
            ordinary,
            "conversation"));
        Assert.Null(CopilotSubagentCompletionNoticePolicy.CreateSnapshot(
            delegated,
            string.Empty));
        Assert.Null(CopilotSubagentCompletionNoticePolicy.CreateSnapshot(
            unsafeDelegated,
            "conversation"));
        Assert.Null(CopilotSubagentCompletionNoticePolicy.CreateSnapshot(
            oversizedDelegated,
            "conversation"));

        var snapshot = CopilotSubagentCompletionNoticePolicy.CreateSnapshot(
            delegated,
            "conversation");

        Assert.NotNull(snapshot);
        Assert.Equal("conversation", snapshot.ConversationId);
        Assert.Equal("explore-finished", snapshot.RunId);
        Assert.Equal("explore", snapshot.RoleId);
        Assert.Equal(CopilotAgentStopReason.Completed, snapshot.StopReason);
        Assert.DoesNotContain("secret", snapshot.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData((int)CopilotAgentStopReason.Completed, "Explore 子代理已完成")]
    [InlineData((int)CopilotAgentStopReason.BudgetExhausted, "Explore 子代理预算耗尽")]
    [InlineData((int)CopilotAgentStopReason.ProviderFailure, "Explore 子代理模型连接中断")]
    [InlineData((int)CopilotAgentStopReason.Cancelled, "Explore 子代理已取消")]
    public void SelectedConversationNoticeUsesKnownRoleAndTerminalStatus(
        int stopReasonValue,
        string expected)
    {
        var conversation = CreateConversation("conversation", "Delegation");
        var snapshot = CreateSnapshot(
            conversation.Id,
            "explore-finished",
            "explore",
            (CopilotAgentStopReason)stopReasonValue);

        var notice = CopilotSubagentCompletionNoticePolicy.Create(
            snapshot,
            conversation,
            selectedConversationId: conversation.Id);

        Assert.NotNull(notice);
        Assert.Equal(conversation.Id, notice.ConversationId);
        Assert.Equal(snapshot.RunId, notice.RunId);
        Assert.Equal(expected, notice.Text);
        Assert.DoesNotContain(snapshot.RunId, notice.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnotherConversationNoticeUsesBoundedTitleAndAggregatesPendingRuns()
    {
        var conversation = CreateConversation(
            "conversation-background",
            "  Background   delegation  ");
        var snapshot = CreateSnapshot(
            conversation.Id,
            "scout-finished",
            "scout",
            CopilotAgentStopReason.Completed);

        var single = CopilotSubagentCompletionNoticePolicy.Create(
            snapshot,
            conversation,
            selectedConversationId: "conversation-selected");
        var aggregate = CopilotSubagentCompletionNoticePolicy.Create(
            snapshot,
            conversation,
            selectedConversationId: "conversation-selected",
            pendingCount: 3);

        Assert.NotNull(single);
        Assert.Equal(
            "Background delegation · Scout 子代理已完成",
            single.Text);
        Assert.NotNull(aggregate);
        Assert.Equal(
            "Background delegation · 3 个子代理结果待查看",
            aggregate.Text);
    }

    [Fact]
    public void UnknownRoleAndUnsafeIdentityAreNeverEchoed()
    {
        var conversation = CreateConversation("conversation", "Task");
        var unknownRole = CreateSnapshot(
            conversation.Id,
            "custom-finished",
            "untrusted-role-label",
            CopilotAgentStopReason.Blocked);

        var notice = CopilotSubagentCompletionNoticePolicy.Create(
            unknownRole,
            conversation,
            selectedConversationId: conversation.Id);

        Assert.NotNull(notice);
        Assert.Equal("子代理任务受阻", notice.Text);
        Assert.DoesNotContain(unknownRole.RoleId, notice.Text, StringComparison.Ordinal);
        Assert.Null(CopilotSubagentCompletionNoticePolicy.Create(
            unknownRole with { RunId = "../unsafe" },
            conversation,
            selectedConversationId: conversation.Id));
    }

    [Fact]
    public void MismatchedOrArchivedConversationDoesNotCreateNotice()
    {
        var conversation = CreateConversation("conversation", "Task");
        var snapshot = CreateSnapshot(
            "another-conversation",
            "explore-finished",
            "explore",
            CopilotAgentStopReason.Completed);

        Assert.Null(CopilotSubagentCompletionNoticePolicy.Create(
            snapshot,
            conversation,
            selectedConversationId: conversation.Id));

        conversation.IsArchived = true;
        Assert.Null(CopilotSubagentCompletionNoticePolicy.Create(
            snapshot with { ConversationId = conversation.Id },
            conversation,
            selectedConversationId: conversation.Id));
    }

    [Fact]
    public void TrackerPrioritizesSelectedConversationDeduplicatesAndAcknowledgesExactly()
    {
        var first = CreateConversation("conversation-first", "First");
        var second = CreateConversation("conversation-second", "Second");
        var tracker = new CopilotSubagentCompletionNoticeTracker();
        Assert.True(tracker.Capture(
            CreateSnapshot(
                first.Id,
                "shared-run",
                "explore",
                CopilotAgentStopReason.Completed),
            first,
            selectedConversationId: second.Id));
        Assert.True(tracker.Capture(
            CreateSnapshot(
                second.Id,
                "shared-run",
                "scout",
                CopilotAgentStopReason.ProviderFailure),
            second,
            selectedConversationId: second.Id));
        Assert.True(tracker.Capture(
            CreateSnapshot(
                first.Id,
                "first-latest",
                "scout",
                CopilotAgentStopReason.Completed),
            first,
            selectedConversationId: second.Id));
        Assert.True(tracker.Capture(
            CreateSnapshot(
                first.Id,
                "first-latest",
                "scout",
                CopilotAgentStopReason.BudgetExhausted),
            first,
            selectedConversationId: second.Id));

        var selectedNotice = tracker.GetCurrent(
            [first, second],
            selectedConversationId: second.Id);
        Assert.NotNull(selectedNotice);
        Assert.Equal(second.Id, selectedNotice.ConversationId);
        Assert.Equal("shared-run", selectedNotice.RunId);
        Assert.Equal("Scout 子代理模型连接中断", selectedNotice.Text);

        Assert.True(tracker.AcknowledgeRun(second.Id, "shared-run"));
        var aggregated = tracker.GetCurrent(
            [first, second],
            selectedConversationId: second.Id);
        Assert.NotNull(aggregated);
        Assert.Equal(first.Id, aggregated.ConversationId);
        Assert.Equal("First · 2 个子代理结果待查看", aggregated.Text);

        Assert.True(tracker.AcknowledgeRun(first.Id, "shared-run"));
        var remaining = tracker.GetCurrent(
            [first, second],
            selectedConversationId: first.Id);
        Assert.NotNull(remaining);
        Assert.Equal("first-latest", remaining.RunId);
        Assert.Equal("Scout 子代理预算耗尽", remaining.Text);

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

    private static CopilotSubagentCompletionSnapshot CreateSnapshot(
        string conversationId,
        string runId,
        string roleId,
        CopilotAgentStopReason stopReason)
    {
        return new CopilotSubagentCompletionSnapshot(
            conversationId,
            runId,
            roleId,
            stopReason);
    }
}
