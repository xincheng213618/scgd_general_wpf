using Microsoft.Extensions.AI;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotSubagentRunner
    {
        private sealed record CopilotSubagentFinalizationOutcome(
            CopilotAgentRunResult? RunResult,
            string Answer,
            bool Completed)
        {
            public static CopilotSubagentFinalizationOutcome Empty { get; } =
                new(null, string.Empty, false);
        }

        private async Task<CopilotSubagentFinalizationOutcome> RunFinalizationAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRunRequest runRequest,
            CopilotAgentRunResult explorationResult,
            CopilotAgentRequest finalizationRequest,
            CopilotSubagentSteeringMetrics steeringMetrics,
            Stopwatch stopwatch,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(parentRequest);
            ArgumentNullException.ThrowIfNull(runRequest);
            ArgumentNullException.ThrowIfNull(explorationResult);
            ArgumentNullException.ThrowIfNull(finalizationRequest);
            ArgumentNullException.ThrowIfNull(steeringMetrics);
            ArgumentNullException.ThrowIfNull(stopwatch);

            var answer = new StringBuilder();
            var finalizationProgressBudget = explorationResult.Budget;
            var finalizationToolActivity = new CopilotSubagentToolActivityTracker();
            try
            {
                var finalizationRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
                    new CopilotToolRegistry(Array.Empty<ICopilotTool>()),
                    new CopilotAgentContextBuilder(),
                    new CopilotToolExecutor(),
                    _chatClientFactory,
                    EmptyExternalToolProvider.Instance,
                    new CopilotCapabilityCatalog(),
                    _stopHookExecutor);
                using var steeringTarget = CopilotSubagentCoordination.TryAttachSteeringTarget(
                    parentRequest.ConversationId,
                    runRequest.RunId,
                    message => finalizationRuntime.EnqueueSteeringMessage(finalizationRequest.TaskId, message));
                var finalizationResult = await finalizationRuntime.RunAsync(
                    finalizationRequest,
                    agentEvent =>
                    {
                        steeringMetrics.Observe(agentEvent);
                        if (agentEvent.Type == CopilotAgentEventType.AnswerReset)
                        {
                            answer.Clear();
                        }
                        else if (agentEvent.Type == CopilotAgentEventType.AnswerDelta)
                        {
                            answer.Append(agentEvent.Text);
                        }
                        var budgetUpdated = agentEvent.Type == CopilotAgentEventType.BudgetUpdated
                            && agentEvent.Budget != null;
                        if (budgetUpdated)
                        {
                            finalizationProgressBudget = CombineBudgets(
                                explorationResult.Budget,
                                agentEvent.Budget,
                                runRequest.RequestTokenBudget,
                                stopwatch.Elapsed,
                                finalizationCompleted: false);
                        }
                        if (budgetUpdated || finalizationToolActivity.Observe(agentEvent))
                        {
                            runRequest.ReportProgress(
                                CopilotSubagentRunPhase.Finalization,
                                finalizationProgressBudget,
                                finalizationToolActivity.ActiveToolName);
                        }
                    },
                    cancellationToken);
                var completed = finalizationResult.StopReason == CopilotAgentStopReason.Completed
                    && !string.IsNullOrWhiteSpace(answer.ToString());
                return new CopilotSubagentFinalizationOutcome(
                    finalizationResult,
                    completed ? answer.ToString().Trim() : string.Empty,
                    completed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(
                    "Copilot subagent budget finalization failed: {0}",
                    CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
                return CopilotSubagentFinalizationOutcome.Empty;
            }
        }
    }
}
