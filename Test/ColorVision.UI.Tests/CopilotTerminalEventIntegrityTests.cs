using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;

namespace ColorVision.UI.Tests;

[Collection(CopilotApprovalReviewTestGroup.CollectionName)]
public sealed class CopilotTerminalEventIntegrityTests
{
    private const string WorkspacePath = @"C:\ColorVision\TerminalIntegrity";

    [Fact]
    public async Task CancellingPendingFrameworkApprovalEmitsTerminalToolResult()
    {
        var scenario = CreateApprovalScenario("pending-cancel");
        try
        {
            scenario.Bridge.PublishAwaitingApproval(
                scenario.Reservation,
                scenario.Handle.Action);

            scenario.Bridge.CancelApproval(
                scenario.Reservation,
                "The test Agent run was cancelled while approval was pending.");
            var decision = await scenario.Handle.Decision.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.Equal(CopilotFrameworkApprovalDecisionKind.Cancelled, decision.Kind);
            Assert.Equal(ConfirmableActionStatus.Cancelled, scenario.Handle.Action.Status);
            AssertTerminalApprovalEvents(scenario);
        }
        finally
        {
            scenario.Coordinator.Cancel(scenario.Handle);
        }
    }

    [Fact]
    public async Task ApprovedButUnexecutedFrameworkCallEmitsCancelledToolResultAtRunEnd()
    {
        var scenario = CreateApprovalScenario("approved-not-executed");
        try
        {
            scenario.Bridge.PublishAwaitingApproval(
                scenario.Reservation,
                scenario.Handle.Action);
            Assert.True(CopilotMcpConfirmationStore.Instance.Approve(
                scenario.Handle.Action.ActionId,
                new CopilotConfirmationReviewContext(
                    scenario.Request.ConversationId,
                    scenario.Request.TaskId,
                    WorkspacePath),
                out _));
            var decision = await scenario.Handle.Decision.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(decision.IsApproved);
            scenario.Bridge.Approve(scenario.Reservation);

            scenario.Bridge.CancelOutstandingApprovals();

            Assert.Equal(ConfirmableActionStatus.Cancelled, scenario.Handle.Action.Status);
            AssertTerminalApprovalEvents(scenario);
        }
        finally
        {
            scenario.Coordinator.Cancel(scenario.Handle);
        }
    }

    [Fact]
    public async Task PermissionRequestEvidenceSurvivesAwaitingAndRejectedEvents()
    {
        var hook = new PromptPermissionHook();
        var scenario = CreateApprovalScenario("permission-evidence", hook);
        try
        {
            var permission = await scenario.Bridge.EvaluatePermissionRequestAsync(
                scenario.Reservation,
                CancellationToken.None);
            Assert.True(permission.Decision.ShouldPrompt);

            scenario.Bridge.PublishAwaitingApproval(
                scenario.Reservation,
                scenario.Handle.Action);
            scenario.Bridge.Reject(
                scenario.Reservation,
                CopilotFrameworkApprovalDecision.FromStatus(
                    ConfirmableActionStatus.Rejected));

            var toolEvents = scenario.Events
                .Where(item => item.Type == CopilotAgentEventType.ToolResult)
                .ToArray();
            Assert.Equal(2, toolEvents.Length);
            Assert.All(toolEvents, item =>
            {
                var run = Assert.Single(item.ToolExecutionHookRuns);
                Assert.Equal(CopilotToolExecutionHookPhase.PermissionRequest, run.Phase);
                Assert.Equal(CopilotToolExecutionHookState.Completed, run.State);
                Assert.Equal("fixed:0", run.SourceId);
            });
            var step = Assert.Single(scenario.Bridge.StepRecords);
            Assert.Equal("approval_rejected", step.Observation.FailureCode);
        }
        finally
        {
            scenario.Coordinator.Cancel(scenario.Handle);
        }
    }

