namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexDefaultModeRequestUserInputTests
{
    [Fact]
    public void DefaultModesRequireTheFeatureWhilePlanModeDoesNot()
    {
        var planRequest = CreateRequest(CopilotAgentMode.Plan);
        var defaultRequest = CreateRequest(CopilotAgentMode.Code);
        var enabledDefaultRequest = CreateRequest(
            CopilotAgentMode.Code,
            defaultModeEnabled: true);
        var globallyDisabledDefaultRequest = CreateRequest(
            CopilotAgentMode.Auto,
            defaultModeEnabled: true,
            toolEnabled: false);

        Assert.True(CopilotMicrosoftAgentFrameworkRuntime.IsRequestUserInputToolEnabled(planRequest));
        Assert.False(CopilotMicrosoftAgentFrameworkRuntime.IsRequestUserInputToolEnabled(defaultRequest));
        Assert.True(CopilotMicrosoftAgentFrameworkRuntime.IsRequestUserInputToolEnabled(enabledDefaultRequest));
        Assert.False(CopilotMicrosoftAgentFrameworkRuntime.IsRequestUserInputToolEnabled(globallyDisabledDefaultRequest));
        Assert.Contains(
            "AskUserQuestion",
            CopilotMicrosoftAgentFrameworkRuntime.BuildCheckpointToolNames(
                enabledDefaultRequest,
                ["ReadLocalFile"]));
        Assert.DoesNotContain(
            "AskUserQuestion",
            CopilotMicrosoftAgentFrameworkRuntime.BuildCheckpointToolNames(
                defaultRequest,
                ["ReadLocalFile"]));
    }

    private static CopilotAgentRequest CreateRequest(
        CopilotAgentMode mode,
        bool defaultModeEnabled = false,
        bool toolEnabled = true) => new()
    {
        Profile = CopilotProfileConfig.CreateDefault(),
        UserText = "Complete the requested task.",
        Mode = mode,
        CodexExperimentalRequestUserInputEnabled = toolEnabled,
        CodexDefaultModeRequestUserInputEnabled = defaultModeEnabled,
    };

}
