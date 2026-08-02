using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationStatisticsTests
{
    [Fact]
    public void StatsAliasResolvesToUsageAndRemainsAvailableDuringAnActiveRun()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/stats 30");
        var usage = CopilotLocalCommandCatalog.FindExact("/usage");

        Assert.NotNull(invocation);
        Assert.Same(usage, invocation.Command);
        Assert.Equal(CopilotLocalCommandKind.Usage, invocation.Command.Kind);
        Assert.Equal("/stats", invocation.InvokedName);
        Assert.Equal("30", invocation.Arguments);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/usage");
        Assert.DoesNotContain(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/stats");
    }

    [Fact]
    public void CaptureDoesNotDoubleCountHistoryCopiedIntoABranch()
    {
        var now = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.FromHours(8));
        var root = new CopilotConversationRecord
        {
            CreatedAt = new DateTime(2026, 7, 28, 9, 0, 0),
            UpdatedAt = new DateTime(2026, 7, 28, 9, 5, 0),
        };
        root.Messages.Add(CreateUserMessage(new DateTime(2026, 7, 28, 9, 0, 0)));
        var rootResponse = CreateAssistantMessage(
            new DateTime(2026, 7, 28, 9, 5, 0),
            new CopilotTokenUsage(100, 50, 150, 40));
        root.Messages.Add(rootResponse);

        var branch = CopilotConversationBranchService.CreateBranch(root, rootResponse, "Alternative");
        branch.CreatedAt = new DateTime(2026, 7, 29, 10, 0, 0);
        branch.Messages.Add(CreateUserMessage(new DateTime(2026, 7, 30, 10, 0, 0)));
        branch.Messages.Add(CreateAssistantMessage(
            new DateTime(2026, 7, 30, 10, 2, 0),
            new CopilotTokenUsage(200, 100, 300, 80)));

        var snapshot = CopilotConversationStatistics.Capture(
            [root, branch],
            now,
            CopilotConversationStatisticsWindow.SevenDays);

        Assert.Equal(2, snapshot.StoredConversations);
        Assert.Equal(2, snapshot.ActiveConversations);
        Assert.Equal(2, snapshot.UserTurns);
        Assert.Equal(2, snapshot.TerminalResponses);
        Assert.Equal(0, snapshot.InterruptedResponses);
        Assert.Equal(2, snapshot.TrackedResponses);
        Assert.Equal(300, snapshot.Usage.InputTokens);
        Assert.Equal(150, snapshot.Usage.OutputTokens);
        Assert.Equal(450, snapshot.Usage.EffectiveTotalTokens);
        Assert.Equal(120, snapshot.Usage.EffectiveCachedInputTokens);
    }

    [Fact]
    public void ThirtyDayWindowExcludesOlderMessagesAndSeparatesActiveResponses()
    {
        var now = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.FromHours(8));
        var conversation = new CopilotConversationRecord();
        conversation.Messages.Add(CreateUserMessage(new DateTime(2026, 6, 10, 8, 0, 0)));
        conversation.Messages.Add(CreateAssistantMessage(
            new DateTime(2026, 6, 10, 8, 1, 0),
            new CopilotTokenUsage(500, 250, 750)));
        conversation.Messages.Add(CreateUserMessage(new DateTime(2026, 7, 25, 8, 0, 0)));
        conversation.Messages.Add(CreateAssistantMessage(
            new DateTime(2026, 7, 25, 8, 1, 0),
            new CopilotTokenUsage(20, 10, 30)));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            CreatedAt = new DateTime(2026, 7, 30, 11, 59, 0),
            IsResponsePending = true,
        });

        var snapshot = CopilotConversationStatistics.Capture(
            [conversation],
            now,
            CopilotConversationStatisticsWindow.ThirtyDays);

        Assert.Equal(new DateOnly(2026, 7, 1), snapshot.StartDate);
        Assert.Equal(1, snapshot.UserTurns);
        Assert.Equal(1, snapshot.TerminalResponses);
        Assert.Equal(0, snapshot.InterruptedResponses);
        Assert.Equal(1, snapshot.TrackedResponses);
        Assert.Equal(1, snapshot.ActiveResponses);
        Assert.Equal(30, snapshot.Usage.EffectiveTotalTokens);
    }

    [Fact]
    public void UsageCumulativeFormatShowsLocalActivity()
    {
        var now = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.FromHours(8));
        var conversation = new CopilotConversationRecord();
        conversation.Messages.Add(CreateUserMessage(new DateTime(2026, 7, 29, 9, 0, 0)));
        conversation.Messages.Add(CreateAssistantMessage(
            new DateTime(2026, 7, 29, 9, 1, 0),
            new CopilotTokenUsage(10, 5, 15)));
        conversation.Messages.Add(CreateUserMessage(new DateTime(2026, 7, 30, 9, 0, 0)));
        conversation.Messages.Add(CreateAssistantMessage(
            new DateTime(2026, 7, 30, 9, 1, 0),
            CopilotTokenUsage.Empty));

        var report = CopilotUsageCommand.Format(
            conversation,
            [conversation],
            now,
            "all",
            CopilotProviderRateLimitSnapshot.Empty);

        Assert.Contains("/usage cumulative · 本地会话统计", report, StringComparison.Ordinal);
        Assert.Contains("范围：全部本地历史 · 2026-07-29 至 2026-07-30", report, StringComparison.Ordinal);
        Assert.Contains("Provider Token：已记录轮次 1/2", report, StringComparison.Ordinal);
        Assert.Contains("未纳入：1 条旧回答、失败回答或未返回 Token 元数据的回答。", report, StringComparison.Ordinal);
        Assert.Contains("全历史当前连续 2 天 · 最长连续 2 天", report, StringComparison.Ordinal);
        Assert.Contains("会话分支复制的历史前缀不会重复计数", report, StringComparison.Ordinal);
        Assert.DoesNotContain('$', report);
    }

    [Fact]
    public void InterruptedResponsesRemainInUsageButHaveDistinctTerminalStatus()
    {
        var now = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.FromHours(8));
        var conversation = new CopilotConversationRecord();
        var interrupted = CreateAssistantMessage(
            new DateTime(2026, 7, 30, 11, 0, 0),
            new CopilotTokenUsage(40, 10, 50));
        interrupted.MarkResponseInterrupted("Provider stopped early.");
        conversation.Messages.Add(interrupted);

        var snapshot = CopilotConversationStatistics.Capture(
            [conversation],
            now,
            CopilotConversationStatisticsWindow.SevenDays);
        var report = CopilotConversationStatistics.Format(
            snapshot,
            "/usage daily",
            CopilotConversationStatisticsDetailMode.Daily);

        Assert.Equal(1, snapshot.TerminalResponses);
        Assert.Equal(1, snapshot.InterruptedResponses);
        Assert.Equal(1, snapshot.TrackedResponses);
        Assert.Equal(50, snapshot.Usage.EffectiveTotalTokens);
        Assert.Contains("已结束回答 1 · 标记中断 1", report, StringComparison.Ordinal);
        Assert.Contains("Provider Token：已记录轮次 1/1", report, StringComparison.Ordinal);
        Assert.DoesNotContain("已完成回答", report, StringComparison.Ordinal);
    }

    private static CopilotChatMessage CreateUserMessage(DateTime createdAt)
    {
        return new CopilotChatMessage(CopilotChatRole.User, "Question")
        {
            CreatedAt = createdAt,
        };
    }

    private static CopilotChatMessage CreateAssistantMessage(
        DateTime createdAt,
        CopilotTokenUsage usage)
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, "Answer")
        {
            CreatedAt = createdAt,
        };
        message.SetReportedUsage(usage);
        return message;
    }
}
