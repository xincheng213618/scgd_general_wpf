using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed record CopilotWorkspaceRollbackActionRequest(
        string ConversationId,
        string WorkspacePath,
        string ChangeSetId);

    internal sealed record CopilotWorkspaceRollbackActionResult(
        ConfirmableAction? Action,
        string ErrorMessage)
    {
        public bool Success => Action != null && string.IsNullOrWhiteSpace(ErrorMessage);

        public static CopilotWorkspaceRollbackActionResult Failed(string errorMessage) =>
            new(null, CopilotUserFacingErrorFormatter.Sanitize(errorMessage));
    }

    internal sealed class CopilotWorkspaceRollbackCoordinator
    {
        private const string ToolName = "RollbackWorkspacePatchEnvelope";
        private const string CallerIdentity = "colorvision-ui-workspace-rollback";
        private const string ChangeSetPrefix = "workspace-change-set:";
        private readonly CopilotToolRegistry _toolRegistry;
        private readonly CopilotToolExecutor _toolExecutor;
        private readonly CopilotMcpConfirmationStore _confirmationStore;

        public CopilotWorkspaceRollbackCoordinator(
            CopilotToolRegistry toolRegistry,
            CopilotToolExecutor toolExecutor)
            : this(
                toolRegistry,
                toolExecutor,
                CopilotMcpConfirmationStore.Instance)
        {
        }

        internal CopilotWorkspaceRollbackCoordinator(
            CopilotToolRegistry toolRegistry,
            CopilotToolExecutor toolExecutor,
            CopilotMcpConfirmationStore confirmationStore)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
            _confirmationStore = confirmationStore ?? throw new ArgumentNullException(nameof(confirmationStore));
        }

        public async Task<CopilotWorkspaceRollbackActionResult> RequestAsync(
            CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(onEvent);
            cancellationToken.ThrowIfCancellationRequested();

            if (!TryNormalizeWorkspacePath(request.WorkspacePath, out var workspacePath))
                return CopilotWorkspaceRollbackActionResult.Failed("The current workspace is unavailable or no longer exists.");
            if (!TryNormalizeChangeSetId(request.ChangeSetId, out var changeSetId))
                return CopilotWorkspaceRollbackActionResult.Failed("The workspace change-set identifier is missing or invalid.");

            var tool = _toolRegistry.Tools.SingleOrDefault(candidate =>
                string.Equals(candidate.Name, ToolName, StringComparison.Ordinal));
            if (tool is not ICopilotFrameworkApprovedTool
                || tool is not ICopilotFrameworkApprovalPresentation approvalPresenter)
            {
                return CopilotWorkspaceRollbackActionResult.Failed(
                    "The protected workspace rollback tool is not available in the current Copilot runtime.");
            }

            var input = new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["changeSetId"] = changeSetId,
                },
            };
            if (!CopilotAgentToolInputSnapshot.TryCreate(input, out var inputSnapshot, out var snapshotError))
                return CopilotWorkspaceRollbackActionResult.Failed(snapshotError);

            var callId = $"ui-rollback-{Guid.NewGuid():N}";
            var requestText = $"Roll back the change using the exact workspace change set {changeSetId}.";
            var agentRequest = new CopilotAgentRequest
            {
                ConversationId = request.ConversationId?.Trim() ?? string.Empty,
                TaskId = callId,
                WorkspacePath = workspacePath,
                UserText = requestText,
                TaskIntentText = requestText,
                SearchRootPaths = [workspacePath],
                TrustedProjectRootPaths = [workspacePath],
                WritableLocalRootPaths = [workspacePath],
                Mode = CopilotAgentMode.Auto,
            };
            var executionSignature = CopilotAgentToolInputExactBinding.CreateExecutionSignature(
                tool.Name,
                inputSnapshot);
            var executionScope = CopilotExecutionScope
                .ForInProcess(CallerIdentity, workspacePath)
                .BindToolCall(tool.Name, callId, executionSignature);
            agentRequest.RuntimeExecutionScope = executionScope;
            var pendingInvocation = CreateInvocation(
                tool,
                agentRequest,
                inputSnapshot,
                executionScope,
                callId,
                frameworkApprovalGranted: false,
                approvalActionId: string.Empty,
                Array.Empty<CopilotToolExecutionHookRun>(),
                Array.Empty<CopilotToolExecutionHookBinding>());
            var permissionOutcome = await _toolExecutor.EvaluatePermissionRequestAsync(
                pendingInvocation,
                cancellationToken).ConfigureAwait(false);
            if (permissionOutcome.WasCancelled)
                cancellationToken.ThrowIfCancellationRequested();
            if (!permissionOutcome.Decision.ShouldPrompt)
            {
                return CopilotWorkspaceRollbackActionResult.Failed(
                    string.IsNullOrWhiteSpace(permissionOutcome.Decision.Reason)
                        ? "ColorVision policy denied this workspace rollback request."
                        : permissionOutcome.Decision.Reason);
            }

            var presentation = approvalPresenter.CreateApprovalPresentation(inputSnapshot);
            ConfirmableAction? action = null;
            action = _confirmationStore.Create(
                presentation.Title,
                presentation.Description,
                "confirmation-required",
                tool.Name,
                CopilotToolApprovalArgumentFormatter.Create(inputSnapshot),
                async token =>
                {
                    if (action == null)
                    {
                        return CopilotMcpToolCallResult.Fail(
                            "rollback_action_unavailable",
                            "The approved workspace rollback action is no longer available.",
                            CopilotToolFailureKind.Internal);
                    }

                    var approvedInvocation = CreateInvocation(
                        tool,
                        agentRequest,
                        inputSnapshot,
                        executionScope,
                        callId,
                        frameworkApprovalGranted: true,
                        action.ActionId,
                        permissionOutcome.HookRuns,
                        permissionOutcome.HookBindings);
                    var outcome = await _toolExecutor.ExecuteAsync(
                        approvedInvocation,
                        onEvent,
                        token).ConfigureAwait(false);
                    return ToMcpResult(outcome.Result);
                },
                executeOnApproval: true,
                requestContext: new CopilotConfirmationRequestContext
                {
                    Scope = executionScope,
                    SourceKind = CopilotApprovalSourceKind.ColorVisionUi,
                    RequestSource = CallerIdentity,
                    ConversationId = agentRequest.ConversationId,
                    TaskId = agentRequest.TaskId,
                    TaskLabel = "撤销 Copilot 工作区修改",
                    WorkspacePath = workspacePath,
                    ImpactSummary = presentation.ImpactSummary,
                    Reversibility = presentation.Reversibility,
                    ReversibilitySummary = presentation.ReversibilitySummary,
                },
                exactArgumentsBinding: CopilotAgentToolInputExactBinding.Create(inputSnapshot),
                reviewDetails: presentation.ReviewDetails,
                agentCallId: callId);

            var awaitingResult = new CopilotToolResult
            {
                ToolName = tool.Name,
                Success = true,
                Summary = "Workspace rollback is waiting for explicit ColorVision approval.",
                Content = $"change_set_id: {changeSetId}",
                Approval = new CopilotToolApprovalInfo
                {
                    ActionId = action.ActionId,
                    Title = action.Title,
                    RiskLevel = action.RiskLevel,
                    ExpiresAtUtc = action.ExpiresAt,
                    ExecuteOnApproval = true,
                    ResumesAgentOnApproval = false,
                },
            };
            onEvent(CopilotAgentEvent.FromToolResult(
                awaitingResult,
                CreateAwaitingApprovalExecution(
                    tool,
                    agentRequest,
                    inputSnapshot,
                    callId,
                    action),
                permissionOutcome.HookRuns));
            return new CopilotWorkspaceRollbackActionResult(action, string.Empty);
        }

        private static CopilotToolInvocation CreateInvocation(
            ICopilotTool tool,
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CopilotExecutionScope executionScope,
            string callId,
            bool frameworkApprovalGranted,
            string approvalActionId,
            IReadOnlyList<CopilotToolExecutionHookRun> hookRuns,
            IReadOnlyList<CopilotToolExecutionHookBinding> hookBindings)
        {
            return new CopilotToolInvocation
            {
                CallId = callId,
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "colorvision-ui",
                Tool = tool,
                AgentRequest = request,
                ExecutionScope = executionScope,
                ToolInput = input,
                ToolCall = new CopilotToolCall
                {
                    ToolName = tool.Name,
                    ToolInput = input,
                    Reason = "Requested directly from the ColorVision workspace change trace.",
                },
                FrameworkApprovalGranted = frameworkApprovalGranted,
                ApprovalActionId = approvalActionId,
                InitialHookRuns = hookRuns,
                InitialHookBindings = hookBindings,
            };
        }

        private static CopilotToolExecutionInfo CreateAwaitingApprovalExecution(
            ICopilotTool tool,
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            string callId,
            ConfirmableAction action)
        {
            var capability = tool.Capability;
            return new CopilotToolExecutionInfo
            {
                CallId = callId,
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "colorvision-ui",
                ToolName = tool.Name,
                Access = capability.Access,
                RiskLevel = capability.RiskLevel,
                ApprovalMode = capability.ApprovalMode,
                Idempotency = capability.Idempotency,
                ConcurrencyMode = CopilotToolExecutor.ResolveConcurrencyMode(tool),
                ConcurrencyKey = CopilotToolExecutor.ResolveConcurrencyKey(tool, request, input),
                ApprovalActionId = action.ActionId,
                ArgumentSummary = CopilotToolExecutionAuditLogger.CreateArgumentSummary(tool, input),
                State = CopilotToolExecutionState.AwaitingApproval,
                FailureKind = CopilotToolFailureKind.None,
                RetryEligible = false,
                StartedAtUtc = action.CreatedAt,
                TimeoutMs = Math.Max(1, (long)capability.EffectiveExecutionTimeout.TotalMilliseconds),
            };
        }

        private static CopilotMcpToolCallResult ToMcpResult(CopilotToolResult result)
        {
            if (result.Success)
            {
                return CopilotMcpToolCallResult.Ok(
                    string.IsNullOrWhiteSpace(result.Summary)
                        ? result.Content
                        : result.Summary);
            }

            return CopilotMcpToolCallResult.Fail(
                string.IsNullOrWhiteSpace(result.FailureCode)
                    ? "workspace_rollback_failed"
                    : result.FailureCode,
                string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? result.Summary
                    : result.ErrorMessage,
                result.FailureKind);
        }

        private static bool TryNormalizeWorkspacePath(string? workspacePath, out string normalized)
        {
            try
            {
                normalized = Path.TrimEndingDirectorySeparator(
                    Path.GetFullPath(workspacePath ?? string.Empty));
                return Directory.Exists(normalized);
            }
            catch
            {
                normalized = string.Empty;
                return false;
            }
        }

        private static bool TryNormalizeChangeSetId(string? changeSetId, out string normalized)
        {
            normalized = (changeSetId ?? string.Empty).Trim();
            return normalized.StartsWith(ChangeSetPrefix, StringComparison.Ordinal)
                && Guid.TryParseExact(normalized[ChangeSetPrefix.Length..], "N", out _);
        }
    }
}
