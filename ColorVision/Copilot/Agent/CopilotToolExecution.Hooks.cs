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
        private async Task<CopilotToolExecutionHookDecision> RunBeforeHooksAsync(
            CopilotToolExecutionHookContext context,
            IReadOnlyList<CopilotToolExecutionHookBinding> hooks,
            List<CopilotToolExecutionHookRun> hookRuns,
            CancellationToken cancellationToken)
        {
            var phaseStopwatch = Stopwatch.StartNew();
            foreach (var binding in hooks)
            {
                var remaining = _hookPhaseTimeout - phaseStopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.BeforeExecute,
                        CopilotToolExecutionHookState.Skipped,
                        0,
                        "tool_hook_phase_timeout");
                    return CreateBeforeHookTimeoutDecision();
                }

                CancellationTokenSource? hookCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task<CopilotToolExecutionHookDecision>? hookTask = null;
                var hookStopwatch = Stopwatch.StartNew();
                try
                {
                    hookTask = binding.Hook.BeforeExecuteAsync(context, hookCancellation.Token);
                    var decision = await hookTask.WaitAsync(remaining, cancellationToken) ?? CopilotToolExecutionHookDecision.Proceed;
                    if (!decision.ShouldProceed)
                    {
                        RecordHookRun(
                            hookRuns,
                            binding.SourceId,
                            CopilotToolExecutionHookPhase.BeforeExecute,
                            CopilotToolExecutionHookState.Denied,
                            hookStopwatch.ElapsedMilliseconds,
                            string.IsNullOrWhiteSpace(decision.FailureCode)
                                ? "tool_hook_denied"
                                : decision.FailureCode);
                        return decision;
                    }
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.BeforeExecute,
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
                        CopilotToolExecutionHookPhase.BeforeExecute,
                        CopilotToolExecutionHookState.TimedOut,
                        hookStopwatch.ElapsedMilliseconds,
                        "tool_hook_timeout");
                    return CreateBeforeHookTimeoutDecision();
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    CancelAndDisposeWithoutWaiting(ref hookCancellation);
                    CopilotCancellationBoundary.ObserveLateFault(hookTask);
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.BeforeExecute,
                        CopilotToolExecutionHookState.Cancelled,
                        hookStopwatch.ElapsedMilliseconds,
                        "tool_hook_cancelled");
                    Log.Warn(
                        $"Copilot pre-tool hook cancelled itself. Tool={context.Invocation.Tool.Name} CallId={context.Invocation.CallId} HookSource={binding.SourceId} Hook={binding.Hook.GetType().FullName}");
                    return CopilotToolExecutionHookDecision.Deny(
                        "A pre-execution hook was cancelled before it could authorize the tool call.",
                        "tool_hook_cancelled",
                        CopilotToolFailureKind.Internal);
                }
                catch (OperationCanceledException)
                {
                    CancelAndDisposeWithoutWaiting(ref hookCancellation);
                    CopilotCancellationBoundary.ObserveLateFault(hookTask);
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.BeforeExecute,
                        CopilotToolExecutionHookState.Cancelled,
                        hookStopwatch.ElapsedMilliseconds,
                        "tool_execution_cancelled");
                    throw;
                }
                catch (Exception ex)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.BeforeExecute,
                        CopilotToolExecutionHookState.Failed,
                        hookStopwatch.ElapsedMilliseconds,
                        "tool_hook_failed");
                    Log.Warn(
                        $"Copilot pre-tool hook failed. Tool={context.Invocation.Tool.Name} CallId={context.Invocation.CallId} HookSource={binding.SourceId} Hook={binding.Hook.GetType().FullName} ErrorType={ex.GetType().FullName}");
                    return CopilotToolExecutionHookDecision.Deny(
                        "A pre-execution hook failed before it could authorize the tool call.",
                        "tool_hook_failed",
                        CopilotToolFailureKind.Internal);
                }
                finally
                {
                    hookCancellation?.Dispose();
                }
            }

            return CopilotToolExecutionHookDecision.Proceed;
        }

        private async Task<CopilotToolExecutionOutcome> PublishOutcomeAsync(
            CopilotToolExecutionOutcome outcome,
            IReadOnlyList<CopilotToolExecutionHookBinding> hooks,
            List<CopilotToolExecutionHookRun> hookRuns,
            Action<CopilotAgentEvent> onEvent)
        {
            outcome.HookRuns = hookRuns;
            var phaseStopwatch = Stopwatch.StartNew();
            foreach (var binding in hooks)
            {
                var remaining = _hookPhaseTimeout - phaseStopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.AfterExecute,
                        CopilotToolExecutionHookState.Skipped,
                        0,
                        "tool_hook_phase_timeout");
                    Log.Warn($"Copilot post-tool hook phase timed out. Tool={outcome.Invocation.Tool.Name} CallId={outcome.Execution.CallId} HookSource={binding.SourceId}");
                    break;
                }

                CancellationTokenSource? hookCancellation = new();
                Task? hookTask = null;
                var hookStopwatch = Stopwatch.StartNew();
                try
                {
                    hookTask = binding.Hook.AfterExecuteAsync(outcome, hookCancellation.Token);
                    await hookTask.WaitAsync(remaining);
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.AfterExecute,
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
                        CopilotToolExecutionHookPhase.AfterExecute,
                        CopilotToolExecutionHookState.TimedOut,
                        hookStopwatch.ElapsedMilliseconds,
                        "tool_hook_timeout");
                    Log.Warn($"Copilot post-tool hook phase timed out. Tool={outcome.Invocation.Tool.Name} CallId={outcome.Execution.CallId} HookSource={binding.SourceId} Hook={binding.Hook.GetType().FullName}");
                    break;
                }
                catch (OperationCanceledException)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.AfterExecute,
                        CopilotToolExecutionHookState.Cancelled,
                        hookStopwatch.ElapsedMilliseconds,
                        "tool_hook_cancelled");
                    Log.Warn($"Copilot post-tool hook cancelled itself. Tool={outcome.Invocation.Tool.Name} CallId={outcome.Execution.CallId} HookSource={binding.SourceId} Hook={binding.Hook.GetType().FullName}");
                }
                catch (CopilotToolExecutionHookSkippedException ex)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.AfterExecute,
                        CopilotToolExecutionHookState.Skipped,
                        hookStopwatch.ElapsedMilliseconds,
                        ex.FailureCode);
                    Log.Info($"Copilot post-tool hook was skipped. Tool={outcome.Invocation.Tool.Name} CallId={outcome.Execution.CallId} HookSource={binding.SourceId} Hook={binding.Hook.GetType().FullName} FailureCode={ex.FailureCode}");
                }
                catch (Exception ex)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.AfterExecute,
                        CopilotToolExecutionHookState.Failed,
                        hookStopwatch.ElapsedMilliseconds,
                        "tool_hook_failed");
                    Log.Warn($"Copilot post-tool hook failed. Tool={outcome.Invocation.Tool.Name} CallId={outcome.Execution.CallId} HookSource={binding.SourceId} Hook={binding.Hook.GetType().FullName} ErrorType={ex.GetType().FullName}");
                }
                finally
                {
                    hookCancellation?.Dispose();
                }
            }

            outcome.HookRuns = hookRuns.ToArray();
            CopilotToolExecutionAuditLogger.Record(outcome);
            onEvent(CopilotAgentEvent.FromToolResult(outcome.Result, outcome.Execution, outcome.HookRuns));
            return outcome;
        }

        private static void RecordHookRun(
            List<CopilotToolExecutionHookRun> hookRuns,
            string sourceId,
            CopilotToolExecutionHookPhase phase,
            CopilotToolExecutionHookState state,
            long durationMs,
            string failureCode = "")
        {
            if (hookRuns.Count >= MaxRecordedHookRuns)
                return;

            hookRuns.Add(CopilotToolExecutionHookRun.Create(
                sourceId,
                phase,
                state,
                durationMs,
                failureCode));
        }

        private static CopilotToolPermissionRequestOutcome CreatePermissionRequestOutcome(
            IReadOnlyList<CopilotToolExecutionHookBinding> hooks,
            IReadOnlyList<CopilotToolExecutionHookRun> hookRuns,
            CopilotToolPermissionRequestDecision decision)
        {
            return new CopilotToolPermissionRequestOutcome
            {
                Decision = decision,
                HookRuns = hookRuns.ToArray(),
                HookBindings = hooks.ToArray(),
            };
        }

        private CopilotToolExecutionHookDecision CreateBeforeHookTimeoutDecision()
        {
            return CopilotToolExecutionHookDecision.Deny(
                $"The pre-execution hook phase exceeded its {FormatTimeout(_hookPhaseTimeout)} timeout.",
                "tool_hook_timeout",
                CopilotToolFailureKind.Internal);
        }
    }
}
