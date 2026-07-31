using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotPromptSuggestionProfileSelectionTests
{
    [Fact]
    public void ResolveRequiresAnExplicitAvailableRouteAndNeverFallsBack()
    {
        var current = CreateProfile("current", "Primary", "primary-model");
        var fast = CreateProfile("fast", "Fast", "fast-model");
        var incomplete = CreateProfile("broken", "Broken", "broken-model");
        incomplete.ApiKey = string.Empty;
        var profiles = new[] { current, fast, incomplete };

        Assert.Null(CopilotPromptSuggestionProfileSelection.Resolve(
            profiles,
            current,
            storedProfileId: string.Empty));
        Assert.Same(current, CopilotPromptSuggestionProfileSelection.Resolve(
            profiles,
            current,
            CopilotPromptSuggestionProfileSelection.CurrentProfileId));
        Assert.Same(fast, CopilotPromptSuggestionProfileSelection.Resolve(
            profiles,
            current,
            fast.Id));
        Assert.Null(CopilotPromptSuggestionProfileSelection.Resolve(
            profiles,
            current,
            incomplete.Id));
        Assert.Null(CopilotPromptSuggestionProfileSelection.Resolve(
            profiles,
            current,
            "missing"));
    }

    [Fact]
    public void CurrentRouteAlsoPausesWhenCurrentProfileIsUnavailable()
    {
        var current = CreateProfile("current", "Primary", "primary-model");
        current.ApiKey = string.Empty;

        Assert.Null(CopilotPromptSuggestionProfileSelection.Resolve(
            [current],
            current,
            CopilotPromptSuggestionProfileSelection.CurrentProfileId));
        Assert.Contains(
            "受控暂停",
            CopilotPromptSuggestionProfileSelection.Describe(
                [current],
                current,
                CopilotPromptSuggestionProfileSelection.CurrentProfileId));
    }

    [Fact]
    public void UsageLabelMakesProfileModelAndTokensVisible()
    {
        var profile = CreateProfile("fast", "Fast", "fast-model");

        var label = CopilotPromptSuggestionProfileSelection.FormatUsage(
            profile,
            new CopilotTokenUsage(1200, 18, 1218));

        Assert.Contains("Fast · fast-model", label);
        Assert.Contains("输入", label);
        Assert.Contains("输出", label);
        Assert.Contains("18", label);
        Assert.DoesNotContain(profile.ApiKey, label, StringComparison.Ordinal);
        Assert.DoesNotContain(profile.BaseUrl, label, StringComparison.Ordinal);
    }

    private static CopilotProfileConfig CreateProfile(
        string id,
        string name,
        string model)
    {
        return new CopilotProfileConfig
        {
            Id = id,
            Name = name,
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = model,
        };
    }
}
