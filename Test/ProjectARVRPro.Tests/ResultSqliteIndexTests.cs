using Microsoft.Data.Sqlite;
using SqlSugar;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class ResultSqliteIndexTests
{
    [Fact]
    public void OpeningExistingResultDatabaseAddsIndexesWithoutChangingRows()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"ProjectARVRPro.Indexes.{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string databasePath = Path.Combine(directory, "ProjectARVRPro.db");

        try
        {
            int latestId;
            using (SqlSugarClient db = CreateClient(databasePath))
            {
                db.CodeFirst.InitTables<ProjectARVRReuslt, ObjectiveTestResultRecord>();
                db.Insertable(new ProjectARVRReuslt { BatchId = 42, SN = "SN-OLD", Model = "MTF" }).ExecuteCommand();
                latestId = db.Insertable(new ProjectARVRReuslt { BatchId = 42, SN = "SN-OLD", Model = "White" }).ExecuteReturnIdentity();
                db.Insertable(new ProjectARVRReuslt { BatchId = 7, SN = "SN-OTHER", Model = "MTF" }).ExecuteCommand();
            }

            using (SqliteConnection connection = new($"Data Source={databasePath}"))
            {
                connection.Open();
                Assert.Equal("SCAN ARVRReuslt", QueryPlan(connection, 42));
            }

            using (SqlSugarClient db = CreateClient(databasePath))
            {
                ResultSqliteSchema.EnsureCreated(db);
                ResultSqliteSchema.EnsureCreated(db);
                Assert.Equal(3, db.Queryable<ProjectARVRReuslt>().Count());
                Assert.Equal(latestId, db.Queryable<ProjectARVRReuslt>()
                    .Where(row => row.BatchId == 42)
                    .OrderBy(row => row.Id, OrderByType.Desc)
                    .First().Id);
            }

            using (SqliteConnection connection = new($"Data Source={databasePath}"))
            {
                connection.Open();
                Assert.Contains("IX_ARVRReuslt_BatchId", QueryPlan(connection, 42), StringComparison.Ordinal);
                using SqliteCommand columnCommand = connection.CreateCommand();
                columnCommand.CommandText = "PRAGMA index_info('IX_ARVRReuslt_BatchId');";
                using SqliteDataReader reader = columnCommand.ExecuteReader();
                Assert.True(reader.Read());
                Assert.Equal("BatchId", reader.GetString(2));
                Assert.False(reader.Read());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (string file in Directory.GetFiles(directory))
                File.Delete(file);
            Directory.Delete(directory);
        }
    }

    private static SqlSugarClient CreateClient(string databasePath) => new(new ConnectionConfig
    {
        ConnectionString = $"Data Source={databasePath}",
        DbType = SqlSugar.DbType.Sqlite,
        IsAutoCloseConnection = true,
        InitKeyType = InitKeyType.Attribute,
    });

    private static string QueryPlan(SqliteConnection connection, int batchId)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN SELECT \"Id\" FROM \"ARVRReuslt\" WHERE \"BatchId\" = $batchId ORDER BY \"Id\" DESC LIMIT 1;";
        command.Parameters.AddWithValue("$batchId", batchId);
        using SqliteDataReader reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return reader.GetString(3);
    }
}
