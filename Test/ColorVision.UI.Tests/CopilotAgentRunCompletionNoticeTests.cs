using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentRunCompletionNoticeTests
{
    [Fact]
    public void CompletedBackgroundAgentCreatesABoundedNoticeWithoutInternalContent()
    {
        var conversation = CreateConversation(
            "conversation-background",
            "  Background   verification  ",
            CopilotAgentStopReason.Completed);
        var run = CreateCompletedRun(conversation.Id, CopilotAgentMode.Code, CopilotAgentStopReason.Completed);

        var notice = CopilotAgentRunCompletionNoticePolicy.Create(
            run,
            conversation,
            selectedConversationId: "conversation-selected");

        Assert.NotNull(notice);
        Assert.Equal(conversation.Id, notice.ConversationId);
        Assert.Equal("Background verification · 已完成", notice.Text);
        Assert.DoesNotContain(run.Id, notice.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("prompt secret", notice.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void SelectedConversationAndChatRunsDoNotCreateBackgroundCompletionNotices()
    {
        var conversation = CreateConversation(
            "conversation-selected",
            "Selected",
            CopilotAgentStopReason.Completed);
        var agentRun = CreateCompletedRun(conversation.Id, CopilotAgentMode.Auto, CopilotAgentStopReason.Completed);
        var chatRun = CreateCompletedRun(conversation.Id, CopilotAgentMode.Chat, CopilotAgentStopReason.Completed);

        Assert.Null(CopilotAgentRunCompletionNoticePolicy.Create(
            agentRun,
            conversation,
            selectedConversationId: conversation.Id));
        Assert.Null(CopilotAgentRunCompletionNoticePolicy.Create(
            chatRun,
            conversation,
            selectedConversationId: "another-conversation"));
    }

    [Theory]
    [InlineData(2, "等待回复")]
    [InlineData(3, "审批未通过")]
    [InlineData(4, "预算耗尽")]
    [InlineData(5, "达到轮次上限")]
    [InlineData(6, "任务受阻")]
    [InlineData(7, "已暂停，可继续")]
    [InlineData(8, "已取消")]
    [InlineData(9, "等待最终回答")]
    [InlineData(10, "模型连接中断")]
    [InlineData(11, "等待最终回答")]
    public void NoticeMapsPersistedAgentStopReasons(int stopReasonValue, string expectedStatus)
    {
        var stopReason = (CopilotAgentStopReason)stopReasonValue;
        var conversation = CreateConversation("conversation", "Task", stopReason);
        var run = CreateCompletedRun(conversation.Id, CopilotAgentMode.Auto, stopReason);

        var notice = CopilotAgentRunCompletionNoticePolicy.Create(
            run,
            conversation,
            selectedConversationId: "selected");

        Assert.NotNull(notice);
        Assert.EndsWith(" · " + expectedStatus, notice.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void InterruptedFinalAnswerUsesTheRecoverableFinalAnswerStatus()
    {
        var conversation = CreateConversation(
            "conversation",
            "Final answer",
            CopilotAgentStopReason.Completed);
        conversation.Messages[^1].WasResponseInterrupted = true;
        var run = CreateCompletedRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            CopilotAgentStopReason.Completed);

        var notice = CopilotAgentRunCompletionNoticePolicy.Create(
            run,
            conversation,
            selectedConversationId: "selected");

        Assert.NotNull(notice);
        Assert.Equal("Final answer · 等待最终回答", notice.Text);
    }

    [Fact]
    public void InterruptedIncompleteTaskUsesTheTaskRecoveryStatus()
    {
        var conversation = CreateConversation(
            "conversation",
            "Interrupted task",
            CopilotAgentStopReason.Interrupted);
        conversation.Messages[^1].AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
        {
            Mode = "execute",
            Items =
            [
                new CopilotAgentTaskItem
                {
                    Id = 1,
                    Title = "Continue verification",
                    IsComplete = false,
                },
            ],
        };
        var run = CreateCompletedRun(
            conversation.Id,
            CopilotAgentMode.Auto,
            CopilotAgentStopReason.Interrupted);

        var notice = CopilotAgentRunCompletionNoticePolicy.Create(
            run,
            conversation,
            selectedConversationId: "selected");

        Assert.NotNull(notice);
        Assert.Equal("Interrupted task · 应用中断，可继续", notice.Text);
    }

    private static CopilotConversationRecord CreateConversation(
        string id,
        string title,
        CopilotAgentStopReason stopReason)
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Id = id;
        conversation.Title = title;
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "prompt secret"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "visible result")
        {
            AgentStopReason = stopReason,
        });
        return conversation;
    }

    private static CopilotHostedAgentRun CreateCompletedRun(
        string conversationId,
        CopilotAgentMode mode,
        CopilotAgentStopReason stopReason)
    {
        var run = new CopilotHostedAgentRun(conversationId, mode);
        Assert.True(run.TryStart());
        run.SetAgentStopReason(stopReason);
        run.Complete(error: null);
        return run;
    }
}
