#pragma warning disable CA1822,CA1826,CA1859,CA1861
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        internal Task<CopilotMcpToolCallResult> CallAsync(
            string toolName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CancellationToken cancellationToken)
        {
            return CallCoreAsync(
                toolName,
                arguments,
                CopilotExecutionScope.ForInProcess("in-process-external"),
                cancellationToken);
        }

        internal Task<CopilotMcpToolCallResult> CallExternalAsync(
            string toolName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            string callerSource,
            CancellationToken cancellationToken)
        {
            return CallCoreAsync(
                toolName,
                arguments,
                CopilotExecutionScope.ForExternalMcpSession(callerSource, callerSource),
                cancellationToken);
        }

        internal Task<CopilotMcpToolCallResult> CallExternalAsync(
            string toolName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            return CallCoreAsync(toolName, arguments, executionScope, cancellationToken);
        }

        private async Task<CopilotMcpToolCallResult> CallCoreAsync(
            string toolName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            executionScope = EnsureWorkspaceScope(executionScope);
            var normalizedToolName = NormalizeToolName(toolName);
            var stopwatch = Stopwatch.StartNew();
            CopilotMcpAuditLogger.ToolCallStarted(normalizedToolName, BuildAuditArgumentSummary(arguments), executionScope);

            try
            {
                if (!_toolDefinitionsByName.TryGetValue(normalizedToolName, out var definition))
                {
                    var missingResult = CopilotMcpToolCallResult.Fail(
                        "tool_not_found",
                        $"Unknown MCP tool: {normalizedToolName}");
                    CopilotMcpAuditLogger.ToolCallCompleted(
                        normalizedToolName,
                        false,
                        stopwatch.Elapsed,
                        missingResult.ErrorCode);
                    return missingResult;
                }

                if (!CopilotToolInputContractValidator.TryValidate(
                        definition.Descriptor.InputSchema,
                        arguments,
                        out var argumentError))
                {
                    var invalidResult = CopilotMcpToolCallResult.Fail(
                        "invalid_arguments",
                        argumentError,
                        CopilotToolFailureKind.Validation);
                    CopilotMcpAuditLogger.ToolCallCompleted(
                        normalizedToolName,
                        false,
                        stopwatch.Elapsed,
                        invalidResult.ErrorCode);
                    return invalidResult;
                }

                var executionTimeout = definition.Descriptor.EffectiveExecutionTimeout
                    < _executionTimeoutLimit
                        ? definition.Descriptor.EffectiveExecutionTimeout
                        : _executionTimeoutLimit;
                var result = CopilotMcpToolResultContract.Capture(
                    normalizedToolName,
                    await CopilotCancellationBoundary.RunTaskAsync(
                        token => definition.Handler(arguments, executionScope, token),
                        executionTimeout,
                        cancellationToken));

                CopilotMcpAuditLogger.ToolCallCompleted(normalizedToolName, result.Success, stopwatch.Elapsed, result.Success ? "OK" : FirstNonEmpty(result.ErrorCode, "tool_call_failed"));
                return result;
            }
            catch (TimeoutException)
            {
                var timeoutResult = CopilotMcpToolCallResult.Fail(
                    "tool_execution_timeout",
                    $"The MCP tool '{normalizedToolName}' exceeded its execution timeout. Its cancellation was requested, but a non-cooperative action may still finish; inspect application state before retrying.",
                    CopilotToolFailureKind.Transient);
                CopilotMcpAuditLogger.ToolCallCompleted(
                    normalizedToolName,
                    false,
                    stopwatch.Elapsed,
                    timeoutResult.ErrorCode);
                return timeoutResult;
            }
            catch (OperationCanceledException)
            {
                CopilotMcpAuditLogger.ToolCallCompleted(normalizedToolName, false, stopwatch.Elapsed, "operation_canceled");
                throw;
            }
            catch (Exception ex)
            {
                CopilotMcpAuditLogger.ToolCallCompleted(normalizedToolName, false, stopwatch.Elapsed, "internal_error");
                return CopilotMcpToolCallResult.Fail("internal_error", $"The MCP tool call failed: {CopilotMcpAuditLogger.RedactText(ex.Message)}");
            }
        }

        public async Task<CopilotApplicationCapabilityCallResult> InvokeAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotApplicationCapabilityCaller caller,
            CancellationToken cancellationToken)
        {
            if (caller != CopilotApplicationCapabilityCaller.InAppAgent)
                throw new ArgumentOutOfRangeException(nameof(caller));
            var result = await CallCoreAsync(
                capabilityName,
                arguments,
                CopilotExecutionScope.ForAgentCaller(GetWorkspaceSnapshot().SolutionDirectoryPath),
                cancellationToken);
            return ToApplicationCapabilityResult(result);
        }

        async Task<CopilotApplicationCapabilityCallResult> ICopilotApprovedApplicationCapabilityInvoker.InvokeApprovedAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotAgentRequest request,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(executionScope);
            var currentWorkspacePath = GetWorkspaceSnapshot().SolutionDirectoryPath;
            if (string.IsNullOrWhiteSpace(request.ConversationId)
                || string.IsNullOrWhiteSpace(request.TaskId)
                || !WorkspaceScopeMatches(request.WorkspacePath, currentWorkspacePath))
            {
                return new CopilotApplicationCapabilityCallResult
                {
                    Success = false,
                    ErrorCode = "approved_capability_scope_mismatch",
                    FailureKind = CopilotToolFailureKind.Authorization,
                    Content = "The approved application capability no longer matches the active Copilot task or workspace.",
                };
            }

            if (!executionScope.HasToolCallBinding
                || !executionScope.MatchesAuthorizationScope(request.RuntimeExecutionScope))
            {
                return new CopilotApplicationCapabilityCallResult
                {
                    Success = false,
                    ErrorCode = "approved_capability_binding_mismatch",
                    FailureKind = CopilotToolFailureKind.Authorization,
                    Content = "The approved application capability is not bound to the active provider tool call and execution scope.",
                };
            }

            if (!CopilotCapabilityRevisionAuthorization.TryValidate(
                executionScope,
                _environment.CapabilityRevisionProvider,
                out var rejectionReason))
            {
                return new CopilotApplicationCapabilityCallResult
                {
                    Success = false,
                    ErrorCode = "approved_capability_revision_changed",
                    FailureKind = CopilotToolFailureKind.Authorization,
                    Content = rejectionReason,
                };
            }

            var approvedExecutionScope = executionScope
                .WithWorkspace(currentWorkspacePath)
                .WithAuthorizationChannel(CopilotExecutionAuthorizationChannel.AgentFrameworkApproved);
            var result = await CallCoreAsync(capabilityName, arguments, approvedExecutionScope, cancellationToken);
            return ToApplicationCapabilityResult(result);
        }

        async Task<CopilotApplicationCapabilityCallResult> ICopilotScopedApplicationCapabilityInvoker.InvokeScopedAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotAgentRequest request,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(executionScope);
            var scopedExecutionScope = executionScope
                .WithWorkspace(GetWorkspaceSnapshot().SolutionDirectoryPath)
                .WithAuthorizationChannel(CopilotExecutionAuthorizationChannel.Standard);
            var result = await CallCoreAsync(capabilityName, arguments, scopedExecutionScope, cancellationToken);
            return ToApplicationCapabilityResult(result);
        }

        private static CopilotApplicationCapabilityCallResult ToApplicationCapabilityResult(
            CopilotMcpToolCallResult result)
        {
            return new CopilotApplicationCapabilityCallResult
            {
                Success = result.Success,
                Content = result.Text,
                ErrorCode = result.ErrorCode,
                FailureKind = result.FailureKind,
                Approval = result.RequiresApproval && !string.IsNullOrWhiteSpace(result.ApprovalActionId)
                    ? new CopilotToolApprovalInfo
                    {
                        ActionId = result.ApprovalActionId,
                        Title = result.ApprovalTitle,
                        RiskLevel = result.ApprovalRiskLevel,
                        ExpiresAtUtc = result.ApprovalExpiresAtUtc,
                        ExecuteOnApproval = result.ExecuteOnApproval,
                        ResumesAgentOnApproval = result.ResumesAgentOnApproval,
                    }
                    : null,
            };
        }

    }
}
