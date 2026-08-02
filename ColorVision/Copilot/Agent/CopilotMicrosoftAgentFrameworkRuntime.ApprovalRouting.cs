#pragma warning disable MAAI001
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        private async Task<CopilotFrameworkApprovalRoutingResult> RouteFrameworkApprovalsAsync(
            IReadOnlyList<ToolApprovalRequestContent> approvalRequests,
            CopilotAgentRequest request,
            HarnessToolBridge bridge,
            IChatClient contextRecoveryChatClient,
            CopilotAgentTaskEventJournalBuilder taskEventJournalBuilder,
            Action<CopilotAgentEvent> emit,
            CopilotTokenUsage usage,
            CancellationToken cancellationToken)
        {
            BeginFrameworkApprovalRouting();
            var approvalRoutingCompleted = false;
            try
            {
                var responses = new List<AIContent>();
                foreach (var approvalRequest in approvalRequests)
                {
                    if (!bridge.TryBeginApproval(approvalRequest, out var reservation, out var error))
                    {
                        var policyDecision = CopilotFrameworkApprovalDecision.PolicyDenied(error);
                        emit(CopilotAgentEvent.Status(policyDecision.FormatStatus("The protected tool call")));
                        responses.Add(approvalRequest.CreateResponse(false, policyDecision.Reason));
                        continue;
                    }

                    var currentWorkspacePath = GetCurrentWorkspacePath();
                    CopilotFrameworkApprovalDecision decision;
                    if (CopilotAgentAccessPolicy.CanAutoApprove(
                        request,
                        reservation.Tool,
                        currentWorkspacePath))
                    {
                        decision = CopilotFrameworkApprovalDecision.ApprovedByFullAccess();
                        reservation.ApprovedByFullAccess = true;
                        bridge.Approve(reservation);
                        emit(CopilotAgentEvent.Status($"{reservation.Tool.Name} was approved by the temporary structured-workspace grant for this ColorVision task."));
                    }
                    else
                    {
                        var permissionOutcome = await bridge.EvaluatePermissionRequestAsync(
                            reservation,
                            cancellationToken);
                        if (permissionOutcome.WasCancelled)
                        {
                            decision = CopilotFrameworkApprovalDecision.Cancelled(
                                permissionOutcome.Decision.Reason);
                            bridge.Reject(reservation, decision);
                            cancellationToken.ThrowIfCancellationRequested();
                            throw new OperationCanceledException(
                                permissionOutcome.Decision.Reason,
                                cancellationToken);
                        }
                        if (!permissionOutcome.Decision.ShouldPrompt)
                        {
                            decision = CopilotFrameworkApprovalDecision.PolicyDenied(
                                permissionOutcome.Decision.Reason,
                                permissionOutcome.Decision.FailureCode);
                            bridge.Reject(reservation, decision);
                        }
                        else
                        {
                            var handle = _approvalCoordinator.RequestApproval(
                                reservation.Tool,
                                request,
                                reservation.ToolInput,
                                reservation.CallId,
                                cancellationToken,
                                reservation.ExecutionScope);
                            bridge.PublishAwaitingApproval(reservation, handle.Action);
                            try
                            {
                                if (CopilotAgentAccessPolicy.CanAutoReview(
                                    request,
                                    reservation.Tool,
                                    currentWorkspacePath))
                                {
                                    emit(CopilotAgentEvent.Status(
                                        $"{reservation.Tool.Name} is being checked by the task-scoped automatic permission reviewer."));
                                    var automaticReview = await _automaticApprovalReviewer.ReviewAsync(
                                        contextRecoveryChatClient,
                                        request,
                                        reservation.Tool,
                                        handle.Action,
                                        cancellationToken);
                                    usage = usage.Add(automaticReview.Usage);
                                    var automaticReviewReason = CopilotAgentTraceEntry.Sanitize(
                                        automaticReview.Reason);
                                    if (automaticReview.Verdict == CopilotAutomaticApprovalReviewVerdict.Approve)
                                    {
                                        var approvalWorkspacePath = GetCurrentWorkspacePath();
                                        var approved = _approvalCoordinator.ApproveAfterAutomaticReview(
                                            handle,
                                            request,
                                            reservation.Tool,
                                            approvalWorkspacePath,
                                            automaticReview.Reason,
                                            out var approvalMessage);
                                        emit(CopilotAgentEvent.Status(approved
                                            ? $"{reservation.Tool.Name} passed automatic permission review ({automaticReview.RiskLevel}): {automaticReviewReason}"
                                            : $"{reservation.Tool.Name} automatic approval could not be applied ({CopilotAgentTraceEntry.Sanitize(approvalMessage)}); the action still requires explicit user approval."));
                                    }
                                    else
                                    {
                                        emit(CopilotAgentEvent.Status(
                                            $"{reservation.Tool.Name} still requires explicit user approval: {automaticReviewReason}"));
                                    }
                                }
                                else
                                {
                                    emit(CopilotAgentEvent.Status(
                                        $"{reservation.Tool.Name} is waiting for explicit approval in ColorVision."));
                                }

                                decision = await handle.Decision;
                                cancellationToken.ThrowIfCancellationRequested();
                            }
                            catch (OperationCanceledException)
                            {
                                bridge.CancelApproval(
                                    reservation,
                                    "The approval request was cancelled with the Agent run.");
                                throw;
                            }
                            if (decision.IsApproved)
                            {
                                bridge.Approve(reservation);
                            }
                            else
                            {
                                bridge.Reject(reservation, decision);
                            }
                        }
                    }
                    emit(CopilotAgentEvent.Status(decision.FormatStatus(reservation.Tool.Name)));
                    if (decision.IsApproved)
                    {
                        taskEventJournalBuilder.RecordApprovalDecision(
                            reservation.Tool.Name,
                            reservation.CallId,
                            reservation.ApprovalActionId,
                            approved: true,
                            decision.Source.ToString());
                    }

                    responses.Add(approvalRequest.CreateResponse(decision.IsApproved, decision.Reason));
                }

                approvalRoutingCompleted = true;
                return new CopilotFrameworkApprovalRoutingResult(responses, usage);
            }
            finally
            {
                if (!approvalRoutingCompleted)
                    CancelFrameworkApprovalRouting();
            }
        }

        private sealed record CopilotFrameworkApprovalRoutingResult(
            List<AIContent> Responses,
            CopilotTokenUsage Usage);
    }
}
