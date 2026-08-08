using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class CopilotCodexReasoningOptionsTests
{
    [Fact]
    public void ClosestTrustedReasoningLayerIsFrozenIntoTheAgentRequest()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                model_reasoning_effort = "low"
                model_reasoning_summary = "auto"
                model_supports_reasoning_summaries = true

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(
                configPath,
                "model_reasoning_effort = \"xhigh\"\nmodel_reasoning_summary = \"concise\"\nmodel_supports_reasoning_summaries = false");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(
                configPath,
                "model_reasoning_effort = \"medium\"\nmodel_reasoning_summary = \"detailed\"\nmodel_supports_reasoning_summaries = true");
            var plan = CopilotAgentRequestFactory.Prepare(
                "Inspect the workspace.",
                CopilotAgentMode.Auto,
                submittedContext);
            var request = CopilotAgentRequestFactory.Create(
                plan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CreateOfficialOpenAiProfile(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(
                globalRoot,
                projectRoot);
            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;

            Assert.Equal(CopilotCodexReasoningEffort.XHigh, submitted.ConfiguredModelReasoningEffort);
            Assert.Equal(CopilotCodexReasoningSummary.Concise, submitted.ConfiguredModelReasoningSummary);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submitted.ModelReasoningEffortSource);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submitted.ModelReasoningSummarySource);
            Assert.False(submitted.ConfiguredModelSupportsReasoningSummaries);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submitted.ModelSupportsReasoningSummariesSource);
            Assert.Equal(CopilotCodexReasoningEffort.XHigh, plan.CodexReasoningEffort);
            Assert.Equal(CopilotCodexReasoningSummary.Concise, plan.CodexReasoningSummary);
            Assert.False(plan.CodexModelSupportsReasoningSummaries);
            Assert.Equal(CopilotCodexReasoningEffort.XHigh, request.CodexReasoningEffort);
            Assert.Equal(CopilotCodexReasoningSummary.Concise, request.CodexReasoningSummary);
            Assert.False(request.CodexModelSupportsReasoningSummaries);
            Assert.Equal(CopilotCodexReasoningEffort.Medium, refreshed.ConfiguredModelReasoningEffort);
            Assert.Equal(CopilotCodexReasoningSummary.Detailed, refreshed.ConfiguredModelReasoningSummary);
            Assert.True(refreshed.ConfiguredModelSupportsReasoningSummaries);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void PlanModeReasoningEffortOverridesOnlyPlanRequestsAndUsesTheSubmittedSnapshot()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                model_reasoning_effort = "low"
                plan_mode_reasoning_effort = "minimal"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(configPath, "plan_mode_reasoning_effort = \"none\"");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(configPath, "plan_mode_reasoning_effort = \"xhigh\"");

            var planModePlan = CopilotAgentRequestFactory.Prepare(
                "Plan the workspace change.",
                CopilotAgentMode.Plan,
                submittedContext);
            var autoModePlan = CopilotAgentRequestFactory.Prepare(
                "Inspect the workspace.",
                CopilotAgentMode.Auto,
                submittedContext);
            var planModeRequest = CopilotAgentRequestFactory.Create(
                planModePlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = CreateOfficialOpenAiProfile(),
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;

            Assert.Equal(CopilotCodexReasoningEffort.Low, submitted.ConfiguredModelReasoningEffort);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, submitted.ModelReasoningEffortSource);
            Assert.Equal(CopilotCodexReasoningEffort.None, submitted.ConfiguredPlanModeReasoningEffort);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submitted.PlanModeReasoningEffortSource);
            Assert.Equal(CopilotCodexReasoningEffort.None, planModePlan.CodexReasoningEffort);
            Assert.Equal(CopilotCodexReasoningEffort.None, planModeRequest.CodexReasoningEffort);
            Assert.Equal(CopilotCodexReasoningEffort.Low, autoModePlan.CodexReasoningEffort);
            Assert.Equal(CopilotCodexReasoningEffort.XHigh, refreshed.ConfiguredPlanModeReasoningEffort);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

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
                "model_reasoning_effort = \"max\"\nplan_mode_reasoning_effort = \"max\"\nmodel_reasoning_summary = \"brief\"\nmodel_supports_reasoning_summaries = \"yes\"");
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

    private static CopilotProfileConfig CreateOfficialOpenAiProfile()
    {
        return new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.OpenAI,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-5.5",
            MaxTokens = 4_096,
        };
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-reasoning-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
