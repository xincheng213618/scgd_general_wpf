#pragma warning disable MAAI001
using System;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        internal sealed partial class HarnessToolBridge
        {
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
                bool canBegin;
                if (reservation.ApprovedByExecPolicy)
                {
                    var execPolicy = CopilotCodexExecPolicyEvaluator.Evaluate(
                        _request,
                        reservation.Tool,
                        reservation.ToolInput);
                    canBegin = execPolicy.Decision == CopilotCodexExecPolicyDecision.Allow;
                    if (!canBegin)
                    {
                        failureCode = "codex_exec_policy_changed";
                        failureReason = string.IsNullOrWhiteSpace(execPolicy.Reason)
                            ? "The submitted turn's frozen Codex exec policy no longer explicitly allows this exact command."
                            : execPolicy.Reason;
                        return false;
                    }
                }
                else if (reservation.ApprovedByFullAccess)
                {
                    canBegin = CopilotAgentAccessPolicy.CanAutoApprove(
                        _request,
                        reservation.Tool,
                        currentWorkspacePath);
                }
                else
                {
                    canBegin = _approvalCoordinator.BeginIfRequired(
                        reservation.ApprovalActionId,
                        _request,
                        currentWorkspacePath,
                        reservation.ApprovalArgumentsDigest,
                        reservation.CallId,
                        reservation.ExecutionScope);
                }
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
                    ApprovalPromptCategoryOverride = reservation.ApprovalPromptCategoryOverride,
                    ApprovalPromptReasonOverride = reservation.ApprovalPromptReasonOverride,
                    PreviousObservationProgressSignature =
                        reservation.PreviousObservationProgressSignature,
                    InitialHookRuns = reservation.PermissionHookRuns,
                    InitialHookBindings = reservation.HookBindings,
                    PreDispatchCheckpoint =
                        TryPublishToolDispatchCheckpointAsync,
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
