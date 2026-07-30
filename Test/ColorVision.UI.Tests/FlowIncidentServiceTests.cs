using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FlowIncidentServiceTests
{
    [Fact]
    public void QueryPagesActiveIncidentsAndHydratesStableRunIdentity()
    {
        WithService((db, service) =>
        {
            FlowRunRecord firstRun =
                InsertRun(db, "run-1", "flow-1", "First flow");
            FlowRunRecord secondRun =
                InsertRun(db, "run-2", "flow-2", "Second flow");
            FlowRunRecord resolvedRun =
                InsertRun(db, "run-3", "flow-3", "Resolved flow");
            InsertIncident(
                db,
                firstRun.Id,
                "Open",
                "Error",
                "NodeFailure",
                "Open failure",
                new DateTime(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc));
            InsertIncident(
                db,
                secondRun.Id,
                "Acknowledged",
                "Warning",
                "PostProcessFailure",
                "Acknowledged failure",
                new DateTime(2026, 7, 31, 11, 0, 0, DateTimeKind.Utc));
            InsertIncident(
                db,
                resolvedRun.Id,
                "Resolved",
                "Error",
                "NodeFailure",
                "Resolved failure",
                new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc));

            FlowIncidentPage firstPage = service.Query(
                new FlowIncidentQuery
                {
                    State = FlowIncidentStates.Active,
                    PageNumber = 1,
                    PageSize = 1,
                });

            Assert.Equal(2, firstPage.TotalCount);
            Assert.Equal(2, firstPage.TotalPages);
            FlowIncidentListItem first = Assert.Single(firstPage.Items);
            Assert.Equal(firstRun.Id, first.RunRecordId);
            Assert.Equal("flow-1", first.FlowIdentifier);
            Assert.Equal("run-1", first.RunIdentifier);
            Assert.Equal("First flow", first.FlowName);

            FlowIncidentPage warningPage = service.Query(
                new FlowIncidentQuery
                {
                    State = FlowIncidentStates.All,
                    Severity = "Warning",
                    SearchText = "Acknowledged",
                });
            FlowIncidentListItem warning = Assert.Single(warningPage.Items);
            Assert.Equal(secondRun.Id, warning.RunRecordId);
            Assert.Equal("Acknowledged", warning.State);
        });
    }

    [Fact]
    public void AcknowledgeAndResolvePersistOperatorNotesAndUtcTimes()
    {
        WithService((db, service) =>
        {
            FlowRunRecord run =
                InsertRun(db, "run-action", "flow-action", "Action flow");
            FlowIncident incident = InsertIncident(
                db,
                run.Id,
                "Open",
                "Error",
                "NodeFailure",
                "Action failure",
                DateTime.UtcNow);
            DateTime acknowledgedLocal =
                new(2026, 7, 31, 20, 0, 0, DateTimeKind.Local);
            DateTime resolvedUnspecified =
                new(2026, 7, 31, 13, 0, 0, DateTimeKind.Unspecified);

            FlowIncident acknowledged = service.Acknowledge(
                incident.Id,
                " operator ",
                " checked ",
                acknowledgedLocal);

            Assert.Equal(FlowIncidentStates.Acknowledged, acknowledged.State);
            Assert.Equal("operator", acknowledged.AcknowledgedOperator);
            Assert.Equal("checked", acknowledged.AcknowledgmentNote);
            Assert.Equal(
                acknowledgedLocal.ToUniversalTime(),
                acknowledged.AcknowledgedTimeUtc);

            FlowIncident resolved = service.Resolve(
                incident.Id,
                " resolver ",
                " replaced cable ",
                resolvedUnspecified);

            Assert.Equal(FlowIncidentStates.Resolved, resolved.State);
            Assert.Equal("resolver", resolved.OperatorName);
            Assert.Equal("replaced cable", resolved.Resolution);
            Assert.Equal(
                DateTime.SpecifyKind(resolvedUnspecified, DateTimeKind.Utc),
                resolved.ResolvedTimeUtc);
            Assert.Throws<InvalidOperationException>(() =>
                service.Acknowledge(incident.Id, "operator", null));
            Assert.Throws<ArgumentException>(() =>
                service.Resolve(incident.Id, "operator", " "));

            FlowIncident persisted =
                db.Queryable<FlowIncident>().InSingle(incident.Id);
            Assert.Equal(FlowIncidentStates.Resolved, persisted.State);
            Assert.Equal("checked", persisted.AcknowledgmentNote);
            Assert.Equal("replaced cable", persisted.Resolution);
        });
    }

    [Fact]
    public void DetailReturnsAssociatedRunEventsAttemptsAndLinkedAttempt()
    {
        WithService((db, service) =>
        {
            FlowRunRecord run =
                InsertRun(db, "run-detail", "flow-detail", "Detail flow");
            var attempt = new FlowNodeAttempt
            {
                RunRecordId = run.Id,
                NodeId = "node-a",
                AttemptNo = 1,
                InvocationId = "invocation-1",
                StartedTimeUtc = DateTime.UtcNow,
                Outcome = "Failed",
            };
            attempt.Id =
                db.Insertable(attempt).ExecuteReturnBigIdentity();
            var executionEvent = new FlowExecutionEvent
            {
                RunRecordId = run.Id,
                SequenceNo = 1,
                EventKey = "node-failed",
                EventType = "NodeFailed",
                OccurredTimeUtc = DateTime.UtcNow,
                NodeId = "node-a",
                AttemptId = attempt.Id,
            };
            executionEvent.Id =
                db.Insertable(executionEvent).ExecuteReturnBigIdentity();
            FlowIncident incident = InsertIncident(
                db,
                run.Id,
                "Open",
                "Error",
                "NodeFailure",
                "Detail failure",
                DateTime.UtcNow,
                attempt.Id,
                "node-a");

            FlowIncidentDetail detail = service.GetDetail(incident.Id);

            Assert.Equal(run.Id, detail.Run?.Id);
            Assert.Equal(executionEvent.Id, Assert.Single(detail.Events).Id);
            Assert.Equal(attempt.Id, Assert.Single(detail.Attempts).Id);
            Assert.Equal(attempt.Id, detail.LinkedAttempt?.Id);
        });
    }

    private static FlowRunRecord InsertRun(
        SqlSugarClient db,
        string runKey,
        string flowKey,
        string flowName)
    {
        var run = new FlowRunRecord
        {
            TemplateId = 42,
            FlowKey = flowKey,
            FlowName = flowName,
            RunKey = runKey,
            SerialNumber = "SN-1",
            BatchId = 9,
            Status = FlowStatus.Failed,
            CompletedTime = DateTime.Now,
            CompletedTimeUtc = DateTime.UtcNow,
        };
        run.Id = db.Insertable(run).ExecuteReturnIdentity();
        return run;
    }

    private static FlowIncident InsertIncident(
        SqlSugarClient db,
        int runRecordId,
        string state,
        string severity,
        string kind,
        string summary,
        DateTime detectedTimeUtc,
        long? attemptId = null,
        string? nodeId = null)
    {
        var incident = new FlowIncident
        {
            RunRecordId = runRecordId,
            IncidentKey = Guid.NewGuid().ToString("N"),
            AttemptId = attemptId,
            NodeId = nodeId,
            Kind = kind,
            Severity = severity,
            State = state,
            Summary = summary,
            DetectedTimeUtc = detectedTimeUtc,
        };
        incident.Id =
            db.Insertable(incident).ExecuteReturnBigIdentity();
        return incident;
    }

    private static void WithService(
        Action<SqlSugarClient, FlowIncidentService> test)
    {
        string dbPath = Path.Combine(
            Path.GetTempPath(),
            $"colorvision-flow-incident-{Guid.NewGuid():N}.db");
        SqlSugarClient? db = null;
        try
        {
            db = CreateDb(dbPath);
            using var service = new FlowIncidentService(db);
            test(db, service);
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
