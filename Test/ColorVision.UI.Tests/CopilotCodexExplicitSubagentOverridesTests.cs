using ColorVision.Copilot;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexExplicitSubagentOverridesTests
{
    [Fact]
    public async Task ExplicitOverridesReachTheRunnerAndWinOverConfiguredDefaults()
    {
        var runner = new RecordingSubagentRunner();
        var tool = new CopilotDelegateExploreTool(runner);
        var request = CreateParentRequest(
            defaultModel: "configured-child-model",
            defaultEffort: CopilotCodexReasoningEffort.Low);
        var arguments = new Dictionary<string, object?>
        {
            ["task"] = "Inspect the bounded workspace evidence.",
            ["model"] = "explicit-child-model",
            ["reasoning_effort"] = "ultra",
        };
        Assert.True(
            tool.InputSchema.TryBind(arguments, out var input, out var bindError),
            bindError);
        var progress = new CopilotToolProgressContext();

        var result = await tool.ExecuteWithProgressAsync(
            request,
            input,
            progress,
            CancellationToken.None);

        Assert.Equal(1, runner.RunCount);
        var runRequest = Assert.IsType<CopilotSubagentRunRequest>(runner.LastRunRequest);
        Assert.Equal("explicit-child-model", runRequest.Model);
        Assert.Equal("ultra", runRequest.ReasoningEffort);
        var childRequest = CopilotSubagentRunner.CreateChildRequest(
            request,
            CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId),
            runRequest);
        Assert.Equal("explicit-child-model", childRequest.Profile.Model);
        Assert.Equal(CopilotCodexReasoningEffort.Ultra, childRequest.CodexReasoningEffort);
        Assert.Equal("parent-model", request.Profile.Model);
        Assert.Equal("configured-child-model", request.CodexDefaultSubagentModel);
        Assert.Equal(CopilotCodexReasoningEffort.Low, request.CodexDefaultSubagentReasoningEffort);

        var delegated = Assert.IsType<CopilotDelegatedRunUsage>(result.DelegatedRunUsage);
        Assert.Equal("explicit-child-model", delegated.Model);
        Assert.Equal("ultra", delegated.ReasoningEffort);
        Assert.Contains("model: explicit-child-model", result.Content, StringComparison.Ordinal);
        Assert.Contains("reasoning_effort: ultra", result.Content, StringComparison.Ordinal);
        Assert.Equal("explicit-child-model", progress.LatestSnapshot?.DelegatedRun?.Model);
        Assert.Equal("ultra", progress.LatestSnapshot?.DelegatedRun?.ReasoningEffort);

        var outcome = CreateOutcome(tool, request, result);
        using var formatted = JsonDocument.Parse(CopilotFrameworkToolResultFormatter.Format(outcome));
        var formattedRun = formatted.RootElement.GetProperty("delegated_run");
        Assert.Equal("explicit-child-model", formattedRun.GetProperty("model").GetString());
        Assert.Equal("ultra", formattedRun.GetProperty("reasoning_effort").GetString());
        var trace = CopilotAgentTraceEntry.FromResult(outcome.Execution, result);
        Assert.Equal(CopilotAgentTraceEntry.CurrentSchemaVersion, trace.SchemaVersion);
        Assert.Equal("explicit-child-model", trace.DelegatedModel);
        Assert.Equal("ultra", trace.DelegatedReasoningEffort);
        Assert.Contains(
            "fields=model,reasoning_effort,task",
            CopilotToolExecutionAuditLogger.CreateArgumentSummary(tool, input),
            StringComparison.Ordinal);
    }

    [Fact]
    public void ReasoningFallbackUsesConfiguredDefaultThenNewModelDefaultThenParent()
    {
        var configuredParent = CreateParentRequest(
            defaultModel: string.Empty,
            defaultEffort: CopilotCodexReasoningEffort.Low);
        var configuredChild = CreateChildRequest(
            configuredParent,
            model: "different-model",
            reasoningEffort: string.Empty);
        Assert.Equal(CopilotCodexReasoningEffort.Low, configuredChild.CodexReasoningEffort);

        var inheritedParent = CreateParentRequest(
            defaultModel: string.Empty,
            defaultEffort: CopilotCodexReasoningEffort.Unspecified);
        var changedModelChild = CreateChildRequest(
            inheritedParent,
            model: "different-model",
            reasoningEffort: string.Empty);
        Assert.Equal("different-model", changedModelChild.Profile.Model);
        Assert.Equal(CopilotCodexReasoningEffort.Unspecified, changedModelChild.CodexReasoningEffort);

        var sameModelChild = CreateChildRequest(
            inheritedParent,
            model: "parent-model",
            reasoningEffort: string.Empty);
        Assert.Equal(CopilotCodexReasoningEffort.Medium, sameModelChild.CodexReasoningEffort);
    }

    [Fact]
    public void ChangedModelDoesNotInheritParentModelResponseMetadata()
    {
        var parent = CreateParentRequest(
            defaultModel: string.Empty,
            defaultEffort: CopilotCodexReasoningEffort.Unspecified);

        var changedModelChild = CreateChildRequest(
            parent,
            model: "different-model",
            reasoningEffort: string.Empty);
        Assert.Equal(CopilotCodexReasoningSummary.Unspecified, changedModelChild.CodexReasoningSummary);
        Assert.Null(changedModelChild.CodexModelSupportsReasoningSummaries);
        Assert.Equal(CopilotCodexModelVerbosity.Unspecified, changedModelChild.CodexModelVerbosity);

        var sameModelChild = CreateChildRequest(
            parent,
            model: "parent-model",
            reasoningEffort: string.Empty);
        Assert.Equal(CopilotCodexReasoningSummary.Concise, sameModelChild.CodexReasoningSummary);
        Assert.True(sameModelChild.CodexModelSupportsReasoningSummaries);
        Assert.Equal(CopilotCodexModelVerbosity.Low, sameModelChild.CodexModelVerbosity);
    }

    [Fact]
    public async Task InvalidInjectedOverridesAreRejectedBeforeTheRunnerStarts()
    {
        var runner = new RecordingSubagentRunner();
        var tool = new CopilotDelegateScoutTool(runner);
        var request = CreateParentRequest(
            defaultModel: string.Empty,
            defaultEffort: CopilotCodexReasoningEffort.Unspecified);
        var invalidEffortArguments = new Dictionary<string, object?>
        {
            ["task"] = "Inspect the public evidence.",
            ["reasoning_effort"] = "extreme",
        };
        Assert.False(tool.InputSchema.TryBind(invalidEffortArguments, out _, out _));

        var invalidEffort = await tool.ExecuteAsync(
            request,
            new CopilotAgentToolInput { Arguments = invalidEffortArguments },
            CancellationToken.None);
        var invalidModel = await tool.ExecuteAsync(
            request,
            new CopilotAgentToolInput
            {
                Arguments = new Dictionary<string, object?>
                {
                    ["task"] = "Inspect the public evidence.",
                    ["model"] = new string('m', CopilotConfiguredModelSelection.MaximumModelCharacters + 1),
                },
            },
            CancellationToken.None);

        Assert.Equal(0, runner.RunCount);
        Assert.Equal(CopilotToolFailureKind.Validation, invalidEffort.FailureKind);
        Assert.Contains("reasoning_effort", invalidEffort.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal(CopilotToolFailureKind.Validation, invalidModel.FailureKind);
        Assert.Contains("model", invalidModel.ErrorMessage, StringComparison.Ordinal);
    }

    private static CopilotAgentRequest CreateParentRequest(
        string defaultModel,
        CopilotCodexReasoningEffort defaultEffort)
    {
        return new CopilotAgentRequest
        {
            ConversationId = "explicit-subagent-" + Guid.NewGuid().ToString("N"),
            UserText = "Delegate a bounded investigation.",
            TaskIntentText = "Delegate a bounded investigation.",
            Profile = new CopilotProfileConfig
            {
                VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "test-key",
                BaseUrl = "https://example.test/v1",
                Model = "parent-model",
                MaxTokens = 4_096,
            },
            CodexDefaultSubagentModel = defaultModel,
            CodexDefaultSubagentReasoningEffort = defaultEffort,
            CodexReasoningEffort = CopilotCodexReasoningEffort.Medium,
            CodexReasoningSummary = CopilotCodexReasoningSummary.Concise,
            CodexModelSupportsReasoningSummaries = true,
            CodexModelVerbosity = CopilotCodexModelVerbosity.Low,
        };
    }

    private static CopilotAgentRequest CreateChildRequest(
        CopilotAgentRequest parentRequest,
        string model,
        string reasoningEffort)
    {
        return CopilotSubagentRunner.CreateChildRequest(
            parentRequest,
            CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId),
            new CopilotSubagentRunRequest
            {
                RunId = "child-" + Guid.NewGuid().ToString("N"),
                Task = "Inspect the bounded workspace evidence.",
                Model = model,
                ReasoningEffort = reasoningEffort,
                RequestTokenBudget = 16_384,
            });
    }

    private static CopilotToolExecutionOutcome CreateOutcome(
        ICopilotTool tool,
        CopilotAgentRequest request,
        CopilotToolResult result)
    {
        return new CopilotToolExecutionOutcome
        {
            Invocation = new CopilotToolInvocation
            {
                CallId = "explicit-subagent-call",
                Round = 1,
                Tool = tool,
                AgentRequest = request,
                ToolCall = new CopilotToolCall { ToolName = tool.Name },
            },
            Result = result,
            Execution = new CopilotToolExecutionInfo
            {
                CallId = "explicit-subagent-call",
                Round = 1,
                ToolName = tool.Name,
                State = CopilotToolExecutionState.Failed,
                FailureKind = result.FailureKind,
            },
        };
    }

    private sealed class RecordingSubagentRunner : ICopilotSubagentRunner
    {
        public int RunCount { get; private set; }

        public CopilotSubagentRunRequest? LastRunRequest { get; private set; }

        public Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken)
        {
            RunCount++;
            LastRunRequest = runRequest;
            return Task.FromResult(new CopilotSubagentResult());
        }
    }
}
