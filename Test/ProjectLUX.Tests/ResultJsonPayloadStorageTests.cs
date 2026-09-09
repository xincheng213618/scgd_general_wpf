using Microsoft.Data.Sqlite;
using SqlSugar;
using System.Data;
using System.IO;
using Xunit;

namespace ProjectLUX.Tests;

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
        Assert.DoesNotContain(nameof(ProjectLUXReuslt.ViewResultJson), viewColumns.Keys);
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
            var view = new ProjectLUXReuslt
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

            ProjectLUXReuslt metadataView = db.Queryable<ProjectLUXReuslt>().InSingle(viewId);
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
        var lazyView = new ProjectLUXReuslt { Id = viewId };
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
        Assert.False(first.ViewFileNameMadeNullable);
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
        Assert.Equal(viewJson, restartedStore.LoadViewResultJson(new ProjectLUXReuslt { Id = 1 }));
        Assert.Equal(objectiveJson, Assert.Single(restartedStore.GetRecords([1])).ObjectiveTestResultJson);
        Assert.Equal(0L, database.ExecuteScalarInt64("SELECT COUNT(*) FROM \"ARVRReuslt\" WHERE \"ViewResultJson\" IS NOT NULL;"));
        Assert.Equal(0L, database.ExecuteScalarInt64("SELECT COUNT(*) FROM \"ObjectiveTestResultRecord\" WHERE \"ObjectiveTestResultJson\" IS NOT NULL;"));

        LegacyResultJsonMigrationReport second = LegacyResultJsonMigration.Execute(database.Path);

        Assert.Equal(0, second.ViewResultRowsMigrated);
        Assert.Equal(0, second.ObjectiveResultRowsMigrated);
        Assert.False(second.ViewFileNameMadeNullable);
        Assert.Equal("ok", second.IntegrityCheck);
        Assert.Equal(0L, database.ExecuteScalarInt64("PRAGMA freelist_count;"));
    }

    [Fact]
    public void LegacyMigrationMakesFileNameNullableAndPreservesRowsIndexesAndIdentity()
    {
        using var database = new TemporaryPayloadDatabase();
        const string json = "{\"name\":\"legacy-not-null\"}";
        database.CreateLegacySchemaWithRequiredFileName();
        database.InsertLegacyResultWithFileName(7, "legacy.png", json, "keep-this-value");

        LegacyResultJsonMigrationReport first = LegacyResultJsonMigration.Execute(database.Path);

        Assert.True(first.ViewFileNameMadeNullable);
        Assert.False(database.IsColumnNotNull("ARVRReuslt", "FileName"));
        Assert.Equal("legacy.png", database.ExecuteScalarString("SELECT \"FileName\" FROM \"ARVRReuslt\" WHERE \"Id\" = 7;"));
        Assert.Equal("keep-this-value", database.ExecuteScalarString("SELECT \"CustomField\" FROM \"ARVRReuslt\" WHERE \"Id\" = 7;"));
        Assert.Equal(json, database.LoadGzip("ARVRReuslt", ResultJsonPayloadStorage.ViewResultColumnName, 7));
        Assert.True(database.IndexExists("IX_ARVRReuslt_CustomField"));
        Assert.Equal(8, database.InsertNullFileNameRow());

        LegacyResultJsonMigrationReport second = LegacyResultJsonMigration.Execute(database.Path);

        Assert.False(second.ViewFileNameMadeNullable);
        Assert.False(database.IsColumnNotNull("ARVRReuslt", "FileName"));
        Assert.Equal("ok", second.IntegrityCheck);
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

    [Fact]
    public void UnmigratedTextRemainsReadableAndUpdatedPayloadClearsOnlyItsOwnText()
    {
        using var database = new TemporaryPayloadDatabase();
        database.CreateLegacySchema(includeCompressedColumns: true);
        const string oldJson = "{\"legacy\":\"现场结果\"}";
        const string newJson = "{\"new\":true}";
        database.InsertLegacyRow("ARVRReuslt", 1, "ViewResultJson", oldJson);
        database.InsertLegacyRow("ObjectiveTestResultRecord", 1, "ObjectiveTestResultJson", oldJson);
        using SqlSugarClient db = database.CreateClient();
        Assert.Equal(oldJson, ResultJsonPayloadStorage.LoadViewResultJson(db, 1));
        Assert.Equal(oldJson, ResultJsonPayloadStorage.LoadObjectiveTestResultJson(db, 1));
        List<ObjectiveTestResultRecord> records = [new() { Id = 1 }];
        ResultJsonPayloadStorage.LoadObjectiveTestResultJsons(db, records);
        Assert.Equal(oldJson, records[0].ObjectiveTestResultJson);

        ResultJsonPayloadStorage.SaveObjectiveTestResultJson(db, 1, newJson);
        Assert.Equal(newJson, ResultJsonPayloadStorage.LoadObjectiveTestResultJson(db, 1));
        Assert.Equal(oldJson, ResultJsonPayloadStorage.LoadViewResultJson(db, 1));
        Assert.Equal("1", database.ExecuteScalarString("SELECT COUNT(*) FROM ObjectiveTestResultRecord WHERE ObjectiveTestResultJson IS NULL;"));
    }

    [Fact]
    public void StatisticsCountAggregateRecordsAndDoNotDependOnJsonOrFlowCount()
    {
        using var database = new TemporaryPayloadDatabase();
        var store = new ResultStatisticsDataStore(database.Path);
        store.InitializeSchema();
        DateTime day = new(2026, 9, 9);
        using (SqlSugarClient db = database.CreateClient())
        {
            db.Insertable(new[]
            {
                new ObjectiveTestResultRecord { SN = "same-sn", TotalResult = true, CreateTime = day.AddHours(10), UpdateTime = day.AddHours(10).AddSeconds(10) },
                new ObjectiveTestResultRecord { SN = "same-sn", TotalResult = false, CreateTime = day.AddHours(11), UpdateTime = day.AddHours(11).AddSeconds(30) },
                new ObjectiveTestResultRecord { SN = "outside", TotalResult = true, CreateTime = day.AddDays(1), UpdateTime = day.AddDays(1) },
            }).ExecuteCommand();
        }
        var query = new ResultStatisticsQuery { From = day, ToExclusive = day.AddDays(1), PageSize = 1 };
        var dashboard = store.QueryDashboard(query, ResultStatisticsPeriodMode.Day, day.AddHours(12));
        Assert.Equal(2, dashboard.Summary.TotalCount);
        Assert.Equal(1, dashboard.Summary.PassCount);
        Assert.Equal(1, dashboard.Summary.FailCount);
        Assert.Equal(0.5, dashboard.Summary.PassRate);
        Assert.InRange(dashboard.Summary.AverageCtMilliseconds, 19999, 20001);
        Assert.Equal(2, store.QueryRecordCount(query));
        Assert.Equal(2, Assert.Single(store.QueryRecords(query)).ExecutionIndex);
        Assert.Equal(2, dashboard.Trend.Count);
        Assert.Single(store.QueryRecords(new ResultStatisticsQuery { From = day, ToExclusive = day.AddDays(1), Result = true }));
        Assert.Equal(3, store.QueryDashboard(new ResultStatisticsQuery { From = DateTime.MinValue, ToExclusive = DateTime.MaxValue }, ResultStatisticsPeriodMode.All, day).Summary.TotalCount);
        using (SqlSugarClient db = database.CreateClient())
        {
            db.Updateable<ObjectiveTestResultRecord>().SetColumns(item => item.UpdateTime == day.AddHours(11).AddSeconds(40))
                .Where(item => item.Id == 2).ExecuteCommand();
        }
        Assert.Equal(2, store.QueryRecordCount(query));
        Assert.InRange(store.QueryDashboard(query, ResultStatisticsPeriodMode.Day, day).Summary.AverageCtMilliseconds, 24999, 25001);
    }

    [Fact]
    public void SchemaUpgradePreservesLegacyJsonAndOriginalFilePath()
    {
        using var database = new TemporaryPayloadDatabase();
        var store = new ResultStatisticsDataStore(database.Path);
        store.InitializeSchema();
        using (SqlSugarClient db = database.CreateClient())
        {
            db.Ado.ExecuteCommand("ALTER TABLE ARVRReuslt ADD COLUMN ViewResultJson TEXT NULL;");
            db.Ado.ExecuteCommand("ALTER TABLE ObjectiveTestResultRecord ADD COLUMN ObjectiveTestResultJson TEXT NULL;");
            db.Insertable(new ProjectLUXReuslt { FileName = @"D:\CVTest\legacy.cvraw" }).ExecuteCommand();
            db.Insertable(new ObjectiveTestResultRecord { SN = "legacy" }).ExecuteCommand();
            db.Ado.ExecuteCommand("UPDATE ARVRReuslt SET ViewResultJson = '{\"old\":true}';");
            db.Ado.ExecuteCommand("UPDATE ObjectiveTestResultRecord SET ObjectiveTestResultJson = '{\"old\":true}';");
        }
        var restartedStore = new ResultStatisticsDataStore(database.Path);
        restartedStore.InitializeSchema();
        Assert.Equal("{\"old\":true}", restartedStore.GetRecord(1)?.ObjectiveTestResultJson);
        Assert.Equal("{\"old\":true}", restartedStore.LoadViewResultJson(new ProjectLUXReuslt { Id = 1 }));
        Assert.Equal(@"D:\CVTest\legacy.cvraw", database.ExecuteScalarString("SELECT FileName FROM ARVRReuslt WHERE Id = 1;"));
    }

    [Fact]
    public void StatisticsWindowRendersAggregateRowsWithoutOpeningProductionDatabase()
    {
        WpfTestHost.Invoke(() =>
        {
            var window = new ResultStatisticsWindow(refreshOnLoad: false) { Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
            window.Resources["GlobalBackground"] = System.Windows.Media.Brushes.White;
            window.Resources["GlobalForeground"] = System.Windows.Media.Brushes.Black;
            window.Show();
            var records = (System.Windows.Controls.DataGrid)window.FindName("Records");
            records.ItemsSource = new[] { new ResultStatisticsRecordRow { Id = 82165, SN = "LUX-DEMO", Result = true, StartTime = new DateTime(2026, 9, 9, 10, 0, 0), EndTime = new DateTime(2026, 9, 9, 10, 0, 20), ExecutionIndex = 1, LastModel = "Distortion" } };
            ((System.Windows.FrameworkElement)window.FindName("SummaryPanel")).DataContext = new ResultStatistics { TotalCount = 1, PassCount = 1, PassRate = 1, AverageCtMilliseconds = 20000 };
            var content = (System.Windows.FrameworkElement)window.Content;
            content.Measure(new System.Windows.Size(1080, 680));
            content.Arrange(new System.Windows.Rect(0, 0, 1080, 680));
            content.UpdateLayout();
            Assert.Single(records.Items.Cast<object>());
            Assert.True(records.ActualHeight > 0);
            string? previewPath = Environment.GetEnvironmentVariable("LUX_STATS_PREVIEW_PATH");
            if (!string.IsNullOrWhiteSpace(previewPath))
            {
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap(1080, 680, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                bitmap.Render(content);
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using var stream = File.Create(previewPath);
                encoder.Save(stream);
            }
            window.Close();
        });
    }

    [Fact]
    public void MissingPayloadRowCannotReportSuccessfulSave()
    {
        using var database = new TemporaryPayloadDatabase();
        database.CreateLegacySchema(includeCompressedColumns: true);
        using SqlSugarClient db = database.CreateClient();
        Assert.Throws<InvalidOperationException>(() => ResultJsonPayloadStorage.SaveViewResultJson(db, 82165, "{}"));
    }

    private sealed class TemporaryPayloadDatabase : IDisposable
    {
        private readonly string _directory;

        public TemporaryPayloadDatabase()
        {
            _directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ProjectLUX.Payload.{Guid.NewGuid():N}");
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

        public void CreateLegacySchemaWithRequiredFileName()
        {
            using var connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE "ARVRReuslt"
                (
                    "Id" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    "FileName" varchar(255) NOT NULL,
                    "ViewResultJson" TEXT NULL,
                    "CustomField" TEXT NULL
                );
                CREATE INDEX "IX_ARVRReuslt_CustomField" ON "ARVRReuslt" ("CustomField");
                CREATE TABLE "ObjectiveTestResultRecord"
                (
                    "Id" INTEGER PRIMARY KEY AUTOINCREMENT,
                    "ObjectiveTestResultJson" TEXT NULL
                );
                """;
            command.ExecuteNonQuery();
        }

        public void InsertLegacyResultWithFileName(int id, string fileName, string json, string customField)
        {
            using var connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO \"ARVRReuslt\" (\"Id\", \"FileName\", \"ViewResultJson\", \"CustomField\") " +
                "VALUES ($id, $fileName, $json, $customField);";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$fileName", fileName);
            command.Parameters.AddWithValue("$json", json);
            command.Parameters.AddWithValue("$customField", customField);
            command.ExecuteNonQuery();
        }

        public bool IsColumnNotNull(string tableName, string columnName)
        {
            using var connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
            using SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                    return reader.GetInt32(3) != 0;
            }
            throw new InvalidOperationException($"未找到 {tableName}.{columnName}。");
        }

        public bool IndexExists(string indexName)
        {
            using var connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $name;";
            command.Parameters.AddWithValue("$name", indexName);
            return Convert.ToInt64(command.ExecuteScalar()) == 1;
        }

        public int InsertNullFileNameRow()
        {
            using var connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO \"ARVRReuslt\" (\"FileName\", \"ViewResultJson\", \"CustomField\") " +
                "VALUES (NULL, NULL, 'new-row'); SELECT last_insert_rowid();";
            return Convert.ToInt32(command.ExecuteScalar());
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
