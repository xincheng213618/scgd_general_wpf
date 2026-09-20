using ColorVision.Engine.Media;
using Newtonsoft.Json;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Black;
using ProjectARVRPro.Process.Chessboard;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using ProjectARVRPro.Process.POI;
using ProjectARVRPro.Process.W255;
using ProjectARVRPro.Process.W51;
using System.ComponentModel;
using System.Reflection;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class ProcessOverlayDisplayConfigTests
{
    [Fact]
    public void PoiProcessTemplatesPreserveExistingOverlayDefaults()
    {
        Assert.Equal(PoiDisplayTemplateDefaults.Cie, new BlackProcessConfig().DisplayTemplate);
        Assert.Equal(PoiDisplayTemplateDefaults.Cie, new PoiDynamicProcessConfig().DisplayTemplate);
        Assert.Equal(PoiDisplayTemplateDefaults.Cie, new W255ProcessConfig().DisplayTemplate);
        Assert.Equal(PoiDisplayTemplateDefaults.Cie, new LuminanceChromaticityProcessConfig().DisplayTemplate);
        Assert.Equal(PoiDisplayTemplateDefaults.Luminance, new ChessboardProcessConfig().DisplayTemplate);
        Assert.Equal(PoiDisplayTemplateDefaults.Luminance, new ChessboardDynamicProcessConfig().DisplayTemplate);
        Assert.Equal(PoiDisplayTemplateDefaults.Luminance, new LuminanceChromaticityYWProcessConfig().DisplayTemplate);
    }

    [Fact]
    public void LegacyConfigJsonUsesTheSameOverlayDefaults()
    {
        Assert.Equal(PoiDisplayTemplateDefaults.Cie, JsonConvert.DeserializeObject<BlackProcessConfig>("{}")!.DisplayTemplate);
        Assert.Equal(PoiDisplayTemplateDefaults.Cie, JsonConvert.DeserializeObject<PoiDynamicProcessConfig>("{}")!.DisplayTemplate);
        Assert.Equal(PoiDisplayTemplateDefaults.Cie, JsonConvert.DeserializeObject<W255ProcessConfig>("{}")!.DisplayTemplate);
        Assert.Equal(PoiDisplayTemplateDefaults.Cie, JsonConvert.DeserializeObject<LuminanceChromaticityProcessConfig>("{}")!.DisplayTemplate);
        Assert.Equal(PoiDisplayTemplateDefaults.Luminance, JsonConvert.DeserializeObject<ChessboardProcessConfig>("{}")!.DisplayTemplate);
        Assert.Equal(PoiDisplayTemplateDefaults.Luminance, JsonConvert.DeserializeObject<ChessboardDynamicProcessConfig>("{}")!.DisplayTemplate);
        Assert.Equal(PoiDisplayTemplateDefaults.Luminance, JsonConvert.DeserializeObject<LuminanceChromaticityYWProcessConfig>("{}")!.DisplayTemplate);
    }

    [Theory]
    [InlineData(typeof(BlackProcessConfig))]
    [InlineData(typeof(PoiDynamicProcessConfig))]
    [InlineData(typeof(W255ProcessConfig))]
    [InlineData(typeof(LuminanceChromaticityProcessConfig))]
    [InlineData(typeof(ChessboardProcessConfig))]
    [InlineData(typeof(ChessboardDynamicProcessConfig))]
    [InlineData(typeof(LuminanceChromaticityYWProcessConfig))]
    public void PoiProcessTemplatesUseTheFieldSelectionEditor(Type configType)
    {
        PropertyInfo property = configType.GetProperty("DisplayTemplate")!;

        Assert.Equal(
            typeof(CvcieTemplatePropertiesEditor),
            property.GetCustomAttribute<PropertyEditorTypeAttribute>()!.EditorType);
    }

    [Fact]
    public void W51DoesNotExposeUnusedCenterKey()
    {
        Assert.Null(typeof(W51ProcessConfig).GetProperty("Key_Center"));
        Assert.True(new W51ProcessConfig().DrawFovOverlay);
    }

}
