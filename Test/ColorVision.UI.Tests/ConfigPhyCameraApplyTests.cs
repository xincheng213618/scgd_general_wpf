using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.PhyCameras.Configs;
using cvColorVision;

namespace ColorVision.UI.Tests;

public sealed class ConfigPhyCameraApplyTests
{
    [Fact]
    public void ApplyToPreservesLocalCameraIdByDefault()
    {
        ConfigCamera target = new()
        {
            CameraID = "LOCAL-CAMERA-ID"
        };
        ConfigPhyCamera source = new()
        {
            CameraID = "PHYSICAL-CAMERA-ID",
            CameraModel = CameraModel.HK_USB,
            ImageBpp = ImageBpp.bpp16
        };

        source.ApplyTo(target);

        Assert.Equal("LOCAL-CAMERA-ID", target.CameraID);
        Assert.Equal(CameraModel.HK_USB, target.CameraModel);
        Assert.Equal(ImageBpp.bpp16, target.ImageBpp);
    }

    [Fact]
    public void ApplyToCanCopyCameraIdExplicitly()
    {
        ConfigCamera target = new()
        {
            CameraID = "LOCAL-CAMERA-ID"
        };
        ConfigPhyCamera source = new()
        {
            CameraID = "PHYSICAL-CAMERA-ID"
        };

        source.ApplyTo(target, includeCameraId: true);

        Assert.Equal("PHYSICAL-CAMERA-ID", target.CameraID);
    }
}
