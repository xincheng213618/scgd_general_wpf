using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexModelSelectionTests
{
    [Fact]
    public void ClosestTrustedModelIsFrozenIntoTheSubmittedAgentRequest()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                model = "gpt-home"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(configPath, "model = \"gpt-project\"");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(configPath, "model = \"gpt-updated\"");
            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;
            var sourceProfile = CreateProfile();
            var requestProfile = CopilotReviewModelSelection.CreateRequestProfile(
                sourceProfile,
                CopilotAgentMode.Code,
                CopilotResponsePersonality.None,
                configuredModelInstructions: null,
                configuredReviewModel: null,
                configuredModel: submitted.ConfiguredModel);
            var plan = CopilotAgentRequestFactory.Prepare(
                "Inspect the workspace.",
                CopilotAgentMode.Code,
                submittedContext);
            var request = CopilotAgentRequestFactory.Create(
                plan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = requestProfile,
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal("gpt-project", submitted.ConfiguredModel);
            Assert.True(submitted.HasModelOverride);
            Assert.Equal(CopilotProjectInstructionConfigSources.TrustedProject, submitted.ModelSource);
            Assert.Equal("gpt-project", requestProfile.Model);
            Assert.Equal("gpt-project", request.Profile.Model);
            Assert.Equal("gpt-updated", refreshed.ConfiguredModel);
            AssertProfileBoundaryIsPreserved(sourceProfile, requestProfile);
            Assert.Equal("gpt-profile", sourceProfile.Model);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ReviewModelOverridesConfiguredModelOnlyForReviewRequests()
    {
        var sourceProfile = CreateProfile();

        var reviewProfile = CopilotReviewModelSelection.CreateRequestProfile(
            sourceProfile,
            CopilotAgentMode.Review,
            CopilotResponsePersonality.None,
            configuredModelInstructions: null,
            configuredReviewModel: "gpt-review",
            configuredModel: "gpt-configured");
        var codeProfile = CopilotReviewModelSelection.CreateRequestProfile(
            sourceProfile,
            CopilotAgentMode.Code,
            CopilotResponsePersonality.None,
            configuredModelInstructions: null,
            configuredReviewModel: "gpt-review",
            configuredModel: "gpt-configured");

        Assert.Equal("gpt-review", reviewProfile.Model);
        Assert.Equal("gpt-configured", codeProfile.Model);
        Assert.Equal("gpt-review", CopilotReviewModelSelection.ResolveEffectiveModel(
            CopilotAgentMode.Review,
            "gpt-review",
            sourceProfile.Model,
            "gpt-configured"));
        Assert.Equal("gpt-configured", CopilotReviewModelSelection.ResolveEffectiveModel(
            CopilotAgentMode.Plan,
            "gpt-review",
            sourceProfile.Model,
            "gpt-configured"));
        AssertProfileBoundaryIsPreserved(sourceProfile, reviewProfile);
        AssertProfileBoundaryIsPreserved(sourceProfile, codeProfile);
    }

    [Fact]
    public void UntrustedOrInvalidModelsCannotReplaceTheCodexHomeContract()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                model = "gpt-home"

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(Path.Combine(configDirectory, "config.toml"), "model = \"gpt-project\"");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            Assert.Equal("gpt-home", untrusted.ConfiguredModel);
            Assert.Equal(CopilotProjectInstructionConfigSources.CodexHome, untrusted.ModelSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            string oversizedModel = new('x', CopilotConfiguredModelSelection.MaximumModelCharacters + 1);
            File.WriteAllText(Path.Combine(globalRoot, "config.toml"), $"model = \"{oversizedModel}\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);

            Assert.False(invalid.HasModelOverride);
            Assert.Empty(invalid.ConfiguredModel);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ModelDiagnosticsExposeValueSourcePrecedenceAndProviderBoundary()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredModel = "gpt-configured",
            HasModelOverride = true,
            ModelSource = CopilotProjectInstructionConfigSources.CodexHome,
            ConfiguredReviewModel = "gpt-review",
            HasReviewModelOverride = true,
            ReviewModelSource = CopilotProjectInstructionConfigSources.TrustedProject,
        };
        var profile = CreateProfile();
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
            ProfileLabel = profile.DisplayLabel,
            Mode = CopilotAgentMode.Review,
            CodexModel = options.ConfiguredModel,
            HasCodexModelOverride = true,
            CodexModelSourceLabel = options.ModelSourceLabel,
            CodexReviewModel = options.ConfiguredReviewModel,
            HasCodexReviewModelOverride = true,
            CodexReviewModelSourceLabel = options.ReviewModelSourceLabel,
        });

        Assert.Contains("Codex model：gpt-configured", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.ModelSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("review_model 优先", memoryReport, StringComparison.Ordinal);
        Assert.Contains("请求模型：gpt-configured", contextReport, StringComparison.Ordinal);
        Assert.Contains("review_model 优先", contextReport, StringComparison.Ordinal);
        Assert.Contains("Provider", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Provider", contextReport, StringComparison.Ordinal);
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            Id = "primary-profile",
            VendorType = CopilotVendorType.OpenAI,
            Name = "Primary",
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://api.openai.com/v1",
            Model = "gpt-profile",
            MaxTokens = 4_096,
        };
    }

    private static void AssertProfileBoundaryIsPreserved(
        CopilotProfileConfig expected,
        CopilotProfileConfig actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.VendorType, actual.VendorType);
        Assert.Equal(expected.ProviderType, actual.ProviderType);
        Assert.Equal(expected.ApiKey, actual.ApiKey);
        Assert.Equal(expected.BaseUrl, actual.BaseUrl);
        Assert.Equal(expected.MaxTokens, actual.MaxTokens);
        Assert.NotSame(expected, actual);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-model-selection-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
