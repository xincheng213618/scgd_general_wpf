using SqlSugar;

namespace ProjectARVRPro
{
    public enum ResultStatisticsPeriodMode
    {
        Day,
        Week,
        Month,
    }

    public readonly record struct ResultStatisticsPeriodRange(DateTime From, DateTime ToExclusive)
    {
        public string ToDisplayText(ResultStatisticsPeriodMode mode)
        {
            return mode switch
            {
                ResultStatisticsPeriodMode.Week => $"{From:yyyy/MM/dd} - {ToExclusive.AddDays(-1):MM/dd}",
                ResultStatisticsPeriodMode.Month => From.ToString("yyyy/MM"),
                _ => From.ToString("yyyy/MM/dd"),
            };
        }
    }

    public static class ResultStatisticsPeriod
    {
        public static ResultStatisticsPeriodRange GetRange(ResultStatisticsPeriodMode mode, DateTime anchor)
        {
            DateTime day = anchor.Date;
            return mode switch
            {
                ResultStatisticsPeriodMode.Week => CreateWeekRange(day),
                ResultStatisticsPeriodMode.Month => new ResultStatisticsPeriodRange(
                    new DateTime(day.Year, day.Month, 1),
                    new DateTime(day.Year, day.Month, 1).AddMonths(1)),
                _ => new ResultStatisticsPeriodRange(day, day.AddDays(1)),
            };
        }

        public static DateTime ShiftAnchor(ResultStatisticsPeriodMode mode, DateTime anchor, int offset)
        {
            return mode switch
            {
                ResultStatisticsPeriodMode.Week => anchor.Date.AddDays(checked(offset * 7)),
                ResultStatisticsPeriodMode.Month => anchor.Date.AddMonths(offset),
                _ => anchor.Date.AddDays(offset),
            };
        }

        private static ResultStatisticsPeriodRange CreateWeekRange(DateTime day)
        {
            int daysSinceMonday = ((int)day.DayOfWeek + 6) % 7;
            DateTime from = day.AddDays(-daysSinceMonday);
            return new ResultStatisticsPeriodRange(from, from.AddDays(7));
        }
    }

    public sealed class ResultStatisticsQuery
    {
        public DateTime From { get; init; } = DateTime.Today;
        public DateTime ToExclusive { get; init; } = DateTime.Today.AddDays(1);
        public string? SN { get; init; }
        public bool? Result { get; init; }
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 1000;
    }

    public sealed class FlowExecutionQuery
    {
        public DateTime From { get; init; } = DateTime.Today;
        public DateTime ToExclusive { get; init; } = DateTime.Today.AddDays(1);
        public string? Model { get; init; }
        public bool? Result { get; init; }
        public int PageNumber { get; init; } = 1;
        public int PageSize { get; init; } = 1000;
    }

    public sealed class FlowExecutionRecordRow
    {
        public int Id { get; set; }
        public string SN { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public DateTime CreateTime { get; set; }
        public long RunTimeMilliseconds { get; set; }
        public bool Result { get; set; }

        public string RunTimeText => ResultStatisticsCalculator.FormatMilliseconds(RunTimeMilliseconds);
        public string ResultText => Result ? "PASS" : "FAIL";
    }

    public sealed class ResultStatisticsSample
    {
        public int Id { get; set; }
        public string SN { get; set; } = string.Empty;
        public bool Result { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public double CycleTimeMilliseconds => Math.Max(0, (EndTime - StartTime).TotalMilliseconds);
        public DateTime ProductionTime => EndTime >= StartTime ? EndTime : StartTime;
    }

    public sealed class ResultStatistics
    {
        public int TotalCount { get; init; }
        public int PassCount { get; init; }
        public int FailCount { get; init; }
        public double PassRate { get; init; }
        public double FailRate { get; init; }
        public double AverageCtMilliseconds { get; init; }
        public double MinimumCtMilliseconds { get; init; }
        public double MaximumCtMilliseconds { get; init; }
        public int CurrentHourCount { get; init; }
        public int TodayCount { get; init; }
        public IReadOnlyList<ResultStatisticsHourlyRow> HourlyRows { get; init; } = [];
        public IReadOnlyList<ResultStatisticsDailyRow> DailyRows { get; init; } = [];

