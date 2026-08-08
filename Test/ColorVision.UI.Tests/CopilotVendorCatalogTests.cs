using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotVendorCatalogTests
{
    [Fact]
    public void OfficialOpenAiPresetsLeadWithGpt56AndPreserveExistingChoices()
    {
        var models = CopilotVendorCatalog.GetModelPresets(CopilotVendorType.OpenAI);

        Assert.Equal(
            ["gpt-5.6", "gpt-5.6-terra", "gpt-5.6-luna", "gpt-5.5", "gpt-4o"],
            models);
        Assert.Equal(models.Count, models.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(
            models,
            model => Assert.Equal(
                CopilotVendorType.OpenAI,
                CopilotVendorCatalog.InferVendorType(string.Empty, model)));
    }
}
