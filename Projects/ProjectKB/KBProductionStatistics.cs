using ColorVision.Database;
using ColorVision.Engine.FlowProcessing;
using SqlSugar;

namespace ProjectKB
{
    public enum KBProductionPeriodMode
    {
        Day,
        Week,
        Month,
        All
    }

    public readonly record struct KBProductionPeriodRange(DateTime From, DateTime ToExclusive)
    {
        public string ToDisplayText(KBProductionPeriodMode mode)
        {
            return mode switch
            {
                KBProductionPeriodMode.All => "全部记录",
                KBProductionPeriodMode.Week => $"{From:yyyy/MM/dd} - {ToExclusive.AddDays(-1):MM/dd}",
                KBProductionPeriodMode.Month => From.ToString("yyyy/MM"),
                _ => From.ToString("yyyy/MM/dd"),
            };
        }
    }

    public static class KBProductionPeriod
    {
        public static KBProductionPeriodRange GetRange(KBProductionPeriodMode mode, DateTime anchor)
        {
            DateTime day = anchor.Date;
            return mode switch
            {
                KBProductionPeriodMode.All => new KBProductionPeriodRange(DateTime.MinValue, DateTime.MaxValue),
                KBProductionPeriodMode.Week => CreateWeekRange(day),
                KBProductionPeriodMode.Month => new KBProductionPeriodRange(
                    new DateTime(day.Year, day.Month, 1),
                    new DateTime(day.Year, day.Month, 1).AddMonths(1)),
                _ => new KBProductionPeriodRange(day, day.AddDays(1)),
            };
        }

        public static DateTime ShiftAnchor(KBProductionPeriodMode mode, DateTime anchor, int offset)
        {
            return mode switch
            {
                KBProductionPeriodMode.All => anchor.Date,
                KBProductionPeriodMode.Week => anchor.Date.AddDays(checked(offset * 7)),
                KBProductionPeriodMode.Month => anchor.Date.AddMonths(offset),
                _ => anchor.Date.AddDays(offset),
            };
        }

        private static KBProductionPeriodRange CreateWeekRange(DateTime day)
        {
            int daysAfterMonday = ((int)day.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            DateTime monday = day.AddDays(-daysAfterMonday);
            return new KBProductionPeriodRange(monday, monday.AddDays(7));
        }
    }

    public sealed class KBProductionQuery
    {
        public DateTime From { get; init; } = DateTime.Today;
        public DateTime ToExclusive { get; init; } = DateTime.Today.AddDays(1);
        public KBProductionPeriodMode PeriodMode { get; init; } = KBProductionPeriodMode.Day;
        public string? Model { get; init; }
        public string? SN { get; init; }
        public bool? Result { get; init; }
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 1000;
    }

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
        public IReadOnlyList<KBProductionTrendPoint> TrendRows { get; init; } = [];

        public string GoodRateText => $"{GoodRate:P2}";
        public string AverageCtText => KBProductionStatisticsCalculator.FormatMilliseconds(AverageCtMilliseconds);
        public string MinimumCtText => KBProductionStatisticsCalculator.FormatMilliseconds(MinimumCtMilliseconds);
        public string MaximumCtText => KBProductionStatisticsCalculator.FormatMilliseconds(MaximumCtMilliseconds);
    }

    public sealed class KBProductionTrendPoint
    {
        public DateTime Time { get; init; }
        public string Label { get; init; } = string.Empty;
        public int ProductionCount { get; init; }
        public double AverageCtMilliseconds { get; init; }
    }

