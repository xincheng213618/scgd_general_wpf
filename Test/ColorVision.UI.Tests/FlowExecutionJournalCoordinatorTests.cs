using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PostProcess;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace ColorVision.UI.Tests;

public sealed class FlowExecutionJournalCoordinatorTests
{
    [Fact]
    public void ScopeRecordsHeartbeatAttemptIncidentAndFinalOutcome()
    {
        var journal = new RecordingJournal();
        using var coordinator = new FlowExecutionJournalCoordinator(
            () => journal,
            TimeSpan.FromMilliseconds(10));
        FlowTemplateSnapshot snapshot =
            FlowTemplateSnapshotFactory.Create(42, [83, 84, 78, 68, 1]);

        using FlowExecutionJournalScope scope =
            Assert.IsType<FlowExecutionJournalScope>(
                coordinator.TryBeginRun(snapshot, new FlowRunRecord
                {
                    TemplateId = 42,
                    FlowName = "Coordinator test",
                    SerialNumber = "SN-COORDINATOR",
                    RunKey = "coordinator-run",
                }));

        Assert.True(SpinWait.SpinUntil(
            () => Volatile.Read(ref journal.HeartbeatCount) > 0,
            TimeSpan.FromSeconds(1)));

        FlowNodeAttempt attempt = Assert.IsType<FlowNodeAttempt>(
            scope.TryBeginAttempt(
                "node-a",
                "invocation-a",
                legacyNodeRecordId: 17));
        scope.TryCompleteAttempt(
            attempt,
            "Failed",
            "NODE_FAILURE",
            "simulated");
        scope.TryCreateIncident(
            "node-a-failure",
            "NodeExecutionFailed",
            "Error",
            "simulated",
            "node-a",
            attempt.Id);

        Assert.True(scope.TryCompleteRun(
            FlowStatus.Failed,
            321,
            FlowFinalOutcome.Failed));
        int heartbeatCountAtCompletion =
            Volatile.Read(ref journal.HeartbeatCount);
        Thread.Sleep(40);

        Assert.Equal(
            heartbeatCountAtCompletion,
            Volatile.Read(ref journal.HeartbeatCount));
        Assert.Equal("invocation-a", journal.StartedAttempt?.InvocationId);
        Assert.Equal(17, journal.StartedAttempt?.LegacyNodeRecordId);
        Assert.Equal("Failed", journal.CompletedAttempt?.Outcome);
        Assert.Single(journal.Incidents);
        Assert.Contains(
            journal.Events,
            executionEvent => executionEvent.EventType == "RunStarted");
        Assert.Contains(
            journal.Events,
            executionEvent => executionEvent.EventType == "NodeStarted");
        Assert.Contains(
            journal.Events,
            executionEvent => executionEvent.EventType == "NodeCompleted");
        Assert.Equal(FlowFinalOutcome.Failed, journal.CompletedRun?.FinalOutcome);
    }

