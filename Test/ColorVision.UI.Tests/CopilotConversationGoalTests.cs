using ColorVision.Copilot;

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
}
