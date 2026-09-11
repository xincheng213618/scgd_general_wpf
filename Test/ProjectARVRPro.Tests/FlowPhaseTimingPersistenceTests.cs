using Microsoft.Data.Sqlite;
using SqlSugar;
using System.Data;
using System.IO;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class FlowPhaseTimingPersistenceTests
{
    private static readonly string[] PhaseTimestampColumns =
    [
        nameof(ProjectARVRReuslt.SwitchRequestedAt),
        nameof(ProjectARVRReuslt.SwitchAcknowledgedAt),
        nameof(ProjectARVRReuslt.PictureSwitchStartedAt),
        nameof(ProjectARVRReuslt.PictureSwitchCompletedAt),
        nameof(ProjectARVRReuslt.PreProcessingCompletedAt),
        nameof(ProjectARVRReuslt.FlowStartedAt),
        nameof(ProjectARVRReuslt.FlowCompletedAt),
        nameof(ProjectARVRReuslt.ResultProcessingCompletedAt),
    ];

    [Fact]
    public void SchemaUpgradeAddsNullablePhaseTimestampsAndPreservesLegacyRows()
    {
        using var database = new TemporaryTimingDatabase();
        int legacyId;
        using (SqlSugarClient db = database.CreateClient())
        {
            db.CodeFirst.InitTables<ProjectARVRReuslt>();
            legacyId = db.Insertable(new ProjectARVRReuslt
            {
                SN = "SN-LEGACY",
                Model = "White51_Fast_Test",
            }).ExecuteReturnIdentity();
            foreach (string column in PhaseTimestampColumns)
                db.Ado.ExecuteCommand($"ALTER TABLE \"ARVRReuslt\" DROP COLUMN \"{column}\";");
        }

        Assert.Empty(PhaseTimestampColumns.Intersect(database.QueryColumnNames()));

        new ResultStatisticsDataStore(database.Path).InitializeSchema();

        Assert.All(PhaseTimestampColumns, column => Assert.Contains(column, database.QueryColumnNames()));
        using SqlSugarClient upgradedDb = database.CreateClient();
        ProjectARVRReuslt legacy = upgradedDb.Queryable<ProjectARVRReuslt>().InSingle(legacyId);
        Assert.NotNull(legacy);
        Assert.All(ReadPhaseTimestamps(legacy), timestamp => Assert.Null(timestamp));
    }

    [Fact]
    public void PhaseTimestampsRoundTripThroughSqlite()
    {
        using var database = new TemporaryTimingDatabase();
        using SqlSugarClient db = database.CreateClient();
        db.CodeFirst.InitTables<ProjectARVRReuslt>();
        DateTime start = new(2026, 9, 4, 21, 25, 9, 100);
        var expected = new ProjectARVRReuslt
        {
            SN = "SN-TIMING",
            Model = "White51_Fast_Test",
            SwitchRequestedAt = start,
            SwitchAcknowledgedAt = start.AddMilliseconds(299),
            PictureSwitchStartedAt = start.AddMilliseconds(364),
            PictureSwitchCompletedAt = start.AddMilliseconds(400),
            PreProcessingCompletedAt = start.AddMilliseconds(420),
            FlowStartedAt = start.AddMilliseconds(430),
            FlowCompletedAt = start.AddMilliseconds(3_470),
            ResultProcessingCompletedAt = start.AddMilliseconds(4_532),
        };

        int id = db.Insertable(expected).ExecuteReturnIdentity();
        ProjectARVRReuslt actual = db.Queryable<ProjectARVRReuslt>().InSingle(id);

        Assert.NotNull(actual);
        Assert.Equal(ReadPhaseTimestamps(expected), ReadPhaseTimestamps(actual));
    }

    private static DateTime?[] ReadPhaseTimestamps(ProjectARVRReuslt result) =>
    [
        result.SwitchRequestedAt,
        result.SwitchAcknowledgedAt,
        result.PictureSwitchStartedAt,
        result.PictureSwitchCompletedAt,
        result.PreProcessingCompletedAt,
        result.FlowStartedAt,
        result.FlowCompletedAt,
        result.ResultProcessingCompletedAt,
    ];

    private sealed class TemporaryTimingDatabase : IDisposable
    {
        private readonly string _directory;

        public TemporaryTimingDatabase()
        {
            _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ProjectARVRPro.FlowTiming.{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "results.db");
        }

        public string Path { get; }

        public SqlSugarClient CreateClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={Path}",
                DbType = SqlSugar.DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
        }

        public HashSet<string> QueryColumnNames()
        {
            using SqlSugarClient db = CreateClient();
            DataTable table = db.Ado.GetDataTable("PRAGMA table_info('ARVRReuslt');");
            return table.Rows.Cast<DataRow>()
                .Select(row => Convert.ToString(row["name"]) ?? string.Empty)
                .ToHashSet(StringComparer.Ordinal);
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(Path))
                File.Delete(Path);
            if (Directory.Exists(_directory))
                Directory.Delete(_directory);
        }
    }
}
