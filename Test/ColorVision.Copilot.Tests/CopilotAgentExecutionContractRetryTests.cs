using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotAgentExecutionContractRetryTests
{
    [Theory]
    [InlineData(false, 1, 2, true)]
    [InlineData(false, 2, 2, false)]
    [InlineData(true, 1, 2, false)]
    public void RetryableFailureReinvokesOnlyBoundedReadOnlyTool(
        bool useWriteTool,
        int attempt,
        int maxAttempts,
        bool shouldReinvoke)
    {
        ICopilotTool tool = useWriteTool
            ? new CopilotSetThemeTool()
            : new CopilotSearchDocsTool();
        var contract = CopilotAgentExecutionContract.Create(
            new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Auto,
                UserText = "Collect the required evidence.",
                RequiredSuccessfulToolNames = [tool.Name],
            },
            [tool]);

        var evaluation = contract.Evaluate([Failure(tool, attempt, maxAttempts)]);

        Assert.False(evaluation.IsSatisfied);
        Assert.Equal(shouldReinvoke, evaluation.ShouldReinvoke);
    }

    private static CopilotAgentStepRecord Failure(
        ICopilotTool tool,
        int attempt,
        int maxAttempts)
    {
        var capability = tool.Capability;
        return new CopilotAgentStepRecord
        {
            Round = 1,
            ToolCall = new CopilotToolCall { ToolName = tool.Name },
            Observation = new CopilotToolObservation
            {
                Success = false,
                FailureKind = CopilotToolFailureKind.Transient,
            },
            Execution = new CopilotToolExecutionInfo
            {
                ToolName = tool.Name,
                Round = 1,
                Attempt = attempt,
                MaxAttempts = maxAttempts,
                Access = capability.Access,
                Idempotency = capability.Idempotency,
                State = CopilotToolExecutionState.Failed,
                FailureKind = CopilotToolFailureKind.Transient,
                RetryEligible = true,
                StartedAtUtc = DateTimeOffset.UtcNow,
            },
        };
    }
}
