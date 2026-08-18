#pragma warning disable MAAI001
#pragma warning disable CA1859
using Anthropic;
using Anthropic.Core;
using ColorVision.Copilot.Mcp;
using ColorVision.Solution;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AIChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        internal sealed partial class HarnessToolBridge
        {
            private async Task<string> ExecuteAsync(
                ICopilotTool tool,
                CopilotAgentToolInput toolInput,
                string? providerCallId,
                CancellationToken cancellationToken)
            {
                var signature = BuildExecutionSignature(tool.Name, toolInput);
                var callResult = await _providerToolCalls.ExecuteOnceAsync(
                    providerCallId,
                    signature,
                    () => ExecuteReservedAsync(tool, toolInput, providerCallId, signature, cancellationToken),
                    cancellationToken);
                if (callResult.HasConflict)
                {
                    return FormatRejectedToolCall(
                        tool.Name,
                        callResult.Error,
                        "duplicate_call_id_conflict",
                        CopilotToolFailureKind.Conflict);
                }

                return callResult.Content;
            }

            private async Task<string> ExecuteReservedAsync(
                ICopilotTool tool,
                CopilotAgentToolInput toolInput,
                string? providerCallId,
                string signature,
                CancellationToken cancellationToken)
            {
                int round;
                int attempt;
                int maxAttempts;
                string previousObservationProgressSignature;
                FrameworkApprovalReservation? approvalReservation;
                string? reservationError = null;
                lock (_syncRoot)
                {
                    if (_approvedCalls.Remove(
                        CopilotFrameworkApprovalReservationKey.Create(providerCallId, signature),
                        out approvalReservation))
                    {
                        round = approvalReservation.Round;
                        attempt = approvalReservation.Attempt;
                        maxAttempts = approvalReservation.MaxAttempts;
                        previousObservationProgressSignature =
                            approvalReservation.PreviousObservationProgressSignature;
                    }
                    else
                    {
                        if (!TryReserveAttempt(
                                tool,
                                signature,
                                out round,
                                out attempt,
                                out previousObservationProgressSignature,
                                out var error))
                        {
                            reservationError = error;
                            maxAttempts = 0;
                        }
                        else
                        {
                            maxAttempts = GetMaximumAttempts(tool);
                        }
                    }
                }

                if (reservationError != null)
                    return RecordGuardRejectedToolCall(tool, toolInput, signature, reservationError, providerCallId);

                var invocationCallId = string.IsNullOrWhiteSpace(providerCallId)
                    ? Guid.NewGuid().ToString("N")
                    : providerCallId.Trim();
                var invocation = approvalReservation == null
                    ? new CopilotToolInvocation
                    {
                        CallId = invocationCallId,
                        Round = round,
                        Attempt = attempt,
                        MaxAttempts = maxAttempts,
                        RuntimeName = "agent-framework",
                        Tool = tool,
                        AgentRequest = _request,
                        ExecutionScope = _executionScope.BindToolCall(
                            tool.Name,
                            invocationCallId,
                            signature),
                        ToolInput = toolInput,
                        ToolCall = CreateToolCall(tool, toolInput),
                        PreviousObservationProgressSignature =
                            previousObservationProgressSignature,
                    }
                    : CreateInvocation(approvalReservation, frameworkApprovalGranted: true);
                if (approvalReservation != null
                    && !CanBeginApprovedExecution(
                        approvalReservation,
                        out var approvalFailureCode,
                        out var approvalFailureReason))
                {
                    _approvalCoordinator.Cancel(
                        approvalReservation.ApprovalActionId,
                        approvalFailureReason);
                    var decision = CopilotFrameworkApprovalDecision.PolicyDenied(
                        approvalFailureReason,
                        approvalFailureCode);
                    Reject(approvalReservation, decision);
                    return FormatRejectedToolCall(
                        tool.Name,
                        decision.Reason,
                        approvalFailureCode,
                        CopilotToolFailureKind.Authorization);
                }

                CopilotToolExecutionOutcome outcome;
                try
                {
                    outcome = await _toolExecutor.ExecuteAsync(invocation, _emit, cancellationToken);
                }
                catch (CopilotToolExecutionCancellationException ex)
                {
                    outcome = ex.Outcome;
                    if (approvalReservation != null)
                        _approvalCoordinator.Complete(approvalReservation.ApprovalActionId, outcome.Result);
                    _ = FormatToolResult(outcome);
                    RecordExecutionOutcome(signature, outcome);
                    throw;
                }
                catch (OperationCanceledException)
                {
                    if (approvalReservation != null)
                    {
                        _approvalCoordinator.Cancel(
                            approvalReservation.ApprovalActionId,
                            "The approved Agent Framework action was cancelled before completion.");
                    }
                    throw;
                }

                if (approvalReservation != null)
                    _approvalCoordinator.Complete(approvalReservation.ApprovalActionId, outcome.Result);

                var formattedModelResult = FormatToolResult(outcome);
                RecordExecutionOutcome(signature, outcome);
                if (outcome.Result.DelegatedRunUsage != null)
                    _recordDelegatedRunUsage?.Invoke(outcome.Result.DelegatedRunUsage);

                await EnqueueHookAdditionalContextAsync(
                    outcome.ModelAdditionalContexts,
                    cancellationToken).ConfigureAwait(false);
                RequestPostToolStop(outcome);

                return formattedModelResult;
            }

            private void RequestPostToolStop(CopilotToolExecutionOutcome outcome)
            {
                if (outcome.PostExecutionControl != CopilotToolPostExecutionControl.Stopped)
                    return;

                var summary = CreatePostToolStopSummary(outcome.PostExecutionControlReason);
                var notify = false;
                lock (_syncRoot)
                {
                    if (_postToolStopBlocker == null)
                    {
                        _postToolStopBlocker = new CopilotAgentBlockerSnapshot
                        {
                            Kind = CopilotAgentBlockerKind.Policy,
                            Code = "post_tool_hook_stopped",
                            Summary = summary,
                            ToolName = outcome.Execution.ToolName,
                            SourceCallKey = CopilotAgentTaskEventIds.ForCall(outcome.Execution.CallId),
                            RetryEligible = false,
                            RequiresUserInput = true,
                        };
                        notify = true;
                    }
                }
                if (notify)
                    _onPostToolStopRequested?.Invoke();
            }

            private static string CreatePostToolStopSummary(string? reason)
            {
                const string defaultSummary =
                    "A synchronous PostToolUse policy stopped the Agent after the completed tool call.";
                var normalizedReason = string.Join(
                    " ",
                    CopilotApprovalRequestReason.Normalize(reason).Split(
                        (char[]?)null,
                        StringSplitOptions.RemoveEmptyEntries));
                var summary = normalizedReason.Length == 0
                    ? defaultSummary
                    : "A synchronous PostToolUse policy stopped the Agent: " + normalizedReason;
                if (summary.Length <= CopilotAgentTaskEventJournal.MaxSummaryLength)
                    return summary;

                var maximumContentLength = CopilotAgentTaskEventJournal.MaxSummaryLength - 3;
                if (maximumContentLength > 0 && char.IsHighSurrogate(summary[maximumContentLength - 1]))
                    maximumContentLength--;
                return summary[..maximumContentLength].TrimEnd() + "...";
            }

            private void RecordExecutionOutcome(
                string signature,
                CopilotToolExecutionOutcome outcome)
            {
                lock (_syncRoot)
                {
                    _stepRecords.Add(outcome.StepRecord);
                    RecordOutcome(signature, outcome);
                    if (outcome.Result.DelegatedRunUsage != null)
                        _delegatedUsage = _delegatedUsage.Add(outcome.Result.DelegatedRunUsage.Usage);
                }
            }

        }
    }
}
