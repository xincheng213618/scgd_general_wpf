using System.IO;

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

    // Request text alone must not end the turn as Blocked; only an explicit signal enforces.
    [Theory]
    [InlineData(CopilotAgentMode.Auto, "帮我改一下这个配置", nameof(CopilotAgentExecutionRequirement.None), nameof(CopilotAgentExecutionRequirement.WorkspaceEdit))]
    [InlineData(CopilotAgentMode.Code, "帮我改一下这段代码", nameof(CopilotAgentExecutionRequirement.WorkspaceEdit), nameof(CopilotAgentExecutionRequirement.None))]
    [InlineData(CopilotAgentMode.Auto, "帮我改一下，参考 https://example.com/spec", nameof(CopilotAgentExecutionRequirement.DirectUrlEvidence), nameof(CopilotAgentExecutionRequirement.WorkspaceEdit))]
    public void RequestTextOnlyHintsInAutoWhileExplicitSignalsEnforce(
        CopilotAgentMode mode,
        string userText,
        string enforcedName,
        string hintedName)
    {
        var enforced = Enum.Parse<CopilotAgentExecutionRequirement>(enforcedName);
        var hinted = Enum.Parse<CopilotAgentExecutionRequirement>(hintedName);
        var root = Path.GetTempPath();
        var request = new CopilotAgentRequest
        {
            Mode = mode,
            UserText = userText,
            WritableLocalRootPaths = [root],
            SearchRootPaths = [root],
        };
        var contract = CopilotAgentExecutionContract.Create(
            request,
            new CopilotToolRegistry(CopilotToolRegistry.CreateDefaultTools()).FindTools(request));

        Assert.Equal(enforced, contract.Requirement);
        Assert.Equal(enforced != CopilotAgentExecutionRequirement.None, contract.IsRequired);
        Assert.Equal(hinted, contract.IntentHintRequirement);
        Assert.Equal(enforced == CopilotAgentExecutionRequirement.None, contract.Evaluate([]).IsSatisfied);
        Assert.Equal(hinted != CopilotAgentExecutionRequirement.None, contract.BuildInitialInstruction().Contains("not a requirement"));
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
