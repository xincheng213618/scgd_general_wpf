using log4net;
using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
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

        private CopilotToolExecutionOutcome CreateOutcome(
            CopilotToolInvocation invocation,
            CopilotToolExecutionState state,
            DateTimeOffset startedAt,
            TimeSpan timeout,
            Stopwatch stopwatch,
            CopilotToolResult result,
            long queueDurationMs = 0)
        {
            stopwatch.Stop();
            var completedAt = _utcNow();
            if (result.Approval != null
                && !CopilotMcpConfirmationStore.Instance.LinkAgentCall(
                    result.Approval.ActionId,
                    invocation.CallId,
                    invocation.AgentRequest,
                    invocation.ExecutionScope))
            {
                result = new CopilotToolResult
                {
                    ToolName = invocation.Tool.Name,
                    Success = false,
                    Summary = "The protected action could not be linked to this Copilot task.",
                    ErrorMessage = "ColorVision rejected an approval action whose source or task scope did not match the active tool call.",
                    FailureKind = CopilotToolFailureKind.Authorization,
                    FailureCode = "approval_scope_link_failed",
                };
                state = CopilotToolExecutionState.Denied;
            }

            var outcome = new CopilotToolExecutionOutcome
            {
                Invocation = invocation,
                Result = result,
                Execution = CreateExecutionInfo(
                    invocation,
                    state,
                    startedAt,
                    completedAt,
                    stopwatch.ElapsedMilliseconds,
                    timeout,
                    result.Approval?.ActionId,
                    result.Success ? CopilotToolFailureKind.None : NormalizeFailureKind(result.FailureKind),
                    CopilotToolRetryPolicy.IsRetryEligible(invocation, result, state),
                    queueDurationMs),
            };

            return outcome;
        }

        private static CopilotToolInvocation NormalizeInvocation(CopilotToolInvocation invocation, string callId)
        {
            var toolInput = invocation.ToolInput ?? CopilotAgentToolInput.Empty;
            var toolCall = invocation.ToolCall ?? new CopilotToolCall();
            var executionSignature = CopilotAgentToolInputExactBinding.CreateExecutionSignature(
                invocation.Tool.Name,
                toolInput);
            var executionScope = invocation.ExecutionScope.IsEmpty
                ? CopilotExecutionScope.ForAgentRequest(invocation.AgentRequest)
                : invocation.ExecutionScope;
            executionScope = executionScope.BindToolCall(
                invocation.Tool.Name,
                callId,
                executionSignature);
            if (string.IsNullOrWhiteSpace(toolCall.ToolName))
            {
                toolCall = new CopilotToolCall
                {
                    ToolName = invocation.Tool.Name,
                    ToolInput = toolInput,
                    Reason = toolCall.Reason,
                    IsFallback = toolCall.IsFallback,
                };
            }

            return new CopilotToolInvocation
            {
                CallId = callId,
                Round = Math.Max(1, invocation.Round),
                Attempt = Math.Max(1, invocation.Attempt),
                MaxAttempts = Math.Max(Math.Max(1, invocation.Attempt), invocation.MaxAttempts),
                RuntimeName = string.IsNullOrWhiteSpace(invocation.RuntimeName) ? "agent" : invocation.RuntimeName.Trim(),
                Tool = invocation.Tool,
                AgentRequest = invocation.AgentRequest,
                ExecutionScope = executionScope,
                ToolInput = toolInput,
                ToolCall = toolCall,
                FrameworkApprovalGranted = invocation.FrameworkApprovalGranted,
                ApprovalActionId = invocation.ApprovalActionId?.Trim() ?? string.Empty,
                ConcurrencyMode = ResolveConcurrencyMode(invocation.Tool),
                ConcurrencyKey = ResolveConcurrencyKey(invocation.Tool, invocation.AgentRequest, toolInput),
                PreviousObservationProgressSignature =
                    invocation.PreviousObservationProgressSignature,
                InitialHookRuns = invocation.InitialHookRuns
                    .Where(run => run?.IsStructurallyValid() == true)
                    .Take(MaxRecordedHookRuns)
                    .ToArray(),
                InitialHookBindings = invocation.InitialHookBindings
                    .Where(binding => binding?.Hook != null)
                    .Take(CopilotToolExecutionHookRegistry.MaxRegistrations + 1)
                    .ToArray(),
            };
        }

        private static CopilotToolExecutionInfo CreateExecutionInfo(
            CopilotToolInvocation invocation,
            CopilotToolExecutionState state,
            DateTimeOffset startedAt,
            DateTimeOffset? completedAt,
            long durationMs,
            TimeSpan timeout,
            string? approvalActionId = null,
            CopilotToolFailureKind failureKind = CopilotToolFailureKind.None,
            bool retryEligible = false,
            long queueDurationMs = 0)
        {
            var capability = invocation.Tool.Capability;
            return new CopilotToolExecutionInfo
            {
                CallId = invocation.CallId,
                Round = invocation.Round,
                Attempt = invocation.Attempt,
                MaxAttempts = invocation.MaxAttempts,
                RuntimeName = invocation.RuntimeName,
                ToolName = invocation.Tool.Name,
                Access = capability.Access,
                RiskLevel = capability.RiskLevel,
                ApprovalMode = capability.ApprovalMode,
                Idempotency = capability.Idempotency,
                ConcurrencyMode = invocation.ConcurrencyMode,
                ConcurrencyKey = invocation.ConcurrencyKey,
                ApprovalActionId = !string.IsNullOrWhiteSpace(approvalActionId)
                    ? approvalActionId.Trim()
                    : invocation.ApprovalActionId?.Trim() ?? string.Empty,
                ArgumentSummary = CopilotToolExecutionAuditLogger.CreateArgumentSummary(invocation.Tool, invocation.ToolInput),
                State = state,
                FailureKind = failureKind,
                RetryEligible = retryEligible,
                StartedAtUtc = startedAt,
                CompletedAtUtc = completedAt,
                DurationMs = Math.Max(0, durationMs),
                QueueDurationMs = Math.Max(0, queueDurationMs),
                TimeoutMs = Math.Max(1, (long)timeout.TotalMilliseconds),
            };
        }

        internal static CopilotToolConcurrencyMode ResolveConcurrencyMode(ICopilotTool tool)
        {
            return tool.Capability.EffectiveConcurrencyMode;
        }

        internal static string ResolveConcurrencyKey(ICopilotTool tool, CopilotAgentRequest request, CopilotAgentToolInput toolInput)
        {
            var key = tool.GetConcurrencyKey(request, toolInput)?.Trim();
            key = string.IsNullOrWhiteSpace(key) ? $"tool:{tool.Name}" : key;
            var fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToUpperInvariant()));
            return $"resource:{Convert.ToHexString(fingerprint.AsSpan(0, 8)).ToLowerInvariant()}";
        }

        private static CopilotToolResult Failure(
            string toolName,
            string summary,
            string errorMessage,
            CopilotToolFailureKind failureKind,
            string failureCode = "")
        {
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = false,
                Summary = summary,
                ErrorMessage = errorMessage,
                FailureKind = failureKind,
                FailureCode = CopilotToolFailureCode.Normalize(failureCode),
            };
        }

        private static CopilotToolFailureKind NormalizeFailureKind(CopilotToolFailureKind failureKind)
        {
            return failureKind == CopilotToolFailureKind.None ? CopilotToolFailureKind.Unspecified : failureKind;
        }

        private static string FormatTimeout(TimeSpan timeout)
        {
            return timeout.TotalSeconds >= 1
                ? $"{timeout.TotalSeconds:0.#}-second"
                : $"{timeout.TotalMilliseconds:0}-millisecond";
        }

        private static string FormatElapsed(long elapsedMs)
        {
            return elapsedMs < 1000
                ? $"{Math.Max(0, elapsedMs)} ms"
                : $"{elapsedMs / 1000d:0.#} s";
        }

        private static void CancelAndDisposeWithoutWaiting(ref CancellationTokenSource? cancellation)
        {
            var ownedCancellation = Interlocked.Exchange(ref cancellation, null);
            if (ownedCancellation != null)
                _ = CancelAndDisposeAsync(ownedCancellation);
        }

        private static async Task CancelAndDisposeAsync(CancellationTokenSource cancellation)
        {
            try
            {
                await cancellation.CancelAsync();
            }
            catch (Exception ex)
            {
                Log.Warn("Copilot tool hook cancellation failed.", ex);
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        private sealed class DeferredExecutionLease : IDisposable
        {
            private IDisposable? _lease;

            public DeferredExecutionLease(IDisposable lease)
            {
                _lease = lease ?? throw new ArgumentNullException(nameof(lease));
            }

            public void HoldUntilCompleted(Task? executionTask)
            {
                if (executionTask == null || executionTask.IsCompleted)
                    return;

                var lease = Interlocked.Exchange(ref _lease, null);
                if (lease != null)
                    _ = ReleaseAfterCompletionAsync(executionTask, lease);
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref _lease, null)?.Dispose();
            }

            private static async Task ReleaseAfterCompletionAsync(Task executionTask, IDisposable lease)
            {
                try
                {
                    await executionTask.ConfigureAwait(false);
                }
                catch
                {
                }
                finally
                {
                    lease.Dispose();
                }
            }
        }
    }
}
