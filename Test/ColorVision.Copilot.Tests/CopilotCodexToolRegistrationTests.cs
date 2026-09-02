using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexToolRegistrationTests
{
    [Fact]
    public void DisabledValuesRemoveFrameworkToolsAndTheirPromptInstructions()
    {
        var enabledRequest = CreatePlanRequest();
        var disabledRequest = CreatePlanRequest(
            requestUserInputEnabled: false,
            updatePlanEnabled: false);
        var environment = CopilotAgentEnvironmentContext.Capture(enabledRequest);

        var enabledToolNames = CopilotMicrosoftAgentFrameworkRuntime.BuildCheckpointToolNames(
            enabledRequest,
            ["ReadLocalFile"]);
        var disabledToolNames = CopilotMicrosoftAgentFrameworkRuntime.BuildCheckpointToolNames(
            disabledRequest,
            ["ReadLocalFile"]);
        string enabledPrompt = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            enabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: CopilotMicrosoftAgentFrameworkRuntime.IsUpdatePlanToolEnabled(enabledRequest),
            agentModeEnabled: true);
        string disabledPrompt = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            disabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: CopilotMicrosoftAgentFrameworkRuntime.IsUpdatePlanToolEnabled(disabledRequest),
            agentModeEnabled: false);

        Assert.True(CopilotMicrosoftAgentFrameworkRuntime.IsRequestUserInputToolEnabled(enabledRequest));
        Assert.True(CopilotMicrosoftAgentFrameworkRuntime.IsUpdatePlanToolEnabled(enabledRequest));
        Assert.Contains("AskUserQuestion", enabledToolNames);
        Assert.Contains("update_plan", enabledToolNames);
        Assert.Contains("AskUserQuestion is a structured clarification pause", enabledPrompt, StringComparison.Ordinal);
        Assert.Contains("Use one concise outcome-oriented todo list", enabledPrompt, StringComparison.Ordinal);
        Assert.False(CopilotMicrosoftAgentFrameworkRuntime.IsRequestUserInputToolEnabled(disabledRequest));
        Assert.False(CopilotMicrosoftAgentFrameworkRuntime.IsTaskLedgerAvailable(disabledRequest));
        Assert.False(CopilotMicrosoftAgentFrameworkRuntime.IsUpdatePlanToolEnabled(disabledRequest));
        Assert.DoesNotContain("AskUserQuestion", disabledToolNames);
        Assert.DoesNotContain("update_plan", disabledToolNames);
        Assert.DoesNotContain("AskUserQuestion is a structured clarification pause", disabledPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Use one concise outcome-oriented todo list", disabledPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void RemovingAFrameworkToolInvalidatesAContextBearingCheckpoint()
    {
        var request = CreatePlanRequest();
        var profile = request.Profile;
        var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            capabilitySnapshot,
            availableToolNames: CopilotMicrosoftAgentFrameworkRuntime.BuildCheckpointToolNames(
                request,
                ["ReadLocalFile"]));
        var disabledRequest = CreatePlanRequest(
            requestUserInputEnabled: false,
            updatePlanEnabled: false);

        var compatibility = checkpoint!.EvaluateFor(
            profile,
            capabilitySnapshot,
            availableToolNames: CopilotMicrosoftAgentFrameworkRuntime.BuildCheckpointToolNames(
                disabledRequest,
                ["ReadLocalFile"]));

        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.ToolSurfaceDrift, compatibility.Kind);
        Assert.Contains("AskUserQuestion", compatibility.RemovedToolNames);
        Assert.Contains("update_plan", compatibility.RemovedToolNames);
        Assert.True(compatibility.RequiresReplan);
    }

    private static CopilotAgentRequest CreatePlanRequest(
        bool requestUserInputEnabled = true,
        bool updatePlanEnabled = true) => new()
    {
        Profile = CopilotProfileConfig.CreateDefault(),
        UserText = "Plan the requested implementation.",
        Mode = CopilotAgentMode.Plan,
        CodexExperimentalRequestUserInputEnabled = requestUserInputEnabled,
        CodexUpdatePlanEnabled = updatePlanEnabled,
    };

}
