using ColorVision.UI;
using ColorVision.Engine.FlowProcessing.PostProcess;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal sealed class FlowExecutionJournal : IFlowExecutionJournal
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowExecutionJournal));
        private static readonly object WriteLock = new object();

        private readonly SqlSugarClient db;
        private readonly bool ownsDb;
        private readonly FlowExecutionOwnerIdentity ownerIdentity;
        private readonly IFlowProcessProbe processProbe;
        private bool disposed;

        public FlowExecutionJournal()
            : this(
                CreateDefaultDb(),
                true,
                FlowExecutionOwnerIdentity.CreateCurrent(),
                new SystemFlowProcessProbe())
        {
        }

        internal FlowExecutionJournal(SqlSugarClient db)
            : this(
                db,
                false,
                FlowExecutionOwnerIdentity.CreateCurrent(),
                new SystemFlowProcessProbe())
        {
        }

        internal FlowExecutionJournal(
            SqlSugarClient db,
            FlowExecutionOwnerIdentity ownerIdentity,
            IFlowProcessProbe processProbe)
            : this(db, false, ownerIdentity, processProbe)
        {
        }

        private FlowExecutionJournal(
            SqlSugarClient db,
            bool ownsDb,
            FlowExecutionOwnerIdentity ownerIdentity,
            IFlowProcessProbe processProbe)
        {
            this.db = db ?? throw new ArgumentNullException(nameof(db));
            this.ownsDb = ownsDb;
            this.ownerIdentity = NormalizeOwnerIdentity(ownerIdentity);
            this.processProbe =
                processProbe ?? throw new ArgumentNullException(nameof(processProbe));

            lock (WriteLock)
            {
                FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
            }
        }

        public FlowRunRecord BeginRun(FlowTemplateSnapshot snapshot, FlowRunRecord run)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(run);
            ThrowIfDisposed();

            NormalizeSnapshot(snapshot);
            AlignFlowKey(snapshot, run);
            if (run.TemplateId == 0)
                run.TemplateId = snapshot.TemplateId;
            if (run.TemplateId != snapshot.TemplateId)
                throw new ArgumentException("运行记录与快照的模板 ID 必须一致。", nameof(run));

            run.RunKey = NormalizeKey(run.RunKey);
            run.StartedTimeUtc = NormalizeUtc(run.StartedTimeUtc ?? DateTime.UtcNow);
            run.Status = FlowStatus.Runing;
            run.ElapsedMs = 0;
            run.CompletedTimeUtc = null;
            run.OwnerInstanceId = ownerIdentity.InstanceId;
            run.OwnerMachine = ownerIdentity.MachineName;
            run.OwnerProcessId = ownerIdentity.ProcessId;
            run.OwnerProcessStartedUtc = ownerIdentity.ProcessStartedUtc;
            run.LastHeartbeatUtc = run.StartedTimeUtc;
            run.RecoveredTimeUtc = null;
            run.RecoveryReason = null;
            run.CompletedTime = run.StartedTimeUtc.Value.ToLocalTime();

            return WriteTransaction(() =>
            {
                FlowRunRecord? existingRun = db.Queryable<FlowRunRecord>()
                    .Where(item => item.RunKey == run.RunKey)
                    .First();
                if (existingRun != null)
                {
                    EnsureSameRun(existingRun, run, snapshot);
                    CopyRun(existingRun, run);
                    return existingRun;
                }

                FlowTemplateSnapshot persistedSnapshot = GetOrCreateSnapshot(snapshot);
                run.SnapshotId = persistedSnapshot.Id;
                run.ContentHash = persistedSnapshot.ContentHash;
                if (run.TemplateRevision == null)
                    run.TemplateRevision = persistedSnapshot.TemplateRevision;

                run.Id = db.Insertable(run).ExecuteReturnIdentity();
                return run;
            });
        }

        public FlowRunRecord HeartbeatRun(
            int runRecordId,
            DateTime? heartbeatTimeUtc = null)
        {
            EnsureRequiredId(runRecordId, nameof(runRecordId));
            ThrowIfDisposed();
            DateTime heartbeatUtc = NormalizeUtc(heartbeatTimeUtc ?? DateTime.UtcNow);

            return WriteTransaction(() =>
            {
                db.Updateable<FlowRunRecord>()
                    .SetColumns(item => item.LastHeartbeatUtc == heartbeatUtc)
                    .Where(item =>
                        item.Id == runRecordId
                        && item.Status == FlowStatus.Runing
                        && item.CompletedTimeUtc == null
                        && item.OwnerInstanceId == ownerIdentity.InstanceId
                        && (item.LastHeartbeatUtc == null
                            || item.LastHeartbeatUtc < heartbeatUtc))
                    .ExecuteCommand();

                FlowRunRecord? run =
                    db.Queryable<FlowRunRecord>().InSingle(runRecordId);
                if (run == null)
                    throw new InvalidOperationException($"找不到流程运行记录 {runRecordId}。");
                if (run.CompletedTimeUtc != null || run.Status != FlowStatus.Runing)
                    return run;
                if (!string.Equals(
                    run.OwnerInstanceId,
                    ownerIdentity.InstanceId,
                    StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"流程运行记录 {runRecordId} 不属于当前运行实例。");
                }
                if (run.LastHeartbeatUtc != null
                    && NormalizeUtc(run.LastHeartbeatUtc.Value) >= heartbeatUtc)
                {
                    return run;
                }
                return run;
            });
        }

        public IReadOnlyList<FlowRunRecoveryResult> RecoverAbandonedRuns(
            DateTime? recoveredTimeUtc = null)
        {
            ThrowIfDisposed();
            DateTime recoveredUtc = NormalizeUtc(recoveredTimeUtc ?? DateTime.UtcNow);
            List<FlowRunRecord> candidates;
            lock (WriteLock)
            {
                ThrowIfDisposed();
                candidates = db.Queryable<FlowRunRecord>()
                    .Where(item =>
                        item.Status == FlowStatus.Runing
                        && item.CompletedTimeUtc == null)
                    .ToList();
            }

            var recoverable = new List<(FlowRunRecord Run, string Reason)>();
            foreach (FlowRunRecord candidate in candidates)
            {
                if (TryGetRecoveryReason(candidate, out string? reason))
                    recoverable.Add((candidate, reason!));
            }

            var results = new List<FlowRunRecoveryResult>(recoverable.Count);
            foreach ((FlowRunRecord candidate, string reason) in recoverable)
            {
                try
                {
                    FlowRunRecoveryResult? recovered = WriteTransaction(
                        () => RecoverRunCore(candidate, reason, recoveredUtc));
                    if (recovered != null)
                        results.Add(recovered);
                }
                catch (Exception ex)
                {
                    // A corrupt/conflicting run must not prevent later
                    // candidates from being recovered.
                    log.Error($"恢复流程运行记录 {candidate.Id} 失败，继续处理其余记录。", ex);
                }
            }
            return results;
        }

        public FlowExecutionEvent AppendEvent(FlowExecutionEvent executionEvent)
        {
            ArgumentNullException.ThrowIfNull(executionEvent);
            ThrowIfDisposed();
            PrepareEvent(executionEvent);

            return WriteTransaction(() =>
            {
                EnsureRunExists(executionEvent.RunRecordId);
                return AppendEventCore(executionEvent);
            });
        }

        public FlowNodeAttempt BeginAttempt(FlowNodeAttempt attempt)
        {
            ArgumentNullException.ThrowIfNull(attempt);
            ThrowIfDisposed();
            EnsureRequiredId(attempt.RunRecordId, nameof(attempt.RunRecordId));
            if (string.IsNullOrWhiteSpace(attempt.NodeId))
                throw new ArgumentException("节点 ID 不能为空。", nameof(attempt));

            attempt.InvocationId = NormalizeKey(attempt.InvocationId);
            attempt.StartedTimeUtc = NormalizeUtc(
                attempt.StartedTimeUtc == default
                    ? DateTime.UtcNow
                    : attempt.StartedTimeUtc);
            attempt.CompletedTimeUtc = null;
            attempt.Outcome = null;
            attempt.ErrorCode = null;
            attempt.ErrorMessage = null;

            return WriteTransaction(() =>
            {
                EnsureRunExists(attempt.RunRecordId);

                FlowNodeAttempt? existing = db.Queryable<FlowNodeAttempt>()
                    .Where(item =>
                        item.RunRecordId == attempt.RunRecordId
                        && item.InvocationId == attempt.InvocationId)
                    .First();
                if (existing != null)
                {
                    if (!string.Equals(existing.NodeId, attempt.NodeId, StringComparison.Ordinal))
                        throw new InvalidOperationException("相同 InvocationId 不能用于不同节点。");
                    CopyAttempt(existing, attempt);
                    return existing;
                }

                FlowNodeAttempt? last = db.Queryable<FlowNodeAttempt>()
                    .Where(item =>
                        item.RunRecordId == attempt.RunRecordId
                        && item.NodeId == attempt.NodeId)
                    .OrderByDescending(item => item.AttemptNo)
                    .First();
                attempt.AttemptNo = (last?.AttemptNo ?? 0) + 1;
                attempt.Id = db.Insertable(attempt).ExecuteReturnBigIdentity();
                return attempt;
            });
        }

        public FlowNodeAttempt CompleteAttempt(
            long attemptId,
            string outcome,
            string? errorCode = null,
            string? errorMessage = null,
            DateTime? completedTimeUtc = null)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
                attemptId);
            if (string.IsNullOrWhiteSpace(outcome))
                throw new ArgumentException("尝试结果不能为空。", nameof(outcome));
            ThrowIfDisposed();

            DateTime completedUtc = NormalizeUtc(completedTimeUtc ?? DateTime.UtcNow);
            return WriteTransaction(() =>
            {
                FlowNodeAttempt? attempt = db.Queryable<FlowNodeAttempt>().InSingle(attemptId);
                if (attempt == null)
                    throw new InvalidOperationException($"找不到节点尝试记录 {attemptId}。");

                if (attempt.CompletedTimeUtc != null)
                {
                    if (!string.Equals(attempt.Outcome, outcome, StringComparison.Ordinal)
                        || !string.Equals(attempt.ErrorCode, errorCode, StringComparison.Ordinal)
                        || !string.Equals(attempt.ErrorMessage, errorMessage, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException($"节点尝试记录 {attemptId} 已以不同结果完成。");
                    }
                    return attempt;
                }

                attempt.CompletedTimeUtc = completedUtc;
                attempt.Outcome = outcome;
                attempt.ErrorCode = errorCode;
                attempt.ErrorMessage = errorMessage;
                db.Updateable(attempt).ExecuteCommand();
                return attempt;
            });
        }

        public FlowIncident CreateIncident(FlowIncident incident)
        {
            ArgumentNullException.ThrowIfNull(incident);
            ThrowIfDisposed();
            PrepareIncident(incident);

            return WriteTransaction(() =>
            {
                EnsureRunExists(incident.RunRecordId);
                return CreateIncidentCore(incident);
            });
        }

        public FlowRunRecord CompleteRun(
            int runRecordId,
            FlowStatus status,
            long elapsedMs,
            DateTime? completedTimeUtc = null,
            FlowFinalOutcome? finalOutcome = null)
        {
            EnsureRequiredId(runRecordId, nameof(runRecordId));
            EnsureTerminalStatus(status);
            ArgumentOutOfRangeException.ThrowIfNegative(elapsedMs);
            ThrowIfDisposed();

            DateTime completedUtc = NormalizeUtc(completedTimeUtc ?? DateTime.UtcNow);
            return WriteTransaction(() =>
            {
                FlowRunRecord? run = db.Queryable<FlowRunRecord>().InSingle(runRecordId);
                if (run == null)
                    throw new InvalidOperationException($"找不到流程运行记录 {runRecordId}。");

                if (run.CompletedTimeUtc != null)
                {
                    if (run.Status != status
                        || run.ElapsedMs != elapsedMs
                        || run.FinalOutcome != finalOutcome)
                        throw new InvalidOperationException($"流程运行记录 {runRecordId} 已以不同结果完成。");
                    return run;
                }

                run.Status = status;
                run.FinalOutcome = finalOutcome;
                run.ElapsedMs = elapsedMs;
                run.CompletedTimeUtc = completedUtc;
                run.CompletedTime = completedUtc.ToLocalTime();
                db.Updateable(run).ExecuteCommand();
                return run;
            });
        }

        public void Dispose()
        {
            lock (WriteLock)
            {
                if (disposed)
                    return;

                disposed = true;
                if (ownsDb)
                    db.Dispose();
            }
        }

        private T WriteTransaction<T>(Func<T> operation)
        {
            lock (WriteLock)
            {
                ThrowIfDisposed();
                db.Ado.BeginTran();
                try
                {
                    T result = operation();
                    db.Ado.CommitTran();
                    return result;
                }
                catch
                {
                    try
                    {
                        db.Ado.RollbackTran();
                    }
                    catch
                    {
                        // Preserve the original write failure.
                    }
                    throw;
                }
            }
        }

        private FlowRunRecoveryResult? RecoverRunCore(
            FlowRunRecord candidate,
            string reason,
            DateTime recoveredUtc)
        {
            FlowRunRecord? run = db.Queryable<FlowRunRecord>().InSingle(candidate.Id);
            if (run == null
                || run.Status != FlowStatus.Runing
                || run.CompletedTimeUtc != null
                || !HasSameOwner(run, candidate))
            {
                return null;
            }

            run.Status = FlowStatus.Failed;
            run.FinalOutcome = FlowFinalOutcome.Failed;
            run.ElapsedMs = CalculateElapsedMs(run.StartedTimeUtc, recoveredUtc);
            run.CompletedTimeUtc = recoveredUtc;
            run.CompletedTime = recoveredUtc.ToLocalTime();
            run.RecoveredTimeUtc = recoveredUtc;
            run.RecoveryReason = reason;

            int interruptedAttempts = db.Updateable<FlowNodeAttempt>()
                .SetColumns(item => new FlowNodeAttempt
                {
                    CompletedTimeUtc = recoveredUtc,
                    Outcome = "Interrupted",
                    ErrorCode = "ProcessInterrupted",
                    ErrorMessage = "流程拥有进程在节点执行期间中断。"
                })
                .Where(item =>
                    item.RunRecordId == run.Id
                    && item.CompletedTimeUtc == null)
                .ExecuteCommand();
            db.Updateable(run).ExecuteCommand();

            string detailsJson = JsonSerializer.Serialize(new
            {
                run.OwnerInstanceId,
                run.OwnerMachine,
                run.OwnerProcessId,
                run.OwnerProcessStartedUtc,
                run.LastHeartbeatUtc,
                RecoveryReason = reason,
                InterruptedAttempts = interruptedAttempts,
            });
            var recoveredEvent = new FlowExecutionEvent
            {
                RunRecordId = run.Id,
                EventKey = "run-recovered",
                EventType = "RunRecovered",
                OccurredTimeUtc = recoveredUtc,
                Code = reason,
                Message = "流程拥有进程已中断，运行记录已安全恢复为失败状态。",
                DataJson = detailsJson,
            };
            PrepareEvent(recoveredEvent);
            FlowExecutionEvent persistedEvent = AppendEventCore(recoveredEvent);

            var incident = new FlowIncident
            {
                RunRecordId = run.Id,
                IncidentKey = "process-interrupted",
                Kind = "ProcessInterrupted",
                Severity = "Error",
                State = "Open",
                Summary = "流程拥有进程在运行期间中断。",
                DetailsJson = detailsJson,
                DetectedTimeUtc = recoveredUtc,
            };
            PrepareIncident(incident);
            FlowIncident persistedIncident = CreateIncidentCore(incident);

            return new FlowRunRecoveryResult(run, persistedEvent, persistedIncident);
        }

        private FlowExecutionEvent AppendEventCore(FlowExecutionEvent executionEvent)
        {
            FlowExecutionEvent? existing = db.Queryable<FlowExecutionEvent>()
                .Where(item =>
                    item.RunRecordId == executionEvent.RunRecordId
                    && item.EventKey == executionEvent.EventKey)
                .First();
            if (existing != null)
            {
                EnsureSameEvent(existing, executionEvent);
                CopyEvent(existing, executionEvent);
                return existing;
            }

            FlowExecutionEvent? last = db.Queryable<FlowExecutionEvent>()
                .Where(item => item.RunRecordId == executionEvent.RunRecordId)
                .OrderByDescending(item => item.SequenceNo)
                .First();
            executionEvent.SequenceNo = (last?.SequenceNo ?? 0) + 1;
            executionEvent.Id = db.Insertable(executionEvent).ExecuteReturnBigIdentity();
            return executionEvent;
        }

        private FlowIncident CreateIncidentCore(FlowIncident incident)
        {
            FlowIncident? existing = db.Queryable<FlowIncident>()
                .Where(item =>
                    item.RunRecordId == incident.RunRecordId
                    && item.IncidentKey == incident.IncidentKey)
                .First();
            if (existing != null)
            {
                EnsureSameIncident(existing, incident);
                CopyIncident(existing, incident);
                return existing;
            }

            incident.Id = db.Insertable(incident).ExecuteReturnBigIdentity();
            return incident;
        }

        private FlowTemplateSnapshot GetOrCreateSnapshot(FlowTemplateSnapshot snapshot)
        {
            FlowTemplateSnapshot? existing;
            if (!string.IsNullOrWhiteSpace(snapshot.FlowKey))
            {
                existing = db.Queryable<FlowTemplateSnapshot>()
                    .Where(item =>
                        item.FlowKey == snapshot.FlowKey
                        && item.ContentHash == snapshot.ContentHash)
                    .First();
            }
            else
            {
                existing = db.Queryable<FlowTemplateSnapshot>()
                    .Where(item =>
                        item.FlowKey == null
                        && item.TemplateId == snapshot.TemplateId
                        && item.ContentHash == snapshot.ContentHash)
                    .First();
            }
            if (existing != null)
            {
                if (existing.ContentLength != snapshot.ContentLength
                    || !existing.Content.SequenceEqual(snapshot.Content))
                {
                    throw new InvalidOperationException("相同内容哈希对应了不同的模板快照。");
                }
                return existing;
            }

            snapshot.Id = db.Insertable(snapshot).ExecuteReturnBigIdentity();
            return snapshot;
        }

        private void EnsureRunExists(int runRecordId)
        {
            if (!db.Queryable<FlowRunRecord>().Any(item => item.Id == runRecordId))
                throw new InvalidOperationException($"找不到流程运行记录 {runRecordId}。");
        }

        private static void NormalizeSnapshot(FlowTemplateSnapshot snapshot)
        {
            if (snapshot.Content == null)
                throw new ArgumentException("模板快照内容不能为空。", nameof(snapshot));

            FlowTemplateSnapshot normalized = FlowTemplateSnapshotFactory.Create(
                snapshot.TemplateId,
                snapshot.Content,
                snapshot.TemplateRevision,
                snapshot.CapturedTimeUtc == default
                    ? DateTime.UtcNow
                    : NormalizeUtc(snapshot.CapturedTimeUtc),
                snapshot.FlowKey);

            if (!string.IsNullOrWhiteSpace(snapshot.ContentHash)
                && !string.Equals(
                    snapshot.ContentHash,
                    normalized.ContentHash,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("模板快照内容哈希与内容不匹配。", nameof(snapshot));
            }

            snapshot.Content = normalized.Content;
            snapshot.ContentHash = normalized.ContentHash;
            snapshot.ContentLength = normalized.ContentLength;
            snapshot.CapturedTimeUtc = normalized.CapturedTimeUtc;
            snapshot.FlowKey = normalized.FlowKey;
        }

        private static void AlignFlowKey(
            FlowTemplateSnapshot snapshot,
            FlowRunRecord run)
        {
            string? snapshotFlowKey = NormalizeOptionalKey(snapshot.FlowKey);
            string? runFlowKey = NormalizeOptionalKey(run.FlowKey);
            if (snapshotFlowKey != null
                && runFlowKey != null
                && !string.Equals(
                    snapshotFlowKey,
                    runFlowKey,
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "运行记录与模板快照的 FlowKey 必须一致。",
                    nameof(run));
            }

            string? flowKey = snapshotFlowKey ?? runFlowKey;
            snapshot.FlowKey = flowKey;
            run.FlowKey = flowKey;
        }

        private bool TryGetRecoveryReason(
            FlowRunRecord run,
            out string? reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(run.OwnerInstanceId)
                || string.IsNullOrWhiteSpace(run.OwnerMachine)
                || run.OwnerProcessId is not > 0
                || run.OwnerProcessStartedUtc == null
                || run.OwnerProcessStartedUtc.Value == default)
            {
                return false;
            }
            if (!string.Equals(
                run.OwnerMachine,
                ownerIdentity.MachineName,
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            if (string.Equals(
                run.OwnerInstanceId,
                ownerIdentity.InstanceId,
                StringComparison.Ordinal))
            {
                return false;
            }

            FlowOwnerProcessState state;
            try
            {
                state = processProbe.GetState(
                    run.OwnerProcessId.Value,
                    NormalizeUtc(run.OwnerProcessStartedUtc.Value));
            }
            catch
            {
                // An unavailable probe is not proof that an owner is dead.
                return false;
            }

            reason = state switch
            {
                FlowOwnerProcessState.NotRunning => "OwnerProcessNotRunning",
                FlowOwnerProcessState.StartTimeMismatch => "OwnerProcessStartTimeMismatch",
                _ => null,
            };
            return reason != null;
        }

        private static bool HasSameOwner(
            FlowRunRecord current,
            FlowRunRecord candidate)
        {
            return string.Equals(
                    current.OwnerInstanceId,
                    candidate.OwnerInstanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    current.OwnerMachine,
                    candidate.OwnerMachine,
                    StringComparison.OrdinalIgnoreCase)
                && current.OwnerProcessId == candidate.OwnerProcessId
                && NullableUtcEquals(
                    current.OwnerProcessStartedUtc,
                    candidate.OwnerProcessStartedUtc);
        }

        private static bool NullableUtcEquals(DateTime? left, DateTime? right)
        {
            if (left == null || right == null)
                return left == right;
            return NormalizeUtc(left.Value) == NormalizeUtc(right.Value);
        }

        private static long CalculateElapsedMs(
            DateTime? startedTimeUtc,
            DateTime completedTimeUtc)
        {
            if (startedTimeUtc == null)
                return 0;

            double elapsed = (
                completedTimeUtc - NormalizeUtc(startedTimeUtc.Value))
                .TotalMilliseconds;
            if (elapsed <= 0)
                return 0;
            return elapsed >= long.MaxValue ? long.MaxValue : (long)elapsed;
        }

        private static void PrepareEvent(FlowExecutionEvent executionEvent)
        {
            EnsureRequiredId(
                executionEvent.RunRecordId,
                nameof(executionEvent.RunRecordId));
            if (string.IsNullOrWhiteSpace(executionEvent.EventType))
                throw new ArgumentException("事件类型不能为空。", nameof(executionEvent));

            executionEvent.EventKey = NormalizeKey(executionEvent.EventKey);
            executionEvent.OccurredTimeUtc = NormalizeUtc(
                executionEvent.OccurredTimeUtc == default
                    ? DateTime.UtcNow
                    : executionEvent.OccurredTimeUtc);
        }

        private static void PrepareIncident(FlowIncident incident)
        {
            EnsureRequiredId(incident.RunRecordId, nameof(incident.RunRecordId));
            if (string.IsNullOrWhiteSpace(incident.Kind))
                throw new ArgumentException("Incident 类型不能为空。", nameof(incident));
            if (string.IsNullOrWhiteSpace(incident.Severity))
                throw new ArgumentException("Incident 严重级别不能为空。", nameof(incident));
            if (string.IsNullOrWhiteSpace(incident.Summary))
                throw new ArgumentException("Incident 摘要不能为空。", nameof(incident));

            incident.IncidentKey = NormalizeKey(incident.IncidentKey);
            incident.State = string.IsNullOrWhiteSpace(incident.State) ? "Open" : incident.State;
            incident.DetectedTimeUtc = NormalizeUtc(
                incident.DetectedTimeUtc == default
                    ? DateTime.UtcNow
                    : incident.DetectedTimeUtc);
        }

        private static void EnsureSameRun(
            FlowRunRecord existing,
            FlowRunRecord requested,
            FlowTemplateSnapshot snapshot)
        {
            if (existing.TemplateId != requested.TemplateId
                || !string.Equals(existing.FlowKey, requested.FlowKey, StringComparison.Ordinal)
                || !string.Equals(existing.ContentHash, snapshot.ContentHash, StringComparison.OrdinalIgnoreCase)
                || existing.TemplateRevision
                    != (requested.TemplateRevision
                        ?? snapshot.TemplateRevision)
                || !string.Equals(existing.FlowName, requested.FlowName, StringComparison.Ordinal)
                || !string.Equals(existing.SerialNumber, requested.SerialNumber, StringComparison.Ordinal)
                || existing.BatchId != requested.BatchId
                || !string.Equals(
                    existing.OwnerInstanceId,
                    requested.OwnerInstanceId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("相同 RunKey 不能用于不同的流程运行。");
            }
        }

        private static void EnsureSameEvent(
            FlowExecutionEvent existing,
            FlowExecutionEvent requested)
        {
            if (!string.Equals(existing.EventType, requested.EventType, StringComparison.Ordinal)
                || !string.Equals(existing.NodeId, requested.NodeId, StringComparison.Ordinal)
                || existing.AttemptId != requested.AttemptId
                || !string.Equals(existing.Code, requested.Code, StringComparison.Ordinal)
                || !string.Equals(existing.Message, requested.Message, StringComparison.Ordinal)
                || !string.Equals(existing.DataJson, requested.DataJson, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("相同 EventKey 不能用于不同的事件内容。");
            }
        }

        private static void EnsureSameIncident(FlowIncident existing, FlowIncident requested)
        {
            if (!string.Equals(existing.Kind, requested.Kind, StringComparison.Ordinal)
                || !string.Equals(existing.Severity, requested.Severity, StringComparison.Ordinal)
                || !string.Equals(existing.Summary, requested.Summary, StringComparison.Ordinal)
                || !string.Equals(existing.NodeId, requested.NodeId, StringComparison.Ordinal)
                || existing.AttemptId != requested.AttemptId
                || !string.Equals(existing.DetailsJson, requested.DetailsJson, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("相同 IncidentKey 不能用于不同的 Incident。");
            }
        }

        private static void EnsureTerminalStatus(FlowStatus status)
        {
            if (status != FlowStatus.Completed
                && status != FlowStatus.Failed
                && status != FlowStatus.Canceled
                && status != FlowStatus.OverTime)
            {
                throw new ArgumentOutOfRangeException(nameof(status), status, "流程只能以终态完成。");
            }
        }

        private static void EnsureRequiredId(int id, string parameterName)
        {
            if (id <= 0)
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static string NormalizeKey(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Guid.NewGuid().ToString("N")
                : value.Trim();
        }

        private static string? NormalizeOptionalKey(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static FlowExecutionOwnerIdentity NormalizeOwnerIdentity(
            FlowExecutionOwnerIdentity identity)
        {
            ArgumentNullException.ThrowIfNull(identity);
            if (string.IsNullOrWhiteSpace(identity.InstanceId))
                throw new ArgumentException("运行实例 ID 不能为空。", nameof(identity));
            if (string.IsNullOrWhiteSpace(identity.MachineName))
                throw new ArgumentException("运行实例机器名不能为空。", nameof(identity));
            if (identity.ProcessId <= 0)
                throw new ArgumentOutOfRangeException(nameof(identity));
            if (identity.ProcessStartedUtc == default)
                throw new ArgumentException("运行实例进程启动时间不能为空。", nameof(identity));

            return identity with
            {
                InstanceId = identity.InstanceId.Trim(),
                MachineName = identity.MachineName.Trim(),
                ProcessStartedUtc = NormalizeUtc(identity.ProcessStartedUtc),
            };
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            };
        }

        private static void CopyRun(FlowRunRecord source, FlowRunRecord target)
        {
            target.Id = source.Id;
            target.FlowKey = source.FlowKey;
            target.SnapshotId = source.SnapshotId;
            target.ContentHash = source.ContentHash;
            target.TemplateRevision = source.TemplateRevision;
            target.Status = source.Status;
            target.FinalOutcome = source.FinalOutcome;
            target.ElapsedMs = source.ElapsedMs;
            target.StartedTimeUtc = source.StartedTimeUtc;
            target.OwnerInstanceId = source.OwnerInstanceId;
            target.OwnerMachine = source.OwnerMachine;
            target.OwnerProcessId = source.OwnerProcessId;
            target.OwnerProcessStartedUtc = source.OwnerProcessStartedUtc;
            target.LastHeartbeatUtc = source.LastHeartbeatUtc;
            target.CompletedTime = source.CompletedTime;
            target.CompletedTimeUtc = source.CompletedTimeUtc;
            target.RecoveredTimeUtc = source.RecoveredTimeUtc;
            target.RecoveryReason = source.RecoveryReason;
        }

        private static void CopyEvent(FlowExecutionEvent source, FlowExecutionEvent target)
        {
            target.Id = source.Id;
            target.SequenceNo = source.SequenceNo;
            target.OccurredTimeUtc = source.OccurredTimeUtc;
        }

        private static void CopyAttempt(FlowNodeAttempt source, FlowNodeAttempt target)
        {
            target.Id = source.Id;
            target.AttemptNo = source.AttemptNo;
            target.StartedTimeUtc = source.StartedTimeUtc;
            target.CompletedTimeUtc = source.CompletedTimeUtc;
            target.Outcome = source.Outcome;
            target.ErrorCode = source.ErrorCode;
            target.ErrorMessage = source.ErrorMessage;
        }

        private static void CopyIncident(FlowIncident source, FlowIncident target)
        {
            target.Id = source.Id;
            target.DetectedTimeUtc = source.DetectedTimeUtc;
            target.AcknowledgedTimeUtc = source.AcknowledgedTimeUtc;
            target.AcknowledgedOperator = source.AcknowledgedOperator;
            target.AcknowledgmentNote = source.AcknowledgmentNote;
            target.ResolvedTimeUtc = source.ResolvedTimeUtc;
            target.Resolution = source.Resolution;
            target.OperatorName = source.OperatorName;
        }

        private static SqlSugarClient CreateDefaultDb()
        {
            FlowNodeRecordConfig config =
                ConfigService.Instance.GetRequiredService<FlowNodeRecordConfig>();
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={config.SqliteDbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
        }
    }
}
