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
    public sealed class CopilotToolInvocation
    {
        public string CallId { get; init; } = string.Empty;

        public int Round { get; init; }

        public int Attempt { get; init; } = 1;

        public int MaxAttempts { get; init; } = 1;

        public string RuntimeName { get; init; } = string.Empty;

        public ICopilotTool Tool { get; init; } = null!;

        public CopilotAgentRequest AgentRequest { get; init; } = null!;

        public CopilotAgentToolInput ToolInput { get; init; } = CopilotAgentToolInput.Empty;

        public CopilotToolCall ToolCall { get; init; } = new();

        public bool FrameworkApprovalGranted { get; internal init; }

        public string ApprovalActionId { get; internal init; } = string.Empty;

        public CopilotToolConcurrencyMode ConcurrencyMode { get; internal init; }

        public string ConcurrencyKey { get; internal init; } = string.Empty;
    }

    public sealed class CopilotToolExecutionOutcome
    {
        public CopilotToolInvocation Invocation { get; init; } = null!;

        public CopilotToolResult Result { get; init; } = new();

        public CopilotToolExecutionInfo Execution { get; init; } = new();

        public CopilotAgentStepRecord StepRecord => new()
        {
            Round = Invocation.Round,
            ToolCall = Invocation.ToolCall,
            Observation = CopilotToolObservation.FromResult(Result),
            Execution = Execution,
        };
    }

    public sealed class CopilotToolExecutionHookContext
    {
        public CopilotToolInvocation Invocation { get; init; } = null!;

        public DateTimeOffset StartedAtUtc { get; init; }

        public TimeSpan Timeout { get; init; }
    }

    public sealed class CopilotToolExecutionHookDecision
    {
        public static CopilotToolExecutionHookDecision Proceed { get; } = new() { ShouldProceed = true };

        public bool ShouldProceed { get; init; }

        public string Reason { get; init; } = string.Empty;

        public static CopilotToolExecutionHookDecision Deny(string reason) => new()
        {
            ShouldProceed = false,
            Reason = reason ?? string.Empty,
        };
    }

    public interface ICopilotToolExecutionHook
    {
        Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(CopilotToolExecutionHookContext context, CancellationToken cancellationToken);

        Task AfterExecuteAsync(CopilotToolExecutionOutcome outcome, CancellationToken cancellationToken);
    }

    public sealed class CopilotWriteToolPolicyHook : ICopilotToolExecutionHook
    {
        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(CopilotToolExecutionHookContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = context.Invocation;
            if (invocation.Tool.Access == CopilotToolAccess.ReadOnly)
                return Task.FromResult(CopilotToolExecutionHookDecision.Proceed);

            if (invocation.Tool.RiskLevel == CopilotToolRiskLevel.High
                && invocation.Tool.ApprovalMode == CopilotToolApprovalMode.Never)
            {
                return Task.FromResult(CopilotToolExecutionHookDecision.Deny("High-risk write tools must declare an approval policy."));
            }

            if (invocation.AgentRequest.Mode == CopilotAgentMode.Chat || string.IsNullOrWhiteSpace(invocation.AgentRequest.UserText))
                return Task.FromResult(CopilotToolExecutionHookDecision.Deny("Write-capable tools require a non-empty explicit user request outside Chat mode."));

            try
            {
                if (!invocation.Tool.CanHandle(invocation.AgentRequest))
                    return Task.FromResult(CopilotToolExecutionHookDecision.Deny("The current request no longer authorizes this write-capable tool."));
            }
            catch (Exception ex)
            {
                return Task.FromResult(CopilotToolExecutionHookDecision.Deny($"Write-tool authorization failed: {ex.Message}"));
            }

            return Task.FromResult(CopilotToolExecutionHookDecision.Proceed);
        }

        public Task AfterExecuteAsync(CopilotToolExecutionOutcome outcome, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    public sealed class CopilotToolExecutor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CopilotToolExecutor));
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan MaximumTimeout = TimeSpan.FromMinutes(10);

        private readonly IReadOnlyList<ICopilotToolExecutionHook> _hooks;
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly CopilotToolExecutionGate _executionGate;

        public CopilotToolExecutor(IEnumerable<ICopilotToolExecutionHook>? hooks = null, Func<DateTimeOffset>? utcNow = null)
        {
            var configuredHooks = hooks?.Where(hook => hook != null) ?? Enumerable.Empty<ICopilotToolExecutionHook>();
            _hooks = new ICopilotToolExecutionHook[] { new CopilotWriteToolPolicyHook() }.Concat(configuredHooks).ToArray();
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
            _executionGate = new CopilotToolExecutionGate();
        }

        public async Task<CopilotToolExecutionOutcome> ExecuteAsync(
            CopilotToolInvocation invocation,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(invocation);
            ArgumentNullException.ThrowIfNull(invocation.Tool);
            ArgumentNullException.ThrowIfNull(invocation.AgentRequest);
            ArgumentNullException.ThrowIfNull(onEvent);

            var callId = string.IsNullOrWhiteSpace(invocation.CallId) ? Guid.NewGuid().ToString("N") : invocation.CallId.Trim();
            invocation = NormalizeInvocation(invocation, callId);
            var startedAt = _utcNow();
            var timeout = NormalizeTimeout(invocation.Tool.ExecutionTimeout);
            var stopwatch = Stopwatch.StartNew();
            var hookContext = new CopilotToolExecutionHookContext
            {
                Invocation = invocation,
                StartedAtUtc = startedAt,
                Timeout = timeout,
            };

            var decision = await RunBeforeHooksAsync(hookContext, cancellationToken);
            if (!decision.ShouldProceed)
            {
                var denied = CreateOutcome(
                    invocation,
                    CopilotToolExecutionState.Denied,
                    startedAt,
                    timeout,
                    stopwatch,
                    Failure(invocation.Tool.Name, $"{invocation.Tool.Name} execution was denied.", decision.Reason, CopilotToolFailureKind.Authorization));
                return await PublishOutcomeAsync(denied, onEvent);
            }

            IDisposable executionLease;
            var queueStopwatch = Stopwatch.StartNew();
            try
            {
                executionLease = await _executionGate.AcquireAsync(invocation.ConcurrencyMode, invocation.ConcurrencyKey, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                queueStopwatch.Stop();
                var cancelled = CreateOutcome(
                    invocation,
                    CopilotToolExecutionState.Cancelled,
                    startedAt,
                    timeout,
                    stopwatch,
                    Failure(invocation.Tool.Name, $"{invocation.Tool.Name} was cancelled while waiting to run.", "Tool execution was cancelled while queued.", CopilotToolFailureKind.Cancelled),
                    queueStopwatch.ElapsedMilliseconds);
                await PublishOutcomeAsync(cancelled, onEvent);
                throw;
            }

            queueStopwatch.Stop();
            var queueDurationMs = queueStopwatch.ElapsedMilliseconds;
            using (executionLease)
            {
                onEvent(CopilotAgentEvent.ToolStarted(CreateExecutionInfo(invocation, CopilotToolExecutionState.Running, startedAt, null, 0, timeout, queueDurationMs: queueDurationMs)));

                using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCancellation.CancelAfter(timeout);
                Task<CopilotToolResult>? executionTask = null;
                try
                {
                    executionTask = invocation.FrameworkApprovalGranted && invocation.Tool is ICopilotFrameworkApprovedTool approvedTool
                        ? approvedTool.ExecuteApprovedAsync(invocation.AgentRequest, invocation.ToolInput, linkedCancellation.Token)
                        : invocation.Tool.ExecuteAsync(invocation.AgentRequest, invocation.ToolInput, linkedCancellation.Token);
                    var result = await executionTask.WaitAsync(timeout, cancellationToken) ?? Failure(invocation.Tool.Name, $"{invocation.Tool.Name} returned no result.", "The tool returned a null result.", CopilotToolFailureKind.Internal);
                    var state = result.Approval != null
                        ? CopilotToolExecutionState.AwaitingApproval
                        : result.Success ? CopilotToolExecutionState.Completed : CopilotToolExecutionState.Failed;
                    return await PublishOutcomeAsync(CreateOutcome(invocation, state, startedAt, timeout, stopwatch, result, queueDurationMs), onEvent);
                }
                catch (TimeoutException)
                {
                    linkedCancellation.Cancel();
                    ObserveLateFault(executionTask);
                    var message = $"The tool exceeded its {FormatTimeout(timeout)} execution timeout.";
                    var outcome = CreateOutcome(
                        invocation,
                        CopilotToolExecutionState.TimedOut,
                        startedAt,
                        timeout,
                        stopwatch,
                        Failure(invocation.Tool.Name, $"{invocation.Tool.Name} timed out.", message, CopilotToolFailureKind.Transient),
                        queueDurationMs);
                    return await PublishOutcomeAsync(outcome, onEvent);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && linkedCancellation.IsCancellationRequested)
                {
                    var message = $"The tool exceeded its {FormatTimeout(timeout)} execution timeout.";
                    var outcome = CreateOutcome(
                        invocation,
                        CopilotToolExecutionState.TimedOut,
                        startedAt,
                        timeout,
                        stopwatch,
                        Failure(invocation.Tool.Name, $"{invocation.Tool.Name} timed out.", message, CopilotToolFailureKind.Transient),
                        queueDurationMs);
                    return await PublishOutcomeAsync(outcome, onEvent);
                }
                catch (OperationCanceledException)
                {
                    var outcome = CreateOutcome(
                        invocation,
                        CopilotToolExecutionState.Cancelled,
                        startedAt,
                        timeout,
                        stopwatch,
                        Failure(invocation.Tool.Name, $"{invocation.Tool.Name} was cancelled.", "Tool execution was cancelled.", CopilotToolFailureKind.Cancelled),
                        queueDurationMs);
                    await PublishOutcomeAsync(outcome, onEvent);
                    throw;
                }
                catch (Exception ex)
                {
                    var outcome = CreateOutcome(
                        invocation,
                        CopilotToolExecutionState.Failed,
                        startedAt,
                        timeout,
                        stopwatch,
                        Failure(invocation.Tool.Name, $"{invocation.Tool.Name} execution failed.", ex.Message, CopilotToolFailureClassifier.Classify(ex)),
                        queueDurationMs);
                    return await PublishOutcomeAsync(outcome, onEvent);
                }
            }
        }

        private async Task<CopilotToolExecutionHookDecision> RunBeforeHooksAsync(CopilotToolExecutionHookContext context, CancellationToken cancellationToken)
        {
            foreach (var hook in _hooks)
            {
                try
                {
                    var decision = await hook.BeforeExecuteAsync(context, cancellationToken) ?? CopilotToolExecutionHookDecision.Proceed;
                    if (!decision.ShouldProceed)
                        return decision;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return CopilotToolExecutionHookDecision.Deny($"A pre-execution hook failed: {ex.Message}");
                }
            }

            return CopilotToolExecutionHookDecision.Proceed;
        }

        private async Task<CopilotToolExecutionOutcome> PublishOutcomeAsync(CopilotToolExecutionOutcome outcome, Action<CopilotAgentEvent> onEvent)
        {
            CopilotToolExecutionAuditLogger.Record(outcome);
            foreach (var hook in _hooks)
            {
                try
                {
                    await hook.AfterExecuteAsync(outcome, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Log.Warn($"Copilot post-tool hook failed. Tool={outcome.Invocation.Tool.Name} CallId={outcome.Execution.CallId}", ex);
                }
            }

            onEvent(CopilotAgentEvent.FromToolResult(outcome.Result, outcome.Execution));
            return outcome;
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

            if (result.Approval != null)
                CopilotMcpConfirmationStore.Instance.LinkAgentCall(result.Approval.ActionId, invocation.CallId);

            return outcome;
        }

        private static CopilotToolInvocation NormalizeInvocation(CopilotToolInvocation invocation, string callId)
        {
            var toolInput = invocation.ToolInput ?? CopilotAgentToolInput.Empty;
            var toolCall = invocation.ToolCall ?? new CopilotToolCall();
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
                ToolInput = toolInput,
                ToolCall = toolCall,
                FrameworkApprovalGranted = invocation.FrameworkApprovalGranted,
                ApprovalActionId = invocation.ApprovalActionId?.Trim() ?? string.Empty,
                ConcurrencyMode = ResolveConcurrencyMode(invocation.Tool),
                ConcurrencyKey = ResolveConcurrencyKey(invocation.Tool, invocation.AgentRequest, toolInput),
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
            return new CopilotToolExecutionInfo
            {
                CallId = invocation.CallId,
                Round = invocation.Round,
                Attempt = invocation.Attempt,
                MaxAttempts = invocation.MaxAttempts,
                RuntimeName = invocation.RuntimeName,
                ToolName = invocation.Tool.Name,
                Access = invocation.Tool.Access,
                RiskLevel = invocation.Tool.RiskLevel,
                ApprovalMode = invocation.Tool.ApprovalMode,
                Idempotency = invocation.Tool.Idempotency,
                ConcurrencyMode = invocation.ConcurrencyMode,
                ConcurrencyKey = invocation.ConcurrencyKey,
                ApprovalActionId = !string.IsNullOrWhiteSpace(approvalActionId)
                    ? approvalActionId.Trim()
                    : invocation.ApprovalActionId?.Trim() ?? string.Empty,
                ArgumentSummary = CopilotToolExecutionAuditLogger.CreateArgumentSummary(invocation.ToolInput),
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
            return tool.Access == CopilotToolAccess.Write
                ? CopilotToolConcurrencyMode.Exclusive
                : tool.ConcurrencyMode;
        }

        internal static string ResolveConcurrencyKey(ICopilotTool tool, CopilotAgentRequest request, CopilotAgentToolInput toolInput)
        {
            var key = tool.GetConcurrencyKey(request, toolInput)?.Trim();
            key = string.IsNullOrWhiteSpace(key) ? $"tool:{tool.Name}" : key;
            var fingerprint = SHA256.HashData(Encoding.UTF8.GetBytes(key.ToUpperInvariant()));
            return $"resource:{Convert.ToHexString(fingerprint.AsSpan(0, 8)).ToLowerInvariant()}";
        }

        private static CopilotToolResult Failure(string toolName, string summary, string errorMessage, CopilotToolFailureKind failureKind)
        {
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = false,
                Summary = summary,
                ErrorMessage = errorMessage,
                FailureKind = failureKind,
            };
        }

        private static CopilotToolFailureKind NormalizeFailureKind(CopilotToolFailureKind failureKind)
        {
            return failureKind == CopilotToolFailureKind.None ? CopilotToolFailureKind.Unspecified : failureKind;
        }

        private static TimeSpan NormalizeTimeout(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
                return DefaultTimeout;
            return timeout > MaximumTimeout ? MaximumTimeout : timeout;
        }

        private static string FormatTimeout(TimeSpan timeout)
        {
            return timeout.TotalSeconds >= 1
                ? $"{timeout.TotalSeconds:0.#}-second"
                : $"{timeout.TotalMilliseconds:0}-millisecond";
        }

        private static void ObserveLateFault(Task? task)
        {
            if (task == null || task.IsCanceled || task.IsCompletedSuccessfully)
                return;

            if (task.IsFaulted)
            {
                _ = task.Exception;
                return;
            }

            _ = task.ContinueWith(
                static completedTask => _ = completedTask.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    internal static class CopilotToolRetryPolicy
    {
        public const int MaximumAttemptsPerCall = 2;

        public static bool IsRetryEligible(CopilotToolInvocation invocation, CopilotToolResult result, CopilotToolExecutionState state)
        {
            return invocation.Tool.Idempotency == CopilotToolIdempotency.Idempotent
                && invocation.Attempt < invocation.MaxAttempts
                && result.FailureKind == CopilotToolFailureKind.Transient
                && state is CopilotToolExecutionState.Failed or CopilotToolExecutionState.TimedOut;
        }
    }

    internal static class CopilotToolFailureClassifier
    {
        public static CopilotToolFailureKind Classify(Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);
            if (exception is HttpRequestException httpException)
                return ClassifyHttpStatus(httpException.StatusCode);

            return exception is TimeoutException or IOException or SocketException
                ? CopilotToolFailureKind.Transient
                : CopilotToolFailureKind.Internal;
        }

        private static CopilotToolFailureKind ClassifyHttpStatus(HttpStatusCode? statusCode)
        {
            if (!statusCode.HasValue
                || statusCode == HttpStatusCode.RequestTimeout
                || statusCode == HttpStatusCode.TooManyRequests
                || (int)statusCode.Value >= 500)
            {
                return CopilotToolFailureKind.Transient;
            }

            return statusCode switch
            {
                HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => CopilotToolFailureKind.Validation,
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => CopilotToolFailureKind.Authorization,
                HttpStatusCode.NotFound or HttpStatusCode.Gone => CopilotToolFailureKind.NotFound,
                HttpStatusCode.Conflict => CopilotToolFailureKind.Conflict,
                _ => CopilotToolFailureKind.Internal,
            };
        }
    }
}
