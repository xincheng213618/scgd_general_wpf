using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using Microsoft.Data.Sqlite;
using SqlSugar;
using System.Diagnostics;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class FlowExecutionRecoveryTests
{
    private static readonly DateTime ProcessStartUtc =
        new(2026, 7, 31, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DeadSameMachineOwnerIsRecoveredAtomicallyAndOnlyOnce()
    {
        WithDatabase(db =>
        {
            var probe = new StubProcessProbe();
            FlowExecutionOwnerIdentity oldOwner =
                Owner("old-instance", "machine-a", 101, ProcessStartUtc);
            FlowExecutionOwnerIdentity currentOwner =
                Owner("current-instance", "machine-a", 202, ProcessStartUtc.AddHours(1));
            probe.Set(101, FlowOwnerProcessState.NotRunning);

            FlowRunRecord run = BeginRun(db, oldOwner, probe, "dead-run");
            FlowNodeAttempt openAttempt;
            using (var ownerJournal =
                new FlowExecutionJournal(db, oldOwner, probe))
            {
                openAttempt = ownerJournal.BeginAttempt(new FlowNodeAttempt
                {
                    RunRecordId = run.Id,
                    NodeId = "node-interrupted",
                    InvocationId = "invocation-interrupted",
                });
            }
            DateTime recoveredUtc = ProcessStartUtc.AddMinutes(10);
            using var recoveryJournal =
                new FlowExecutionJournal(db, currentOwner, probe);

            FlowRunRecoveryResult recovered =
                Assert.Single(recoveryJournal.RecoverAbandonedRuns(recoveredUtc));

            Assert.Equal(run.Id, recovered.Run.Id);
            Assert.Equal(FlowStatus.Failed, recovered.Run.Status);
            Assert.Equal(recoveredUtc, recovered.Run.CompletedTimeUtc);
            Assert.Equal(recoveredUtc, recovered.Run.RecoveredTimeUtc);
            Assert.Equal("OwnerProcessNotRunning", recovered.Run.RecoveryReason);
            Assert.Equal("RunRecovered", recovered.Event.EventType);
            Assert.Equal(1, recovered.Event.SequenceNo);
            Assert.Equal("ProcessInterrupted", recovered.Incident.Kind);
            FlowNodeAttempt interruptedAttempt =
                db.Queryable<FlowNodeAttempt>().InSingle(openAttempt.Id);
            Assert.Equal("Interrupted", interruptedAttempt.Outcome);
            Assert.Equal("ProcessInterrupted", interruptedAttempt.ErrorCode);
            Assert.Equal(recoveredUtc, interruptedAttempt.CompletedTimeUtc);

            Assert.Empty(recoveryJournal.RecoverAbandonedRuns(recoveredUtc.AddMinutes(1)));
            Assert.Equal(
                1,
                db.Queryable<FlowExecutionEvent>()
                    .Where(item => item.RunRecordId == run.Id)
                    .Count());
            Assert.Equal(
                1,
                db.Queryable<FlowIncident>()
                    .Where(item => item.RunRecordId == run.Id)
                    .Count());
        });
    }

    [Fact]
    public void LiveOrUnverifiableOwnerIsNeverRecovered()
    {
        WithDatabase(db =>
        {
            var probe = new StubProcessProbe();
            FlowExecutionOwnerIdentity liveOwner =
                Owner("live-instance", "machine-a", 301, ProcessStartUtc);
            FlowExecutionOwnerIdentity unknownOwner =
                Owner("unknown-instance", "machine-a", 302, ProcessStartUtc);
            FlowExecutionOwnerIdentity currentOwner =
                Owner("current-instance", "machine-a", 303, ProcessStartUtc.AddHours(1));
            probe.Set(301, FlowOwnerProcessState.Alive);
            probe.Set(302, FlowOwnerProcessState.Unknown);

            FlowRunRecord liveRun = BeginRun(db, liveOwner, probe, "live-run");
            FlowRunRecord unknownRun = BeginRun(db, unknownOwner, probe, "unknown-run");
            using var recoveryJournal =
                new FlowExecutionJournal(db, currentOwner, probe);

            Assert.Empty(recoveryJournal.RecoverAbandonedRuns());
            Assert.Equal(FlowStatus.Runing, LoadRun(db, liveRun.Id).Status);
            Assert.Equal(FlowStatus.Runing, LoadRun(db, unknownRun.Id).Status);
            Assert.Empty(db.Queryable<FlowExecutionEvent>().ToList());
            Assert.Empty(db.Queryable<FlowIncident>().ToList());
        });
    }

    [Fact]
    public void ReusedPidWithDifferentStartTimeRecoversOldOwner()
    {
        WithDatabase(db =>
        {
            var probe = new StubProcessProbe();
            FlowExecutionOwnerIdentity oldOwner =
                Owner("reused-pid-owner", "machine-a", 401, ProcessStartUtc);
            FlowExecutionOwnerIdentity currentOwner =
                Owner("current-instance", "machine-a", 402, ProcessStartUtc.AddHours(1));
            probe.Set(401, FlowOwnerProcessState.StartTimeMismatch);

            FlowRunRecord run = BeginRun(db, oldOwner, probe, "pid-reused-run");
            using var recoveryJournal =
                new FlowExecutionJournal(db, currentOwner, probe);

            FlowRunRecoveryResult recovered =
                Assert.Single(recoveryJournal.RecoverAbandonedRuns());

            Assert.Equal(run.Id, recovered.Run.Id);
            Assert.Equal(
                "OwnerProcessStartTimeMismatch",
                recovered.Run.RecoveryReason);
            Assert.Equal(FlowStatus.Failed, recovered.Run.Status);
        });
    }

    [Fact]
    public void RecoveryFailureRollsBackOnlyConflictingRunAndContinues()
    {
        WithDatabase(db =>
        {
            var probe = new StubProcessProbe();
            FlowExecutionOwnerIdentity oldOwner =
                Owner("conflict-owner", "machine-a", 451, ProcessStartUtc);
            FlowExecutionOwnerIdentity currentOwner =
                Owner("current-instance", "machine-a", 452, ProcessStartUtc.AddHours(1));
            probe.Set(451, FlowOwnerProcessState.NotRunning);

            FlowRunRecord run = BeginRun(db, oldOwner, probe, "conflict-run");
            FlowRunRecord healthyRun =
                BeginRun(db, oldOwner, probe, "healthy-run");
            using (var seedJournal =
                new FlowExecutionJournal(db, oldOwner, probe))
            {
                seedJournal.AppendEvent(new FlowExecutionEvent
                {
                    RunRecordId = run.Id,
                    EventKey = "run-recovered",
                    EventType = "ConflictingEvent",
                });
            }

            using var recoveryJournal =
                new FlowExecutionJournal(db, currentOwner, probe);
            FlowRunRecoveryResult recovered =
                Assert.Single(recoveryJournal.RecoverAbandonedRuns());
            Assert.Equal(healthyRun.Id, recovered.Run.Id);

            FlowRunRecord persisted = LoadRun(db, run.Id);
            Assert.Equal(FlowStatus.Runing, persisted.Status);
            Assert.Null(persisted.CompletedTimeUtc);
            Assert.Null(persisted.RecoveredTimeUtc);
            Assert.Equal(FlowStatus.Failed, LoadRun(db, healthyRun.Id).Status);
            Assert.Single(db.Queryable<FlowIncident>().ToList());
        });
    }

    [Fact]
    public void PersistedCurrentProcessStartTimeIsRecognizedAsAlive()
    {
        WithDatabase(db =>
        {
            using Process process = Process.GetCurrentProcess();
            DateTime actualStartedUtc = process.StartTime.ToUniversalTime();
            var processProbe = new SystemFlowProcessProbe();
            FlowExecutionOwnerIdentity persistedOwner = Owner(
                "persisted-current-process",
                Environment.MachineName,
                process.Id,
                actualStartedUtc);
            FlowExecutionOwnerIdentity recoveryOwner = Owner(
                "new-journal-instance",
                Environment.MachineName,
                process.Id,
                actualStartedUtc);

            FlowRunRecord run = BeginRun(
                db,
                persistedOwner,
                processProbe,
                "sqlite-process-roundtrip");
            FlowRunRecord roundTripped = LoadRun(db, run.Id);
            Assert.NotNull(roundTripped.OwnerProcessStartedUtc);
            DateTime persistedStartedUtc =
                roundTripped.OwnerProcessStartedUtc!.Value.Kind switch
                {
                    DateTimeKind.Utc =>
                        roundTripped.OwnerProcessStartedUtc.Value,
                    DateTimeKind.Local =>
                        roundTripped.OwnerProcessStartedUtc.Value.ToUniversalTime(),
                    _ => DateTime.SpecifyKind(
                        roundTripped.OwnerProcessStartedUtc.Value,
                        DateTimeKind.Utc),
                };
            Assert.True(
                (persistedStartedUtc - actualStartedUtc).Duration()
                <= SystemFlowProcessProbe.StartTimeTolerance);

            using var recoveryJournal =
                new FlowExecutionJournal(db, recoveryOwner, processProbe);
            Assert.Empty(recoveryJournal.RecoverAbandonedRuns());
            Assert.Equal(FlowStatus.Runing, LoadRun(db, run.Id).Status);
        });
    }

    [Fact]
    public void LegacyCrossMachineAndCurrentInstanceRunsAreNotAutoRecovered()
    {
        WithDatabase(db =>
        {
            var probe = new StubProcessProbe
            {
                DefaultState = FlowOwnerProcessState.NotRunning,
            };
            FlowExecutionOwnerIdentity crossMachineOwner =
                Owner("remote-instance", "machine-b", 501, ProcessStartUtc);
            FlowExecutionOwnerIdentity currentOwner =
                Owner("current-instance", "machine-a", 502, ProcessStartUtc.AddHours(1));

            FlowRunRecord legacy = InsertLegacyRunningRun(db, "legacy-run");
            FlowRunRecord remote =
                BeginRun(db, crossMachineOwner, probe, "remote-run");
            using var recoveryJournal =
                new FlowExecutionJournal(db, currentOwner, probe);
            FlowRunRecord current = BeginRun(
                recoveryJournal,
                "current-run",
                templateId: 42,
                flowKey: "flow-current");

            Assert.Empty(recoveryJournal.RecoverAbandonedRuns());
            Assert.Equal(0, probe.CallCount);
            Assert.Equal(FlowStatus.Runing, LoadRun(db, legacy.Id).Status);
            Assert.Equal(FlowStatus.Runing, LoadRun(db, remote.Id).Status);
            Assert.Equal(FlowStatus.Runing, LoadRun(db, current.Id).Status);
        });
    }

    [Fact]
    public void HeartbeatIsOwnerCheckedAndNeverMovesBackward()
    {
        WithDatabase(db =>
        {
            var probe = new StubProcessProbe();
            FlowExecutionOwnerIdentity owner =
                Owner("heartbeat-owner", "machine-a", 601, ProcessStartUtc);
            using var journal = new FlowExecutionJournal(db, owner, probe);
            FlowRunRecord run = BeginRun(
                journal,
                "heartbeat-run",
                templateId: 42,
                flowKey: "flow-heartbeat");
            DateTime newerHeartbeat = ProcessStartUtc.AddMinutes(4);

            FlowRunRecord updated =
                journal.HeartbeatRun(run.Id, newerHeartbeat);
            FlowRunRecord repeated =
                journal.HeartbeatRun(run.Id, newerHeartbeat.AddMinutes(-1));

            Assert.Equal(newerHeartbeat, updated.LastHeartbeatUtc);
            Assert.Equal(newerHeartbeat, repeated.LastHeartbeatUtc);
            Assert.Equal(owner.InstanceId, repeated.OwnerInstanceId);
            Assert.Equal(owner.MachineName, repeated.OwnerMachine);
            Assert.Equal(owner.ProcessId, repeated.OwnerProcessId);
            Assert.Equal(owner.ProcessStartedUtc, repeated.OwnerProcessStartedUtc);

            FlowExecutionOwnerIdentity otherOwner =
                Owner("other-instance", "machine-a", 602, ProcessStartUtc.AddHours(1));
            using var otherJournal =
                new FlowExecutionJournal(db, otherOwner, probe);
            Assert.Throws<InvalidOperationException>(
                () => otherJournal.HeartbeatRun(run.Id));
        });
    }

    [Fact]
    public void FlowKeyDeduplicatesAcrossReorderedTemplateIdsAndLegacyFallsBackToId()
    {
        WithDatabase(db =>
        {
            var probe = new StubProcessProbe();
            FlowExecutionOwnerIdentity owner =
                Owner("flow-key-owner", "machine-a", 701, ProcessStartUtc);
            using var journal = new FlowExecutionJournal(db, owner, probe);
            byte[] content = [83, 84, 78, 68, 1, 7, 0, 1];

            FlowRunRecord stableFirst = BeginRun(
                journal,
                "stable-1",
                templateId: 7,
                flowKey: "flow-stable",
                content);
            FlowRunRecord stableAfterReorder = BeginRun(
                journal,
                "stable-2",
                templateId: 99,
                flowKey: "flow-stable",
                content);
            FlowRunRecord legacyFirst = BeginRun(
                journal,
                "legacy-1",
                templateId: 7,
                flowKey: null,
                content);
            FlowRunRecord legacySameId = BeginRun(
                journal,
                "legacy-2",
                templateId: 7,
                flowKey: null,
                content);
            FlowRunRecord legacyOtherId = BeginRun(
                journal,
                "legacy-3",
                templateId: 99,
                flowKey: null,
                content);

            Assert.Equal(stableFirst.SnapshotId, stableAfterReorder.SnapshotId);
            Assert.Equal(7, stableFirst.TemplateId);
            Assert.Equal(99, stableAfterReorder.TemplateId);
            Assert.Equal("flow-stable", stableAfterReorder.FlowKey);
            Assert.Equal(legacyFirst.SnapshotId, legacySameId.SnapshotId);
            Assert.NotEqual(legacyFirst.SnapshotId, legacyOtherId.SnapshotId);
            Assert.Equal(3, db.Queryable<FlowTemplateSnapshot>().Count());
        });
    }

    private static FlowExecutionOwnerIdentity Owner(
        string instanceId,
        string machine,
        int processId,
        DateTime processStartedUtc)
    {
        return new FlowExecutionOwnerIdentity(
            instanceId,
            machine,
            processId,
            processStartedUtc);
    }

    private static FlowRunRecord BeginRun(
        SqlSugarClient db,
        FlowExecutionOwnerIdentity owner,
        IFlowProcessProbe probe,
        string runKey)
    {
        using var journal = new FlowExecutionJournal(db, owner, probe);
        return BeginRun(
            journal,
            runKey,
            templateId: 42,
            flowKey: $"flow-{runKey}");
    }

    private static FlowRunRecord BeginRun(
        FlowExecutionJournal journal,
        string runKey,
        int templateId,
        string? flowKey,
        byte[]? content = null)
    {
        content ??= [83, 84, 78, 68, 1, 9, 9, 9];
        FlowTemplateSnapshot snapshot = FlowTemplateSnapshotFactory.Create(
            templateId,
            content,
            flowKey: flowKey);
        return journal.BeginRun(snapshot, new FlowRunRecord
        {
            TemplateId = templateId,
            FlowKey = flowKey,
            FlowName = "Recovery test",
            SerialNumber = "SN-RECOVERY",
            BatchId = 20,
            RunKey = runKey,
            StartedTimeUtc = ProcessStartUtc.AddMinutes(1),
        });
    }

    private static FlowRunRecord InsertLegacyRunningRun(
        SqlSugarClient db,
        string runKey)
    {
        var run = new FlowRunRecord
        {
            TemplateId = 42,
            FlowName = "Legacy recovery test",
            RunKey = runKey,
            StartedTimeUtc = ProcessStartUtc,
            Status = FlowStatus.Runing,
            CompletedTime = ProcessStartUtc.ToLocalTime(),
        };
        run.Id = db.Insertable(run).ExecuteReturnIdentity();
        return run;
    }

    private static FlowRunRecord LoadRun(SqlSugarClient db, int runId)
    {
        return db.Queryable<FlowRunRecord>().InSingle(runId);
    }

    private static void WithDatabase(Action<SqlSugarClient> test)
    {
        string dbPath = Path.Combine(
            Path.GetTempPath(),
            $"colorvision-flow-recovery-{Guid.NewGuid():N}.db");
        SqlSugarClient? db = null;
        try
        {
            db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={dbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
            FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
            test(db);
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

    private sealed class StubProcessProbe : IFlowProcessProbe
    {
        private readonly Dictionary<int, FlowOwnerProcessState> states = [];

        public FlowOwnerProcessState DefaultState { get; init; } =
            FlowOwnerProcessState.Unknown;

        public int CallCount { get; private set; }

        public void Set(int processId, FlowOwnerProcessState state)
        {
            states[processId] = state;
        }

        public FlowOwnerProcessState GetState(
            int processId,
            DateTime expectedStartedUtc)
        {
            CallCount++;
            return states.TryGetValue(processId, out FlowOwnerProcessState state)
                ? state
                : DefaultState;
        }
    }
}
