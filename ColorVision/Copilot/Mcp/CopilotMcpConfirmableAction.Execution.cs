using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpConfirmationStore
    {
        public async Task<CopilotMcpToolCallResult> ExecuteApprovedAsync(
            string actionId,
            string toolName,
            string argumentsDigest,
            string callerSource,
            string workspacePath,
            CancellationToken cancellationToken)
        {
            return await ExecuteApprovedAsync(
                actionId,
                toolName,
                argumentsDigest,
                CopilotExecutionScope.ForExternalMcpSession(
                    callerSource,
                    callerSource,
                    workspacePath),
                cancellationToken);
        }

        public async Task<CopilotMcpToolCallResult> ExecuteApprovedAsync(
            string actionId,
            string toolName,
            string argumentsDigest,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(executionScope);
            var action = Find(actionId);
            if (action == null)
                return CopilotMcpToolCallResult.Fail("action_not_found", $"No confirmable action exists for action_id={actionId}.");

            if (ExpireIfNeeded(action))
                return CopilotMcpToolCallResult.Fail("action_expired", $"The action has expired: action_id={action.ActionId}; expires_at={action.ExpiresAt:O}.");

            Func<CancellationToken, Task<CopilotMcpToolCallResult>> executor;
            lock (_syncRoot)
            {
                if (!string.Equals(action.ToolName, toolName, StringComparison.OrdinalIgnoreCase))
                    return CopilotMcpToolCallResult.Fail("action_tool_mismatch", $"The action was created for tool_name={action.ToolName}, not {toolName}.");

                if (!ArgumentsDigestsMatch(action.ArgumentsDigest, argumentsDigest))
                    return CopilotMcpToolCallResult.Fail("action_arguments_mismatch", "The confirmation arguments_digest does not match the approved action.");

                if (action.RequestContext.SourceKind == CopilotApprovalSourceKind.ExternalMcp
                    && !string.Equals(
                        action.RequestContext.RequestSource,
                        Sanitize(executionScope.CallerIdentity),
                        StringComparison.Ordinal))
                {
                    return CopilotMcpToolCallResult.Fail(
                        "action_source_mismatch",
                        "The approved action belongs to a different MCP caller/source.");
                }

                var actionWorkspacePath = NormalizeWorkspaceForComparison(action.RequestContext.WorkspacePath);
                var currentWorkspacePath = NormalizeWorkspaceForComparison(executionScope.WorkspacePath);
                if (action.RequestContext.SourceKind is CopilotApprovalSourceKind.InAppAgent or CopilotApprovalSourceKind.ExternalMcp
                    && !string.Equals(actionWorkspacePath, currentWorkspacePath, StringComparison.OrdinalIgnoreCase))
                {
                    return CopilotMcpToolCallResult.Fail(
                        "action_workspace_mismatch",
                        "The active workspace changed after this action was approved.");
                }

                if (!action.RequestContext.ResolveExecutionScope().MatchesAuthorizationScope(executionScope))
                {
                    return CopilotMcpToolCallResult.Fail(
                        "action_scope_mismatch",
                        "The approved action belongs to a different execution session, task, run, caller, or capability contract.");
                }

                if (!string.Equals(action.RiskLevel, "confirmation-required", StringComparison.OrdinalIgnoreCase))
                    return CopilotMcpToolCallResult.Fail("action_invalid_risk", $"The action risk level is {action.RiskLevel}; confirm_action only executes confirmation-required actions.");

                if (action.Status == ConfirmableActionStatus.Pending)
                    return CopilotMcpToolCallResult.Fail("action_pending", $"The action is waiting for user approval in ColorVision: action_id={action.ActionId}.");

                if (action.Status == ConfirmableActionStatus.Rejected)
                    return CopilotMcpToolCallResult.Fail("action_rejected", $"The action was rejected in ColorVision: action_id={action.ActionId}.");

                if (action.Status == ConfirmableActionStatus.Cancelled)
                    return CopilotMcpToolCallResult.Fail("action_cancelled", $"The action was cancelled in ColorVision: action_id={action.ActionId}.");

                if (action.Status == ConfirmableActionStatus.Executed || action.Status == ConfirmableActionStatus.Executing)
                    return CopilotMcpToolCallResult.Fail("action_already_executed", $"The action has already been executed: action_id={action.ActionId}.");

                if (action.Status != ConfirmableActionStatus.Approved)
                    return CopilotMcpToolCallResult.Fail("action_not_approved", $"The action status is {action.StatusLabel}, not Approved.");

                action.Status = ConfirmableActionStatus.Executing;
                executor = action.Executor;
            }

            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            CopilotMcpToolCallResult result;
            try
            {
                result = await executor(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                lock (_syncRoot)
                {
                    action.ExecutionSucceeded = false;
                    action.ExecutionResultText = "The approved action execution was cancelled before completion.";
                    action.CompletedAt = DateTimeOffset.UtcNow;
                    action.Status = ConfirmableActionStatus.Cancelled;
                    action.ReleaseExecutor();
                }

                CopilotMcpAuditLogger.ActionCancelled(action);
                RaiseActionStatusChanged(action);
                RaiseActionsChanged();
                throw;
            }
            catch (Exception ex)
            {
                result = CopilotMcpToolCallResult.Fail("action_execution_failed", $"The approved action failed: {CopilotMcpAuditLogger.RedactText(ex.Message)}");
            }

            lock (_syncRoot)
            {
                action.ExecutionSucceeded = result.Success;
                action.ExecutionResultText = Sanitize(result.Text);
                action.CompletedAt = DateTimeOffset.UtcNow;
                action.Status = ConfirmableActionStatus.Executed;
                action.ReleaseExecutor();
            }

            CopilotMcpAuditLogger.ActionExecuted(action, result.Success, result.Success ? "OK" : result.Text);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            return result;
        }

        public async Task<CopilotMcpToolCallResult> ApproveAndExecuteAsync(
            string actionId,
            CopilotConfirmationReviewContext reviewContext,
            CancellationToken cancellationToken)
        {
            var action = Find(actionId);
            if (action == null)
                return CopilotMcpToolCallResult.Fail("action_not_found", $"No confirmable action exists for action_id={actionId}.");

            if (!action.ExecuteOnApproval)
                return CopilotMcpToolCallResult.Fail("action_requires_client_confirmation", "This action requires the MCP client to call confirm_action after user approval.");

            if (!Approve(actionId, reviewContext, out var approvalMessage))
                return CopilotMcpToolCallResult.Fail("action_approval_failed", approvalMessage);

            return await ExecuteApprovedAsync(
                action.ActionId,
                action.ToolName,
                action.ArgumentsDigest,
                action.RequestContext.ResolveExecutionScope().WithWorkspace(reviewContext.WorkspacePath),
                cancellationToken);
        }

        internal bool BeginAgentFrameworkAction(
            string actionId,
            CopilotAgentRequest request,
            string currentWorkspacePath,
            string argumentsDigest,
            string agentCallId,
            CopilotExecutionScope? executionScope = null)
        {
            ArgumentNullException.ThrowIfNull(request);
            var action = Find(actionId);
            if (action == null)
                return false;

            executionScope = executionScope?.WithWorkspace(currentWorkspacePath);
            var expired = false;
            lock (_syncRoot)
            {
                if (!ValidateReviewContextNoLock(
                    action,
                    new CopilotConfirmationReviewContext(
                        request.ConversationId,
                        request.TaskId,
                        currentWorkspacePath),
                    out _))
                {
                    return false;
                }
                if (string.IsNullOrWhiteSpace(agentCallId)
                    || string.IsNullOrWhiteSpace(action.AgentCallId)
                    || !string.Equals(
                        action.AgentCallId,
                        agentCallId.Trim(),
                        StringComparison.Ordinal))
                {
                    return false;
                }
                if (!ArgumentsDigestsMatch(action.ArgumentsDigest, argumentsDigest))
                {
                    return false;
                }
                if (executionScope != null)
                {
                    var actionScope = action.RequestContext.ResolveExecutionScope();
                    if (!actionScope.MatchesAuthorizationScope(executionScope)
                        || !actionScope.MatchesOperationBinding(executionScope))
                    {
                        return false;
                    }
                }

                if ((action.Status == ConfirmableActionStatus.Pending || action.Status == ConfirmableActionStatus.Approved)
                    && action.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    action.Status = ConfirmableActionStatus.Expired;
                    action.CompletedAt = DateTimeOffset.UtcNow;
                    action.ReleaseExecutor();
                    expired = true;
                }
                else if (!action.ResumesAgentOnApproval || action.Status != ConfirmableActionStatus.Approved)
                {
                    return false;
                }

                if (!expired)
                    action.Status = ConfirmableActionStatus.Executing;
            }

            if (expired)
            {
                PublishExpired(action);
                return false;
            }

            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            return true;
        }

        internal bool CompleteAgentFrameworkAction(string actionId, CopilotToolResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            var action = Find(actionId);
            if (action == null)
                return false;

            lock (_syncRoot)
            {
                if (!action.ResumesAgentOnApproval || action.Status != ConfirmableActionStatus.Executing)
                    return false;

                action.ExecutionSucceeded = result.Success;
                action.ExecutionResultText = Sanitize(result.Success
                    ? FirstNonEmpty(result.Summary, result.Content, "The approved Agent Framework action completed.")
                    : FirstNonEmpty(result.ErrorMessage, result.Summary, "The approved Agent Framework action failed."));
                action.CompletedAt = DateTimeOffset.UtcNow;
                action.Status = ConfirmableActionStatus.Executed;
                action.ReleaseExecutor();
            }

            CopilotMcpAuditLogger.ActionExecuted(action, result.Success, result.Success ? "OK" : action.ExecutionResultText);
            RaiseActionStatusChanged(action);
            RaiseActionsChanged();
            return true;
        }
    }
}
