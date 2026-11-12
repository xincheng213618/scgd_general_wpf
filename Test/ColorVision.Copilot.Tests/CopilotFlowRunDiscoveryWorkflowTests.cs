using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Services.FlowDiagnostics;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.IO;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFlowRunDiscoveryWorkflowTests
{
    [Fact]
    public async Task RunsWithoutIncidentsCanBeDiscoveredAndUsedForExactHistoryEventAndAttemptEvidence()
    {
        using var fixture = new JournalFixture();
        var started = new DateTime(2026, 10, 27, 2, 0, 0, DateTimeKind.Utc);
        const string flowKey = "production-line-a";
        const string nodeId = "recorded-sensor-node";
        const string detailedError = "Sensor acknowledgement was not received after the acquisition request.";
        int earlierId;
        int targetId;
        long linkedAttemptId = 0;
        using (var database = fixture.Create())
        {
            earlierId = AddRun(database, flowKey, started, 1000);
            targetId = AddRun(database, flowKey, started.AddMinutes(1), 5000);
            var otherId = AddRun(database, "production-line-b", started.AddMinutes(2), 250);
            for (var invocation = 0; invocation <= 10; invocation++)
            {
                var attempt = new FlowNodeAttempt
                {
                    RunRecordId = targetId, NodeId = nodeId, AttemptNo = 1,
                    InvocationId = "private-invocation-" + Guid.NewGuid().ToString("N"),
                    StartedTimeUtc = started.AddMinutes(1).AddMilliseconds(invocation * 100),
                    CompletedTimeUtc = started.AddMinutes(1).AddMilliseconds(invocation * 100 + 20),
                    Outcome = invocation == 0 ? "Failed" : "Succeeded",
                    ErrorCode = invocation == 0 ? "AckTimeout" : null,
                    ErrorMessage = invocation == 0 ? detailedError : null,
                };
                var attemptId = database.Insertable(attempt).ExecuteReturnBigIdentity();
                if (invocation == 0) linkedAttemptId = attemptId;
            }
            database.Insertable(new FlowNodeAttempt
            {
                RunRecordId = targetId, NodeId = "other-recorded-node", AttemptNo = 1,
                InvocationId = Guid.NewGuid().ToString("N"), StartedTimeUtc = started.AddMinutes(1),
                Outcome = "Failed", ErrorMessage = "An unrelated node's diagnostic detail.",
            }).ExecuteCommand();
            database.Insertable(new[]
            {
                new FlowExecutionEvent
                {
                    RunRecordId = targetId, SequenceNo = 1, EventType = "NodeCompleted",
                    OccurredTimeUtc = started.AddMinutes(1).AddMilliseconds(20), Message = "Recorded node outcome.",
                    NodeId = nodeId, AttemptId = linkedAttemptId, Code = "AckTimeout",
                    DataJson = "private-event-payload-7319",
                },
                new FlowExecutionEvent
                {
                    RunRecordId = otherId, SequenceNo = 1, EventType = "RunCompleted",
                    OccurredTimeUtc = started.AddMinutes(2).AddMilliseconds(250), Message = "Other flow's event.",
                },
            }).ExecuteCommand();
        }
        var originalBytes = File.ReadAllBytes(fixture.DatabasePath);
        var discovery = new CopilotFlowRunQueryService(new FlowRunQueryReader(fixture.DatabasePath).ReadPageAsync);

        var recent = await discovery.ListAsync(null, 20, null, CancellationToken.None);
        Assert.True(recent.Success, recent.ErrorMessage);
        using var recentPage = JsonDocument.Parse(recent.Content);
        var candidates = recentPage.RootElement.GetProperty("runs").EnumerateArray().ToArray();
        Assert.Equal(3, candidates.Length);
        var discoveredFlowKey = candidates.Single(run => run.GetProperty("elapsed_ms").GetInt64() == 5000)
            .GetProperty("flow_key").GetString();
        Assert.Equal(flowKey, discoveredFlowKey);
        var first = await discovery.ListAsync(discoveredFlowKey, 1, null, CancellationToken.None);

        Assert.True(first.Success, first.ErrorMessage);
        using var firstPage = JsonDocument.Parse(first.Content);
        var discoveredId = Assert.Single(firstPage.RootElement.GetProperty("runs").EnumerateArray())
            .GetProperty("run_record_id").GetInt32();
        Assert.Equal(targetId, discoveredId);
        Assert.True(firstPage.RootElement.GetProperty("has_more").GetBoolean());
        var cursor = firstPage.RootElement.GetProperty("next_cursor").GetString();
        Assert.NotNull(cursor);
        var second = await discovery.ListAsync(discoveredFlowKey, 1, cursor, CancellationToken.None);
        Assert.True(second.Success, second.ErrorMessage);
        using var secondPage = JsonDocument.Parse(second.Content);
        Assert.Equal(earlierId, Assert.Single(secondPage.RootElement.GetProperty("runs").EnumerateArray())
            .GetProperty("run_record_id").GetInt32());
        Assert.False(secondPage.RootElement.GetProperty("has_more").GetBoolean());

        var history = await new CopilotFlowRunHistoryInspectionService(new FlowRunHistoryReader(fixture.DatabasePath).ReadAsync)
            .InspectAsync(discoveredId, nodeId, 10, CancellationToken.None);
        Assert.True(history.Success, history.ErrorMessage);
        using var historyPage = JsonDocument.Parse(history.Content);
        Assert.Equal(targetId, historyPage.RootElement.GetProperty("anchor").GetProperty("run_record_id").GetInt32());
        Assert.Equal(earlierId, Assert.Single(historyPage.RootElement.GetProperty("previous_runs").EnumerateArray())
            .GetProperty("run_record_id").GetInt32());
        var historyAnchor = historyPage.RootElement.GetProperty("anchor");
        Assert.True(historyAnchor.GetProperty("node_attempts_truncated").GetBoolean());
        Assert.Equal(10, historyAnchor.GetProperty("node_attempts").GetArrayLength());
        Assert.DoesNotContain(historyAnchor.GetProperty("node_attempts").EnumerateArray(),
            attempt => attempt.GetProperty("id").GetInt64() == linkedAttemptId);

        var events = await new CopilotFlowRunEventInspectionService(new FlowRunEventReader(fixture.DatabasePath).ReadPageAsync)
            .InspectAsync(discoveredId, 10, null, CancellationToken.None);
        Assert.True(events.Success, events.ErrorMessage);
        using var eventPage = JsonDocument.Parse(events.Content);
        var runEvent = Assert.Single(eventPage.RootElement.GetProperty("events").EnumerateArray());
        Assert.Equal(targetId, runEvent.GetProperty("run_record_id").GetInt32());
        Assert.Equal("Recorded node outcome.", runEvent.GetProperty("message").GetString());
        Assert.Equal(linkedAttemptId, runEvent.GetProperty("attempt_id").GetInt64());
        var recordedNodeId = runEvent.GetProperty("node_id").GetString();
        Assert.Equal(nodeId, recordedNodeId);
        var attempts = new CopilotFlowRunAttemptInspectionService(new FlowRunAttemptReader(fixture.DatabasePath).ReadPageAsync);
        var deliveredAttemptIds = new List<long>();
        var foundLinkedDetails = false;
        string? attemptCursor = null;
        do
        {
            var result = await attempts.InspectAsync(discoveredId, recordedNodeId, 4, attemptCursor, CancellationToken.None);
            Assert.True(result.Success, result.ErrorMessage);
            Assert.DoesNotContain("private-", result.Content, StringComparison.Ordinal);
            using var page = JsonDocument.Parse(result.Content);
            var rows = page.RootElement.GetProperty("attempts").EnumerateArray().ToArray();
            Assert.InRange(rows.Length, 1, 4);
            foreach (var row in rows)
            {
                var attemptId = row.GetProperty("attempt_id").GetInt64();
                deliveredAttemptIds.Add(attemptId);
                Assert.Equal(targetId, row.GetProperty("run_record_id").GetInt32());
                Assert.Equal(nodeId, row.GetProperty("node_id").GetString());
                Assert.Equal(1, row.GetProperty("attempt_no").GetInt32());
                if (attemptId != linkedAttemptId) continue;
                foundLinkedDetails = true;
                Assert.Equal("Failed", row.GetProperty("outcome").GetString());
                Assert.Equal(detailedError, row.GetProperty("error_message").GetString());
                Assert.True(row.GetProperty("duration_valid").GetBoolean());
                Assert.Equal(20, row.GetProperty("duration_ms").GetDouble());
            }
            var hasMore = page.RootElement.GetProperty("has_more").GetBoolean();
            if (attemptCursor == null)
            {
                Assert.True(hasMore);
                Assert.False(foundLinkedDetails); // The event references an attempt older than the newest window.
                Assert.NotEmpty(result.PartialResultMessage);
            }
            attemptCursor = page.RootElement.GetProperty("next_cursor").GetString();
            Assert.Equal(hasMore, attemptCursor != null);
        } while (attemptCursor != null);
        Assert.True(foundLinkedDetails);
        Assert.Equal(11, deliveredAttemptIds.Count);
        Assert.Equal(deliveredAttemptIds.Count, deliveredAttemptIds.Distinct().Count());
        foreach (var result in new[] { recent, first, second, history, events })
            Assert.DoesNotContain("private-", result.Content, StringComparison.Ordinal);
        Assert.Equal(originalBytes, File.ReadAllBytes(fixture.DatabasePath));
        Assert.Equal(new[] { "runs.db" }, Directory.GetFiles(fixture.Root).Select(Path.GetFileName));
    }

    private static int AddRun(SqlSugarClient database, string flowKey, DateTime started, long durationMilliseconds)
        => database.Insertable(new FlowRunRecord
        {
            FlowKey = flowKey, FlowName = "Shared production flow name", TemplateId = 42,
            RunKey = Guid.NewGuid().ToString("N"), Status = FlowStatus.Completed, FinalOutcome = FlowFinalOutcome.Succeeded,
            ElapsedMs = durationMilliseconds, StartedTimeUtc = started, CompletedTimeUtc = started.AddMilliseconds(durationMilliseconds),
            TemplateRevision = 7, ContentHash = new string('a', 64),
            SerialNumber = "private-shared-serial-7319", OwnerMachine = "private-owner-7319",
        }).ExecuteReturnIdentity();

    private sealed class JournalFixture : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("CopilotRunDiscovery-").FullName;
        public string DatabasePath => Path.Combine(Root, "runs.db");

        public SqlSugarClient Create()
        {
            var database = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString(),
                DbType = SqlSugar.DbType.Sqlite, IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute,
            });
            database.CodeFirst.InitTables<FlowRunRecord, FlowExecutionEvent, FlowNodeAttempt>();
            return database;
        }

        public void Dispose()
        {
            var absoluteRoot = Path.GetFullPath(Root);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetTempPath()), Path.GetDirectoryName(absoluteRoot), ignoreCase: true);
            Assert.StartsWith("CopilotRunDiscovery-", Path.GetFileName(absoluteRoot), StringComparison.Ordinal);
            Directory.Delete(absoluteRoot, recursive: true);
        }
    }
}
