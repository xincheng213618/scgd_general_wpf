using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.PhyCameras.Configs;
using cvColorVision;
using Newtonsoft.Json;

namespace ColorVision.UI.Tests;

public sealed class ConfigPhyCameraApplyTests
{
    [Theory]
    [InlineData(CameraMode.LV_MODE, ImageChannel.Three, ImageChannel.One)]
    [InlineData(CameraMode.BV_MODE, ImageChannel.One, ImageChannel.Three)]
    [InlineData(CameraMode.CV_MODE, ImageChannel.One, ImageChannel.Three)]
    [InlineData(CameraMode.LVTOBV_MODE, ImageChannel.One, ImageChannel.Three)]
    public void MeasurementChannelCountFollowsModeEvenForLegacySettings(CameraMode mode, ImageChannel stored, ImageChannel expected)
    {
        foreach (string json in new[]
        {
            $"{{\"CameraMode\":{(int)mode},\"Channel\":{(int)stored}}}",
            $"{{\"Channel\":{(int)stored},\"CameraMode\":{(int)mode}}}"
        })
        {
            var physical = JsonConvert.DeserializeObject<ConfigPhyCamera>(json)!;
            var logical = JsonConvert.DeserializeObject<ConfigCamera>(json)!;
            Assert.Equal(expected, physical.Channel);
            Assert.Equal(expected, logical.Channel);
            physical.ApplyTo(logical);
            Assert.Equal(expected, logical.Channel);
            Assert.Equal(expected == ImageChannel.Three, logical.IsChannelThree);
        }
    }

    [Fact]
    public void ApplyToSynchronizesPhysicalCameraIdByDefault()
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

        Assert.Equal("PHYSICAL-CAMERA-ID", target.CameraID);
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
