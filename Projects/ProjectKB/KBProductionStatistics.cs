using ColorVision.Database;
using ColorVision.Engine.FlowProcessing;
using SqlSugar;

namespace ProjectKB
{
    [SugarTable("KBProductionSession")]
    public sealed class KBProductionSession : EntityBase
    {
        public DateTime StartTime { get; set; } = DateTime.Now;

        [SugarColumn(IsNullable = true)]
        public DateTime? EndTime { get; set; }

        public string Model { get; set; } = string.Empty;
        public string Stage { get; set; } = string.Empty;
        public string LineNo { get; set; } = string.Empty;
        public string WorkerNo { get; set; } = string.Empty;
        public string OpNo { get; set; } = string.Empty;
        public string MachineNo { get; set; } = string.Empty;
        public int TargetProduction { get; set; }
        public DateTime UpdatedTime { get; set; } = DateTime.Now;
    }

    public sealed class KBProductionStatistics
    {
        public int TotalRuns { get; init; }
        public int ProductionCount { get; init; }
        public int GoodCount { get; init; }
        public int DefectiveCount { get; init; }
        public int ExecutionFailureCount { get; init; }
        public int TargetProduction { get; init; }
        public int CurrentHourProduction { get; init; }
        public int TodayProduction { get; init; }
        public double GoodRate { get; init; }
        public double AverageCtMilliseconds { get; init; }
        public long MinimumCtMilliseconds { get; init; }
        public long MaximumCtMilliseconds { get; init; }
        public IReadOnlyList<KBHourlyProductionRow> HourlyRows { get; init; } = [];
        public IReadOnlyList<KBDailyProductionRow> DailyRows { get; init; } = [];
        public IReadOnlyList<KBProductionSessionRow> SessionRows { get; init; } = [];

        public string GoodRateText => $"{GoodRate:P2}";
        public string AverageCtText => KBProductionStatisticsCalculator.FormatMilliseconds(AverageCtMilliseconds);
        public string MinimumCtText => KBProductionStatisticsCalculator.FormatMilliseconds(MinimumCtMilliseconds);
        public string MaximumCtText => KBProductionStatisticsCalculator.FormatMilliseconds(MaximumCtMilliseconds);
    }

    public sealed class KBHourlyProductionRow
    {
        public DateTime Hour { get; init; }
        public int ProductionCount { get; init; }
        public int GoodCount { get; init; }
        public int DefectiveCount { get; init; }
        public int ExecutionFailureCount { get; init; }
        public double GoodRate { get; init; }
        public double AverageCtMilliseconds { get; init; }

        public string HourText => Hour.ToString("yyyy/MM/dd HH:00");
        public string GoodRateText => $"{GoodRate:P2}";
        public string AverageCtText => KBProductionStatisticsCalculator.FormatMilliseconds(AverageCtMilliseconds);
    }

    public sealed class KBDailyProductionRow
    {
        public DateTime Date { get; init; }
        public int TargetProduction { get; init; }
        public int ProductionCount { get; init; }
        public int GoodCount { get; init; }
        public int DefectiveCount { get; init; }
        public int ExecutionFailureCount { get; init; }
        public double GoodRate { get; init; }
        public double AchievementRate { get; init; }
        public double AverageCtMilliseconds { get; init; }

        public string DateText => Date.ToString("yyyy/MM/dd");
        public string GoodRateText => $"{GoodRate:P2}";
        public string AchievementRateText => TargetProduction > 0 ? $"{AchievementRate:P2}" : "-";
        public string AverageCtText => KBProductionStatisticsCalculator.FormatMilliseconds(AverageCtMilliseconds);
    }

    public sealed class KBProductionSessionRow
    {
        public int SessionId { get; init; }
        public DateTime StartTime { get; init; }
        public DateTime? EndTime { get; init; }
        public string Model { get; init; } = string.Empty;
        public string Stage { get; init; } = string.Empty;
        public string LineNo { get; init; } = string.Empty;
        public string WorkerNo { get; init; } = string.Empty;
        public string OpNo { get; init; } = string.Empty;
        public string MachineNo { get; init; } = string.Empty;
        public int TargetProduction { get; init; }
        public int ProductionCount { get; init; }
        public int GoodCount { get; init; }
        public int DefectiveCount { get; init; }
        public int ExecutionFailureCount { get; init; }
        public double GoodRate { get; init; }
        public double AverageCtMilliseconds { get; init; }

