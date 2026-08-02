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
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            ArgumentNullException.ThrowIfNull(invocation.Tool);
            ArgumentNullException.ThrowIfNull(invocation.AgentRequest);

            var callId = string.IsNullOrWhiteSpace(invocation.CallId)
                ? Guid.NewGuid().ToString("N")
                : invocation.CallId.Trim();
            invocation = NormalizeInvocation(invocation, callId);
            var hooks = ResolveInvocationHooks(invocation.Tool.Name);
            var permissionHooks = hooks
                .Where(binding => binding.Hook is ICopilotToolPermissionRequestHook)
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

            var context = new CopilotToolPermissionRequestContext
            {
                Invocation = invocation,
                RequestedAtUtc = _utcNow(),
            };
            var phaseStopwatch = Stopwatch.StartNew();
            foreach (var binding in permissionHooks)
            {
                var remaining = _hookPhaseTimeout - phaseStopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.PermissionRequest,
                        CopilotToolExecutionHookState.Skipped,
                        0,
                        "permission_hook_phase_timeout");
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
                Task<CopilotToolPermissionRequestDecision>? hookTask = null;
                var hookStopwatch = Stopwatch.StartNew();
                try
                {
                    hookTask = hook.OnPermissionRequestAsync(context, hookCancellation.Token);
                    var decision = await hookTask.WaitAsync(remaining, cancellationToken)
                        ?? CopilotToolPermissionRequestDecision.Prompt;
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
                            failureCode);
                        return CreatePermissionRequestOutcome(
                            hooks,
                            hookRuns,
                            CopilotToolPermissionRequestDecision.Deny(
                                string.IsNullOrWhiteSpace(decision.Reason)
                                    ? "A permission-request hook denied this protected tool call."
                                    : CopilotUserFacingErrorFormatter.Sanitize(decision.Reason),
                                failureCode));
                    }

                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.PermissionRequest,
                        CopilotToolExecutionHookState.Completed,
                        hookStopwatch.ElapsedMilliseconds);
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
                        "permission_hook_timeout");
                    return CreatePermissionRequestOutcome(
                        hooks,
                        hookRuns,
                        CopilotToolPermissionRequestDecision.Deny(
                            $"A permission-request hook exceeded the {FormatTimeout(_hookPhaseTimeout)} phase timeout.",
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
                        "permission_hook_cancelled");
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
                        "approval_cancelled");
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
                catch (Exception ex)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.PermissionRequest,
                        CopilotToolExecutionHookState.Failed,
                        hookStopwatch.ElapsedMilliseconds,
                        "permission_hook_failed");
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
                CopilotToolPermissionRequestDecision.Prompt);
        }
    }
}
