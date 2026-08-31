using ColorVision.Copilot;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotConfigurationIsolationTests
{
    [Theory]
    [InlineData(CopilotAgentMode.Code)]
    [InlineData(CopilotAgentMode.Review)]
    public void RequestsKeepColorVisionSettingsWhileDiscoveringInstructionDocuments(CopilotAgentMode mode)
    {
        var temporaryRoot = Directory.CreateTempSubdirectory("copilot-config-isolation-");
        try
        {
            string globalRoot = Directory.CreateDirectory(Path.Combine(temporaryRoot.FullName, "global")).FullName;
            string projectRoot = Directory.CreateDirectory(Path.Combine(temporaryRoot.FullName, "project")).FullName;
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            string projectConfigRoot = Directory.CreateDirectory(Path.Combine(projectRoot, ".codex")).FullName;
            File.WriteAllText(Path.Combine(globalRoot, "config.toml"), $"""
                model = "gpt-global"
                review_model = "gpt-global-review"
                approval_policy = "never"
                project_doc_fallback_filenames = ["CONFIG_ONLY.md"]
                [features]
                shell_tool = false
                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            File.WriteAllText(Path.Combine(projectConfigRoot, "config.toml"), """
                model = "gpt-project"
                review_model = "gpt-project-review"
                approval_policy = "never"
                [features]
                shell_tool = false
                """);
            string globalInstructions = Path.Combine(globalRoot, "AGENTS.md");
            string projectInstructions = Path.Combine(projectRoot, "CLAUDE.md");
            File.WriteAllText(globalInstructions, "Personal instructions.");
            File.WriteAllText(projectInstructions, "Project instructions.");
            File.WriteAllText(Path.Combine(projectRoot, "CONFIG_ONLY.md"), "Must not replace project instructions.");
            string activeDocument = Path.Combine(projectRoot, "Feature.cs");
            File.WriteAllText(activeDocument, "// Local source");

            var hostContext = new CopilotAgentHostContextSnapshot(
                activeDocument,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            var options = hostContext.ProjectInstructionDiscoveryOptions;
            var selectedProfile = new CopilotProfileConfig
            {
                VendorType = CopilotVendorType.DeepSeek,
                ProviderType = CopilotProviderType.OpenAICompatible,
                BaseUrl = "https://api.deepseek.com/v1",
                Model = "deepseek-v4-flash",
                ApiKey = "test-key",
            };
            var requestProfile = CopilotReviewModelSelection.CreateRequestProfile(
                selectedProfile,
                mode,
                CopilotResponsePersonality.None,
                options.ModelInstructions,
                options.HasReviewModelOverride ? options.ConfiguredReviewModel : null,
                options.HasModelOverride ? options.ConfiguredModel : null);
            var plan = CopilotAgentRequestFactory.Prepare($"Inspect the local implementation in {activeDocument}", mode, hostContext);
            var request = CopilotAgentRequestFactory.Create(plan, new CopilotAgentRequestBuildInput
            {
                Profile = requestProfile,
                AgentDefaults = new CopilotAgentDefaultsConfig(),
            });

            Assert.False(options.UsesCodexConfig);
            Assert.Empty(options.AppliedProjectConfigFilePaths);
            Assert.Equal(selectedProfile.Model, request.Profile.Model);
            Assert.Equal(selectedProfile.VendorType, request.Profile.VendorType);
            Assert.Equal(selectedProfile.ProviderType, request.Profile.ProviderType);
            Assert.Equal(selectedProfile.BaseUrl, request.Profile.BaseUrl);
            Assert.Equal(selectedProfile.ApiKey, request.Profile.ApiKey);
            Assert.Equal(CopilotCodexApprovalPolicyMode.Unspecified, request.CodexApprovalPolicy.Mode);
            Assert.True(request.CodexShellToolEnabled);
            Assert.Equal(
                new[] { globalInstructions, projectInstructions },
                request.ProjectInstructions.Select(document => document.Path),
                StringComparer.OrdinalIgnoreCase);

            // Discovery without a supplied snapshot is a separate entry point.
            var discovered = CopilotAgentProjectInstructions.DiscoverWithGlobal(
                [projectRoot], activeDocument, additionalTargetFilePaths: null, globalRoot);
            Assert.Equal(
                new[] { globalInstructions, projectInstructions },
                discovered.Select(document => document.Path),
                StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            temporaryRoot.Delete(recursive: true);
        }
    }
}
