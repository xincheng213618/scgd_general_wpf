using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Templates;
using FlowEngineLib;
using FlowEngineLib.Algorithm;
using ST.Library.UI.NodeEditor;
using System.Reflection;
using System.Runtime.CompilerServices;

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
            using var camera = new CurrentCameraDefaultsScope();
            STNode node = (STNode)Activator.CreateInstance(nodeType)!;
            node.Create();

            Assert.True(CameraNodeCreationDefaults.InitializeFromCurrentCamera(node));
            Assert.Equal(800f, Get<float>(node, "ExpTime"));
            Assert.Equal(7f, Get<float>(node, "Gain"));
            Assert.Equal(4, Get<int>(node, "AvgCount"));
            Assert.Equal(CVImageFlipMode.XY, Get<CVImageFlipMode>(node, "FlipMode"));
            string calibrationProperty = node is LocalCameraNode ? "CalibTempName" : "CaliTempName";
            Assert.Equal("calibration-template", Get<string>(node, calibrationProperty));
            var effective = CameraNodeCreationDefaults.CaptureEffectiveDefaults(node);
            Assert.Equal(camera.Camera.Code, effective.DeviceCode);
            Assert.Equal(new[] { 800f }, effective.ExposureMilliseconds);
        });
    }

    [Fact]
    public void CvNodeReceivesThreeIndependentExposures()
    {
        StaTest.Run(() =>
        {
            using var camera = new CurrentCameraDefaultsScope();
            CVCameraNode node = new();
            node.Create();

            Assert.True(CameraNodeCreationDefaults.InitializeFromCurrentCamera(node));
            Assert.Equal(10f, node.TempR);
            Assert.Equal(20f, node.TempG);
            Assert.Equal(30f, node.TempB);
            Assert.Equal(7f, node.Gain);
            Assert.Equal(4, node.AvgCount);
            Assert.Equal(CVImageFlipMode.XY, node.FlipMode);
            Assert.Equal("calibration-template", node.CalibTempName);
            Assert.Equal(new[] { 10f, 20f, 30f }, CameraNodeCreationDefaults.CaptureEffectiveDefaults(node).ExposureMilliseconds);
        });
    }

    [Theory]
    [InlineData(double.NaN, CVImageFlipMode.XY)]
    [InlineData(double.Epsilon, CVImageFlipMode.XY)]
    [InlineData(800, (CVImageFlipMode)1234)]
    public void InvalidSourceAcquisitionSettingsDoNotPartiallyChangeNewNode(double exposure, CVImageFlipMode flipMode)
    {
        StaTest.Run(() =>
        {
            LVCameraNode node = new();
            node.Create();
            CameraAcquisitionDefaults invalid = NewDefaults() with { ExpTime = exposure, FlipMode = flipMode };

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

internal sealed class CurrentCameraDefaultsScope : IDisposable
{
    private static readonly FieldInfo ManagerField = typeof(ServiceManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
    private readonly ServiceManager? previousManager = ServiceManager.Current;
    public DeviceCamera Camera { get; }
    public DisplayCameraConfig Display { get; } = new()
    {
        ExpTime = 800,
        ExpTimeR = 10,
        ExpTimeG = 20,
        ExpTimeB = 30,
        Gain = 3,
        AvgCount = 4,
        FlipMode = CVImageFlipMode.XY,
        CalibrationTemplateIndex = 1
    };
    public ServiceManager Manager { get; }

    public CurrentCameraDefaultsScope()
    {
        string code = "CAMERA.DEFAULTS.TEST." + Guid.NewGuid().ToString("N");
        Camera = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
        Camera.Config = new ConfigCamera { Code = code, CameraMode = cvColorVision.CameraMode.CV_MODE, TakeImageMode = cvColorVision.TakeImageMode.Measure_Normal };
        Camera.SysResourceModel = new SysResourceModel { Code = code };
        var physical = (PhyCamera)RuntimeHelpers.GetUninitializedObject(typeof(PhyCamera));
        physical.CalibrationParams = [new("calibration-template", new CalibrationParam { Id = 42, Name = "calibration-template", CalibrationMode = "group1" })];
        physical.VisualChildren = [new GroupResource(new SysResourceModel { Name = "group1", Value = "{\"Gain\":7}" })];
        typeof(DeviceCamera).GetField("_PhyCamera", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(Camera, physical);
        DisplayConfigManager.Instance.Configs[code] = Display;
        Manager = (ServiceManager)RuntimeHelpers.GetUninitializedObject(typeof(ServiceManager));
        Manager.DeviceServices = [Camera];
        ManagerField.SetValue(null, Manager);
    }

    public void Dispose()
    {
        ManagerField.SetValue(null, previousManager);
        DisplayConfigManager.Instance.Configs.TryRemove(Camera.Code, out _);
    }
}
