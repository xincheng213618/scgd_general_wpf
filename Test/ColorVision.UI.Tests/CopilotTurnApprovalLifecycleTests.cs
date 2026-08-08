using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotTurnApprovalLifecycleTests
{
    private const string TurnId = "run:11111111111111111111111111111111";
    private const string ActionId = "approval:11111111111111111111111111111111";

    [Fact]
    public void ReducerAcceptsApprovalRequestExecutionAndTerminalResult()
    {
        var state = Observe(CreateStartedState(), CreateApprovalRequest());

        state = Observe(
            state,
            CopilotAgentEvent.ToolStarted(CreateExecution(
                CopilotToolExecutionState.Running,
                ActionId)));
        state = Observe(
            state,
            CopilotAgentEvent.FromToolResult(
                new CopilotToolResult
                {
                    ToolName = "ProtectedTool",
                    Success = true,
                    Summary = "Protected tool completed.",
                },
                CreateExecution(CopilotToolExecutionState.Completed, ActionId)));
        state = Observe(state, CopilotAgentEvent.Completed());

        Assert.True(state.AgentCompleted);
    }

    [Fact]
    public void ReducerAcceptsApprovalRequestFollowedByDenial()
    {
        var state = Observe(CreateStartedState(), CreateApprovalRequest());

        state = Observe(
            state,
            CopilotAgentEvent.FromToolResult(
                new CopilotToolResult
                {
                    ToolName = "ProtectedTool",
                    Success = false,
                    Summary = "Approval was denied.",
                    FailureKind = CopilotToolFailureKind.Authorization,
                },
                CreateExecution(CopilotToolExecutionState.Denied, ActionId)));
        state = Observe(state, CopilotAgentEvent.Completed());

        Assert.True(state.AgentCompleted);
    }

    [Fact]
    public void ReducerRejectsAgentCompletionWithPendingTurnBlockingApproval()
    {
        var state = Observe(CreateStartedState(), CreateApprovalRequest());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(state, CopilotAgentEvent.Completed()));

        Assert.Contains("approval request was still pending", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsContinuationWithDifferentApprovalAction()
    {
        var state = Observe(CreateStartedState(), CreateApprovalRequest());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                state,
                CopilotAgentEvent.ToolStarted(CreateExecution(
                    CopilotToolExecutionState.Running,
                    "approval:22222222222222222222222222222222"))));

        Assert.Contains("did not match", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerRejectsDuplicateApprovalRequest()
    {
        var state = Observe(CreateStartedState(), CreateApprovalRequest());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(state, CreateApprovalRequest()));

        Assert.Contains("more than once", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReducerAllowsDeferredApprovalToOutliveAgentTurn()
    {
        var state = Observe(
            CreateStartedState(),
            CreateApprovalRequest(
                executeOnApproval: true,
                resumesAgentOnApproval: false));

        state = Observe(state, CopilotAgentEvent.Completed());

        Assert.True(state.AgentCompleted);
    }

    [Fact]
    public void ReducerRejectsContradictoryApprovalExecutionSemantics()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Observe(
                CreateStartedState(),
                CreateApprovalRequest(
                    executeOnApproval: true,
                    resumesAgentOnApproval: true)));

        Assert.Contains("invalid approval request", exception.Message, StringComparison.Ordinal);
    }

    private static CopilotTurnEventState CreateStartedState() =>
        CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Auto, TurnId),
            new CopilotTurnStartedEvent(TurnId, CopilotAgentMode.Auto));

    private static CopilotTurnEventState Observe(
        CopilotTurnEventState state,
        CopilotAgentEvent agentEvent) =>
        CopilotTurnEventReducer.Reduce(state, new CopilotTurnAgentEvent(agentEvent));

    private static CopilotAgentEvent CreateApprovalRequest(
        bool executeOnApproval = false,
        bool resumesAgentOnApproval = true) =>
        CopilotAgentEvent.FromToolResult(
            new CopilotToolResult
            {
                ToolName = "ProtectedTool",
                Success = true,
                Summary = "Protected tool is waiting for approval.",
                Approval = new CopilotToolApprovalInfo
                {
                    ActionId = ActionId,
                    Title = "Run protected tool",
                    RiskLevel = "confirmation-required",
                    ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(5),
                    ExecuteOnApproval = executeOnApproval,
                    ResumesAgentOnApproval = resumesAgentOnApproval,
                },
            },
            CreateExecution(CopilotToolExecutionState.AwaitingApproval, ActionId));

    private static CopilotToolExecutionInfo CreateExecution(
        CopilotToolExecutionState state,
        string actionId) => new()
        {
            CallId = "provider-call-1",
            Round = 1,
            Attempt = 1,
            MaxAttempts = 2,
            RuntimeName = "agent-framework",
            ToolName = "ProtectedTool",
            Access = CopilotToolAccess.Write,
            RiskLevel = CopilotToolRiskLevel.High,
            ApprovalMode = CopilotToolApprovalMode.Always,
            Idempotency = CopilotToolIdempotency.NonIdempotent,
            ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
            ConcurrencyKey = "resource:test",
            ApprovalActionId = actionId,
            ArgumentSummary = "path=C:\\workspace\\file.txt",
            State = state,
            FailureKind = state == CopilotToolExecutionState.Denied
                ? CopilotToolFailureKind.Authorization
                : CopilotToolFailureKind.None,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
            CompletedAtUtc = state is CopilotToolExecutionState.Completed or CopilotToolExecutionState.Denied
                ? DateTimeOffset.UtcNow
                : null,
            TimeoutMs = 30_000,
        };
}
