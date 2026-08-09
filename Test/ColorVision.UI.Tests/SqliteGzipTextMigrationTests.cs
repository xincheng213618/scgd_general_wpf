using ColorVision.Database;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.Data;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class SqliteGzipTextMigrationTests
{
    private static readonly SqliteGzipTextMigrationSpec MigrationSpec = new(
        "PayloadLog",
        "id",
        "legacy_text",
        "payload_gzip",
        "payload_utf8_length",
        "payload_preview",
        PreviewCharacters: 24);

    [Fact]
    public void CodecRoundTripsUnicodeAndKeepsNullAndEmptyDistinct()
    {
        const string unicode = "中文结果 🧪 — Καλημέρα";

        GzipTextPayload nullPayload = GzipTextPayloadCodec.Encode(null);
        GzipTextPayload emptyPayload = GzipTextPayloadCodec.Encode(string.Empty);
        GzipTextPayload unicodePayload = GzipTextPayloadCodec.Encode(unicode);

        Assert.Null(nullPayload.CompressedBytes);
        Assert.Null(nullPayload.Utf8Length);
        Assert.Null(GzipTextPayloadCodec.Decode(nullPayload.CompressedBytes, nullPayload.Utf8Length));

        Assert.NotNull(emptyPayload.CompressedBytes);
        Assert.Equal(0, emptyPayload.Utf8Length);
        Assert.Equal(string.Empty, GzipTextPayloadCodec.Decode(emptyPayload.CompressedBytes, emptyPayload.Utf8Length));

        Assert.True(unicodePayload.Utf8Length > unicode.Length);
        Assert.Equal(unicode, GzipTextPayloadCodec.Decode(unicodePayload.CompressedBytes, unicodePayload.Utf8Length));
        Assert.Equal("😀…", GzipTextPayloadCodec.CreatePreview("😀abcdef", 2));
    }

    [Fact]
    public void CodecRejectsCorruptPayloadInvalidLengthAndOversizedDeclaration()
    {
        GzipTextPayload valid = GzipTextPayloadCodec.Encode("payload");

        Assert.Throws<InvalidDataException>(() =>
            GzipTextPayloadCodec.Decode([1, 2, 3, 4], 7));
        Assert.Throws<InvalidDataException>(() =>
            GzipTextPayloadCodec.Decode(valid.CompressedBytes, valid.Utf8Length + 1));
        Assert.Throws<InvalidDataException>(() =>
            GzipTextPayloadCodec.Decode(valid.CompressedBytes, valid.Utf8Length, maximumUtf8Bytes: 3));
        Assert.Throws<InvalidDataException>(() =>
            GzipTextPayloadCodec.Decode(null, 0));
    }

    [Fact]
    public void PayloadStoreCreatesOnlyNewColumnsAndPersistsNullEmptyAndUnicode()
    {
        using var database = new TemporarySqliteDatabase();
        using SqlSugarClient db = database.CreateSugarClient();
        db.Ado.ExecuteCommand("CREATE TABLE PayloadLog (id INTEGER PRIMARY KEY AUTOINCREMENT);");

        SqliteGzipTextPayloadStore.EnsureSchema(
            db,
            MigrationSpec.TableName,
            MigrationSpec.GzipColumnName,
            MigrationSpec.Utf8LengthColumnName,
            MigrationSpec.PreviewColumnName);
        db.Ado.ExecuteCommand("INSERT INTO PayloadLog DEFAULT VALUES; INSERT INTO PayloadLog DEFAULT VALUES; INSERT INTO PayloadLog DEFAULT VALUES;");

        SqliteGzipTextPayloadStore.Save(db, "PayloadLog", "id", 1, "payload_gzip", "payload_utf8_length", null, "payload_preview", 24);
        SqliteGzipTextPayloadStore.Save(db, "PayloadLog", "id", 2, "payload_gzip", "payload_utf8_length", string.Empty, "payload_preview", 24);
        SqliteGzipTextPayloadStore.Save(db, "PayloadLog", "id", 3, "payload_gzip", "payload_utf8_length", "按需读取🧪", "payload_preview", 24);

        Dictionary<string, string> columns = database.QueryColumns("PayloadLog");
        Assert.Equal("BLOB", columns["payload_gzip"]);
        Assert.Equal("INTEGER", columns["payload_utf8_length"]);
        Assert.Equal("TEXT", columns["payload_preview"]);
        Assert.DoesNotContain("legacy_text", columns.Keys);
        Assert.Null(SqliteGzipTextPayloadStore.Load(db, "PayloadLog", "id", 1, "payload_gzip", "payload_utf8_length"));
        Assert.Equal(string.Empty, SqliteGzipTextPayloadStore.Load(db, "PayloadLog", "id", 2, "payload_gzip", "payload_utf8_length"));
        Assert.Equal("按需读取🧪", SqliteGzipTextPayloadStore.Load(db, "PayloadLog", "id", 3, "payload_gzip", "payload_utf8_length"));
        Assert.Equal("null", database.ExecuteScalarString("SELECT typeof(payload_gzip) FROM PayloadLog WHERE id = 1;"));
        Assert.Equal("blob", database.ExecuteScalarString("SELECT typeof(payload_gzip) FROM PayloadLog WHERE id = 2;"));
        Assert.Equal(0L, database.ExecuteScalarInt64("SELECT payload_utf8_length FROM PayloadLog WHERE id = 2;"));
    }

    [Fact]
    public void MigrationProcessesMoreThanFiveHundredRowsClearsLegacyTextAndIsIdempotent()
    {
        using var database = new TemporarySqliteDatabase();
        database.CreateLegacyPayloadTable();
        const int payloadRows = 1_001;
        string firstText = CreateLargeText(1);
        string lastText = CreateLargeText(payloadRows);
        database.InsertLegacyRows(payloadRows, CreateLargeText);
        database.ExecuteNonQuery("INSERT INTO PayloadLog (legacy_text) VALUES (NULL);");

        SqliteGzipTextMigrationReport first = SqliteGzipTextMigration.Execute(
            database.Path,
            [MigrationSpec],
            batchSize: 500);

        SqliteGzipTextMigrationTableReport firstTable = Assert.Single(first.Tables);
        Assert.Equal(payloadRows, firstTable.MigratedRows);
        Assert.Equal(0, firstTable.ResidualRowsCleared);
        Assert.True(firstTable.LegacyColumnExists);
        Assert.Equal("ok", first.IntegrityCheck);
        Assert.True(first.AfterBytes < first.BeforeBytes);
        Assert.Equal(0L, database.ExecuteScalarInt64("SELECT COUNT(*) FROM PayloadLog WHERE legacy_text IS NOT NULL;"));
        Assert.Equal(payloadRows, database.ExecuteScalarInt64("SELECT COUNT(*) FROM PayloadLog WHERE payload_gzip IS NOT NULL;"));
        Assert.Equal(0L, database.ExecuteScalarInt64("PRAGMA freelist_count;"));
        Assert.Equal(firstText, database.LoadPayload(1));
        Assert.Equal(lastText, database.LoadPayload(payloadRows));
        Assert.Equal(GzipTextPayloadCodec.CreatePreview(firstText, 24), database.ExecuteScalarString("SELECT payload_preview FROM PayloadLog WHERE id = 1;"));

        Dictionary<string, string> columns = database.QueryColumns("PayloadLog");
        Assert.Equal("TEXT", columns["legacy_text"]);
        Assert.Equal("BLOB", columns["payload_gzip"]);
        Assert.Equal("INTEGER", columns["payload_utf8_length"]);

        SqliteGzipTextMigrationReport second = SqliteGzipTextMigration.Execute(
            database.Path,
            [MigrationSpec],
            batchSize: 500);

        SqliteGzipTextMigrationTableReport secondTable = Assert.Single(second.Tables);
        Assert.Equal(0, secondTable.MigratedRows);
        Assert.Equal(0, secondTable.ResidualRowsCleared);
        Assert.Equal("ok", second.IntegrityCheck);
        Assert.Equal(0L, database.ExecuteScalarInt64("PRAGMA freelist_count;"));
        Assert.Equal(firstText, database.LoadPayload(1));
    }

    [Fact]
    public void MigrationClearsMatchingResidualButKeepsMismatchedResidualText()
    {
        using var matchingDatabase = new TemporarySqliteDatabase();
        matchingDatabase.CreateLegacyPayloadTable(includeCompressedColumns: true);
        const string matchingText = "resume-after-completed-batch";
        matchingDatabase.InsertResidualRow(1, matchingText, GzipTextPayloadCodec.Encode(matchingText));

        SqliteGzipTextMigrationReport matchingReport = SqliteGzipTextMigration.Execute(
            matchingDatabase.Path,
            [MigrationSpec]);

        SqliteGzipTextMigrationTableReport matchingTable = Assert.Single(matchingReport.Tables);
        Assert.Equal(0, matchingTable.MigratedRows);
        Assert.Equal(1, matchingTable.ResidualRowsCleared);
        Assert.Null(matchingDatabase.ExecuteScalar("SELECT legacy_text FROM PayloadLog WHERE id = 1;"));
        Assert.Equal(matchingText, matchingDatabase.LoadPayload(1));

        using var mismatchedDatabase = new TemporarySqliteDatabase();
        mismatchedDatabase.CreateLegacyPayloadTable(includeCompressedColumns: true);
        const string onlyValidText = "the-only-valid-copy";
        GzipTextPayload mismatchedPayload = GzipTextPayloadCodec.Encode("different-copy");
        mismatchedDatabase.InsertResidualRow(1, onlyValidText, mismatchedPayload);
        byte[] originalPayload = Assert.IsType<byte[]>(mismatchedDatabase.ExecuteScalar("SELECT payload_gzip FROM PayloadLog WHERE id = 1;"));

        InvalidDataException error = Assert.Throws<InvalidDataException>(() =>
            SqliteGzipTextMigration.Execute(mismatchedDatabase.Path, [MigrationSpec]));

        Assert.Contains("旧文本已保留", error.Message, StringComparison.Ordinal);
        Assert.Equal(onlyValidText, mismatchedDatabase.ExecuteScalarString("SELECT legacy_text FROM PayloadLog WHERE id = 1;"));
        Assert.Equal(originalPayload, Assert.IsType<byte[]>(mismatchedDatabase.ExecuteScalar("SELECT payload_gzip FROM PayloadLog WHERE id = 1;")));
        Assert.Equal("ok", SqliteFileMaintenance.QuickCheck(mismatchedDatabase.Path));
    }

    [Fact]
    public void VerifiedBackupIncludesCommittedWalRowsAndPassesQuickCheck()
    {
        using var database = new TemporarySqliteDatabase();
        using SqliteConnection source = database.OpenConnection();
        database.ExecuteNonQuery(source, "PRAGMA journal_mode = WAL;");
        database.ExecuteNonQuery(source, "CREATE TABLE Sample (id INTEGER PRIMARY KEY, value TEXT NOT NULL);");
        database.ExecuteNonQuery(source, "INSERT INTO Sample (id, value) VALUES (1, 'from-wal');");

        SqliteBackupFileResult result = SqliteFileMaintenance.CreateVerifiedBackup(
            database.Path,
            "Backups",
            "payload-log");

        Assert.True(File.Exists(result.FilePath));
        Assert.True(result.FileSizeBytes > 0);
        Assert.Equal("ok", result.IntegrityCheck);
        Assert.Equal("ok", SqliteFileMaintenance.QuickCheck(result.FilePath));
        Assert.False(File.Exists(result.FilePath + ".part"));
        using SqliteConnection backup = TemporarySqliteDatabase.OpenReadOnly(result.FilePath);
        Assert.Equal("from-wal", TemporarySqliteDatabase.ExecuteScalarString(backup, "SELECT value FROM Sample WHERE id = 1;"));
    }

    [Fact]
    public void VacuumReclaimsDeletedPagesAndLeavesDatabaseHealthy()
    {
        using var database = new TemporarySqliteDatabase();
        using (SqliteConnection connection = database.OpenConnection())
        {
            database.ExecuteNonQuery(connection, "CREATE TABLE LargePayload (id INTEGER PRIMARY KEY, payload BLOB NOT NULL);");
            database.ExecuteNonQuery(connection, "INSERT INTO LargePayload VALUES (1, zeroblob(2097152)), (2, zeroblob(2097152));");
            database.ExecuteNonQuery(connection, "DELETE FROM LargePayload;");
        }
        long freePagesBefore = database.ExecuteScalarInt64("PRAGMA freelist_count;");
        Assert.True(freePagesBefore > 0);

        SqliteVacuumResult result = SqliteFileMaintenance.VacuumAndCheck(database.Path);

        Assert.Equal("ok", result.IntegrityCheck);
        Assert.True(result.AfterBytes < result.BeforeBytes);
        Assert.Equal(0L, database.ExecuteScalarInt64("PRAGMA freelist_count;"));
        Assert.Equal("ok", SqliteFileMaintenance.QuickCheck(database.Path));
    }

    private static string CreateLargeText(int id) =>
        $"{{\"id\":{id},\"name\":\"测试🧪\",\"data\":\"{new string((char)('A' + id % 20), 4_096)}\"}}";

    private sealed class TemporarySqliteDatabase : IDisposable
    {
        private readonly string directoryPath;

        public TemporarySqliteDatabase()
        {
            directoryPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"ColorVision.SqliteGzipText.{Guid.NewGuid():N}");
            Directory.CreateDirectory(directoryPath);
            Path = System.IO.Path.Combine(directoryPath, "payload.db");
        }

        public string Path { get; }

        public SqlSugarClient CreateSugarClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={Path};Pooling=False",
                DbType = SqlSugar.DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
        }

        public SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = Path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false,
            }.ToString());
            connection.Open();
            return connection;
        }

        public static SqliteConnection OpenReadOnly(string path)
        {
            var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString());
            connection.Open();
            return connection;
        }

        public void CreateLegacyPayloadTable(bool includeCompressedColumns = false)
        {
            string compressedColumns = includeCompressedColumns
                ? ", payload_gzip BLOB NULL, payload_utf8_length INTEGER NULL, payload_preview TEXT NULL"
                : string.Empty;
            ExecuteNonQuery(
                $"CREATE TABLE PayloadLog (id INTEGER PRIMARY KEY AUTOINCREMENT, legacy_text TEXT NULL{compressedColumns});");
        }

        public void InsertLegacyRows(int count, Func<int, string> textFactory)
        {
            using SqliteConnection connection = OpenConnection();
            using SqliteTransaction transaction = connection.BeginTransaction();
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO PayloadLog (legacy_text) VALUES ($text);";
            SqliteParameter textParameter = command.Parameters.Add("$text", SqliteType.Text);
            for (int id = 1; id <= count; id++)
            {
                textParameter.Value = textFactory(id);
                command.ExecuteNonQuery();
            }
            transaction.Commit();
        }

        public void InsertResidualRow(long id, string legacyText, GzipTextPayload payload)
        {
            using SqliteConnection connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO PayloadLog (id, legacy_text, payload_gzip, payload_utf8_length) " +
                "VALUES ($id, $legacy, $payload, $length);";
            command.Parameters.AddWithValue("$id", id);
            command.Parameters.AddWithValue("$legacy", legacyText);
            command.Parameters.Add("$payload", SqliteType.Blob).Value = payload.CompressedBytes!;
            command.Parameters.AddWithValue("$length", payload.Utf8Length!.Value);
            command.ExecuteNonQuery();
        }

        public string? LoadPayload(long id)
        {
            using SqliteConnection connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText =
                "SELECT payload_gzip, payload_utf8_length FROM PayloadLog WHERE id = $id;";
            command.Parameters.AddWithValue("$id", id);
            using SqliteDataReader reader = command.ExecuteReader();
            Assert.True(reader.Read());
            return GzipTextPayloadCodec.Decode(
                reader.IsDBNull(0) ? null : reader.GetFieldValue<byte[]>(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1));
        }

        public Dictionary<string, string> QueryColumns(string tableName)
        {
            using SqliteConnection connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{tableName}\");";
            using SqliteDataReader reader = command.ExecuteReader();
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            while (reader.Read())
                result[reader.GetString(1)] = reader.GetString(2).ToUpperInvariant();
            return result;
        }

        public void ExecuteNonQuery(string sql)
        {
            using SqliteConnection connection = OpenConnection();
            ExecuteNonQuery(connection, sql);
        }

        public void ExecuteNonQuery(SqliteConnection connection, string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        public object? ExecuteScalar(string sql)
        {
            using SqliteConnection connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            object? value = command.ExecuteScalar();
            return value == DBNull.Value ? null : value;
        }

        public string ExecuteScalarString(string sql)
        {
            using SqliteConnection connection = OpenConnection();
            return ExecuteScalarString(connection, sql);
        }

        public static string ExecuteScalarString(SqliteConnection connection, string sql)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
        }

        public long ExecuteScalarInt64(string sql)
        {
            using SqliteConnection connection = OpenConnection();
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar());
        }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directoryPath))
                Directory.Delete(directoryPath, recursive: true);
        }
    }
}