        public string SessionText => SessionId > 0 ? $"#{SessionId}" : "未关联";
        public string GoodRateText => $"{GoodRate:P2}";
        public string AverageCtText => KBProductionStatisticsCalculator.FormatMilliseconds(AverageCtMilliseconds);
    }

    public static class KBProductionStatisticsCalculator
    {
        public static KBProductionStatistics Calculate(
            IEnumerable<KBItemMaster> sourceResults,
            IEnumerable<KBProductionSession> sourceSessions,
            DateTime from,
            DateTime toExclusive,
            DateTime now)
        {
            ArgumentNullException.ThrowIfNull(sourceResults);
            ArgumentNullException.ThrowIfNull(sourceSessions);
            if (toExclusive <= from)
                throw new ArgumentOutOfRangeException(nameof(toExclusive), "结束时间必须晚于开始时间。");

            List<KBItemMaster> results = sourceResults
                .Where(item => item.CreateTime >= from && item.CreateTime < toExclusive)
                .OrderBy(item => item.CreateTime)
                .ThenBy(item => item.Id)
                .ToList();
            List<KBProductionSession> sessions = sourceSessions
                .Where(item => item.StartTime >= from && item.StartTime < toExclusive)
                .OrderBy(item => item.StartTime)
                .ThenBy(item => item.Id)
                .ToList();

            ProductionMetrics overall = CalculateMetrics(results);
            List<KBHourlyProductionRow> hourlyRows = results
                .GroupBy(item => new DateTime(item.CreateTime.Year, item.CreateTime.Month, item.CreateTime.Day, item.CreateTime.Hour, 0, 0))
                .OrderByDescending(group => group.Key)
                .Select(group =>
                {
                    ProductionMetrics metrics = CalculateMetrics(group);
                    return new KBHourlyProductionRow
                    {
                        Hour = group.Key,
                        ProductionCount = metrics.ProductionCount,
                        GoodCount = metrics.GoodCount,
                        DefectiveCount = metrics.DefectiveCount,
                        ExecutionFailureCount = metrics.ExecutionFailureCount,
                        GoodRate = metrics.GoodRate,
                        AverageCtMilliseconds = metrics.AverageCtMilliseconds
                    };
                })
                .ToList();

            Dictionary<DateTime, int> dailyTargets = sessions
                .GroupBy(item => item.StartTime.Date)
                .ToDictionary(group => group.Key, group => group.Sum(item => Math.Max(0, item.TargetProduction)));
            IEnumerable<DateTime> dailyKeys = results.Select(item => item.CreateTime.Date)
                .Concat(dailyTargets.Keys)
                .Distinct()
                .OrderByDescending(date => date);
            List<KBDailyProductionRow> dailyRows = dailyKeys
                .Select(date =>
                {
                    ProductionMetrics metrics = CalculateMetrics(results.Where(item => item.CreateTime.Date == date));
                    int target = dailyTargets.GetValueOrDefault(date);
                    return new KBDailyProductionRow
                    {
                        Date = date,
                        TargetProduction = target,
                        ProductionCount = metrics.ProductionCount,
                        GoodCount = metrics.GoodCount,
                        DefectiveCount = metrics.DefectiveCount,
                        ExecutionFailureCount = metrics.ExecutionFailureCount,
                        GoodRate = metrics.GoodRate,
                        AchievementRate = target > 0 ? metrics.ProductionCount / (double)target : 0,
                        AverageCtMilliseconds = metrics.AverageCtMilliseconds
                    };
                })
                .ToList();

            var sessionRows = new List<KBProductionSessionRow>();
            foreach (KBProductionSession session in sessions.OrderByDescending(item => item.StartTime))
            {
                List<KBItemMaster> sessionResults = results
                    .Where(item => item.ProductionSessionId == session.Id)
                    .ToList();
                ProductionMetrics metrics = CalculateMetrics(sessionResults);
                sessionRows.Add(CreateSessionRow(session, metrics));
            }

            List<KBItemMaster> legacyResults = results
                .Where(item => !item.ProductionSessionId.HasValue || item.ProductionSessionId <= 0)
                .ToList();
            if (legacyResults.Count > 0)
            {
                ProductionMetrics metrics = CalculateMetrics(legacyResults);
                sessionRows.Add(new KBProductionSessionRow
                {
                    SessionId = 0,
                    StartTime = legacyResults.Min(item => item.CreateTime),
                    EndTime = legacyResults.Max(item => item.CreateTime),
                    Stage = "历史数据",
                    ProductionCount = metrics.ProductionCount,
                    GoodCount = metrics.GoodCount,
                    DefectiveCount = metrics.DefectiveCount,
                    ExecutionFailureCount = metrics.ExecutionFailureCount,
                    GoodRate = metrics.GoodRate,
                    AverageCtMilliseconds = metrics.AverageCtMilliseconds
                });
            }

            DateTime currentHour = new(now.Year, now.Month, now.Day, now.Hour, 0, 0);
            int targetProduction = sessions.Sum(item => Math.Max(0, item.TargetProduction));
            return new KBProductionStatistics
            {
                TotalRuns = results.Count,
                ProductionCount = overall.ProductionCount,
                GoodCount = overall.GoodCount,
                DefectiveCount = overall.DefectiveCount,
                ExecutionFailureCount = overall.ExecutionFailureCount,
                TargetProduction = targetProduction,
                CurrentHourProduction = hourlyRows.FirstOrDefault(item => item.Hour == currentHour)?.ProductionCount ?? 0,
                TodayProduction = dailyRows.FirstOrDefault(item => item.Date == now.Date)?.ProductionCount ?? 0,
                GoodRate = overall.GoodRate,
                AverageCtMilliseconds = overall.AverageCtMilliseconds,
                MinimumCtMilliseconds = overall.MinimumCtMilliseconds,
                MaximumCtMilliseconds = overall.MaximumCtMilliseconds,
                HourlyRows = hourlyRows,
                DailyRows = dailyRows,
                SessionRows = sessionRows
                    .OrderByDescending(item => item.StartTime)
                    .ThenByDescending(item => item.SessionId)
                    .ToList()
            };
        }

