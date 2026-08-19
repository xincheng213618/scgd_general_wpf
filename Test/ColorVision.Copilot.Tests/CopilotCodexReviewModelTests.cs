using ColorVision.Copilot;
using System;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexReviewModelTests
{
    [Fact]
    public void ClosestTrustedReviewModelIsFrozenIntoTheSubmittedAgentRequest()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                review_model = "gpt-review-home"

                [projects.'{projectRoot}']
                trust_level = "trusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "config.toml");
            File.WriteAllText(configPath, "review_model = \"gpt-review-project\"");

            var submittedContext = new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                projectRoot,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: globalRoot);
            File.WriteAllText(configPath, "review_model = \"gpt-review-updated\"");
            var submitted = submittedContext.ProjectInstructionDiscoveryOptions;
            var sourceProfile = CreateProfile();
            var reviewProfile = CopilotReviewModelSelection.CreateRequestProfile(
                sourceProfile,
                CopilotAgentMode.Review,
                CopilotResponsePersonality.None,
                configuredModelInstructions: null,
                configuredReviewModel: submitted.ConfiguredReviewModel);
            var plan = CopilotAgentRequestFactory.Prepare(
                "Review the workspace.",
                CopilotAgentMode.Review,
                submittedContext);
            var request = CopilotAgentRequestFactory.Create(
                plan,
                new CopilotAgentRequestBuildInput
                {
                    Profile = reviewProfile,
                    AgentDefaults = new CopilotAgentDefaultsConfig(),
                });
            var refreshed = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);

            Assert.Equal("gpt-review-project", submitted.ConfiguredReviewModel);
            Assert.True(submitted.HasReviewModelOverride);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.TrustedProject,
                submitted.ReviewModelSource);
            Assert.Equal("gpt-review-project", reviewProfile.Model);
            Assert.Equal("gpt-review-project", request.Profile.Model);
            Assert.Equal("gpt-review-updated", refreshed.ConfiguredReviewModel);
            AssertProfileBoundaryIsPreserved(sourceProfile, reviewProfile);
            Assert.Equal("gpt-primary", sourceProfile.Model);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ReviewModelAppliesOnlyToReviewAndPreservesTheSelectedProviderBoundary()
    {
        var sourceProfile = CreateProfile();

        var reviewProfile = CopilotReviewModelSelection.CreateRequestProfile(
            sourceProfile,
            CopilotAgentMode.Review,
            CopilotResponsePersonality.Pragmatic,
            configuredModelInstructions: null,
            configuredReviewModel: "gpt-review");
        var codeProfile = CopilotReviewModelSelection.CreateRequestProfile(
            sourceProfile,
            CopilotAgentMode.Code,
            CopilotResponsePersonality.Pragmatic,
            configuredModelInstructions: null,
            configuredReviewModel: "gpt-review");

        Assert.Equal("gpt-review", reviewProfile.Model);
        Assert.Equal("gpt-primary", codeProfile.Model);
        Assert.Equal("gpt-review", CopilotReviewModelSelection.ResolveEffectiveModel(
            CopilotAgentMode.Review,
            "gpt-review",
            sourceProfile.Model));
        Assert.Equal("gpt-primary", CopilotReviewModelSelection.ResolveEffectiveModel(
            CopilotAgentMode.Plan,
            "gpt-review",
            sourceProfile.Model));
        AssertProfileBoundaryIsPreserved(sourceProfile, reviewProfile);
        AssertProfileBoundaryIsPreserved(sourceProfile, codeProfile);
        Assert.NotSame(sourceProfile, reviewProfile);
        Assert.NotSame(sourceProfile, codeProfile);
    }

    [Fact]
    public void UntrustedOrInvalidReviewModelsCannotReplaceTheCodexHomeContract()
    {
        string globalRoot = CreateTemporaryDirectory();
        string projectRoot = CreateTemporaryDirectory();
        try
        {
            Directory.CreateDirectory(Path.Combine(projectRoot, ".git"));
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"""
                review_model = "gpt-review-home"

                [projects.'{projectRoot}']
                trust_level = "untrusted"
                """);
            string configDirectory = Path.Combine(projectRoot, ".codex");
            Directory.CreateDirectory(configDirectory);
            File.WriteAllText(
                Path.Combine(configDirectory, "config.toml"),
                "review_model = \"gpt-review-project\"");

            var untrusted = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot, projectRoot);
            Assert.Equal("gpt-review-home", untrusted.ConfiguredReviewModel);
            Assert.Equal(
                CopilotProjectInstructionConfigSources.CodexHome,
                untrusted.ReviewModelSource);
            Assert.Empty(untrusted.AppliedProjectConfigFilePaths);

            string oversizedModel = new('x', CopilotReviewModelSelection.MaximumModelCharacters + 1);
            File.WriteAllText(
                Path.Combine(globalRoot, "config.toml"),
                $"review_model = \"{oversizedModel}\"");
            var invalid = CopilotProjectInstructionDiscoveryConfig.Load(globalRoot);
            Assert.False(invalid.HasReviewModelOverride);
            Assert.Empty(invalid.ConfiguredReviewModel);
        }
        finally
        {
            Directory.Delete(globalRoot, recursive: true);
            Directory.Delete(projectRoot, recursive: true);
        }
    }

    [Fact]
    public void ReviewModelDiagnosticsExposeValueSourceModeAndProviderBoundary()
    {
        var options = CopilotProjectInstructionDiscoveryConfig.CreateDefault() with
        {
            ConfiguredReviewModel = "gpt-review",
            HasReviewModelOverride = true,
            ReviewModelSource = CopilotProjectInstructionConfigSources.CodexHome,
        };
        var sourceProfile = CreateProfile();
        var reviewProfile = CopilotReviewModelSelection.CreateRequestProfile(
            sourceProfile,
            CopilotAgentMode.Review,
            CopilotResponsePersonality.None,
            configuredModelInstructions: null,
            configuredReviewModel: options.ConfiguredReviewModel);
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
            ProfileLabel = reviewProfile.DisplayLabel,
            Mode = CopilotAgentMode.Review,
            CodexReviewModel = options.ConfiguredReviewModel,
            HasCodexReviewModelOverride = true,
            CodexReviewModelSourceLabel = options.ReviewModelSourceLabel,
        });
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                SelectedProfile = sourceProfile,
                ComposerMode = CopilotAgentMode.Review,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex review_model：gpt-review", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.ReviewModelSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("Review 模型：gpt-review", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex review_model：gpt-review", debugReport, StringComparison.Ordinal);
        Assert.Contains("当前 Review 模式生效", contextReport, StringComparison.Ordinal);
        Assert.Contains("当前 Review 模式生效", debugReport, StringComparison.Ordinal);
        Assert.Contains("Provider", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Provider", contextReport, StringComparison.Ordinal);
        Assert.Contains("Provider", debugReport, StringComparison.Ordinal);
        Assert.Contains("当前有效模型 gpt-review", debugReport, StringComparison.Ordinal);
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
            Model = "gpt-primary",
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
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"copilot-review-model-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
