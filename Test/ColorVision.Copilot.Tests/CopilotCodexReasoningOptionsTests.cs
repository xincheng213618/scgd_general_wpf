using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexReasoningOptionsTests
{
    [Fact]
    public void UntrustedOrInvalidReasoningValuesCannotReplaceTheCodexHomeContract()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                model_reasoning_effort = "minimal"
                plan_mode_reasoning_effort = "minimal"
                model_reasoning_summary = "none"
                model_supports_reasoning_summaries = true

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(
                Path.Combine(configDirectory, "config.toml"),
                "model_reasoning_effort = \"xhigh\"\nplan_mode_reasoning_effort = \"none\"\nmodel_reasoning_summary = \"detailed\"\nmodel_supports_reasoning_summaries = false");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            Assert.Equal(CopilotCodexReasoningEffort.Minimal, untrusted.ConfiguredModelReasoningEffort);
            Assert.Equal(CopilotCodexReasoningEffort.Minimal, untrusted.ConfiguredPlanModeReasoningEffort);
            Assert.Equal(CopilotCodexReasoningSummary.None, untrusted.ConfiguredModelReasoningSummary);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.ModelReasoningEffortSource);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.PlanModeReasoningEffortSource);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.ModelReasoningSummarySource);
            Assert.True(untrusted.ConfiguredModelSupportsReasoningSummaries);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.ModelSupportsReasoningSummariesSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "model_reasoning_effort = \"extreme\"\nplan_mode_reasoning_effort = \"extreme\"\nmodel_reasoning_summary = \"brief\"\nmodel_supports_reasoning_summaries = \"yes\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            Assert.False(invalid.HasModelReasoningEffortOverride);
            Assert.False(invalid.HasPlanModeReasoningEffortOverride);
            Assert.False(invalid.HasModelReasoningSummaryOverride);
            Assert.False(invalid.HasModelSupportsReasoningSummariesOverride);
            Assert.Equal(CopilotCodexReasoningEffort.Unspecified, invalid.ConfiguredModelReasoningEffort);
            Assert.Equal(CopilotCodexReasoningEffort.Unspecified, invalid.ConfiguredPlanModeReasoningEffort);
            Assert.Equal(CopilotCodexReasoningSummary.Unspecified, invalid.ConfiguredModelReasoningSummary);
            Assert.False(CopilotCodexReasoningEffortSelection.TryParse("none", out _));
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ReasoningDiagnosticsExposeValuesSourcesAndResponsesScope()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredModelReasoningEffort = CopilotCodexReasoningEffort.XHigh,
            HasModelReasoningEffortOverride = true,
            ModelReasoningEffortSource = CopilotProjectInstructionConfigSources.CodexHome,
            ConfiguredPlanModeReasoningEffort = CopilotCodexReasoningEffort.None,
            HasPlanModeReasoningEffortOverride = true,
            PlanModeReasoningEffortSource = CopilotProjectInstructionConfigSources.TrustedProject,
            ConfiguredModelReasoningSummary = CopilotCodexReasoningSummary.Concise,
            HasModelReasoningSummaryOverride = true,
            ModelReasoningSummarySource = CopilotProjectInstructionConfigSources.CodexHome,
            ConfiguredModelSupportsReasoningSummaries = false,
            HasModelSupportsReasoningSummariesOverride = true,
            ModelSupportsReasoningSummariesSource = CopilotProjectInstructionConfigSources.CodexHome,
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
            CodexReasoningEffort = options.ConfiguredModelReasoningEffort,
            HasCodexReasoningEffortOverride = true,
            CodexReasoningEffortSourceLabel = options.ModelReasoningEffortSourceLabel,
            CodexReasoningSummary = options.ConfiguredModelReasoningSummary,
            HasCodexReasoningSummaryOverride = true,
            CodexReasoningSummarySourceLabel = options.ModelReasoningSummarySourceLabel,
            CodexModelSupportsReasoningSummaries = options.ConfiguredModelSupportsReasoningSummaries,
            HasCodexModelSupportsReasoningSummariesOverride = true,
            CodexModelSupportsReasoningSummariesSourceLabel = options.ModelSupportsReasoningSummariesSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex model_reasoning_effort：xhigh", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Codex plan_mode_reasoning_effort：none", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_reasoning_summary：concise", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_supports_reasoning_summaries：false", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.ModelReasoningEffortSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.PlanModeReasoningEffortSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("推理强度：xhigh", contextReport, StringComparison.Ordinal);
        Assert.Contains("推理摘要：concise", contextReport, StringComparison.Ordinal);
        Assert.Contains("推理元数据能力：false", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_reasoning_effort：xhigh", debugReport, StringComparison.Ordinal);
        Assert.Contains("Codex plan_mode_reasoning_effort：none", debugReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_reasoning_summary：concise", debugReport, StringComparison.Ordinal);
        Assert.Contains("Codex model_supports_reasoning_summaries：false", debugReport, StringComparison.Ordinal);
        Assert.Contains("阻断", memoryReport, StringComparison.Ordinal);
        Assert.Contains("阻断", contextReport, StringComparison.Ordinal);
        Assert.Contains("阻断", debugReport, StringComparison.Ordinal);
        Assert.Contains("仅 Agent 官方 OpenAI Responses 生效", memoryReport, StringComparison.Ordinal);
        Assert.Contains("仅 Agent 官方 OpenAI Responses 生效", contextReport, StringComparison.Ordinal);
        Assert.Contains("仅 Agent 官方 OpenAI Responses 生效", debugReport, StringComparison.Ordinal);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-reasoning-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
