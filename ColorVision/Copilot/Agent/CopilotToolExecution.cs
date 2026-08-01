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

    public sealed class CopilotToolExecutor
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(CopilotToolExecutor));
        private static readonly TimeSpan DefaultHookPhaseTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MaximumHookPhaseTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan DefaultProgressInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MaximumProgressInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan MinimumStructuredProgressInterval = TimeSpan.FromMilliseconds(250);
        private static readonly ICopilotToolExecutionHook BuiltInWriteToolPolicyHook = new CopilotWriteToolPolicyHook();
        private const int MaxRecordedHookRuns = (CopilotToolExecutionHookRegistry.MaxRegistrations + 1) * 3;

        private readonly IReadOnlyList<ICopilotToolExecutionHook> _fixedHooks;
        private readonly CopilotToolExecutionHookRegistry? _hookRegistry;
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly CopilotToolExecutionGate _executionGate;
        private readonly TimeSpan _hookPhaseTimeout;
        private readonly TimeSpan _progressInterval;

        public CopilotToolExecutor(
            IEnumerable<ICopilotToolExecutionHook>? hooks = null,
            Func<DateTimeOffset>? utcNow = null,
            TimeSpan? hookPhaseTimeout = null)
            : this(
                hooks == null ? CopilotToolExecutionHookRegistry.Shared : null,
                hooks,
                utcNow,
                hookPhaseTimeout,
                DefaultProgressInterval)
        {
        }

        public CopilotToolExecutor(
            CopilotToolExecutionHookRegistry hookRegistry,
            Func<DateTimeOffset>? utcNow = null,
            TimeSpan? hookPhaseTimeout = null)
            : this(
                hookRegistry ?? throw new ArgumentNullException(nameof(hookRegistry)),
                hooks: null,
                utcNow,
                hookPhaseTimeout,
                DefaultProgressInterval)
        {
        }

        internal CopilotToolExecutor(
            IEnumerable<ICopilotToolExecutionHook>? hooks,
            Func<DateTimeOffset>? utcNow,
            TimeSpan? hookPhaseTimeout,
            TimeSpan progressInterval)
            : this(
                hooks == null ? CopilotToolExecutionHookRegistry.Shared : null,
                hooks,
                utcNow,
                hookPhaseTimeout,
                progressInterval)
        {
        }

        private CopilotToolExecutor(
            CopilotToolExecutionHookRegistry? hookRegistry,
            IEnumerable<ICopilotToolExecutionHook>? hooks,
            Func<DateTimeOffset>? utcNow,
            TimeSpan? hookPhaseTimeout,
            TimeSpan progressInterval)
        {
            _hookRegistry = hookRegistry;
            _fixedHooks = (hooks ?? Enumerable.Empty<ICopilotToolExecutionHook>())
                .Where(hook => hook != null)
                .ToArray();
            _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
            _executionGate = new CopilotToolExecutionGate();
            _hookPhaseTimeout = hookPhaseTimeout ?? DefaultHookPhaseTimeout;
            if (_hookPhaseTimeout <= TimeSpan.Zero || _hookPhaseTimeout > MaximumHookPhaseTimeout)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(hookPhaseTimeout),
                    $"Tool hook phase timeout must be greater than zero and no longer than {MaximumHookPhaseTimeout.TotalSeconds:0} seconds.");
            }
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(progressInterval, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(progressInterval, MaximumProgressInterval);
            _progressInterval = progressInterval;
        }

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
            var hooks = invocation.InitialHookBindings.Count > 0
                ? invocation.InitialHookBindings.ToArray()
                : ResolveInvocationHooks(invocation.Tool.Name);
            var hookRuns = new List<CopilotToolExecutionHookRun>(
                Math.Min(MaxRecordedHookRuns, invocation.InitialHookRuns.Count + hooks.Length * 2));
            hookRuns.AddRange(invocation.InitialHookRuns
                .Where(run => run?.IsStructurallyValid() == true)
                .Take(MaxRecordedHookRuns));
            var startedAt = _utcNow();
            var timeout = invocation.Tool.Capability.EffectiveExecutionTimeout;
            var stopwatch = Stopwatch.StartNew();
            if (invocation.Tool.Capability.RequiresNativeApproval)
            {
                var approvalError = invocation.Tool is not ICopilotFrameworkApprovedTool
                    ? $"{invocation.Tool.Name} requires native approval but has no approved execution path."
                    : !invocation.FrameworkApprovalGranted
                        ? $"{invocation.Tool.Name} requires approval for this exact call before it can execute."
                        : string.Empty;
                if (!string.IsNullOrEmpty(approvalError))
                {
                    var denied = CreateOutcome(
                        invocation,
                        CopilotToolExecutionState.Denied,
                        startedAt,
                        timeout,
                        stopwatch,
                        Failure(invocation.Tool.Name, $"{invocation.Tool.Name} execution was denied.", approvalError, CopilotToolFailureKind.Authorization));
                    return await PublishOutcomeAsync(denied, hooks, hookRuns, onEvent);
                }
            }

            var hookContext = new CopilotToolExecutionHookContext
            {
                Invocation = invocation,
                StartedAtUtc = startedAt,
                Timeout = timeout,
            };

            CopilotToolExecutionHookDecision decision;
            try
            {
                decision = await RunBeforeHooksAsync(hookContext, hooks, hookRuns, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                var cancelled = CreateOutcome(
                    invocation,
                    CopilotToolExecutionState.Cancelled,
                    startedAt,
                    timeout,
                    stopwatch,
                    Failure(
                        invocation.Tool.Name,
                        $"{invocation.Tool.Name} was cancelled during pre-execution checks.",
                        "Tool execution was cancelled before its pre-execution hooks completed.",
                        CopilotToolFailureKind.Cancelled,
                        "tool_execution_cancelled"));
                await PublishOutcomeAsync(cancelled, hooks, hookRuns, onEvent);
                throw;
            }
            if (!decision.ShouldProceed)
            {
                var failureCode = CopilotToolFailureCode.Normalize(decision.FailureCode);
                var failureKind = decision.FailureKind != CopilotToolFailureKind.None
                    && Enum.IsDefined(decision.FailureKind)
                        ? decision.FailureKind
                        : CopilotToolFailureKind.Authorization;
                var reason = string.IsNullOrWhiteSpace(decision.Reason)
                    ? "A pre-execution hook denied the tool call."
                    : CopilotUserFacingErrorFormatter.Sanitize(decision.Reason);
                var denied = CreateOutcome(
                    invocation,
                    CopilotToolExecutionState.Denied,
                    startedAt,
                    timeout,
                    stopwatch,
                    Failure(
                        invocation.Tool.Name,
                        $"{invocation.Tool.Name} execution was denied.",
                        reason,
                        failureKind,
                        string.IsNullOrWhiteSpace(failureCode) ? "tool_hook_denied" : failureCode));
                return await PublishOutcomeAsync(denied, hooks, hookRuns, onEvent);
            }

            IDisposable executionLease;
            var queueStopwatch = Stopwatch.StartNew();
            using var queueProgressCancellation = new CancellationTokenSource();
            var queueProgressTask = PublishToolQueueProgressAsync(
                invocation,
                startedAt,
                timeout,
                queueStopwatch,
                stopwatch,
                onEvent,
                queueProgressCancellation.Token);
            async Task StopQueueProgressAsync()
            {
                await queueProgressCancellation.CancelAsync();
                await queueProgressTask;
            }
            try
            {
                executionLease = await _executionGate.AcquireAsync(invocation.ConcurrencyMode, invocation.ConcurrencyKey, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                queueStopwatch.Stop();
                await StopQueueProgressAsync();
                var cancelled = CreateOutcome(
                    invocation,
                    CopilotToolExecutionState.Cancelled,
                    startedAt,
                    timeout,
                    stopwatch,
                    Failure(invocation.Tool.Name, $"{invocation.Tool.Name} was cancelled while waiting to run.", "Tool execution was cancelled while queued.", CopilotToolFailureKind.Cancelled),
                    queueStopwatch.ElapsedMilliseconds);
                await PublishOutcomeAsync(cancelled, hooks, hookRuns, onEvent);
                throw;
            }
            catch
            {
                queueStopwatch.Stop();
                await StopQueueProgressAsync();
                throw;
            }

            queueStopwatch.Stop();
            await StopQueueProgressAsync();
            var queueDurationMs = queueStopwatch.ElapsedMilliseconds;
            using (var executionLeaseGuard = new DeferredExecutionLease(executionLease))
            {
                onEvent(CopilotAgentEvent.ToolStarted(CreateExecutionInfo(invocation, CopilotToolExecutionState.Running, startedAt, null, 0, timeout, queueDurationMs: queueDurationMs)));

                using var executionCancellation = new CopilotNonBlockingCancellationSource();
                Task<CopilotToolResult>? executionTask = null;
                var executionProgress = new CopilotToolProgressContext();
                using var progressCancellation = new CancellationTokenSource();
                var progressTask = PublishToolProgressAsync(
                    invocation,
                    startedAt,
                    timeout,
                    queueDurationMs,
                    stopwatch,
                    executionProgress,
                    onEvent,
                    progressCancellation.Token);
                var progressStopped = 0;

                async Task StopProgressAsync()
                {
                    if (Interlocked.Exchange(ref progressStopped, 1) != 0)
                        return;

                    await progressCancellation.CancelAsync();
                    await progressTask;
                }

                async Task<CopilotToolExecutionOutcome> PublishExecutionOutcomeAsync(CopilotToolExecutionOutcome outcome)
                {
                    executionProgress.Complete();
                    await StopProgressAsync();
                    return await PublishOutcomeAsync(outcome, hooks, hookRuns, onEvent);
                }

                try
                {
                    // Keep third-party synchronous prefixes and cancellation callbacks outside
                    // the runtime loop. The independent source is cancelled only after the
                    // caller/timeout boundary has already released this invocation.
                    executionTask = Task.Run(
                        () => ExecuteToolAsync(invocation, executionProgress, executionCancellation.Token),
                        executionCancellation.Token);
                    var result = await executionTask.WaitAsync(timeout, cancellationToken) ?? Failure(invocation.Tool.Name, $"{invocation.Tool.Name} returned no result.", "The tool returned a null result.", CopilotToolFailureKind.Internal);
                    var state = result.Approval != null
                        ? CopilotToolExecutionState.AwaitingApproval
                        : result.Success ? CopilotToolExecutionState.Completed : CopilotToolExecutionState.Failed;
                    return await PublishExecutionOutcomeAsync(CreateOutcome(invocation, state, startedAt, timeout, stopwatch, result, queueDurationMs));
                }
                catch (TimeoutException)
                {
                    executionCancellation.RequestCancellation();
                    executionLeaseGuard.HoldUntilCompleted(executionTask);
                    CopilotCancellationBoundary.ObserveLateFault(executionTask);
                    var message = $"The tool exceeded its {FormatTimeout(timeout)} execution timeout.";
                    var outcome = CreateOutcome(
                        invocation,
                        CopilotToolExecutionState.TimedOut,
                        startedAt,
                        timeout,
                        stopwatch,
                        Failure(invocation.Tool.Name, $"{invocation.Tool.Name} timed out.", message, CopilotToolFailureKind.Transient),
                        queueDurationMs);
                    return await PublishExecutionOutcomeAsync(outcome);
                }
                catch (OperationCanceledException)
                {
                    executionCancellation.RequestCancellation();
                    executionLeaseGuard.HoldUntilCompleted(executionTask);
                    CopilotCancellationBoundary.ObserveLateFault(executionTask);
                    var outcome = CreateOutcome(
                        invocation,
                        CopilotToolExecutionState.Cancelled,
                        startedAt,
                        timeout,
                        stopwatch,
                        Failure(invocation.Tool.Name, $"{invocation.Tool.Name} was cancelled.", "Tool execution was cancelled.", CopilotToolFailureKind.Cancelled),
                        queueDurationMs);
                    await PublishExecutionOutcomeAsync(outcome);
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
                    return await PublishExecutionOutcomeAsync(outcome);
                }
                finally
                {
                    executionProgress.Complete();
                    await StopProgressAsync();
                }
            }
        }

        private async Task PublishToolQueueProgressAsync(
            CopilotToolInvocation invocation,
            DateTimeOffset startedAt,
            TimeSpan timeout,
            Stopwatch queueStopwatch,
            Stopwatch totalStopwatch,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken)
        {
            try
            {
                using var timer = new PeriodicTimer(_progressInterval);
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    var queueDurationMs = Math.Max(0, queueStopwatch.ElapsedMilliseconds);
                    var execution = CreateExecutionInfo(
                        invocation,
                        CopilotToolExecutionState.Pending,
                        startedAt,
                        completedAt: null,
                        Math.Max(0, totalStopwatch.ElapsedMilliseconds),
                        timeout,
                        queueDurationMs: queueDurationMs);
                    onEvent(CopilotAgentEvent.ToolProgress(
                        execution,
                        $"{invocation.Tool.Name} is waiting for an execution slot · {FormatElapsed(queueDurationMs)} queued."));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Log.Warn($"Copilot tool queue progress reporting stopped unexpectedly. Tool={invocation.Tool.Name} CallId={invocation.CallId}", ex);
            }
        }

        private async Task PublishToolProgressAsync(
            CopilotToolInvocation invocation,
            DateTimeOffset startedAt,
            TimeSpan timeout,
            long queueDurationMs,
            Stopwatch stopwatch,
            CopilotToolProgressContext progressContext,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken)
        {
            try
            {
                var hasPublishedStructuredProgress = false;
                var lastStructuredProgressAt = TimeSpan.Zero;
                var lastPublishedProgressVersion = 0L;
                while (true)
                {
                    var waitResult = await progressContext.WaitForUpdateAsync(
                        _progressInterval,
                        cancellationToken);
                    if (waitResult == CopilotToolProgressWaitResult.Completed)
                        return;

                    CopilotToolProgressUpdate? reportedProgress;
                    if (waitResult == CopilotToolProgressWaitResult.Updated)
                    {
                        if (hasPublishedStructuredProgress)
                        {
                            var remainingDelay = MinimumStructuredProgressInterval
                                - (stopwatch.Elapsed - lastStructuredProgressAt);
                            if (remainingDelay > TimeSpan.Zero)
                                await Task.Delay(remainingDelay, cancellationToken);
                        }

                        progressContext.DrainUpdateNotifications();
                        var progressSnapshot = progressContext.GetLatestSnapshot();
                        reportedProgress = progressSnapshot.Update;
                        if (reportedProgress == null
                            || progressSnapshot.Version <= lastPublishedProgressVersion)
                            continue;
                        lastStructuredProgressAt = stopwatch.Elapsed;
                        hasPublishedStructuredProgress = true;
                        lastPublishedProgressVersion = progressSnapshot.Version;
                    }
                    else
                    {
                        var progressSnapshot = progressContext.GetLatestSnapshot();
                        reportedProgress = progressSnapshot.Update;
                        lastPublishedProgressVersion = Math.Max(
                            lastPublishedProgressVersion,
                            progressSnapshot.Version);
                    }

                    if (!stopwatch.IsRunning)
                        return;

                    var elapsedMs = Math.Max(0, stopwatch.ElapsedMilliseconds);
                    var execution = CreateExecutionInfo(
                        invocation,
                        CopilotToolExecutionState.Running,
                        startedAt,
                        completedAt: null,
                        elapsedMs,
                        timeout,
                        queueDurationMs: queueDurationMs);
                    var progressText = FormatReportedProgress(reportedProgress);
                    onEvent(CopilotAgentEvent.ToolProgress(
                        execution,
                        string.IsNullOrWhiteSpace(progressText)
                            ? $"{invocation.Tool.Name} is still running · {FormatElapsed(elapsedMs)} elapsed."
                            : $"{invocation.Tool.Name} · {progressText} · {FormatElapsed(elapsedMs)} elapsed.",
                        reportedProgress));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Log.Warn($"Copilot tool progress reporting stopped unexpectedly. Tool={invocation.Tool.Name} CallId={invocation.CallId}", ex);
            }
        }

        private static async Task<CopilotToolResult> ExecuteToolAsync(
            CopilotToolInvocation invocation,
            CopilotToolProgressContext progress,
            CancellationToken cancellationToken)
        {
            using var invocationContext = CopilotToolInvocationContext.Enter(invocation);
            if (invocation.FrameworkApprovalGranted
                && invocation.Tool is ICopilotFrameworkApprovedProgressReportingTool approvedProgressTool)
            {
                return await approvedProgressTool.ExecuteApprovedWithProgressAsync(
                    invocation.AgentRequest,
                    invocation.ToolInput,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            if (invocation.FrameworkApprovalGranted
                && invocation.Tool is ICopilotFrameworkApprovedTool approvedTool)
            {
                return await approvedTool.ExecuteApprovedAsync(
                    invocation.AgentRequest,
                    invocation.ToolInput,
                    cancellationToken).ConfigureAwait(false);
            }

            if (invocation.Tool is ICopilotProgressReportingTool progressTool)
            {
                return await progressTool.ExecuteWithProgressAsync(
                    invocation.AgentRequest,
                    invocation.ToolInput,
                    progress,
                    cancellationToken).ConfigureAwait(false);
            }

            return await invocation.Tool.ExecuteAsync(
                invocation.AgentRequest,
                invocation.ToolInput,
                cancellationToken).ConfigureAwait(false);
        }

        private static string FormatReportedProgress(CopilotToolProgressUpdate? progress)
        {
            if (progress == null)
                return string.Empty;

            var count = progress.Completed.HasValue && progress.Total.HasValue
                ? $"{progress.Completed.Value}/{progress.Total.Value}"
                : progress.Completed?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(count) && !string.IsNullOrWhiteSpace(progress.Unit))
                count += " " + progress.Unit;
            if (string.IsNullOrWhiteSpace(progress.Message))
                return count;
            return string.IsNullOrWhiteSpace(count)
                ? progress.Message
                : $"{count} · {progress.Message}";
        }

        private CopilotToolExecutionHookBinding[] ResolveInvocationHooks(string toolName)
        {
            var configuredHooks = _hookRegistry?.Resolve(toolName)
                ?? _fixedHooks.Select((hook, index) =>
                    new CopilotToolExecutionHookBinding($"fixed:{index}", hook)).ToArray();
            var hooks = new CopilotToolExecutionHookBinding[configuredHooks.Count + 1];
            hooks[0] = new CopilotToolExecutionHookBinding(
                "builtin:write-tool-policy",
                BuiltInWriteToolPolicyHook);
            for (var i = 0; i < configuredHooks.Count; i++)
                hooks[i + 1] = configuredHooks[i];
            return hooks;
        }

        internal CopilotToolExecutionHookRegistrySnapshot GetHookSurfaceSnapshot()
        {
            var configuredSnapshot = _hookRegistry?.GetSnapshot()
                ?? CopilotToolExecutionHookRegistry.CreateSnapshot(
                    revision: 0,
                    _fixedHooks.Select((hook, index) =>
                        CopilotToolExecutionHookRegistry.CreateSnapshotEntry(
                            $"fixed:{index}",
                            "*",
                            index,
                            hook)));
            return CreateHookSurfaceSnapshot(configuredSnapshot);
        }

        internal static CopilotToolExecutionHookRegistrySnapshot GetSharedHookSurfaceSnapshot()
        {
            return CreateHookSurfaceSnapshot(CopilotToolExecutionHookRegistry.Shared.GetSnapshot());
        }

        private static CopilotToolExecutionHookRegistrySnapshot CreateHookSurfaceSnapshot(
            CopilotToolExecutionHookRegistrySnapshot configuredSnapshot)
        {
            return CopilotToolExecutionHookRegistry.CreateSnapshot(
                configuredSnapshot.Revision,
                new[]
                {
                    CopilotToolExecutionHookRegistry.CreateSnapshotEntry(
                        "builtin:write-tool-policy",
                        "*",
                        int.MinValue,
                        BuiltInWriteToolPolicyHook),
                }.Concat(configuredSnapshot.Entries));
        }

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

    internal static class CopilotToolRetryPolicy
    {
        public const int MaximumAttemptsPerCall = 2;
        public const int MaximumRepeatableObservationAttempts = 8;

        public static bool IsRetryEligible(CopilotToolInvocation invocation, CopilotToolResult result, CopilotToolExecutionState state)
        {
            return IsRepeatableObservationEligible(invocation, result, state)
                || invocation.Tool.Capability.Idempotency == CopilotToolIdempotency.Idempotent
                && invocation.Attempt < invocation.MaxAttempts
                && result.FailureKind == CopilotToolFailureKind.Transient
                && state is CopilotToolExecutionState.Failed or CopilotToolExecutionState.TimedOut;
        }

        private static bool IsRepeatableObservationEligible(
            CopilotToolInvocation invocation,
            CopilotToolResult result,
            CopilotToolExecutionState state)
        {
            if (invocation.Tool is not ICopilotRepeatableObservationTool
                || invocation.Tool.Capability.Access != CopilotToolAccess.ReadOnly
                || invocation.Attempt >= invocation.MaxAttempts
                || state != CopilotToolExecutionState.Completed
                || !result.Success
                || !result.ObservationCanRepeat)
            {
                return false;
            }

            var currentSignature = NormalizeObservationProgressSignature(
                result.ObservationProgressSignature);
            if (currentSignature.Length == 0)
                return false;

            var previousSignature = NormalizeObservationProgressSignature(
                invocation.PreviousObservationProgressSignature);
            return previousSignature.Length == 0
                || !string.Equals(
                    previousSignature,
                    currentSignature,
                    StringComparison.OrdinalIgnoreCase);
        }

        internal static string NormalizeObservationProgressSignature(
            string? signature)
        {
            var normalized = (signature ?? string.Empty).Trim();
            return normalized.Length == 64
                && normalized.All(Uri.IsHexDigit)
                    ? normalized.ToLowerInvariant()
                    : string.Empty;
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
