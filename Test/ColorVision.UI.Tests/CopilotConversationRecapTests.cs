using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationRecapTests
{
    [Fact]
    public void RecapCommandIsArgumentFreeAndAvailableDuringAgentRuns()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/recap");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Recap, invocation.Command.Kind);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Null(CopilotLocalCommandCatalog.Parse("/recap now"));
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/recap");
    }

    [Fact]
    public void EmptyRecapRemainsLocalAndActionable()
    {
        var report = CopilotConversationRecap.Format(null, 0);

        Assert.Contains("当前没有可回顾的会话", report);
        Assert.Contains("不调用模型、工具或外部服务", report);
    }

    [Fact]
    public void ReportSummarizesVisibleTurnGoalAndPendingStateWithoutPrivatePayloads()
    {
        var conversation = new CopilotConversationRecord
        {
            Title = "Camera investigation",
            UpdatedAt = new DateTime(2026, 7, 31, 9, 45, 0),
            DraftText = "private unsent draft",
            Goal = CopilotConversationGoal.Create(
                "Continue verifying camera recovery",
                new DateTimeOffset(2026, 7, 31, 8, 0, 0, TimeSpan.Zero)),
        };
        conversation.Attachments.Add(CopilotAttachmentItem.CreateFile(@"C:\private\camera.log"));
        conversation.ComposerStash = CopilotComposerStash.Capture(
            "private stashed prompt",
            3,
            CopilotAgentMode.Diagnose,
            [CopilotAttachmentItem.CreateFile(@"C:\private\stash.txt")]);
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Inspect the camera recovery")
        {
            RequestContent = "hidden request payload",
        });
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "The service is reachable, but verification is incomplete.")
        {
            ReasoningContent = "hidden reasoning",
            ExecutionContent = "hidden tool trace",
            RequestMode = CopilotAgentMode.Code,
            AgentStopReason = CopilotAgentStopReason.ProviderFailure,
            WasResponseInterrupted = true,
            AgentTaskLedger = new CopilotAgentTaskLedgerSnapshot
            {
                Mode = "execute",
                Items =
                [
                    new CopilotAgentTaskItem { Id = 1, Title = "Inspect", IsComplete = true },
                    new CopilotAgentTaskItem { Id = 2, Title = "Verify", IsComplete = false },
                ],
            },
        });

        var report = CopilotConversationRecap.Format(conversation, 2);

        Assert.Contains("会话：Camera investigation · 更新于 2026-07-31 09:45", report);
        Assert.Contains("持续目标：活动 · Continue verifying camera recovery", report);
        Assert.Contains("最近请求：Inspect the camera recovery", report);
        Assert.Contains("最近回答：The service is reachable, but verification is incomplete.", report);
        Assert.Contains("1/2 已完成", report);
        Assert.Contains("模型连接中断", report);
        Assert.Contains("回答不完整", report);
        Assert.Contains("可操作：继续任务", report);
        Assert.Contains("待执行：2 条排队后续", report);
        Assert.Contains("草稿 20 字符 · 1 个附件 · 有暂存草稿", report);
        Assert.DoesNotContain("hidden request payload", report, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden reasoning", report, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden tool trace", report, StringComparison.Ordinal);
        Assert.DoesNotContain("private unsent draft", report, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\private", report, StringComparison.Ordinal);
    }

    [Fact]
    public void VisiblePreviewsAreBoundedWithoutSplittingSurrogatePairs()
    {
        var conversation = new CopilotConversationRecord();
        conversation.Messages.Add(new CopilotChatMessage(
            CopilotChatRole.User,
            new string('x', CopilotConversationRecap.MaximumPreviewCharacters - 1) + "😀tail"));

        var report = CopilotConversationRecap.Format(conversation, -3);
        var requestLine = report.Split(Environment.NewLine)
            .Single(line => line.StartsWith("最近请求：", StringComparison.Ordinal));
        var preview = requestLine["最近请求：".Length..];

        Assert.Equal(CopilotConversationRecap.MaximumPreviewCharacters, preview.Length);
        Assert.DoesNotContain('�', preview);
        Assert.EndsWith("…", preview, StringComparison.Ordinal);
        Assert.Contains("待执行：0 条排队后续", report);
    }
}