    public sealed class KBProductionRecordRow
    {
        public int Id { get; set; }
        public string SN { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public DateTime CreateTime { get; set; }
        public FlowStatus FlowStatus { get; set; }
        public bool Result { get; set; }
        public long RunTimeMilliseconds { get; set; }
        public int NbrFailPoints { get; set; }
        public string Msg { get; set; } = string.Empty;

        public string ResultText => Result ? "PASS" : "FAIL";
        public string CycleTimeText => KBProductionStatisticsCalculator.FormatMilliseconds(RunTimeMilliseconds);
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
            DateTime now,
            KBProductionPeriodMode periodMode)
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
            Dictionary<DateTime, List<KBItemMaster>> resultsByDate = results
                .GroupBy(item => item.CreateTime.Date)
                .ToDictionary(group => group.Key, group => group.ToList());
            IEnumerable<DateTime> dailyKeys = resultsByDate.Keys
                .Concat(dailyTargets.Keys)
                .Distinct()
                .OrderByDescending(date => date);
            List<KBDailyProductionRow> dailyRows = dailyKeys
                .Select(date =>
                {
                    ProductionMetrics metrics = CalculateMetrics(resultsByDate.GetValueOrDefault(date) ?? []);
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

            Dictionary<int, List<KBItemMaster>> resultsBySession = results
                .Where(item => item.ProductionSessionId > 0)
                .GroupBy(item => item.ProductionSessionId!.Value)
                .ToDictionary(group => group.Key, group => group.ToList());
            var sessionRows = new List<KBProductionSessionRow>();
            foreach (KBProductionSession session in sessions.OrderByDescending(item => item.StartTime))
            {
                List<KBItemMaster> sessionResults = resultsBySession.GetValueOrDefault(session.Id) ?? [];
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
                    .ToList(),
                TrendRows = BuildTrendRows(results, periodMode)
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

        private static List<KBProductionTrendPoint> BuildTrendRows(
            IEnumerable<KBItemMaster> source,
            KBProductionPeriodMode periodMode)
        {
            List<KBItemMaster> completed = source
                .Where(item => item.FlowStatus == FlowStatus.Completed)
                .OrderBy(item => item.CreateTime)
                .ThenBy(item => item.Id)
                .ToList();
            if (periodMode == KBProductionPeriodMode.All)
            {
                return completed
                    .GroupBy(item => new DateTime(item.CreateTime.Year, item.CreateTime.Month, 1))
                    .OrderBy(group => group.Key)
                    .Select(group =>
                    {
                        ProductionMetrics metrics = CalculateMetrics(group);
                        return new KBProductionTrendPoint
                        {
                            Time = group.Key,
                            Label = group.Key.ToString("yyyy/MM"),
                            ProductionCount = metrics.ProductionCount,
                            AverageCtMilliseconds = metrics.AverageCtMilliseconds
                        };
                    })
                    .ToList();
            }

            return completed.Select(item => new KBProductionTrendPoint
            {
                Time = item.CreateTime,
                Label = periodMode == KBProductionPeriodMode.Day
                    ? item.CreateTime.ToString("HH:mm:ss")
                    : item.CreateTime.ToString("MM/dd HH:mm:ss"),
                ProductionCount = 1,
                AverageCtMilliseconds = Math.Max(0, item.RunTime)
            }).ToList();
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
                db.Ado.ExecuteCommand("CREATE INDEX IF NOT EXISTS \"IX_KBItemMaster_CreateTime\" ON \"KBItemMaster\" (\"CreateTime\");");
                db.Ado.ExecuteCommand("CREATE INDEX IF NOT EXISTS \"IX_KBItemMaster_Model\" ON \"KBItemMaster\" (\"Model\");");
                db.Ado.ExecuteCommand("CREATE INDEX IF NOT EXISTS \"IX_KBItemMaster_SN\" ON \"KBItemMaster\" (\"SN\");");
                db.Ado.ExecuteCommand("CREATE INDEX IF NOT EXISTS \"IX_KBItemMaster_Result\" ON \"KBItemMaster\" (\"Result\");");
                db.Ado.ExecuteCommand(
                    "CREATE INDEX IF NOT EXISTS \"IX_KBItemMaster_StatisticsCover\" ON \"KBItemMaster\" " +
                    "(\"CreateTime\", \"Id\", \"ProductionSessionId\", \"FlowStatus\", \"Result\", \"RunTime\", \"Model\", \"SN\");");
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
                .Distinct()
                .ToList()
                .Where(model => !string.IsNullOrWhiteSpace(model))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public IReadOnlyList<string> QuerySerialNumbers()
        {
            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            System.Data.DataTable table = db.Ado.GetDataTable(
                "SELECT MIN(TRIM(\"SN\")) AS \"Value\" " +
                "FROM \"KBItemMaster\" " +
                "WHERE \"SN\" IS NOT NULL AND TRIM(\"SN\") <> '' " +
                "GROUP BY TRIM(\"SN\") COLLATE NOCASE " +
                "ORDER BY MAX(\"Id\") DESC;");
            return table.Rows.Cast<System.Data.DataRow>()
                .Select(row => Convert.ToString(row["Value"]) ?? string.Empty)
                .Where(sn => sn.Length > 0)
                .ToList();
        }

        public KBProductionStatistics QueryStatistics(KBProductionQuery query, DateTime now)
        {
            ValidateQuery(query);
            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            List<KBItemMaster> results = ApplyResultFilters(db.Queryable<KBItemMaster>(), query)
                .Select(item => new KBItemMaster
                {
                    Id = item.Id,
                    ProductionSessionId = item.ProductionSessionId,
                    FlowStatus = item.FlowStatus,
                    Result = item.Result,
                    RunTime = item.RunTime,
                    CreateTime = item.CreateTime,
                })
                .ToList();

            ISugarQueryable<KBProductionSession> sessionQuery = db.Queryable<KBProductionSession>()
                .Where(item => item.StartTime >= query.From && item.StartTime < query.ToExclusive);
            if (!string.IsNullOrWhiteSpace(query.Model))
            {
                string model = query.Model.Trim();
                sessionQuery = sessionQuery.Where(item => item.Model.Contains(model));
            }

            return KBProductionStatisticsCalculator.Calculate(
                results,
                sessionQuery.ToList(),
                query.From,
                query.ToExclusive,
                now,
                query.PeriodMode);
        }

        public IReadOnlyList<KBProductionRecordRow> QueryRecords(KBProductionQuery query)
        {
            ValidateQuery(query);
            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            return ApplyResultFilters(db.Queryable<KBItemMaster>(), query)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .Select(item => new KBProductionRecordRow
                {
                    Id = item.Id,
                    SN = item.SN,
                    Model = item.Model,
                    CreateTime = item.CreateTime,
                    FlowStatus = item.FlowStatus,
                    Result = item.Result,
                    RunTimeMilliseconds = item.RunTime,
                    NbrFailPoints = item.NbrFailPoints,
                    Msg = item.Msg
                })
                .ToPageList(query.PageNumber, query.PageSize);
        }

        public int QueryRecordCount(KBProductionQuery query)
        {
            ValidateQuery(query);
            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            return ApplyResultFilters(db.Queryable<KBItemMaster>(), query).Count();
        }

        private SqlSugarClient CreateClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={DatabasePath};Default Timeout=5",
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

        private static ISugarQueryable<KBItemMaster> ApplyResultFilters(
            ISugarQueryable<KBItemMaster> resultQuery,
            KBProductionQuery query)
        {
            if (query.PeriodMode != KBProductionPeriodMode.All || query.From != DateTime.MinValue || query.ToExclusive != DateTime.MaxValue)
                resultQuery = resultQuery.Where(item => item.CreateTime >= query.From && item.CreateTime < query.ToExclusive);
            if (!string.IsNullOrWhiteSpace(query.Model))
            {
                string model = query.Model.Trim();
                resultQuery = resultQuery.Where(item => item.Model.Contains(model));
            }
            if (!string.IsNullOrWhiteSpace(query.SN))
            {
                string sn = query.SN.Trim();
                resultQuery = resultQuery.Where(item => item.SN.Contains(sn));
            }
            if (query.Result.HasValue)
            {
                bool result = query.Result.Value;
                resultQuery = resultQuery.Where(item => item.Result == result);
            }
            return resultQuery;
        }

        private static void ValidateQuery(KBProductionQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            if (query.ToExclusive <= query.From)
                throw new ArgumentOutOfRangeException(nameof(query), "结束时间必须晚于开始时间。");
            if (query.PageNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(query), query.PageNumber, "页码必须大于零。");
            if (query.PageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(query), query.PageSize, "每页数量必须大于零。");
        }

        private static string Normalize(string? value) => value?.Trim() ?? string.Empty;
    }

    internal static class KBProductionSuggestionFilter
    {
        public static IReadOnlyList<string> Filter(IEnumerable<string> suggestions, string? text, int limit)
        {
            ArgumentNullException.ThrowIfNull(suggestions);
            if (limit <= 0)
                return [];

            string filter = text?.Trim() ?? string.Empty;
            IEnumerable<string> candidates = suggestions
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);
            if (filter.Length == 0)
                return candidates.Take(limit).ToList();

            return candidates
                .Where(item => item.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                .ThenBy(item => item, StringComparer.OrdinalIgnoreCase)
                .Take(limit)
                .ToList();
        }
    }
}
