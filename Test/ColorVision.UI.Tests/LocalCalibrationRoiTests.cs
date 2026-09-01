using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras.Configs;

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

        LocalCalibrationRoi roi = LocalCalibrationRoi.Resolve(cameraConfig, 800, 700);

        Assert.True(roi.IsConfigured);
        Assert.Equal(5000, roi.X);
        Assert.Equal(2200, roi.Y);
        Assert.Equal(800, roi.Width);
        Assert.Equal(700, roi.Height);
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
