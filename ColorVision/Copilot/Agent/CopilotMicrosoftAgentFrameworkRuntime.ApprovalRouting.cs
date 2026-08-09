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
            CopilotAutomaticApprovalDenialCircuitBreaker automaticReviewCircuitBreaker,
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
                    var circuitBreakerSnapshot = default(CopilotAutomaticApprovalDenialCircuitBreakerSnapshot);
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
                            var useAutomaticReview = CopilotAgentAccessPolicy.CanAutoReview(
                                request,
                                reservation.Tool,
                                currentWorkspacePath);
                            var useConfiguredAutomaticReview = useAutomaticReview
                                && CopilotCodexApprovalsReviewerSelection.IsExplicitAutoReview(request);
                            var handle = _approvalCoordinator.RequestApproval(
                                reservation.Tool,
                                request,
                                reservation.ToolInput,
                                reservation.CallId,
                                cancellationToken,
                                reservation.ExecutionScope,
                                userReviewVisible: !useConfiguredAutomaticReview);
                            bridge.PublishAwaitingApproval(
                                reservation,
                                handle.Action,
                                useConfiguredAutomaticReview);
                            try
                            {
                                if (useAutomaticReview)
                                {
                                    var isExplicitAutoReview = useConfiguredAutomaticReview;
                                    var hasExplicitRetryOverride = isExplicitAutoReview
                                        && _automaticApprovalOverrideStore.TryConsume(
                                            request,
                                            reservation.Tool,
                                            handle.Action);
                                    emit(CopilotAgentEvent.Status(
                                        hasExplicitRetryOverride
                                            ? $"{reservation.Tool.Name} is being checked again after the ColorVision user authorized one exact retry; automatic review still decides whether it may run."
                                            : isExplicitAutoReview
                                            ? $"{reservation.Tool.Name} is being checked by the configured automatic permission reviewer."
                                            : $"{reservation.Tool.Name} is being checked by the task-scoped automatic permission reviewer."));
                                    var automaticReview = await _automaticApprovalReviewer.ReviewAsync(
                                        contextRecoveryChatClient,
                                        request,
                                        reservation.Tool,
                                        handle.Action,
                                        cancellationToken);
                                    usage = usage.Add(automaticReview.Usage);
                                    circuitBreakerSnapshot = isExplicitAutoReview
                                        ? automaticReviewCircuitBreaker.Observe(automaticReview.Verdict)
                                        : default;
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
                                            : isExplicitAutoReview
                                                ? $"{reservation.Tool.Name} automatic approval could not be applied ({CopilotAgentTraceEntry.Sanitize(approvalMessage)}); execution was closed without falling back to a user approval prompt."
                                                : $"{reservation.Tool.Name} automatic approval could not be applied ({CopilotAgentTraceEntry.Sanitize(approvalMessage)}); the action still requires explicit user approval."));
                                        if (!approved && isExplicitAutoReview)
                                            _approvalCoordinator.Cancel(handle);
                                    }
                                    else if (isExplicitAutoReview
                                        && automaticReview.Verdict == CopilotAutomaticApprovalReviewVerdict.Deny)
                                    {
                                        var reviewWorkspacePath = GetCurrentWorkspacePath();
                                        var rejected = _approvalCoordinator.RejectAfterAutomaticReview(
                                            handle,
                                            request,
                                            reservation.Tool,
                                            reviewWorkspacePath,
                                            automaticReview.Reason,
                                            out var rejectionMessage);
                                        emit(CopilotAgentEvent.Status(rejected
                                            ? $"{reservation.Tool.Name} was denied by automatic permission review ({automaticReview.RiskLevel}): {automaticReviewReason}. Use a materially safer path or stop and ask the user."
                                            : $"{reservation.Tool.Name} automatic denial could not be recorded ({CopilotAgentTraceEntry.Sanitize(rejectionMessage)}); execution was closed."));
                                        if (rejected)
                                            _automaticApprovalOverrideStore.RecordDenial(handle.Action);
                                        if (!rejected)
                                            _approvalCoordinator.Cancel(handle);
                                    }
                                    else if (isExplicitAutoReview)
                                    {
                                        var reviewWorkspacePath = GetCurrentWorkspacePath();
                                        var closed = _approvalCoordinator.CloseAfterAutomaticReviewUnavailable(
                                            handle,
                                            request,
                                            reservation.Tool,
                                            reviewWorkspacePath,
                                            automaticReview.Reason,
                                            out var closeMessage);
                                        emit(CopilotAgentEvent.Status(closed
                                            ? $"{reservation.Tool.Name} automatic permission review was unavailable: {automaticReviewReason}. Execution stayed closed; this alone does not establish that the action is unsafe."
                                            : $"{reservation.Tool.Name} could not be closed after automatic review became unavailable ({CopilotAgentTraceEntry.Sanitize(closeMessage)}); execution was cancelled."));
                                        if (!closed)
                                            _approvalCoordinator.Cancel(handle);
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
                    if (circuitBreakerSnapshot.IsTripped)
                    {
                        approvalRoutingCompleted = true;
                        return new CopilotFrameworkApprovalRoutingResult(
                            responses,
                            usage,
                            circuitBreakerSnapshot);
                    }
                }

                approvalRoutingCompleted = true;
                return new CopilotFrameworkApprovalRoutingResult(responses, usage, null);
            }
            finally
            {
                if (!approvalRoutingCompleted)
                    CancelFrameworkApprovalRouting();
            }
        }

        private sealed record CopilotFrameworkApprovalRoutingResult(
            List<AIContent> Responses,
            CopilotTokenUsage Usage,
            CopilotAutomaticApprovalDenialCircuitBreakerSnapshot? CircuitBreakerSnapshot);
    }
}
