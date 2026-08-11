using Microsoft.Data.Sqlite;
using SqlSugar;
using System.Data;
using System.IO;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class ResultJsonPayloadStorageTests
{
    [Theory]
    [InlineData("{\"name\":\"中文结果\",\"value\":123.45}")]
    [InlineData("{\"emoji\":\"测试🧪\",\"items\":[1,2,3]}")]
    public void GzipPayloadRoundTripsExactly(string json)
    {
        byte[] payload = Assert.IsType<byte[]>(ResultJsonPayloadStorage.Compress(json));

        Assert.Equal(json, ResultJsonPayloadStorage.Decompress(payload));
    }

    [Fact]
    public void NewSchemaContainsOnlyCompressedPayloadColumns()
    {
        using var database = new TemporaryPayloadDatabase();

        new ResultStatisticsDataStore(database.Path).InitializeSchema();

        Dictionary<string, string> viewColumns = database.QueryColumns("ARVRReuslt");
        Dictionary<string, string> objectiveColumns = database.QueryColumns("ObjectiveTestResultRecord");
        Assert.Equal("BLOB", viewColumns[ResultJsonPayloadStorage.ViewResultColumnName]);
        Assert.Equal("BLOB", objectiveColumns[ResultJsonPayloadStorage.ObjectiveResultColumnName]);
        Assert.DoesNotContain(nameof(ProjectARVRReuslt.ViewResultJson), viewColumns.Keys);
        Assert.DoesNotContain(nameof(ObjectiveTestResultRecord.ObjectiveTestResultJson), objectiveColumns.Keys);
    }

    [Fact]
    public void MetadataQueriesDoNotLoadPayloadsUntilExplicitlyRequested()
    {
        using var database = new TemporaryPayloadDatabase();
        var store = new ResultStatisticsDataStore(database.Path);
        store.InitializeSchema();
        const string viewJson = "{\"flow\":\"MTF\",\"text\":\"按需读取\"}";
        const string objectiveJson = "{\"totalResult\":true,\"text\":\"整组结果\"}";

        int viewId;
        int objectiveId;
        using (SqlSugarClient db = database.CreateClient())
        {
            var view = new ProjectARVRReuslt
            {
                SN = "SN-PAYLOAD",
                Model = "MTF",
                ViewResultJson = viewJson,
            };
            viewId = db.Insertable(view).ExecuteReturnIdentity();
            ResultJsonPayloadStorage.SaveViewResultJson(db, viewId, viewJson);

            var objective = new ObjectiveTestResultRecord
            {
                SN = "SN-PAYLOAD",
                LastCode = "CODE",
                LastModel = "MTF",
                LastFlowStatus = "Completed",
                ObjectiveTestResultJson = objectiveJson,
            };
            objectiveId = db.Insertable(objective).ExecuteReturnIdentity();
            ResultJsonPayloadStorage.SaveObjectiveTestResultJson(db, objectiveId, objectiveJson);

            ProjectARVRReuslt metadataView = db.Queryable<ProjectARVRReuslt>().InSingle(viewId);
            ObjectiveTestResultRecord metadataObjective = db.Queryable<ObjectiveTestResultRecord>().InSingle(objectiveId);
            Assert.Null(metadataView.ViewResultJson);
            Assert.Null(metadataObjective.ObjectiveTestResultJson);
        }

        Assert.Equal("blob", database.ExecuteScalarString(
            $"SELECT typeof(\"{ResultJsonPayloadStorage.ViewResultColumnName}\") FROM \"ARVRReuslt\" WHERE \"Id\" = {viewId};"));
        Assert.Equal("blob", database.ExecuteScalarString(
            $"SELECT typeof(\"{ResultJsonPayloadStorage.ObjectiveResultColumnName}\") FROM \"ObjectiveTestResultRecord\" WHERE \"Id\" = {objectiveId};"));

        // Simulate a later application start. CodeFirst must preserve the unmapped BLOB columns and their data.
        var restartedStore = new ResultStatisticsDataStore(database.Path);
        restartedStore.InitializeSchema();
        var lazyView = new ProjectARVRReuslt { Id = viewId };
        Assert.Equal(viewJson, restartedStore.LoadViewResultJson(lazyView));
        Assert.Equal(objectiveJson, restartedStore.GetRecord(objectiveId)?.ObjectiveTestResultJson);
        Assert.Equal(objectiveJson, Assert.Single(restartedStore.GetRecords([objectiveId])).ObjectiveTestResultJson);
    }

    [Fact]
    public void LegacyMigrationClearsOldFieldsAndCanBeRepeated()
    {
        using var database = new TemporaryPayloadDatabase();
        string viewJson = $"{{\"name\":\"legacy-view\",\"data\":\"{new string('V', 2_000_000)}\"}}";
        string objectiveJson = $"{{\"name\":\"legacy-objective\",\"data\":\"{new string('O', 1_000_000)}\"}}";
        database.CreateLegacySchema();
        database.InsertLegacyRow("ARVRReuslt", 1, "ViewResultJson", viewJson);
        database.InsertLegacyRow("ARVRReuslt", 2, "ViewResultJson", string.Empty);
        database.InsertLegacyRow("ObjectiveTestResultRecord", 1, "ObjectiveTestResultJson", objectiveJson);
        long beforeBytes = new FileInfo(database.Path).Length;

        LegacyResultJsonMigrationReport first = LegacyResultJsonMigration.Execute(database.Path);

        Assert.Equal(1, first.ViewResultRowsMigrated);
        Assert.Equal(1, first.ObjectiveResultRowsMigrated);
        Assert.Equal("ok", first.IntegrityCheck);
        Assert.True(first.AfterBytes < beforeBytes);
        Assert.Equal(0L, database.ExecuteScalarInt64("SELECT COUNT(*) FROM \"ARVRReuslt\" WHERE \"ViewResultJson\" IS NOT NULL;"));
        Assert.Equal(0L, database.ExecuteScalarInt64("SELECT COUNT(*) FROM \"ObjectiveTestResultRecord\" WHERE \"ObjectiveTestResultJson\" IS NOT NULL;"));
        Assert.Equal(viewJson, database.LoadGzip("ARVRReuslt", ResultJsonPayloadStorage.ViewResultColumnName, 1));
        Assert.Equal(objectiveJson, database.LoadGzip("ObjectiveTestResultRecord", ResultJsonPayloadStorage.ObjectiveResultColumnName, 1));
        Assert.Equal("BLOB", database.QueryColumns("ARVRReuslt")[ResultJsonPayloadStorage.ViewResultColumnName]);
        Assert.Equal("BLOB", database.QueryColumns("ObjectiveTestResultRecord")[ResultJsonPayloadStorage.ObjectiveResultColumnName]);

        // Simulate the first normal application start after migrating a field database.
        var restartedStore = new ResultStatisticsDataStore(database.Path);
        restartedStore.InitializeSchema();
        Assert.Equal(viewJson, restartedStore.LoadViewResultJson(new ProjectARVRReuslt { Id = 1 }));
        Assert.Equal(objectiveJson, Assert.Single(restartedStore.GetRecords([1])).ObjectiveTestResultJson);
        Assert.Equal(0L, database.ExecuteScalarInt64("SELECT COUNT(*) FROM \"ARVRReuslt\" WHERE \"ViewResultJson\" IS NOT NULL;"));
        Assert.Equal(0L, database.ExecuteScalarInt64("SELECT COUNT(*) FROM \"ObjectiveTestResultRecord\" WHERE \"ObjectiveTestResultJson\" IS NOT NULL;"));

        LegacyResultJsonMigrationReport second = LegacyResultJsonMigration.Execute(database.Path);

        Assert.Equal(0, second.ViewResultRowsMigrated);
        Assert.Equal(0, second.ObjectiveResultRowsMigrated);
        Assert.Equal("ok", second.IntegrityCheck);
        Assert.Equal(0L, database.ExecuteScalarInt64("PRAGMA freelist_count;"));
    }

    [Fact]
    public void LegacyMigrationVerifiesExistingCompressedPayloadBeforeClearingResidualText()
    {
        using var database = new TemporaryPayloadDatabase();
        const string json = "{\"name\":\"resume-after-interruption\"}";
        database.CreateLegacySchema(includeCompressedColumns: true);
        database.InsertLegacyRow(
            "ARVRReuslt",
            1,
            "ViewResultJson",
            json,
            ResultJsonPayloadStorage.ViewResultColumnName,
            ResultJsonPayloadStorage.Compress(json));

        LegacyResultJsonMigrationReport report = LegacyResultJsonMigration.Execute(database.Path);

        Assert.Equal(0, report.ViewResultRowsMigrated);
        Assert.Equal(1, report.ViewResidualRowsCleared);
        Assert.Equal(json, database.LoadGzip("ARVRReuslt", ResultJsonPayloadStorage.ViewResultColumnName, 1));
        Assert.Equal(0L, database.ExecuteScalarInt64("SELECT COUNT(*) FROM \"ARVRReuslt\" WHERE \"ViewResultJson\" IS NOT NULL;"));
    }

    [Theory]
    [InlineData("mismatch")]
    [InlineData("corrupt")]
    public void LegacyMigrationKeepsOldTextWhenExistingCompressedPayloadIsInvalid(string payloadKind)
    {
        using var database = new TemporaryPayloadDatabase();
        const string json = "{\"name\":\"only-valid-copy\"}";
        byte[] payload = payloadKind == "mismatch"
            ? Assert.IsType<byte[]>(ResultJsonPayloadStorage.Compress("{\"name\":\"different\"}"))
            : [1, 2, 3, 4];
        database.CreateLegacySchema(includeCompressedColumns: true);
        database.InsertLegacyRow(
            "ARVRReuslt",
            1,
            "ViewResultJson",
            json,
            ResultJsonPayloadStorage.ViewResultColumnName,
            payload);

        Assert.Throws<InvalidDataException>(() => LegacyResultJsonMigration.Execute(database.Path));

        Assert.Equal(json, database.ExecuteScalarString("SELECT \"ViewResultJson\" FROM \"ARVRReuslt\" WHERE \"Id\" = 1;"));
        Assert.Equal("ok", database.ExecuteScalarString("PRAGMA quick_check;"));
    }

    private sealed class TemporaryPayloadDatabase : IDisposable
    {
        private readonly string _directory;

        public TemporaryPayloadDatabase()
        {
            _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ProjectARVRPro.Payload.{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
            Path = System.IO.Path.Combine(_directory, "payload.db");
        }

        public string Path { get; }

        public SqlSugarClient CreateClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={Path};Pooling=False",
                DbType = SqlSugar.DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
        }

        public Dictionary<string, string> QueryColumns(string tableName)
        {
            using var connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
            using SqliteDataReader reader = command.ExecuteReader();
            var columns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
                columns[reader.GetString(1)] = reader.GetString(2).ToUpperInvariant();
            return columns;
        }

        public void CreateLegacySchema(bool includeCompressedColumns = false)
        {
            using var connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            string viewCompressedColumn = includeCompressedColumns
                ? $", \"{ResultJsonPayloadStorage.ViewResultColumnName}\" BLOB NULL"
                : string.Empty;
            string objectiveCompressedColumn = includeCompressedColumns
                ? $", \"{ResultJsonPayloadStorage.ObjectiveResultColumnName}\" BLOB NULL"
                : string.Empty;
            command.CommandText = $"""
                CREATE TABLE "ARVRReuslt"
                (
                    "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
                    "ViewResultJson" TEXT NULL{viewCompressedColumn}
                );
                CREATE TABLE "ObjectiveTestResultRecord"
                (
                    "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
                    "ObjectiveTestResultJson" TEXT NULL{objectiveCompressedColumn}
                );
                """;
            command.ExecuteNonQuery();
        }

        public void InsertLegacyRow(
            string tableName,
            int id,
            string legacyColumn,
            string legacyJson,
            string? gzipColumn = null,
            byte[]? gzipPayload = null)
        {
            using var connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            string gzipSql = gzipColumn == null ? string.Empty : $", \"{gzipColumn}\"";
            string gzipValueSql = gzipColumn == null ? string.Empty : ", $gzip";
            command.CommandText =
                $"INSERT INTO \"{tableName}\" (\"Id\", \"{legacyColumn}\"{gzipSql}) VALUES ($id, $legacy{gzipValueSql});";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$legacy", legacyJson);
            if (gzipColumn != null)
                command.Parameters.Add("$gzip", SqliteType.Blob).Value = gzipPayload ?? (object)DBNull.Value;
            command.ExecuteNonQuery();
        }

        public string? LoadGzip(string tableName, string columnName, int id)
        {
            using var connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT \"{columnName}\" FROM \"{tableName}\" WHERE \"Id\" = $id;";
            command.Parameters.AddWithValue("$id", id);
            return ResultJsonPayloadStorage.Decompress(command.ExecuteScalar() as byte[]);
        }

        public string ExecuteScalarString(string sql)
        {
            using var connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
        }

        public long ExecuteScalarInt64(string sql)
        {
            using var connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar());
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(Path))
                File.Delete(Path);
            if (Directory.Exists(_directory))
                Directory.Delete(_directory);
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection($"Data Source={Path};Pooling=False");
            connection.Open();
            return connection;
        }
    }
}
