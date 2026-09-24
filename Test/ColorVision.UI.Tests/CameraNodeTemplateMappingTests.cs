using ColorVision.Engine.FlowProcessing.Editor.NodeConfiguration;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.PropertyEditor;
using FlowEngineLib;
using FlowEngineLib.Node.Camera;
using FlowEngineLib.PropertyEditor;
using System.ComponentModel;
using System.Reflection;

namespace ColorVision.UI.Tests;

public class CameraNodeTemplateMappingTests
{
    [Fact]
    public void CameraCalibrationPropertiesUseCalibrationTemplateEditor()
    {
        AssertEditor(typeof(AOILocAndRegPixelsCameraNode), nameof(AOILocAndRegPixelsCameraNode.CaliTempName), typeof(CalibrationTemplatePropertiesEditor));
        AssertEditor(typeof(AOILocatePixelsCameraNode), nameof(AOILocatePixelsCameraNode.CaliTempName), typeof(CalibrationTemplatePropertiesEditor));
        AssertEditor(typeof(AOIRegisterPixelsCameraNode), nameof(AOIRegisterPixelsCameraNode.CaliTempName), typeof(CalibrationTemplatePropertiesEditor));
        AssertEditor(typeof(CommCameraNode), nameof(CommCameraNode.CalibTempName), typeof(CalibrationTemplatePropertiesEditor));
        AssertEditor(typeof(CVAOI2CameraNode), nameof(CVAOI2CameraNode.CalibTempName), typeof(CalibrationTemplatePropertiesEditor));
        AssertEditor(typeof(CVAOICameraNode), nameof(CVAOICameraNode.CalibTempName), typeof(CalibrationTemplatePropertiesEditor));
        AssertEditor(typeof(CVCameraNode), nameof(CVCameraNode.CalibTempName), typeof(CalibrationTemplatePropertiesEditor));
    }

    [Theory]
    [InlineData(nameof(LVCameraNode.CaliTempName), typeof(CalibrationTemplatePropertiesEditor))]
    [InlineData(nameof(LVCameraNode.POITempName), typeof(PoiTemplatePropertiesEditor))]
    [InlineData(nameof(LVCameraNode.POIFilterTempName), typeof(PoiFilterTemplatePropertiesEditor))]
    [InlineData(nameof(LVCameraNode.POIReviseTempName), typeof(PoiReviseTemplatePropertiesEditor))]
    public void EngineCameraPropertiesUseDirectEditors(string propertyName, Type editorType)
    {
        PropertyInfo property = typeof(LVCameraNode).GetProperty(propertyName)!;
        Assert.Equal(editorType, property.GetCustomAttribute<PropertyEditorTypeAttribute>()?.EditorType);
        Assert.Null(FlowNodePropertyMetadataProvider.Instance.GetEditorType(property));
        Assert.False(typeof(FlowPropertyEditorProxy).IsAssignableFrom(editorType));
        Assert.Same(typeof(LVCameraNode).Assembly, property.DeclaringType!.Assembly);
    }

    [Fact]
    public void AoiCameraPropertiesMatchCameraServiceTemplateContracts()
    {
        AssertEditor(typeof(AOILocatePixelsCameraNode), nameof(AOILocatePixelsCameraNode.AlgTempName), typeof(LedCheck2TemplatePropertiesEditor));

        AssertEditor(typeof(AOILocAndRegPixelsCameraNode), nameof(AOILocAndRegPixelsCameraNode.AlgTempName), typeof(LedCheck2TemplatePropertiesEditor));
        AssertEditor(typeof(AOILocAndRegPixelsCameraNode), nameof(AOILocAndRegPixelsCameraNode.OutputTempName), typeof(PoiOutputTemplatePropertiesEditor));

        AssertEditor(typeof(AOIRegisterPixelsCameraNode), nameof(AOIRegisterPixelsCameraNode.AlgTempName), typeof(LedCheck2TemplatePropertiesEditor));
        AssertEditor(typeof(AOIRegisterPixelsCameraNode), nameof(AOIRegisterPixelsCameraNode.OutputTempName), typeof(PoiOutputTemplatePropertiesEditor));

        AssertEditor(typeof(CVAOICameraNode), nameof(CVAOICameraNode.AlgTempName), typeof(LedCheck2TemplatePropertiesEditor));
        AssertEditor(typeof(CVAOI2CameraNode), nameof(CVAOI2CameraNode.AlgTempName), typeof(LedCheck2TemplatePropertiesEditor));
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
        Assert.Equal(editorType, nodeType.GetProperty(propertyName)!.GetCustomAttribute<PropertyEditorTypeAttribute>()?.EditorType);
    }
}
