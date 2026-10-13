namespace ColorVision.Copilot.Tests;

public sealed class CopilotVendorCatalogTests
{
    public static TheoryData<CopilotVendorType, string[]> CurrentModelPresets => new()
    {
        { CopilotVendorType.DeepSeek, ["deepseek-v4-pro", "deepseek-flash"] },
        { CopilotVendorType.OpenAI, ["gpt-6-astra", "gpt-6.1-sol", "gpt-6-sol", "gpt-6-luna", "gpt-5.6", "gpt-5.6-sol", "gpt-5.6-terra", "gpt-5.6-luna"] },
        { CopilotVendorType.Claude, ["claude-fable-5-1", "claude-opus-5-5", "claude-sonnet-5-5", "claude-haiku-5-5", "claude-haiku-4-5-20251001"] },
        { CopilotVendorType.Grok, ["grok-4.7", "grok-4.6", "grok-4.5", "grok-4.3"] },
        { CopilotVendorType.Gemini, ["gemini-3.1-pro-preview", "gemini-3.8-flash", "gemini-3.7-flash", "gemini-3.6-flash", "gemini-3.5-flash-lite"] },
        { CopilotVendorType.GLM, ["glm-5.3", "glm-5.3-flash", "glm-5.3-flashx", "glm-5.2", "glm-5-turbo", "glm-4.7-flash", "glm-4.5-air"] },
        { CopilotVendorType.MiniMax, ["MiniMax-M3", "MiniMax-M2.7", "MiniMax-M2.7-highspeed", "MiniMax-M2.5", "MiniMax-M2.5-highspeed"] },
        { CopilotVendorType.Xiaomi, ["mimo-v2.6-pro", "mimo-v2.6-flash"] },
        { CopilotVendorType.SenseNova, ["sensenova-6.8-flash-lite"] },
    };

    [Theory]
    [MemberData(nameof(CurrentModelPresets))]
    public void CurrentPresetsAreUniqueAndResolveToTheirVendor(
        CopilotVendorType vendorType,
        string[] expected)
    {
        var models = CopilotVendorCatalog.GetModelPresets(vendorType);

        Assert.Equal(expected, models);
        Assert.Equal(models.Count, models.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(
            models,
            model => Assert.Equal(
                vendorType,
                CopilotVendorCatalog.InferVendorType(string.Empty, model)));
    }

    [Fact]
    public void RetiredOrSupersededPresetsAreNotOfferedForNewProfiles()
    {
        Assert.DoesNotContain("gpt-4o", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.OpenAI));
        Assert.DoesNotContain("claude-mythos-5", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.Claude));
        Assert.DoesNotContain("claude-sonnet-4-7", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.Claude));
        Assert.DoesNotContain("grok-3", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.Grok));
        Assert.DoesNotContain("gemini-2.0-flash", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.Gemini));
        Assert.DoesNotContain("gemini-3.5-flash", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.Gemini));
        Assert.DoesNotContain("gemini-3.1-flash-lite", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.Gemini));
        Assert.DoesNotContain("glm-4.5", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.GLM));
        Assert.DoesNotContain("MiniMax-M1", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.MiniMax));
        Assert.DoesNotContain("MiniMax-Text-01", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.MiniMax));
        Assert.DoesNotContain("MiniMax-M3.1-Flash-Preview", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.MiniMax));
        Assert.DoesNotContain("mimo-v2.5-pro", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.Xiaomi));
        Assert.DoesNotContain("mimo-v2.5", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.Xiaomi));
    }

    [Theory]
    [InlineData(CopilotVendorType.Xiaomi, "https://api.xiaomimimo.com/v1", "https://api.xiaomimimo.com/anthropic")]
    [InlineData(CopilotVendorType.MiniMax, "https://api.minimax.cn/v1", "https://api.minimax.cn/anthropic")]
    public void DualProtocolPresetsProvideTheMatchingEndpoint(CopilotVendorType vendor, string openAiUrl, string anthropicUrl)
    {
        Assert.Equal(openAiUrl, CopilotVendorCatalog.GetDefaultBaseUrl(vendor, CopilotProviderType.OpenAICompatible));
        Assert.Equal(anthropicUrl, CopilotVendorCatalog.GetDefaultBaseUrl(vendor, CopilotProviderType.AnthropicCompatible));
    }

    [Theory]
    [InlineData("gpt-5.6-sol", "GPT-5.6 Sol")]
    [InlineData("GPT-6-ASTRA", "GPT-6 Astra")]
    [InlineData("deepseek-flash", "deepseek-flash")]
    public void ModelDisplayNameUsesReadableOpenAiCasing(string model, string expected)
    {
        Assert.Equal(expected, CopilotVendorCatalog.FormatModelDisplayName(model));
    }
}
