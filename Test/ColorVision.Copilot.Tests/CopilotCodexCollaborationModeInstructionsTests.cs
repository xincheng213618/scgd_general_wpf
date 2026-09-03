using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexCollaborationModeInstructionsTests
{
    [Fact]
    public void DisabledSnapshotOmitsModeGuidanceWithoutChangingPlanRuntimePolicy()
    {
        var enabledRequest = CreatePlanRequest(includeCollaborationModeInstructions: true);
        var disabledRequest = CreatePlanRequest(includeCollaborationModeInstructions: false);
        var environment = new CopilotAgentEnvironmentContext();

        string enabledHarness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            enabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: true,
            agentModeEnabled: true);
        string disabledHarness = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            disabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: true,
            agentModeEnabled: true);
        var contextBuilder = new CopilotAgentContextBuilder();
        string enabledAnswer = contextBuilder.BuildPreparedUserMessageContent(
            enabledRequest,
            Array.Empty<CopilotToolResult>());
        string disabledAnswer = contextBuilder.BuildPreparedUserMessageContent(
            disabledRequest,
            Array.Empty<CopilotToolResult>());

        Assert.Contains("Operate in user-selected plan-only mode", enabledHarness, StringComparison.Ordinal);
        Assert.Contains("Use one concise outcome-oriented todo list", enabledHarness, StringComparison.Ordinal);
        Assert.Contains("This is a user-selected plan-only request", enabledHarness, StringComparison.Ordinal);
        Assert.DoesNotContain("Operate in user-selected plan-only mode", disabledHarness, StringComparison.Ordinal);
        Assert.DoesNotContain("Use one concise outcome-oriented todo list", disabledHarness, StringComparison.Ordinal);
        Assert.DoesNotContain("This is a user-selected plan-only request", disabledHarness, StringComparison.Ordinal);
        Assert.Contains("Operate in user-selected plan-only mode", enabledAnswer, StringComparison.Ordinal);
        Assert.DoesNotContain("Operate in user-selected plan-only mode", disabledAnswer, StringComparison.Ordinal);
        Assert.Contains("ColorVision Agent runtime", disabledHarness, StringComparison.Ordinal);

        Assert.True(CopilotMicrosoftAgentFrameworkRuntime.IsUpdatePlanToolEnabled(disabledRequest));
        Assert.Contains(
            "update_plan",
            CopilotMicrosoftAgentFrameworkRuntime.BuildCheckpointToolNames(
                disabledRequest,
                ["ReadLocalFile"]));
        Assert.False(CopilotToolRegistry.IsAllowedForMode(
            new CopilotSetThemeTool(),
            disabledRequest));
    }

    private static CopilotAgentRequest CreatePlanRequest(bool includeCollaborationModeInstructions) => new()
    {
        Profile = CopilotProfileConfig.CreateDefault(),
        UserText = "Plan the requested implementation.",
        Mode = CopilotAgentMode.Plan,
        HarnessFeatures = CopilotAgentHarnessFeatures.Full,
        CodexUpdatePlanEnabled = true,
        CodexIncludeCollaborationModeInstructions = includeCollaborationModeInstructions,
    };

}
