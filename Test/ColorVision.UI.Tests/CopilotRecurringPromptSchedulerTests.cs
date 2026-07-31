using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotRecurringPromptSchedulerTests
{
    [Theory]
    [InlineData("30m check deployment", 30, "check deployment", false)]
    [InlineData("check deployment every hour", 60, "check deployment", false)]
    [InlineData("每 30 分钟 检查部署", 30, "检查部署", false)]
    [InlineData("30s check deployment", 1, "check deployment", true)]
    public void ParserAcceptsCompactAndNaturalIntervals(
        string arguments,
        int expectedMinutes,
        string expectedPrompt,
        bool expectedClamp)
    {
        var request = CopilotLoopCommand.Parse(arguments);

        Assert.Equal(CopilotLoopCommandAction.Create, request.Action);
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), request.Interval);
        Assert.Equal(expectedPrompt, request.Prompt);
        Assert.Equal(expectedClamp, request.IntervalWasClamped);
        Assert.Empty(request.ErrorMessage);
    }

    [Fact]
    public void ParserSeparatesLifecycleActionsFromPromptCreation()
    {
        var usage = CopilotLoopCommand.Parse("");
        var list = CopilotLoopCommand.Parse("list");
        var cancel = CopilotLoopCommand.Parse("cancel LOOP:1A2B3C4D");
        var tooLong = CopilotLoopCommand.Parse("8d check deployment");
        var missingPrompt = CopilotLoopCommand.Parse("30m");

        Assert.Equal(CopilotLoopCommandAction.Usage, usage.Action);
        Assert.Equal(CopilotLoopCommandAction.List, list.Action);
        Assert.Equal(CopilotLoopCommandAction.Cancel, cancel.Action);
        Assert.Equal("loop:1a2b3c4d", cancel.JobId);
        Assert.Equal(CopilotLoopCommandAction.Invalid, tooLong.Action);
        Assert.Contains("不能超过 7 天", tooLong.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(CopilotLoopCommandAction.Invalid, missingPrompt.Action);
    }

    [Fact]
    public void SchedulerClaimsImmediateRunsOnceAndAdvancesAfterAdmission()
    {
        var scheduler = new CopilotRecurringPromptScheduler();
        var now = new DateTimeOffset(2026, 7, 31, 10, 0, 0, TimeSpan.Zero);

        Assert.True(scheduler.TryCreate(
            "conversation-1",
            "Conversation",
            "profile-1",
            @"C:\workspace",
            "check deployment",
            TimeSpan.FromMinutes(30),
            now,
            out var created,
            out var error));
        Assert.NotNull(created);
        Assert.Empty(error);
        Assert.Equal(now, created.NextRunAtUtc);

        Assert.True(scheduler.TryClaimDue(now, out var dispatch));
        Assert.NotNull(dispatch);
        Assert.Equal(created.Id, dispatch.Job.Id);
        Assert.False(scheduler.TryClaimDue(now, out _));

        Assert.True(scheduler.CompleteDispatch(
            created.Id,
            scheduled: true,
            "已排入 Agent 宿主",
            now));
        var advanced = Assert.Single(scheduler.GetJobs(now));
        Assert.Equal(1, advanced.FireCount);
        Assert.Equal(now.AddMinutes(30), advanced.NextRunAtUtc);
        Assert.Equal("已排入 Agent 宿主", advanced.LastStatus);
        Assert.False(scheduler.TryClaimDue(now.AddMinutes(29), out _));
        Assert.True(scheduler.TryClaimDue(now.AddMinutes(30), out _));
    }

    [Fact]
    public void SchedulerRetriesRejectedAdmissionAndExpiresAfterSevenDays()
    {
        var scheduler = new CopilotRecurringPromptScheduler();
        var now = new DateTimeOffset(2026, 7, 31, 10, 0, 0, TimeSpan.Zero);
        Assert.True(scheduler.TryCreate(
            "conversation-1",
            "Conversation",
            "profile-1",
            string.Empty,
            "check deployment",
            TimeSpan.FromMinutes(1),
            now,
            out var created,
            out _));
        Assert.True(scheduler.TryClaimDue(now, out _));

        Assert.True(scheduler.CompleteDispatch(
            created!.Id,
            scheduled: false,
            "队列已满",
            now));
        var deferred = Assert.Single(scheduler.GetJobs(now));
        Assert.Equal(now.Add(CopilotRecurringPromptScheduler.DeferredRetryDelay), deferred.NextRunAtUtc);
        Assert.Equal(0, deferred.FireCount);
        Assert.Equal("队列已满", deferred.LastStatus);
        Assert.False(scheduler.TryClaimDue(now.AddSeconds(4), out _));
        Assert.True(scheduler.TryClaimDue(now.AddSeconds(5), out _));

        Assert.Empty(scheduler.GetJobs(now.AddDays(7)));
        Assert.False(scheduler.HasJobs);
    }

    [Fact]
    public void SchedulerCanCancelOneJobOrEveryJobForAConversation()
    {
        var scheduler = new CopilotRecurringPromptScheduler();
        var now = new DateTimeOffset(2026, 7, 31, 10, 0, 0, TimeSpan.Zero);
        Assert.True(Create(scheduler, "conversation-1", "first", now, out var first));
        Assert.True(Create(scheduler, "conversation-1", "second", now, out _));
        Assert.True(Create(scheduler, "conversation-2", "third", now, out _));

        Assert.True(scheduler.Cancel(first!.Id, out var cancelled));
        Assert.Equal("first", cancelled!.Prompt);
        Assert.Equal(1, scheduler.CancelConversation("conversation-1"));
        var remaining = Assert.Single(scheduler.GetJobs(now));
        Assert.Equal("conversation-2", remaining.ConversationId);
    }

    [Fact]
    public void DiagnosticsStateSessionBoundaryAndBoundPromptPreview()
    {
        var scheduler = new CopilotRecurringPromptScheduler();
        var now = new DateTimeOffset(2026, 7, 31, 10, 0, 0, TimeSpan.Zero);
        var prompt = new string('x', 200);
        Assert.True(Create(scheduler, "conversation-1", prompt, now, out var created));

        var report = CopilotRecurringPromptDiagnostics.Format(scheduler.GetJobs(now), now);

        Assert.Contains(created!.Id, report, StringComparison.Ordinal);
        Assert.Contains("仅当前应用会话有效", report, StringComparison.Ordinal);
        Assert.Contains("每次触发仍遵循现有工具审批策略", report, StringComparison.Ordinal);
        Assert.Contains("取消：/loop cancel", report, StringComparison.Ordinal);
        Assert.DoesNotContain(prompt, report, StringComparison.Ordinal);
    }

    private static bool Create(
        CopilotRecurringPromptScheduler scheduler,
        string conversationId,
        string prompt,
        DateTimeOffset now,
        out CopilotRecurringPromptJobSnapshot? job)
    {
        return scheduler.TryCreate(
            conversationId,
            "Conversation",
            "profile-1",
            @"C:\workspace",
            prompt,
            TimeSpan.FromMinutes(5),
            now,
            out job,
            out _);
    }
}