    [Fact]
    public void InitializationFailureReturnsQuicklyWithoutEscaping()
    {
        using var coordinator = new FlowExecutionJournalCoordinator(
            () => throw new InvalidOperationException("simulated init failure"));
        FlowTemplateSnapshot snapshot =
            FlowTemplateSnapshotFactory.Create(42, [83, 84, 78, 68, 1]);
        var stopwatch = Stopwatch.StartNew();

        FlowExecutionJournalScope? scope = coordinator.TryBeginRun(
            snapshot,
            new FlowRunRecord
            {
                TemplateId = 42,
                RunKey = "init-failure",
            });

        stopwatch.Stop();
        Assert.Null(scope);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void TransientCompletionFailureRetriesTheSameTerminalOutcome()
    {
        var journal = new RecordingJournal
        {
            CompleteRunFailuresBeforeSuccess = 1,
        };
        using var coordinator =
            new FlowExecutionJournalCoordinator(() => journal);
        using FlowExecutionJournalScope scope = BeginScope(
            coordinator,
            "transient-completion-failure");

        Assert.True(scope.TryCompleteRun(
            FlowStatus.Completed,
            654,
            FlowFinalOutcome.Succeeded));

        Assert.True(scope.IsCompletionRequested);
        Assert.Equal(2, journal.CompleteRunCallCount);
        Assert.Equal(FlowStatus.Completed, journal.CompletedRun?.Status);
        Assert.Equal(654, journal.CompletedRun?.ElapsedMs);
        Assert.Equal(
            FlowFinalOutcome.Succeeded,
            journal.CompletedRun?.FinalOutcome);

        // A repeated identical terminal request is idempotent and does not
        // touch the journal again.
        Assert.True(scope.TryCompleteRun(
            FlowStatus.Completed,
            654,
            FlowFinalOutcome.Succeeded));
        Assert.Equal(2, journal.CompleteRunCallCount);
    }

    [Fact]
    public void FailedCompletionStaysRetryableAndRejectsDefaultFailedOutcome()
    {
        var journal = new RecordingJournal
        {
            CompleteRunFailuresBeforeSuccess = 2,
        };
        using var coordinator =
            new FlowExecutionJournalCoordinator(() => journal);
        using FlowExecutionJournalScope scope = BeginScope(
            coordinator,
            "retryable-completion-failure");

        Assert.False(scope.TryCompleteRun(
            FlowStatus.Completed,
            777,
            FlowFinalOutcome.Succeeded));
        Assert.False(scope.IsCompletionRequested);
        Assert.Equal(2, journal.CompleteRunCallCount);

        // This mirrors FlowExecutionSession's defensive finally path. Once a
        // successful terminal tuple is known, a default Failed tuple can
        // neither replace it nor issue another database write.
        Assert.False(scope.TryCompleteRun(
            FlowStatus.Failed,
            777,
            FlowFinalOutcome.Failed));
        Assert.Equal(2, journal.CompleteRunCallCount);

        Assert.True(scope.TryCompleteRun(
            FlowStatus.Completed,
            777,
            FlowFinalOutcome.Succeeded));
        Assert.True(scope.IsCompletionRequested);
        Assert.Equal(3, journal.CompleteRunCallCount);
        Assert.Equal(FlowStatus.Completed, journal.CompletedRun?.Status);
        Assert.Equal(
            FlowFinalOutcome.Succeeded,
            journal.CompletedRun?.FinalOutcome);
    }

    [Fact]
    public void SynchronousLegacyWriteWaitIsBoundedWhenWriterNeverCompletes()
    {
        var completion = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = Stopwatch.StartNew();

        bool completed =
            FlowNodeRecordDataBaseHelper.TryGetSynchronousWriteResult(
                completion.Task,
                TimeSpan.FromMilliseconds(25),
                out int result);

        stopwatch.Stop();
        Assert.False(completed);
        Assert.Equal(0, result);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1));
    }

    private static FlowExecutionJournalScope BeginScope(
        FlowExecutionJournalCoordinator coordinator,
        string runKey)
    {
        FlowTemplateSnapshot snapshot =
            FlowTemplateSnapshotFactory.Create(42, [83, 84, 78, 68, 1]);
        return Assert.IsType<FlowExecutionJournalScope>(
            coordinator.TryBeginRun(snapshot, new FlowRunRecord
            {
                TemplateId = 42,
                FlowName = "Coordinator completion test",
                SerialNumber = "SN-COMPLETION",
                RunKey = runKey,
            }));
    }

    private sealed class RecordingJournal : IFlowExecutionJournal
    {
        private long nextId = 100;

        public int HeartbeatCount;

        public ConcurrentBag<FlowExecutionEvent> Events { get; } = [];

        public ConcurrentBag<FlowIncident> Incidents { get; } = [];

        public FlowNodeAttempt? StartedAttempt { get; private set; }

        public FlowNodeAttempt? CompletedAttempt { get; private set; }

        public FlowRunRecord? CompletedRun { get; private set; }

        public int CompleteRunFailuresBeforeSuccess { get; init; }

        public int CompleteRunCallCount { get; private set; }

        public FlowRunRecord BeginRun(
            FlowTemplateSnapshot snapshot,
            FlowRunRecord run)
        {
            run.Id = 11;
            run.Status = FlowStatus.Runing;
            run.StartedTimeUtc = DateTime.UtcNow;
            return run;
        }

        public FlowRunRecord HeartbeatRun(
            int runRecordId,
            DateTime? heartbeatTimeUtc = null)
        {
            Interlocked.Increment(ref HeartbeatCount);
            return new FlowRunRecord
            {
                Id = runRecordId,
                Status = FlowStatus.Runing,
                LastHeartbeatUtc = heartbeatTimeUtc ?? DateTime.UtcNow,
            };
        }

        public IReadOnlyList<FlowRunRecoveryResult> RecoverAbandonedRuns(
            DateTime? recoveredTimeUtc = null)
        {
            return [];
        }

        public FlowExecutionEvent AppendEvent(
            FlowExecutionEvent executionEvent)
        {
            executionEvent.Id = Interlocked.Increment(ref nextId);
            Events.Add(executionEvent);
            return executionEvent;
        }

        public FlowNodeAttempt BeginAttempt(FlowNodeAttempt attempt)
        {
            attempt.Id = Interlocked.Increment(ref nextId);
            attempt.AttemptNo = 1;
            StartedAttempt = attempt;
            return attempt;
        }

        public FlowNodeAttempt CompleteAttempt(
            long attemptId,
            string outcome,
            string? errorCode = null,
            string? errorMessage = null,
            DateTime? completedTimeUtc = null)
        {
            FlowNodeAttempt attempt = StartedAttempt
                ?? throw new InvalidOperationException();
            attempt.CompletedTimeUtc = completedTimeUtc ?? DateTime.UtcNow;
            attempt.Outcome = outcome;
            attempt.ErrorCode = errorCode;
            attempt.ErrorMessage = errorMessage;
            CompletedAttempt = attempt;
            return attempt;
        }

        public FlowIncident CreateIncident(FlowIncident incident)
        {
            incident.Id = Interlocked.Increment(ref nextId);
            Incidents.Add(incident);
            return incident;
        }

        public FlowRunRecord CompleteRun(
            int runRecordId,
            FlowStatus status,
            long elapsedMs,
            DateTime? completedTimeUtc = null,
            FlowFinalOutcome? finalOutcome = null)
        {
            CompleteRunCallCount++;
            if (CompleteRunCallCount <= CompleteRunFailuresBeforeSuccess)
                throw new InvalidOperationException("simulated completion failure");

            CompletedRun = new FlowRunRecord
            {
                Id = runRecordId,
                Status = status,
                FinalOutcome = finalOutcome,
                ElapsedMs = elapsedMs,
                CompletedTimeUtc = completedTimeUtc,
            };
            return CompletedRun;
        }

        public void Dispose()
        {
        }
    }
}
