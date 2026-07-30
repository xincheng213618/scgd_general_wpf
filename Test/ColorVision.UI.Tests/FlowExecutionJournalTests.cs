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
            Assert.Equal(1, first.SequenceNo);
            Assert.Equal(2, second.SequenceNo);
            Assert.Equal(1, otherRunFirst.SequenceNo);
            Assert.Equal(3, db.Queryable<FlowExecutionEvent>().Count());
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
            FlowNodeAttempt second = journal.BeginAttempt(new FlowNodeAttempt
            {
                RunRecordId = run.Id,
                NodeId = "node-a",
                InvocationId = "node-a-invocation-2",
            });

            Assert.Equal(first.Id, duplicate.Id);
            Assert.Equal(1, first.AttemptNo);
            Assert.Equal(2, second.AttemptNo);

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
            Assert.Equal(2, db.Queryable<FlowNodeAttempt>().Count());
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
