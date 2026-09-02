using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotVendorCatalogTests
{
    [Fact]
    public void OfficialOpenAiPresetsAreUniqueAndResolveToTheirVendor()
    {
        var models = CopilotVendorCatalog.GetModelPresets(CopilotVendorType.OpenAI);

        Assert.NotEmpty(models);
        Assert.Equal(models.Count, models.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(
            models,
            model => Assert.Equal(
                CopilotVendorType.OpenAI,
                CopilotVendorCatalog.InferVendorType(string.Empty, model)));
    }
}
