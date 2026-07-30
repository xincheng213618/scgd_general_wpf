using ColorVision.Engine.FlowProcessing.PostProcess;
using log4net;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    /// <summary>
    /// Fail-open bridge between the runtime and the durable execution journal.
    /// Journal availability must never decide whether a production flow runs.
    /// </summary>
    internal sealed class FlowExecutionJournalCoordinator : IDisposable
    {
        private static readonly ILog log =
            LogManager.GetLogger(typeof(FlowExecutionJournalCoordinator));
        private static readonly TimeSpan InitializationRetryDelay =
            TimeSpan.FromSeconds(30);

        private readonly Func<IFlowExecutionJournal> journalFactory;
        private readonly TimeSpan heartbeatInterval;
        private readonly object sync = new();
        private IFlowExecutionJournal? journal;
        private DateTime nextInitializationAttemptUtc;
        private int recoveryStartRequested;
        private bool disposed;

        public static FlowExecutionJournalCoordinator Shared { get; } =
            new(() => new FlowExecutionJournal(), TimeSpan.FromSeconds(5));

        internal FlowExecutionJournalCoordinator(
            Func<IFlowExecutionJournal> journalFactory,
            TimeSpan? heartbeatInterval = null)
        {
            this.journalFactory =
                journalFactory ?? throw new ArgumentNullException(nameof(journalFactory));
            this.heartbeatInterval = heartbeatInterval ?? TimeSpan.FromSeconds(5);
            if (this.heartbeatInterval <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(heartbeatInterval));
        }

        public void StartRecovery()
        {
            if (Interlocked.Exchange(ref recoveryStartRequested, 1) != 0)
                return;

            try
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    GetOrCreateJournal();
                });
            }
            catch (Exception ex)
            {
                log.Error("调度流程运行启动恢复失败。", ex);
            }
        }

        public FlowExecutionJournalScope? TryBeginRun(
            FlowTemplateSnapshot snapshot,
            FlowRunRecord run)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(run);

            IFlowExecutionJournal? activeJournal = GetOrCreateJournal();
            if (activeJournal == null)
                return null;

            try
            {
                FlowRunRecord persistedRun = activeJournal.BeginRun(snapshot, run);
                var scope = new FlowExecutionJournalScope(
                    this,
                    persistedRun.Id,
                    heartbeatInterval);
                scope.TryAppendEvent(
                    "run-started",
                    "RunStarted",
                    message: "流程运行已开始。");
                scope.StartHeartbeat();
                return scope;
            }
            catch (Exception ex)
            {
                log.Error("创建流程运行 journal 失败，当前流程降级为 legacy 记录。", ex);
                return null;
            }
        }

        internal bool TryHeartbeat(int runRecordId)
        {
            return TryInvoke(
                activeJournal => activeJournal.HeartbeatRun(runRecordId),
                $"更新流程运行 {runRecordId} 心跳失败。");
        }

        internal bool TryAppendEvent(FlowExecutionEvent executionEvent)
        {
            return TryInvoke(
                activeJournal => activeJournal.AppendEvent(executionEvent),
                $"写入流程事件 {executionEvent.EventType} 失败。");
        }

        internal FlowNodeAttempt? TryBeginAttempt(FlowNodeAttempt attempt)
        {
            return TryInvokeResult(
                activeJournal => activeJournal.BeginAttempt(attempt),
                $"创建节点 {attempt.NodeId} attempt 失败。");
        }

        internal bool TryCompleteAttempt(
            long attemptId,
            string outcome,
            string? errorCode,
            string? errorMessage)
        {
            return TryInvoke(
                activeJournal => activeJournal.CompleteAttempt(
                    attemptId,
                    outcome,
                    errorCode,
                    errorMessage),
                $"完成节点 attempt {attemptId} 失败。");
        }

        internal bool TryCreateIncident(FlowIncident incident)
        {
            return TryInvoke(
                activeJournal => activeJournal.CreateIncident(incident),
                $"写入流程 Incident {incident.Kind} 失败。");
        }

        internal bool TryCompleteRun(
            int runRecordId,
            FlowStatus status,
            long elapsedMs,
            FlowFinalOutcome? finalOutcome)
        {
            return TryInvoke(
                activeJournal => activeJournal.CompleteRun(
                    runRecordId,
                    status,
                    elapsedMs,
                    DateTime.UtcNow,
                    finalOutcome),
                $"完成流程运行 journal {runRecordId} 失败。");
        }

        private IFlowExecutionJournal? GetOrCreateJournal()
        {
            lock (sync)
            {
                if (disposed)
                    return null;
                if (journal != null)
                    return journal;
                if (DateTime.UtcNow < nextInitializationAttemptUtc)
                    return null;

                try
                {
                    journal = journalFactory();
                    try
                    {
                        IReadOnlyList<FlowRunRecoveryResult> recovered =
                            journal.RecoverAbandonedRuns();
                        if (recovered.Count > 0)
                            log.Warn($"启动恢复已终结 {recovered.Count} 条中断的流程运行。");
                    }
                    catch (Exception ex)
                    {
                        // Recovery is auxiliary; a recovery query failure must
                        // not disable new run journaling.
                        log.Error("启动恢复流程运行 journal 失败，将继续记录新运行。", ex);
                    }
                    return journal;
                }
                catch (Exception ex)
                {
                    nextInitializationAttemptUtc =
                        DateTime.UtcNow + InitializationRetryDelay;
                    log.Error(
                        "初始化流程运行 journal 失败，30 秒内降级为 legacy 记录。",
                        ex);
                    return null;
                }
            }
        }

        private bool TryInvoke<T>(
            Func<IFlowExecutionJournal, T> action,
            string failureMessage)
        {
            IFlowExecutionJournal? activeJournal = GetOrCreateJournal();
            if (activeJournal == null)
                return false;

            try
            {
                action(activeJournal);
                return true;
            }
            catch (Exception ex)
            {
                log.Error(failureMessage, ex);
                return false;
            }
        }

        private T? TryInvokeResult<T>(
            Func<IFlowExecutionJournal, T> action,
            string failureMessage)
            where T : class
        {
            IFlowExecutionJournal? activeJournal = GetOrCreateJournal();
            if (activeJournal == null)
                return null;

            try
            {
                return action(activeJournal);
            }
            catch (Exception ex)
            {
                log.Error(failureMessage, ex);
                return null;
            }
        }

        public void Dispose()
        {
            IFlowExecutionJournal? journalToDispose;
            lock (sync)
            {
                if (disposed)
                    return;

                disposed = true;
                journalToDispose = journal;
                journal = null;
            }

            try
            {
                journalToDispose?.Dispose();
            }
            catch (Exception ex)
            {
                log.Warn("释放流程运行 journal 失败。", ex);
            }
        }
    }

    internal sealed class FlowExecutionJournalScope : IDisposable
    {
        private readonly FlowExecutionJournalCoordinator coordinator;
        private readonly TimeSpan heartbeatInterval;
        private readonly Timer heartbeatTimer;
        private readonly object completionSync = new();
        private CompletionRequest? completionRequest;
        private bool completionPersisted;
        private int disposed;

        private readonly record struct CompletionRequest(
            FlowStatus Status,
            long ElapsedMs,
            FlowFinalOutcome? FinalOutcome);

        internal FlowExecutionJournalScope(
            FlowExecutionJournalCoordinator coordinator,
            int runRecordId,
            TimeSpan heartbeatInterval)
        {
            this.coordinator =
                coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            RunRecordId = runRecordId;
            this.heartbeatInterval = heartbeatInterval;
            heartbeatTimer = new Timer(
                Heartbeat,
                null,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
        }

        public int RunRecordId { get; }

        internal void StartHeartbeat()
        {
            if (CanWrite)
            {
                heartbeatTimer.Change(
                    heartbeatInterval,
                    heartbeatInterval);
            }
        }

        public bool TryAppendEvent(
            string eventKey,
            string eventType,
            string? nodeId = null,
            long? attemptId = null,
            string? code = null,
            string? message = null,
            string? dataJson = null)
        {
            if (!CanWrite)
                return false;

            return coordinator.TryAppendEvent(new FlowExecutionEvent
            {
                RunRecordId = RunRecordId,
                EventKey = eventKey,
                EventType = eventType,
                OccurredTimeUtc = DateTime.UtcNow,
                NodeId = nodeId,
                AttemptId = attemptId,
                Code = code,
                Message = message,
                DataJson = dataJson,
            });
        }

        public FlowNodeAttempt? TryBeginAttempt(
            string nodeId,
            string invocationId,
            int? legacyNodeRecordId = null)
        {
            if (!CanWrite)
                return null;

            FlowNodeAttempt? attempt =
                coordinator.TryBeginAttempt(new FlowNodeAttempt
                {
                    RunRecordId = RunRecordId,
                    LegacyNodeRecordId = legacyNodeRecordId,
                    NodeId = nodeId,
                    InvocationId = invocationId,
                    StartedTimeUtc = DateTime.UtcNow,
                });
            if (attempt != null)
            {
                TryAppendEvent(
                    $"node-started:{invocationId}",
                    "NodeStarted",
                    nodeId,
                    attempt.Id,
                    message: $"节点第 {attempt.AttemptNo} 次尝试已开始。");
            }
            return attempt;
        }

        public void TryCompleteAttempt(
            FlowNodeAttempt? attempt,
            string outcome,
            string? errorCode = null,
            string? errorMessage = null)
        {
            if (!CanWrite || attempt == null || attempt.Id <= 0)
                return;

            if (!coordinator.TryCompleteAttempt(
                    attempt.Id,
                    outcome,
                    errorCode,
                    errorMessage))
            {
                return;
            }

            TryAppendEvent(
                $"node-completed:{attempt.InvocationId}",
                "NodeCompleted",
                attempt.NodeId,
                attempt.Id,
                errorCode,
                outcome);
        }

        public void TryCreateIncident(
            string incidentKey,
            string kind,
            string severity,
            string summary,
            string? nodeId = null,
            long? attemptId = null,
            string? detailsJson = null)
        {
            if (!CanWrite)
                return;

            coordinator.TryCreateIncident(new FlowIncident
            {
                RunRecordId = RunRecordId,
                IncidentKey = incidentKey,
                AttemptId = attemptId,
                NodeId = nodeId,
                Kind = kind,
                Severity = severity,
                State = "Open",
                Summary = summary,
                DetailsJson = detailsJson,
                DetectedTimeUtc = DateTime.UtcNow,
            });
        }

        public bool TryCompleteRun(
            FlowStatus status,
            long elapsedMs,
            FlowFinalOutcome? finalOutcome)
        {
            var requestedCompletion = new CompletionRequest(
                status,
                Math.Max(0, elapsedMs),
                finalOutcome);
            lock (completionSync)
            {
                if (Volatile.Read(ref disposed) != 0)
                    return false;
                if (completionRequest is CompletionRequest existingRequest
                    && existingRequest != requestedCompletion)
                {
                    return false;
                }

                completionRequest ??= requestedCompletion;
                if (completionPersisted)
                    return true;

                StopHeartbeat();
                // A write may fail after SQLite has already accepted it. The
                // journal's CompleteRun contract is idempotent, so one
                // immediate retry with the exact same terminal tuple is safe.
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    if (coordinator.TryCompleteRun(
                        RunRecordId,
                        requestedCompletion.Status,
                        requestedCompletion.ElapsedMs,
                        requestedCompletion.FinalOutcome))
                    {
                        completionPersisted = true;
                        return true;
                    }
                }
                return false;
            }
        }

        private bool CanWrite
        {
            get
            {
                lock (completionSync)
                {
                    return completionRequest == null
                        && Volatile.Read(ref disposed) == 0;
                }
            }
        }

        // FlowExecutionSession uses this as its "journal terminal state was
        // safely written" guard. A merely attempted/failed write must remain
        // retryable and must not be reported as completed.
        internal bool IsCompletionRequested
        {
            get
            {
                lock (completionSync)
                    return completionPersisted;
            }
        }

        private void Heartbeat(object? state)
        {
            if (CanWrite)
                coordinator.TryHeartbeat(RunRecordId);
        }

        private void StopHeartbeat()
        {
            try
            {
                heartbeatTimer.Change(
                    Timeout.InfiniteTimeSpan,
                    Timeout.InfiniteTimeSpan);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            StopHeartbeat();
            heartbeatTimer.Dispose();
        }
    }
}
