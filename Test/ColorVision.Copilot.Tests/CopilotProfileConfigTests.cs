namespace ColorVision.Copilot.Tests;

public sealed class CopilotProfileConfigTests
{
    [Theory]
    [InlineData(CopilotVendorType.Xiaomi, "https://api.xiaomimimo.com/anthropic", "mimo-v2.5-pro")]
    [InlineData(CopilotVendorType.DeepSeek, "https://api.deepseek.com/anthropic", "deepseek-v4-flash")]
    [InlineData(CopilotVendorType.Gemini, "https://generativelanguage.googleapis.com/v1beta/openai", "gemini-3.5-flash")]
    public void CatalogUpdatesDoNotSwitchSavedProfiles(CopilotVendorType vendor, string endpoint, string model)
    {
        var profile = new CopilotProfileConfig
        {
            VendorType = vendor, BaseUrl = endpoint, Model = model, ApiKey = "synthetic-key", SupportsImageInput = true,
        };
        var id = profile.Id;
        var config = new CopilotConfig { Profiles = [profile] };

        config.EnsureInitialized();

        Assert.Equal(id, profile.Id);
        Assert.Equal(model, profile.Model);
        Assert.Equal(endpoint, profile.BaseUrl);
        Assert.Equal("synthetic-key", profile.ApiKey);
        Assert.True(profile.SupportsImageInput);
        Assert.Same(profile, config.GetPreferredDefaultProfile());
        Assert.False(config.EnsureInitialized());
    }

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
