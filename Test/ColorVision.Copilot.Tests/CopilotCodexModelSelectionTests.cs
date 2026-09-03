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
