using ColorVision.Engine.FlowProcessing;
using Microsoft.Data.Sqlite;
using ProjectKB;
using SqlSugar;
using System.IO;
using Xunit;

namespace ProjectKB.Tests;

public class KBProductionStatisticsTests
{
    [Fact]
    public void CalculateSeparatesProductNgFromExecutionFailuresAndUsesCompletedCt()
    {
        DateTime day = new(2026, 8, 5);
        var sessions = new[]
        {
            new KBProductionSession
            {
                Id = 11,
                StartTime = day.AddHours(8),
                Model = "AOI TEMP",
                Stage = "F100",
                LineNo = "L1",
                TargetProduction = 100
            }
        };
        var results = new[]
        {
            CreateResult(1, 11, day.AddHours(10).AddMinutes(5), FlowStatus.Completed, true, 1_000),
            CreateResult(2, 11, day.AddHours(10).AddMinutes(20), FlowStatus.Completed, false, 3_000),
            CreateResult(3, 11, day.AddHours(10).AddMinutes(30), FlowStatus.Failed, false, 500),
            CreateResult(4, 11, day.AddHours(11), FlowStatus.Completed, true, 2_000)
        };

        KBProductionStatistics statistics = KBProductionStatisticsCalculator.Calculate(
            results,
            sessions,
            day,
            day.AddDays(1),
            day.AddHours(10).AddMinutes(45),
            KBProductionPeriodMode.Day);

        Assert.Equal(4, statistics.TotalRuns);
        Assert.Equal(3, statistics.ProductionCount);
        Assert.Equal(2, statistics.GoodCount);
        Assert.Equal(1, statistics.DefectiveCount);
        Assert.Equal(1, statistics.ExecutionFailureCount);
        Assert.Equal(2_000, statistics.AverageCtMilliseconds);
        Assert.Equal(1_000, statistics.MinimumCtMilliseconds);
        Assert.Equal(3_000, statistics.MaximumCtMilliseconds);
        Assert.Equal(2, statistics.CurrentHourProduction);
        Assert.Equal(3, statistics.TodayProduction);

        KBHourlyProductionRow tenOClock = Assert.Single(statistics.HourlyRows, item => item.Hour.Hour == 10);
        Assert.Equal(2, tenOClock.ProductionCount);
        Assert.Equal(1, tenOClock.DefectiveCount);
        Assert.Equal(1, tenOClock.ExecutionFailureCount);
        Assert.Equal(2_000, tenOClock.AverageCtMilliseconds);

        KBDailyProductionRow daily = Assert.Single(statistics.DailyRows);
        Assert.Equal(100, daily.TargetProduction);
        Assert.Equal(3, daily.ProductionCount);
        Assert.Equal(0.03, daily.AchievementRate, 10);
    }

    [Fact]
    public void CalculateKeepsLegacyRowsInAnUnlinkedSession()
    {
        DateTime now = new(2026, 8, 5, 12, 0, 0);
        KBItemMaster legacy = CreateResult(1, null, now.AddMinutes(-1), FlowStatus.Completed, true, 1_500);

        KBProductionStatistics statistics = KBProductionStatisticsCalculator.Calculate(
            [legacy],
            [],
            now.Date,
            now.Date.AddDays(1),
            now,
            KBProductionPeriodMode.Day);

        KBProductionSessionRow session = Assert.Single(statistics.SessionRows);
        Assert.Equal(0, session.SessionId);
        Assert.Equal("未关联", session.SessionText);
        Assert.Equal(1, session.ProductionCount);
    }