        public int TotalProduction => TotalCount;
        public int SuccessCount => PassCount;
        public int FailureCount => FailCount;
        public double SuccessRate => PassRate;
        public double FailureRate => FailRate;
        public int CurrentHourProduction => CurrentHourCount;
        public int TodayProduction => TodayCount;
        public string PassRateText => ResultStatisticsCalculator.FormatRate(PassRate);
        public string FailRateText => ResultStatisticsCalculator.FormatRate(FailRate);
        public string AverageCtText => TotalCount > 0 ? ResultStatisticsCalculator.FormatMilliseconds(AverageCtMilliseconds) : "-";
        public string MinimumCtText => TotalCount > 0 ? ResultStatisticsCalculator.FormatMilliseconds(MinimumCtMilliseconds) : "-";
        public string MaximumCtText => TotalCount > 0 ? ResultStatisticsCalculator.FormatMilliseconds(MaximumCtMilliseconds) : "-";
    }

    public sealed class ResultStatisticsHourlyRow
    {
        public DateTime Hour { get; init; }
        public int TotalCount { get; init; }
        public int PassCount { get; init; }
        public int FailCount { get; init; }
        public double PassRate { get; init; }
        public double FailRate { get; init; }
        public double AverageCtMilliseconds { get; init; }

        public string HourText => Hour.ToString("yyyy/MM/dd HH:00");
        public string PassRateText => ResultStatisticsCalculator.FormatRate(PassRate);
        public string FailRateText => ResultStatisticsCalculator.FormatRate(FailRate);
        public string AverageCtText => ResultStatisticsCalculator.FormatMilliseconds(AverageCtMilliseconds);
    }

    public sealed class ResultStatisticsDailyRow
    {
        public DateTime Date { get; init; }
        public int TotalCount { get; init; }
        public int PassCount { get; init; }
        public int FailCount { get; init; }
        public double PassRate { get; init; }
        public double FailRate { get; init; }
        public double AverageCtMilliseconds { get; init; }

        public string DateText => Date.ToString("yyyy/MM/dd");
        public string PassRateText => ResultStatisticsCalculator.FormatRate(PassRate);
        public string FailRateText => ResultStatisticsCalculator.FormatRate(FailRate);
        public string AverageCtText => ResultStatisticsCalculator.FormatMilliseconds(AverageCtMilliseconds);
    }

    public sealed class ResultStatisticsRecordRow
    {
        public int Id { get; set; }
        public int ExecutionIndex { get; set; }
        public string SN { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool Result { get; set; }
        public string LastModel { get; set; } = string.Empty;
        public int BatchId { get; set; }
        public int ResultId { get; set; }
        public string Msg { get; set; } = string.Empty;
        public int PreviousResultId { get; set; }
        public int FlowCount { get; set; }
        public long FlowRunTimeMilliseconds { get; set; }

        public double CycleTimeMilliseconds => Math.Max(0, (EndTime - StartTime).TotalMilliseconds);
        public string ExecutionText => $"第 {ExecutionIndex} 次";
        public string CycleTimeText => ResultStatisticsCalculator.FormatMilliseconds(CycleTimeMilliseconds);
        public string FlowCountText => FlowCount > 0 ? $"{FlowCount} 个" : "-";
        public string FlowRunTimeText => FlowCount > 0 ? ResultStatisticsCalculator.FormatMilliseconds(FlowRunTimeMilliseconds) : "-";
        public string ResultText => Result ? "PASS" : "FAIL";
    }

