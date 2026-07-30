using ColorVision.Copilot;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotToolExecutionHookIntegrityTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ExplicitHookDenialPublishesStableTerminalOutcome()
    {
        var hook = new RecordingHook((_, _) => Task.FromResult(
            CopilotToolExecutionHookDecision.Deny(
                "Blocked by the repository policy.",
                "Repository Policy Denied")));
        var tool = new RecordingTool();
        var events = new List<CopilotAgentEvent>();

        var outcome = await new CopilotToolExecutor([hook]).ExecuteAsync(
            CreateInvocation(tool, "explicit-denial"),
            events.Add,
            CancellationToken.None);

        AssertDeniedOutcome(
            outcome,
            "repository_policy_denied",
            CopilotToolFailureKind.Authorization);
        Assert.Equal(0, tool.ExecutionCount);
        Assert.Equal(1, hook.AfterCount);
        Assert.Same(outcome, hook.LastOutcome);
        AssertHookRun(
            outcome.HookRuns,
            "fixed:0",
            CopilotToolExecutionHookPhase.BeforeExecute,
            CopilotToolExecutionHookState.Denied,
            "repository_policy_denied");
        AssertTerminalEvent(events, CopilotToolExecutionState.Denied, "repository_policy_denied");
    }

    [Fact]
    public async Task SelfCancelledHookIsDeniedWithoutMasqueradingAsCallerCancellation()
    {
        var hook = new RecordingHook((_, _) => Task.FromCanceled<CopilotToolExecutionHookDecision>(
            new CancellationToken(canceled: true)));
        var tool = new RecordingTool();
        var events = new List<CopilotAgentEvent>();

        var outcome = await new CopilotToolExecutor([hook]).ExecuteAsync(
            CreateInvocation(tool, "self-cancelled-hook"),
            events.Add,
            CancellationToken.None);

        AssertDeniedOutcome(
            outcome,
            "tool_hook_cancelled",
            CopilotToolFailureKind.Internal);
        Assert.Equal(0, tool.ExecutionCount);
        Assert.Equal(1, hook.AfterCount);
        AssertHookRun(
            outcome.HookRuns,
            "fixed:0",
            CopilotToolExecutionHookPhase.BeforeExecute,
            CopilotToolExecutionHookState.Cancelled,
            "tool_hook_cancelled");
        AssertTerminalEvent(events, CopilotToolExecutionState.Denied, "tool_hook_cancelled");
    }

    [Fact]
    public async Task FailedHookIsDeniedWithoutExposingItsExceptionMessage()
    {
        const string sensitiveMessage = "api_key=hook-secret-value";
        var hook = new RecordingHook((_, _) =>
            Task.FromException<CopilotToolExecutionHookDecision>(
                new InvalidOperationException(sensitiveMessage)));
        var tool = new RecordingTool();
        var events = new List<CopilotAgentEvent>();

        var outcome = await new CopilotToolExecutor([hook]).ExecuteAsync(
            CreateInvocation(tool, "failed-hook"),
            events.Add,
            CancellationToken.None);

        AssertDeniedOutcome(
            outcome,
            "tool_hook_failed",
            CopilotToolFailureKind.Internal);
        Assert.DoesNotContain(sensitiveMessage, outcome.Result.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(0, tool.ExecutionCount);
        Assert.Equal(1, hook.AfterCount);
        AssertHookRun(
            outcome.HookRuns,
            "fixed:0",
            CopilotToolExecutionHookPhase.BeforeExecute,
            CopilotToolExecutionHookState.Failed,
            "tool_hook_failed");
        AssertTerminalEvent(events, CopilotToolExecutionState.Denied, "tool_hook_failed");
    }

    [Fact]
    public async Task HookPhaseTimeoutIsAStableDeniedOutcome()
    {
        var neverCompletes = new TaskCompletionSource<CopilotToolExecutionHookDecision>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var hook = new RecordingHook((_, _) => neverCompletes.Task);
        var tool = new RecordingTool();
        var events = new List<CopilotAgentEvent>();

        var outcome = await new CopilotToolExecutor(
            [hook],
            hookPhaseTimeout: TimeSpan.FromMilliseconds(50)).ExecuteAsync(
                CreateInvocation(tool, "timed-out-hook"),
                events.Add,
                CancellationToken.None).WaitAsync(TestTimeout);

        AssertDeniedOutcome(
            outcome,
            "tool_hook_timeout",
            CopilotToolFailureKind.Internal);
        Assert.Equal(0, tool.ExecutionCount);
        Assert.Equal(1, hook.AfterCount);
        AssertHookRun(
            outcome.HookRuns,
            "fixed:0",
            CopilotToolExecutionHookPhase.BeforeExecute,
            CopilotToolExecutionHookState.TimedOut,
            "tool_hook_timeout");
        AssertTerminalEvent(events, CopilotToolExecutionState.Denied, "tool_hook_timeout");
    }

    [Fact]
    public async Task CallerCancellationDuringHookPublishesCancelledTerminalBeforeRethrow()
    {
        var hookStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var hook = new RecordingHook(async (_, cancellationToken) =>
        {
            hookStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return CopilotToolExecutionHookDecision.Proceed;
        });
        var tool = new RecordingTool();
        var events = new List<CopilotAgentEvent>();
        using var cancellation = new CancellationTokenSource();
        var executionTask = new CopilotToolExecutor([hook]).ExecuteAsync(
            CreateInvocation(tool, "caller-cancelled-hook"),
            events.Add,
            cancellation.Token);
        await hookStarted.Task.WaitAsync(TestTimeout);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => executionTask.WaitAsync(TestTimeout));
        Assert.Equal(0, tool.ExecutionCount);
        Assert.Equal(1, hook.AfterCount);
        Assert.Equal(
            CopilotToolExecutionState.Cancelled,
            Assert.IsType<CopilotToolExecutionOutcome>(hook.LastOutcome).Execution.State);
        var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
        AssertHookRun(
            terminal.ToolExecutionHookRuns,
            "fixed:0",
            CopilotToolExecutionHookPhase.BeforeExecute,
            CopilotToolExecutionHookState.Cancelled,
            "tool_execution_cancelled");
        AssertTerminalEvent(
            events,
            CopilotToolExecutionState.Cancelled,
            "tool_execution_cancelled");
    }

    [Fact]
    public async Task BuiltInReviewPolicyUsesStableFailureCode()
    {
        var tool = new RecordingTool(writeCapable: true);
        var events = new List<CopilotAgentEvent>();

        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            CreateInvocation(tool, "review-write", CopilotAgentMode.Review),
            events.Add,
            CancellationToken.None);

        AssertDeniedOutcome(
            outcome,
            "review_mode_write_denied",
            CopilotToolFailureKind.Authorization);
        Assert.Equal(0, tool.ExecutionCount);
        AssertHookRun(
            outcome.HookRuns,
            "builtin:write-tool-policy",
            CopilotToolExecutionHookPhase.BeforeExecute,
            CopilotToolExecutionHookState.Denied,
            "review_mode_write_denied");
        AssertTerminalEvent(events, CopilotToolExecutionState.Denied, "review_mode_write_denied");
    }

    [Fact]
    public async Task FailedAfterHookRemainsObservableWithoutChangingToolOutcomeOrModelPayload()
    {
        const string sensitiveMessage = "api_key=post-hook-secret";
        var hook = new RecordingHook(
            (_, _) => Task.FromResult(CopilotToolExecutionHookDecision.Proceed),
            (_, _) => Task.FromException(new InvalidOperationException(sensitiveMessage)));
        var events = new List<CopilotAgentEvent>();

        var outcome = await new CopilotToolExecutor([hook]).ExecuteAsync(
            CreateInvocation(new RecordingTool(), "failed-after-hook"),
            events.Add,
            CancellationToken.None);

        Assert.True(outcome.Result.Success);
        AssertHookRun(
            outcome.HookRuns,
            "fixed:0",
            CopilotToolExecutionHookPhase.AfterExecute,
            CopilotToolExecutionHookState.Failed,
            "tool_hook_failed");
        var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.Equal(outcome.HookRuns, terminal.ToolExecutionHookRuns);
        var audit = Assert.Single(
            CopilotToolExecutionAuditLogger.GetRecentEntries(),
            item => item.CallId == "failed-after-hook");
        Assert.Contains("after:fixed:0=failed/", audit.HookSummary, StringComparison.Ordinal);
        Assert.Contains("tool_hook_failed", audit.HookSummary, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveMessage, audit.HookSummary, StringComparison.Ordinal);

        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        CopilotAssistantMessagePresenter.ApplyAgentEvent(assistant, terminal);
        var trace = Assert.Single(assistant.AgentTraceEntries);
        AssertHookRun(
            trace.HookRuns,
            "fixed:0",
            CopilotToolExecutionHookPhase.AfterExecute,
            CopilotToolExecutionHookState.Failed,
            "tool_hook_failed");
        Assert.Contains("after fixed:0 · failed", trace.DiagnosticDetails, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveMessage, trace.DiagnosticDetails, StringComparison.Ordinal);

        var serializedTrace = JsonConvert.SerializeObject(trace);
        var restoredTrace = Assert.IsType<CopilotAgentTraceEntry>(
            JsonConvert.DeserializeObject<CopilotAgentTraceEntry>(serializedTrace));
        restoredTrace.EnsureValid(DateTimeOffset.UtcNow);
        AssertHookRun(
            restoredTrace.HookRuns,
            "fixed:0",
            CopilotToolExecutionHookPhase.AfterExecute,
            CopilotToolExecutionHookState.Failed,
            "tool_hook_failed");
        Assert.DoesNotContain("fixed:0", CopilotFrameworkToolResultFormatter.Format(outcome), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TimedOutAfterHookRemainsObservableWithoutChangingToolOutcome()
    {
        var neverCompletes = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var hook = new RecordingHook(
            (_, _) => Task.FromResult(CopilotToolExecutionHookDecision.Proceed),
            (_, _) => neverCompletes.Task);

        var outcome = await new CopilotToolExecutor(
            [hook],
            hookPhaseTimeout: TimeSpan.FromMilliseconds(50)).ExecuteAsync(
                CreateInvocation(new RecordingTool(), "timed-out-after-hook"),
                _ => { },
                CancellationToken.None).WaitAsync(TestTimeout);

        Assert.True(outcome.Result.Success);
        AssertHookRun(
            outcome.HookRuns,
            "fixed:0",
            CopilotToolExecutionHookPhase.AfterExecute,
            CopilotToolExecutionHookState.TimedOut,
            "tool_hook_timeout");
    }

    private static void AssertDeniedOutcome(
        CopilotToolExecutionOutcome outcome,
        string expectedFailureCode,
        CopilotToolFailureKind expectedFailureKind)
    {
        Assert.Equal(CopilotToolExecutionState.Denied, outcome.Execution.State);
        Assert.False(outcome.Result.Success);
        Assert.Equal(expectedFailureKind, outcome.Result.FailureKind);
        Assert.Equal(expectedFailureCode, outcome.Result.FailureCode);
        Assert.False(outcome.Execution.RetryEligible);
    }

    private static void AssertTerminalEvent(
        IReadOnlyList<CopilotAgentEvent> events,
        CopilotToolExecutionState expectedState,
        string expectedFailureCode)
    {
        Assert.DoesNotContain(events, item => item.Type == CopilotAgentEventType.ToolStarted);
        var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.Equal(
            expectedState,
            Assert.IsType<CopilotToolExecutionInfo>(terminal.ToolExecution).State);
        Assert.Equal(
            expectedFailureCode,
            Assert.IsType<CopilotToolResult>(terminal.ToolResult).FailureCode);
        Assert.NotEmpty(terminal.ToolExecutionHookRuns);
    }

    private static void AssertHookRun(
        IReadOnlyList<CopilotToolExecutionHookRun> hookRuns,
        string sourceId,
        CopilotToolExecutionHookPhase phase,
        CopilotToolExecutionHookState state,
        string failureCode)
    {
        var hookRun = Assert.Single(hookRuns, item =>
            item.SourceId == sourceId
            && item.Phase == phase);
        Assert.Equal(state, hookRun.State);
        Assert.Equal(failureCode, hookRun.FailureCode);
        Assert.InRange(hookRun.DurationMs, 0, CopilotToolExecutionHookRun.MaxDurationMs);
        Assert.True(hookRun.IsStructurallyValid());
    }

    private static CopilotToolInvocation CreateInvocation(
        ICopilotTool tool,
        string callId,
        CopilotAgentMode mode = CopilotAgentMode.Auto)
    {
        return new CopilotToolInvocation
        {
            CallId = callId,
            Round = 1,
            Attempt = 1,
            MaxAttempts = 1,
            RuntimeName = "hook-integrity-test",
            Tool = tool,
            AgentRequest = new CopilotAgentRequest
            {
                Mode = mode,
                UserText = "Run the hook integrity test.",
            },
        };
    }

    private sealed class RecordingHook : ICopilotToolExecutionHook
    {
        private readonly Func<
            CopilotToolExecutionHookContext,
            CancellationToken,
            Task<CopilotToolExecutionHookDecision>> _beforeExecute;
        private readonly Func<
            CopilotToolExecutionOutcome,
            CancellationToken,
            Task> _afterExecute;
        private int _afterCount;

        public RecordingHook(
            Func<
                CopilotToolExecutionHookContext,
                CancellationToken,
                Task<CopilotToolExecutionHookDecision>> beforeExecute,
            Func<
                CopilotToolExecutionOutcome,
                CancellationToken,
                Task>? afterExecute = null)
        {
            _beforeExecute = beforeExecute;
            _afterExecute = afterExecute ?? ((_, _) => Task.CompletedTask);
        }

        public int AfterCount => Volatile.Read(ref _afterCount);

        public CopilotToolExecutionOutcome? LastOutcome { get; private set; }

        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken) =>
            _beforeExecute(context, cancellationToken);

        public Task AfterExecuteAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken)
        {
            LastOutcome = outcome;
            Interlocked.Increment(ref _afterCount);
            return _afterExecute(outcome, cancellationToken);
        }
    }

    private sealed class RecordingTool : ICopilotTool
    {
        private readonly bool _writeCapable;
        private int _executionCount;

        public RecordingTool(bool writeCapable = false)
        {
            _writeCapable = writeCapable;
        }

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public string Name => "HookIntegrityTool";

        public string Description => "Records whether hook integrity tests reached tool execution.";

        public CopilotToolCapabilityDescriptor Capability => _writeCapable
            ? new CopilotToolCapabilityDescriptor
            {
                Access = CopilotToolAccess.Write,
                RiskLevel = CopilotToolRiskLevel.Medium,
                ApprovalMode = CopilotToolApprovalMode.Never,
                Idempotency = CopilotToolIdempotency.NonIdempotent,
                ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
                EvidenceMode = CopilotToolEvidenceMode.None,
            }
            : CopilotToolCapabilityDescriptor.ReadOnly();

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executionCount);
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Tool executed.",
            });
        }
    }
}
