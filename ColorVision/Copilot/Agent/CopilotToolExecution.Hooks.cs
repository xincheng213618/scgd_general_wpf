using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
            CopilotToolExecutionHookEventPublisher hookEvents,
            CancellationToken cancellationToken)
        {
            var phaseStopwatch = Stopwatch.StartNew();
            foreach (var binding in hooks)
            {
                if (!binding.Phases.HasFlag(CopilotToolExecutionHookPhases.BeforeExecute))
                    continue;
                if (binding.ExecutionMode == CopilotToolExecutionHookMode.Async)
                {
                    ScheduleAsyncHook(
                        binding,
                        CopilotToolExecutionHookPhase.BeforeExecute,
                        context.Invocation,
                        hookRuns,
                        async token =>
                        {
                            CopilotToolPreExecutionOutput? output = null;
                            CopilotToolExecutionHookDecision decision;
                            if (binding.Hook is ICopilotToolPreExecutionOutputHook outputHook)
                            {
                                output = await outputHook.BeforeExecuteWithOutputAsync(
                                    context,
                                    token).ConfigureAwait(false);
                                decision = output?.Decision
                                    ?? CopilotToolExecutionHookDecision.Proceed;
                            }
                            else
                            {
                                decision = await binding.Hook.BeforeExecuteAsync(
                                    context,
                                    token).ConfigureAwait(false);
                            }
                            if (decision?.ShouldProceed == false)
                            {
                                Log.Warn(
                                    $"Copilot async pre-tool hook control decision was ignored. Tool={context.Invocation.Tool.Name} CallId={context.Invocation.CallId} HookSource={binding.SourceId}");
                            }
                            if (output?.HasOutput == true
                                && binding.Hook is not CopilotCodexCommandHook)
                            {
                                Log.Warn(
                                    $"Copilot async pre-tool hook output was ignored by the notification-only execution mode. Tool={context.Invocation.Tool.Name} CallId={context.Invocation.CallId} HookSource={binding.SourceId}");
                            }
                            return CopilotCodexAsyncHookOutput.From(output, decision);
                        });
                    continue;
                }

                BeginHookRun(
                    hookRuns,
                    hookEvents,
                    binding.SourceId,
                    CopilotToolExecutionHookPhase.BeforeExecute);
                var remaining = GetHookTimeout(binding, phaseStopwatch);
                if (remaining <= TimeSpan.Zero)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.BeforeExecute,
                        CopilotToolExecutionHookState.Skipped,
                        0,
                        "tool_hook_phase_timeout",
                        hookEvents);
                    return CreateBeforeHookTimeoutDecision(_hookPhaseTimeout);
                }

                CancellationTokenSource? hookCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task? hookTask = null;
                var hookStopwatch = Stopwatch.StartNew();
                try
                {
                    CopilotToolPreExecutionOutput? output = null;
                    CopilotToolExecutionHookDecision decision;
                    if (binding.Hook is ICopilotToolPreExecutionOutputHook outputHook)
                    {
                        var outputTask = outputHook.BeforeExecuteWithOutputAsync(
                            context,
                            hookCancellation.Token);
                        hookTask = outputTask;
                        output = await outputTask.WaitAsync(remaining, cancellationToken);
                        decision = output?.Decision
                            ?? CopilotToolExecutionHookDecision.Proceed;
                    }
                    else
                    {
                        var decisionTask = binding.Hook.BeforeExecuteAsync(
                            context,
                            hookCancellation.Token);
                        hookTask = decisionTask;
                        decision = await decisionTask.WaitAsync(remaining, cancellationToken)
                            ?? CopilotToolExecutionHookDecision.Proceed;
                    }
                    ApplyPreExecutionOutput(
                        context.Invocation,
                        output,
                        binding.SourceId,
                        hookEvents);
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
                                : decision.FailureCode,
                            hookEvents);
                        return decision;
                    }
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.BeforeExecute,
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
                        CopilotToolExecutionHookPhase.BeforeExecute,
                        CopilotToolExecutionHookState.TimedOut,
                        hookStopwatch.ElapsedMilliseconds,
                        "tool_hook_timeout",
                        hookEvents);
                    return CreateBeforeHookTimeoutDecision(remaining);
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
                        "tool_hook_cancelled",
                        hookEvents);
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
                        "tool_execution_cancelled",
                        hookEvents);
                    throw;
                }
                catch (Exception ex) when (ex is not CopilotToolExecutionHookEventDispatchException)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.BeforeExecute,
                        CopilotToolExecutionHookState.Failed,
                        hookStopwatch.ElapsedMilliseconds,
                        "tool_hook_failed",
                        hookEvents);
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
            Action<CopilotAgentEvent> onEvent,
            bool toolWasExecuted = false)
        {
            try
            {
                return await RunPostHooksAndPublishOutcomeAsync(
                    outcome,
                    hooks,
                    hookRuns,
                    onEvent,
                    toolWasExecuted);
            }
            catch (CopilotToolExecutionHookEventDispatchException ex)
            {
                SealOutcome(outcome, hookRuns, toolWasExecuted);
                throw new CopilotToolResultEventDispatchException(
                    outcome,
                    ex.InnerException ?? ex);
            }
        }

        private async Task<CopilotToolExecutionOutcome> RunPostHooksAndPublishOutcomeAsync(
            CopilotToolExecutionOutcome outcome,
            IReadOnlyList<CopilotToolExecutionHookBinding> hooks,
            List<CopilotToolExecutionHookRun> hookRuns,
            Action<CopilotAgentEvent> onEvent,
            bool toolWasExecuted)
        {
            foreach (var context in outcome.Invocation.PreToolAdditionalContexts)
            {
                outcome.AddModelAdditionalContext(
                    context.Text,
                    context.MaximumTokens,
                    isPreToolUse: true);
            }
            outcome.HookRuns = CreateHookRunSnapshot(hookRuns);
            var hookEvents = new CopilotToolExecutionHookEventPublisher(
                onEvent,
                () => CreateExecutionInfo(
                    outcome.Invocation,
                    CopilotToolExecutionState.Running,
                    outcome.Execution.StartedAtUtc,
                    completedAt: null,
                    outcome.Execution.DurationMs,
                    TimeSpan.FromMilliseconds(outcome.Execution.TimeoutMs),
                    outcome.Execution.ApprovalActionId,
                    queueDurationMs: outcome.Execution.QueueDurationMs));
            var phaseStopwatch = Stopwatch.StartNew();
            foreach (var binding in hooks)
            {
                if (!binding.Phases.HasFlag(CopilotToolExecutionHookPhases.AfterExecute))
                    continue;
                if (binding.Hook is ICopilotToolPostExecutionOutputHook
                    && (!toolWasExecuted
                        || outcome.Execution.State == CopilotToolExecutionState.AwaitingApproval))
                {
                    continue;
                }
                if (binding.ExecutionMode == CopilotToolExecutionHookMode.Async)
                {
                    ScheduleAsyncHook(
                        binding,
                        CopilotToolExecutionHookPhase.AfterExecute,
                        outcome.Invocation,
                        hookRuns,
                        token => RunAsyncPostHookNotificationAsync(binding.Hook, outcome, token));
                    continue;
                }

                BeginHookRun(
                    hookRuns,
                    hookEvents,
                    binding.SourceId,
                    CopilotToolExecutionHookPhase.AfterExecute);
                var remaining = GetHookTimeout(binding, phaseStopwatch);
                if (remaining <= TimeSpan.Zero)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.AfterExecute,
                        CopilotToolExecutionHookState.Skipped,
                        0,
                        "tool_hook_phase_timeout",
                        hookEvents);
                    Log.Warn($"Copilot post-tool hook phase timed out. Tool={outcome.Invocation.Tool.Name} CallId={outcome.Execution.CallId} HookSource={binding.SourceId}");
                    break;
                }

                CancellationTokenSource? hookCancellation = new();
                Task? hookTask = null;
                var hookStopwatch = Stopwatch.StartNew();
                try
                {
                    CopilotToolPostExecutionOutput? output = null;
                    if (binding.Hook is ICopilotToolPostExecutionOutputHook outputHook)
                    {
                        var outputTask = outputHook.AfterExecuteWithOutputAsync(
                            outcome,
                            hookCancellation.Token);
                        hookTask = outputTask;
                        output = await outputTask.WaitAsync(remaining);
                    }
                    else
                    {
                        hookTask = binding.Hook.AfterExecuteAsync(outcome, hookCancellation.Token);
                        await hookTask.WaitAsync(remaining);
                    }
                    var state = ApplyPostExecutionOutput(
                        outcome,
                        output,
                        binding.SourceId,
                        onEvent);
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.AfterExecute,
                        state,
                        hookStopwatch.ElapsedMilliseconds,
                        output?.HasFailure == true
                            ? "configured_hook_invalid_output"
                            : string.Empty,
                        hookEvents: hookEvents);
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
                        "tool_hook_timeout",
                        hookEvents);
                    Log.Warn($"Copilot post-tool hook exceeded its {FormatTimeout(remaining)} timeout. Tool={outcome.Invocation.Tool.Name} CallId={outcome.Execution.CallId} HookSource={binding.SourceId} Hook={binding.Hook.GetType().FullName}");
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
                        "tool_hook_cancelled",
                        hookEvents);
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
                        ex.FailureCode,
                        hookEvents);
                    Log.Info($"Copilot post-tool hook was skipped. Tool={outcome.Invocation.Tool.Name} CallId={outcome.Execution.CallId} HookSource={binding.SourceId} Hook={binding.Hook.GetType().FullName} FailureCode={ex.FailureCode}");
                }
                catch (Exception ex) when (ex is not CopilotToolExecutionHookEventDispatchException)
                {
                    RecordHookRun(
                        hookRuns,
                        binding.SourceId,
                        CopilotToolExecutionHookPhase.AfterExecute,
                        CopilotToolExecutionHookState.Failed,
                        hookStopwatch.ElapsedMilliseconds,
                        "tool_hook_failed",
                        hookEvents);
                    Log.Warn($"Copilot post-tool hook failed. Tool={outcome.Invocation.Tool.Name} CallId={outcome.Execution.CallId} HookSource={binding.SourceId} Hook={binding.Hook.GetType().FullName} ErrorType={ex.GetType().FullName}");
                }
                finally
                {
                    hookCancellation?.Dispose();
                }
            }

            SealOutcome(outcome, hookRuns, toolWasExecuted);
            try
            {
                onEvent(CopilotAgentEvent.FromToolResult(
                    outcome.Result,
                    outcome.Execution,
                    outcome.HookRuns,
                    outcome.FormattedModelResult));
            }
            catch (Exception ex)
            {
                throw new CopilotToolResultEventDispatchException(outcome, ex);
            }
            return outcome;
        }

        private static void SealOutcome(
            CopilotToolExecutionOutcome outcome,
            List<CopilotToolExecutionHookRun> hookRuns,
            bool toolWasExecuted)
        {
            if (toolWasExecuted)
                AddChangedPathProjectInstructions(outcome);
            outcome.HookRuns = CreateHookRunSnapshot(hookRuns);
            outcome.FormattedModelResult = CopilotToolOutputArchivePolicy.Format(
                outcome,
                outcome.Invocation.AgentRequest.ToolOutputTokenLimitOverride);
            RecordReviewEvidence(outcome);
            CopilotToolExecutionAuditLogger.Record(outcome);
        }

        private static ReadOnlyCollection<CopilotToolExecutionHookRun> CreateHookRunSnapshot(
            List<CopilotToolExecutionHookRun> hookRuns) =>
            Array.AsReadOnly(hookRuns.ToArray());

        private static async Task<CopilotCodexAsyncHookOutput?> RunAsyncPostHookNotificationAsync(
            ICopilotToolExecutionHook hook,
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken)
        {
            if (hook is ICopilotToolPostExecutionOutputHook outputHook)
            {
                var output = await outputHook.AfterExecuteWithOutputAsync(
                    outcome,
                    cancellationToken).ConfigureAwait(false);
                if (output?.HasOutput == true
                    && hook is not CopilotCodexCommandHook)
                {
                    Log.Warn(
                        $"Copilot async post-tool hook output was ignored by the notification-only execution mode. Tool={outcome.Invocation.Tool.Name} CallId={outcome.Execution.CallId} Hook={hook.GetType().FullName}");
                }
                return CopilotCodexAsyncHookOutput.From(output);
            }

            await hook.AfterExecuteAsync(outcome, cancellationToken).ConfigureAwait(false);
            return CopilotCodexAsyncHookOutput.Empty;
        }

        private static void ApplyPreExecutionOutput(
            CopilotToolInvocation invocation,
            CopilotToolPreExecutionOutput? output,
            string sourceId,
            CopilotToolExecutionHookEventPublisher hookEvents)
        {
            if (output == null)
                return;

            var systemMessage = CopilotApprovalRequestReason.Normalize(output.SystemMessage);
            if (systemMessage.Length > 0)
            {
                hookEvents.Diagnostic(
                    $"PreToolUse hook warning · {sourceId}: {systemMessage}");
            }
            invocation.AddPreToolAdditionalContext(
                output.AdditionalContext,
                output.AdditionalContextLimitTokens);
        }

        private static CopilotToolExecutionHookState ApplyPostExecutionOutput(
            CopilotToolExecutionOutcome outcome,
            CopilotToolPostExecutionOutput? output,
            string sourceId,
            Action<CopilotAgentEvent> onEvent)
        {
            if (output == null)
                return CopilotToolExecutionHookState.Completed;

            var systemMessage = CopilotApprovalRequestReason.Normalize(output.SystemMessage);
            if (systemMessage.Length > 0)
            {
                onEvent(CopilotAgentEvent.RuntimeDiagnostic(
                    $"PostToolUse hook warning · {sourceId}: {systemMessage}"));
            }
            if (output.HasFailure)
            {
                Log.Warn(
                    $"Copilot post-tool hook returned invalid output. Tool={outcome.Invocation.Tool.Name} CallId={outcome.Execution.CallId} HookSource={sourceId} Detail={CopilotUserFacingErrorFormatter.Sanitize(output.FailureMessage)}");
                return CopilotToolExecutionHookState.Failed;
            }

            outcome.ApplyModelVisibleFeedback(output.FeedbackMessage);
            outcome.ApplyPostExecutionControl(output.Control, output.FeedbackMessage);
            outcome.AddModelAdditionalContext(
                output.AdditionalContext,
                output.AdditionalContextLimitTokens);
            return output.Control switch
            {
                CopilotToolPostExecutionControl.Blocked =>
                    CopilotToolExecutionHookState.Blocked,
                CopilotToolPostExecutionControl.Stopped =>
                    CopilotToolExecutionHookState.Stopped,
                _ => CopilotToolExecutionHookState.Completed,
            };
        }

        private void ScheduleAsyncHook(
            CopilotToolExecutionHookBinding binding,
            CopilotToolExecutionHookPhase phase,
            CopilotToolInvocation invocation,
            List<CopilotToolExecutionHookRun> hookRuns,
            Func<CancellationToken, Task<CopilotCodexAsyncHookOutput?>> callback)
        {
            var scheduled = binding.Hook is CopilotCodexCommandHook
                ? CopilotCodexLifecycleHookBackgroundScheduler.Shared.TrySchedule(
                    invocation.AgentRequest.ConversationId,
                    binding.SourceId,
                    GetConfiguredHookEventName(phase),
                    invocation.AgentRequest.TaskId,
                    binding.ExecutionTimeout ?? _hookPhaseTimeout,
                    callback)
                : CopilotToolExecutionHookBackgroundScheduler.Shared.TrySchedule(
                    binding.SourceId,
                    phase,
                    invocation.Tool.Name,
                    invocation.CallId,
                    binding.ExecutionTimeout ?? _hookPhaseTimeout,
                    async cancellationToken =>
                    {
                        _ = await callback(cancellationToken).ConfigureAwait(false);
                    });
            RecordHookRun(
                hookRuns,
                binding.SourceId,
                phase,
                scheduled
                    ? CopilotToolExecutionHookState.Scheduled
                    : CopilotToolExecutionHookState.Skipped,
                durationMs: 0,
                failureCode: scheduled ? string.Empty : "tool_hook_async_queue_full",
                executionMode: CopilotToolExecutionHookMode.Async);
            if (!scheduled)
            {
                Log.Warn(
                    $"Copilot async tool hook queue is full. Tool={invocation.Tool.Name} CallId={invocation.CallId} HookSource={binding.SourceId} Phase={phase}");
            }
        }

        private static string GetConfiguredHookEventName(
            CopilotToolExecutionHookPhase phase) => phase switch
        {
            CopilotToolExecutionHookPhase.PermissionRequest => "PermissionRequest",
            CopilotToolExecutionHookPhase.BeforeExecute => "PreToolUse",
            CopilotToolExecutionHookPhase.AfterExecute => "PostToolUse",
            _ => throw new ArgumentOutOfRangeException(nameof(phase)),
        };

        private static void BeginHookRun(
            List<CopilotToolExecutionHookRun> hookRuns,
            CopilotToolExecutionHookEventPublisher hookEvents,
            string sourceId,
            CopilotToolExecutionHookPhase phase)
        {
            if (hookRuns.Count < MaxRecordedHookRuns)
                hookEvents.Started(sourceId, phase);
        }

        private static void RecordHookRun(
            List<CopilotToolExecutionHookRun> hookRuns,
            string sourceId,
            CopilotToolExecutionHookPhase phase,
            CopilotToolExecutionHookState state,
            long durationMs,
            string failureCode = "",
            CopilotToolExecutionHookEventPublisher? hookEvents = null,
            CopilotToolExecutionHookMode executionMode = CopilotToolExecutionHookMode.Sync)
        {
            if (hookRuns.Count >= MaxRecordedHookRuns)
                return;

            var hookRun = CopilotToolExecutionHookRun.Create(
                sourceId,
                phase,
                state,
                durationMs,
                failureCode,
                executionMode);
            hookRuns.Add(hookRun);
            hookEvents?.Completed(hookRun);
        }

        private sealed class CopilotToolExecutionHookEventPublisher
        {
            private readonly Action<CopilotAgentEvent>? _onEvent;
            private readonly Func<CopilotToolExecutionInfo> _executionFactory;

            public CopilotToolExecutionHookEventPublisher(
                Action<CopilotAgentEvent>? onEvent,
                Func<CopilotToolExecutionInfo> executionFactory)
            {
                _onEvent = onEvent;
                _executionFactory = executionFactory ?? throw new ArgumentNullException(nameof(executionFactory));
            }

            public void Started(string sourceId, CopilotToolExecutionHookPhase phase)
            {
                if (_onEvent != null)
                    Publish(CopilotAgentEvent.HookStarted(_executionFactory(), sourceId, phase));
            }

            public void Completed(CopilotToolExecutionHookRun hookRun)
            {
                if (_onEvent != null)
                    Publish(CopilotAgentEvent.HookCompleted(_executionFactory(), hookRun));
            }

            public void Diagnostic(string message)
            {
                if (_onEvent != null)
                    Publish(CopilotAgentEvent.RuntimeDiagnostic(message));
            }

            private void Publish(CopilotAgentEvent agentEvent)
            {
                if (_onEvent == null)
                    return;

                try
                {
                    _onEvent(agentEvent);
                }
                catch (Exception ex)
                {
                    throw new CopilotToolExecutionHookEventDispatchException(ex);
                }
            }
        }

        private sealed class CopilotToolExecutionHookEventDispatchException : Exception
        {
            public CopilotToolExecutionHookEventDispatchException(Exception innerException)
                : base("A Copilot tool hook lifecycle event could not be published.", innerException)
            {
            }
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

        private TimeSpan GetHookTimeout(
            CopilotToolExecutionHookBinding binding,
            Stopwatch phaseStopwatch)
        {
            return binding.ExecutionTimeout
                ?? (_hookPhaseTimeout - phaseStopwatch.Elapsed);
        }

        private static CopilotToolExecutionHookDecision CreateBeforeHookTimeoutDecision(
            TimeSpan timeout)
        {
            return CopilotToolExecutionHookDecision.Deny(
                $"A pre-execution hook exceeded its {FormatTimeout(timeout)} timeout.",
                "tool_hook_timeout",
                CopilotToolFailureKind.Internal);
        }
    }
}
