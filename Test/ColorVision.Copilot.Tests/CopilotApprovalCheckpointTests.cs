using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotApprovalCheckpointTests
{
    [Fact]
    public async Task AwaitingApprovalIsCancelledWhenRequiredCheckpointIsRejected()
    {
        var request = new CopilotAgentRequest
        {
            ConversationId = "approval-checkpoint-conversation",
            TaskId = CopilotAgentTaskEventIds.CreateRunId(),
            WorkspacePath = Path.GetTempPath(),
            UserText = "Run one protected test action.",
            TaskIntentText = "Run one protected test action.",
            Mode = CopilotAgentMode.Auto,
        };
        var tool = new CheckpointProtectedTool();
        var input = CopilotAgentToolInput.Empty;
        var callId = "approval-checkpoint-call";
        var signature = CopilotAgentToolInputExactBinding.CreateExecutionSignature(
            tool.Name,
            input);
        var executionScope = CopilotExecutionScope
            .ForAgentRequest(request)
            .BindToolCall(tool.Name, callId, signature);
        var coordinator = new CopilotFrameworkApprovalCoordinator();
        var events = new List<CopilotAgentEvent>();
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(
            request,
            executionScope,
            [tool],
            1,
            new CopilotToolExecutor(),
            coordinator,
            events.Add,
            capabilityRevisionProvider: () => 1);
        var handle = coordinator.RequestApproval(
            tool,
            request,
            input,
            callId,
            CancellationToken.None,
            executionScope);
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
            StartedAtUtc = DateTimeOffset.UtcNow,
        };
        var eventCountAtCheckpoint = 0;
        bridge.AttachInteractionCheckpointPublisher(_ =>
        {
            eventCountAtCheckpoint = events.Count;
            return ValueTask.FromResult(false);
        });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await bridge.PublishAwaitingApprovalAsync(
                reservation,
                handle.Action,
                automaticReview: false,
                CancellationToken.None));
        var decision = await handle.Decision.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Contains("could not be checkpointed", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, eventCountAtCheckpoint);
        Assert.Equal(CopilotFrameworkApprovalDecisionKind.Cancelled, decision.Kind);
        Assert.Collection(
            events,
            requested =>
            {
                Assert.Equal(CopilotAgentEventType.ToolResult, requested.Type);
                Assert.Equal(CopilotToolExecutionState.AwaitingApproval, requested.ToolExecution!.State);
                Assert.Equal(handle.Action.ActionId, requested.ToolResult!.Approval!.ActionId);
            },
            cancelled =>
            {
                Assert.Equal(CopilotAgentEventType.ToolResult, cancelled.Type);
                Assert.Equal(CopilotToolExecutionState.Cancelled, cancelled.ToolExecution!.State);
                Assert.Equal(handle.Action.ActionId, cancelled.ToolExecution.ApprovalActionId);
            });
    }

    private sealed class CheckpointProtectedTool : ICopilotTool
    {
        public string Name => "CheckpointProtectedTool";

        public string Description => "A protected tool used to verify approval checkpoint ordering.";

        public CopilotToolCapabilityDescriptor Capability { get; } = new()
        {
            Access = CopilotToolAccess.Write,
            RiskLevel = CopilotToolRiskLevel.High,
            ApprovalMode = CopilotToolApprovalMode.Always,
            Idempotency = CopilotToolIdempotency.NonIdempotent,
            ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
        };

        public CopilotToolInputSchema InputSchema => CopilotToolInputSchema.Empty;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) => throw new InvalidOperationException(
                "The protected tool must not execute when its approval checkpoint is rejected.");
    }
}
