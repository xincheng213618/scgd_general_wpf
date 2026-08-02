using System;
using System.Collections.Generic;
using ColorVision.Engine.FlowProcessing.PostProcess;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal interface IFlowExecutionJournal : IDisposable
    {
        FlowRunRecord BeginRun(FlowTemplateSnapshot snapshot, FlowRunRecord run);

        FlowRunRecord HeartbeatRun(int runRecordId, DateTime? heartbeatTimeUtc = null);

        IReadOnlyList<FlowRunRecoveryResult> RecoverAbandonedRuns(
            DateTime? recoveredTimeUtc = null);

        FlowExecutionEvent AppendEvent(FlowExecutionEvent executionEvent);

        FlowNodeAttempt BeginAttempt(FlowNodeAttempt attempt);

        FlowNodeAttempt CompleteAttempt(
            long attemptId,
            string outcome,
            string? errorCode = null,
            string? errorMessage = null,
            DateTime? completedTimeUtc = null);

        FlowIncident CreateIncident(FlowIncident incident);

        FlowRunRecord CompleteRun(
            int runRecordId,
            FlowStatus status,
            long elapsedMs,
            DateTime? completedTimeUtc = null,
            FlowFinalOutcome? finalOutcome = null);
    }
}
