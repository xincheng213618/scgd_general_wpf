using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexReviewModelTests
{
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
}
