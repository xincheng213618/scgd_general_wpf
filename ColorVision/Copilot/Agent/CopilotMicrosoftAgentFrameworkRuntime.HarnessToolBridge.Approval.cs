#pragma warning disable MAAI001
using Anthropic;
using ColorVision.Copilot.Mcp;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        internal sealed partial class HarnessToolBridge
        {
            public bool TryBeginApproval(
                ToolApprovalRequestContent request,
                out FrameworkApprovalReservation reservation,
                out string error)
            {
                reservation = null!;
                if (request.ToolCall is not FunctionCallContent functionCall)
                {
                    error = "The approval request does not contain a function call.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(functionCall.Name)
                    || !_toolsByFunctionName.TryGetValue(functionCall.Name, out var tool)
                    || !RequiresNativeApproval(tool))
                {
                    error = $"Function {functionCall.Name} is not registered as a natively approved ColorVision tool.";
                    return false;
                }
                if (string.IsNullOrWhiteSpace(functionCall.CallId))
                {
                    error = "The protected Agent Framework tool call is missing its provider call id.";
                    return false;
                }

                var arguments = functionCall.Arguments == null
                    ? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, object?>(functionCall.Arguments, StringComparer.OrdinalIgnoreCase);
                if (!tool.InputSchema.TryBind(arguments, out var toolInput, out error))
                {
                    RecordRejectedToolCall(tool, arguments, error, functionCall.CallId);
                    return false;
                }
                if (!CopilotAgentToolInputSnapshot.TryCreate(toolInput, out var approvedToolInput, out error))
                {
                    RecordRejectedToolCall(tool, arguments, error, functionCall.CallId);
                    return false;
                }

                var signature = BuildExecutionSignature(tool.Name, approvedToolInput);
                string? reservationError = null;
                lock (_syncRoot)
                {
                    if (!_providerToolCalls.TryReserveApproval(functionCall.CallId, signature, out error))
                    {
                        return false;
                    }
                    if (!TryReserveAttempt(
                            tool,
                            signature,
                            out var round,
                            out var attempt,
                            out var previousObservationProgressSignature,
                            out error))
                    {
                        reservationError = error;
                    }
                    else
                    {
                        reservation = new FrameworkApprovalReservation
                        {
                            CallId = functionCall.CallId.Trim(),
                            Round = round,
                            Attempt = attempt,
                            MaxAttempts = GetMaximumAttempts(tool),
                            Signature = signature,
                            ProviderCallId = string.IsNullOrWhiteSpace(functionCall.CallId) ? string.Empty : functionCall.CallId.Trim(),
                            Tool = tool,
                            ToolInput = approvedToolInput,
                            PreviousObservationProgressSignature =
                                previousObservationProgressSignature,
                            ExecutionScope = _executionScope.BindToolCall(
                                tool.Name,
                                functionCall.CallId,
                                signature),
                            StartedAtUtc = DateTimeOffset.UtcNow,
                        };
                    }
                }

                if (reservationError != null)
                {
                    RecordGuardRejectedToolCall(tool, approvedToolInput, signature, reservationError, functionCall.CallId);
                    error = reservationError;
                    return false;
                }

                error = string.Empty;
                return true;
            }

            public async Task<CopilotToolPermissionRequestOutcome> EvaluatePermissionRequestAsync(
                FrameworkApprovalReservation reservation,
                CancellationToken cancellationToken)
            {
                ArgumentNullException.ThrowIfNull(reservation);
                var outcome = await _toolExecutor.EvaluatePermissionRequestAsync(
                    CreateInvocation(reservation, frameworkApprovalGranted: false),
                    cancellationToken,
                    _emit);
                reservation.PermissionHookRuns = outcome.HookRuns;
                reservation.HookBindings = outcome.HookBindings;
                return outcome;
            }

            public async ValueTask PublishAwaitingApprovalAsync(
                FrameworkApprovalReservation reservation,
                Mcp.ConfirmableAction action,
                bool automaticReview,
                CancellationToken cancellationToken)
            {
                reservation.ApprovalActionId = action.ActionId;
                reservation.ApprovalArgumentsDigest = action.ArgumentsDigest;
                var result = new CopilotToolResult
                {
                    ToolName = reservation.Tool.Name,
                    Success = true,
                    Summary = automaticReview
                        ? $"{reservation.Tool.Name} is waiting for the configured automatic permission reviewer."
                        : $"{reservation.Tool.Name} is waiting for explicit ColorVision approval.",
                    Approval = new CopilotToolApprovalInfo
                    {
                        ActionId = action.ActionId,
                        Title = action.Title,
                        RiskLevel = action.RiskLevel,
                        ExpiresAtUtc = action.ExpiresAt,
                        ExecuteOnApproval = false,
                        ResumesAgentOnApproval = true,
                    },
                };
                try
                {
                    _emit(CopilotAgentEvent.FromToolResult(
                        result,
                        CreateApprovalExecutionInfo(reservation, CopilotToolExecutionState.AwaitingApproval, action.ActionId),
                        reservation.PermissionHookRuns));
                    if (!await TryPublishInteractionCheckpointAsync(cancellationToken).ConfigureAwait(false))
                    {
                        throw new InvalidOperationException(
                            "The approval request could not be checkpointed before waiting for a decision.");
                    }
                }
                catch
                {
                    CancelApproval(
                        reservation,
                        "The approval request could not be published and checkpointed before waiting for a decision.");
                    throw;
                }
            }

            public void Approve(FrameworkApprovalReservation reservation)
            {
                lock (_syncRoot)
                    _approvedCalls[CopilotFrameworkApprovalReservationKey.Create(
                        reservation.ProviderCallId,
                        reservation.Signature)] = reservation;
            }

            public void CancelOutstandingApprovals()
            {
                FrameworkApprovalReservation[] outstanding;
                lock (_syncRoot)
                {
                    outstanding = _approvedCalls.Values.ToArray();
                    _approvedCalls.Clear();
                }

                foreach (var reservation in outstanding)
                {
                    CancelApproval(
                        reservation,
                        "The approved action was not executed before the Agent run ended.");
                }
            }

            public void CancelApproval(
                FrameworkApprovalReservation reservation,
                string reason)
            {
                ArgumentNullException.ThrowIfNull(reservation);
                var cancellation = CopilotFrameworkApprovalDecision.Cancelled(reason);
                _approvalCoordinator.Cancel(
                    reservation.ApprovalActionId,
                    cancellation.Reason);
                Reject(reservation, cancellation);
            }

            public void Reject(FrameworkApprovalReservation reservation, CopilotFrameworkApprovalDecision decision)
            {
                ArgumentNullException.ThrowIfNull(decision);
                if (decision.IsApproved)
                    throw new ArgumentException("An approved decision cannot be recorded as a rejected tool call.", nameof(decision));

                var failureKind = decision.Kind == CopilotFrameworkApprovalDecisionKind.Cancelled
                    ? CopilotToolFailureKind.Cancelled
                    : CopilotToolFailureKind.Authorization;
                var result = new CopilotToolResult
                {
                    ToolName = reservation.Tool.Name,
                    Success = false,
                    Summary = decision.FormatToolSummary(reservation.Tool.Name),
                    ErrorMessage = decision.Reason,
                    FailureKind = failureKind,
                    FailureCode = decision.FailureCode,
                };
                var execution = CreateApprovalExecutionInfo(
                    reservation,
                    decision.Kind == CopilotFrameworkApprovalDecisionKind.Cancelled
                        ? CopilotToolExecutionState.Cancelled
                        : CopilotToolExecutionState.Denied,
                    reservation.ApprovalActionId,
                    DateTimeOffset.UtcNow,
                    failureKind);
                var invocation = CreateInvocation(reservation, frameworkApprovalGranted: false);
                var outcome = new CopilotToolExecutionOutcome
                {
                    Invocation = invocation,
                    Result = result,
                    Execution = execution,
                    HookRuns = reservation.PermissionHookRuns,
                };
                CopilotToolExecutionAuditLogger.Record(outcome);
                lock (_syncRoot)
                {
                    _stepRecords.Add(outcome.StepRecord);
                    RecordOutcome(reservation.Signature, outcome);
                }
                _emit(CopilotAgentEvent.FromToolResult(result, execution, outcome.HookRuns));
            }
        }
    }
}
