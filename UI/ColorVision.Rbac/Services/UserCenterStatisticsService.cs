using ColorVision.Database;
using System.Globalization;

namespace ColorVision.Rbac.Services
{
    public sealed record UserCenterFlowDay(DateTime Date, int ExecutionCount, int CompletedCount, double AverageDurationMs);

    public sealed record UserCenterStatisticsSnapshot(
        bool IsAvailable,
        string StatusMessage,
        long TotalExecutionCount,
        IReadOnlyList<UserCenterFlowDay> Days);

    public sealed record UserCenterStatisticsSummary(
        bool IsAvailable,
        string StatusMessage,
        long TotalExecutionCount,
        IReadOnlyList<UserCenterActivityDay> ActivityDays,
        int PeriodExecutionCount,
        int RecentExecutionCount,
        int RecentCompletedCount,
        double? RecentCompletionRatePercent,
        double? AverageDurationMs,
        UserCenterFlowDay? BusiestDay,
        int ActiveDayCount);

    /// <summary>
    /// 为用户中心提供只读流程执行统计。一条测量批次记录代表一次流程执行尝试。
    /// </summary>
    public static class UserCenterStatisticsService
    {
        // 与 Engine.Templates.Flow.FlowStatus.Completed 的持久化值保持一致，避免 UI 模块反向依赖 Engine。
        private const int CompletedResultCode = 6;

        private const string DailyQuerySql = """
            SELECT
                DATE(create_date) AS ActivityDate,
                COUNT(*) AS ExecutionCount,
                SUM(CASE WHEN result_code = @completedResultCode THEN 1 ELSE 0 END) AS CompletedCount,
                COALESCE(AVG(total_time), 0) AS AverageDurationMs
            FROM t_scgd_measure_batch
            WHERE create_date >= @startInclusive AND create_date < @endExclusive
            GROUP BY DATE(create_date)
            ORDER BY ActivityDate
            """;

        private const string TotalQuerySql = """
            SELECT COUNT(*) AS TotalExecutionCount
            FROM t_scgd_measure_batch
            """;

        public static async Task<UserCenterStatisticsSnapshot> QueryAsync(
            DateTime startInclusive,
            DateTime endExclusive,
            CancellationToken cancellationToken = default)
        {
            if (!MySqlControl.GetInstance().IsConnect)
                return Unavailable("业务数据库未连接，流程统计暂不可用");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var db = MySqlControl.CreateDbClient();
                var rows = await db.Ado.SqlQueryAsync<FlowDayRow>(DailyQuerySql, new
                {
                    startInclusive,
                    endExclusive,
                    completedResultCode = CompletedResultCode,
                });
                cancellationToken.ThrowIfCancellationRequested();
                var totals = await db.Ado.SqlQueryAsync<FlowTotalRow>(TotalQuerySql);
                cancellationToken.ThrowIfCancellationRequested();

                var days = rows
                    .Where(row => row.ActivityDate.HasValue)
                    .Select(row => new UserCenterFlowDay(
                        row.ActivityDate!.Value.Date,
                        Math.Max(0, row.ExecutionCount),
                        Math.Max(0, row.CompletedCount),
                        Math.Max(0, row.AverageDurationMs)))
                    .ToArray();
                var total = Math.Max(0, totals.FirstOrDefault()?.TotalExecutionCount ?? 0);
                return new UserCenterStatisticsSnapshot(true, "统计已更新", total, days);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return Unavailable("流程统计读取失败，请稍后重试");
            }
        }

        private static UserCenterStatisticsSnapshot Unavailable(string message) =>
            new(false, message, 0, Array.Empty<UserCenterFlowDay>());

        private sealed class FlowDayRow
        {
            public DateTime? ActivityDate { get; set; }
            public int ExecutionCount { get; set; }
            public int CompletedCount { get; set; }
            public double AverageDurationMs { get; set; }
        }

