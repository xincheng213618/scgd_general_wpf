using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.Messages;
using ColorVision.SocketProtocol;
using ColorVision.UI.Desktop.Feedback;
using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Security.Cryptography;

namespace ColorVision.UI.Tests;

public sealed class SqliteFeedbackCollectorTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("ColorVision-FeedbackSqlite-").FullName;
    private readonly List<string> outputs = [];
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.FromHours(8));
    private string SourcePath => Path.Combine(directory, "source #1.db");

    [Fact]
    public void CollectorsUseExistingSelectedSevenDayControls()
    {
        foreach (SqliteFeedbackCollector collector in new SqliteFeedbackCollector[]
                 { new FlowDiagnosticsFeedbackCollector(), new SocketMessageFeedbackCollector(), new MsgRecordFeedbackCollector() })
        {
            var item = new CollectorItem(collector);
            Assert.True(item.IsChecked);
            Assert.Equal(7, item.SelectedDays);
            Assert.Equal(new[] { 1, 3, 7, 14, 30 }, item.TimeRangeOptions.Select(option => option.Days));
            item.SelectedDays = 3;
            Assert.Equal(3, collector.RecentDays);
            Assert.False(collector is IFeedbackDiagnosticCleanupSource);
        }
    }

    [Theory]
    [InlineData(7, "1,2,3,4")]
    [InlineData(1, "2,3,4")]
    public void MqttExportUsesSendReceiveAndPersistenceTimesAndKeepsPayloads(int days, string expectedIds)
    {
        using (var source = Open(SourcePath))
            Execute(source, """
                CREATE TABLE MsgRecord (Id INTEGER PRIMARY KEY, SendTime TEXT, ReciveTime TEXT, CreateTime TEXT, UpdateTime TEXT, MsgID TEXT, MsgSendJson TEXT, MsgReturnJson TEXT, MsgRecordState INTEGER);
                INSERT INTO MsgRecord VALUES
                  (1, '2026-09-09 12:00:00', NULL, NULL, NULL, 'send-boundary', '{"request":1}', NULL, 1),
                  (2, '2026-09-01 12:00:00', '2026-09-16 11:00:00', NULL, NULL, 'recent-response', '{"request":2}', '{"Code":0,"Data":"完整响应"}', 2),
                  (3, '0001-01-01', '0001-01-01', NULL, '2026-09-16 11:00:00', 'timeout', '{"request":3}', NULL, 4),
                  (4, NULL, NULL, '2026-09-16 11:00:00', NULL, 'created', '{"request":4}', NULL, 0),
                  (5, '2026-09-01 12:00:00', NULL, NULL, NULL, 'old', NULL, NULL, 0),
                  (6, '2026-09-17 12:00:00', NULL, NULL, NULL, 'future', NULL, NULL, 0);
                """);
        var files = Collect(new MsgRecordFeedbackCollector { RecentDays = days });
        Assert.True(ReadStatus(files).Value<string>("Error") == null, ReadStatus(files).ToString());
        using var result = Open(files.Single(file => file.EntryPath == "Database/MsgRecords.db").FilePath);
        Assert.Equal(expectedIds, Scalar(result, "SELECT group_concat(Id) FROM MsgRecord;"));
        Assert.Equal("{\"Code\":0,\"Data\":\"完整响应\"}", Scalar(result, "SELECT MsgReturnJson FROM MsgRecord WHERE Id=2;"));
        Assert.Equal("4", Scalar(result, "SELECT MsgRecordState FROM MsgRecord WHERE Id=3;"));
        using var original = Open(SourcePath);
        Assert.Equal("6", Scalar(original, "SELECT COUNT(*) FROM MsgRecord;"));
    }

    [Theory]
    [InlineData(7, "1,2,3")]
    [InlineData(1, "3")]
    public void SocketExportFiltersRecordTimesAndPreservesWalPayloads(int days, string expectedIds)
    {
        using var source = Open(SourcePath);
        Execute(source, """
            PRAGMA journal_mode=WAL;
            PRAGMA wal_autocheckpoint=0;
            CREATE TABLE SocketMessage (Id INTEGER PRIMARY KEY, MessageTime TEXT, Content TEXT, ContentGzip BLOB);
            PRAGMA wal_checkpoint(TRUNCATE);
            INSERT INTO SocketMessage VALUES
                (1, '2026-09-09 12:00:00', 'lower boundary', X'010203'),
                (2, '2026-09-10 12:00:00', 'recent', X'040506'),
                (3, '2026-09-16 12:00:00', 'upper boundary', X'070809'),
                (4, '2026-09-09 11:59:59', 'old', X'10'),
                (5, '2026-09-16 12:00:01', 'future', X'11');
            """);
        byte[] before = HashSharedFile(SourcePath);
        Assert.True(new FileInfo(SourcePath + "-wal").Length > 0);

        var files = Collect(new SocketMessageFeedbackCollector { RecentDays = days });
        Assert.True(ReadStatus(files).Value<string>("Error") == null, ReadStatus(files).ToString());
        using var result = Open(files.Single(file => file.EntryPath.EndsWith(".db")).FilePath);
        Assert.Equal(expectedIds, Scalar(result, "SELECT group_concat(Id) FROM SocketMessage ORDER BY Id;"));
        Assert.Equal("070809", Scalar(result, "SELECT hex(ContentGzip) FROM SocketMessage WHERE Id=3;"));
        Assert.Equal("upper boundary", Scalar(result, "SELECT Content FROM SocketMessage WHERE Id=3;"));
        Assert.Equal("ok", Scalar(result, "PRAGMA quick_check;"));
        Assert.Equal("5", Scalar(source, "SELECT COUNT(*) FROM SocketMessage;"));
        Assert.Equal(before, HashSharedFile(SourcePath));
        Assert.Equal("ok", ReadStatus(files).Value<string>("Status"));
    }

    [Fact]
    public void ExportUsesOneSourceSnapshotAndRejectsSourceWrites()
    {
        using var source = Open(SourcePath);
        Execute(source, "PRAGMA journal_mode=WAL; CREATE TABLE Record (Id INTEGER PRIMARY KEY); INSERT INTO Record VALUES (1);");
        string target = Path.Combine(directory, "snapshot.db");
        using (var export = new SqliteFeedbackExport(SourcePath, target, Now, 7, new(), new()))
        {
            Execute(source, "INSERT INTO Record VALUES (2);");
            // Even faulty provider SQL cannot write to the attached source.
            SqliteException exception = Assert.Throws<SqliteException>(() =>
                export.ExportTable("Record", "1; DELETE FROM source.Record;"));
            Assert.Equal(8, exception.SqliteErrorCode);
            export.Complete();
        }
        using var result = Open(target);
        Assert.Equal("1", Scalar(result, "SELECT group_concat(Id) FROM Record;"));
        Assert.Equal("1,2", Scalar(source, "SELECT group_concat(Id) FROM Record;"));
    }

    [Fact]
    public void FlowExportKeepsLinkedEvidenceAndUsesUtcForModernRecords()
    {
        using (var source = Open(SourcePath))
            Execute(source, """
                CREATE TABLE FlowRunRecord (id INTEGER PRIMARY KEY, batch_id INTEGER, serial_number TEXT, completed_time TEXT, started_time_utc TEXT, snapshot_id INTEGER);
                CREATE TABLE FlowNodeRecord (id INTEGER PRIMARY KEY, batch_id INTEGER, serial_number TEXT, start_time TEXT, end_time TEXT, elapsed_ms INTEGER);
                CREATE TABLE FlowNodeMessage (id INTEGER PRIMARY KEY, batch_id INTEGER, serial_number TEXT, node_record_id INTEGER, send_time TEXT, recv_time TEXT, send_payload_gzip BLOB);
                CREATE TABLE FlowExecutionEvent (id INTEGER PRIMARY KEY, run_record_id INTEGER, occurred_time_utc TEXT);
                CREATE TABLE FlowNodeAttempt (id INTEGER PRIMARY KEY, run_record_id INTEGER, started_time_utc TEXT, legacy_node_record_id INTEGER);
                CREATE TABLE FlowIncident (id INTEGER PRIMARY KEY, run_record_id INTEGER, detected_time_utc TEXT);
                CREATE TABLE FlowTemplateSnapshot (id INTEGER PRIMARY KEY, content BLOB);
                INSERT INTO FlowRunRecord VALUES
                  (1, 11, 'new', '2026-09-16 11:00:00', NULL, 100),
                  (2, 12, 'boundary', '2026-09-08 12:00:00', '2026-09-09 04:00:00', 100),
                  (3, 11, 'old-other-sn', '2026-09-01 12:00:00', NULL, 101),
                  (4, 13, NULL, '2026-09-01 12:00:00', NULL, 100);
                INSERT INTO FlowNodeRecord VALUES
                  (10, 11, 'new', '2026-09-08 12:00:00', NULL, 18000),
                  (11, 11, 'old-other-sn', '2026-09-08 12:00:00', NULL, 20000),
                  (12, 13, NULL, '2026-09-16 11:00:00', NULL, 18000);
                INSERT INTO FlowNodeMessage VALUES
                  (20, 11, 'new', 10, '2026-09-08 12:00:00', NULL, X'010203'),
                  (21, 11, 'old-other-sn', 11, '2026-09-08 12:00:00', NULL, X'04');
                INSERT INTO FlowExecutionEvent VALUES (30, 1, '2026-09-08 12:00:00'), (31, 3, '2026-09-08 12:00:00');
                INSERT INTO FlowNodeAttempt VALUES (40, 1, '2026-09-08 12:00:00', 10);
                INSERT INTO FlowIncident VALUES (50, 1, '2026-09-08 12:00:00');
                INSERT INTO FlowTemplateSnapshot VALUES (100, X'0102'), (101, X'0304');
                """);
        var files = Collect(new FlowDiagnosticsFeedbackCollector());
        Assert.True(ReadStatus(files).Value<string>("Error") == null, ReadStatus(files).ToString());
        using var result = Open(files.Single(file => file.EntryPath.EndsWith(".db")).FilePath);
        Assert.Equal("1,2,4", Scalar(result, "SELECT group_concat(id) FROM FlowRunRecord;"));
        Assert.Equal("10,12", Scalar(result, "SELECT group_concat(id) FROM FlowNodeRecord;"));
        Assert.Equal("20", Scalar(result, "SELECT group_concat(id) FROM FlowNodeMessage;"));
        Assert.Equal("010203", Scalar(result, "SELECT hex(send_payload_gzip) FROM FlowNodeMessage;"));
        Assert.Equal("30", Scalar(result, "SELECT group_concat(id) FROM FlowExecutionEvent;"));
        Assert.Equal("40", Scalar(result, "SELECT group_concat(id) FROM FlowNodeAttempt;"));
        Assert.Equal("50", Scalar(result, "SELECT group_concat(id) FROM FlowIncident;"));
        Assert.Equal("100", Scalar(result, "SELECT group_concat(id) FROM FlowTemplateSnapshot;"));
        Assert.Equal("ok", ReadStatus(files).Value<string>("Status"));
    }

    [Fact]
    public void LegacyFlowDatabaseKeepsNodesLinkedToRecentMessagesWithoutMigrating()
    {
        using (var source = Open(SourcePath))
            Execute(source, """
                CREATE TABLE FlowNodeRecord (id INTEGER PRIMARY KEY, batch_id INTEGER, serial_number TEXT, start_time TEXT);
                CREATE TABLE FlowNodeMessage (id INTEGER PRIMARY KEY, batch_id INTEGER, serial_number TEXT, node_record_id INTEGER, send_time TEXT, recv_time TEXT, send_payload TEXT);
                INSERT INTO FlowNodeRecord VALUES (1, 1, 'old', '2026-09-01 12:00:00');
                INSERT INTO FlowNodeMessage VALUES (1, 1, 'old', 1, '2026-09-01 12:00:00', '2026-09-16 11:00:00', 'legacy payload');
                """);
        var files = Collect(new FlowDiagnosticsFeedbackCollector());
        Assert.True(ReadStatus(files).Value<string>("Error") == null, ReadStatus(files).ToString());
        using var result = Open(files.Single(file => file.EntryPath.EndsWith(".db")).FilePath);
        Assert.Equal("1", Scalar(result, "SELECT id FROM FlowNodeRecord;"));
        Assert.Equal("legacy payload", Scalar(result, "SELECT send_payload FROM FlowNodeMessage;"));
        Assert.Equal("partial", ReadStatus(files).Value<string>("Status"));
        using var original = Open(SourcePath);
        Assert.Equal("2", Scalar(original, "SELECT COUNT(*) FROM sqlite_schema WHERE type='table';"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrUnfilterableDatabaseReturnsErrorReportAndNoDatabase(bool createInvalidSchema)
    {
        if (createInvalidSchema)
        {
            using var source = Open(SourcePath);
            Execute(source, "CREATE TABLE SocketMessage (Id INTEGER PRIMARY KEY, Secret TEXT); INSERT INTO SocketMessage VALUES (1, 'do not export');");
        }
        var files = Collect(new SocketMessageFeedbackCollector());
        Assert.Single(files);
        Assert.Equal("error", ReadStatus(files).Value<string>("Status"));
        Assert.Equal(createInvalidSchema, File.Exists(SourcePath));
    }

    private IReadOnlyList<(string EntryPath, string FilePath)> Collect(SqliteFeedbackCollector collector)
    {
        var files = collector.CollectFiles(SourcePath, Now);
        outputs.AddRange(files.Select(file => file.FilePath));
        return files;
    }

    private static JObject ReadStatus(IReadOnlyList<(string EntryPath, string FilePath)> files) =>
        JObject.Parse(File.ReadAllText(files.Single(file => file.EntryPath.EndsWith(".json")).FilePath));

    private static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        return connection;
    }

    private static byte[] HashSharedFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return SHA256.HashData(stream);
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static string Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(command.ExecuteScalar())!;
    }

    public void Dispose()
    {
        foreach (string path in outputs) File.Delete(path);
        Directory.Delete(directory, recursive: true);
    }
}
