using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexSkillMcpDependencyInstallFeatureTests
{
    [Fact]
    public void DiagnosticsExplainConfirmationAndDisabledBehavior()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredSkillMcpDependencyInstallEnabled = false,
            HasSkillMcpDependencyInstallEnabledOverride = true,
            SkillMcpDependencyInstallEnabledSource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        string instructions = CopilotProjectInstructionDiagnostics.Format(
            new CopilotProjectInstructionSnapshot(
                string.Empty,
                string.Empty,
                string.Empty,
                options,
                Array.Empty<CopilotProjectInstructionDocument>()),
            hasActiveAgentRun: false);
        string effective = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("features.skill_mcp_dependency_install：false", instructions, StringComparison.Ordinal);
        Assert.Contains("不提示或写入", instructions, StringComparison.Ordinal);
        Assert.Contains("features.skill_mcp_dependency_install：false", effective, StringComparison.Ordinal);
        Assert.Contains(options.SkillMcpDependencyInstallEnabledSourceLabel, effective, StringComparison.Ordinal);
        Assert.Contains("已有外部 MCP 配置保持有效", effective, StringComparison.Ordinal);
    }
}
