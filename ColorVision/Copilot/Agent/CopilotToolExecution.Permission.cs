using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotToolExecutor
    {
        internal async Task<CopilotToolPermissionRequestOutcome> EvaluatePermissionRequestAsync(
            CopilotToolInvocation invocation,
            CancellationToken cancellationToken,
            Action<CopilotAgentEvent>? onEvent = null)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            ArgumentNullException.ThrowIfNull(invocation.Tool);
            ArgumentNullException.ThrowIfNull(invocation.AgentRequest);

            var callId = string.IsNullOrWhiteSpace(invocation.CallId)
                ? Guid.NewGuid().ToString("N")
                : invocation.CallId.Trim();
            invocation = NormalizeInvocation(invocation, callId);
            var hooks = ResolveInvocationHooks(
                invocation.Tool.Name,
                invocation.AgentRequest);
            var permissionHooks = hooks
                .Where(binding => binding.Hook is ICopilotToolPermissionRequestHook
                    && binding.Phases.HasFlag(CopilotToolExecutionHookPhases.PermissionRequest))
                .ToArray();
            var hookRuns = new List<CopilotToolExecutionHookRun>(
                Math.Min(permissionHooks.Length, MaxRecordedHookRuns));
            if (cancellationToken.IsCancellationRequested)
            {
                return new CopilotToolPermissionRequestOutcome
                {
                    Decision = CopilotToolPermissionRequestDecision.Deny(
                        "The permission request was cancelled before module policy checks completed.",
                        "approval_cancelled"),
                    HookRuns = hookRuns.ToArray(),
                    HookBindings = hooks.ToArray(),
                    WasCancelled = true,
                };
            }
            if (!CopilotCodexApprovalPolicySelection.AllowsApprovalPrompt(
                invocation.AgentRequest.CodexApprovalPolicy,
                invocation.Tool.Capability.ApprovalPromptCategory))
            {
                return CreatePermissionRequestOutcome(
                    hooks,
                    hookRuns,
                    CopilotToolPermissionRequestDecision.Deny(
                        CopilotCodexApprovalPolicySelection.GetApprovalDenialReason(
                            invocation.AgentRequest.CodexApprovalPolicy,
                            invocation.Tool.Capability.ApprovalPromptCategory),
                        "codex_approval_prompt_disabled"));
            }

            var approvalReason = CopilotCodexApprovalPolicySelection.GetApprovalPromptReason(
                invocation.AgentRequest.CodexApprovalPolicy,
                invocation.Tool);

            var context = new CopilotToolPermissionRequestContext
            {
                Invocation = invocation,
                RequestedAtUtc = _utcNow(),
            };
            var phaseStopwatch = Stopwatch.StartNew();
            var hookEvents = new CopilotToolExecutionHookEventPublisher(
                onEvent,
                () => CreateExecutionInfo(
                    invocation,
                    CopilotToolExecutionState.Pending,
                    context.RequestedAtUtc,
                    completedAt: null,
                    phaseStopwatch.ElapsedMilliseconds,
                    _hookPhaseTimeout));
            foreach (var binding in permissionHooks)
            {
                if (binding.ExecutionMode == CopilotToolExecutionHookMode.Async)
                {
                    var asyncPermissionHook =
                        (ICopilotToolPermissionRequestHook)binding.Hook;
                    ScheduleAsyncHook(
                        binding,
                        CopilotToolExecutionHookPhase.PermissionRequest,
                        invocation,
                        hookRuns,
                        async token =>
                        {
                            CopilotToolPermissionRequestOutput? output = null;
                            CopilotToolPermissionRequestDecision decision;
                            if (asyncPermissionHook is ICopilotToolPermissionRequestOutputHook outputHook)
                            {
                                output = await outputHook.OnPermissionRequestWithOutputAsync(
                                    context,
                                    token).ConfigureAwait(false);
                                decision = output?.Decision
                                    ?? CopilotToolPermissionRequestDecision.Prompt;
                            }
                            else
                            {
                                decision = await asyncPermissionHook.OnPermissionRequestAsync(
                                    context,
                                    token).ConfigureAwait(false)
                                    ?? CopilotToolPermissionRequestDecision.Prompt;
                            }
                            if (!decision.ShouldPrompt
                                || !string.IsNullOrWhiteSpace(decision.Reason))
                            {
                                Log.Warn(
                                    $"Copilot async permission-hook control decision was ignored. Tool={invocation.Tool.Name} CallId={invocation.CallId} HookSource={binding.SourceId}");
                            }
                            if (output?.HasOutput == true)
                            {
                                Log.Warn(
                                    $"Copilot async permission-hook output was ignored by the notification-only execution mode. Tool={invocation.Tool.Name} CallId={invocation.CallId} HookSource={binding.SourceId}");
                            }
                        });
                    continue;
                }

                BeginHookRun(
                    hookRuns,
                    hookEvents,
                    binding.SourceId,
                    CopilotToolExecutionHookPhase.PermissionRequest);
                var remaining = GetHookTimeout(binding, phaseStopwatch);
                if (remaining <= TimeSpan.Zero)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.PermissionRequest,
                        CopilotToolExecutionHookState.Skipped,
                        0,
                        "permission_hook_phase_timeout",
                        hookEvents);
                    return CreatePermissionRequestOutcome(
                        hooks,
                        hookRuns,
                        CopilotToolPermissionRequestDecision.Deny(
                            $"The permission-request hook phase exceeded its {FormatTimeout(_hookPhaseTimeout)} timeout.",
                            "permission_hook_timeout"));
                }

                var hook = (ICopilotToolPermissionRequestHook)binding.Hook;
                CancellationTokenSource? hookCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task? hookTask = null;
                var hookStopwatch = Stopwatch.StartNew();
                try
                {
                    CopilotToolPermissionRequestOutput? output = null;
                    CopilotToolPermissionRequestDecision decision;
                    if (hook is ICopilotToolPermissionRequestOutputHook outputHook)
                    {
                        var outputTask = outputHook.OnPermissionRequestWithOutputAsync(
                            context,
                            hookCancellation.Token);
                        hookTask = outputTask;
                        output = await outputTask.WaitAsync(remaining, cancellationToken);
                        decision = output?.Decision
                            ?? CopilotToolPermissionRequestDecision.Prompt;
                    }
                    else
                    {
                        var decisionTask = hook.OnPermissionRequestAsync(
                            context,
                            hookCancellation.Token);
                        hookTask = decisionTask;
                        decision = await decisionTask.WaitAsync(remaining, cancellationToken)
                            ?? CopilotToolPermissionRequestDecision.Prompt;
                    }
                    ApplyPermissionRequestOutput(
                        output,
                        binding.SourceId,
                        hookEvents);
                    if (!decision.ShouldPrompt)
                    {
                        var failureCode = string.IsNullOrWhiteSpace(decision.FailureCode)
                            ? "permission_hook_denied"
                            : decision.FailureCode;
                        RecordHookRun(
                            hookRuns,
                            binding.SourceId,
                            CopilotToolExecutionHookPhase.PermissionRequest,
                            CopilotToolExecutionHookState.Denied,
                            hookStopwatch.ElapsedMilliseconds,
                            failureCode,
                            hookEvents);
                        return CreatePermissionRequestOutcome(
                            hooks,
                            hookRuns,
                            CopilotToolPermissionRequestDecision.Deny(
                                string.IsNullOrWhiteSpace(decision.Reason)
                                    ? "A permission-request hook denied this protected tool call."
                                    : CopilotUserFacingErrorFormatter.Sanitize(decision.Reason),
                            failureCode));
                    }

                    approvalReason = CopilotApprovalRequestReason.Combine(
                        approvalReason,
                        decision.Reason);

                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.PermissionRequest,
                        CopilotToolExecutionHookState.Completed,
                        hookStopwatch.ElapsedMilliseconds,
                        hookEvents: hookEvents);
                }
                catch (TimeoutException)
                {
                    CancelAndDisposeWithoutWaiting(ref hookCancellation);
                    CopilotCancellationBoundary.ObserveLateFault(hookTask);
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.PermissionRequest,
                        CopilotToolExecutionHookState.TimedOut,
                        hookStopwatch.ElapsedMilliseconds,
                        "permission_hook_timeout",
                        hookEvents);
                    return CreatePermissionRequestOutcome(
                        hooks,
                        hookRuns,
                        CopilotToolPermissionRequestDecision.Deny(
                            $"A permission-request hook exceeded its {FormatTimeout(remaining)} timeout.",
                            "permission_hook_timeout"));
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    CancelAndDisposeWithoutWaiting(ref hookCancellation);
                    CopilotCancellationBoundary.ObserveLateFault(hookTask);
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.PermissionRequest,
                        CopilotToolExecutionHookState.Cancelled,
                        hookStopwatch.ElapsedMilliseconds,
                        "permission_hook_cancelled",
                        hookEvents);
                    Log.Warn(
                        $"Copilot permission-request hook cancelled itself. Tool={invocation.Tool.Name} CallId={invocation.CallId} HookSource={binding.SourceId} Hook={binding.Hook.GetType().FullName}");
                    return CreatePermissionRequestOutcome(
                        hooks,
                        hookRuns,
                        CopilotToolPermissionRequestDecision.Deny(
                            "A permission-request hook was cancelled before it could inspect the protected tool call.",
                            "permission_hook_cancelled"));
                }
                catch (OperationCanceledException)
                {
                    CancelAndDisposeWithoutWaiting(ref hookCancellation);
                    CopilotCancellationBoundary.ObserveLateFault(hookTask);
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.PermissionRequest,
                        CopilotToolExecutionHookState.Cancelled,
                        hookStopwatch.ElapsedMilliseconds,
                        "approval_cancelled",
                        hookEvents);
                    return new CopilotToolPermissionRequestOutcome
                    {
                        Decision = CopilotToolPermissionRequestDecision.Deny(
                            "The permission request was cancelled with the Agent run.",
                            "approval_cancelled"),
                        HookRuns = hookRuns.ToArray(),
                        HookBindings = hooks.ToArray(),
                        WasCancelled = true,
                    };
                }
                catch (Exception ex) when (ex is not CopilotToolExecutionHookEventDispatchException)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.PermissionRequest,
                        CopilotToolExecutionHookState.Failed,
                        hookStopwatch.ElapsedMilliseconds,
                        "permission_hook_failed",
                        hookEvents);
                    Log.Warn(
                        $"Copilot permission-request hook failed. Tool={invocation.Tool.Name} CallId={invocation.CallId} HookSource={binding.SourceId} Hook={binding.Hook.GetType().FullName} ErrorType={ex.GetType().FullName}");
                    return CreatePermissionRequestOutcome(
                        hooks,
                        hookRuns,
                        CopilotToolPermissionRequestDecision.Deny(
                            "A permission-request hook failed before it could inspect the protected tool call.",
                            "permission_hook_failed"));
                }
                finally
                {
                    hookCancellation?.Dispose();
                }
            }

            return CreatePermissionRequestOutcome(
                hooks,
                hookRuns,
                CopilotToolPermissionRequestDecision.PromptWithReason(approvalReason));
        }

        private static void ApplyPermissionRequestOutput(
            CopilotToolPermissionRequestOutput? output,
            string sourceId,
            CopilotToolExecutionHookEventPublisher hookEvents)
        {
            if (output == null)
                return;

            var systemMessage = CopilotApprovalRequestReason.Normalize(output.SystemMessage);
            if (systemMessage.Length > 0)
            {
                hookEvents.Diagnostic(
                    $"PermissionRequest hook warning · {sourceId}: {systemMessage}");
            }
        }
    }
}
