using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.UI;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.IO;
using System.Security.Cryptography;

namespace ColorVision.UI.Tests;

public class FlowDiagnosticsSchemaTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SnapshotHashUsesStnBytesAndOwnsAStableCopy(bool useBase64)
    {
        byte[] content = [83, 84, 78, 68, 1, 10, 20, 30];
        DateTime captured = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

        FlowTemplateSnapshot snapshot = useBase64
            ? FlowTemplateSnapshotFactory.Create(42, Convert.ToBase64String(content), 7, captured, "flow-stable-key")
            : FlowTemplateSnapshotFactory.Create(42, content, 7, captured, "flow-stable-key");

        string expectedHash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        Assert.Equal(expectedHash, snapshot.ContentHash);
        Assert.Equal(content, snapshot.Content);
        Assert.Equal(content.Length, snapshot.ContentLength);
        Assert.Equal(captured, snapshot.CapturedTimeUtc);
        Assert.Equal("flow-stable-key", snapshot.FlowKey);

        content[0] = 0;
        Assert.Equal(83, snapshot.Content[0]);
    }

    [Fact]
    public void InvalidBase64IsRejected()
    {
        Assert.Throws<FormatException>(() =>
            FlowTemplateSnapshotFactory.Create(templateId: 1, "not-base64"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NodeWriterRetainsTimingOnlyWhenRequestedAfterPayloadFailure(bool preserveTiming)
    {
        string path = ConfigService.Instance.GetRequiredService<FlowNodeRecordConfig>().SqliteDbPath;
        using SqlSugarClient db = CreateDb(path);
        const int batch = 782_961;
        string serial = $"writer'校验-{Guid.NewGuid():N}";
        var startedAt = new DateTime(2026, 11, 27, 5, 3, 2).AddTicks(1234567);
        var finishedAt = startedAt.AddSeconds(2);
        FlowNodeRecord record = new()
        {
            BatchId = batch, SerialNumber = serial, NodeId = "node-1", NodeName = "采集'🧭",
            StartTime = startedAt, EndTime = finishedAt, ElapsedMs = 99,
        };
        FlowNodeMessage message = new()
        {
            BatchId = batch, SerialNumber = serial, MsgId = "MQTT'🧭", SendTime = startedAt, SendPayload = "",
            RecvTime = finishedAt, State = FlowMessageState.Fail,
        };

        int recordId = await FlowNodeRecordDataBaseHelper.InsertNodeExecutionAsync(record, message, preserveTiming);
        Assert.True(recordId > 0);
        Assert.Equal(recordId, message.NodeRecordId);
        var storedRecord = db.Queryable<FlowNodeRecord>().InSingle(recordId);
        Assert.Equal(batch, storedRecord.BatchId);
        Assert.Equal(serial, storedRecord.SerialNumber);
        Assert.Equal(record.NodeId, storedRecord.NodeId);
        Assert.Equal(record.NodeName, storedRecord.NodeName);
        Assert.Null(storedRecord.NodeType);
        Assert.Equal(startedAt, storedRecord.StartTime);
        Assert.Null(storedRecord.EndTime);
        Assert.Equal(0, storedRecord.ElapsedMs);
        var stored = db.Queryable<FlowNodeMessage>().InSingle(message.Id);
        Assert.Equal(message.MsgId, stored.MsgId);
        Assert.Equal(serial, stored.SerialNumber);
        Assert.Equal(startedAt, stored.SendTime);
        Assert.Null(stored.RecvTime);
        Assert.Equal(FlowMessageState.Sent, stored.State);
        Assert.Equal(new FlowNodeMessagePayloads("", null), FlowNodeMessagePayloadStorage.LoadPayloads(db, message.Id));

        record.EndTime = finishedAt;
        record.ElapsedMs = 3_000_000_000;
        message.RecvPayload = "\ud800"; // Strict UTF-8 rejects an unpaired surrogate after the metadata write.
        await FlowNodeRecordDataBaseHelper.UpdateNodeExecutionAsync(record, message, preserveTiming);
        storedRecord = db.Queryable<FlowNodeRecord>().InSingle(recordId);
        Assert.Equal(preserveTiming ? finishedAt : (DateTime?)null, storedRecord.EndTime);
        Assert.Equal(preserveTiming ? record.ElapsedMs : 0, storedRecord.ElapsedMs);
        stored = db.Queryable<FlowNodeMessage>().InSingle(message.Id);
        Assert.Null(stored.RecvTime);
        Assert.Equal(FlowMessageState.Sent, stored.State);

        message.RecvPayload = "接收 🧭";
        message.StatusCode = -1;
        record.NodeName = null!;
        record.NodeType = "CameraNode";
        await FlowNodeRecordDataBaseHelper.UpdateNodeExecutionAsync(record, message, preserveTiming);
        storedRecord = db.Queryable<FlowNodeRecord>().InSingle(recordId);
        Assert.Null(storedRecord.NodeName);
        Assert.Equal(record.NodeType, storedRecord.NodeType);
        Assert.Equal(finishedAt, storedRecord.EndTime);
        Assert.Equal(record.ElapsedMs, storedRecord.ElapsedMs);
        stored = db.Queryable<FlowNodeMessage>().InSingle(message.Id);
        Assert.Equal(finishedAt, stored.RecvTime);
        Assert.Equal(-1, stored.StatusCode);
        Assert.Equal(FlowMessageState.Fail, stored.State);
        Assert.Equal(new FlowNodeMessagePayloads("", message.RecvPayload), FlowNodeMessagePayloadStorage.LoadPayloads(db, message.Id));

        string failedSerial = serial + "-invalid-send";
        FlowNodeRecord failedRecord = new() { BatchId = batch, SerialNumber = failedSerial, StartTime = startedAt };
        FlowNodeMessage failedMessage = new()
        {
            BatchId = batch, SerialNumber = failedSerial, SendTime = startedAt, SendPayload = "\ud800",
        };
        int failedId = await FlowNodeRecordDataBaseHelper.InsertNodeExecutionAsync(failedRecord, failedMessage, preserveTiming);
        Assert.Equal(preserveTiming, failedId > 0);
        Assert.Equal(0, failedMessage.Id);
        Assert.Null(failedMessage.NodeRecordId);
        Assert.Equal(preserveTiming, db.Queryable<FlowNodeRecord>().Any(row => row.SerialNumber == failedSerial));
        Assert.False(db.Queryable<FlowNodeMessage>().Any(row => row.SerialNumber == failedSerial));
    }

    [Fact]
    public async Task ConfiguredReadersReuseNodeWriterSchemaDuringAnotherWrite()
    {
        string path = Path.GetFullPath(ConfigService.Instance.GetRequiredService<FlowNodeRecordConfig>().SqliteDbPath);
        string tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        Assert.StartsWith(tempRoot, path, StringComparison.OrdinalIgnoreCase);
        Assert.True(FlowNodeRecordDataBaseHelper.FlushPendingWrites(TimeSpan.FromSeconds(5)));

        var query = new FlowIncidentQuery { State = FlowIncidentStates.All };
        using var prepared = new FlowIncidentService();
        int committed = prepared.Query(query).TotalCount;

        using var writer = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        writer.Open();
        using var transaction = writer.BeginTransaction(deferred: false);
        using var command = writer.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO FlowIncident(run_record_id, kind, severity, state, summary, detected_time_utc)
            VALUES(1, 'NodeFailure', 'Error', 'Open', 'Uncommitted fixture', '2026-11-27 00:00:00');
            """;
        command.ExecuteNonQuery();

        Task<int> reader = Task.Run(() =>
        {
            using var journal = new FlowExecutionJournal();
            using var incidents = new FlowIncidentService();
            return incidents.Query(query).TotalCount;
        });
        Task completed = await Task.WhenAny(reader, Task.Delay(TimeSpan.FromSeconds(3)));
        transaction.Rollback();
        int actual = await reader;
        Assert.Same(reader, completed);
        Assert.Equal(committed, actual);
    }

    [Fact]
    public void SchemaMigrationUpgradesLegacyRunTableAndIsIdempotent()
    {
        string dbPath = Path.Combine(
            Path.GetTempPath(),
            $"colorvision-flow-schema-{Guid.NewGuid():N}.db");
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            db.Ado.ExecuteCommand(
                """
                CREATE TABLE FlowRunRecord (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    template_id INTEGER NOT NULL,
                    flow_name TEXT NULL,
                    serial_number TEXT NULL,
                    status INTEGER NOT NULL,
                    elapsed_ms INTEGER NOT NULL,
                    completed_time TEXT NOT NULL
                );
                """);

            FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
            FlowDiagnosticsSchemaMigrator.EnsureSchema(db);

            Assert.Equal("idx_flow_run_normalized_key_id", db.Ado.GetString("""
                SELECT name FROM sqlite_master
                WHERE type = 'index' AND tbl_name = 'FlowRunRecord' AND name = 'idx_flow_run_normalized_key_id'
                """));
            string legacySerialIndex = db.Ado.GetString("""
                SELECT sql FROM sqlite_master
                WHERE type = 'index' AND tbl_name = 'FlowRunRecord' AND name = 'idx_flow_run_legacy_serial'
                """);
            Assert.Contains("(serial_number COLLATE BINARY)", legacySerialIndex, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WHERE batch_id IS NULL AND serial_number IS NOT NULL", legacySerialIndex, StringComparison.OrdinalIgnoreCase);
            string attemptIndex = db.Ado.GetString("""
                SELECT sql FROM sqlite_master
                WHERE type = 'index' AND tbl_name = 'FlowNodeAttempt' AND name = 'idx_flow_node_attempt_run_node_number'
                """);
            Assert.Contains("(run_record_id, node_id, attempt_no DESC)", attemptIndex, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("idx_flow_node_attempt_run_node", db.Ado.GetString("""
                SELECT name FROM sqlite_master
                WHERE type = 'index' AND tbl_name = 'FlowNodeAttempt' AND name = 'idx_flow_node_attempt_run_node'
                """));
            string incidentIndex = db.Ado.GetString("""
                SELECT sql FROM sqlite_master
                WHERE type = 'index' AND tbl_name = 'FlowIncident' AND name = 'idx_flow_incident_detected_id_state'
                """);
            Assert.Contains("(detected_time_utc DESC, id DESC, state)", incidentIndex, StringComparison.OrdinalIgnoreCase);
            string messageIndex = db.Ado.GetString("""
                SELECT sql FROM sqlite_master
                WHERE type = 'index' AND tbl_name = 'FlowNodeMessage' AND name = 'idx_flow_node_message_batch_node'
                """);
            Assert.Contains("(batch_id, node_id)", messageIndex, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("WHERE batch_id IS NOT NULL AND node_id IS NOT NULL", messageIndex, StringComparison.OrdinalIgnoreCase);

            HashSet<string> columns = db.DbMaintenance
                .GetColumnInfosByTableName("FlowRunRecord")
                .Select(column => column.DbColumnName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("run_key", columns);
            Assert.Contains("batch_id", columns);
            Assert.Contains("started_time_utc", columns);
            Assert.Contains("flow_key", columns);
            Assert.Contains("owner_instance_id", columns);
            Assert.Contains("owner_machine", columns);
            Assert.Contains("owner_process_id", columns);
            Assert.Contains("owner_process_started_utc", columns);
            Assert.Contains("last_heartbeat_utc", columns);
            Assert.Contains("final_outcome", columns);
            Assert.Contains("template_revision", columns);
            Assert.Contains("content_hash", columns);
            Assert.Contains("snapshot_id", columns);
            Assert.Contains("completed_time_utc", columns);
            Assert.Contains("recovered_time_utc", columns);
            Assert.Contains("recovery_reason", columns);

            string[] requiredTables =
            [
                "FlowTemplateSnapshot",
                "FlowExecutionEvent",
                "FlowNodeAttempt",
                "FlowIncident",
            ];
            HashSet<string> tables = db.DbMaintenance
                .GetTableInfoList(false)
                .Select(table => table.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (string table in requiredTables)
                Assert.Contains(table, tables);

            HashSet<string> snapshotColumns = db.DbMaintenance
                .GetColumnInfosByTableName("FlowTemplateSnapshot")
                .Select(column => column.DbColumnName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("flow_key", snapshotColumns);

            var snapshot = FlowTemplateSnapshotFactory.Create(
                templateId: 42,
                [83, 84, 78, 68, 1],
                flowKey: "flow-schema");
            long snapshotId = db.Insertable(snapshot).ExecuteReturnBigIdentity();
            Assert.True(snapshotId > 0);
            Assert.Equal(snapshot.ContentHash, db.Queryable<FlowTemplateSnapshot>()
                .InSingle(snapshotId)
                .ContentHash);

            var run = new FlowRunRecord
            {
                TemplateId = 42,
                FlowKey = snapshot.FlowKey,
                FlowName = "Schema test",
                SerialNumber = "SN-1",
                BatchId = 9,
                RunKey = "run-1",
                StartedTimeUtc = DateTime.UtcNow,
                TemplateRevision = 3,
                ContentHash = snapshot.ContentHash,
                SnapshotId = snapshotId,
                Status = FlowStatus.Completed,
                ElapsedMs = 12,
                CompletedTime = DateTime.Now,
                CompletedTimeUtc = DateTime.UtcNow,
            };
            int runId = db.Insertable(run).ExecuteReturnIdentity();
            Assert.True(runId > 0);
            FlowRunRecord persistedRun =
                db.Queryable<FlowRunRecord>().InSingle(runId);
            Assert.Equal(3, persistedRun.TemplateRevision);
        }
        finally
        {
            db?.Ado.Close();
            db?.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    [Fact]
    public void LatestNodeDurationUsesCompletedPositiveRowsAndPreservesTiesAcrossBatches()
    {
        using var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = "Data Source=:memory:",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = false,
        });
        db.CodeFirst.InitTables<FlowNodeRecord>();
        db.Ado.ExecuteCommand("""
            INSERT INTO FlowNodeRecord (id, batch_id, node_id, start_time, end_time, elapsed_ms) VALUES
                (1, 1, 'node', '2026-10-01 08:00:01', '2026-10-01 08:00:08', 90),
                (2, 2, 'node', '2026-10-01 08:00:05', '2026-10-01 08:00:08', 30),
                (3, 3, 'node', '2026-10-01 08:00:05', '2026-10-01 08:00:08', 40),
                (4, 4, 'node', '2026-10-01 08:00:06', '2026-10-01 08:00:09', 0),
                (5, 5, 'node', '2026-10-01 08:00:07', '2026-10-01 08:00:10', -1),
                (6, 6, 'node', '2026-10-01 08:00:08', NULL, 70),
                (7, 7, 'quoted''node', '2026-10-01 08:00:08', '2026-10-01 08:00:10', 25),
                (8, 8, 'unfinished', '2026-10-01 08:00:08', NULL, 45);
            """);
        FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
        FlowDiagnosticsSchemaMigrator.EnsureSchema(db);

        string indexSql = db.Ado.GetString("SELECT sql FROM sqlite_master WHERE name = 'idx_flow_node_record_completed_end'");
        Assert.Contains("WHERE end_time IS NOT NULL AND elapsed_ms > 0", indexSql, StringComparison.OrdinalIgnoreCase);
        var requested = Enumerable.Range(0, 300).Select(index => $"missing-{index}")
            .Prepend("node").Append("quoted'node").Append("unfinished");
        var durations = new Dictionary<string, long>(StringComparer.Ordinal);
        FlowNodeRecordDataBaseHelper.ReadLatestCompletedDurations(db, requested, durations);
        Assert.Equal(2, durations.Count);
        Assert.Equal(30, durations["node"]);
        Assert.Equal(25, durations["quoted'node"]);
    }

    [Fact]
    public void LatestFlowDurationUsesBothIdentityBranchesAndKeepsTies()
    {
        using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = "Data Source=:memory:", DbType = DbType.Sqlite });
        FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
        var identity = new FlowIdentity(42, "plan'甲", "Flow");
        var time = new DateTime(2026, 11, 27, 10, 0, 0).AddTicks(1234567);

        void Add(int template, string? key, long elapsed, int seconds, FlowStatus status = FlowStatus.Completed)
        {
            db.Insertable(new FlowRunRecord
            {
                TemplateId = template, FlowKey = key, ElapsedMs = elapsed,
                CompletedTime = time.AddSeconds(seconds), Status = status,
            }).ExecuteCommand();
        }

        long ReadDuration() => FlowNodeRecordDataBaseHelper.ReadLastCompletedFlowElapsed(db, identity);
        long ReadWithOriginalOrdering() => FlowNodeRecordDataBaseHelper.ApplyFlowIdentityFilter(
                db.Queryable<FlowRunRecord>().Where(row => row.Status == FlowStatus.Completed && row.ElapsedMs > 0), identity)
            .OrderByDescending(row => row.CompletedTime).First().ElapsedMs;

        Add(42, "different", 600, 6);
        int reads = 0;
        db.Aop.OnLogExecuting = (_, _) => reads++;
        Assert.Equal(0, ReadDuration());
        db.Aop.OnLogExecuting = null;
        Assert.Equal(1, reads);

        Add(42, null, 100, 1);
        Add(42, "", 200, 2);
        Assert.Equal(200, ReadDuration());

        Add(43, identity.FlowKey, 3_000_000_000, 3);
        Add(42, " ", 500, 5);
        Add(42, identity.FlowKey, 700, 7, FlowStatus.Failed);
        Add(42, identity.FlowKey, 0, 8);
        Assert.Equal(3_000_000_000, ReadDuration());

        Add(42, null, 400, 4);
        Assert.Equal(400, ReadDuration());
        Add(42, identity.FlowKey, 405, 4);
        Assert.Equal(ReadWithOriginalOrdering(), ReadDuration());
        Add(42, identity.FlowKey, 901, 9);
        Add(43, identity.FlowKey, 902, 9);
        Assert.Equal(ReadWithOriginalOrdering(), ReadDuration());
    }

    [Fact]
    public void SchemaMigrationAddsIncidentAcknowledgementColumnsToLegacyTable()
    {
        string dbPath = Path.Combine(
            Path.GetTempPath(),
            $"colorvision-flow-incident-schema-{Guid.NewGuid():N}.db");
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            db.Ado.ExecuteCommand(
                """
                CREATE TABLE FlowIncident (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    run_record_id INTEGER NOT NULL,
                    incident_key TEXT NULL,
                    attempt_id INTEGER NULL,
                    node_id TEXT NULL,
                    kind TEXT NOT NULL,
                    severity TEXT NOT NULL,
                    state TEXT NOT NULL,
                    summary TEXT NOT NULL,
                    details_json TEXT NULL,
                    detected_time_utc TEXT NOT NULL,
                    resolved_time_utc TEXT NULL,
                    resolution TEXT NULL,
                    operator_name TEXT NULL
                );
                """);

            FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
            FlowDiagnosticsSchemaMigrator.EnsureSchema(db);

            HashSet<string> columns = db.DbMaintenance
                .GetColumnInfosByTableName("FlowIncident")
                .Select(column => column.DbColumnName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.Contains("acknowledged_time_utc", columns);
            Assert.Contains("acknowledged_operator", columns);
            Assert.Contains("acknowledgment_note", columns);
        }
        finally
        {
            db?.Ado.Close();
            db?.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    private static SqlSugarClient CreateDb(string dbPath)
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={dbPath}",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
        });
    }
}
