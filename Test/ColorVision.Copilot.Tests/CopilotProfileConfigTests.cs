using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotProfileConfigTests
{
    [Fact]
    public void ClonePreservesCredentialRecoveryState()
    {
        var source = CopilotProfileConfig.CreateDefault();
        source.ApiKey = string.Empty;
        source.CredentialNeedsReentry = true;
        source.SupportsImageInput = true;

        var clone = source.Clone();

        Assert.True(clone.CredentialNeedsReentry);
        Assert.True(clone.SupportsImageInput);
        Assert.Contains("re-entry required", clone.ConfigurationStatusToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void EndpointChangesClearImageInputDeclaration()
    {
        var profile = CopilotProfileConfig.CreateDefault();

        profile.SupportsImageInput = true;
        profile.Model = "another-model";
        Assert.False(profile.SupportsImageInput);

        profile.SupportsImageInput = true;
        profile.BaseUrl = "https://example.test/v1";
        Assert.False(profile.SupportsImageInput);

        profile.SupportsImageInput = true;
        profile.ProviderType = CopilotProviderType.OpenAICompatible;
        Assert.False(profile.SupportsImageInput);
    }
}
