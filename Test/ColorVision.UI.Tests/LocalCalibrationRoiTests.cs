using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras.Configs;
using cvColorVision;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

public class LocalCalibrationRoiTests
{
    [Fact]
    public void ResolveUsesPhysicalCameraRoiForMatchingRawDimensions()
    {
        PhyCameraCfg cameraConfig = new()
        {
            PointX = 5000,
            PointY = 2200,
            Width = 800,
            Height = 700
        };

        LocalCalibrationRoi roi = LocalCalibrationRoi.ResolveForFrame(new LocalFrameMetadata { Width = 800, Height = 700 }, cameraConfig);

        Assert.True(roi.IsConfigured);
        Assert.Equal(5000, roi.X);
        Assert.Equal(2200, roi.Y);
        Assert.Equal(800, roi.Width);
        Assert.Equal(700, roi.Height);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiveFrameCalibrationUsesTheRoiSentWhenTheCameraOpened(bool cropped)
    {
        var cameraConfig = new PhyCameraCfg
        {
            PointX = cropped ? 2 : 0,
            PointY = cropped ? 3 : 0,
            Width = cropped ? 4 : 0,
            Height = cropped ? 3 : 0
        };
        JObject openedConfig = JObject.Parse(LocalCameraSession.BuildCameraConfigurationJson(cameraConfig));
        var native = new LocalCameraSessionTests.FakeNative { OpenedCalibrationRoi = LocalCameraNative.ReadCalibrationRoi(openedConfig) };
        using var session = new LocalCameraSession(native, new CameraBackendState(true));
        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("camera", TakeImageMode.Measure_Normal, 8));
        using var frame = LocalFlowFrame.Allocate(new LocalFrameMetadata
        {
            Width = 4, Height = 3, SourceBpp = 8, Channels = 1,
            CaptureCalibrationRoi = session.OpenedCalibrationRoi
        }, 12, 0);

        cameraConfig.PointX = 7;
        cameraConfig.PointY = 8;
        cameraConfig.Width = 4;
        cameraConfig.Height = 3;
        frame.PrepareForCalibration("test", 12 * sizeof(float), hasBasicCalibration: false);

        LocalCalibrationRoi roi = LocalCalibrationRoi.ResolveForFrame(frame.Metadata, cameraConfig);
        var options = OpenCvLocalCalibrationCache.CreateExecutionOptions([10], roi);
        Assert.Equal(cropped ? 2u : 0u, options.RoiX);
        Assert.Equal(cropped ? 3u : 0u, options.RoiY);
        Assert.Equal(cropped ? 4u : 0u, options.RoiWidth);
        Assert.Equal(cropped ? 3u : 0u, options.RoiHeight);
    }

    [Fact]
    public void ResolveKeepsFullFrameCalibrationForDifferentImageDimensions()
    {
        PhyCameraCfg cameraConfig = new()
        {
            PointX = 5000,
            PointY = 2200,
            Width = 800,
            Height = 700
        };

        LocalCalibrationRoi roi = LocalCalibrationRoi.Resolve(cameraConfig, 9568, 6380);

        Assert.False(roi.IsConfigured);
    }

    [Fact]
    public void ExecutionOptionsCarryRoiToNativeCalibration()
    {
        LocalCalibrationRoi roi = new(5000, 2200, 800, 700);

        var options = OpenCvLocalCalibrationCache.CreateExecutionOptions(new[] { 100f, 101f, 102f }, roi);

        Assert.Equal((uint)5000, options.RoiX);
        Assert.Equal((uint)2200, options.RoiY);
        Assert.Equal((uint)800, options.RoiWidth);
        Assert.Equal((uint)700, options.RoiHeight);
    }
}
