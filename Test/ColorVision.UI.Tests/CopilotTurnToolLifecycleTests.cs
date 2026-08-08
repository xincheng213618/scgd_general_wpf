using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotTurnToolLifecycleTests
{
    private const string TurnId = "run:11111111111111111111111111111111";

    [Fact]
    public void ReducerAcceptsQueuedRunningAndTerminalToolLifecycle()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.ToolProgress(
                CreateExecution(CopilotToolExecutionState.Pending, durationMs: 100, queueDurationMs: 100),
                "ProtectedTool is waiting for an execution slot."));
        state = Observe(
            state,
            CopilotAgentEvent.ToolStarted(
                CreateExecution(CopilotToolExecutionState.Running, durationMs: 0, queueDurationMs: 100)));
        state = Observe(
            state,
            CopilotAgentEvent.ToolProgress(
                CreateExecution(CopilotToolExecutionState.Running, durationMs: 150, queueDurationMs: 100),
                "ProtectedTool is running.",
                new CopilotToolProgressUpdate
                {
                    Message = "Applying changes",
                    Completed = 1,
                    Total = 2,
                    Unit = "files",
                }));
        state = Observe(
            state,
            CreateTerminalResult(
                CreateExecution(CopilotToolExecutionState.Completed, durationMs: 200, queueDurationMs: 100),
                success: true));
        state = Observe(state, CopilotAgentEvent.Completed());

        Assert.True(state.AgentCompleted);
    }

    [Fact]
    public void ReducerRejectsRunningProgressBeforeToolStart()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                CreateStartedState(),
                CopilotAgentEvent.ToolProgress(
                    CreateExecution(CopilotToolExecutionState.Running, durationMs: 10),
                    "ProtectedTool is running.")));

        Assert.Contains("before the tool started", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsPendingProgressAfterToolStart()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.ToolStarted(CreateExecution(CopilotToolExecutionState.Running)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.ToolProgress(
                    CreateExecution(CopilotToolExecutionState.Pending, durationMs: 10),
                    "ProtectedTool is queued.")));

        Assert.Contains("back to pending", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsAgentCompletionWhileToolIsRunning()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.ToolStarted(CreateExecution(CopilotToolExecutionState.Running)));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(state, CopilotAgentEvent.Completed()));

        Assert.Contains("tool item was still active", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerAcceptsDirectRejectedResultWithoutToolStart()
    {
        var state = Observe(
            CreateStartedState(),
            CreateTerminalResult(
                CreateExecution(CopilotToolExecutionState.Failed, durationMs: 0),
                success: false));
        state = Observe(state, CopilotAgentEvent.Completed());

        Assert.True(state.AgentCompleted);
    }

    [Fact]
    public void ReducerRejectsResultThatChangesExecutionIdentity()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.ToolStarted(CreateExecution(CopilotToolExecutionState.Running)));
        var mismatched = CreateExecution(
            CopilotToolExecutionState.Completed,
            durationMs: 10,
            runtimeName: "different-runtime");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(state, CreateTerminalResult(mismatched, success: true)));

        Assert.Contains("did not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsCompletedStateWithFailedResult()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                CreateStartedState(),
                CreateTerminalResult(
                    CreateExecution(CopilotToolExecutionState.Completed),
                    success: false)));

        Assert.Contains("invalid state metadata", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsToolProgressThatMovesBackwards()
    {
        var state = Observe(
            CreateStartedState(),
            CopilotAgentEvent.ToolStarted(CreateExecution(CopilotToolExecutionState.Running)));
        state = Observe(
            state,
            CopilotAgentEvent.ToolProgress(
                CreateExecution(CopilotToolExecutionState.Running, durationMs: 100),
                "ProtectedTool is running."));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.ToolProgress(
                    CreateExecution(CopilotToolExecutionState.Running, durationMs: 90),
                    "ProtectedTool is running.")));

        Assert.Contains("moved backwards", exception.Message, StringComparison.Ordinal);
    }

    private static CopilotTurnEventState CreateStartedState() =>
        CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Auto, TurnId),
            new CopilotTurnStartedEvent(TurnId, CopilotAgentMode.Auto));

    private static CopilotTurnEventState Observe(
        CopilotTurnEventState state,
        CopilotAgentEvent agentEvent) =>
        CopilotTurnEventReducer.Reduce(state, new CopilotTurnAgentEvent(agentEvent));

    private static CopilotAgentEvent CreateTerminalResult(
        CopilotToolExecutionInfo execution,
        bool success) =>
        CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = execution.ToolName,
                Success = success,
                Summary = success ? "Protected tool completed." : "Protected tool was rejected.",
                FailureKind = success
                    ? CopilotToolFailureKind.None
                    : CopilotToolFailureKind.Validation,
            },
            execution);

    private static CopilotToolExecutionInfo CreateExecution(
        CopilotToolExecutionState state,
        long durationMs = 0,
        long queueDurationMs = 0,
        string runtimeName = "agent-framework") => new()
        {
            CallId = "provider-call-1",
            Round = 1,
            Attempt = 1,
            MaxAttempts = 2,
            RuntimeName = runtimeName,
            ToolName = "ProtectedTool",
            Access = CopilotToolAccess.Write,
            RiskLevel = CopilotToolRiskLevel.High,
            ApprovalMode = CopilotToolApprovalMode.Always,
            Idempotency = CopilotToolIdempotency.NonIdempotent,
            ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
            ConcurrencyKey = "resource:test",
            ArgumentSummary = "path=C:\\workspace\\file.txt",
            State = state,
            FailureKind = state == CopilotToolExecutionState.Failed
                ? CopilotToolFailureKind.Validation
                : CopilotToolFailureKind.None,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            CompletedAtUtc = state is not (CopilotToolExecutionState.Pending
                or CopilotToolExecutionState.Running
                or CopilotToolExecutionState.AwaitingApproval)
                    ? DateTimeOffset.UtcNow
                    : null,
            DurationMs = durationMs,
            QueueDurationMs = queueDurationMs,
            TimeoutMs = 30_000,
        };
}