        public static string FormatMilliseconds(double milliseconds)
        {
            return milliseconds > 0 ? $"{milliseconds / 1000d:F3} s" : "-";
        }

        private static KBProductionSessionRow CreateSessionRow(KBProductionSession session, ProductionMetrics metrics)
        {
            return new KBProductionSessionRow
            {
                SessionId = session.Id,
                StartTime = session.StartTime,
                EndTime = session.EndTime,
                Model = session.Model,
                Stage = session.Stage,
                LineNo = session.LineNo,
                WorkerNo = session.WorkerNo,
                OpNo = session.OpNo,
                MachineNo = session.MachineNo,
                TargetProduction = session.TargetProduction,
                ProductionCount = metrics.ProductionCount,
                GoodCount = metrics.GoodCount,
                DefectiveCount = metrics.DefectiveCount,
                ExecutionFailureCount = metrics.ExecutionFailureCount,
                GoodRate = metrics.GoodRate,
                AverageCtMilliseconds = metrics.AverageCtMilliseconds
            };
        }

        private static ProductionMetrics CalculateMetrics(IEnumerable<KBItemMaster> source)
        {
            List<KBItemMaster> results = source.ToList();
            List<KBItemMaster> completed = results.Where(item => item.FlowStatus == FlowStatus.Completed).ToList();
            List<long> cycleTimes = completed.Where(item => item.RunTime > 0).Select(item => item.RunTime).ToList();
            int goodCount = completed.Count(item => item.Result);
            int defectiveCount = completed.Count - goodCount;
            return new ProductionMetrics(
                completed.Count,
                goodCount,
                defectiveCount,
                results.Count(IsExecutionFailure),
                completed.Count > 0 ? goodCount / (double)completed.Count : 0,
                cycleTimes.Count > 0 ? cycleTimes.Average() : 0,
                cycleTimes.Count > 0 ? cycleTimes.Min() : 0,
                cycleTimes.Count > 0 ? cycleTimes.Max() : 0);
        }

        private static bool IsExecutionFailure(KBItemMaster item)
        {
            return item.FlowStatus is FlowStatus.Failed or FlowStatus.OverTime or FlowStatus.Canceled;
        }

        private sealed record ProductionMetrics(
            int ProductionCount,
            int GoodCount,
            int DefectiveCount,
            int ExecutionFailureCount,
            double GoodRate,
            double AverageCtMilliseconds,
            long MinimumCtMilliseconds,
            long MaximumCtMilliseconds);
    }

    public sealed class KBProductionDataStore
    {
        private static readonly Lazy<KBProductionDataStore> LazyInstance = new(() => new KBProductionDataStore());
        private static readonly object SessionGate = new();
        private readonly string? _databasePath;
        private readonly object _schemaGate = new();
        private bool _schemaInitialized;

        public static KBProductionDataStore Instance => LazyInstance.Value;