    [Fact]
    public void DataStoreReusesMatchingSessionAndStartsANewOneWhenProductionInfoChanges()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"ProjectKB-{Guid.NewGuid():N}.db");
        try
        {
            var store = new KBProductionDataStore(databasePath);
            var summary = new Summary
            {
                Stage = "F100",
                LineNO = "L1",
                WorkerNO = "W1",
                Opno = "OP1",
                MachineNO = "M1",
                TargetProduction = 120
            };
            DateTime start = new(2026, 8, 5, 8, 0, 0);

            int firstId = store.EnsureCurrentSession(summary, "AOI TEMP", start);
            int reusedId = store.EnsureCurrentSession(summary, "AOI TEMP", start.AddMinutes(1));
            summary.WorkerNO = "W2";
            int secondId = store.EnsureCurrentSession(summary, "AOI TEMP", start.AddMinutes(2));
            int secondReusedId = store.EnsureCurrentSession(summary, "AOI TEMP", start.AddMinutes(3));

            Assert.True(firstId > 0);
            Assert.Equal(firstId, reusedId);
            Assert.NotEqual(firstId, secondId);
            Assert.Equal(secondId, secondReusedId);

            KBProductionStatistics statistics = store.QueryStatistics(new KBProductionQuery
            {
                From = start.Date,
                ToExclusive = start.Date.AddDays(1)
            }, start.AddHours(1));
            Assert.Collection(
                statistics.SessionRows,
                current =>
                {
                    Assert.Equal(secondId, current.SessionId);
                    Assert.Null(current.EndTime);
                    Assert.Equal("AOI TEMP", current.Model);
                    Assert.Equal("W2", current.WorkerNo);
                },
                previous =>
                {
                    Assert.Equal(firstId, previous.SessionId);
                    Assert.Equal(start.AddMinutes(2), previous.EndTime);
                    Assert.Equal("W1", previous.WorkerNo);
                });

            KBProductionStatistics otherModel = store.QueryStatistics(new KBProductionQuery
            {
                From = start.Date,
                ToExclusive = start.Date.AddDays(1),
                Model = "OTHER"
            }, start.AddHours(1));
            Assert.Empty(otherModel.SessionRows);
            Assert.Equal(0, otherModel.TargetProduction);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    [Fact]
    public void PeriodUsesMondayBasedWeeksAndSupportsCurrentMonthNavigation()
    {
        DateTime anchor = new(2026, 8, 5);

        KBProductionPeriodRange week = KBProductionPeriod.GetRange(KBProductionPeriodMode.Week, anchor);
        KBProductionPeriodRange month = KBProductionPeriod.GetRange(KBProductionPeriodMode.Month, anchor);

        Assert.Equal(new DateTime(2026, 8, 3), week.From);
        Assert.Equal(new DateTime(2026, 8, 10), week.ToExclusive);
        Assert.Equal(new DateTime(2026, 8, 1), month.From);
        Assert.Equal(new DateTime(2026, 9, 1), month.ToExclusive);
        Assert.Equal(new DateTime(2026, 7, 5), KBProductionPeriod.ShiftAnchor(KBProductionPeriodMode.Month, anchor, -1));
    }

    [Fact]
    public void DataStoreFiltersAndPagesIndividualDetectionRecordsWithoutGrouping()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"ProjectKB-Records-{Guid.NewGuid():N}.db");
        try
        {
            var store = new KBProductionDataStore(databasePath);
            store.InitializeSchema();
            DateTime day = new(2026, 8, 5);
            using (var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={databasePath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute
            }))
            {
                db.Insertable(new[]
                {
                    CreateResult(0, null, day.AddHours(8), FlowStatus.Completed, true, 1_000, "SN-A1", "AOI TEMP"),
                    CreateResult(0, null, day.AddHours(9), FlowStatus.Completed, false, 2_000, "SN-A2", "AOI TEMP"),
                    CreateResult(0, null, day.AddHours(10), FlowStatus.Completed, true, 3_000, "SN-B1", "OTHER")
                }).ExecuteCommand();
            }

            var query = new KBProductionQuery
            {
                From = day,
                ToExclusive = day.AddDays(1),
                PeriodMode = KBProductionPeriodMode.Day,
                Model = "AOI",
                SN = "A",
                PageNumber = 1,
                PageSize = 1
            };

            Assert.Equal(2, store.QueryRecordCount(query));
            KBProductionRecordRow firstPage = Assert.Single(store.QueryRecords(query));
            Assert.Equal("SN-A2", firstPage.SN);
            KBProductionRecordRow secondPage = Assert.Single(store.QueryRecords(new KBProductionQuery
            {
                From = query.From,
                ToExclusive = query.ToExclusive,
                PeriodMode = query.PeriodMode,
                Model = query.Model,
                SN = query.SN,
                PageNumber = 2,
                PageSize = 1
            }));
            Assert.Equal("SN-A1", secondPage.SN);

            KBProductionStatistics statistics = store.QueryStatistics(query, day.AddHours(11));
            Assert.Equal(2, statistics.TotalRuns);
            Assert.Equal(2, statistics.ProductionCount);
            Assert.Equal(2, statistics.TrendRows.Count);
            Assert.All(statistics.TrendRows, point => Assert.Equal(1, point.ProductionCount));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    [Fact]
    public void InitializeSchemaUpgradesALegacyResultTableWithoutLosingRows()
    {
        string databasePath = Path.Combine(Path.GetTempPath(), $"ProjectKB-Legacy-{Guid.NewGuid():N}.db");
        string connectionString = $"Data Source={databasePath};Pooling=False";
        try
        {
            using (var connection = new SqliteConnection(connectionString))
            {
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE KBItemMaster (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        BatchId INTEGER NOT NULL DEFAULT 0,
                        Model TEXT NOT NULL DEFAULT '',
                        Result INTEGER NOT NULL DEFAULT 0,
                        FlowStatus INTEGER NOT NULL DEFAULT 0,
                        RunTime INTEGER NOT NULL DEFAULT 0,
                        CreateTime TEXT NOT NULL
                    );
                    INSERT INTO KBItemMaster (Model, Result, FlowStatus, RunTime, CreateTime)
                    VALUES ('AOI TEMP', 1, 6, 1250, '2026-08-05 08:00:00');
                    """;
                command.ExecuteNonQuery();
            }

            var store = new KBProductionDataStore(databasePath);
            store.InitializeSchema();

            using var verificationConnection = new SqliteConnection(connectionString);
            verificationConnection.Open();
            using SqliteCommand columnCommand = verificationConnection.CreateCommand();
            columnCommand.CommandText = "PRAGMA table_info('KBItemMaster')";
            using SqliteDataReader reader = columnCommand.ExecuteReader();
            var columns = new List<string>();
            while (reader.Read())
                columns.Add(reader.GetString(1));
            reader.Close();

            Assert.Contains(columns, column => string.Equals(column, "ProductionSessionId", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(columns, column => string.Equals(column, "ImageWidth", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(columns, column => string.Equals(column, "ImageHeight", StringComparison.OrdinalIgnoreCase));

            using SqliteCommand countCommand = verificationConnection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM KBItemMaster";
            Assert.Equal(1L, (long)countCommand.ExecuteScalar()!);

            using SqliteCommand sessionTableCommand = verificationConnection.CreateCommand();
            sessionTableCommand.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'KBProductionSession'";
            Assert.Equal(1L, (long)sessionTableCommand.ExecuteScalar()!);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
                File.Delete(databasePath);
        }
    }

    private static KBItemMaster CreateResult(
        int id,
        int? sessionId,
        DateTime createTime,
        FlowStatus status,
        bool result,
        long runTime,
        string sn = "",
        string model = "AOI TEMP")
    {
        return new KBItemMaster
        {
            Id = id,
            ProductionSessionId = sessionId,
            CreateTime = createTime,
            FlowStatus = status,
            Result = result,
            RunTime = runTime,
            Model = model,
            SN = sn,
            KeyLcNeighborhoodRadiusMm = 0,
            KeyLcPixelsPerMillimeter = 0,
            KeyLcNeighborhoodVersion = 0
        };
    }
}
