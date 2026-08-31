using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexModelSelectionTests
{
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
        string debugReport = CopilotEffectiveConfigDiagnostics.Format(
            new CopilotEffectiveConfigDiagnosticContext
            {
                Config = new CopilotConfig(),
                State = new CopilotChatState(),
                SelectedProfile = profile,
                ComposerMode = CopilotAgentMode.Review,
                CodexConfigOptions = options,
            });

        Assert.Contains("Codex model：gpt-configured", memoryReport, StringComparison.Ordinal);
        Assert.Contains(options.ModelSourceLabel, memoryReport, StringComparison.Ordinal);
        Assert.Contains("review_model 优先", memoryReport, StringComparison.Ordinal);
        Assert.Contains("请求模型：gpt-configured", contextReport, StringComparison.Ordinal);
        Assert.Contains("review_model 优先", contextReport, StringComparison.Ordinal);
        Assert.Contains("Codex model：gpt-configured", debugReport, StringComparison.Ordinal);
        Assert.Contains("当前有效模型 gpt-review", debugReport, StringComparison.Ordinal);
        Assert.Contains("Provider", memoryReport, StringComparison.Ordinal);
        Assert.Contains("Provider", contextReport, StringComparison.Ordinal);
        Assert.Contains("Provider", debugReport, StringComparison.Ordinal);
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
}
