using Microsoft.Data.Sqlite;
using SqlSugar;
using System.Data;
using System.IO;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class ResultStatisticsTests
{
    [Theory]
    [InlineData(ResultStatisticsPeriodMode.Day, "2026-08-09", "2026-08-10", "2026/08/09")]
    [InlineData(ResultStatisticsPeriodMode.Week, "2026-08-03", "2026-08-10", "2026/08/03 - 08/09")]
    [InlineData(ResultStatisticsPeriodMode.Month, "2026-08-01", "2026-09-01", "2026/08")]
    public void PeriodModesCreateStableCalendarRanges(
        ResultStatisticsPeriodMode mode,
        string expectedFrom,
        string expectedToExclusive,
        string expectedDisplayText)
    {
        ResultStatisticsPeriodRange range = ResultStatisticsPeriod.GetRange(mode, new DateTime(2026, 8, 9, 16, 30, 0));

        Assert.Equal(DateTime.Parse(expectedFrom), range.From);
        Assert.Equal(DateTime.Parse(expectedToExclusive), range.ToExclusive);
        Assert.Equal(expectedDisplayText, range.ToDisplayText(mode));
    }

    [Theory]
    [InlineData(ResultStatisticsPeriodMode.Day, 1, "2026-08-10")]
    [InlineData(ResultStatisticsPeriodMode.Week, -1, "2026-08-02")]
    [InlineData(ResultStatisticsPeriodMode.Month, 1, "2026-09-09")]
    public void PeriodNavigationUsesTheSelectedCalendarUnit(ResultStatisticsPeriodMode mode, int offset, string expected)
    {
        DateTime shifted = ResultStatisticsPeriod.ShiftAnchor(mode, new DateTime(2026, 8, 9), offset);

        Assert.Equal(DateTime.Parse(expected), shifted);
    }

    [Fact]
    public void ObjectiveRecordUsesTheRealSessionStartTime()
    {
        DateTime sessionStart = DateTime.Now.AddSeconds(-5);
        var result = new ProjectARVRReuslt
        {
            Id = 10,
            BatchId = 20,
            SN = "SN-SESSION",
            Model = "W255",
            Result = true,
        };
        var objectiveResult = new ObjectiveTestResult
        {
            SessionStartTime = sessionStart,
            TotalResult = true,
        };

        ObjectiveTestResultRecord record = ObjectiveTestResultRecord.Create(result, objectiveResult);

        Assert.Equal(sessionStart, record.CreateTime);
        Assert.True(record.UpdateTime >= sessionStart);
        Assert.False(record.IsFinalized);
        Assert.DoesNotContain(nameof(ObjectiveTestResult.SessionStartTime), Newtonsoft.Json.JsonConvert.SerializeObject(objectiveResult));
    }

    [Fact]
    public void ProductionBucketsUseTheCompletionTime()
    {
        DateTime from = new(2026, 8, 9);
        ResultStatisticsSample sample = CreateSample(
            1,
            "SN-CROSS-HOUR",
            true,
            new DateTime(2026, 8, 9, 9, 59, 59, 500),
            1_000);

        ResultStatistics statistics = ResultStatisticsCalculator.Calculate(
            [sample],
            from,
            from.AddDays(1),
            new DateTime(2026, 8, 9, 10, 30, 0));

        ResultStatisticsHourlyRow row = Assert.Single(statistics.HourlyRows);
        Assert.Equal("2026/08/09 10:00", row.HourText);
        Assert.Equal(1, statistics.CurrentHourCount);
    }

    [Fact]
    public void CycleTimeGroupsExposeTheWholeExecutionResult()
    {
        CycleTimeResultSample[] samples =
        [
            new() { Id = 1, SN = "SN-A", TestType = 0, Result = true, RunTime = 100 },
            new() { Id = 2, SN = "SN-A", TestType = 1, Result = false, RunTime = 200 },
            new() { Id = 3, SN = "SN-A", TestType = 0, Result = true, RunTime = 300 },
        ];

        IReadOnlyList<CycleTimeGroup> groups = CycleTimeCalculator.Calculate(samples);

        Assert.Equal(2, groups.Count);
        Assert.False(groups[1].Result);
        Assert.Equal("FAIL", groups[1].ResultText);
        Assert.True(groups[0].Result);
    }

    [Fact]
    public void CalculatorBuildsOverallHourlyAndDailyStatistics()
    {
        DateTime now = new(2026, 8, 9, 10, 30, 0);
        DateTime from = new(2026, 8, 8);
        DateTime toExclusive = new(2026, 8, 10);
        ResultStatisticsSample[] samples =
        [
            CreateSample(1, "SN-1", true, new DateTime(2026, 8, 8, 23, 0, 0), 1_000),
            CreateSample(2, "SN-2", false, new DateTime(2026, 8, 9, 9, 0, 0), 2_000),
            CreateSample(3, "SN-3", true, new DateTime(2026, 8, 9, 10, 0, 0), 3_000),
            CreateSample(4, "SN-4", false, new DateTime(2026, 8, 9, 10, 15, 0), 4_000),
        ];

        ResultStatistics statistics = ResultStatisticsCalculator.Calculate(samples, from, toExclusive, now);

        Assert.Equal(4, statistics.TotalCount);
        Assert.Equal(2, statistics.PassCount);
        Assert.Equal(2, statistics.FailCount);
        Assert.Equal(0.5, statistics.PassRate, 6);
        Assert.Equal(0.5, statistics.FailRate, 6);
        Assert.Equal(2_500, statistics.AverageCtMilliseconds);
        Assert.Equal(1_000, statistics.MinimumCtMilliseconds);
        Assert.Equal(4_000, statistics.MaximumCtMilliseconds);
        Assert.Equal(2, statistics.CurrentHourCount);
        Assert.Equal(3, statistics.TodayCount);

        Assert.Equal(3, statistics.HourlyRows.Count);
        ResultStatisticsHourlyRow currentHour = statistics.HourlyRows[0];
        Assert.Equal("2026/08/09 10:00", currentHour.HourText);
        Assert.Equal(2, currentHour.TotalCount);
        Assert.Equal(1, currentHour.PassCount);
        Assert.Equal(1, currentHour.FailCount);
        Assert.Equal("50.00%", currentHour.PassRateText);
        Assert.Equal("3.500 s", currentHour.AverageCtText);

        Assert.Equal(2, statistics.DailyRows.Count);
        Assert.Equal("2026/08/09", statistics.DailyRows[0].DateText);
        Assert.Equal(3, statistics.DailyRows[0].TotalCount);
    }

    [Fact]
    public void CalculatorFiltersRangeAndClampsNegativeCycleTime()
    {
        DateTime from = new(2026, 8, 9);
        DateTime toExclusive = from.AddDays(1);
        ResultStatisticsSample[] samples =
        [
            new ResultStatisticsSample
            {
                Id = 1,
                SN = "SN-1",
                Result = true,
                StartTime = from.AddHours(1),
                EndTime = from,
            },
            CreateSample(2, "outside", false, toExclusive, 5_000),
        ];

        ResultStatistics statistics = ResultStatisticsCalculator.Calculate(samples, from, toExclusive, from.AddHours(2));

        Assert.Equal(1, statistics.TotalCount);
        Assert.Equal(0, statistics.AverageCtMilliseconds);
        Assert.Equal("0.000 s", statistics.AverageCtText);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ResultStatisticsCalculator.Calculate(samples, toExclusive, from, from));
    }

    [Fact]
    public void SnSummariesGroupRepeatedSerialNumbersAndIgnoreEmptyValues()
    {
        DateTime start = new(2026, 8, 9, 8, 0, 0);
        ResultStatisticsSample[] samples =
        [
            CreateSample(1, "SN-A", true, start, 1_000),
            CreateSample(2, " SN-A ", false, start.AddHours(1), 2_000),
            CreateSample(3, "SN-B", true, start.AddHours(2), 3_000),
            CreateSample(4, "  ", false, start.AddHours(3), 4_000),
        ];

        IReadOnlyList<ResultStatisticsSnSummary> summaries = ResultStatisticsCalculator.CalculateSnSummaries(samples);

        Assert.Equal(2, summaries.Count);
        ResultStatisticsSnSummary snB = summaries[0];
        Assert.Equal("SN-B", snB.SN);
        Assert.Equal(1, snB.TotalCount);
        ResultStatisticsSnSummary snA = summaries[1];
        Assert.Equal("SN-A", snA.SN);
        Assert.Equal(2, snA.TotalCount);
        Assert.Equal(1, snA.PassCount);
        Assert.Equal(1, snA.FailCount);
        Assert.Equal("50.00%", snA.PassRateText);
        Assert.Equal(start, snA.FirstTime);
        Assert.Equal(start.AddHours(1).AddMilliseconds(2_000), snA.LastTime);
    }

    [Fact]
    public void SnSuggestionFilterPrioritizesPrefixMatchesAndCapsTheDropdown()
    {
        string[] suggestions = ["ZZ-AB", "AB-20", "ab-10", "AB-30", "AB-20", "unrelated"];

        IReadOnlyList<string> matches = ResultStatisticsSuggestionFilter.Filter(suggestions, "ab", 3);

        Assert.Equal(["ab-10", "AB-20", "AB-30"], matches);
        Assert.Equal(2, ResultStatisticsSuggestionFilter.Filter(suggestions, string.Empty, 2).Count);
        Assert.Empty(ResultStatisticsSuggestionFilter.Filter(suggestions, "ab", 0));
    }

    [Fact]
    public void DataStoreCreatesIndexesAndSeparatesStatisticsFromPagedRecords()
    {
        using var database = new TemporaryResultDatabase();
        ResultStatisticsDataStore store = new(database.Path);
        store.InitializeSchema();
        database.Insert(
            CreateRecord("SN-A", true, new DateTime(2026, 8, 9, 8, 0, 0), 1_000, "first"),
            CreateRecord("SN-A", false, new DateTime(2026, 8, 9, 9, 0, 0), 2_000, "second"),
            CreateRecord("SN-B", true, new DateTime(2026, 8, 9, 10, 0, 0), 3_000, "third"),
            CreateRecord("SN-A", true, new DateTime(2026, 8, 10, 8, 0, 0), 4_000, "outside"),
            CreateRecord("SN-A", false, new DateTime(2026, 8, 9, 11, 0, 0), 5_000, "in-progress", false));

        var query = new ResultStatisticsQuery
        {
            From = new DateTime(2026, 8, 9),
            ToExclusive = new DateTime(2026, 8, 10),
            SN = "SN-A",
            PageSize = 1,
        };

        ResultStatistics statistics = store.QueryStatistics(query, new DateTime(2026, 8, 9, 9, 30, 0));
        IReadOnlyList<ResultStatisticsRecordRow> records = store.QueryRecords(query);

        Assert.Equal(2, statistics.TotalCount);
        Assert.Equal(1, statistics.PassCount);
        Assert.Equal(1, statistics.FailCount);
        Assert.Equal(2, store.QueryRecordCount(query));
        ResultStatisticsRecordRow record = Assert.Single(records);
        Assert.Equal("second", record.LastModel);
        Assert.Equal(2, record.ExecutionIndex);
        Assert.Equal("第 2 次", record.ExecutionText);
        Assert.Equal("FAIL", record.ResultText);
        Assert.Equal("2.000 s", record.CycleTimeText);
        Assert.NotNull(store.GetRecord(record.Id));
        ObjectiveTestResultRecord selectedRecord = Assert.Single(store.GetRecords([record.Id, record.Id, -1]));
        Assert.Equal(record.Id, selectedRecord.Id);

        ResultStatistics passOnly = store.QueryStatistics(new ResultStatisticsQuery
        {
            From = query.From,
            ToExclusive = query.ToExclusive,
            SN = query.SN,
            Result = true,
            PageSize = 1,
        }, new DateTime(2026, 8, 9, 9, 30, 0));
        Assert.Equal(1, passOnly.TotalCount);
        Assert.Equal(1, passOnly.PassCount);

        IReadOnlyList<ResultStatisticsRecordRow> passRecords = store.QueryRecords(new ResultStatisticsQuery
        {
            From = query.From,
            ToExclusive = query.ToExclusive,
            SN = query.SN,
            Result = true,
            PageSize = 10,
        });
        ResultStatisticsRecordRow passRecord = Assert.Single(passRecords);
        Assert.Equal("first", passRecord.LastModel);
        Assert.Equal("第 1 次", passRecord.ExecutionText);

        IReadOnlyList<ResultStatisticsRecordRow> secondPage = store.QueryRecords(new ResultStatisticsQuery
        {
            From = query.From,
            ToExclusive = query.ToExclusive,
            SN = query.SN,
            PageNumber = 2,
            PageSize = 1,
        });
        ResultStatisticsRecordRow firstRecord = Assert.Single(secondPage);
        Assert.Equal("first", firstRecord.LastModel);
        Assert.Equal("第 1 次", firstRecord.ExecutionText);

        HashSet<string> indexNames = database.QueryIndexNames();
        Assert.Contains("IX_ObjectiveTestResultRecord_SN", indexNames);
        Assert.Contains("IX_ObjectiveTestResultRecord_CreateTime", indexNames);
        Assert.Contains("IX_ObjectiveTestResultRecord_UpdateTime", indexNames);
        Assert.Contains("IX_ObjectiveTestResultRecord_TotalResult", indexNames);
        Assert.Contains("IX_ObjectiveTestResultRecord_IsFinalized_UpdateTime", indexNames);
        Assert.Contains("IX_ARVRReuslt_CreateTime", indexNames);
        Assert.Contains("IX_ARVRReuslt_SN_CreateTime", indexNames);
    }

    [Fact]
    public void DataStoreLoadsFlowCtDetailsForTheSelectedBatchRecord()
    {
        using var database = new TemporaryResultDatabase();
        ResultStatisticsDataStore store = new(database.Path);
        store.InitializeSchema();
        DateTime start = new(2026, 8, 9, 8, 0, 0);
        database.InsertFlow(new ProjectARVRReuslt { SN = "SN-A", Model = "before", CreateTime = start.AddSeconds(-1), RunTime = 50 });
        ProjectARVRReuslt first = database.InsertFlow(new ProjectARVRReuslt { SN = "SN-A", Model = "first", CreateTime = start.AddSeconds(1), RunTime = 100 });
        ProjectARVRReuslt last = database.InsertFlow(new ProjectARVRReuslt { SN = "SN-A", Model = "last", CreateTime = start.AddSeconds(2), RunTime = 200 });
        database.InsertFlow(new ProjectARVRReuslt { SN = "SN-A", Model = "next", CreateTime = start.AddSeconds(2), RunTime = 300 });
        database.InsertFlow(new ProjectARVRReuslt { SN = "SN-B", Model = "other", CreateTime = start.AddSeconds(1), RunTime = 400 });

        IReadOnlyList<ProjectARVRReuslt> details = store.QueryFlowDetails(new ResultStatisticsRecordRow
        {
            SN = "SN-A",
            StartTime = start,
            EndTime = start.AddSeconds(3),
            ResultId = last.Id,
        });

        Assert.Equal([first.Id, last.Id], details.Select(item => item.Id));
        Assert.Equal(300, details.Sum(item => item.RunTime));
    }

    [Fact]
    public void DataStoreBuildsSnSuggestionsFromAllObjectiveRecords()
    {
        using var database = new TemporaryResultDatabase();
        ResultStatisticsDataStore store = new(database.Path);
        store.InitializeSchema();
        DateTime start = new(2026, 8, 9, 8, 0, 0);
        database.Insert(
            CreateRecord("SN-A", true, start, 1_000, "first"),
            CreateRecord(" SN-A ", false, start.AddHours(1), 2_000, "second"),
            CreateRecord("SN-B", true, start.AddHours(2), 3_000, "third"),
            CreateRecord("SN-C", false, start.AddHours(3), 4_000, "in-progress", false),
            CreateRecord("   ", true, start.AddHours(4), 5_000, "blank"));

        IReadOnlyList<ResultStatisticsSnSummary> summaries = store.QuerySnSummaries();

        Assert.Equal(2, summaries.Count);
        Assert.Equal("SN-B", summaries[0].SN);
        Assert.Equal("SN-A", summaries[1].SN);
        Assert.Equal(2, summaries[1].TotalCount);
        Assert.Equal(1, summaries[1].PassCount);
        Assert.Equal(1, summaries[1].FailCount);
    }

    [Fact]
    public void DataStoreUpgradesLegacyObjectiveTableWithFinalizationColumn()
    {
        using var database = new TemporaryResultDatabase();
        new ResultStatisticsDataStore(database.Path).InitializeSchema();
        database.Insert(CreateRecord("SN-LEGACY", true, new DateTime(2026, 8, 8, 8, 0, 0), 1_000, "legacy"));
        database.DropFinalizedColumnForLegacySchema();

        Assert.DoesNotContain(nameof(ObjectiveTestResultRecord.IsFinalized), database.QueryColumnNames());

        new ResultStatisticsDataStore(database.Path).InitializeSchema();

        Assert.Contains(nameof(ObjectiveTestResultRecord.IsFinalized), database.QueryColumnNames());
        Assert.True(database.FirstFinalizationValueIsNull());
    }

    private static ResultStatisticsSample CreateSample(int id, string sn, bool result, DateTime start, double milliseconds)
    {
        return new ResultStatisticsSample
        {
            Id = id,
            SN = sn,
            Result = result,
            StartTime = start,
            EndTime = start.AddMilliseconds(milliseconds),
        };
    }

    private static ObjectiveTestResultRecord CreateRecord(
        string sn,
        bool result,
        DateTime start,
        int milliseconds,
        string model,
        bool? isFinalized = null)
    {
        return new ObjectiveTestResultRecord
        {
            SN = sn,
            LastModel = model,
            LastCode = model,
            LastFlowStatus = "Completed",
            Msg = string.Empty,
            LastResult = result,
            TotalResult = result,
            IsFinalized = isFinalized,
            CreateTime = start,
            UpdateTime = start.AddMilliseconds(milliseconds),
            ObjectiveTestResultJson = $"{{\"model\":\"{model}\"}}",
        };
    }

    private sealed class TemporaryResultDatabase : IDisposable
    {
        private readonly string _directory;

        public TemporaryResultDatabase()
        {
            _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ProjectARVRPro.ResultStatistics.{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "results.db");
        }

        public string Path { get; }

        public void Insert(params ObjectiveTestResultRecord[] records)
        {
            using SqlSugarClient db = CreateClient();
            db.Insertable(records).ExecuteCommand();
        }

        public ProjectARVRReuslt InsertFlow(ProjectARVRReuslt result)
        {
            using SqlSugarClient db = CreateClient();
            result.Id = db.Insertable(result).ExecuteReturnIdentity();
            return result;
        }

        public HashSet<string> QueryIndexNames()
        {
            using SqlSugarClient db = CreateClient();
            DataTable table = db.Ado.GetDataTable("SELECT name FROM sqlite_master WHERE type = 'index';");
            return table.Rows.Cast<DataRow>()
                .Select(row => Convert.ToString(row["name"]) ?? string.Empty)
                .ToHashSet(StringComparer.Ordinal);
        }

        public HashSet<string> QueryColumnNames()
        {
            using SqlSugarClient db = CreateClient();
            DataTable table = db.Ado.GetDataTable("PRAGMA table_info('ObjectiveTestResultRecord');");
            return table.Rows.Cast<DataRow>()
                .Select(row => Convert.ToString(row["name"]) ?? string.Empty)
                .ToHashSet(StringComparer.Ordinal);
        }

        public void DropFinalizedColumnForLegacySchema()
        {
            using SqlSugarClient db = CreateClient();
            db.Ado.ExecuteCommand("DROP INDEX IF EXISTS \"IX_ObjectiveTestResultRecord_IsFinalized_UpdateTime\";");
            db.Ado.ExecuteCommand("ALTER TABLE \"ObjectiveTestResultRecord\" DROP COLUMN \"IsFinalized\";");
        }

        public bool FirstFinalizationValueIsNull()
        {
            using SqlSugarClient db = CreateClient();
            DataTable table = db.Ado.GetDataTable(
                "SELECT \"IsFinalized\" FROM \"ObjectiveTestResultRecord\" ORDER BY \"Id\" LIMIT 1;");
            return table.Rows.Count == 1 && table.Rows[0].IsNull("IsFinalized");
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(Path))
                File.Delete(Path);
            if (Directory.Exists(_directory))
                Directory.Delete(_directory);
        }

        private SqlSugarClient CreateClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={Path}",
                DbType = SqlSugar.DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
        }
    }
}