    [Fact]
    public void AgentCompletedItemInterruptsOnlyTracesMissingTerminalResults()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            RequestMode = CopilotAgentMode.Auto,
            IsExecutionInProgress = true,
        };
        assistant.UpsertAgentTrace(CreateTrace("running", CopilotToolExecutionState.Running));
        assistant.UpsertAgentTrace(CreateTrace("awaiting", CopilotToolExecutionState.AwaitingApproval));
        assistant.UpsertAgentTrace(CreateTrace("completed", CopilotToolExecutionState.Completed));

        var presentation = CopilotAssistantMessagePresenter.ApplyAgentEvent(
            assistant,
            CopilotAgentEvent.Completed());

        Assert.True(presentation.IsHandled);
        Assert.Equal(CopilotAgentEventPersistenceMode.Immediate, presentation.PersistenceMode);
        Assert.False(assistant.IsExecutionInProgress);
        Assert.Collection(
            assistant.AgentTraceEntries,
            trace => AssertInterruptedMissingTerminalResult(trace),
            trace => AssertInterruptedMissingTerminalResult(trace),
            trace => Assert.Equal(CopilotToolExecutionState.Completed, trace.State));
    }

    [Fact]
    public void HostedCancellationClosesRunningTraceAsCancelled()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            RequestMode = CopilotAgentMode.Auto,
            IsExecutionInProgress = true,
        };
        assistant.UpsertAgentTrace(CreateTrace("running", CopilotToolExecutionState.Running));
        conversation.Messages.Add(assistant);

        CopilotHostedTurnCompletion.CompleteCancellation(
            conversation,
            assistant,
            CopilotAgentControlIntent.Cancel);

        var trace = Assert.Single(assistant.AgentTraceEntries);
        Assert.Equal(CopilotToolExecutionState.Cancelled, trace.State);
        Assert.Equal(CopilotToolFailureKind.Cancelled, trace.FailureKind);
        Assert.Equal("tool_execution_cancelled", trace.FailureCode);
        Assert.NotNull(trace.CompletedAtUtc);
        Assert.False(assistant.IsExecutionInProgress);
        Assert.Equal(CopilotAgentStopReason.Cancelled, assistant.AgentStopReason);
    }

    [Fact]
    public void HostedSuccessCannotLeaveRunningTraceBehind()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Final answer.")
        {
            RequestMode = CopilotAgentMode.Auto,
            IsExecutionInProgress = true,
        };
        assistant.UpsertAgentTrace(CreateTrace("running", CopilotToolExecutionState.Running));
        conversation.Messages.Add(assistant);

        CopilotHostedTurnCompletion.CompleteTerminalTurn(
            conversation,
            assistant,
            CopilotTokenUsage.Empty);

        AssertInterruptedMissingTerminalResult(Assert.Single(assistant.AgentTraceEntries));
        Assert.False(assistant.IsExecutionInProgress);
    }

    [Fact]
    public void AppExitRecoveryClosesRunningTraceAsInterrupted()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            RequestMode = CopilotAgentMode.Auto,
            IsResponsePending = true,
            IsExecutionInProgress = true,
        };
        assistant.UpsertAgentTrace(CreateTrace("running", CopilotToolExecutionState.Running));
        conversation.Messages.Add(assistant);

        Assert.True(CopilotInterruptedResponseRecovery.Normalize(
            conversation,
            assistant));

        AssertInterruptedMissingTerminalResult(Assert.Single(assistant.AgentTraceEntries));
        Assert.False(assistant.IsExecutionInProgress);
        Assert.True(assistant.WasResponseInterrupted);
    }

    private static ApprovalScenario CreateApprovalScenario(
        string suffix,
        params ICopilotToolExecutionHook[] hooks)
    {
        var request = new CopilotAgentRequest
        {
            ConversationId = "terminal-integrity-conversation-" + suffix,
            TaskId = "terminal-integrity-task-" + suffix,
            WorkspacePath = WorkspacePath,
            UserText = "Run the protected test operation.",
            TaskIntentText = "Run the protected test operation.",
            Mode = CopilotAgentMode.Code,
            SearchRootPaths = [WorkspacePath],
            WritableLocalRootPaths = [WorkspacePath],
        };
        request.RuntimeExecutionScope = CopilotExecutionScope.ForAgentRequest(
                request,
                runId: "terminal-integrity-run-" + suffix)
            .WithRuntimeSnapshot("workspace-snapshot-" + suffix, capabilityRevision: 11);
        var tool = new ProtectedTestTool();
        var input = new CopilotAgentToolInput
        {
            Query = suffix,
        };
        var callId = "terminal-integrity-call-" + suffix;
        var signature = CopilotAgentToolInputExactBinding.CreateExecutionSignature(
            tool.Name,
            input);
        var executionScope = request.RuntimeExecutionScope.BindToolCall(
            tool.Name,
            callId,
            signature);
        var events = new List<CopilotAgentEvent>();
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(
            request,
            request.RuntimeExecutionScope,
            [tool],
            maxToolCalls: 4,
            new CopilotToolExecutor(hooks),
            coordinator,
            events.Add,
            capabilityRevisionProvider: () => 11);
        var reservation = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.FrameworkApprovalReservation
        {
            CallId = callId,
            Round = 1,
            Attempt = 1,
            MaxAttempts = 1,
            Signature = signature,
            ProviderCallId = callId,
            Tool = tool,
            ToolInput = input,
            ExecutionScope = executionScope,
            StartedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-1),
        };
        var handle = coordinator.RequestApproval(
            tool,
            request,
            input,
            callId,
            CancellationToken.None,
            executionScope);
        return new ApprovalScenario(
            request,
            bridge,
            reservation,
            coordinator,
            handle,
            events);
    }

    private static void AssertTerminalApprovalEvents(ApprovalScenario scenario)
    {
        var toolEvents = scenario.Events
            .Where(item => item.Type == CopilotAgentEventType.ToolResult)
            .ToArray();
        Assert.Collection(
            toolEvents,
            item =>
            {
                Assert.Equal(
                    CopilotToolExecutionState.AwaitingApproval,
                    Assert.IsType<CopilotToolExecutionInfo>(item.ToolExecution).State);
                Assert.True(Assert.IsType<CopilotToolResult>(item.ToolResult).Success);
            },
            item =>
            {
                Assert.Equal(
                    CopilotToolExecutionState.Cancelled,
                    Assert.IsType<CopilotToolExecutionInfo>(item.ToolExecution).State);
                var result = Assert.IsType<CopilotToolResult>(item.ToolResult);
                Assert.False(result.Success);
                Assert.Equal(CopilotToolFailureKind.Cancelled, result.FailureKind);
                Assert.Equal("approval_cancelled", result.FailureCode);
            });
        var step = Assert.Single(scenario.Bridge.StepRecords);
        Assert.Equal(CopilotToolExecutionState.Cancelled, step.Execution.State);
        Assert.Equal(CopilotToolFailureKind.Cancelled, step.Observation.FailureKind);
    }

    private static CopilotAgentTraceEntry CreateTrace(
        string callId,
        CopilotToolExecutionState state)
    {
        var startedAt = DateTimeOffset.UtcNow.AddSeconds(-2);
        return new CopilotAgentTraceEntry
        {
            CallId = callId,
            Round = 1,
            ToolName = "ProtectedTestTool",
            State = state,
            StartedAtUtc = startedAt,
            CompletedAtUtc = state == CopilotToolExecutionState.Completed
                ? startedAt.AddSeconds(1)
                : null,
        };
    }

    private static void AssertInterruptedMissingTerminalResult(
        CopilotAgentTraceEntry trace)
    {
        Assert.Equal(CopilotToolExecutionState.Interrupted, trace.State);
        Assert.Equal(CopilotToolFailureKind.Internal, trace.FailureKind);
        Assert.Equal("tool_terminal_event_missing", trace.FailureCode);
        Assert.NotNull(trace.CompletedAtUtc);
    }

    private sealed record ApprovalScenario(
        CopilotAgentRequest Request,
        CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge Bridge,
        CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge.FrameworkApprovalReservation Reservation,
        CopilotFrameworkApprovalCoordinator Coordinator,
        CopilotFrameworkApprovalHandle Handle,
        List<CopilotAgentEvent> Events);

    private sealed class ProtectedTestTool : ICopilotFrameworkApprovedTool
    {
        public string Name => "ProtectedTestTool";

        public string Description => "Runs a protected test operation.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ProtectedWrite(
                CopilotToolIdempotency.NonIdempotent);

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The protected test tool requires approval.");

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
            });
    }

    private sealed class PromptPermissionHook : ICopilotToolPermissionRequestHook
    {
        public Task<CopilotToolPermissionRequestDecision> OnPermissionRequestAsync(
            CopilotToolPermissionRequestContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CopilotToolPermissionRequestDecision.Prompt);
        }

        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(
            CopilotToolExecutionHookContext context,
            CancellationToken cancellationToken) =>
            Task.FromResult(CopilotToolExecutionHookDecision.Proceed);

        public Task AfterExecuteAsync(
            CopilotToolExecutionOutcome outcome,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
