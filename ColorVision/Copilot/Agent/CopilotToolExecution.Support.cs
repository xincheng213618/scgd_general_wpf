using ColorVision.Copilot.Mcp;
using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotToolExecutor
    {
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

        private static bool TryNormalizeInvocation(
            CopilotToolInvocation invocation,
            string callId,
            out CopilotToolInvocation normalized,
            out string error)
        {
            if (!CopilotAgentToolInputSnapshot.TryCreate(
                    invocation.ToolInput,
                    out var toolInput,
                    out var inputSnapshotError))
            {
                normalized = CreateNormalizedInvocation(
                    invocation,
                    callId,
                    CopilotAgentToolInput.Empty,
                    inputIsValid: false);
                error = inputSnapshotError;
                return false;
            }

            CopilotAgentToolInput boundInput;
            try
            {
                var inputSchema = invocation.Tool.InputSchema;
                if (inputSchema == null)
                {
                    normalized = CreateNormalizedInvocation(
                        invocation,
                        callId,
                        toolInput,
                        inputIsValid: false);
                    error = "The registered tool does not declare an input schema.";
                    return false;
                }
                if (!inputSchema.TryBind(toolInput.Arguments, out boundInput, out error))
                {
                    normalized = CreateNormalizedInvocation(
                        invocation,
                        callId,
                        toolInput,
                        inputIsValid: false);
                    return false;
                }
            }
            catch
            {
                normalized = CreateNormalizedInvocation(
                    invocation,
                    callId,
                    toolInput,
                    inputIsValid: false);
                error = "The tool arguments could not be validated safely.";
                return false;
            }

            if (!CopilotAgentToolInputSnapshot.TryCreate(
                    boundInput,
                    out toolInput,
                    out inputSnapshotError))
            {
                normalized = CreateNormalizedInvocation(
                    invocation,
                    callId,
                    CopilotAgentToolInput.Empty,
                    inputIsValid: false);
                error = inputSnapshotError;
                return false;
            }

            normalized = CreateNormalizedInvocation(
                invocation,
                callId,
                toolInput,
                inputIsValid: true);
            error = string.Empty;
            return true;
        }

        private static CopilotToolInvocation CreateNormalizedInvocation(
            CopilotToolInvocation invocation,
            string callId,
            CopilotAgentToolInput toolInput,
            bool inputIsValid)
        {
            var sourceToolCall = invocation.ToolCall ?? new CopilotToolCall();
            var toolCall = new CopilotToolCall
            {
                ToolName = invocation.Tool.Name,
                ToolInput = toolInput,
                Reason = sourceToolCall.Reason ?? string.Empty,
                IsFallback = sourceToolCall.IsFallback,
            };
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
                ApprovalPromptCategoryOverride = invocation.ApprovalPromptCategoryOverride,
                ApprovalPromptReasonOverride = invocation.ApprovalPromptReasonOverride?.Trim() ?? string.Empty,
                ConcurrencyMode = inputIsValid
                    ? ResolveConcurrencyMode(invocation.Tool)
                    : CopilotToolConcurrencyMode.Exclusive,
                ConcurrencyKey = inputIsValid
                    ? ResolveConcurrencyKey(invocation.Tool, invocation.AgentRequest, toolInput)
                    : ResolveRejectedInputConcurrencyKey(invocation.Tool.Name),
                PreviousObservationProgressSignature =
                    invocation.PreviousObservationProgressSignature,
                InitialHookRuns = invocation.InitialHookRuns
                    .Where(run => run?.IsStructurallyValid() == true)
                    .Where(run => invocation.AgentRequest.CodexExtensionHooksEnabled
                        || !IsExtensionHookSource(run.SourceId))
                    .Take(MaxRecordedHookRuns)
                    .ToArray(),
                InitialHookBindings = invocation.InitialHookBindings
                    .Where(binding => binding?.Hook != null)
                    .Where(binding => invocation.AgentRequest.CodexExtensionHooksEnabled
                        || !IsExtensionHookSource(binding.SourceId))
                    .Take(MaxInvocationHookBindings)
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
            return CreateConcurrencyKey(key);
        }

        private static string ResolveRejectedInputConcurrencyKey(string toolName)
        {
            return CreateConcurrencyKey($"invalid-input:{toolName?.Trim()}");
        }

        private static string CreateConcurrencyKey(string key)
        {
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

        private static CopilotToolResult CreateExecutionBoundaryFailure(
            CopilotToolInvocation invocation,
            TimeSpan timeout,
            bool wasCancelled,
            bool outcomeUnknown)
        {
            if (outcomeUnknown)
            {
                var boundary = wasCancelled
                    ? "caller cancellation"
                    : $"its {FormatTimeout(timeout)} execution timeout";
                return Failure(
                    invocation.Tool.Name,
                    $"{invocation.Tool.Name} crossed {boundary} before its final outcome was known.",
                    "The operation may still be completing or may already have completed. Verify the current external state before retrying.",
                    CopilotToolFailureKind.OutcomeUnknown,
                    CopilotToolFailureCode.OutcomeUnknown);
            }

            return wasCancelled
                ? Failure(
                    invocation.Tool.Name,
                    $"{invocation.Tool.Name} was cancelled.",
                    "Tool execution was cancelled.",
                    CopilotToolFailureKind.Cancelled)
                : Failure(
                    invocation.Tool.Name,
                    $"{invocation.Tool.Name} timed out.",
                    $"The tool exceeded its {FormatTimeout(timeout)} execution timeout.",
                    CopilotToolFailureKind.Transient);
        }

        private static bool HasUnknownOutcomeAfterExecutionBoundary(CopilotToolInvocation invocation)
        {
            var capability = invocation.Tool.Capability;
            return capability.Access == CopilotToolAccess.Write
                || capability.Idempotency != CopilotToolIdempotency.Idempotent;
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
