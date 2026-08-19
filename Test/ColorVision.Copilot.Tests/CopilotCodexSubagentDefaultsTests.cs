using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexSubagentDefaultsTests
{
    [Fact]
    public void ClosestTrustedDefaultsAreFrozenAndAppliedToTheActualChildRequest()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [agents]
                default_subagent_model = "home-child-model"
                default_subagent_reasoning_effort = "medium"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            string projectConfigPath = Path.Combine(projectConfigDirectory, "config.toml");
            File.WriteAllText(
                projectConfigPath,
                """
                model_reasoning_effort = "low"
                agents.default_subagent_model = "project-child-model"
                agents.default_subagent_reasoning_effort = "high"
                """);

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var submittedPlan = CopilotAgentRequestFactory.Prepare(
                "Delegate a bounded workspace investigation.",
                CopilotAgentMode.Auto,
                submittedContext);
            var parentProfile = new CopilotProfileConfig
            {
                VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "test-key",
                BaseUrl = "https://example.test/v1",
                Model = "parent-model",
                MaxTokens = 4_096,
            };
            var parentRequest = CopilotAgentRequestFactory.Create(
                submittedPlan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = parentProfile,
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            File.WriteAllText(
                projectConfigPath,
                "[agents]\ndefault_subagent_model = \"changed-model\"\ndefault_subagent_reasoning_effort = \"xhigh\"");

            var childRequest = CopilotSubagentRunner.CreateChildRequest(
                parentRequest,
                CopilotSubagentRoleCatalog.Default.GetRequired(CopilotSubagentRoleCatalog.ExploreRoleId),
                new CopilotSubagentRunRequest
                {
                    RunId = "child-run-1",
                    Task = "Inspect the bounded workspace evidence.",
                    RequestTokenBudget = 16_384,
                });

            Assert.Equal("project-child-model", submittedPlan.CodexDefaultSubagentModel);
            Assert.Equal(
                CopilotCodexReasoningEffort.High,
                submittedPlan.CodexDefaultSubagentReasoningEffort);
            Assert.Equal("project-child-model", parentRequest.CodexDefaultSubagentModel);
            Assert.Equal(CopilotCodexReasoningEffort.Low, parentRequest.CodexReasoningEffort);
            Assert.Equal("parent-model", parentProfile.Model);
            Assert.NotSame(parentProfile, childRequest.Profile);
            Assert.Equal("project-child-model", childRequest.Profile.Model);
            Assert.Equal(CopilotSubagentRunner.MaximumExplorationOutputTokens, childRequest.Profile.MaxTokens);
            Assert.Equal(CopilotCodexReasoningEffort.High, childRequest.CodexReasoningEffort);
            Assert.Equal(
                CopilotCodexReasoningEffort.High,
                childRequest.CodexDefaultSubagentReasoningEffort);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submittedContext.ProjectInstructionDiscoveryOptions.DefaultSubagentModelSource);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submittedContext.ProjectInstructionDiscoveryOptions.DefaultSubagentReasoningEffortSource);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void UntrustedAndInvalidDefaultsCannotOverrideOrCreateAContract()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                [agents]
                default_subagent_model = "home-child-model"
                default_subagent_reasoning_effort = "medium"

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string projectConfigDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(projectConfigDirectory);
            File.WriteAllText(
                Path.Combine(projectConfigDirectory, "config.toml"),
                "[agents]\ndefault_subagent_model = \"project-child-model\"\ndefault_subagent_reasoning_effort = \"xhigh\"");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal("home-child-model", untrusted.ConfiguredDefaultSubagentModel);
            Assert.Equal(
                CopilotCodexReasoningEffort.Medium,
                untrusted.ConfiguredDefaultSubagentReasoningEffort);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.DefaultSubagentModelSource);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.DefaultSubagentReasoningEffortSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                "[agents]\ndefault_subagent_model = \"\"\ndefault_subagent_reasoning_effort = \"none\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.False(invalid.HasDefaultSubagentModelOverride);
            Assert.False(invalid.HasDefaultSubagentReasoningEffortOverride);
            Assert.Equal(string.Empty, invalid.ConfiguredDefaultSubagentModel);
            Assert.Equal(
                CopilotCodexReasoningEffort.Unspecified,
                invalid.ConfiguredDefaultSubagentReasoningEffort);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

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

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-subagent-defaults-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
