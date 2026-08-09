using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using SqlSugar;
using System.IO;
using System.Security.Cryptography;

namespace ColorVision.UI.Tests;

public class FlowDiagnosticsSchemaTests
{
    [Fact]
    public void SnapshotHashUsesDecodedStnBytesAndOwnsAStableCopy()
    {
        byte[] content = [83, 84, 78, 68, 1, 10, 20, 30];
        string dataBase64 = Convert.ToBase64String(content);
        DateTime captured = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

        FlowTemplateSnapshot snapshot = FlowTemplateSnapshotFactory.Create(
            templateId: 42,
            dataBase64,
            templateRevision: 7,
            capturedTimeUtc: captured,
            flowKey: "flow-stable-key");

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