        private sealed class FlowTotalRow
        {
            public long TotalExecutionCount { get; set; }
        }
    }

    public sealed class UserCenterActivityDay
    {
        public required DateTime Date { get; init; }
        public required int ExecutionCount { get; init; }
        public required double Intensity { get; init; }
        public string ToolTip => string.Format(
            CultureInfo.CurrentCulture,
            "{0:yyyy-MM-dd} · {1:N0} 次流程",
            Date,
            ExecutionCount);
    }

    public static class UserCenterStatisticsPresenter
    {
        public static UserCenterStatisticsSummary Build(
            UserCenterStatisticsSnapshot snapshot,
            DateTime startDate,
            DateTime today,
            int activityDayCount)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (activityDayCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(activityDayCount));

            startDate = startDate.Date;
            today = today.Date;
            if (startDate > today)
                throw new ArgumentException("The activity start date must not be after today.", nameof(startDate));
            if ((today - startDate).Days + 1 != activityDayCount)
                throw new ArgumentException("The activity day count must match the inclusive date range.", nameof(activityDayCount));

            var periodDays = snapshot.Days
                .Where(day => day.Date.Date >= startDate && day.Date.Date <= today)
                .GroupBy(day => day.Date.Date)
                .Select(group =>
                {
                    var executionCount = group.Sum(day => Math.Max(0, day.ExecutionCount));
                    var completedCount = Math.Min(
                        executionCount,
                        group.Sum(day => Math.Max(0, day.CompletedCount)));
                    return new UserCenterFlowDay(
                        group.Key,
                        executionCount,
                        completedCount,
                        WeightedAverage(group));
                })
                .OrderBy(day => day.Date)
                .ToArray();
            var rowsByDate = periodDays.ToDictionary(day => day.Date.Date);
            var maxCount = periodDays.Length == 0 ? 0 : periodDays.Max(day => day.ExecutionCount);
            var activityDays = new List<UserCenterActivityDay>(activityDayCount);
            for (var date = startDate; date <= today; date = date.AddDays(1))
            {
                var count = rowsByDate.TryGetValue(date, out var day) ? day.ExecutionCount : 0;
                activityDays.Add(new UserCenterActivityDay
                {
                    Date = date,
                    ExecutionCount = count,
                    Intensity = GetActivityIntensity(count, maxCount),
                });
            }

            var recentStart = today.AddDays(-6);
            var recentDays = periodDays.Where(day => day.Date >= recentStart).ToArray();
            var recentExecutionCount = recentDays.Sum(day => day.ExecutionCount);
            var recentCompletedCount = recentDays.Sum(day => day.CompletedCount);
            var periodExecutionCount = periodDays.Sum(day => day.ExecutionCount);
            double? averageDurationMs = periodExecutionCount == 0
                ? null
                : periodDays.Sum(day => day.ExecutionCount * day.AverageDurationMs) / periodExecutionCount;
            double? recentCompletionRatePercent = recentExecutionCount == 0
                ? null
                : recentCompletedCount * 100d / recentExecutionCount;
            var busiestDay = periodDays
                .Where(day => day.ExecutionCount > 0)
                .OrderByDescending(day => day.ExecutionCount)
                .ThenByDescending(day => day.Date)
                .FirstOrDefault();

            return new UserCenterStatisticsSummary(
                snapshot.IsAvailable,
                snapshot.StatusMessage,
                Math.Max(0, snapshot.TotalExecutionCount),
                activityDays,
                periodExecutionCount,
                recentExecutionCount,
                recentCompletedCount,
                recentCompletionRatePercent,
                averageDurationMs,
                busiestDay,
                periodDays.Count(day => day.ExecutionCount > 0));
        }

        public static double GetActivityIntensity(int count, int maxCount)
        {
            if (count <= 0 || maxCount <= 0)
                return 0.12;
            var ratio = count / (double)maxCount;
            return ratio switch
            {
                <= 0.25 => 0.35,
                <= 0.5 => 0.55,
                <= 0.75 => 0.75,
                _ => 1,
            };
        }

        private static double WeightedAverage(IEnumerable<UserCenterFlowDay> days)
        {
            var rows = days.ToArray();
            var total = rows.Sum(day => Math.Max(0, day.ExecutionCount));
            return total == 0
                ? 0
                : rows.Sum(day => Math.Max(0, day.ExecutionCount) * Math.Max(0, day.AverageDurationMs)) / total;
        }
    }
}
