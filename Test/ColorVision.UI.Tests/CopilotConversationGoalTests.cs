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
                evaluated: true,
                continued: true,
                "继续第一轮",
                firstUpdate);

        var updated = goal.WithTurnOutcome(
            CopilotConversationGoalState.Achieved,
            CopilotTokenUsage.Empty,
            evaluated: true,
            continued: false,
            "目标已达成",
            createdAt.AddMinutes(5));

        Assert.Equal(firstUpdate, updated.UpdatedAtUtc);
        Assert.Equal(firstUpdate, updated.LastEvaluatedAtUtc);
        Assert.True(updated.IsStructurallyValid());
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
        Assert.Equal(40_000, result.Goal.TokenBudget);
        Assert.Equal(CopilotConversationGoalState.Paused, result.Goal.State);
    }

    [Fact]
    public void TokenBudgetAndUsageSurviveSerialization()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var original = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithTokenBudget(40_000, createdAt)
            .WithTurnOutcome(
                CopilotConversationGoalState.Paused,
                new CopilotTokenUsage(10, 5, 15),
                evaluated: true,
                continued: false,
                "等待下一轮",
                createdAt.AddMinutes(1));

        var json = JsonConvert.SerializeObject(original);
        var restored = JsonConvert.DeserializeObject<CopilotConversationGoal>(json);

        Assert.NotNull(restored);
        Assert.True(restored.IsStructurallyValid());
        Assert.Equal(original.TokenBudget, restored.TokenBudget);
        Assert.Equal(original.TokensUsed, restored.TokensUsed);
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
    public void SettingExhaustedBudgetPausesActiveGoal()
    {
        var createdAt = new DateTimeOffset(2026, 8, 8, 8, 0, 0, TimeSpan.Zero);
        var current = CopilotConversationGoal.Create("持续改进 Copilot", createdAt)
            .WithTurnOutcome(
                CopilotConversationGoalState.Active,
                new CopilotTokenUsage(60, 40, 100),
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
        Assert.Equal(CopilotConversationGoalState.Paused, result.Goal.State);
        Assert.Equal(80, result.Goal.TokenBudget);
        Assert.True(result.Goal.IsTokenBudgetExhausted);
        Assert.Contains("自动暂停", result.Goal.LastEvaluationReason, StringComparison.Ordinal);
    }

    [Fact]
    public void ContinuationPausesWhenTurnReachesGoalTokenBudget()
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
            evaluation,
            createdAt.AddMinutes(1));

        Assert.Equal(CopilotGoalTurnAction.Pause, decision.Action);
        Assert.Equal(CopilotConversationGoalState.Paused, decision.Goal.State);
        Assert.Equal(100, decision.Goal.TokensUsed);
        Assert.Contains("不再排入下一轮", decision.Reason, StringComparison.Ordinal);
    }
}