        public KBProductionDataStore(string? databasePath = null)
        {
            _databasePath = databasePath;
        }

        private string DatabasePath => _databasePath ?? ViewResultManager.SqliteDbPath;

        public void InitializeSchema()
        {
            if (_schemaInitialized)
                return;

            lock (_schemaGate)
            {
                if (_schemaInitialized)
                    return;

                using SqlSugarClient db = CreateClient();
                db.CodeFirst.InitTables<KBItemMaster, KBProductionSession>();
                _schemaInitialized = true;
            }
        }

        public int EnsureCurrentSession(Summary summary, string model, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(summary);
            ArgumentException.ThrowIfNullOrWhiteSpace(model);
            InitializeSchema();

            lock (SessionGate)
            {
                using SqlSugarClient db = CreateClient();
                db.Ado.BeginTran();
                try
                {
                    List<KBProductionSession> activeSessions = db.Queryable<KBProductionSession>()
                        .Where(item => item.EndTime == null)
                        .OrderBy(item => item.Id, OrderByType.Desc)
                        .ToList();
                    KBProductionSession? matchingSession = activeSessions.FirstOrDefault(item =>
                        item.StartTime.Date == now.Date && Matches(item, summary, model));

                    foreach (KBProductionSession activeSession in activeSessions.Where(item => item.Id != matchingSession?.Id))
                    {
                        activeSession.EndTime = now;
                        activeSession.UpdatedTime = now;
                        db.Updateable(activeSession).ExecuteCommand();
                    }

                    if (matchingSession != null)
                    {
                        db.Ado.CommitTran();
                        return matchingSession.Id;
                    }

                    var session = new KBProductionSession
                    {
                        StartTime = now,
                        Model = Normalize(model),
                        Stage = Normalize(summary.Stage),
                        LineNo = Normalize(summary.LineNO),
                        WorkerNo = Normalize(summary.WorkerNO),
                        OpNo = Normalize(summary.Opno),
                        MachineNo = Normalize(summary.MachineNO),
                        TargetProduction = Math.Max(0, summary.TargetProduction),
                        UpdatedTime = now
                    };
                    session.Id = db.Insertable(session).ExecuteReturnIdentity();
                    db.Ado.CommitTran();
                    return session.Id;
                }
                catch
                {
                    db.Ado.RollbackTran();
                    throw;
                }
            }
        }

        public IReadOnlyList<string> QueryModels()
        {
            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            return db.Queryable<KBItemMaster>()
                .Where(item => item.Model != string.Empty)
                .Select(item => item.Model)
                .ToList()
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public KBProductionStatistics QueryStatistics(DateTime from, DateTime toExclusive, string? model, DateTime now)
        {
            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            ISugarQueryable<KBItemMaster> resultQuery = db.Queryable<KBItemMaster>()
                .Where(item => item.CreateTime >= from && item.CreateTime < toExclusive);
            if (!string.IsNullOrWhiteSpace(model))
                resultQuery = resultQuery.Where(item => item.Model == model);

            List<KBItemMaster> results = resultQuery.ToList();
            List<KBProductionSession> sessions = db.Queryable<KBProductionSession>()
                .Where(item => item.StartTime >= from && item.StartTime < toExclusive)
                .ToList()
                .Where(item => string.IsNullOrWhiteSpace(model)
                    || string.Equals(item.Model, model, StringComparison.OrdinalIgnoreCase))
                .ToList();
            return KBProductionStatisticsCalculator.Calculate(results, sessions, from, toExclusive, now);
        }

        private SqlSugarClient CreateClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={DatabasePath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            });
        }

        private static bool Matches(KBProductionSession session, Summary summary, string model)
        {
            return string.Equals(session.Model, Normalize(model), StringComparison.OrdinalIgnoreCase)
                && string.Equals(session.Stage, Normalize(summary.Stage), StringComparison.Ordinal)
                && string.Equals(session.LineNo, Normalize(summary.LineNO), StringComparison.Ordinal)
                && string.Equals(session.WorkerNo, Normalize(summary.WorkerNO), StringComparison.Ordinal)
                && string.Equals(session.OpNo, Normalize(summary.Opno), StringComparison.Ordinal)
                && string.Equals(session.MachineNo, Normalize(summary.MachineNO), StringComparison.Ordinal)
                && session.TargetProduction == Math.Max(0, summary.TargetProduction);
        }

        private static string Normalize(string? value) => value?.Trim() ?? string.Empty;
    }
}
