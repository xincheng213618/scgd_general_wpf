using Microsoft.Data.Sqlite;
using SqlSugar;
using ColorVision.Database;
using System.Globalization;

namespace ProjectARVRPro
{
    public enum ResultStatisticsPeriodMode
    {
        Day,
        Week,
        Month,
        All,
    }

    public readonly record struct ResultStatisticsPeriodRange(DateTime From, DateTime ToExclusive)
    {
        public string ToDisplayText(ResultStatisticsPeriodMode mode)
        {
            return mode switch
            {
                ResultStatisticsPeriodMode.All => "全部记录",
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
                ResultStatisticsPeriodMode.All => new ResultStatisticsPeriodRange(DateTime.MinValue, DateTime.MaxValue),
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
                ResultStatisticsPeriodMode.All => anchor.Date,
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
        public double? FlowRunTimeMilliseconds { get; set; }

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
        public int FlowRunTimeSampleCount { get; init; }
        public double AverageFlowRunTimeMilliseconds { get; init; }
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
        public string AverageFlowRunTimeText => FlowRunTimeSampleCount > 0 ? ResultStatisticsCalculator.FormatMilliseconds(AverageFlowRunTimeMilliseconds) : "-";
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

    public sealed class ResultStatisticsTrendPoint
    {
        public DateTime Time { get; init; }
        public string Label { get; init; } = string.Empty;
        public int TotalCount { get; init; }
        public double AverageCtMilliseconds { get; init; }
    }

    public sealed class ResultStatisticsDashboard
    {
        public ResultStatistics Summary { get; init; } = new();
        public IReadOnlyList<ResultStatisticsTrendPoint> Trend { get; init; } = [];
    }

    public sealed class ResultStatisticsCombinedDashboard
    {
        public ResultStatistics Summary { get; init; } = new();
        public IReadOnlyList<ResultStatisticsTrendPoint> Trend { get; init; } = [];
        public double AverageTransitionMilliseconds { get; init; }

        public string AverageTransitionText => Summary.TotalCount > 0
            ? ResultStatisticsCalculator.FormatMilliseconds(AverageTransitionMilliseconds)
            : "-";
    }

    public static class ResultStatisticsTrendBuilder
    {
        public static IReadOnlyList<ResultStatisticsTrendPoint> BuildHourly(
            IReadOnlyList<ResultStatisticsTrendPoint> details,
            DateTime from,
            DateTime toExclusive)
        {
            ArgumentNullException.ThrowIfNull(details);
            ResultStatisticsCalculator.ValidateRange(from, toExclusive);
            Dictionary<DateTime, ResultStatisticsTrendPoint> hourly = details
                .Where(item => item.TotalCount > 0 && item.Time >= from && item.Time < toExclusive)
                .GroupBy(item => new DateTime(item.Time.Year, item.Time.Month, item.Time.Day, item.Time.Hour, 0, 0))
                .ToDictionary(group => group.Key, group =>
                {
                    int count = group.Sum(item => item.TotalCount);
                    return new ResultStatisticsTrendPoint
                    {
                        Time = group.Key,
                        TotalCount = count,
                        AverageCtMilliseconds = group.Sum(item => item.AverageCtMilliseconds * item.TotalCount) / count,
                    };
                });
            List<ResultStatisticsTrendPoint> points = [];
            for (DateTime hour = new(from.Year, from.Month, from.Day, from.Hour, 0, 0); hour < toExclusive; hour = hour.AddHours(1))
            {
                points.Add(hourly.TryGetValue(hour, out ResultStatisticsTrendPoint? point)
                    ? point
                    : new ResultStatisticsTrendPoint { Time = hour });
            }
            return points;
        }

        public static IReadOnlyList<ResultStatisticsTrendPoint> BuildMonthly(ResultStatistics statistics)
        {
            ArgumentNullException.ThrowIfNull(statistics);

            Dictionary<DateTime, ResultStatisticsTrendPoint> monthly = statistics.DailyRows
                .GroupBy(item => new DateTime(item.Date.Year, item.Date.Month, 1))
                .ToDictionary(group => group.Key, group => CreatePoint(group.Key, group));
            if (monthly.Count == 0)
                return [];

            DateTime firstMonth = monthly.Keys.Min();
            DateTime lastMonth = monthly.Keys.Max();
            int monthCount = checked((lastMonth.Year - firstMonth.Year) * 12 + lastMonth.Month - firstMonth.Month + 1);
            return Enumerable.Range(0, monthCount)
                .Select(firstMonth.AddMonths)
                .Select(month => monthly.TryGetValue(month, out ResultStatisticsTrendPoint? point)
                    ? point
                    : new ResultStatisticsTrendPoint { Time = month, Label = month.ToString("yyyy/MM") })
                .ToList();
        }

        public static IReadOnlyList<ResultStatisticsTrendPoint> BuildDetails(
            IEnumerable<ResultStatisticsSample> samples,
            ResultStatisticsPeriodMode mode)
        {
            ArgumentNullException.ThrowIfNull(samples);
            if (mode == ResultStatisticsPeriodMode.All)
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "全部周期必须使用月度聚合趋势。");

            return samples
                .OrderBy(item => item.EndTime)
                .ThenBy(item => item.Id)
                .Select(item => new ResultStatisticsTrendPoint
                {
                    Time = item.EndTime,
                    Label = mode == ResultStatisticsPeriodMode.Day
                        ? item.EndTime.ToString("HH:mm:ss")
                        : item.EndTime.ToString("MM/dd HH:mm:ss"),
                    TotalCount = 1,
                    AverageCtMilliseconds = Math.Max(0, (item.EndTime - item.StartTime).TotalMilliseconds),
                })
                .ToList();
        }

        private static ResultStatisticsTrendPoint CreatePoint(DateTime month, IEnumerable<ResultStatisticsDailyRow> rows)
        {
            List<ResultStatisticsDailyRow> values = rows.ToList();
            int totalCount = values.Sum(item => item.TotalCount);
            double averageCtMilliseconds = totalCount == 0
                ? 0
                : values.Sum(item => item.AverageCtMilliseconds * item.TotalCount) / totalCount;
            return new ResultStatisticsTrendPoint
            {
                Time = month,
                Label = month.ToString("yyyy/MM"),
                TotalCount = totalCount,
                AverageCtMilliseconds = averageCtMilliseconds,
            };
        }
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
        public int PreviousObjectiveId { get; set; }

        public double CycleTimeMilliseconds => Math.Max(0, (EndTime - StartTime).TotalMilliseconds);
        public string ExecutionText => $"第 {ExecutionIndex} 次";
        public string CycleTimeText => ResultStatisticsCalculator.FormatMilliseconds(CycleTimeMilliseconds);
        public string FlowCountText => FlowCount > 0 ? FlowCount.ToString(CultureInfo.InvariantCulture) : "-";
        public string FlowRunTimeText => FlowCount > 0 ? ResultStatisticsCalculator.FormatMilliseconds(FlowRunTimeMilliseconds) : "-";
        public string ResultText => Result ? "PASS" : "FAIL";
    }

    public enum ResultStatisticsSide
    {
        Left,
        Right,
    }

    public sealed class ResultStatisticsCombinedRecordRow
    {
        public required string SN { get; init; }
        public required ResultStatisticsRecordRow Left { get; init; }
        public required ResultStatisticsRecordRow Right { get; init; }

        public int Id => Right.Id;
        public DateTime StartTime => Left.StartTime;
        public DateTime LeftEndTime => Left.EndTime;
        public DateTime RightStartTime => Right.StartTime;
        public DateTime EndTime => Right.EndTime;
        public bool Result => Left.Result && Right.Result;
        public int FlowCount => Left.FlowCount + Right.FlowCount;
        public long FlowRunTimeMilliseconds => Left.FlowRunTimeMilliseconds + Right.FlowRunTimeMilliseconds;
        public double CycleTimeMilliseconds => Math.Max(0, (EndTime - StartTime).TotalMilliseconds);
        public double TransitionMilliseconds => Math.Max(0, (RightStartTime - LeftEndTime).TotalMilliseconds);
        public string CycleTimeText => ResultStatisticsCalculator.FormatMilliseconds(CycleTimeMilliseconds);
        public string TransitionText => ResultStatisticsCalculator.FormatMilliseconds(TransitionMilliseconds);
        public string SideCycleTimeText => $"L {Left.CycleTimeText} / R {Right.CycleTimeText}";
        public string FlowCountText => FlowCount > 0 ? FlowCount.ToString(CultureInfo.InvariantCulture) : "-";
        public string FlowRunTimeText => FlowCount > 0 ? ResultStatisticsCalculator.FormatMilliseconds(FlowRunTimeMilliseconds) : "-";
        public string ResultText => Result ? "PASS" : "FAIL";
    }

    public sealed class ResultStatisticsCombinedPage
    {
        public int TotalCount { get; init; }
        public IReadOnlyList<ResultStatisticsCombinedRecordRow> Rows { get; init; } = [];
    }

    public static class ResultStatisticsLrPairMatcher
    {
        public static IReadOnlyList<ResultStatisticsCombinedRecordRow> MatchAdjacent(IEnumerable<ResultStatisticsRecordRow> source)
        {
            ArgumentNullException.ThrowIfNull(source);
            List<ResultStatisticsRecordRow> rows = source.OrderBy(item => item.Id).ToList();
            var result = new List<ResultStatisticsCombinedRecordRow>();
            for (int index = 1; index < rows.Count; index++)
            {
                ResultStatisticsRecordRow left = rows[index - 1];
                ResultStatisticsRecordRow right = rows[index];
                if (right.PreviousObjectiveId != left.Id
                    || !TryParse(left.SN, out string leftBaseSn, out ResultStatisticsSide leftSide)
                    || !TryParse(right.SN, out string rightBaseSn, out ResultStatisticsSide rightSide)
                    || leftSide != ResultStatisticsSide.Left
                    || rightSide != ResultStatisticsSide.Right
                    || !string.Equals(leftBaseSn, rightBaseSn, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add(new ResultStatisticsCombinedRecordRow
                {
                    SN = leftBaseSn,
                    Left = left,
                    Right = right,
                });
            }

            return result;
        }

        public static bool TryParse(string? sn, out string baseSn, out ResultStatisticsSide side)
        {
            baseSn = string.Empty;
            side = default;
            string value = sn?.Trim() ?? string.Empty;
            int timeSeparator = value.LastIndexOf('_');
            if (timeSeparator <= 0 || value.Length - timeSeparator - 1 != 6)
                return false;
            if (!value.AsSpan(timeSeparator + 1).ContainsAnyExceptInRange('0', '9'))
            {
                int sideSeparator = value.LastIndexOf('_', timeSeparator - 1);
                if (sideSeparator <= 0 || timeSeparator - sideSeparator != 2)
                    return false;

                char sideToken = char.ToUpperInvariant(value[sideSeparator + 1]);
                if (sideToken is not ('L' or 'R'))
                    return false;

                baseSn = value[..sideSeparator].Trim();
                side = sideToken == 'L' ? ResultStatisticsSide.Left : ResultStatisticsSide.Right;
                return baseSn.Length > 0;
            }

            return false;
        }
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
            List<double> flowRunTimes = samples.Where(item => item.FlowRunTimeMilliseconds.HasValue)
                .Select(item => Math.Max(0, item.FlowRunTimeMilliseconds!.Value)).ToList();

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
                FlowRunTimeSampleCount = flowRunTimes.Count,
                AverageFlowRunTimeMilliseconds = flowRunTimes.Count > 0 ? flowRunTimes.Average() : 0,
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

    internal sealed class ResultStatisticsSuggestions
    {
        private readonly string[] _recent;
        private readonly string[] _alphabetical;

        public int Count => _recent.Length;

        public ResultStatisticsSuggestions(IEnumerable<string> suggestions)
        {
            ArgumentNullException.ThrowIfNull(suggestions);
            _recent = suggestions
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _alphabetical = (string[])_recent.Clone();
            Array.Sort(_alphabetical, StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyList<string> Filter(string? text, int limit)
        {
            if (limit <= 0)
                return [];
            string filter = text?.Trim() ?? string.Empty;
            if (filter.Length == 0)
                return _recent.Take(limit).ToArray();

            var matches = new List<string>();
            var substringMatches = new List<string>();
            foreach (string item in _alphabetical)
            {
                if (item.StartsWith(filter, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(item);
                    if (matches.Count == limit)
                        return matches;
                }
                else if (substringMatches.Count < limit && item.Contains(filter, StringComparison.OrdinalIgnoreCase))
                {
                    substringMatches.Add(item);
                }
            }

            matches.AddRange(substringMatches.Take(limit - matches.Count));
            return matches;
        }
    }

    public sealed class ResultStatisticsDataStore
    {
        private const string SerialNumberFilterSql = "\"SN\" LIKE @SN ESCAPE '\\'";

        // Unfinished batches still delimit ownership of the flow rows they already saved.
        private const string RecordFlowBoundariesSql = """
            SELECT "Id", "SN", "ResultId", "CreateTime", "UpdateTime", "TotalResult",
                   "LastModel", "BatchId", "Msg", "IsFinalized",
                   COALESCE(LAG("ResultId") OVER (PARTITION BY TRIM("SN") ORDER BY "Id"), 0) AS "PreviousResultId"
            FROM "ObjectiveTestResultRecord"
            """;

        private static readonly Lazy<ResultStatisticsDataStore> LazyInstance = new(() => new ResultStatisticsDataStore());
        private readonly string? _databasePath;
        private readonly ReadOnlySqliteDatabase? _readOnlyDatabase;
        private readonly object _schemaGate = new();
        private bool _schemaInitialized;

        public static ResultStatisticsDataStore Instance => LazyInstance.Value;

        public ResultStatisticsDataStore(string? databasePath = null) : this(databasePath, readOnly: false) { }

        public ResultStatisticsDataStore(string? databasePath, bool readOnly)
        {
            _databasePath = databasePath;
            if (readOnly)
            {
                _readOnlyDatabase = new ReadOnlySqliteDatabase(databasePath ?? throw new ArgumentNullException(nameof(databasePath)));
                _readOnlyDatabase.RequireColumns("ObjectiveTestResultRecord", "Id", "SN", "ResultId", "BatchId", "CreateTime", "UpdateTime", "TotalResult");
                _readOnlyDatabase.RequireColumns("ARVRReuslt", "Id", "SN", "BatchId", "Model", "CreateTime", "RunTime");
            }
        }

        private string DatabasePath => _databasePath ?? ViewResultManager.SqliteDbPath;

        public void InitializeSchema()
        {
            if (_readOnlyDatabase != null)
                return;
            if (_schemaInitialized)
                return;

            lock (_schemaGate)
            {
                if (_schemaInitialized)
                    return;

                using SqlSugarClient db = CreateClient();
                db.Ado.ExecuteCommand("PRAGMA busy_timeout = 5000;");
                db.Ado.ExecuteCommand("PRAGMA journal_mode = WAL;");
                ResultSqliteSchema.EnsureCreated(db);
                _schemaInitialized = true;
            }
        }

        public ResultStatistics QueryStatistics(ResultStatisticsQuery query, DateTime now)
        {
            ArgumentNullException.ThrowIfNull(query);
            ResultStatisticsCalculator.ValidateRange(query.From, query.ToExclusive);
            InitializeSchema();

            using SqlSugarClient db = CreateClient();
            List<ResultStatisticsSample> samples = ApplyFilters(Query<ObjectiveTestResultRecord>(db), query)
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

        public ResultStatisticsDashboard QueryDashboard(
            ResultStatisticsQuery query,
            ResultStatisticsPeriodMode mode,
            DateTime now)
        {
            ArgumentNullException.ThrowIfNull(query);
            ResultStatisticsCalculator.ValidateRange(query.From, query.ToExclusive);
            InitializeSchema();

            using SqlSugarClient db = CreateClient();
            // Share a read snapshot across the summary and trend without reserving SQLite's writer lock.
            db.Ado.Open();
            db.Ado.Transaction = ((SqliteConnection)db.Ado.Connection).BeginTransaction(deferred: true);
            try
            {
                const string cycleTimeExpression = "CASE WHEN julianday(\"UpdateTime\") >= julianday(\"CreateTime\") THEN (julianday(\"UpdateTime\") - julianday(\"CreateTime\")) * 86400000.0 ELSE 0.0 END";
                string optionalFilter = string.Empty;
                if (!string.IsNullOrWhiteSpace(query.SN))
                    optionalFilter += " AND " + SerialNumberFilterSql;
                if (query.Result.HasValue)
                    optionalFilter += " AND \"TotalResult\" = @Result";
                string resultFilter = $$"""
                    "UpdateTime" >= @From AND "UpdateTime" < @ToExclusive
                    AND ("IsFinalized" = 1 OR "IsFinalized" IS NULL)
                    {{optionalFilter}}
                    """;
                // Wider periods can select most of the database, making a separate SN set more expensive.
                bool restrictHistory = query.ToExclusive - query.From <= TimeSpan.FromDays(1)
                    && (optionalFilter.Length > 0 || HasRecordsOutsideRange(db, query));
                string selectedSerialNumbers = restrictHistory ? $$"""
                    SelectedSerialNumbers AS
                    (
                        SELECT DISTINCT TRIM("SN") AS "NormalizedSerialNumber"
                        FROM "ObjectiveTestResultRecord"
                        WHERE {{resultFilter}}
                    ),
                    """ : string.Empty;
                const string selectedHistoryFilter = """
                    WHERE TRIM("SN") IN (SELECT "NormalizedSerialNumber" FROM SelectedSerialNumbers)
                       OR ("SN" IS NULL AND EXISTS (SELECT 1 FROM SelectedSerialNumbers WHERE "NormalizedSerialNumber" IS NULL))
                    """;
                string summarySql = $$"""
                    WITH {{selectedSerialNumbers}}
                    RecordFlowBoundaries AS
                    (
                        SELECT "SN", "ResultId", "CreateTime", "UpdateTime", "TotalResult", "IsFinalized",
                               COALESCE(LAG("ResultId") OVER (PARTITION BY TRIM("SN") ORDER BY "Id"), 0) AS "PreviousResultId"
                        FROM "ObjectiveTestResultRecord"
                        {{(restrictHistory ? selectedHistoryFilter : string.Empty)}}
                    ),
                    TimedRecords AS
                    (
                        SELECT R.*,
                               (SELECT SUM(F."RunTime")
                                FROM "ARVRReuslt" AS F
                                WHERE F."SN" = R."SN"
                                  AND F."Id" > R."PreviousResultId"
                                  AND F."Id" <= R."ResultId") AS "FlowRunTimeMilliseconds"
                        FROM RecordFlowBoundaries AS R
                        WHERE {{resultFilter}}
                    )
                    SELECT COUNT(*) AS "TotalCount",
                           COALESCE(SUM(CASE WHEN "TotalResult" = 1 THEN 1 ELSE 0 END), 0) AS "PassCount",
                           COALESCE(AVG({{cycleTimeExpression}}), 0.0) AS "AverageCtMilliseconds",
                           COALESCE(MIN({{cycleTimeExpression}}), 0.0) AS "MinimumCtMilliseconds",
                           COALESCE(MAX({{cycleTimeExpression}}), 0.0) AS "MaximumCtMilliseconds",
                           COUNT("FlowRunTimeMilliseconds") AS "FlowRunTimeSampleCount",
                           COALESCE(AVG("FlowRunTimeMilliseconds"), 0.0) AS "AverageFlowRunTimeMilliseconds",
                           COALESCE(SUM(CASE WHEN "UpdateTime" >= @CurrentHour AND "UpdateTime" < @NextHour THEN 1 ELSE 0 END), 0) AS "CurrentHourCount",
                           COALESCE(SUM(CASE WHEN "UpdateTime" >= @Today AND "UpdateTime" < @Tomorrow THEN 1 ELSE 0 END), 0) AS "TodayCount"
                    FROM TimedRecords;
                    """;
                ResultStatisticsAggregateRow aggregate = db.Ado.SqlQuery<ResultStatisticsAggregateRow>(
                    ReadSql(summarySql),
                    CreateDashboardParameters(query, now)).Single();

                IReadOnlyList<ResultStatisticsTrendPoint> trend;
                if (mode == ResultStatisticsPeriodMode.All)
                {
                    const string bucketExpression = "strftime('%Y-%m-01 00:00:00', \"UpdateTime\")";
                    string trendSql = $$"""
                        SELECT {{bucketExpression}} AS "Bucket",
                               COUNT(*) AS "TotalCount",
                               COALESCE(SUM(CASE WHEN "TotalResult" = 1 THEN 1 ELSE 0 END), 0) AS "PassCount",
                               COALESCE(AVG({{cycleTimeExpression}}), 0.0) AS "AverageCtMilliseconds"
                        FROM "ObjectiveTestResultRecord"
                        WHERE {{resultFilter}}
                        GROUP BY {{bucketExpression}}
                        ORDER BY {{bucketExpression}};
                        """;
                    List<ResultStatisticsAggregateBucketRow> buckets = db.Ado.SqlQuery<ResultStatisticsAggregateBucketRow>(
                        ReadSql(trendSql),
                        CreateDashboardParameters(query, now));
                    ResultStatistics bucketStatistics = CreateBucketStatistics(buckets, mode);
                    trend = ResultStatisticsTrendBuilder.BuildMonthly(bucketStatistics);
                }
                else
                {
                    List<ResultStatisticsSample> samples = ApplyFilters(Query<ObjectiveTestResultRecord>(db), query)
                        .OrderBy(item => item.UpdateTime)
                        .OrderBy(item => item.Id)
                        .Select(item => new ResultStatisticsSample
                        {
                            Id = item.Id,
                            StartTime = item.CreateTime,
                            EndTime = item.UpdateTime,
                        })
                        .ToList();
                    trend = ResultStatisticsTrendBuilder.BuildDetails(samples, mode);
                }

                ResultStatistics summary = CreateStatistics(aggregate);
                db.Ado.CommitTran();
                return new ResultStatisticsDashboard { Summary = summary, Trend = trend };
            }
            catch
            {
                db.Ado.RollbackTran();
                throw;
            }
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
            const string sql = $$"""
                WITH PageRecords AS
                (
                    SELECT "Id", "SN", "CreateTime" AS "StartTime", "UpdateTime" AS "EndTime", "TotalResult" AS "Result",
                           "LastModel", "BatchId", "ResultId", "Msg"
                    FROM "ObjectiveTestResultRecord"
                    WHERE "UpdateTime" >= @From
                      AND "UpdateTime" < @ToExclusive
                      AND ("IsFinalized" = 1 OR "IsFinalized" IS NULL)
                      AND (@SN IS NULL OR {{SerialNumberFilterSql}})
                      AND (@Result IS NULL OR "TotalResult" = @Result)
                    ORDER BY "Id" DESC
                    LIMIT @PageSize OFFSET @Skip
                ),
                RankedRecords AS
                (
                    SELECT "Id", COALESCE(LAG("ResultId") OVER SerialNumberHistory, 0) AS "PreviousResultId",
                           COUNT(CASE WHEN "IsFinalized" = 1 OR "IsFinalized" IS NULL THEN 1 END)
                               OVER SerialNumberHistory AS "ExecutionIndex"
                    FROM "ObjectiveTestResultRecord"
                    WHERE TRIM("SN") IN (SELECT TRIM("SN") FROM PageRecords)
                       OR ("SN" IS NULL AND EXISTS (SELECT 1 FROM PageRecords WHERE "SN" IS NULL))
                    WINDOW SerialNumberHistory AS (PARTITION BY TRIM("SN") ORDER BY "Id" ROWS UNBOUNDED PRECEDING)
                )
                SELECT P."Id", R."ExecutionIndex", P."SN", P."StartTime", P."EndTime", P."Result",
                       P."LastModel", P."BatchId", P."ResultId", P."Msg", R."PreviousResultId",
                       CASE WHEN P."ResultId" > R."PreviousResultId" THEN
                           (SELECT COUNT(*)
                            FROM "ARVRReuslt" AS F
                            WHERE F."SN" = P."SN"
                              AND F."Id" > R."PreviousResultId"
                              AND F."Id" <= P."ResultId")
                           ELSE 0 END AS "FlowCount",
                       CASE WHEN P."ResultId" > R."PreviousResultId" THEN
                           COALESCE((SELECT SUM(F."RunTime")
                                     FROM "ARVRReuslt" AS F
                                     WHERE F."SN" = P."SN"
                                       AND F."Id" > R."PreviousResultId"
                                       AND F."Id" <= P."ResultId"), 0)
                           ELSE 0 END AS "FlowRunTimeMilliseconds"
                FROM PageRecords AS P
                JOIN RankedRecords AS R ON R."Id" = P."Id"
                ORDER BY P."Id" DESC;
                """;
            return db.Ado.SqlQuery<ResultStatisticsRecordRow>(
                ReadSql(sql),
                new SugarParameter("@From", query.From),
                new SugarParameter("@ToExclusive", query.ToExclusive),
                new SugarParameter("@SN", (object?)CreateLiteralContainsPattern(query.SN) ?? DBNull.Value),
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
            return ApplyFilters(Query<ObjectiveTestResultRecord>(db), query).Count();
        }

        public ResultStatisticsCombinedDashboard QueryCombinedDashboard(
            ResultStatisticsQuery query,
            ResultStatisticsPeriodMode mode,
            DateTime now)
        {
            ArgumentNullException.ThrowIfNull(query);
            ResultStatisticsCalculator.ValidateRange(query.From, query.ToExclusive);

            List<ResultStatisticsCombinedRecordRow> pairs = QueryCombinedRows(query);
            List<ResultStatisticsSample> samples = pairs.Select(item => new ResultStatisticsSample
            {
                Id = item.Id,
                SN = item.SN,
                Result = item.Result,
                StartTime = item.StartTime,
                EndTime = item.EndTime,
                FlowRunTimeMilliseconds = item.Left.FlowCount > 0 && item.Right.FlowCount > 0 ? item.FlowRunTimeMilliseconds : null,
            }).ToList();
            ResultStatistics summary = ResultStatisticsCalculator.Calculate(samples, query.From, query.ToExclusive, now);
            IReadOnlyList<ResultStatisticsTrendPoint> trend = mode == ResultStatisticsPeriodMode.All
                ? ResultStatisticsTrendBuilder.BuildMonthly(summary)
                : ResultStatisticsTrendBuilder.BuildDetails(samples, mode);
            return new ResultStatisticsCombinedDashboard
            {
                Summary = summary,
                Trend = trend,
                AverageTransitionMilliseconds = pairs.Count > 0 ? pairs.Average(item => item.TransitionMilliseconds) : 0,
            };
        }

        public ResultStatisticsCombinedPage QueryCombinedRecords(ResultStatisticsQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            ResultStatisticsCalculator.ValidateRange(query.From, query.ToExclusive);
            if (query.PageNumber <= 0)
                throw new ArgumentOutOfRangeException(nameof(query), query.PageNumber, "页码必须大于零。");
            if (query.PageSize <= 0)
                throw new ArgumentOutOfRangeException(nameof(query), query.PageSize, "每页数量必须大于零。");

            List<ResultStatisticsCombinedRecordRow> rows = QueryCombinedRows(query)
                .OrderByDescending(item => item.Id)
                .ToList();
            int skip = checked((query.PageNumber - 1) * query.PageSize);
            return new ResultStatisticsCombinedPage
            {
                TotalCount = rows.Count,
                Rows = rows.Skip(skip).Take(query.PageSize).ToList(),
            };
        }

        public IReadOnlyList<ProjectARVRReuslt> QueryFlowDetails(ResultStatisticsCombinedRecordRow record)
        {
            ArgumentNullException.ThrowIfNull(record);
            return QueryFlowDetails(record.Left).Concat(QueryFlowDetails(record.Right)).ToList();
        }

        public IReadOnlyList<ProjectARVRReuslt> QueryFlowDetails(ResultStatisticsRecordRow record)
        {
            ArgumentNullException.ThrowIfNull(record);
            if (string.IsNullOrWhiteSpace(record.SN))
                return [];

            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            return QueryFlowDetails(db, record);
        }

        private List<ProjectARVRReuslt> QueryFlowDetails(SqlSugarClient db, ResultStatisticsRecordRow record)
        {
            string sn = record.SN;
            if (record.ResultId > record.PreviousResultId)
            {
                int previousResultId = Math.Max(0, record.PreviousResultId);
                int resultId = record.ResultId;
                return Query<ProjectARVRReuslt>(db)
                    .Where(item => item.SN == sn && item.Id > previousResultId && item.Id <= resultId)
                    .OrderBy(item => item.Id, OrderByType.Asc)
                    .ToList();
            }

            DateTime startTime = record.StartTime;
            DateTime endTime = record.EndTime >= startTime ? record.EndTime : startTime;
            ISugarQueryable<ProjectARVRReuslt> query = Query<ProjectARVRReuslt>(db)
                .Where(item => item.SN == sn && item.CreateTime >= startTime && item.CreateTime <= endTime);
            if (record.ResultId > 0)
            {
                int resultId = record.ResultId;
                query = query.Where(item => item.Id <= resultId);
            }

            return query.OrderBy(item => item.Id, OrderByType.Asc).ToList();
        }

        private List<ResultStatisticsCombinedRecordRow> QueryCombinedRows(ResultStatisticsQuery query)
        {
            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            bool restrictHistory = query.ToExclusive - query.From <= TimeSpan.FromDays(1) && HasRecordsOutsideRange(db, query);
            // Completion times can be out of ID order. Check the actual predecessor of each possible right side.
            const string candidateRecordsSql = """
                InRangeRecords AS
                (
                    SELECT "Id", "SN"
                    FROM "ObjectiveTestResultRecord"
                    WHERE "UpdateTime" >= @From AND "UpdateTime" < @ToExclusive
                      AND ("IsFinalized" = 1 OR "IsFinalized" IS NULL)
                ),
                CandidateRecords AS
                (
                    SELECT "Id", "SN" FROM InRangeRecords
                    UNION ALL
                    SELECT L."Id", L."SN"
                    FROM InRangeRecords AS R
                    CROSS JOIN
                    (
                        SELECT "Id", "SN", "IsFinalized", "UpdateTime"
                        FROM "ObjectiveTestResultRecord"
                    ) AS L
                    WHERE instr(upper(R."SN"), '_R_') > 0
                      AND L."Id" = (SELECT P."Id"
                                    FROM "ObjectiveTestResultRecord" AS P
                                    WHERE P."Id" < R."Id"
                                    ORDER BY P."Id" DESC LIMIT 1)
                      AND (L."IsFinalized" = 1 OR L."IsFinalized" IS NULL)
                      AND (L."UpdateTime" < @From OR L."UpdateTime" >= @ToExclusive OR L."UpdateTime" IS NULL)
                ),
                """;
            // Keep a superset of possible sides. The matcher still validates the full suffix and global adjacency.
            const string candidateHistoryFilter = """
                WHERE TRIM("SN") IN (SELECT TRIM("SN") FROM CandidateRecords
                                    WHERE instr(upper("SN"), '_L_') > 0 OR instr(upper("SN"), '_R_') > 0)
                """;
            const string flowColumns = """
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
                    """;
            string sql = $$"""
                WITH {{candidateRecordsSql}}
                RecordFlowBoundaries AS
                (
                    {{RecordFlowBoundariesSql}}
                    {{(restrictHistory ? candidateHistoryFilter : string.Empty)}}
                ),
                RankedRecords AS
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
                           "PreviousResultId",
                           ROW_NUMBER() OVER (PARTITION BY TRIM("SN") ORDER BY "Id") AS "ExecutionIndex",
                           COALESCE((SELECT P."Id"
                                     FROM "ObjectiveTestResultRecord" AS P
                                     WHERE P."Id" < B."Id"
                                     ORDER BY P."Id" DESC
                                     LIMIT 1), 0) AS "PreviousObjectiveId"
                    FROM RecordFlowBoundaries AS B
                    WHERE "IsFinalized" = 1 OR "IsFinalized" IS NULL
                )
                SELECT R."Id", R."ExecutionIndex", R."SN", R."StartTime", R."EndTime", R."Result",
                       R."LastModel", R."BatchId", R."ResultId", R."Msg", R."PreviousResultId", R."PreviousObjectiveId",
                       {{flowColumns}}
                FROM RankedRecords AS R
                WHERE R."Id" IN (SELECT "Id" FROM CandidateRecords)
                ORDER BY R."Id";
                """;
            List<ResultStatisticsRecordRow> candidates = db.Ado.SqlQuery<ResultStatisticsRecordRow>(
                ReadSql(sql),
                new SugarParameter("@From", query.From),
                new SugarParameter("@ToExclusive", query.ToExclusive));
            IEnumerable<ResultStatisticsCombinedRecordRow> pairs = ResultStatisticsLrPairMatcher.MatchAdjacent(candidates)
                .Where(item => item.EndTime >= query.From && item.EndTime < query.ToExclusive);
            string? sn = string.IsNullOrWhiteSpace(query.SN) ? null : query.SN.Trim();
            if (sn != null)
            {
                pairs = pairs.Where(item => item.SN.Contains(sn, StringComparison.OrdinalIgnoreCase)
                    || item.Left.SN.Contains(sn, StringComparison.OrdinalIgnoreCase)
                    || item.Right.SN.Contains(sn, StringComparison.OrdinalIgnoreCase));
            }
            if (query.Result.HasValue)
            {
                bool expected = query.Result.Value;
                pairs = pairs.Where(item => item.Result == expected);
            }

            return pairs.ToList();
        }

        private bool HasRecordsOutsideRange(SqlSugarClient db, ResultStatisticsQuery query)
        {
            const string sql = """
                SELECT EXISTS (SELECT 1 FROM "ObjectiveTestResultRecord" WHERE "UpdateTime" < @From)
                    OR EXISTS (SELECT 1 FROM "ObjectiveTestResultRecord" WHERE "UpdateTime" >= @ToExclusive);
                """;
            return db.Ado.GetInt(ReadSql(sql), new SugarParameter("@From", query.From),
                new SugarParameter("@ToExclusive", query.ToExclusive)) != 0;
        }

        public IReadOnlyList<ProjectARVRReuslt> QueryFlowDetailsForExport(ResultStatisticsRecordRow record)
        {
            List<ProjectARVRReuslt> results = QueryFlowDetails(record).ToList();
            if (results.Count == 0)
                return results;

            using SqlSugarClient db = CreateClient();
            ResultJsonPayloadStorage.LoadViewResultJsons(db, results, _readOnlyDatabase);
            return results;
        }

        internal IReadOnlyDictionary<int, IReadOnlyList<ProjectARVRReuslt>> QueryFlowDetailsForBatchExport(
            IReadOnlyList<ResultStatisticsRecordRow> records)
        {
            var idsByRecord = records.ToDictionary(record => record.Id, _ => new List<int>());
            if (idsByRecord.Count == 0)
                return new Dictionary<int, IReadOnlyList<ProjectARVRReuslt>>();

            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            var resultsById = new Dictionary<int, ProjectARVRReuslt>();
            var ranged = new List<ResultStatisticsRecordRow>();
            foreach (ResultStatisticsRecordRow record in records)
            {
                if (string.IsNullOrWhiteSpace(record.SN))
                    continue;
                if (record.ResultId > record.PreviousResultId)
                {
                    ranged.Add(record);
                    continue;
                }
                // Legacy rows without a usable ID range retain the same time-based ownership rules.
                foreach (ProjectARVRReuslt result in QueryFlowDetails(db, record))
                {
                    resultsById.TryAdd(result.Id, result);
                    idsByRecord[record.Id].Add(result.Id);
                }
            }

            // Four parameters per request. Stay below SQLite's legacy 999-variable limit.
            foreach (ResultStatisticsRecordRow[] chunk in ranged.Chunk(200))
            {
                var parameters = new List<SugarParameter>(chunk.Length * 4);
                string[] ranges = new string[chunk.Length];
                for (int index = 0; index < chunk.Length; index++)
                {
                    ResultStatisticsRecordRow row = chunk[index];
                    ranges[index] = $"(@record{index}, @sn{index}, @first{index}, @last{index})";
                    parameters.Add(new($"@record{index}", row.Id));
                    parameters.Add(new($"@sn{index}", row.SN));
                    parameters.Add(new($"@first{index}", Math.Max(0, row.PreviousResultId)));
                    parameters.Add(new($"@last{index}", row.ResultId));
                }
                using var reader = db.Ado.GetDataReader($$"""
                    WITH Requested(RecordId, SN, FirstId, LastId) AS (VALUES {{string.Join(", ", ranges)}})
                    SELECT R.RecordId, F.Id
                    FROM Requested AS R CROSS JOIN "ARVRReuslt" AS F
                    WHERE F.SN = R.SN AND F.Id > R.FirstId AND F.Id <= R.LastId
                    ORDER BY R.RecordId, F.Id;
                    """, parameters.ToArray());
                while (reader.Read())
                    idsByRecord[reader.GetInt32(0)].Add(reader.GetInt32(1));
            }

            foreach (int[] ids in idsByRecord.Values.SelectMany(ids => ids).Distinct().Where(id => !resultsById.ContainsKey(id)).Chunk(500))
            {
                foreach (ProjectARVRReuslt result in Query<ProjectARVRReuslt>(db).Where(item => ids.Contains(item.Id)).ToList())
                    resultsById.Add(result.Id, result);
            }
            ResultJsonPayloadStorage.LoadViewResultJsons(db, resultsById.Values, _readOnlyDatabase);
            return idsByRecord.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<ProjectARVRReuslt>)pair.Value
                .Where(resultsById.ContainsKey).Select(id => resultsById[id]).ToArray());
        }

        public IReadOnlyList<FlowExecutionRecordRow> QueryFlowExecutions(FlowExecutionQuery query)
        {
            ValidateFlowExecutionQuery(query);
            InitializeSchema();

            using SqlSugarClient db = CreateClient();
            return ApplyFlowFilters(Query<ProjectARVRReuslt>(db), query)
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
            return ApplyFlowFilters(Query<ProjectARVRReuslt>(db), query).Count();
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

        internal IReadOnlyList<string> QuerySnSuggestions()
        {
            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            const string sql = """
                SELECT TRIM("SN") AS "SN"
                FROM "ObjectiveTestResultRecord"
                WHERE TRIM("SN") <> '' AND ("IsFinalized" = 1 OR "IsFinalized" IS NULL)
                GROUP BY TRIM("SN")
                ORDER BY MAX("UpdateTime") DESC;
                """;
            return db.Ado.SqlQuery<string>(ReadSql(sql));
        }

        public IReadOnlyList<ResultStatisticsSnSummary> QuerySnSummaries()
        {
            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            List<ResultStatisticsSnAggregate> aggregates = Query<ObjectiveTestResultRecord>(db)
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
            ObjectiveTestResultRecord? record = Query<ObjectiveTestResultRecord>(db).Where(item => item.Id == id).First();
            if (record != null)
                record.ObjectiveTestResultJson = ReadObjectivePayload(db, record.Id) ?? string.Empty;
            return record;
        }

        public IReadOnlyList<ObjectiveTestResultRecord> GetRecords(IEnumerable<int> ids)
        {
            ArgumentNullException.ThrowIfNull(ids);
            int[] recordIds = ids.Where(id => id > 0).Distinct().ToArray();
            if (recordIds.Length == 0)
                return [];

            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            List<ObjectiveTestResultRecord> records = Query<ObjectiveTestResultRecord>(db)
                .Where(item => recordIds.Contains(item.Id))
                .ToList();
            ResultJsonPayloadStorage.LoadObjectiveTestResultJsons(db, records, _readOnlyDatabase);
            foreach (ObjectiveTestResultRecord record in records.Where(item => item.ObjectiveTestResultJson == null))
                record.ObjectiveTestResultJson = string.Empty;
            return records;
        }

        public string? LoadViewResultJson(ProjectARVRReuslt result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (result.ViewResultJson != null || result.Id <= 0)
                return result.ViewResultJson;

            InitializeSchema();
            using SqlSugarClient db = CreateClient();
            result.ViewResultJson = ReadFlowPayload(db, result.Id) ?? string.Empty;
            return result.ViewResultJson;
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
            if (CreateLiteralContainsPattern(query.SN) is string snPattern)
                queryResult = queryResult.Where(SerialNumberFilterSql, new { SN = snPattern });
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
            if (CreateLiteralContainsPattern(query.Model) is string modelPattern)
                queryResult = queryResult.Where("\"Model\" LIKE @Model ESCAPE '\\'", new { Model = modelPattern });
            if (query.Result.HasValue)
            {
                bool expectedResult = query.Result.Value;
                queryResult = queryResult.Where(item => item.Result == expectedResult);
            }

            return queryResult;
        }

        private static string? CreateLiteralContainsPattern(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string literal = value.Trim().Replace("\\", "\\\\", StringComparison.Ordinal)
                .Replace("%", "\\%", StringComparison.Ordinal)
                .Replace("_", "\\_", StringComparison.Ordinal);
            return "%" + literal + "%";
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

        private static SugarParameter[] CreateDashboardParameters(ResultStatisticsQuery query, DateTime now)
        {
            DateTime currentHour = new(now.Year, now.Month, now.Day, now.Hour, 0, 0, now.Kind);
            return
            [
                new SugarParameter("@From", query.From),
                new SugarParameter("@ToExclusive", query.ToExclusive),
                new SugarParameter("@SN", (object?)CreateLiteralContainsPattern(query.SN) ?? DBNull.Value),
                new SugarParameter("@Result", query.Result.HasValue ? query.Result.Value : DBNull.Value),
                new SugarParameter("@CurrentHour", currentHour),
                new SugarParameter("@NextHour", currentHour.AddHours(1)),
                new SugarParameter("@Today", now.Date),
                new SugarParameter("@Tomorrow", now.Date.AddDays(1)),
            ];
        }

        private static ResultStatistics CreateStatistics(ResultStatisticsAggregateRow aggregate)
        {
            int failCount = aggregate.TotalCount - aggregate.PassCount;
            return new ResultStatistics
            {
                TotalCount = aggregate.TotalCount,
                PassCount = aggregate.PassCount,
                FailCount = failCount,
                PassRate = aggregate.TotalCount > 0 ? aggregate.PassCount / (double)aggregate.TotalCount : 0,
                FailRate = aggregate.TotalCount > 0 ? failCount / (double)aggregate.TotalCount : 0,
                AverageCtMilliseconds = aggregate.AverageCtMilliseconds,
                MinimumCtMilliseconds = aggregate.MinimumCtMilliseconds,
                MaximumCtMilliseconds = aggregate.MaximumCtMilliseconds,
                FlowRunTimeSampleCount = aggregate.FlowRunTimeSampleCount,
                AverageFlowRunTimeMilliseconds = aggregate.AverageFlowRunTimeMilliseconds,
                CurrentHourCount = aggregate.CurrentHourCount,
                TodayCount = aggregate.TodayCount,
            };
        }

        private static ResultStatistics CreateBucketStatistics(
            IEnumerable<ResultStatisticsAggregateBucketRow> buckets,
            ResultStatisticsPeriodMode mode)
        {
            List<ResultStatisticsAggregateBucketRow> rows = buckets.ToList();
            if (mode == ResultStatisticsPeriodMode.Day)
            {
                return new ResultStatistics
                {
                    HourlyRows = rows.Select(item => new ResultStatisticsHourlyRow
                    {
                        Hour = ParseBucket(item.Bucket),
                        TotalCount = item.TotalCount,
                        PassCount = item.PassCount,
                        FailCount = item.TotalCount - item.PassCount,
                        PassRate = item.TotalCount > 0 ? item.PassCount / (double)item.TotalCount : 0,
                        FailRate = item.TotalCount > 0 ? (item.TotalCount - item.PassCount) / (double)item.TotalCount : 0,
                        AverageCtMilliseconds = item.AverageCtMilliseconds,
                    }).ToList(),
                };
            }

            return new ResultStatistics
            {
                DailyRows = rows.Select(item => new ResultStatisticsDailyRow
                {
                    Date = ParseBucket(item.Bucket).Date,
                    TotalCount = item.TotalCount,
                    PassCount = item.PassCount,
                    FailCount = item.TotalCount - item.PassCount,
                    PassRate = item.TotalCount > 0 ? item.PassCount / (double)item.TotalCount : 0,
                    FailRate = item.TotalCount > 0 ? (item.TotalCount - item.PassCount) / (double)item.TotalCount : 0,
                    AverageCtMilliseconds = item.AverageCtMilliseconds,
                }).ToList(),
            };
        }

        private static DateTime ParseBucket(string value)
        {
            return DateTime.ParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private SqlSugarClient CreateClient()
        {
            if (_readOnlyDatabase != null)
                return _readOnlyDatabase.OpenClient();
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={DatabasePath};Default Timeout=5",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
        }

        private ISugarQueryable<T> Query<T>(SqlSugarClient db) where T : class, new()
            => _readOnlyDatabase?.Query<T>(db) ?? db.Queryable<T>();

        private string ReadSql(string sql) => _readOnlyDatabase == null ? sql
            : sql.Replace("FROM \"ObjectiveTestResultRecord\"", $"FROM ({_readOnlyDatabase.SelectSql<ObjectiveTestResultRecord>()})");

        private string? ReadObjectivePayload(SqlSugarClient db, int id) => _readOnlyDatabase == null
            ? ResultJsonPayloadStorage.LoadObjectiveTestResultJson(db, id)
            : _readOnlyDatabase.ReadPayload("ObjectiveTestResultRecord", "Id", id, "ObjectiveTestResultJson", "ObjectiveTestResultJsonGzip");

        private string? ReadFlowPayload(SqlSugarClient db, int id) => _readOnlyDatabase == null
            ? ResultJsonPayloadStorage.LoadViewResultJson(db, id)
            : _readOnlyDatabase.ReadPayload("ARVRReuslt", "Id", id, "ViewResultJson", "ViewResultJsonGzip");

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

        private sealed class ResultStatisticsAggregateRow
        {
            public int TotalCount { get; set; }
            public int PassCount { get; set; }
            public double AverageCtMilliseconds { get; set; }
            public double MinimumCtMilliseconds { get; set; }
            public double MaximumCtMilliseconds { get; set; }
            public int FlowRunTimeSampleCount { get; set; }
            public double AverageFlowRunTimeMilliseconds { get; set; }
            public int CurrentHourCount { get; set; }
            public int TodayCount { get; set; }
        }

        private sealed class ResultStatisticsAggregateBucketRow
        {
            public string Bucket { get; set; } = string.Empty;
            public int TotalCount { get; set; }
            public int PassCount { get; set; }
            public double AverageCtMilliseconds { get; set; }
        }
    }
}
