using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotVendorCatalogTests
{
    public static TheoryData<CopilotVendorType, string[]> CurrentModelPresets => new()
    {
        { CopilotVendorType.DeepSeek, ["deepseek-v4-pro", "deepseek-v4-flash", "deepseek-v4-flash-vision-exp"] },
        { CopilotVendorType.OpenAI, ["gpt-6-astra", "gpt-5.6", "gpt-5.6-sol", "gpt-5.6-terra", "gpt-5.6-luna"] },
        { CopilotVendorType.Claude, ["claude-fable-5", "claude-mythos-5", "claude-opus-5", "claude-sonnet-5", "claude-haiku-4-5-20251001"] },
        { CopilotVendorType.Grok, ["grok-4.6", "grok-4.5", "grok-4.20"] },
        { CopilotVendorType.Gemini, ["gemini-3.1-pro-preview", "gemini-3.8-flash", "gemini-3.7-flash", "gemini-3.6-flash", "gemini-3.5-flash", "gemini-3.5-flash-lite"] },
        { CopilotVendorType.GLM, ["glm-5.2", "glm-5-turbo", "glm-4.7-flash", "glm-4.5-air"] },
        { CopilotVendorType.MiniMax, ["MiniMax-M2.7", "MiniMax-M2.7-highspeed", "MiniMax-M2.5", "MiniMax-M2.5-highspeed"] },
        { CopilotVendorType.Xiaomi, ["mimo-v2.5-pro", "mimo-v2.5"] },
        { CopilotVendorType.SenseNova, ["sensenova-6.7-flash-lite"] },
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
        Assert.DoesNotContain("claude-fable-5-1", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.Claude));
        Assert.DoesNotContain("claude-sonnet-4-7", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.Claude));
        Assert.DoesNotContain("grok-3", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.Grok));
        Assert.DoesNotContain("gemini-2.0-flash", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.Gemini));
        Assert.DoesNotContain("gemini-3.1-flash-lite", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.Gemini));
        Assert.DoesNotContain("glm-4.5", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.GLM));
        Assert.DoesNotContain("MiniMax-M1", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.MiniMax));
        Assert.DoesNotContain("MiniMax-Text-01", CopilotVendorCatalog.GetModelPresets(CopilotVendorType.MiniMax));
    }
}
