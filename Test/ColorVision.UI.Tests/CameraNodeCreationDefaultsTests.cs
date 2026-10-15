using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using FlowEngineLib;
using FlowEngineLib.Algorithm;
using ST.Library.UI.NodeEditor;

namespace ColorVision.UI.Tests;

public sealed class CameraNodeCreationDefaultsTests
{
    public static TheoryData<Type> SingleExposureNodes => new()
    {
        typeof(LVCameraNode),
        typeof(LocalCameraNode),
        typeof(AOILocatePixelsCameraNode),
        typeof(AOIRegisterPixelsCameraNode),
        typeof(AOILocAndRegPixelsCameraNode)
    };

    [Fact]
    public void CaptureUsesSelectedCalibrationNameAndGroupGain()
    {
        StaTest.Run(() =>
        {
            DisplayCameraConfig display = new()
            {
                ExpTime = 800,
                ExpTimeR = 10,
                ExpTimeG = 20,
                ExpTimeB = 30,
                Gain = 1,
                AvgCount = 4,
                FlipMode = CVImageFlipMode.XY,
                CalibrationTemplateIndex = 1
            };
            CalibrationParam calibration = new() { Id = 42, Name = "calibration-template", CalibrationMode = "group1" };
            TemplateModel<CalibrationParam>[] templates = [new("calibration-template", calibration)];
            GroupResource group = new(new SysResourceModel { Name = "group1", Value = "{\"Gain\":7}" });

            CameraAcquisitionDefaults defaults = CameraNodeCreationDefaults.Capture(display, true, templates, [group]);

            Assert.Equal("calibration-template", defaults.CalibrationTemplateName);
            Assert.Equal(7f, defaults.Gain);
            Assert.Equal(800, defaults.ExpTime);
            Assert.Equal(4, defaults.AvgCount);
            Assert.Equal(CVImageFlipMode.XY, defaults.FlipMode);
        });
    }

    [Fact]
    public void EmptyCalibrationKeepsTheManualGain()
    {
        StaTest.Run(() =>
        {
            DisplayCameraConfig display = new() { Gain = 3, CalibrationTemplateIndex = 0 };
            CalibrationParam calibration = new() { Id = 42, Name = "calibration-template", CalibrationMode = "group1" };
            TemplateModel<CalibrationParam>[] templates = [new("calibration-template", calibration)];
            GroupResource group = new(new SysResourceModel { Name = "group1", Value = "{\"Gain\":7}" });

            CameraAcquisitionDefaults defaults = CameraNodeCreationDefaults.Capture(display, false, templates, [group]);

            Assert.Empty(defaults.CalibrationTemplateName);
            Assert.Equal(3f, defaults.Gain);
        });
    }

    [Theory]
    [MemberData(nameof(SingleExposureNodes))]
    public void DirectSingleExposureNodesReceiveCurrentSettings(Type nodeType)
    {
        StaTest.Run(() =>
        {
            STNode node = (STNode)Activator.CreateInstance(nodeType)!;
            node.Create();

            Assert.True(CameraNodeCreationDefaults.TryApply(node, NewDefaults()));
            Assert.Equal(800f, Get<float>(node, "ExpTime"));
            Assert.Equal(7f, Get<float>(node, "Gain"));
            Assert.Equal(4, Get<int>(node, "AvgCount"));
            Assert.Equal(CVImageFlipMode.XY, Get<CVImageFlipMode>(node, "FlipMode"));
            string calibrationProperty = node is LocalCameraNode ? "CalibTempName" : "CaliTempName";
            Assert.Equal("calibration-template", Get<string>(node, calibrationProperty));
        });
    }

    [Fact]
    public void CvNodeReceivesThreeIndependentExposures()
    {
        StaTest.Run(() =>
        {
            CVCameraNode node = new();
            node.Create();

            Assert.True(CameraNodeCreationDefaults.TryApply(node, NewDefaults()));
            Assert.Equal(10f, node.TempR);
            Assert.Equal(20f, node.TempG);
            Assert.Equal(30f, node.TempB);
            Assert.Equal(7f, node.Gain);
            Assert.Equal(4, node.AvgCount);
            Assert.Equal(CVImageFlipMode.XY, node.FlipMode);
            Assert.Equal("calibration-template", node.CalibTempName);
        });
    }

    [Fact]
    public void InvalidSourceExposureDoesNotPartiallyChangeNewNode()
    {
        StaTest.Run(() =>
        {
            LVCameraNode node = new();
            node.Create();
            CameraAcquisitionDefaults invalid = NewDefaults() with { ExpTime = double.NaN };

            Assert.False(CameraNodeCreationDefaults.TryApply(node, invalid));
            Assert.Equal(100f, node.ExpTime);
            Assert.Equal(10f, node.Gain);
            Assert.Equal(1, node.AvgCount);
            Assert.Empty(node.CaliTempName);
        });
    }

    private static CameraAcquisitionDefaults NewDefaults() => new(800, 10, 20, 30, true, 7, 4, CVImageFlipMode.XY, "calibration-template");

    private static T Get<T>(object node, string name) => (T)node.GetType().GetProperty(name)!.GetValue(node)!;
}
