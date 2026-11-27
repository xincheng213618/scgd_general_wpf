using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PostProcess;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FlowExecutionJournalTests
{
    [Fact]
    public void BeginRunReusesSnapshotByTemplateAndContentHash()
    {
        WithJournal((db, journal) =>
        {
            byte[] content = [83, 84, 78, 68, 1, 10, 20, 30];

            FlowRunRecord first = BeginRun(
                journal,
                FlowTemplateSnapshotFactory.Create(42, content, templateRevision: 7),
                "run-snapshot-1");
            FlowRunRecord second = BeginRun(
                journal,
                FlowTemplateSnapshotFactory.Create(42, content, templateRevision: 8),
                "run-snapshot-2");

            Assert.NotEqual(first.Id, second.Id);
            Assert.Equal(first.SnapshotId, second.SnapshotId);
            Assert.Equal(1, db.Queryable<FlowTemplateSnapshot>().Count());
            Assert.Equal(2, db.Queryable<FlowRunRecord>().Count());

            FlowRunRecord retried = BeginRun(
                journal,
                FlowTemplateSnapshotFactory.Create(42, content, templateRevision: 7),
                "run-snapshot-1");
            Assert.Equal(first.Id, retried.Id);
            Assert.Equal(2, db.Queryable<FlowRunRecord>().Count());
        });
    }

    [Fact]
    public void BeginRunReusesKeyedSnapshotAcrossTemplateIdsAndChecksStoredBytes()
    {
        WithJournal((db, journal) =>
        {
            byte[] content = [83, 84, 78, 68, 0, 128, 255];
            FlowRunRecord first = BeginRun(journal,
                FlowTemplateSnapshotFactory.Create(42, content, templateRevision: 7, flowKey: "stable-flow"), "snapshot-first");
            FlowRunRecord second = BeginRun(journal,
                FlowTemplateSnapshotFactory.Create(43, content, flowKey: "stable-flow"), "snapshot-second");

            Assert.Equal(43, second.TemplateId);
            Assert.Equal(7, second.TemplateRevision);
            Assert.Equal(first.SnapshotId, second.SnapshotId);
            Assert.Equal(first.ContentHash, second.ContentHash);
            Assert.Equal(content, Assert.Single(db.Queryable<FlowTemplateSnapshot>().ToList()).Content);

            db.Ado.ExecuteCommand("UPDATE FlowTemplateSnapshot SET content = @content WHERE id = @id",
                new SugarParameter("@content", new byte[] { 83, 84, 78, 68, 0, 128, 254 }),
                new SugarParameter("@id", first.SnapshotId));

            Assert.Throws<InvalidOperationException>(() => BeginRun(journal,
                FlowTemplateSnapshotFactory.Create(44, content, flowKey: "stable-flow"), "snapshot-corrupt"));
            Assert.Equal(2, db.Queryable<FlowRunRecord>().Count());
        });
    }

    [Fact]
    public void AppendEventAllocatesMonotonicSequencePerRunAndDeduplicatesByKey()
    {
        WithJournal((db, journal) =>
        {
            FlowRunRecord firstRun = BeginRun(
                journal,
                FlowTemplateSnapshotFactory.Create(42, [83, 84, 78, 68, 1]),
                "run-events-1");
            FlowRunRecord secondRun = BeginRun(
                journal,
                FlowTemplateSnapshotFactory.Create(42, [83, 84, 78, 68, 1]),
                "run-events-2");

            FlowExecutionEvent first = journal.AppendEvent(new FlowExecutionEvent
            {
                RunRecordId = firstRun.Id,
                EventKey = "started",
                EventType = "RunStarted",
            });
            FlowExecutionEvent duplicate = journal.AppendEvent(new FlowExecutionEvent
            {
                RunRecordId = firstRun.Id,
                EventKey = "started",
                EventType = "RunStarted",
            });
            Assert.Throws<InvalidOperationException>(() => journal.AppendEvent(new FlowExecutionEvent
            {
                RunRecordId = firstRun.Id,
                EventKey = "started",
                EventType = "DifferentEvent",
            }));
            Assert.Throws<InvalidOperationException>(() => journal.AppendEvent(new FlowExecutionEvent
            {
                RunRecordId = int.MaxValue,
                EventKey = "missing",
                EventType = "RunStarted",
            }));
            FlowExecutionEvent second = journal.AppendEvent(new FlowExecutionEvent
            {
                RunRecordId = firstRun.Id,
                EventKey = "engine-completed",
                EventType = "EngineCompleted",
            });
            FlowExecutionEvent otherRunFirst = journal.AppendEvent(new FlowExecutionEvent
            {
                RunRecordId = secondRun.Id,
                EventKey = "started",
                EventType = "RunStarted",
            });

            Assert.Equal(first.Id, duplicate.Id);
            Assert.Equal(first.Id + 1, second.Id);
            Assert.Equal(1, first.SequenceNo);
            Assert.Equal(2, second.SequenceNo);
            Assert.Equal(1, otherRunFirst.SequenceNo);
            Assert.Equal(3, db.Queryable<FlowExecutionEvent>().Count());
        });
    }

    [Fact]
    public void AppendEventKeepsPayloadAndLargeIdsAfterRunCompletes()
    {
        WithJournal((db, journal) =>
        {
            FlowRunRecord run = BeginRun(
                journal, FlowTemplateSnapshotFactory.Create(42, [83, 84, 78, 68, 1]), "run-large-id");
            const int runId = int.MaxValue - 1;
            db.Ado.ExecuteCommand("UPDATE FlowRunRecord SET id = @large WHERE id = @original",
                new SugarParameter("@large", runId), new SugarParameter("@original", run.Id));
            DateTime occurred = new DateTime(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc).AddTicks(1234567);
            journal.CompleteRun(runId, FlowStatus.Completed, 10, occurred.AddMilliseconds(-1), FlowFinalOutcome.Succeeded);

            FlowExecutionEvent written = journal.AppendEvent(new FlowExecutionEvent
            {
                RunRecordId = runId,
                EventKey = "notice",
                EventType = "PostRunNotice",
                OccurredTimeUtc = occurred,
                NodeId = "中文节点",
                AttemptId = long.MaxValue - 1,
                Code = "ok",
                Message = "已完成",
                DataJson = """{"detail":"正常"}""",
            });
            FlowExecutionEvent persisted = db.Queryable<FlowExecutionEvent>().InSingle(written.Id);

            Assert.Equal(runId, persisted.RunRecordId);
            Assert.Equal(1, persisted.SequenceNo);
            Assert.Equal(occurred, persisted.OccurredTimeUtc);
            Assert.Equal(written.NodeId, persisted.NodeId);
            Assert.Equal(written.AttemptId, persisted.AttemptId);
            Assert.Equal(written.Code, persisted.Code);
            Assert.Equal(written.Message, persisted.Message);
            Assert.Equal(written.DataJson, persisted.DataJson);
            Assert.Equal(FlowStatus.Completed, db.Queryable<FlowRunRecord>().InSingle(runId).Status);
        });
    }

    [Fact]
    public void CompleteRunIsIdempotentAndRejectsConflictingTerminalResult()
    {
        WithJournal((db, journal) =>
        {
            FlowRunRecord run = BeginRun(
                journal,
                FlowTemplateSnapshotFactory.Create(42, [83, 84, 78, 68, 1]),
                "run-complete");
            DateTime completedUtc =
                new(2026, 7, 31, 12, 30, 0, DateTimeKind.Utc);

            FlowRunRecord first = journal.CompleteRun(
                run.Id,
                FlowStatus.Completed,
                elapsedMs: 1234,
                completedUtc,
                FlowFinalOutcome.Succeeded);
            FlowRunRecord repeated = journal.CompleteRun(
                run.Id,
                FlowStatus.Completed,
                elapsedMs: 1234,
                completedUtc.AddMinutes(1),
                FlowFinalOutcome.Succeeded);

            Assert.Equal(first.CompletedTimeUtc, repeated.CompletedTimeUtc);
            Assert.Equal(completedUtc, repeated.CompletedTimeUtc);
            Assert.Equal(FlowStatus.Completed, repeated.Status);
            Assert.Equal(FlowFinalOutcome.Succeeded, repeated.FinalOutcome);
            Assert.Equal(1234, repeated.ElapsedMs);
            Assert.Throws<InvalidOperationException>(() =>
                journal.CompleteRun(run.Id, FlowStatus.Failed, 1234));

            FlowRunRecord persisted = db.Queryable<FlowRunRecord>().InSingle(run.Id);
            Assert.Equal(FlowStatus.Completed, persisted.Status);
            Assert.Equal(FlowFinalOutcome.Succeeded, persisted.FinalOutcome);
            Assert.Equal(completedUtc, persisted.CompletedTimeUtc);
        });
    }

    [Fact]
    public void TerminalRunMarksAttemptsWithNoRecordedEndAndRejectsNewAttempts()
    {
        WithJournal((db, journal) =>
        {
            FlowTemplateSnapshot snapshot = FlowTemplateSnapshotFactory.Create(42, [83, 84, 78, 68, 1]);
            FlowRunRecord run = BeginRun(journal, snapshot, "run-terminal-with-open-attempt");
            FlowRunRecord otherRun = BeginRun(journal, snapshot, "run-still-active");
            FlowNodeAttempt missingEnd = journal.BeginAttempt(CreateAttempt(run.Id, "missing-end"));
            FlowNodeAttempt observed = journal.BeginAttempt(CreateAttempt(run.Id, "observed"));
            FlowNodeAttempt other = journal.BeginAttempt(CreateAttempt(otherRun.Id, "other"));
            FlowNodeAttempt completed = journal.CompleteAttempt(observed.Id, "Succeeded");

            journal.CompleteRun(run.Id, FlowStatus.Failed, 1234, finalOutcome: FlowFinalOutcome.Failed);

            FlowNodeAttempt interrupted = db.Queryable<FlowNodeAttempt>().InSingle(missingEnd.Id);
            Assert.Equal("Interrupted", interrupted.Outcome);
            Assert.Equal("NodeEndNotRecorded", interrupted.ErrorCode);
            Assert.Null(interrupted.CompletedTimeUtc);
            FlowNodeAttempt persistedCompleted = db.Queryable<FlowNodeAttempt>().InSingle(observed.Id);
            Assert.Equal(completed.CompletedTimeUtc, persistedCompleted.CompletedTimeUtc);
            Assert.Equal("Succeeded", persistedCompleted.Outcome);
            Assert.Null(db.Queryable<FlowNodeAttempt>().InSingle(other.Id).Outcome);

            Assert.Equal(missingEnd.Id, journal.BeginAttempt(CreateAttempt(run.Id, "missing-end")).Id);
            Assert.Throws<InvalidOperationException>(() => journal.BeginAttempt(CreateAttempt(run.Id, "late-start")));

            FlowNodeAttempt lateEnd = journal.CompleteAttempt(missingEnd.Id, "Canceled");
            Assert.Equal("Canceled", lateEnd.Outcome);
            Assert.NotNull(lateEnd.CompletedTimeUtc);
            Assert.Null(lateEnd.ErrorCode);
            journal.CompleteRun(run.Id, FlowStatus.Failed, 1234, finalOutcome: FlowFinalOutcome.Failed);
            Assert.Equal("Canceled", db.Queryable<FlowNodeAttempt>().InSingle(missingEnd.Id).Outcome);
        });

        static FlowNodeAttempt CreateAttempt(int runId, string invocationId) => new()
        {
            RunRecordId = runId,
            NodeId = "node-a",
            InvocationId = invocationId,
        };
    }

    [Fact]
    public void CompletionKeepsLegacyAttemptIdentityAndTimestampConversion()
    {
        WithJournal((db, journal) =>
        {
            FlowRunRecord run = BeginRun(journal, FlowTemplateSnapshotFactory.Create(42, [83, 84, 78, 68]), "legacy-attempt-date");
            const long attemptId = (long)int.MaxValue + 42;
            db.Ado.ExecuteCommand("""
                INSERT INTO FlowNodeAttempt (id, run_record_id, legacy_node_record_id,
                    node_id, attempt_no, invocation_id, started_time_utc)
                VALUES (@id, @runId, NULL, 'old-node', 8, 'old-invocation', '2026-11-26T10:00:00.1234567+09:00');
                """, new SugarParameter("@id", attemptId), new SugarParameter("@runId", run.Id));
            FlowNodeAttempt original = db.Queryable<FlowNodeAttempt>().InSingle(attemptId);
            DateTime terminalTime = new DateTime(2026, 11, 26, 14, 30, 0, DateTimeKind.Utc).AddTicks(1234567);
            journal.CompleteRun(run.Id, FlowStatus.Failed, 1000, terminalTime, FlowFinalOutcome.Failed);

            DateTime completedAt = terminalTime.AddTicks(42);
            FlowNodeAttempt completed = journal.CompleteAttempt(attemptId, "Failed", "", "结束 🧭", completedAt);
            FlowNodeAttempt retried = journal.CompleteAttempt(attemptId, "Failed", "", "结束 🧭", completedAt.AddSeconds(1));
            Assert.Equal(attemptId, completed.Id);
            Assert.Equal(run.Id, completed.RunRecordId);
            Assert.Equal(original.StartedTimeUtc, completed.StartedTimeUtc);
            Assert.Equal(original.StartedTimeUtc.Kind, completed.StartedTimeUtc.Kind);
            Assert.Equal(original.InvocationId, completed.InvocationId);
            Assert.Equal(original.AttemptNo, completed.AttemptNo);
            Assert.Null(completed.LegacyNodeRecordId);
            Assert.Equal(completedAt, retried.CompletedTimeUtc);
            Assert.Throws<InvalidOperationException>(() => journal.CompleteAttempt(attemptId, "Succeeded"));

            using var reader = db.Ado.GetDataReader("""
                SELECT @expectedStart, started_time_utc, error_code, error_message FROM FlowNodeAttempt WHERE id = @id;
                """,
                new SugarParameter("@expectedStart", original.StartedTimeUtc), new SugarParameter("@id", attemptId));
            Assert.True(reader.Read());
            Assert.Equal(reader.GetString(0), reader.GetString(1));
            Assert.Equal("", reader.GetString(2));
            Assert.Equal("结束 🧭", reader.GetString(3));
        });
    }

    [Fact]
    public void AttemptsAndIncidentsUseStableIdempotencyKeys()
    {
        WithJournal((db, journal) =>
        {
            FlowRunRecord run = BeginRun(
                journal,
                FlowTemplateSnapshotFactory.Create(42, [83, 84, 78, 68, 1]),
                "run-details");

            FlowNodeAttempt first = journal.BeginAttempt(new FlowNodeAttempt
            {
                RunRecordId = run.Id,
                NodeId = "node-a",
                InvocationId = "node-a-invocation-1",
            });
            FlowNodeAttempt duplicate = journal.BeginAttempt(new FlowNodeAttempt
            {
                RunRecordId = run.Id,
                NodeId = "node-a",
                InvocationId = "node-a-invocation-1",
            });
            Assert.Throws<InvalidOperationException>(() => journal.BeginAttempt(new FlowNodeAttempt
            {
                RunRecordId = run.Id,
                NodeId = "node-b",
                InvocationId = "node-a-invocation-1",
            }));
            FlowNodeAttempt second = journal.BeginAttempt(new FlowNodeAttempt
            {
                RunRecordId = run.Id,
                NodeId = "node-a",
                InvocationId = "node-a-invocation-2",
            });
            FlowNodeAttempt otherNode = journal.BeginAttempt(new FlowNodeAttempt
            {
                RunRecordId = run.Id,
                NodeId = "node-b",
                InvocationId = "node-b-invocation-1",
            });

            Assert.Equal(first.Id, duplicate.Id);
            Assert.Equal(first.Id + 1, second.Id);
            Assert.Equal(1, first.AttemptNo);
            Assert.Equal(2, second.AttemptNo);
            Assert.Equal(1, otherNode.AttemptNo);

            FlowNodeAttempt completed = journal.CompleteAttempt(
                first.Id,
                "Failed",
                "NODE_ERROR",
                "simulated");
            FlowNodeAttempt repeated = journal.CompleteAttempt(
                first.Id,
                "Failed",
                "NODE_ERROR",
                "simulated");
            Assert.Equal(completed.CompletedTimeUtc, repeated.CompletedTimeUtc);

            FlowIncident incident = journal.CreateIncident(new FlowIncident
            {
                RunRecordId = run.Id,
                IncidentKey = "node-a-failure",
                AttemptId = first.Id,
                NodeId = "node-a",
                Kind = "NodeFailure",
                Severity = "Error",
                Summary = "Node failed",
            });
            FlowIncident duplicateIncident = journal.CreateIncident(new FlowIncident
            {
                RunRecordId = run.Id,
                IncidentKey = "node-a-failure",
                AttemptId = first.Id,
                NodeId = "node-a",
                Kind = "NodeFailure",
                Severity = "Error",
                Summary = "Node failed",
            });

            Assert.Equal(incident.Id, duplicateIncident.Id);
            Assert.Equal(3, db.Queryable<FlowNodeAttempt>().Count());
            Assert.Equal(1, db.Queryable<FlowIncident>().Count());
        });
    }

    private static FlowRunRecord BeginRun(
        FlowExecutionJournal journal,
        FlowTemplateSnapshot snapshot,
        string runKey)
    {
        return journal.BeginRun(snapshot, new FlowRunRecord
        {
            TemplateId = snapshot.TemplateId,
            TemplateRevision = snapshot.TemplateRevision,
            FlowName = "Journal test",
            SerialNumber = "SN-1",
            BatchId = 9,
            RunKey = runKey,
        });
    }

    private static void WithJournal(Action<SqlSugarClient, FlowExecutionJournal> test)
    {
        string dbPath = Path.Combine(
            Path.GetTempPath(),
            $"colorvision-flow-journal-{Guid.NewGuid():N}.db");
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            using var journal = new FlowExecutionJournal(db);
            test(db, journal);
        }
        finally
        {
            if (db != null)
            {
                db.Ado.Close();
                db.Dispose();
            }
            SqliteConnection.ClearAllPools();
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
