using ColorVision.Engine.Templates.Jsons.OLEDImageProcessing;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.Node.Algorithm;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public class OLEDImageAlgorithmNodeTests
{
    private sealed class TestableAlgorithmOLEDImgNode : AlgorithmOLEDImgNode
    {
        public string EventName => operatorCode;

        public AlgorithmOLEDImgParam BuildPayload(CVStartCFC start)
        {
            return (AlgorithmOLEDImgParam)getBaseEventData(start);
        }
    }

    [Theory]
    [InlineData(AlgorithmOLEDImgType.局部图像增强, "OLED.LocalizationImageEnhan")]
    [InlineData(AlgorithmOLEDImgType.解串扰, "OLED.Dediffusion")]
    public void AlgorithmSelectionMapsToServiceContract(AlgorithmOLEDImgType algorithm, string expectedEventName)
    {
        var node = new TestableAlgorithmOLEDImgNode
        {
            Algorithm = algorithm,
            TempName = "OLED template",
            ImgFileName = "input.cvraw",
            OutputFileName = "result.cvraw"
        };

        AlgorithmOLEDImgParam payload = node.BuildPayload(new CVStartCFC("unit-test"));

        Assert.Equal(expectedEventName, node.EventName);
        Assert.Equal("OLED template", payload.TemplateParam.Name);
        Assert.Equal("input.cvraw", payload.ImgFileName);
        Assert.Equal(FileExtType.Raw, payload.FileType);
        Assert.Equal("result.cvraw", payload.ResultDataFileName);
    }

    [Fact]
    public void TemplateDefinitionsMatchDatabaseContracts()
    {
        var enhancement = new TemplateLocalizationImageEnhancement();
        var dediffusion = new TemplateDediffusion();

        Assert.Equal(201, enhancement.TemplateDicId);
        Assert.Equal("OLED.LocalizationImageEnhan", enhancement.Code);
        Assert.Equal(202, dediffusion.TemplateDicId);
        Assert.Equal("OLED.Dediffusion", dediffusion.Code);
        Assert.Contains("ON DUPLICATE KEY UPDATE", enhancement.GetMysqlCommand()!.GetRecover());
        Assert.Contains("ON DUPLICATE KEY UPDATE", dediffusion.GetMysqlCommand()!.GetRecover());

        using JsonDocument enhancementJson = JsonDocument.Parse(enhancement.Description);
        Assert.Equal(31, enhancementJson.RootElement.GetProperty("blurSize").GetInt32());
        Assert.Equal(256, enhancementJson.RootElement.GetProperty("img_format_convert_factor").GetInt32());

        using JsonDocument dediffusionJson = JsonDocument.Parse(dediffusion.Description);
        JsonElement rebuildConfig = dediffusionJson.RootElement.GetProperty("rebuildCfg");
        Assert.Equal(25, rebuildConfig.GetProperty("de_kernel").GetArrayLength());
        Assert.True(rebuildConfig.GetProperty("de_defusion_en").GetBoolean());
        Assert.Equal(300, rebuildConfig.GetProperty("de_iterationlimit").GetInt32());
    }
}