    public sealed class ResultStatisticsSnSummary
    {
        public string SN { get; init; } = string.Empty;
        public int TotalCount { get; init; }
        public int PassCount { get; init; }
        public int FailCount { get; init; }
        public double PassRate { get; init; }
        public DateTime FirstTime { get; init; }
        public DateTime LastTime { get; init; }

        public string PassRateText => ResultStatisticsCalculator.FormatRate(PassRate);
    }

    public static class ResultStatisticsCalculator
    {
        public static ResultStatistics Calculate(
            IEnumerable<ResultStatisticsSample> source,
            DateTime from,
            DateTime toExclusive,
            DateTime now)
        {
            ArgumentNullException.ThrowIfNull(source);
            ValidateRange(from, toExclusive);

            List<ResultStatisticsSample> samples = source
                .Where(item => item.ProductionTime >= from && item.ProductionTime < toExclusive)
                .OrderBy(item => item.ProductionTime)
                .ThenBy(item => item.Id)
                .ToList();
            ResultStatisticsMetrics overall = CalculateMetrics(samples);

            List<ResultStatisticsHourlyRow> hourlyRows = samples
                .GroupBy(item => new DateTime(
                    item.ProductionTime.Year,
                    item.ProductionTime.Month,
                    item.ProductionTime.Day,
                    item.ProductionTime.Hour,
                    0,
                    0,
                    item.ProductionTime.Kind))
                .OrderByDescending(group => group.Key)
                .Select(group =>
                {
                    ResultStatisticsMetrics metrics = CalculateMetrics(group);
                    return new ResultStatisticsHourlyRow
                    {
                        Hour = group.Key,
                        TotalCount = metrics.TotalCount,
                        PassCount = metrics.PassCount,
                        FailCount = metrics.FailCount,
                        PassRate = metrics.PassRate,
                        FailRate = metrics.FailRate,
                        AverageCtMilliseconds = metrics.AverageCtMilliseconds,
                    };
                })
                .ToList();

            List<ResultStatisticsDailyRow> dailyRows = samples
                .GroupBy(item => item.ProductionTime.Date)
                .OrderByDescending(group => group.Key)
                .Select(group =>
                {
                    ResultStatisticsMetrics metrics = CalculateMetrics(group);
                    return new ResultStatisticsDailyRow
                    {
                        Date = group.Key,
                        TotalCount = metrics.TotalCount,
                        PassCount = metrics.PassCount,
                        FailCount = metrics.FailCount,
                        PassRate = metrics.PassRate,
                        FailRate = metrics.FailRate,
                        AverageCtMilliseconds = metrics.AverageCtMilliseconds,
                    };
                })
                .ToList();

            DateTime currentHour = new(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Kind);
            return new ResultStatistics
            {
                TotalCount = overall.TotalCount,
                PassCount = overall.PassCount,
                FailCount = overall.FailCount,
                PassRate = overall.PassRate,
                FailRate = overall.FailRate,
                AverageCtMilliseconds = overall.AverageCtMilliseconds,
                MinimumCtMilliseconds = overall.MinimumCtMilliseconds,
                MaximumCtMilliseconds = overall.MaximumCtMilliseconds,
                CurrentHourCount = hourlyRows.FirstOrDefault(item => item.Hour == currentHour)?.TotalCount ?? 0,
                TodayCount = dailyRows.FirstOrDefault(item => item.Date == now.Date)?.TotalCount ?? 0,
                HourlyRows = hourlyRows,
                DailyRows = dailyRows,
            };
        }

        public static IReadOnlyList<ResultStatisticsSnSummary> CalculateSnSummaries(IEnumerable<ResultStatisticsSample> source)
        {
            ArgumentNullException.ThrowIfNull(source);

            return source
                .Where(item => !string.IsNullOrWhiteSpace(item.SN))
                .GroupBy(item => item.SN.Trim(), StringComparer.Ordinal)
                .Select(group =>
                {
                    ResultStatisticsMetrics metrics = CalculateMetrics(group);
                    return new ResultStatisticsSnSummary
                    {
                        SN = group.Key,
                        TotalCount = metrics.TotalCount,
                        PassCount = metrics.PassCount,
                        FailCount = metrics.FailCount,
                        PassRate = metrics.PassRate,
                        FirstTime = group.Min(item => item.StartTime),
                        LastTime = group.Max(item => item.EndTime),
                    };
                })
                .OrderByDescending(item => item.LastTime)
                .ThenBy(item => item.SN, StringComparer.Ordinal)
                .ToList();
        }

