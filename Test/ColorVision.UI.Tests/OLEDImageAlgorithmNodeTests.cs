using ColorVision.Engine.Templates.Jsons.OLEDImageProcessing;
using FlowEngineLib.Algorithm;
using FlowEngineLib.Base;
using FlowEngineLib.Node.Algorithm;

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
    }
}
