using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationGoalTests
{
    [Fact]
    public void TurnOutcomeKeepsTimestampsValidWhenClockMovesBeforeCreation()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var goal = CopilotConversationGoal.Create("持续改进 Copilot", createdAt);

        var updated = goal.WithTurnOutcome(
            CopilotConversationGoalState.Active,
            new CopilotTokenUsage(10, 5, 15),
            elapsedSeconds: 15,
            evaluated: true,
            continued: true,
            "继续验证恢复路径",
            createdAt.AddMinutes(-5));

        Assert.Equal(createdAt, updated.UpdatedAtUtc);
        Assert.Equal(createdAt, updated.LastEvaluatedAtUtc);
        Assert.True(updated.IsStructurallyValid());
    }

    [Fact]
    public void LaterTurnDoesNotMoveGoalTimestampsBackwards()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var firstUpdate = createdAt.AddMinutes(10);
        var goal = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithTurnOutcome(
                CopilotConversationGoalState.Active,
                CopilotTokenUsage.Empty,
                elapsedSeconds: 120,
                evaluated: true,
                continued: true,
                "继续第一轮",
                firstUpdate);

        var updated = goal.WithTurnOutcome(
            CopilotConversationGoalState.Achieved,
            CopilotTokenUsage.Empty,
            elapsedSeconds: 30,
            evaluated: true,
            continued: false,
            "目标已达成",
            createdAt.AddMinutes(5));

        Assert.Equal(firstUpdate, updated.UpdatedAtUtc);
        Assert.Equal(firstUpdate, updated.LastEvaluatedAtUtc);
        Assert.True(updated.IsStructurallyValid());
    }

    [Fact]
    public void TurnOutcomeAccumulatesElapsedSecondsSafely()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var goal = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithTurnOutcome(
                CopilotConversationGoalState.Active,
                CopilotTokenUsage.Empty,
                elapsedSeconds: long.MaxValue,
                evaluated: false,
                continued: true,
                "继续",
                createdAt.AddSeconds(1));

        var saturated = goal.WithTurnOutcome(
            CopilotConversationGoalState.Active,
            CopilotTokenUsage.Empty,
            elapsedSeconds: 10,
            evaluated: false,
            continued: true,
            "继续",
            createdAt.AddSeconds(2));
        var ignoredNegative = saturated.WithTurnOutcome(
            CopilotConversationGoalState.Paused,
            CopilotTokenUsage.Empty,
            elapsedSeconds: -10,
            evaluated: false,
            continued: false,
            "暂停",
            createdAt.AddSeconds(3));

        Assert.Equal(long.MaxValue, saturated.TimeUsedSeconds);
        Assert.Equal(long.MaxValue, ignoredNegative.TimeUsedSeconds);
        Assert.True(ignoredNegative.IsStructurallyValid());
    }

    [Theory]
    [InlineData(0, "0 秒", "0s")]
    [InlineData(65, "1 分钟 5 秒", "1m 5s")]
    [InlineData(3_660, "1 小时 1 分钟", "1h 1m")]
    [InlineData(90_000, "1 天 1 小时", "1d 1h")]
    public void ElapsedUsageFormattingIsConcise(long seconds, string chinese, string english)
    {
        Assert.Equal(chinese, CopilotConversationGoalUsageText.FormatElapsed(seconds));
        Assert.Equal(english, CopilotConversationGoalUsageText.FormatElapsedEnglish(seconds));
    }

    [Fact]
    public void StateTransitionDoesNotMoveUpdatedTimestampBackwards()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var firstUpdate = createdAt.AddMinutes(10);
        var goal = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithState(CopilotConversationGoalState.Paused, firstUpdate, "用户暂停");

        var resumed = goal.WithState(
            CopilotConversationGoalState.Active,
            createdAt.AddMinutes(5),
            "用户恢复");

        Assert.Equal(firstUpdate, resumed.UpdatedAtUtc);
        Assert.True(resumed.IsStructurallyValid());
    }

    [Fact]
    public void ReenteringSameNonTerminalGoalPreservesUsageAndStartsWork()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var current = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithTurnOutcome(
                CopilotConversationGoalState.Paused,
                new CopilotTokenUsage(10, 5, 15),
                elapsedSeconds: 65,
                evaluated: true,
                continued: true,
                "等待下一轮",
                createdAt.AddMinutes(1));

        var result = CopilotConversationGoalCommand.Execute(
            current,
            "  持续改进 Copilot  ",
            createdAt.AddMinutes(2));

        Assert.True(result.Changed);
        Assert.True(result.StartsWork);
        Assert.NotNull(result.Goal);
        Assert.Equal(current.Id, result.Goal.Id);
        Assert.Equal(current.TurnCount, result.Goal.TurnCount);
        Assert.Equal(current.EvaluationCount, result.Goal.EvaluationCount);
        Assert.Equal(current.TokensUsed, result.Goal.TokensUsed);
        Assert.Equal(current.TimeUsedSeconds, result.Goal.TimeUsedSeconds);
        Assert.Equal(CopilotConversationGoalState.Active, result.Goal.State);
        Assert.Equal(0, result.Goal.ConsecutiveContinuationCount);
        Assert.Contains("保留", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReenteringAchievedGoalStartsFreshUsageAccounting()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var current = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithTurnOutcome(
                CopilotConversationGoalState.Achieved,
                new CopilotTokenUsage(10, 5, 15),
                elapsedSeconds: 42,
                evaluated: true,
                continued: false,
                "目标已达成",
                createdAt.AddMinutes(1));

        var result = CopilotConversationGoalCommand.Execute(
            current,
            current.Objective,
            createdAt.AddMinutes(2));

        Assert.True(result.Changed);
        Assert.True(result.StartsWork);
        Assert.NotNull(result.Goal);
        Assert.NotEqual(current.Id, result.Goal.Id);
        Assert.Equal(0, result.Goal.TurnCount);
        Assert.Equal(0, result.Goal.EvaluationCount);
        Assert.Equal(0, result.Goal.TokensUsed);
        Assert.Equal(0, result.Goal.TimeUsedSeconds);
        Assert.Equal(CopilotConversationGoalState.Active, result.Goal.State);
    }

    [Fact]
    public void BudgetCommandPreservesProgressAndSetsLimit()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var current = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithTurnOutcome(
                CopilotConversationGoalState.Paused,
                new CopilotTokenUsage(10, 5, 15),
                elapsedSeconds: 61,
                evaluated: true,
                continued: false,
                "等待预算",
                createdAt.AddMinutes(1));

        var result = CopilotConversationGoalCommand.Execute(
            current,
            "budget 40,000",
            createdAt.AddMinutes(2));

        Assert.True(result.Changed);
        Assert.False(result.StartsWork);
        Assert.NotNull(result.Goal);
        Assert.Equal(current.Id, result.Goal.Id);
        Assert.Equal(current.TurnCount, result.Goal.TurnCount);
        Assert.Equal(current.TokensUsed, result.Goal.TokensUsed);
        Assert.Equal(current.TimeUsedSeconds, result.Goal.TimeUsedSeconds);
        Assert.Equal(40_000, result.Goal.TokenBudget);
        Assert.Equal(CopilotConversationGoalState.Paused, result.Goal.State);
    }

    [Theory]
    [InlineData(CopilotConversationGoalState.Paused)]
    [InlineData(CopilotConversationGoalState.Blocked)]
    [InlineData(CopilotConversationGoalState.UsageLimited)]
    [InlineData(CopilotConversationGoalState.BudgetLimited)]
    public void GoalStateBudgetAndUsageSurviveSerialization(CopilotConversationGoalState state)
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var original = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithTokenBudget(40_000, createdAt)
            .WithTurnOutcome(
                state,
                new CopilotTokenUsage(10, 5, 15),
                elapsedSeconds: 125,
                evaluated: true,
                continued: false,
                "等待下一轮",
                createdAt.AddMinutes(1));

        var json = JsonConvert.SerializeObject(original);
        var restored = JsonConvert.DeserializeObject<CopilotConversationGoal>(json);

        Assert.NotNull(restored);
        Assert.True(restored.IsStructurallyValid());
        Assert.Equal(state, restored.State);
        Assert.Equal(original.TokenBudget, restored.TokenBudget);
        Assert.Equal(original.TokensUsed, restored.TokensUsed);
        Assert.Equal(original.TimeUsedSeconds, restored.TimeUsedSeconds);
    }

    [Fact]
    public void ExhaustedBudgetBlocksResumeWithoutDiscardingGoal()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var current = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithTokenBudget(10, createdAt)
            .WithTurnOutcome(
                CopilotConversationGoalState.Paused,
                new CopilotTokenUsage(7, 3, 10),
                elapsedSeconds: 10,
                evaluated: true,
                continued: false,
                "预算已用尽",
                createdAt.AddMinutes(1));

        var result = CopilotConversationGoalCommand.Execute(
            current,
            "resume",
            createdAt.AddMinutes(2));

        Assert.False(result.Changed);
        Assert.False(result.StartsWork);
        Assert.Same(current, result.Goal);
        Assert.Contains("提高预算", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SettingExhaustedBudgetLimitsActiveGoal()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var current = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithTurnOutcome(
                CopilotConversationGoalState.Active,
                new CopilotTokenUsage(60, 40, 100),
                elapsedSeconds: 10,
                evaluated: true,
                continued: true,
                "继续迭代",
                createdAt.AddMinutes(1));

        var result = CopilotConversationGoalCommand.Execute(
            current,
            "budget 80",
            createdAt.AddMinutes(2));

        Assert.True(result.Changed);
        Assert.False(result.StartsWork);
        Assert.NotNull(result.Goal);
        Assert.Equal(CopilotConversationGoalState.BudgetLimited, result.Goal.State);
        Assert.Equal(80, result.Goal.TokenBudget);
        Assert.True(result.Goal.IsTokenBudgetExhausted);
        Assert.Contains("预算受限", result.Goal.LastEvaluationReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinuationBecomesBudgetLimitedWhenTurnReachesGoalTokenBudget()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var goal = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithTokenBudget(100, createdAt);
        var evaluation = new CopilotGoalEvaluationResult(
            CopilotGoalEvaluationVerdict.Continue,
            "仍需验证",
            CopilotTokenUsage.Empty);

        var decision = CopilotGoalContinuationPolicy.Evaluate(
            goal,
            CopilotAgentMode.Auto,
            CopilotAgentStopReason.Completed,
            wasResponseInterrupted: false,
            new CopilotTokenUsage(60, 40, 100),
            elapsedSeconds: 90,
            evaluation,
            createdAt.AddMinutes(1));

        Assert.Equal(CopilotGoalTurnAction.Pause, decision.Action);
        Assert.Equal(CopilotConversationGoalState.BudgetLimited, decision.Goal.State);
        Assert.Equal(100, decision.Goal.TokensUsed);
        Assert.Equal(90, decision.Goal.TimeUsedSeconds);
        Assert.Contains("不再排入下一轮", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void BlockedAgentStopUsesBlockedGoalState()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var goal = CopilotConversationGoal.Create("持续改进 Copilot", createdAt);

        var decision = CopilotGoalContinuationPolicy.Evaluate(
            goal,
            CopilotAgentMode.Auto,
            CopilotAgentStopReason.Blocked,
            wasResponseInterrupted: false,
            CopilotTokenUsage.Empty,
            elapsedSeconds: 12,
            evaluation: null,
            createdAt.AddMinutes(1));

        Assert.Equal(CopilotGoalTurnAction.Pause, decision.Action);
        Assert.Equal(CopilotConversationGoalState.Blocked, decision.Goal.State);
        Assert.Equal(12, decision.Goal.TimeUsedSeconds);
        Assert.Contains("标记为受阻", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TokenBudgetCrossingTakesPriorityOverBlockedState()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var goal = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithTokenBudget(10, createdAt);

        var decision = CopilotGoalContinuationPolicy.Evaluate(
            goal,
            CopilotAgentMode.Auto,
            CopilotAgentStopReason.Blocked,
            wasResponseInterrupted: false,
            new CopilotTokenUsage(7, 3, 10),
            elapsedSeconds: 15,
            evaluation: null,
            createdAt.AddMinutes(1));

        Assert.Equal(CopilotGoalTurnAction.Pause, decision.Action);
        Assert.Equal(CopilotConversationGoalState.BudgetLimited, decision.Goal.State);
        Assert.Contains("预算受限", decision.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AchievedGoalCannotResumeInPlace()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var current = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithState(CopilotConversationGoalState.Achieved, createdAt.AddMinutes(1), "目标已达成");

        var result = CopilotConversationGoalCommand.Execute(
            current,
            "resume",
            createdAt.AddMinutes(2));

        Assert.False(result.Changed);
        Assert.False(result.StartsWork);
        Assert.Same(current, result.Goal);
        Assert.Contains("不能原地恢复", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BranchCopyPreservesBlockedReasonWithFreshAccounting()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var source = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithTokenBudget(40_000, createdAt)
            .WithTurnOutcome(
                CopilotConversationGoalState.Blocked,
                new CopilotTokenUsage(10, 5, 15),
                elapsedSeconds: 3_600,
                evaluated: false,
                continued: false,
                "等待外部依赖",
                createdAt.AddMinutes(1));

        var branch = source.CopyForBranch(createdAt.AddMinutes(2));

        Assert.NotEqual(source.Id, branch.Id);
        Assert.Equal(CopilotConversationGoalState.Blocked, branch.State);
        Assert.Equal(source.TokenBudget, branch.TokenBudget);
        Assert.Equal("等待外部依赖", branch.LastEvaluationReason);
        Assert.Equal(0, branch.TurnCount);
        Assert.Equal(0, branch.TokensUsed);
        Assert.Equal(0, branch.TimeUsedSeconds);
        Assert.True(branch.IsStructurallyValid());
    }

    [Fact]
    public void BranchDefersCopiedActiveGoalUntilExplicitAgentTurn()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var source = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        source.Goal = CopilotConversationGoal.Create("持续改进 Copilot", createdAt);
        source.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "开始目标"));
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "已经完成第一项工作");
        source.Messages.Add(assistant);

        var branch = CopilotConversationBranchService.CreateBranch(source, assistant);

        Assert.NotNull(branch.Goal);
        Assert.True(branch.Goal.IsActive);
        Assert.True(branch.IsGoalContinuationDeferred);
        Assert.Contains("目标待接管", branch.GoalDisplayText, StringComparison.Ordinal);
        Assert.Contains("下一条显式 Agent 任务", branch.GoalToolTip, StringComparison.Ordinal);
        Assert.False(branch.TryBeginGoalTurn(isAgentTurn: false, isAutomaticGoalContinuation: false));
        Assert.False(branch.TryBeginGoalTurn(isAgentTurn: true, isAutomaticGoalContinuation: true));
        Assert.True(branch.IsGoalContinuationDeferred);

        Assert.True(branch.TryBeginGoalTurn(isAgentTurn: true, isAutomaticGoalContinuation: false));

        Assert.False(branch.IsGoalContinuationDeferred);
        Assert.False(branch.TryBeginGoalTurn(isAgentTurn: true, isAutomaticGoalContinuation: false));
        Assert.StartsWith("持续目标", branch.GoalDisplayText, StringComparison.Ordinal);
    }

    [Fact]
    public void DeferredBranchGoalSurvivesSerializationAndProcessRestartRecovery()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var source = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        source.Goal = CopilotConversationGoal.Create("持续改进 Copilot", createdAt);
        source.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "开始目标"));
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "已经完成第一项工作");
        source.Messages.Add(assistant);
        var deferred = CopilotConversationBranchService.CreateBranch(source, assistant);
        var regular = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        regular.Goal = CopilotConversationGoal.Create("继续普通目标", createdAt);
        var state = new CopilotChatState();
        state.Conversations.Add(deferred);
        state.Conversations.Add(regular);

        var json = JsonConvert.SerializeObject(state);
        var restored = JsonConvert.DeserializeObject<CopilotChatState>(json);

        Assert.NotNull(restored);
        var restoredDeferred = restored.Conversations[0];
        var restoredRegular = restored.Conversations[1];
        Assert.True(restoredDeferred.IsGoalContinuationDeferred);
        Assert.True(CopilotConversationGoalRecovery.PauseActiveGoalsAfterProcessRestart(
            restored,
            createdAt.AddMinutes(1)));
        Assert.True(restoredDeferred.Goal?.IsActive);
        Assert.True(restoredDeferred.IsGoalContinuationDeferred);
        Assert.Equal(CopilotConversationGoalState.Paused, restoredRegular.Goal?.State);
    }

    [Fact]
    public void ValidationClearsDeferredMarkerOutsideActiveBranchGoal()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.IsGoalContinuationDeferred = true;

        Assert.True(conversation.EnsureValid());

        Assert.False(conversation.IsGoalContinuationDeferred);

        conversation.Goal = CopilotConversationGoal.Create(
            "普通会话目标",
            new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero));
        conversation.IsGoalContinuationDeferred = true;

        Assert.True(conversation.EnsureValid());
        Assert.False(conversation.IsGoalContinuationDeferred);
    }
}