        public static string FormatMilliseconds(double milliseconds) => $"{Math.Max(0, milliseconds) / 1000d:F3} s";

        public static string FormatRate(double rate) => $"{Math.Clamp(rate, 0, 1):P2}";

        internal static void ValidateRange(DateTime from, DateTime toExclusive)
        {
            if (toExclusive <= from)
                throw new ArgumentOutOfRangeException(nameof(toExclusive), "结束时间必须晚于开始时间。");
        }

        private static ResultStatisticsMetrics CalculateMetrics(IEnumerable<ResultStatisticsSample> source)
        {
            List<ResultStatisticsSample> samples = source.ToList();
            int passCount = samples.Count(item => item.Result);
            int failCount = samples.Count - passCount;
            List<double> cycleTimes = samples.Select(item => item.CycleTimeMilliseconds).ToList();
            return new ResultStatisticsMetrics(
                samples.Count,
                passCount,
                failCount,
                samples.Count > 0 ? passCount / (double)samples.Count : 0,
                samples.Count > 0 ? failCount / (double)samples.Count : 0,
                cycleTimes.Count > 0 ? cycleTimes.Average() : 0,
                cycleTimes.Count > 0 ? cycleTimes.Min() : 0,
                cycleTimes.Count > 0 ? cycleTimes.Max() : 0);
        }

        private sealed record ResultStatisticsMetrics(
            int TotalCount,
            int PassCount,
            int FailCount,
            double PassRate,
            double FailRate,
            double AverageCtMilliseconds,
            double MinimumCtMilliseconds,
            double MaximumCtMilliseconds);
    }

    internal static class ResultStatisticsSuggestionFilter
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

    public sealed class ResultStatisticsDataStore
    {
        private const string TableName = "ObjectiveTestResultRecord";
        private static readonly Lazy<ResultStatisticsDataStore> LazyInstance = new(() => new ResultStatisticsDataStore());
        private readonly string? _databasePath;
        private readonly object _schemaGate = new();
        private bool _schemaInitialized;

        public static ResultStatisticsDataStore Instance => LazyInstance.Value;

