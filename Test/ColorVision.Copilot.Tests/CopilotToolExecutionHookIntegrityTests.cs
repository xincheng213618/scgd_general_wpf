using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotToolExecutionHookIntegrityTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task SlowHooksPublishOrderedLifecycleBeforeTerminalReconciliation()
    {
        var beforeEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBefore = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hook = new RecordingHook(async (_, cancellationToken) =>
        {
            beforeEntered.TrySetResult();
            await releaseBefore.Task.WaitAsync(cancellationToken);
            return CopilotToolExecutionHookDecision.Proceed;
        });
        var events = new System.Collections.Concurrent.ConcurrentQueue<CopilotAgentEvent>();

        var executionTask = new CopilotToolExecutor([hook]).ExecuteAsync(
            CreateInvocation(new RecordingTool(), "live-hook-lifecycle"),
            events.Enqueue,
            CancellationToken.None);
        await beforeEntered.Task.WaitAsync(TestTimeout);

        var liveEvents = events.ToArray();
        var liveStart = Assert.Single(liveEvents, item =>
            item.Type == CopilotAgentEventType.HookStarted
            && item.ToolExecutionHook?.SourceId == "fixed:0");
        Assert.Equal(CopilotToolExecutionHookPhase.BeforeExecute, liveStart.ToolExecutionHook?.Phase);
        Assert.False(liveStart.ToolExecutionHook?.IsCompleted);
        Assert.DoesNotContain(liveEvents, item =>
            item.Type == CopilotAgentEventType.HookCompleted
            && item.ToolExecutionHook?.SourceId == "fixed:0");
        Assert.DoesNotContain(liveEvents, item => item.Type == CopilotAgentEventType.ToolResult);

        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        foreach (var agentEvent in liveEvents)
            CopilotAssistantMessagePresenter.ApplyAgentEvent(assistant, agentEvent);
        var liveTrace = Assert.Single(assistant.AgentTraceEntries);
        Assert.Contains("Running pre-execution hook fixed:0", liveTrace.ResultSummary, StringComparison.Ordinal);

        releaseBefore.TrySetResult();
        var outcome = await executionTask.WaitAsync(TestTimeout);
        var published = events.ToArray();
        Assert.Equal(
            [
                CopilotAgentEventType.HookStarted,
                CopilotAgentEventType.HookCompleted,
                CopilotAgentEventType.HookStarted,
                CopilotAgentEventType.HookCompleted,
                CopilotAgentEventType.ToolStarted,
                CopilotAgentEventType.HookStarted,
                CopilotAgentEventType.HookCompleted,
                CopilotAgentEventType.HookStarted,
                CopilotAgentEventType.HookCompleted,
                CopilotAgentEventType.ToolResult,
            ],
            published.Select(item => item.Type));

        foreach (var agentEvent in published.Skip(liveEvents.Length))
            CopilotAssistantMessagePresenter.ApplyAgentEvent(assistant, agentEvent);

        var finalTrace = Assert.Single(assistant.AgentTraceEntries);
        Assert.Equal(4, finalTrace.HookRuns.Count);
        Assert.Equal(outcome.HookRuns.Select(run => (run.SourceId, run.Phase, run.State, run.FailureCode)),
            finalTrace.HookRuns.Select(run => (run.SourceId, run.Phase, run.State, run.FailureCode)));
        Assert.Equal(CopilotToolExecutionState.Completed, finalTrace.State);
    }

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
    public async Task TerminalEventDispatchFailureDoesNotReclassifyTheCompletedTool()
    {
        var tool = new RecordingTool();
        var terminalEvents = new List<CopilotAgentEvent>();

        var exception = await Assert.ThrowsAsync<CopilotToolResultEventDispatchException>(() =>
            new CopilotToolExecutor().ExecuteAsync(
                CreateInvocation(tool, "terminal-dispatch-failure"),
                agentEvent =>
                {
                    if (agentEvent.Type != CopilotAgentEventType.ToolResult)
                        return;

                    terminalEvents.Add(agentEvent);
                    throw new InvalidOperationException("The event consumer stopped accepting results.");
                },
                CancellationToken.None));

        var terminal = Assert.Single(terminalEvents);
        Assert.Equal(1, tool.ExecutionCount);
        Assert.Equal(CopilotToolExecutionState.Completed, terminal.ToolExecution?.State);
        Assert.Same(exception.Outcome.Execution, terminal.ToolExecution);
        Assert.Same(exception.Outcome.Result, terminal.ToolResult);
        Assert.Equal(CopilotToolExecutionState.Completed, exception.Outcome.Execution.State);
        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public async Task FrameworkBridgeRetainsTheCommittedOutcomeWhenTerminalDispatchFails()
    {
        var tool = new RecordingTool();
        var request = new CopilotAgentRequest
        {
            ConversationId = "dispatch-failure-conversation",
            TaskId = "dispatch-failure-task",
            Mode = CopilotAgentMode.Auto,
            UserText = "Run the recording tool.",
            TaskIntentText = "Run the recording tool.",
        };
        var terminalEvents = new List<CopilotAgentEvent>();
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(
            request,
            CopilotExecutionScope.ForAgentRun(request),
            [tool],
            maxToolCalls: 1,
            new CopilotToolExecutor(),
            new CopilotFrameworkApprovalCoordinator(),
            agentEvent =>
            {
                if (agentEvent.Type != CopilotAgentEventType.ToolResult)
                    return;

                terminalEvents.Add(agentEvent);
                throw new InvalidOperationException("The event consumer stopped accepting results.");
            },
            capabilityRevisionProvider: () => 1);
        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(bridge.CreateFunctions()));

        var exception = await Assert.ThrowsAsync<CopilotToolResultEventDispatchException>(() =>
            function.InvokeAsync(
                new AIFunctionArguments(),
                CancellationToken.None).AsTask());

        var step = Assert.Single(bridge.StepRecords);
        Assert.Single(terminalEvents);
        Assert.Equal(1, tool.ExecutionCount);
        Assert.Equal(CopilotToolExecutionState.Completed, step.Execution.State);
        Assert.Same(exception.Outcome.Execution, step.Execution);
        Assert.Equal(exception.Outcome.Result.Success, step.Observation.Success);
        Assert.Equal(exception.Outcome.Result.Summary, step.Observation.Summary);
    }

    [Fact]
    public async Task FrozenHookBindingsCannotReplaceTheMonotonicWriteGuard()
    {
        var spoofedGuard = new RecordingHook((_, _) =>
            Task.FromResult(CopilotToolExecutionHookDecision.Proceed));
        var tool = new RecordingTool(writeCapable: true);
        var invocation = CopyWithInitialHooks(
            CreateInvocation(tool, "spoofed-write-guard", CopilotAgentMode.Plan),
            [
                new CopilotToolExecutionHookBinding(
                    "builtin:write-tool-policy",
                    spoofedGuard),
            ]);

        var outcome = await new CopilotToolExecutor([]).ExecuteAsync(
            invocation,
            _ => { },
            CancellationToken.None);

        AssertDeniedOutcome(
            outcome,
            "plan_mode_write_denied",
            CopilotToolFailureKind.Authorization);
        Assert.Equal(0, tool.ExecutionCount);
        Assert.Equal(0, spoofedGuard.BeforeCount);
        AssertHookRun(
            outcome.HookRuns,
            "builtin:write-tool-policy",
            CopilotToolExecutionHookPhase.BeforeExecute,
            CopilotToolExecutionHookState.Denied,
            "plan_mode_write_denied");
    }

    [Fact]
    public async Task FrozenHookBindingsRetainCommandHooksAfterFullRegistrySurface()
    {
        var spoofedGuard = new RecordingHook((_, _) =>
            Task.FromResult(CopilotToolExecutionHookDecision.Proceed));
        var registryHook = new RecordingHook((_, _) =>
            Task.FromResult(CopilotToolExecutionHookDecision.Proceed));
        var finalCommandHook = new RecordingHook((_, _) => Task.FromResult(
            CopilotToolExecutionHookDecision.Deny(
                "The final captured command hook denied execution.",
                "late_captured_hook_denied")));
        var captured = new List<CopilotToolExecutionHookBinding>
        {
            new("builtin:write-tool-policy", spoofedGuard),
        };
        captured.AddRange(Enumerable.Range(0, CopilotToolExecutionHookRegistry.MaxRegistrations)
            .Select(index => new CopilotToolExecutionHookBinding(
                $"registry:{index}",
                registryHook)));
        captured.Add(new CopilotToolExecutionHookBinding("command:final", finalCommandHook));
        var tool = new RecordingTool();
        var invocation = CopyWithInitialHooks(
            CreateInvocation(tool, "full-frozen-hook-surface"),
            captured);

        var outcome = await new CopilotToolExecutor([]).ExecuteAsync(
            invocation,
            _ => { },
            CancellationToken.None);

        AssertDeniedOutcome(
            outcome,
            "late_captured_hook_denied",
            CopilotToolFailureKind.Authorization);
        Assert.Equal(0, tool.ExecutionCount);
        Assert.Equal(1, finalCommandHook.BeforeCount);
        Assert.Equal(CopilotToolExecutionHookRegistry.MaxRegistrations, registryHook.BeforeCount);
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
        Assert.DoesNotContain(
            sensitiveMessage,
            JsonConvert.SerializeObject(events.Where(item =>
                item.Type is CopilotAgentEventType.HookStarted or CopilotAgentEventType.HookCompleted)),
            StringComparison.Ordinal);
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
    public async Task BuiltInPlanPolicyUsesStableFailureCode()
    {
        var tool = new RecordingTool(writeCapable: true);
        var events = new List<CopilotAgentEvent>();

        var outcome = await new CopilotToolExecutor().ExecuteAsync(
            CreateInvocation(tool, "plan-write", CopilotAgentMode.Plan),
            events.Add,
            CancellationToken.None);

        AssertDeniedOutcome(
            outcome,
            "plan_mode_write_denied",
            CopilotToolFailureKind.Authorization);
        Assert.Equal(0, tool.ExecutionCount);
        AssertHookRun(
            outcome.HookRuns,
            "builtin:write-tool-policy",
            CopilotToolExecutionHookPhase.BeforeExecute,
            CopilotToolExecutionHookState.Denied,
            "plan_mode_write_denied");
        AssertTerminalEvent(events, CopilotToolExecutionState.Denied, "plan_mode_write_denied");
    }

    [Fact]
    public async Task InvocationSnapshotPreventsHooksFromChangingTheExecutedArguments()
    {
        var nested = new Dictionary<string, object?> { ["mode"] = "safe" };
        var sourceArguments = new Dictionary<string, object?>
        {
            ["query"] = "safe",
            ["options"] = nested,
        };
        var toolInput = new CopilotAgentToolInput
        {
            Arguments = sourceArguments,
            Query = "safe",
        };
        var hook = new RecordingHook((context, _) =>
        {
            sourceArguments["query"] = "tampered";
            nested["mode"] = "tampered";
            var publishedArguments = Assert.IsAssignableFrom<IDictionary<string, object?>>(
                context.Invocation.ToolInput.Arguments);
            Assert.Throws<NotSupportedException>(() => publishedArguments["query"] = "hook-tampered");
            return Task.FromResult(CopilotToolExecutionHookDecision.Proceed);
        });
        var tool = new RecordingTool(inputSchema: CreateOpenObjectInputSchema());
        var invocation = CreateInvocation(tool, "immutable-input-snapshot");
        invocation = new CopilotToolInvocation
        {
            CallId = invocation.CallId,
            Round = invocation.Round,
            Attempt = invocation.Attempt,
            MaxAttempts = invocation.MaxAttempts,
            RuntimeName = invocation.RuntimeName,
            Tool = invocation.Tool,
            AgentRequest = invocation.AgentRequest,
            ToolInput = toolInput,
            ToolCall = new CopilotToolCall
            {
                ToolName = "SpoofedTool",
                ToolInput = toolInput,
            },
        };

        var outcome = await new CopilotToolExecutor([hook]).ExecuteAsync(
            invocation,
            _ => { },
            CancellationToken.None);

        var executedInput = Assert.IsType<CopilotAgentToolInput>(tool.LastInput);
        Assert.Equal("safe", executedInput.GetStringArgument("query"));
        Assert.True(executedInput.TryGetJsonElementArgument("options", out var options));
        Assert.Equal("safe", options.GetProperty("mode").GetString());
        Assert.Same(outcome.Invocation.ToolInput, outcome.Invocation.ToolCall.ToolInput);
        Assert.Equal(tool.Name, outcome.Invocation.ToolCall.ToolName);
        Assert.Equal("safe", outcome.Invocation.ToolInput.GetStringArgument("query"));
    }

    [Fact]
    public async Task InvalidInputContractSkipsHooksAndToolExecution()
    {
        var hook = new RecordingHook((_, _) =>
            Task.FromResult(CopilotToolExecutionHookDecision.Proceed));
        var tool = new RecordingTool(
            inputSchema: CopilotToolInputSchema.Query("Required query.", required: true));
        var events = new List<CopilotAgentEvent>();

        var outcome = await new CopilotToolExecutor([hook]).ExecuteAsync(
            CreateInvocation(tool, "invalid-input-contract"),
            events.Add,
            CancellationToken.None);

        Assert.Equal(CopilotToolExecutionState.Failed, outcome.Execution.State);
        Assert.Equal(CopilotToolFailureKind.Validation, outcome.Result.FailureKind);
        Assert.Equal("invalid_arguments", outcome.Result.FailureCode);
        Assert.Equal(0, tool.ExecutionCount);
        Assert.Equal(0, hook.BeforeCount);
        Assert.Equal(0, hook.AfterCount);
        Assert.Empty(outcome.HookRuns);
        Assert.Equal(CopilotAgentEventType.ToolResult, Assert.Single(events).Type);
    }

    [Fact]
    public async Task ValidatedArgumentsAreTheCanonicalExecutedInput()
    {
        var tool = new RecordingTool(
            inputSchema: CopilotToolInputSchema.Query("Required query.", required: true));
        var invocation = CreateInvocation(tool, "canonical-input-contract");
        invocation = new CopilotToolInvocation
        {
            CallId = invocation.CallId,
            Round = invocation.Round,
            RuntimeName = invocation.RuntimeName,
            Tool = tool,
            AgentRequest = invocation.AgentRequest,
            ToolInput = new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["query"] = "canonical",
                },
                Query = "untrusted-legacy-value",
            },
        };

        var outcome = await new CopilotToolExecutor([]).ExecuteAsync(
            invocation,
            _ => { },
            CancellationToken.None);

        Assert.True(outcome.Result.Success);
        Assert.Equal("canonical", Assert.IsType<CopilotAgentToolInput>(tool.LastInput).Query);
        Assert.Equal("canonical", outcome.Invocation.ToolInput.Query);
        Assert.Equal("canonical", outcome.Invocation.ToolCall.ToolInput.Query);
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

    private static CopilotToolInputSchema CreateOpenObjectInputSchema()
    {
        return CopilotToolInputSchema.FromJsonSchema(
            System.Text.Json.JsonSerializer.SerializeToElement(
                new Dictionary<string, object?>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = true,
                }));
    }

    private static CopilotToolInvocation CopyWithInitialHooks(
        CopilotToolInvocation source,
        IReadOnlyList<CopilotToolExecutionHookBinding> hookBindings)
    {
        return new CopilotToolInvocation
        {
            CallId = source.CallId,
            Round = source.Round,
            Attempt = source.Attempt,
            MaxAttempts = source.MaxAttempts,
            RuntimeName = source.RuntimeName,
            Tool = source.Tool,
            AgentRequest = source.AgentRequest,
            ToolInput = source.ToolInput,
            ToolCall = source.ToolCall,
            InitialHookBindings = hookBindings,
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

        public int BeforeCount { get; private set; }

        public CopilotToolExecutionOutcome? LastOutcome { get; private set; }

        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken)
        {
            BeforeCount++;
            return _beforeExecute(context, cancellationToken);
        }

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
        private readonly CopilotToolInputSchema _inputSchema;
        private int _executionCount;

        public RecordingTool(
            bool writeCapable = false,
            CopilotToolInputSchema? inputSchema = null)
        {
            _writeCapable = writeCapable;
            _inputSchema = inputSchema ?? CopilotToolInputSchema.OptionalQuery;
        }

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public CopilotAgentToolInput? LastInput { get; private set; }

        public string Name => "HookIntegrityTool";

        public string Description => "Records whether hook integrity tests reached tool execution.";

        public CopilotToolInputSchema InputSchema => _inputSchema;

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
            LastInput = toolInput;
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
