using ColorVision.Engine.FlowProcessing.Editor.NodeConfiguration;
using FlowEngineLib;
using FlowEngineLib.Node.Camera;
using FlowEngineLib.PropertyEditor;
using System.Reflection;

namespace ColorVision.UI.Tests;

public class CameraNodeTemplateMappingTests
{
    [Fact]
    public void CameraCalibrationPropertiesUseCalibrationTemplateEditor()
    {
        AssertEditor(typeof(AOILocAndRegPixelsCameraNode), nameof(AOILocAndRegPixelsCameraNode.CaliTempName), typeof(FlowCalibrationTemplateEditor));
        AssertEditor(typeof(AOILocatePixelsCameraNode), nameof(AOILocatePixelsCameraNode.CaliTempName), typeof(FlowCalibrationTemplateEditor));
        AssertEditor(typeof(AOIRegisterPixelsCameraNode), nameof(AOIRegisterPixelsCameraNode.CaliTempName), typeof(FlowCalibrationTemplateEditor));
        AssertEditor(typeof(CommCameraNode), nameof(CommCameraNode.CalibTempName), typeof(FlowCalibrationTemplateEditor));
        AssertEditor(typeof(CVAOI2CameraNode), nameof(CVAOI2CameraNode.CalibTempName), typeof(FlowCalibrationTemplateEditor));
        AssertEditor(typeof(CVAOICameraNode), nameof(CVAOICameraNode.CalibTempName), typeof(FlowCalibrationTemplateEditor));
        AssertEditor(typeof(CVCameraNode), nameof(CVCameraNode.CalibTempName), typeof(FlowCalibrationTemplateEditor));
        AssertEditor(typeof(LVCameraNode), nameof(LVCameraNode.CaliTempName), typeof(FlowCalibrationTemplateEditor));
    }

    [Fact]
    public void AoiCameraPropertiesMatchCameraServiceTemplateContracts()
    {
        AssertEditor(typeof(AOILocatePixelsCameraNode), nameof(AOILocatePixelsCameraNode.AlgTempName), typeof(FlowLedCheck2JsonTemplateEditor));

        AssertEditor(typeof(AOILocAndRegPixelsCameraNode), nameof(AOILocAndRegPixelsCameraNode.AlgTempName), typeof(FlowLedCheck2JsonTemplateEditor));
        AssertEditor(typeof(AOILocAndRegPixelsCameraNode), nameof(AOILocAndRegPixelsCameraNode.OutputTempName), typeof(FlowPoiOutputTemplateEditor));

        AssertEditor(typeof(AOIRegisterPixelsCameraNode), nameof(AOIRegisterPixelsCameraNode.AlgTempName), typeof(FlowLedCheck2JsonTemplateEditor));
        AssertEditor(typeof(AOIRegisterPixelsCameraNode), nameof(AOIRegisterPixelsCameraNode.OutputTempName), typeof(FlowPoiOutputTemplateEditor));

        AssertEditor(typeof(CVAOICameraNode), nameof(CVAOICameraNode.AlgTempName), typeof(FlowLedCheck2JsonTemplateEditor));
        AssertEditor(typeof(CVAOI2CameraNode), nameof(CVAOI2CameraNode.AlgTempName), typeof(FlowLedCheck2JsonTemplateEditor));
    }

    [Fact]
    public void AoiRegisterPixelsHasAutoExposureTemplateConfigurator()
    {
        NodeConfiguratorAttribute? attribute = typeof(AOIRegisterPixelsCameraNodeConfigurator).GetCustomAttribute<NodeConfiguratorAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(typeof(AOIRegisterPixelsCameraNode), attribute.NodeType);
    }

    private static void AssertEditor(Type nodeType, string propertyName, Type editorType)
    {
        Assert.Equal(editorType, FlowNodePropertyEditorAttribute.Resolve(nodeType, propertyName));
    }
}