        public ResultStatisticsDataStore(string? databasePath = null)
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
                db.Ado.ExecuteCommand("PRAGMA busy_timeout = 5000;");
                db.Ado.ExecuteCommand("PRAGMA journal_mode = WAL;");
                db.CodeFirst.InitTables<ObjectiveTestResultRecord, ProjectARVRReuslt>();
                db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{TableName}_SN\" ON \"{TableName}\" (\"SN\");");
                db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{TableName}_CreateTime\" ON \"{TableName}\" (\"CreateTime\");");
                db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{TableName}_UpdateTime\" ON \"{TableName}\" (\"UpdateTime\");");
                db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{TableName}_TotalResult\" ON \"{TableName}\" (\"TotalResult\");");
                db.Ado.ExecuteCommand($"CREATE INDEX IF NOT EXISTS \"IX_{TableName}_IsFinalized_UpdateTime\" ON \"{TableName}\" (\"IsFinalized\", \"UpdateTime\");");
                db.Ado.ExecuteCommand("CREATE INDEX IF NOT EXISTS \"IX_ARVRReuslt_CreateTime\" ON \"ARVRReuslt\" (\"CreateTime\");");
                db.Ado.ExecuteCommand("CREATE INDEX IF NOT EXISTS \"IX_ARVRReuslt_SN_CreateTime\" ON \"ARVRReuslt\" (\"SN\", \"CreateTime\");");
                db.Ado.ExecuteCommand("CREATE INDEX IF NOT EXISTS \"IX_ARVRReuslt_SN_Id\" ON \"ARVRReuslt\" (\"SN\", \"Id\");");
                _schemaInitialized = true;
            }
        }

        public ResultStatistics QueryStatistics(ResultStatisticsQuery query, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(query);
            ResultStatisticsCalculator.ValidateRange(query.From, query.ToExclusive);
            InitializeSchema();

            using SqlSugarClient db = CreateClient();
            List<ResultStatisticsSample> samples = ApplyFilters(db.Queryable<ObjectiveTestResultRecord>(), query)
                .Select(item => new ResultStatisticsSample
                {
                    Id = item.Id,
                    SN = item.SN,
                    Result = item.TotalResult,
                    StartTime = item.CreateTime,
                    EndTime = item.UpdateTime,
                })
                .ToList();

            return ResultStatisticsCalculator.Calculate(samples, query.From, query.ToExclusive, now);
        }

        public IReadOnlyList<ResultStatisticsRecordRow> QueryRecords(ResultStatisticsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            ResultStatisticsCalculator.ValidateRange(query.From, query.ToExclusive);
            if (query.PageNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(query), query.PageNumber, "页码必须大于零。");
            if (query.PageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(query), query.PageSize, "每页数量必须大于零。");

            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            int skip = checked((query.PageNumber - 1) * query.PageSize);
            const string sql = """
                WITH RankedRecords AS
                (
                    SELECT "Id",
                           "SN",
                           "CreateTime" AS "StartTime",
                           "UpdateTime" AS "EndTime",
                           "TotalResult" AS "Result",
                           "LastModel",
                           "BatchId",
                           "ResultId",
                           "Msg",
                           COALESCE(LAG("ResultId") OVER (PARTITION BY TRIM("SN") ORDER BY "Id"), 0) AS "PreviousResultId",
                           ROW_NUMBER() OVER (PARTITION BY TRIM("SN") ORDER BY "Id") AS "ExecutionIndex"
                    FROM "ObjectiveTestResultRecord"
                    WHERE "IsFinalized" = 1 OR "IsFinalized" IS NULL
                )
                SELECT R."Id", R."ExecutionIndex", R."SN", R."StartTime", R."EndTime", R."Result",
                       R."LastModel", R."BatchId", R."ResultId", R."Msg", R."PreviousResultId",
                       CASE WHEN R."ResultId" > R."PreviousResultId" THEN
                           (SELECT COUNT(*)
                            FROM "ARVRReuslt" AS F
                            WHERE F."SN" = R."SN"
                              AND F."Id" > R."PreviousResultId"
                              AND F."Id" <= R."ResultId")
                           ELSE 0 END AS "FlowCount",
                       CASE WHEN R."ResultId" > R."PreviousResultId" THEN
                           COALESCE((SELECT SUM(F."RunTime")
                                     FROM "ARVRReuslt" AS F
                                     WHERE F."SN" = R."SN"
                                       AND F."Id" > R."PreviousResultId"
                                       AND F."Id" <= R."ResultId"), 0)
                           ELSE 0 END AS "FlowRunTimeMilliseconds"
                FROM RankedRecords AS R
                WHERE R."EndTime" >= @From
                  AND R."EndTime" < @ToExclusive
                  AND (@SN IS NULL OR R."SN" LIKE '%' || @SN || '%')
                  AND (@Result IS NULL OR R."Result" = @Result)
                ORDER BY R."Id" DESC
                LIMIT @PageSize OFFSET @Skip;
                """;
            return db.Ado.SqlQuery<ResultStatisticsRecordRow>(
                sql,
                new SugarParameter("@From", query.From),
                new SugarParameter("@ToExclusive", query.ToExclusive),
                new SugarParameter("@SN", string.IsNullOrWhiteSpace(query.SN) ? DBNull.Value : query.SN.Trim()),
                new SugarParameter("@Result", query.Result.HasValue ? query.Result.Value : DBNull.Value),
                new SugarParameter("@PageSize", query.PageSize),
                new SugarParameter("@Skip", skip));
        }

        public int QueryRecordCount(ResultStatisticsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            ResultStatisticsCalculator.ValidateRange(query.From, query.ToExclusive);
            InitializeSchema();

            using SqlSugarClient db = CreateClient();
            return ApplyFilters(db.Queryable<ObjectiveTestResultRecord>(), query).Count();
        }

        public IReadOnlyList<ProjectARVRReuslt> QueryFlowDetails(ResultStatisticsRecordRow record)
        {
            ArgumentNullException.ThrowIfNull(record);
            if (string.IsNullOrWhiteSpace(record.SN))
                return [];

            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            string sn = record.SN;
            if (record.ResultId > record.PreviousResultId)
            {
                int previousResultId = Math.Max(0, record.PreviousResultId);
                int resultId = record.ResultId;
                return db.Queryable<ProjectARVRReuslt>()
                    .Where(item => item.SN == sn && item.Id > previousResultId && item.Id <= resultId)
                    .OrderBy(item => item.Id, OrderByType.Asc)
                    .ToList();
            }

            DateTime startTime = record.StartTime;
            DateTime endTime = record.EndTime >= startTime ? record.EndTime : startTime;
            ISugarQueryable<ProjectARVRReuslt> query = db.Queryable<ProjectARVRReuslt>()
                .Where(item => item.SN == sn && item.CreateTime >= startTime && item.CreateTime <= endTime);
            if (record.ResultId > 0)
            {
                int resultId = record.ResultId;
                query = query.Where(item => item.Id <= resultId);
            }

            return query.OrderBy(item => item.Id, OrderByType.Asc).ToList();
        }

        public IReadOnlyList<FlowExecutionRecordRow> QueryFlowExecutions(FlowExecutionQuery query)
        {
            ValidateFlowExecutionQuery(query);
            InitializeSchema();

            using SqlSugarClient db = CreateClient();
            return ApplyFlowFilters(db.Queryable<ProjectARVRReuslt>(), query)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .Select(item => new FlowExecutionRecordRow
                {
                    Id = item.Id,
                    SN = item.SN,
                    Model = item.Model,
                    CreateTime = item.CreateTime,
                    RunTimeMilliseconds = item.RunTime,
                    Result = item.Result,
                })
                .ToPageList(query.PageNumber, query.PageSize);
        }

        public int QueryFlowExecutionCount(FlowExecutionQuery query)
        {
            ValidateFlowExecutionQuery(query);
            InitializeSchema();

            using SqlSugarClient db = CreateClient();
            return ApplyFlowFilters(db.Queryable<ProjectARVRReuslt>(), query).Count();
        }

        public IReadOnlyList<string> QueryFlowNames()
        {
            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            const string sql = """
                SELECT DISTINCT TRIM("Model") AS "Model"
                FROM "ARVRReuslt"
                WHERE TRIM("Model") <> ''
                ORDER BY TRIM("Model");
                """;
            return db.Ado.SqlQuery<FlowExecutionNameRow>(sql)
                .Select(item => item.Model)
                .ToList();
        }

        public IReadOnlyList<ResultStatisticsSnSummary> QuerySnSummaries()
        {
            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            List<ResultStatisticsSnAggregate> aggregates = db.Queryable<ObjectiveTestResultRecord>()
                .Where(item => item.SN.Trim() != string.Empty && (item.IsFinalized == true || item.IsFinalized == null))
                .GroupBy(item => item.SN.Trim())
                .Select(item => new ResultStatisticsSnAggregate
                {
                    SN = item.SN.Trim(),
                    TotalCount = SqlFunc.AggregateCount(item.Id),
                    PassCount = SqlFunc.AggregateSum(item.TotalResult ? 1 : 0),
                    FirstTime = SqlFunc.AggregateMin(item.CreateTime),
                    LastTime = SqlFunc.AggregateMax(item.UpdateTime),
                })
                .OrderBy(item => item.LastTime, OrderByType.Desc)
                .ToList();

            return aggregates.Select(item => new ResultStatisticsSnSummary
            {
                SN = item.SN,
                TotalCount = item.TotalCount,
                PassCount = item.PassCount,
                FailCount = item.TotalCount - item.PassCount,
                PassRate = item.TotalCount > 0 ? item.PassCount / (double)item.TotalCount : 0,
                FirstTime = item.FirstTime,
                LastTime = item.LastTime,
            }).ToList();
        }

        public ObjectiveTestResultRecord? GetRecord(int id)
        {
            if (id <= 0)
                return null;

            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            return db.Queryable<ObjectiveTestResultRecord>().Where(item => item.Id == id).First();
        }

        public IReadOnlyList<ObjectiveTestResultRecord> GetRecords(IEnumerable<int> ids)
        {
            ArgumentNullException.ThrowIfNull(ids);
            int[] recordIds = ids.Where(id => id > 0).Distinct().ToArray();
            if (recordIds.Length == 0)
                return [];

            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            return db.Queryable<ObjectiveTestResultRecord>()
                .Where(item => recordIds.Contains(item.Id))
                .ToList();
        }

        private static ISugarQueryable<ObjectiveTestResultRecord> ApplyFilters(
            ISugarQueryable<ObjectiveTestResultRecord> queryable,
            ResultStatisticsQuery query)
        {
            DateTime from = query.From;
            DateTime toExclusive = query.ToExclusive;
            ISugarQueryable<ObjectiveTestResultRecord> queryResult = queryable
                .Where(item => item.UpdateTime >= from
                    && item.UpdateTime < toExclusive
                    && (item.IsFinalized == true || item.IsFinalized == null));
            string? sn = string.IsNullOrWhiteSpace(query.SN) ? null : query.SN.Trim();
            if (sn != null)
                queryResult = queryResult.Where(item => item.SN.Contains(sn));
            if (query.Result.HasValue)
            {
                bool expectedResult = query.Result.Value;
                queryResult = queryResult.Where(item => item.TotalResult == expectedResult);
            }

            return queryResult;
        }

        private static ISugarQueryable<ProjectARVRReuslt> ApplyFlowFilters(
            ISugarQueryable<ProjectARVRReuslt> queryable,
            FlowExecutionQuery query)
        {
            DateTime from = query.From;
            DateTime toExclusive = query.ToExclusive;
            ISugarQueryable<ProjectARVRReuslt> queryResult = queryable
                .Where(item => item.CreateTime >= from && item.CreateTime < toExclusive);
            string? model = string.IsNullOrWhiteSpace(query.Model) ? null : query.Model.Trim();
            if (model != null)
                queryResult = queryResult.Where(item => item.Model.Contains(model));
            if (query.Result.HasValue)
            {
                bool expectedResult = query.Result.Value;
                queryResult = queryResult.Where(item => item.Result == expectedResult);
            }

            return queryResult;
        }

        private static void ValidateFlowExecutionQuery(FlowExecutionQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            ResultStatisticsCalculator.ValidateRange(query.From, query.ToExclusive);
            if (query.PageNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(query), query.PageNumber, "页码必须大于零。");
            if (query.PageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(query), query.PageSize, "每页数量必须大于零。");
        }

        private SqlSugarClient CreateClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={DatabasePath};Default Timeout=5",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
        }

        private sealed class ResultStatisticsSnAggregate
        {
            public string SN { get; set; } = string.Empty;
            public int TotalCount { get; set; }
            public int PassCount { get; set; }
            public DateTime FirstTime { get; set; }
            public DateTime LastTime { get; set; }
        }

        private sealed class FlowExecutionNameRow
        {
            public string Model { get; set; } = string.Empty;
        }
    }
}
