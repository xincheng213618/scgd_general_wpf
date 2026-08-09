using ColorVision.Database;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using SqlSugar;
using System.Data;
using System.IO;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class FlowNodeMessageStorageTests : IDisposable
{
    private readonly string _tempDirectory =
        Directory.CreateTempSubdirectory("ColorVision-FlowDiagnostics-").FullName;

    private string DatabasePath =>
        Path.Combine(_tempDirectory, "FlowNodeRecords.db");

    [Fact]
    public void RuntimeStorageWritesOnlyGzipAndLoadsPayloadsById()
    {
        using SqlSugarClient db = CreateDbClient();
        FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
        db.Ado.ExecuteCommand(
            "ALTER TABLE \"FlowNodeMessage\" ADD COLUMN \"send_payload\" TEXT NULL;");
        db.Ado.ExecuteCommand(
            "ALTER TABLE \"FlowNodeMessage\" ADD COLUMN \"recv_payload\" TEXT NULL;");

        string sendPayload = "{\"command\":\"测试🙂\",\"values\":[1,2,3]}";
        string recvPayload = "{\"result\":\"完成\",\"ok\":true}";
        var message = new FlowNodeMessage
        {
            BatchId = 42,
            SerialNumber = "SN-42",
            NodeId = "node-1",
            NodeName = "发送消息",
            MsgId = "message-1",
            EventName = "Run",
            SendTopic = "flow/send",
            SendPayload = sendPayload,
            SendTime = new DateTime(2026, 8, 10, 3, 0, 0),
            RecvTopic = "flow/recv",
            RecvPayload = recvPayload,
            RecvTime = new DateTime(2026, 8, 10, 3, 0, 1),
            State = FlowMessageState.Success,
        };

        db.Ado.BeginTran();
        int id;
        try
        {
            id = db.Insertable(message).ExecuteReturnIdentity();
            FlowNodeMessagePayloadStorage.SaveSendPayload(db, id, sendPayload);
            FlowNodeMessagePayloadStorage.SaveRecvPayload(db, id, recvPayload);
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }

        DataRow stored = db.Ado.GetDataTable(
            "SELECT \"send_payload\",\"recv_payload\"," +
            "\"send_payload_gzip\",\"send_payload_utf8_length\"," +
            "\"recv_payload_gzip\",\"recv_payload_utf8_length\" " +
            "FROM \"FlowNodeMessage\" WHERE \"id\" = @id;",
            new SugarParameter("@id", id)).Rows[0];
        Assert.Equal(DBNull.Value, stored["send_payload"]);
        Assert.Equal(DBNull.Value, stored["recv_payload"]);
        Assert.NotEmpty(Assert.IsType<byte[]>(stored["send_payload_gzip"]));
        Assert.NotEmpty(Assert.IsType<byte[]>(stored["recv_payload_gzip"]));
        Assert.Equal(
            Encoding.UTF8.GetByteCount(sendPayload),
            Convert.ToInt32(stored["send_payload_utf8_length"]));
        Assert.Equal(
            Encoding.UTF8.GetByteCount(recvPayload),
            Convert.ToInt32(stored["recv_payload_utf8_length"]));

        FlowNodeMessage summary = db.Queryable<FlowNodeMessage>()
            .Where(item => item.Id == id)
            .Single();
        Assert.Null(summary.SendPayload);
        Assert.Null(summary.RecvPayload);

        FlowNodeMessagePayloads payloads =
            FlowNodeMessagePayloadStorage.LoadPayloads(db, id);
        Assert.Equal(sendPayload, payloads.SendPayload);
        Assert.Equal(recvPayload, payloads.RecvPayload);
    }

    [Fact]
    public void SchemaCreatesCompressedColumnsAndQueryIndexesWithoutMappingPayloads()
    {
        using SqlSugarClient db = CreateDbClient();

        FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
        FlowDiagnosticsSchemaMigrator.EnsureSchema(db);

        HashSet<string> messageColumns = db.DbMaintenance
            .GetColumnInfosByTableName("FlowNodeMessage")
            .Select(column => column.DbColumnName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("send_payload_gzip", messageColumns);
        Assert.Contains("send_payload_utf8_length", messageColumns);
        Assert.Contains("recv_payload_gzip", messageColumns);
        Assert.Contains("recv_payload_utf8_length", messageColumns);
        Assert.DoesNotContain("send_payload", messageColumns);
        Assert.DoesNotContain("recv_payload", messageColumns);

        string listSql = db.Queryable<FlowNodeMessage>().ToSqlString();
        Assert.DoesNotContain("send_payload", listSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("recv_payload", listSql, StringComparison.OrdinalIgnoreCase);

        HashSet<string> indexes = db.Ado.GetDataTable(
                "SELECT \"name\" FROM \"sqlite_master\" WHERE \"type\" = 'index';")
            .Rows
            .Cast<DataRow>()
            .Select(row => Convert.ToString(row["name"]) ?? string.Empty)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        string[] requiredIndexes =
        [
            "idx_flow_node_record_batch_time",
            "idx_flow_node_record_node_time",
            "idx_flow_node_message_batch_time",
            "idx_flow_node_message_send_time",
            "idx_flow_run_batch_started",
            "idx_flow_run_flow_key_completed",
        ];
        foreach (string indexName in requiredIndexes)
            Assert.Contains(indexName, indexes);
    }

    [Fact]
    public void LegacyMigrationClearsTextAndIsIdempotent()
    {
        using (SqlSugarClient db = CreateDbClient())
        {
            db.Ado.ExecuteCommand(
                "CREATE TABLE \"FlowNodeMessage\"(" +
                "\"id\" INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT," +
                "\"send_payload\" TEXT NULL," +
                "\"recv_payload\" TEXT NULL);");
            db.Ado.ExecuteCommand(
                "INSERT INTO \"FlowNodeMessage\"(" +
                "\"send_payload\",\"recv_payload\") VALUES " +
                "(@large,@reply),('',NULL),(NULL,NULL);",
                new SugarParameter("@large", new string('流', 800)),
                new SugarParameter("@reply", "{\"状态\":\"完成🙂\"}"));
        }

        SqliteGzipTextMigrationReport first =
            LegacyFlowNodeMessagePayloadMigration.Execute(DatabasePath);
        SqliteGzipTextMigrationReport second =
            LegacyFlowNodeMessagePayloadMigration.Execute(DatabasePath);

        Assert.Equal(2, first.Tables[0].MigratedRows);
        Assert.Equal(1, first.Tables[1].MigratedRows);
        Assert.All(second.Tables, table => Assert.Equal(0, table.MigratedRows));
        Assert.Equal("ok", first.IntegrityCheck);

        using SqlSugarClient verification = CreateDbClient();
        DataTable rows = verification.Ado.GetDataTable(
            "SELECT \"id\",\"send_payload\",\"recv_payload\" " +
            "FROM \"FlowNodeMessage\" ORDER BY \"id\";");
        Assert.All(rows.Rows.Cast<DataRow>(), row =>
        {
            Assert.Equal(DBNull.Value, row["send_payload"]);
            Assert.Equal(DBNull.Value, row["recv_payload"]);
        });
        Assert.Equal(
            new string('流', 800),
            FlowNodeMessagePayloadStorage.LoadPayloads(verification, 1).SendPayload);
        Assert.Equal(
            "{\"状态\":\"完成🙂\"}",
            FlowNodeMessagePayloadStorage.LoadPayloads(verification, 1).RecvPayload);
        Assert.Equal(
            string.Empty,
            FlowNodeMessagePayloadStorage.LoadPayloads(verification, 2).SendPayload);
        Assert.Null(
            FlowNodeMessagePayloadStorage.LoadPayloads(verification, 3).SendPayload);
    }

    [Fact]
    public void CorruptedPayloadIsReportedInsteadOfReturningEmptyText()
    {
        using SqlSugarClient db = CreateDbClient();
        FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
        int id = db.Insertable(new FlowNodeMessage
        {
            BatchId = 1,
            SendTime = DateTime.Now,
            State = FlowMessageState.Success,
        }).ExecuteReturnIdentity();
        db.Ado.ExecuteCommand(
            "UPDATE \"FlowNodeMessage\" SET " +
            "\"send_payload_gzip\" = @payload," +
            "\"send_payload_utf8_length\" = 12 WHERE \"id\" = @id;",
            new SugarParameter("@payload", new byte[] { 1, 2, 3, 4 }),
            new SugarParameter("@id", id));

        Assert.Throws<InvalidDataException>(() =>
            FlowNodeMessagePayloadStorage.LoadPayloads(db, id));
    }

    [Fact]
    public void MonthlyCleanupPreservesLegacyEvidenceForProtectedRuns()
    {
        using (SqlSugarClient db = CreateDbClient())
        {
            FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
            DateTime oldTime = new(2026, 5, 1, 8, 0, 0);
            int openRun = InsertRunGraph(
                db,
                "open",
                batchId: 100,
                serialNumber: "SN-OPEN",
                oldTime,
                FlowStatus.Completed,
                FlowIncidentStates.Open,
                null);
            int unresolvedTimestampRun = InsertRunGraph(
                db,
                "resolved-without-time",
                batchId: 101,
                serialNumber: "SN-NO-RESOLVED-TIME",
                oldTime.AddMinutes(1),
                FlowStatus.Completed,
                FlowIncidentStates.Resolved,
                null);
            int runningRun = InsertRunGraph(
                db,
                "running",
                batchId: 200,
                serialNumber: "SN-RUNNING",
                oldTime.AddMinutes(2),
                FlowStatus.Runing,
                null,
                null);
            _ = InsertRunGraph(
                db,
                "resolved",
                batchId: 100,
                serialNumber: "SN-RESOLVED",
                oldTime.AddMinutes(3),
                FlowStatus.Completed,
                FlowIncidentStates.Resolved,
                oldTime.AddMinutes(4));
            _ = InsertRunGraph(
                db,
                "no-incident",
                batchId: 200,
                serialNumber: "SN-COMPLETED",
                oldTime.AddMinutes(5),
                FlowStatus.Completed,
                null,
                null);

            Assert.True(openRun > 0);
            Assert.True(unresolvedTimestampRun > 0);
            Assert.True(runningRun > 0);
        }

        DatabaseCleanupExecutionResult result =
            FlowDiagnosticsSqliteCleanupProvider.CleanupHistoryCore(
                1,
                DatabasePath,
                new DateTime(2026, 8, 10, 12, 0, 0));

        using SqlSugarClient verification = CreateDbClient();
        List<FlowRunRecord> remainingRuns = verification.Queryable<FlowRunRecord>()
            .OrderBy(item => item.Id)
            .ToList();
        Assert.Equal(3, remainingRuns.Count);
        Assert.Equal(
            new string?[] { "open", "resolved-without-time", "running" },
            remainingRuns.Select(item => item.RunKey).ToArray());
        Assert.Equal(3, verification.Queryable<FlowExecutionEvent>().Count());
        Assert.Equal(3, verification.Queryable<FlowNodeAttempt>().Count());
        Assert.Equal(2, verification.Queryable<FlowIncident>().Count());
        Assert.Equal(
            new[] { "SN-NO-RESOLVED-TIME", "SN-OPEN", "SN-RUNNING" },
            verification.Queryable<FlowNodeRecord>()
                .OrderBy(item => item.SerialNumber)
                .Select(item => item.SerialNumber)
                .ToArray());
        List<FlowNodeMessage> remainingMessages =
            verification.Queryable<FlowNodeMessage>()
                .ToList()
                .OrderBy(item => item.SerialNumber, StringComparer.Ordinal)
                .ThenBy(item => item.Id)
                .ToList();
        Assert.Equal(6, remainingMessages.Count);
        Assert.Equal(
            new[]
            {
                "SN-NO-RESOLVED-TIME",
                "SN-NO-RESOLVED-TIME",
                "SN-OPEN",
                "SN-OPEN",
                "SN-RUNNING",
                "SN-RUNNING",
            },
            remainingMessages.Select(item => item.SerialNumber).ToArray());
        Assert.Equal(3, remainingMessages.Count(item => item.NodeRecordId.HasValue));
        Assert.Equal(3, remainingMessages.Count(item => !item.NodeRecordId.HasValue));
        Assert.Contains("FlowRunRecord: 删除 2 行", result.SummaryLines);
        Assert.Contains("FlowNodeRecord: 删除 2 行", result.SummaryLines);
        Assert.Contains("FlowNodeMessage: 删除 4 行", result.SummaryLines);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    private SqlSugarClient CreateDbClient()
    {
        return new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={DatabasePath};Default Timeout=30",
            DbType = SqlSugar.DbType.Sqlite,
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
        });
    }

    private static int InsertRunGraph(
        SqlSugarClient db,
        string runKey,
        int batchId,
        string serialNumber,
        DateTime completedTime,
        FlowStatus status,
        string? incidentState,
        DateTime? resolvedTimeUtc)
    {
        int runId = db.Insertable(new FlowRunRecord
        {
            TemplateId = 1,
            RunKey = runKey,
            BatchId = batchId,
            SerialNumber = serialNumber,
            Status = status,
            ElapsedMs = 100,
            CompletedTime = completedTime,
            CompletedTimeUtc = status == FlowStatus.Runing
                ? null
                : completedTime.ToUniversalTime(),
        }).ExecuteReturnIdentity();
        db.Insertable(new FlowExecutionEvent
        {
            RunRecordId = runId,
            SequenceNo = 1,
            EventKey = $"event-{runId}",
            EventType = "Completed",
            OccurredTimeUtc = completedTime.ToUniversalTime(),
        }).ExecuteCommand();
        db.Insertable(new FlowNodeAttempt
        {
            RunRecordId = runId,
            NodeId = "node",
            AttemptNo = 1,
            InvocationId = $"invocation-{runId}",
            StartedTimeUtc = completedTime.ToUniversalTime(),
            CompletedTimeUtc = completedTime.ToUniversalTime(),
            Outcome = "Completed",
        }).ExecuteCommand();
        if (incidentState != null)
        {
            db.Insertable(new FlowIncident
            {
                RunRecordId = runId,
                IncidentKey = $"incident-{runId}",
                Kind = "Test",
                Severity = "Error",
                State = incidentState,
                Summary = runKey,
                DetectedTimeUtc = completedTime.ToUniversalTime(),
                ResolvedTimeUtc = resolvedTimeUtc?.ToUniversalTime(),
            }).ExecuteCommand();
        }

        int nodeRecordId = db.Insertable(new FlowNodeRecord
        {
            BatchId = batchId,
            SerialNumber = serialNumber,
            NodeId = $"node-{runKey}",
            NodeName = runKey,
            NodeType = "Test",
            StartTime = completedTime.AddSeconds(-1),
            EndTime = completedTime,
            ElapsedMs = 1000,
        }).ExecuteReturnIdentity();
        db.Insertable(new FlowNodeMessage
        {
            BatchId = batchId,
            SerialNumber = serialNumber,
            NodeRecordId = nodeRecordId,
            NodeId = $"node-{runKey}",
            NodeName = runKey,
            SendTime = completedTime.AddMilliseconds(-500),
            RecvTime = completedTime,
            State = FlowMessageState.Success,
        }).ExecuteCommand();
        db.Insertable(new FlowNodeMessage
        {
            BatchId = batchId,
            SerialNumber = serialNumber,
            NodeRecordId = null,
            NodeId = $"unlinked-{runKey}",
            NodeName = runKey,
            SendTime = completedTime.AddMilliseconds(-500),
            RecvTime = completedTime,
            State = FlowMessageState.Success,
        }).ExecuteCommand();
        return runId;
    }
}
