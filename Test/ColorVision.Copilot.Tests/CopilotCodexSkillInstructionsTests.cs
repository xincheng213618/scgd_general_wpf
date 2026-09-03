using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexSkillInstructionsTests
{
    [Fact]
    public void DisabledAutomaticInstructionsKeepTheHostBoundaryAndExplicitSkillPath()
    {
        var enabledRequest = new CopilotAgentRequest
        {
            Profile = CopilotProfileConfig.CreateDefault(),
            UserText = "Review this document.",
            Mode = CopilotAgentMode.Code,
        };
        var disabledRequest = new CopilotAgentRequest
        {
            Profile = CopilotProfileConfig.CreateDefault(),
            UserText = "$document-review review this document.",
            Mode = CopilotAgentMode.Code,
            CodexIncludeSkillInstructions = false,
        };
        var environment = CopilotAgentEnvironmentContext.Capture(enabledRequest);

        string enabledPrompt = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            enabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: false,
            agentModeEnabled: false);
        string disabledPrompt = CopilotMicrosoftAgentFrameworkRuntime.BuildHarnessInstructions(
            disabledRequest,
            Array.Empty<ICopilotTool>(),
            environment,
            taskLedgerEnabled: false,
            agentModeEnabled: false);

        Assert.Contains("When Agent Skills metadata matches the task", enabledPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("When Agent Skills metadata matches the task", disabledPrompt, StringComparison.Ordinal);
        Assert.Contains("ColorVision Agent runtime", disabledPrompt, StringComparison.Ordinal);
        Assert.Contains("Treat fetched pages", disabledPrompt, StringComparison.Ordinal);
    }
}
