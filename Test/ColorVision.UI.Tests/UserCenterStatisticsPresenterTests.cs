using ColorVision.Rbac.Services;

namespace ColorVision.UI.Tests;

public sealed class UserCenterStatisticsPresenterTests
{
    [Fact]
    public void BuildCreatesCompleteHeatmapAndAggregatesFlowMetrics()
    {
        var today = new DateTime(2026, 7, 20);
        var startDate = today.AddDays(-13);
        var snapshot = new UserCenterStatisticsSnapshot(
            true,
            "ready",
            125,
            new[]
            {
                new UserCenterFlowDay(today.AddDays(-8), 3, 2, 100),
                new UserCenterFlowDay(today.AddDays(-2), 2, 1, 400),
                new UserCenterFlowDay(today, 4, 4, 250),
            });

        var summary = UserCenterStatisticsPresenter.Build(snapshot, startDate, today, 14);

        Assert.True(summary.IsAvailable);
        Assert.Equal(125, summary.TotalExecutionCount);
        Assert.Equal(14, summary.ActivityDays.Count);
        Assert.Equal(startDate, summary.ActivityDays[0].Date);
        Assert.Equal(today, summary.ActivityDays[^1].Date);
        Assert.Equal(9, summary.PeriodExecutionCount);
        Assert.Equal(6, summary.RecentExecutionCount);
        Assert.Equal(5, summary.RecentCompletedCount);
        Assert.Equal(83.333, summary.RecentCompletionRatePercent!.Value, 3);
        Assert.Equal(233.333, summary.AverageDurationMs!.Value, 3);
        Assert.Equal(today, summary.BusiestDay!.Date);
        Assert.Equal(3, summary.ActiveDayCount);
        Assert.Equal(0.12, summary.ActivityDays.First(day => day.ExecutionCount == 0).Intensity);
        Assert.Equal(1, summary.ActivityDays.Single(day => day.Date == today).Intensity);
    }

    [Fact]
    public void BuildMergesDuplicateDatesAndClampsInvalidCounts()
    {
        var today = new DateTime(2026, 7, 20);
        var snapshot = new UserCenterStatisticsSnapshot(
            true,
            "ready",
            -5,
            new[]
            {
                new UserCenterFlowDay(today, 2, 1, 100),
                new UserCenterFlowDay(today.AddHours(6), 3, 20, 400),
                new UserCenterFlowDay(today.AddDays(-1), -9, -4, -100),
            });

        var summary = UserCenterStatisticsPresenter.Build(snapshot, today.AddDays(-1), today, 2);

        Assert.Equal(0, summary.TotalExecutionCount);
        Assert.Equal(5, summary.PeriodExecutionCount);
        Assert.Equal(5, summary.RecentCompletedCount);
        Assert.Equal(100, summary.RecentCompletionRatePercent);
        Assert.Equal(280, summary.AverageDurationMs);
        Assert.Equal(1, summary.ActiveDayCount);
        Assert.Equal(5, summary.ActivityDays.Single(day => day.Date == today).ExecutionCount);
    }

    [Fact]
    public void BuildKeepsUnavailableSnapshotPresentable()
    {
        var today = new DateTime(2026, 7, 20);
        var snapshot = new UserCenterStatisticsSnapshot(false, "offline", 0, Array.Empty<UserCenterFlowDay>());

        var summary = UserCenterStatisticsPresenter.Build(snapshot, today.AddDays(-6), today, 7);

        Assert.False(summary.IsAvailable);
        Assert.Equal("offline", summary.StatusMessage);
        Assert.Equal(7, summary.ActivityDays.Count);
        Assert.All(summary.ActivityDays, day => Assert.Equal(0.12, day.Intensity));
        Assert.Null(summary.RecentCompletionRatePercent);
        Assert.Null(summary.AverageDurationMs);
        Assert.Null(summary.BusiestDay);
    }

    [Fact]
    public void BuildRejectsInvalidRanges()
    {
        var today = new DateTime(2026, 7, 20);
        var snapshot = new UserCenterStatisticsSnapshot(true, "ready", 0, Array.Empty<UserCenterFlowDay>());

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UserCenterStatisticsPresenter.Build(snapshot, today, today, 0));
        Assert.Throws<ArgumentException>(() =>
            UserCenterStatisticsPresenter.Build(snapshot, today.AddDays(1), today, 1));
        Assert.Throws<ArgumentException>(() =>
            UserCenterStatisticsPresenter.Build(snapshot, today.AddDays(-1), today, 1));
    }
}
