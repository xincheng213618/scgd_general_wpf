using Conoscope.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Conoscope.Tests;

public class AdvancedExportSettingsTests
{
    [Theory]
    [InlineData("{\"ExportDecimalPlaces\":6,\"AdvancedExport\":{\"UseAzimuthCrossSection\":false,\"CrossSectionPolarAngle\":37}}")]
    [InlineData("{\"AdvancedExport\":{\"UseAzimuthCrossSection\":false,\"CrossSectionPolarAngle\":37},\"ExportDecimalPlaces\":6}")]
    public void LegacyJsonLoadsRegardlessOfPropertyOrder(string json)
    {
        ConoscopeConfig config = JsonConvert.DeserializeObject<ConoscopeConfig>(json)!;

        Assert.Equal(6, config.ExportDecimalPlaces);
        Assert.Equal(6, config.AdvancedExport.DecimalPlaces);
        Assert.Equal(CrossSectionType.Polar, config.AdvancedExport.CrossSectionType);
        Assert.Equal(37, config.AdvancedExport.CrossSectionAngle);
    }

    [Fact]
    public void JsonKeepsLegacyAliasesWithoutSerializingDerivedState()
    {
        ConoscopeConfig config = new ConoscopeConfig
        {
            AdvancedExport = new AdvancedExportSettings
            {
                DecimalPlaces = 7,
                CrossSectionType = CrossSectionType.Polar,
                CrossSectionPolarAngle = 32
            }
        };

        JObject json = JObject.Parse(JsonConvert.SerializeObject(config));
        JObject advancedExport = (JObject)json[nameof(ConoscopeConfig.AdvancedExport)]!;

        Assert.Equal(7, json[nameof(ConoscopeConfig.ExportDecimalPlaces)]!.Value<int>());
        Assert.Equal(7, advancedExport[nameof(AdvancedExportSettings.DecimalPlaces)]!.Value<int>());
        Assert.False(advancedExport[nameof(AdvancedExportSettings.UseAzimuthCrossSection)]!.Value<bool>());
        Assert.Null(advancedExport[nameof(AdvancedExportSettings.CrossSectionType)]);
        Assert.Null(advancedExport[nameof(AdvancedExportSettings.CrossSectionAngle)]);
    }

    [Fact]
    public void CrossSectionAngleAlwaysFollowsSelectedType()
    {
        AdvancedExportSettings settings = new AdvancedExportSettings
        {
            CrossSectionAzimuthAngle = 24,
            CrossSectionPolarAngle = 48
        };

        Assert.Equal(24, settings.CrossSectionAngle);
        settings.CrossSectionType = CrossSectionType.Polar;
        Assert.Equal(48, settings.CrossSectionAngle);
    }
}
