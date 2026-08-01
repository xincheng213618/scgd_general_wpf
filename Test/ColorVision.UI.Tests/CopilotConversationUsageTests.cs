using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationUsageTests
{
    [Fact]
    public void UsageCommandIsReadOnlyAndAvailableDuringAnActiveRequest()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/usage");
        var daily = CopilotLocalCommandCatalog.Parse("/usage daily");
        var legacyStats = CopilotLocalCommandCatalog.Parse("/stats 30");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Usage, invocation.Command.Kind);
        Assert.Empty(invocation.Arguments);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.True(invocation.Command.AcceptsArguments);
        Assert.NotNull(daily);
        Assert.Same(invocation.Command, daily.Command);
        Assert.Equal("daily", daily.Arguments);
        Assert.NotNull(legacyStats);
        Assert.Same(invocation.Command, legacyStats.Command);
        Assert.Equal("/stats", legacyStats.InvokedName);
        Assert.Equal("30", legacyStats.Arguments);
        Assert.Contains(CopilotLocalCommandCatalog.Suggest("/"), command => command.Name == "/usage");
    }

    [Fact]
    public void StatsAliasWithoutArgumentsKeepsTheLegacyDailyView()
    {
        var now = new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.FromHours(8));
        var conversation = CreateConversation();

        var report = CopilotUsageCommand.Format(
            conversation,
            [conversation],
            now,
            string.Empty,
            CopilotProviderRateLimitSnapshot.Empty,
            "/stats");

        Assert.StartsWith("/usage daily · 本地会话统计", report);
        Assert.Contains("范围：最近 7 天", report, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("daily", "最近 7 天", "/usage daily", "最近 7 日")]
    [InlineData("weekly", "最近 30 天", "/usage weekly", "本窗口周活动")]
    [InlineData("cumulative", "全部本地历史", "/usage cumulative", "最近活跃日")]
    [InlineData("7", "最近 7 天", "/usage daily", "最近 7 日")]
    [InlineData("30", "最近 30 天", "/usage weekly", "本窗口周活动")]
    [InlineData("all", "全部本地历史", "/usage cumulative", "最近活跃日")]
    public void UsageTimeViewsReuseBoundedLocalConversationStatistics(
        string arguments,
        string expectedWindow,
        string expectedHeading,
        string expectedDetailHeading)
    {
        var now = new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.FromHours(8));
        var conversation = new CopilotConversationRecord();
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Question")
        {
            CreatedAt = new DateTime(2026, 7, 31, 10, 0, 0),
        });
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Answer")
        {
            CreatedAt = new DateTime(2026, 7, 31, 10, 1, 0),
        };
        assistant.SetReportedUsage(new CopilotTokenUsage(80, 20, 100));
        conversation.Messages.Add(assistant);

        var report = CopilotUsageCommand.Format(
            conversation,
            [conversation],
            now,
            arguments,
            CopilotProviderRateLimitSnapshot.Empty);

        Assert.StartsWith(expectedHeading + " · 本地会话统计", report);
        Assert.Contains("范围：" + expectedWindow, report, StringComparison.Ordinal);
        Assert.Contains(expectedDetailHeading, report, StringComparison.Ordinal);
        Assert.Contains("Provider Token：已记录轮次 1/1", report, StringComparison.Ordinal);
        Assert.Contains("只汇总本机已保存消息", report, StringComparison.Ordinal);
        Assert.Contains("不代表账户账单", report, StringComparison.Ordinal);
    }

    [Fact]
    public void UsageSessionViewKeepsProviderLimitsAndRejectsUnknownViews()
    {
        var conversation = CreateConversation();
        var rateLimits = new CopilotProviderRateLimitSnapshot
        {
            CapturedAtUtc = new DateTimeOffset(2026, 7, 31, 9, 0, 0, TimeSpan.Zero),
            RequestLimit = 10,
            RequestRemaining = 3,
        };

        var report = CopilotUsageCommand.Format(
            conversation,
            [conversation],
            DateTimeOffset.Now,
            "session",
            rateLimits);

        Assert.StartsWith("使用量 · ", report);
        Assert.Contains("供应商限额：请求：剩余 3/10", report, StringComparison.Ordinal);
        Assert.Equal(
            "/usage 参数无效。可用 /usage、/usage session、/usage daily、/usage weekly 或 /usage cumulative。",
            CopilotUsageCommand.Format(
                conversation,
                [conversation],
                DateTimeOffset.Now,
                "account",
                rateLimits));
    }

    [Fact]
    public void UsageWeeklyViewAggregatesMondayThroughSundayBuckets()
    {
        var conversation = new CopilotConversationRecord();
        AddCompletedTurn(
            conversation,
            new DateTime(2026, 7, 2, 9, 0, 0),
            new CopilotTokenUsage(40, 10, 50));
        AddCompletedTurn(
            conversation,
            new DateTime(2026, 7, 20, 9, 0, 0),
            new CopilotTokenUsage(80, 20, 100));
        AddCompletedTurn(
            conversation,
            new DateTime(2026, 7, 27, 9, 0, 0),
            new CopilotTokenUsage(150, 50, 200));

        var report = CopilotUsageCommand.Format(
            conversation,
            [conversation],
            new DateTimeOffset(2026, 7, 31, 12, 0, 0, TimeSpan.FromHours(8)),
            "weekly",
            CopilotProviderRateLimitSnapshot.Empty);

        Assert.Contains(
            "07-02 至 07-05 · 提问 1 · 回答 1 · 中断 0 · Token 50",
            report,
            StringComparison.Ordinal);
        Assert.Contains(
            "07-20 至 07-26 · 提问 1 · 回答 1 · 中断 0 · Token 100",
            report,
            StringComparison.Ordinal);
        Assert.Contains(
            "07-27 至 07-31 · 提问 1 · 回答 1 · 中断 0 · Token 200",
            report,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TerminalResponsesPersistAndAggregateProviderReportedUsage()
    {
        var conversation = CreateConversation();
        var first = AddAssistant(conversation, "First answer");
        CopilotHostedTurnCompletion.CompleteTerminalTurn(
            conversation,
            first,
            new CopilotTokenUsage(100, 20, 120, 40));
        var unreported = AddAssistant(conversation, "Legacy answer");
        var second = AddAssistant(conversation, "Second answer");
        CopilotHostedTurnCompletion.CompleteTerminalTurn(
            conversation,
            second,
            new CopilotTokenUsage(200, 50, 260));
        var active = AddAssistant(conversation, string.Empty);
        active.IsResponsePending = true;

        var snapshot = CopilotConversationUsageDiagnostics.Capture(conversation);
        var report = CopilotConversationUsageDiagnostics.Format(conversation);

        Assert.Equal(new CopilotTokenUsage(300, 70, 380, 40), snapshot.TotalUsage);
        Assert.Equal(second.ReportedUsage, snapshot.LastUsage);
        Assert.Equal(2, snapshot.TrackedResponses);
        Assert.Equal(1, snapshot.UnreportedResponses);
        Assert.Equal(1, snapshot.ActiveResponses);
        Assert.False(unreported.ReportedUsage.HasAny);
        Assert.Contains("Provider", report, StringComparison.Ordinal);
        Assert.Contains("不代表账户账单", report, StringComparison.Ordinal);
        Assert.Contains("进行中：1 条", report, StringComparison.Ordinal);
    }

    [Fact]
    public void UsageIncludesLatestProviderLimitsWithoutClaimingAccountBalance()
    {
        var conversation = CreateConversation();
        var report = CopilotConversationUsageDiagnostics.Format(
            conversation,
            new CopilotProviderRateLimitSnapshot
            {
                CapturedAtUtc = new DateTimeOffset(2026, 7, 31, 8, 30, 0, TimeSpan.Zero),
                RequestLimit = 20,
                RequestRemaining = 4,
                RequestReset = "2s",
                TokenLimit = 1000,
                TokenRemaining = 250,
                TokenReset = "1s",
                RequestId = "req_usage_limit",
            });

        Assert.Contains(
            "供应商限额：请求：剩余 4/20（重置 2s） · Token：剩余 250/1,000（重置 1s） · 请求 req_usage_limit · 快照 2026-07-31 08:30:00 UTC",
            report,
            StringComparison.Ordinal);
        Assert.Contains("可能随时间过期", report, StringComparison.Ordinal);
        Assert.Contains("不代表账户套餐余额", report, StringComparison.Ordinal);
        Assert.DoesNotContain("账户剩余", report, StringComparison.Ordinal);
        Assert.DoesNotContain("费用：", report, StringComparison.Ordinal);
    }

    [Fact]
    public void UsageWithoutConversationStillShowsProviderLimitAvailability()
    {
        var report = CopilotConversationUsageDiagnostics.Format(
            conversation: null,
            CopilotProviderRateLimitSnapshot.Empty);

        Assert.Contains("当前没有可统计的 Copilot 会话", report, StringComparison.Ordinal);
        Assert.Contains(
            "供应商限额：尚未收到可识别的限额响应头",
            report,
            StringComparison.Ordinal);
        Assert.Contains("不代表账户套餐余额", report, StringComparison.Ordinal);
    }

    [Fact]
    public void InterruptedResponsesKeepActualUsageWithoutMasqueradingAsComplete()
    {
        var conversation = CreateConversation();
        var interrupted = AddAssistant(conversation, "Partial answer");
        interrupted.MarkResponseInterrupted("Provider stopped early.");

        CopilotHostedTurnCompletion.CompleteTerminalTurn(
            conversation,
            interrupted,
            new CopilotTokenUsage(80, 20, 100));

        var snapshot = CopilotConversationUsageDiagnostics.Capture(conversation);
        var report = CopilotConversationUsageDiagnostics.Format(conversation);

        Assert.Equal(1, snapshot.TrackedResponses);
        Assert.Equal(1, snapshot.InterruptedResponses);
        Assert.Equal(100, snapshot.TotalUsage.EffectiveTotalTokens);
        Assert.Contains("已记录轮次：1", report, StringComparison.Ordinal);
        Assert.Contains("标记中断：1 条", report, StringComparison.Ordinal);
        Assert.DoesNotContain("成功完成", report, StringComparison.Ordinal);
    }

    [Fact]
    public void UsageAggregatesAgentHierarchyRecoveryAndLatencyWithoutEstimatingCost()
    {
        var conversation = CreateConversation();
        var first = AddAssistant(conversation, "First Agent answer");
        first.AgentRunBudget = new CopilotAgentBudgetSnapshot
        {
            ProviderCalls = 3,
            ToolCalls = 2,
            ProviderRetryCount = 2,
            ProviderRateLimitRetryCount = 1,
            ProviderFirstContentTimeoutCount = 1,
            ProviderStreamInactivityTimeoutCount = 1,
            ProviderResponseCount = 2,
            ProviderFirstResponseLatencyTotalMs = 1_200,
            ProviderFirstResponseLatencyMaxMs = 800,
            ProviderCallDurationTotalMs = 4_200,
            ContextRecoveryCount = 1,
            ElapsedMs = 5_000,
            UsedEstimatedUsage = true,
        };
        first.AgentTraceEntries.Add(new CopilotAgentTraceEntry
        {
            CallId = "delegate-1",
            ToolName = "DelegateExplore",
            State = CopilotToolExecutionState.Completed,
            DelegatedRunId = "delegated-run-1",
            DelegatedProviderCalls = 1,
            DelegatedToolCalls = 4,
        });
        var second = AddAssistant(conversation, "Second Agent answer");
        second.AgentRunBudget = new CopilotAgentBudgetSnapshot
        {
            ProviderCalls = 2,
            ToolCalls = 1,
            ProviderResponseCount = 1,
            ProviderFirstResponseLatencyTotalMs = 900,
            ProviderFirstResponseLatencyMaxMs = 900,
            ProviderCallDurationTotalMs = 1_800,
            ElapsedMs = 3_000,
        };
        second.IsResponsePending = true;

        var snapshot = CopilotConversationUsageDiagnostics.Capture(conversation);
        var report = CopilotConversationUsageDiagnostics.Format(conversation);

        Assert.Equal(2, snapshot.AgentUsage.Runs);
        Assert.Equal(1, snapshot.AgentUsage.ActiveRuns);
        Assert.Equal(5, snapshot.AgentUsage.ProviderCalls);
        Assert.Equal(7, snapshot.AgentUsage.ToolCalls);
        Assert.Equal(1, snapshot.AgentUsage.DelegatedRuns);
        Assert.Equal(2, snapshot.AgentUsage.ProviderRetries);
        Assert.Equal(1, snapshot.AgentUsage.ProviderRateLimitRetries);
        Assert.Equal(2, snapshot.AgentUsage.ProviderStallTerminations);
        Assert.Equal(1, snapshot.AgentUsage.ContextRecoveries);
        Assert.Equal(3, snapshot.AgentUsage.ProviderResponses);
        Assert.Equal(2_100, snapshot.AgentUsage.ProviderFirstResponseLatencyTotalMs);
        Assert.Equal(900, snapshot.AgentUsage.ProviderFirstResponseLatencyMaxMs);
        Assert.Equal(6_000, snapshot.AgentUsage.ProviderCallDurationTotalMs);
        Assert.Equal(8_000, snapshot.AgentUsage.ElapsedMs);
        Assert.Equal(1, snapshot.AgentUsage.EstimatedUsageRuns);
        Assert.Contains("Agent 运行：2 轮（进行中 1） · 模型调用 5 · 工具调用 7 · 委派 1", report, StringComparison.Ordinal);
        Assert.Contains("时延：Agent 累计 8s · 首响应平均 700ms · 最慢 900ms · 模型调用累计 6s", report, StringComparison.Ordinal);
        Assert.Contains("恢复与估算：Provider 重试 2（限流 1） · 停顿中止 2 · 窗口恢复 1 · 预算计数含估算 1 轮", report, StringComparison.Ordinal);
        Assert.Contains("Agent 指标来自本地保存的任务快照", report, StringComparison.Ordinal);
        Assert.Contains("不代表账户账单", report, StringComparison.Ordinal);
        Assert.DoesNotContain("$", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportedUsageRoundTripsAndLegacyLastUsageMigratesOnlyToTheLatestAnswer()
    {
        var conversation = CreateConversation();
        var older = AddAssistant(conversation, "Older answer");
        var latest = AddAssistant(conversation, "Latest answer");
        conversation.SetLastUsage(new CopilotTokenUsage(90, 10, 100, 25));

        Assert.True(conversation.EnsureValid());
        Assert.False(older.ReportedUsage.HasAny);
        Assert.Equal(conversation.LastUsage, latest.ReportedUsage);

        var serialized = JsonConvert.SerializeObject(conversation);
        var restored = JsonConvert.DeserializeObject<CopilotConversationRecord>(serialized);

        Assert.NotNull(restored);
        restored.EnsureValid();
        Assert.False(restored.EnsureValid());
        Assert.Equal(new CopilotTokenUsage(90, 10, 100, 25), restored.Messages[^1].ReportedUsage);
        Assert.Contains(nameof(CopilotChatMessage.ReportedUsageTotalTokens), serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ReportedUsage\":", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void LegacyLastUsageSkipsAnAnswerInterruptedByRestart()
    {
        var conversation = CreateConversation();
        var completed = AddAssistant(conversation, "Completed answer");
        var interrupted = AddAssistant(conversation, string.Empty);
        interrupted.IsResponsePending = true;
        conversation.SetLastUsage(new CopilotTokenUsage(75, 15, 90));

        Assert.True(conversation.EnsureValid());

        Assert.True(interrupted.WasResponseInterrupted);
        Assert.False(interrupted.ReportedUsage.HasAny);
        Assert.Equal(conversation.LastUsage, completed.ReportedUsage);
    }

    [Fact]
    public void BranchUsageStopsAtTheSelectedAssistantMessage()
    {
        var conversation = CreateConversation();
        var first = AddAssistant(conversation, "First answer");
        first.SetReportedUsage(new CopilotTokenUsage(60, 10, 70));
        var second = AddAssistant(conversation, "Second answer");
        second.SetReportedUsage(new CopilotTokenUsage(120, 20, 140));

        var branch = CopilotConversationBranchService.CreateBranch(conversation, first);
        var sourceUsage = CopilotConversationUsageDiagnostics.Capture(conversation);
        var branchUsage = CopilotConversationUsageDiagnostics.Capture(branch);

        Assert.Equal(210, sourceUsage.TotalUsage.EffectiveTotalTokens);
        Assert.Equal(70, branchUsage.TotalUsage.EffectiveTotalTokens);
        Assert.Single(branch.Messages);
        Assert.Equal(first.ReportedUsage, branch.Messages[0].ReportedUsage);
    }

    [Fact]
    public void UsageAdditionPreservesProviderTotalsAndSaturatesInsteadOfOverflowing()
    {
        var combined = new CopilotTokenUsage(int.MaxValue, 1, int.MaxValue)
            .Add(new CopilotTokenUsage(1, int.MaxValue, int.MaxValue));

        Assert.Equal(int.MaxValue, combined.InputTokens);
        Assert.Equal(int.MaxValue, combined.OutputTokens);
        Assert.Equal(int.MaxValue, combined.EffectiveTotalTokens);
    }

    [Fact]
    public void UsageProgressSaturatesDerivedTotalInsteadOfOverflowing()
    {
        var merged = new CopilotTokenUsage(int.MaxValue, 0, 0)
            .MergeProgress(new CopilotTokenUsage(0, int.MaxValue, 0));

        Assert.Equal(int.MaxValue, merged.TotalTokens);
        Assert.Equal(int.MaxValue, merged.EffectiveTotalTokens);
    }

    private static CopilotConversationRecord CreateConversation()
    {
        return CopilotConversationRecord.CreateEmpty("profile-id", "Primary");
    }

    private static CopilotChatMessage AddAssistant(
        CopilotConversationRecord conversation,
        string content)
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, content);
        conversation.Messages.Add(message);
        return message;
    }

    private static void AddCompletedTurn(
        CopilotConversationRecord conversation,
        DateTime createdAt,
        CopilotTokenUsage usage)
    {
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Question")
        {
            CreatedAt = createdAt,
        });
        var assistant = AddAssistant(conversation, "Answer");
        assistant.CreatedAt = createdAt.AddMinutes(1);
        assistant.SetReportedUsage(usage);
    }
}
