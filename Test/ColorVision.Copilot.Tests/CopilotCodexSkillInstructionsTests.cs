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

    [Fact]
    public void DiagnosticsExplainAutomaticAndExplicitSkillBoundaries()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredIncludeSkillInstructions = false,
            HasIncludeSkillInstructionsOverride = true,
            IncludeSkillInstructionsSource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string memoryReport = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string contextReport = CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
        {
            ProfileLabel = "Profile",
            Mode = CopilotAgentMode.Code,
            CodexIncludeSkillInstructions = false,
            HasCodexIncludeSkillInstructionsOverride = true,
            CodexIncludeSkillInstructionsSourceLabel = options.IncludeSkillInstructionsSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex skills.include_instructions：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.IncludeSkillInstructionsSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("显式 $name 或 /name", memoryReport, StringComparison.Ordinal);
        Assert.Contains("自动 Skill 说明：省略", contextReport, StringComparison.Ordinal);
        Assert.Contains("仅显式 $name 或 /name", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex skills.include_instructions：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("显式 $name 或 /name", debugReport, StringComparison.Ordinal);
    }
}
