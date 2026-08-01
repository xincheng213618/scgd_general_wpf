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
                    return CopilotFrameworkToolResultFormatter.FormatRejected(
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
                    return CopilotFrameworkToolResultFormatter.FormatRejected(
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

                lock (_syncRoot)
                {
                    _stepRecords.Add(outcome.StepRecord);
                    RecordOutcome(signature, outcome);
                    if (outcome.Result.DelegatedRunUsage != null)
                        _delegatedUsage = _delegatedUsage.Add(outcome.Result.DelegatedRunUsage.Usage);
                }
                if (outcome.Result.DelegatedRunUsage != null)
                    _recordDelegatedRunUsage?.Invoke(outcome.Result.DelegatedRunUsage);

                return CopilotFrameworkToolResultFormatter.Format(outcome);
            }

            private bool CanBeginApprovedExecution(
                FrameworkApprovalReservation reservation,
                out string failureCode,
                out string failureReason)
            {
                if (!CopilotAgentToolInputExactBinding.MatchesExecutionSignature(
                    reservation.Tool.Name,
                    reservation.ToolInput,
                    reservation.Signature))
                {
                    failureCode = "approval_operation_binding_changed";
                    failureReason = "The approved tool call arguments no longer match the exact operation binding.";
                    return false;
                }

                if (!CopilotCapabilityRevisionAuthorization.TryValidate(
                    reservation.ExecutionScope,
                    _capabilityRevisionProvider,
                    out failureReason))
                {
                    failureCode = "approval_capability_revision_changed";
                    return false;
                }

                var currentWorkspacePath = GetCurrentWorkspacePath();
                var canBegin = reservation.ApprovedByFullAccess
                    ? CopilotAgentAccessPolicy.CanAutoApprove(
                        _request,
                        reservation.Tool,
                        currentWorkspacePath)
                    : _approvalCoordinator.BeginIfRequired(
                        reservation.ApprovalActionId,
                        _request,
                        currentWorkspacePath,
                        reservation.ApprovalArgumentsDigest,
                        reservation.CallId,
                        reservation.ExecutionScope);
                if (canBegin)
                {
                    failureCode = string.Empty;
                    failureReason = string.Empty;
                    return true;
                }

                failureCode = "approval_no_longer_executable";
                failureReason = "The approved Agent Framework action no longer matches the active task, workspace, access policy, or approval state.";
                return false;
            }

            private CopilotToolInvocation CreateInvocation(FrameworkApprovalReservation reservation, bool frameworkApprovalGranted)
            {
                return new CopilotToolInvocation
                {
                    CallId = reservation.CallId,
                    Round = reservation.Round,
                    Attempt = reservation.Attempt,
                    MaxAttempts = reservation.MaxAttempts,
                    RuntimeName = "agent-framework",
                    Tool = reservation.Tool,
                    AgentRequest = _request,
                    ExecutionScope = reservation.ExecutionScope,
                    ToolInput = reservation.ToolInput,
                    ToolCall = CreateToolCall(reservation.Tool, reservation.ToolInput),
                    FrameworkApprovalGranted = frameworkApprovalGranted,
                    ApprovalActionId = reservation.ApprovalActionId,
                    PreviousObservationProgressSignature =
                        reservation.PreviousObservationProgressSignature,
                    InitialHookRuns = reservation.PermissionHookRuns,
                    InitialHookBindings = reservation.HookBindings,
                };
            }

            private static CopilotToolCall CreateToolCall(ICopilotTool tool, CopilotAgentToolInput toolInput)
            {
                return new CopilotToolCall
                {
                    ToolName = tool.Name,
                    ToolInput = toolInput,
                    Reason = "Selected by Microsoft Agent Framework.",
                };
            }

            private CopilotToolExecutionInfo CreateApprovalExecutionInfo(
                FrameworkApprovalReservation reservation,
                CopilotToolExecutionState state,
                string approvalActionId,
                DateTimeOffset? completedAtUtc = null,
                CopilotToolFailureKind failureKind = CopilotToolFailureKind.None)
            {
                var capability = reservation.Tool.Capability;
                return new CopilotToolExecutionInfo
                {
                    CallId = reservation.CallId,
                    Round = reservation.Round,
                    Attempt = reservation.Attempt,
                    MaxAttempts = reservation.MaxAttempts,
                    RuntimeName = "agent-framework",
                    ToolName = reservation.Tool.Name,
                    Access = capability.Access,
                    RiskLevel = capability.RiskLevel,
                    ApprovalMode = capability.ApprovalMode,
                    Idempotency = capability.Idempotency,
                    ConcurrencyMode = CopilotToolExecutor.ResolveConcurrencyMode(reservation.Tool),
                    ConcurrencyKey = CopilotToolExecutor.ResolveConcurrencyKey(reservation.Tool, _request, reservation.ToolInput),
                    ApprovalActionId = approvalActionId,
                    ArgumentSummary = CopilotToolExecutionAuditLogger.CreateArgumentSummary(reservation.Tool, reservation.ToolInput),
                    State = state,
                    FailureKind = failureKind,
                    RetryEligible = false,
                    StartedAtUtc = reservation.StartedAtUtc,
                    CompletedAtUtc = completedAtUtc,
                    DurationMs = completedAtUtc.HasValue ? Math.Max(0, (long)(completedAtUtc.Value - reservation.StartedAtUtc).TotalMilliseconds) : 0,
                    QueueDurationMs = 0,
                    TimeoutMs = Math.Max(1, (long)capability.EffectiveExecutionTimeout.TotalMilliseconds),
                };
            }

        }
    }
}
