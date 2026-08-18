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
        private static readonly ILog Log = LogManager.GetLogger(typeof(CopilotToolExecutor));
        private static readonly TimeSpan DefaultHookPhaseTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MaximumHookPhaseTimeout = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan DefaultProgressInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan MaximumProgressInterval = TimeSpan.FromMinutes(1);
        private static readonly TimeSpan MinimumStructuredProgressInterval = TimeSpan.FromMilliseconds(250);
        private static readonly ICopilotToolExecutionHook BuiltInWriteToolPolicyHook = new CopilotWriteToolPolicyHook();
        private const string BuiltInWriteToolPolicySourceId = "builtin:write-tool-policy";
        private const string ExtensionHookSourcePrefix = "extension:";
        private const int MaxInvocationHookBindings =
            CopilotToolExecutionHookRegistry.MaxRegistrations
            + CopilotProjectInstructionDiscoveryConfig.MaximumConfiguredHookHandlers
            + 1;
        private const int MaxRecordedHookRuns = MaxInvocationHookBindings * 3;

        private readonly IReadOnlyList<ICopilotToolExecutionHook> _fixedHooks;
        private readonly CopilotToolExecutionHookRegistry? _hookRegistry;
        private readonly Func<DateTimeOffset> _utcNow;
        private readonly CopilotToolExecutionGate _executionGate;
        private readonly TimeSpan _hookPhaseTimeout;
        private readonly TimeSpan _progressInterval;
        private readonly ICopilotCodexCommandHookRunner? _codexCommandHookRunner;

        public CopilotToolExecutor(
            IEnumerable<ICopilotToolExecutionHook>? hooks = null,
            Func<DateTimeOffset>? utcNow = null,
            TimeSpan? hookPhaseTimeout = null)
            : this(
                hooks == null ? CopilotToolExecutionHookRegistry.Shared : null,
                hooks,
                utcNow,
                hookPhaseTimeout,
                DefaultProgressInterval,
                codexCommandHookRunner: null)
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
                DefaultProgressInterval,
                codexCommandHookRunner: null)
        {
        }

        internal CopilotToolExecutor(
            IEnumerable<ICopilotToolExecutionHook>? hooks,
            Func<DateTimeOffset>? utcNow,
            TimeSpan? hookPhaseTimeout,
            TimeSpan progressInterval,
            ICopilotCodexCommandHookRunner? codexCommandHookRunner = null)
            : this(
                hooks == null ? CopilotToolExecutionHookRegistry.Shared : null,
                hooks,
                utcNow,
                hookPhaseTimeout,
                progressInterval,
                codexCommandHookRunner)
        {
        }

        private CopilotToolExecutor(
            CopilotToolExecutionHookRegistry? hookRegistry,
            IEnumerable<ICopilotToolExecutionHook>? hooks,
            Func<DateTimeOffset>? utcNow,
            TimeSpan? hookPhaseTimeout,
            TimeSpan progressInterval,
            ICopilotCodexCommandHookRunner? codexCommandHookRunner)
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
            _codexCommandHookRunner = codexCommandHookRunner;
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
            var hooks = BindMonotonicExecutionGuards(
                invocation.InitialHookBindings.Count > 0
                    ? invocation.InitialHookBindings
                    : ResolveInvocationHooks(invocation.Tool.Name, invocation.AgentRequest));
            var hookRuns = new List<CopilotToolExecutionHookRun>(
                Math.Min(MaxRecordedHookRuns, invocation.InitialHookRuns.Count + hooks.Length * 2));
            hookRuns.AddRange(invocation.InitialHookRuns
                .Where(run => run?.IsStructurallyValid() == true)
                .Take(MaxRecordedHookRuns));
            var startedAt = _utcNow();
            var timeout = invocation.Tool.Capability.EffectiveExecutionTimeout;
            var stopwatch = Stopwatch.StartNew();
            if (!CopilotToolRegistry.IsAllowedForCodexAgentPolicy(
                invocation.Tool,
                invocation.AgentRequest))
            {
                var denied = CreateOutcome(
                    invocation,
                    CopilotToolExecutionState.Denied,
                    startedAt,
                    timeout,
                    stopwatch,
                    Failure(
                        invocation.Tool.Name,
                        $"{invocation.Tool.Name} execution was denied.",
                        "Codex agents.enabled=false disables subagent tools for this submitted turn.",
                        CopilotToolFailureKind.Authorization,
                        "codex_agents_disabled"));
                return await PublishOutcomeAsync(denied, hooks, hookRuns, onEvent);
            }
            if (!CopilotToolRegistry.IsAllowedForCodexShellToolPolicy(
                invocation.Tool,
                invocation.AgentRequest))
            {
                var denied = CreateOutcome(
                    invocation,
                    CopilotToolExecutionState.Denied,
                    startedAt,
                    timeout,
                    stopwatch,
                    Failure(
                        invocation.Tool.Name,
                        $"{invocation.Tool.Name} execution was denied.",
                        "Codex features.shell_tool=false disables shell command starts for this submitted turn.",
                        CopilotToolFailureKind.Authorization,
                        "codex_shell_tool_disabled"));
                return await PublishOutcomeAsync(denied, hooks, hookRuns, onEvent);
            }
            if (!CopilotToolRegistry.IsAllowedForCodexSandboxPolicy(
                invocation.Tool,
                invocation.AgentRequest))
            {
                var denied = CreateOutcome(
                    invocation,
                    CopilotToolExecutionState.Denied,
                    startedAt,
                    timeout,
                    stopwatch,
                    Failure(
                        invocation.Tool.Name,
                        $"{invocation.Tool.Name} execution was denied.",
                        "Codex sandbox_mode=read-only disables write-capable tools for this submitted turn.",
                        CopilotToolFailureKind.Authorization,
                        "codex_read_only_sandbox"));
                return await PublishOutcomeAsync(denied, hooks, hookRuns, onEvent);
            }
            var requiresCodexApproval = CopilotCodexApprovalPolicySelection.RequiresNativeApproval(
                invocation.AgentRequest.CodexApprovalPolicy,
                invocation.Tool);
            if (requiresCodexApproval)
            {
                var approvalPromptDisabled = !invocation.FrameworkApprovalGranted
                    && !CopilotCodexApprovalPolicySelection.AllowsApprovalPrompt(
                        invocation.AgentRequest.CodexApprovalPolicy,
                        invocation.EffectiveApprovalPromptCategory);
                string approvalError;
                if (approvalPromptDisabled)
                {
                    approvalError = CopilotCodexApprovalPolicySelection.GetApprovalDenialReason(
                        invocation.AgentRequest.CodexApprovalPolicy,
                        invocation.EffectiveApprovalPromptCategory);
                }
                else if (invocation.Tool.Capability.RequiresNativeApproval
                    && invocation.Tool is not ICopilotFrameworkApprovedTool)
                {
                    approvalError = $"{invocation.Tool.Name} requires native approval but has no approved execution path.";
                }
                else
                {
                    approvalError = invocation.FrameworkApprovalGranted
                        ? string.Empty
                        : $"{invocation.Tool.Name} requires approval for this exact call before it can execute.";
                }
                if (!string.IsNullOrEmpty(approvalError))
                {
                    var denied = CreateOutcome(
                        invocation,
                        CopilotToolExecutionState.Denied,
                        startedAt,
                        timeout,
                        stopwatch,
                        Failure(
                            invocation.Tool.Name,
                            $"{invocation.Tool.Name} execution was denied.",
                            approvalError,
                            CopilotToolFailureKind.Authorization,
                            approvalPromptDisabled
                                ? "codex_approval_prompt_disabled"
                                : string.Empty));
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
                var beforeHookEvents = new CopilotToolExecutionHookEventPublisher(
                    onEvent,
                    () => CreateExecutionInfo(
                        invocation,
                        CopilotToolExecutionState.Pending,
                        startedAt,
                        completedAt: null,
                        stopwatch.ElapsedMilliseconds,
                        timeout));
                decision = await RunBeforeHooksAsync(
                    hookContext,
                    hooks,
                    hookRuns,
                    beforeHookEvents,
                    cancellationToken);
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
                    return await PublishOutcomeAsync(
                        outcome,
                        hooks,
                        hookRuns,
                        onEvent,
                        toolWasExecuted: true);
                }

                try
                {
                    // Keep third-party synchronous prefixes and cancellation callbacks outside
                    // the runtime loop. The independent source is cancelled only after the
                    // caller/timeout boundary has already released this invocation.
                    executionTask = Task.Run(
                        () => ExecuteToolAsync(invocation, executionProgress, executionCancellation.Token),
                        executionCancellation.Token);
                    var result = CopilotToolResultContract.Capture(
                        invocation.Tool.Name,
                        await executionTask.WaitAsync(timeout, cancellationToken));
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
                    var result = CreateExecutionBoundaryFailure(
                        invocation,
                        timeout,
                        wasCancelled: false,
                        outcomeUnknown: HasUnknownOutcomeAfterExecutionBoundary(invocation));
                    var outcome = CreateOutcome(
                        invocation,
                        CopilotToolExecutionState.TimedOut,
                        startedAt,
                        timeout,
                        stopwatch,
                        result,
                        queueDurationMs);
                    return await PublishExecutionOutcomeAsync(outcome);
                }
                catch (OperationCanceledException)
                {
                    executionCancellation.RequestCancellation();
                    executionLeaseGuard.HoldUntilCompleted(executionTask);
                    CopilotCancellationBoundary.ObserveLateFault(executionTask);
                    var outcomeUnknown = executionTask is { IsCompleted: false }
                        && HasUnknownOutcomeAfterExecutionBoundary(invocation);
                    var outcome = CreateOutcome(
                        invocation,
                        outcomeUnknown
                            ? CopilotToolExecutionState.Interrupted
                            : CopilotToolExecutionState.Cancelled,
                        startedAt,
                        timeout,
                        stopwatch,
                        CreateExecutionBoundaryFailure(
                            invocation,
                            timeout,
                            wasCancelled: true,
                            outcomeUnknown),
                        queueDurationMs);
                    await PublishExecutionOutcomeAsync(outcome);
                    throw new CopilotToolExecutionCancellationException(
                        outcome,
                        cancellationToken);
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

        private CopilotToolExecutionHookBinding[] ResolveInvocationHooks(
            string toolName,
            CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            var configuredHooks = _hookRegistry?.Resolve(toolName)
                ?? _fixedHooks.Select((hook, index) =>
                    new CopilotToolExecutionHookBinding($"fixed:{index}", hook)).ToArray();
            var effectiveHooks = request.CodexExtensionHooksEnabled
                ? configuredHooks
                : configuredHooks.Where(binding => !IsExtensionHookSource(binding.SourceId)).ToArray();
            var commandHooks = request.CodexHooksEnabled
                ? CopilotCodexCommandHookFactory.Resolve(
                    request.CodexCommandHooks,
                    toolName,
                    _codexCommandHookRunner)
                : Array.Empty<CopilotToolExecutionHookBinding>();
            var hooks = new CopilotToolExecutionHookBinding[
                effectiveHooks.Count + commandHooks.Count + 1];
            hooks[0] = new CopilotToolExecutionHookBinding(
                BuiltInWriteToolPolicySourceId,
                BuiltInWriteToolPolicyHook);
            for (var i = 0; i < effectiveHooks.Count; i++)
                hooks[i + 1] = effectiveHooks[i];
            for (var i = 0; i < commandHooks.Count; i++)
                hooks[effectiveHooks.Count + i + 1] = commandHooks[i];
            return hooks;
        }

        private static CopilotToolExecutionHookBinding[] BindMonotonicExecutionGuards(
            IReadOnlyList<CopilotToolExecutionHookBinding> capturedHooks)
        {
            var extensibleHooks = (capturedHooks ?? Array.Empty<CopilotToolExecutionHookBinding>())
                .Where(binding => binding?.Hook != null)
                .Where(binding => !string.Equals(
                    binding.SourceId,
                    BuiltInWriteToolPolicySourceId,
                    StringComparison.OrdinalIgnoreCase))
                .Take(MaxInvocationHookBindings - 1)
                .ToArray();
            var hooks = new CopilotToolExecutionHookBinding[extensibleHooks.Length + 1];
            hooks[0] = new CopilotToolExecutionHookBinding(
                BuiltInWriteToolPolicySourceId,
                BuiltInWriteToolPolicyHook);
            Array.Copy(extensibleHooks, 0, hooks, 1, extensibleHooks.Length);
            return hooks;
        }

        internal CopilotToolExecutionHookRegistrySnapshot GetHookSurfaceSnapshot(
            bool codexHooksEnabled = true,
            bool codexPluginsEnabled = true,
            IReadOnlyList<CopilotCodexCommandHookDefinition>? configuredCommandHooks = null)
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
            return CreateHookSurfaceSnapshot(
                configuredSnapshot,
                codexHooksEnabled,
                codexPluginsEnabled,
                configuredCommandHooks);
        }

        internal static CopilotToolExecutionHookRegistrySnapshot GetSharedHookSurfaceSnapshot(
            bool codexHooksEnabled = true,
            bool codexPluginsEnabled = true,
            IReadOnlyList<CopilotCodexCommandHookDefinition>? configuredCommandHooks = null)
        {
            return CreateHookSurfaceSnapshot(
                CopilotToolExecutionHookRegistry.Shared.GetSnapshot(),
                codexHooksEnabled,
                codexPluginsEnabled,
                configuredCommandHooks);
        }

        private static CopilotToolExecutionHookRegistrySnapshot CreateHookSurfaceSnapshot(
            CopilotToolExecutionHookRegistrySnapshot configuredSnapshot,
            bool codexHooksEnabled,
            bool codexPluginsEnabled,
            IReadOnlyList<CopilotCodexCommandHookDefinition>? configuredCommandHooks)
        {
            var configuredEntries = codexHooksEnabled && codexPluginsEnabled
                ? configuredSnapshot.Entries
                : configuredSnapshot.Entries
                    .Where(entry => !IsExtensionHookSource(entry.SourceId))
                    .ToArray();
            var commandEntries = codexHooksEnabled
                ? CopilotCodexCommandHookFactory.CreateSnapshotEntries(configuredCommandHooks)
                : Array.Empty<CopilotToolExecutionHookRegistryEntry>();
            return CopilotToolExecutionHookRegistry.CreateSnapshot(
                configuredSnapshot.Revision,
                new[]
                {
                    CopilotToolExecutionHookRegistry.CreateSnapshotEntry(
                        BuiltInWriteToolPolicySourceId,
                        "*",
                        int.MinValue,
                        BuiltInWriteToolPolicyHook),
                }.Concat(configuredEntries).Concat(commandEntries));
        }

        private static bool IsExtensionHookSource(string? sourceId) =>
            sourceId?.StartsWith(ExtensionHookSourcePrefix, StringComparison.OrdinalIgnoreCase) == true;

    }

}
