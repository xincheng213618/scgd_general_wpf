using System.Text.Json;
using ColorVision.Copilot;
using Microsoft.Extensions.AI;

namespace ColorVision.UI.Tests;

public sealed class CopilotRepeatableObservationTests
{
    private const string FirstSignature =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SecondSignature =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task ProgressingObservationCanRepeatUntilItsSignatureStagnates()
    {
        var tool = new StubObservationTool(
            CreateObservation(FirstSignature),
            CreateObservation(SecondSignature),
            CreateObservation(SecondSignature));
        var bridge = CreateBridge(tool);
        var function = Assert.IsAssignableFrom<AIFunction>(
            Assert.Single(bridge.CreateFunctions()));

        using var first = await InvokeAsync(function);
        using var second = await InvokeAsync(function);
        using var stagnant = await InvokeAsync(function);
        using var rejected = await InvokeAsync(function);

        Assert.True(ReadRetryAllowed(first));
        Assert.True(ReadRetryAllowed(second));
        Assert.False(ReadRetryAllowed(stagnant));
        Assert.False(
            rejected.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(
            "conflict",
            rejected.RootElement.GetProperty("failure_kind").GetString());
        Assert.Equal(3, tool.ExecutionCount);
        Assert.Collection(
            bridge.StepRecords,
            step => Assert.True(step.Execution.RetryEligible),
            step => Assert.True(step.Execution.RetryEligible),
            step => Assert.False(step.Execution.RetryEligible),
            step =>
            {
                Assert.Equal(
                    CopilotToolExecutionState.Failed,
                    step.Execution.State);
                Assert.Equal(
                    CopilotToolFailureKind.Conflict,
                    step.Execution.FailureKind);
            });
    }

    [Fact]
    public async Task InvalidProgressSignatureCannotBypassTheRepeatGuard()
    {
        var tool = new StubObservationTool(
            CreateObservation("not-a-valid-signature"));
        var bridge = CreateBridge(tool);
        var function = Assert.IsAssignableFrom<AIFunction>(
            Assert.Single(bridge.CreateFunctions()));

        using var first = await InvokeAsync(function);
        using var rejected = await InvokeAsync(function);

        Assert.False(ReadRetryAllowed(first));
        Assert.False(
            rejected.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal(1, tool.ExecutionCount);
    }

    private static CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge
        CreateBridge(StubObservationTool tool)
    {
        var request = new CopilotAgentRequest
        {
            ConversationId = "repeatable-observation-conversation",
            TaskId = "repeatable-observation-task",
            Mode = CopilotAgentMode.Auto,
            UserText = "wait for the background observation",
        };
        request.RuntimeExecutionScope = CopilotExecutionScope.ForAgentRequest(
            request,
            runId: "repeatable-observation-run");
        return new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(
            request,
            request.RuntimeExecutionScope,
            [tool],
            maxToolCalls: 8,
            new CopilotToolExecutor(),
            new CopilotFrameworkApprovalCoordinator(),
            _ => { },
            capabilityRevisionProvider: () => 1);
    }

    private static CopilotToolResult CreateObservation(string signature) =>
        new()
        {
            ToolName = StubObservationTool.ToolName,
            Success = true,
            Summary = "The observed task is still running.",
            ObservationCanRepeat = true,
            ObservationProgressSignature = signature,
        };

    private static async Task<JsonDocument> InvokeAsync(AIFunction function)
    {
        var result = await function.InvokeAsync(
                new AIFunctionArguments(),
                CancellationToken.None)
            .AsTask();
        return JsonDocument.Parse(Assert.IsType<string>(result));
    }

    private static bool ReadRetryAllowed(JsonDocument document) =>
        document.RootElement.GetProperty("retry_allowed").GetBoolean();

    private sealed class StubObservationTool(
        params CopilotToolResult[] results) :
        ICopilotRepeatableObservationTool
    {
        public const string ToolName = "ObserveChangingState";
        private readonly Queue<CopilotToolResult> _results = new(results);

        public int ExecutionCount { get; private set; }

        public string Name => ToolName;

        public string Description =>
            "Returns one bounded read-only observation.";

        public int MaximumObservationAttempts => 4;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExecutionCount++;
            return Task.FromResult(_results.Dequeue());
        }
    }
}
