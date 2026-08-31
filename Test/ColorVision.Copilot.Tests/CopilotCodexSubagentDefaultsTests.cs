using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexSubagentDefaultsTests
{
    [Fact]
    public void DiagnosticsExposeDefaultSubagentValuesAndSources()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredDefaultSubagentModel = "gpt-5.6-terra",
            HasDefaultSubagentModelOverride = true,
            DefaultSubagentModelSource = CopilotProjectInstructionConfigSources.TrustedProject,
            ConfiguredDefaultSubagentReasoningEffort = CopilotCodexReasoningEffort.High,
            HasDefaultSubagentReasoningEffortOverride = true,
            DefaultSubagentReasoningEffortSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            CodexDefaultSubagentModel = options.ConfiguredDefaultSubagentModel,
            HasCodexDefaultSubagentModelOverride = true,
            CodexDefaultSubagentModelSourceLabel = options.DefaultSubagentModelSourceLabel,
            CodexDefaultSubagentReasoningEffort = options.ConfiguredDefaultSubagentReasoningEffort,
            HasCodexDefaultSubagentReasoningEffortOverride = true,
            CodexDefaultSubagentReasoningEffortSourceLabel = options.DefaultSubagentReasoningEffortSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                ComposerMode = CopilotAgentMode.Code,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex agents.default_subagent_model：gpt-5.6-terra", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.DefaultSubagentModelSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("Codex agents.default_subagent_reasoning_effort：high", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.DefaultSubagentReasoningEffortSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("子代理默认模型：gpt-5.6-terra", contextReport, StringComparison.Ordinal);
        Assert.Contains("子代理默认推理强度：high", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex agents.default_subagent_model：gpt-5.6-terra", debugReport, StringComparison.Ordinal);
        Assert.Contains("Codex agents.default_subagent_reasoning_effort：high", debugReport, StringComparison.Ordinal);
    }
}
