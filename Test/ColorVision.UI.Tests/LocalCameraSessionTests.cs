using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras.Configs;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

public class LocalCameraSessionTests
{
    [Fact]
    public void CameraConfigurationJson_MapsPhysicalCameraSettingsAndRoi()
    {
        PhyCameraCfg config = new()
        {
            Ob = 4,
            ObR = 5,
            ObT = 6,
            ObB = 7,
            TempCtlChecked = true,
            TargetTemp = 8.5f,
            TempSpanTime = 42,
            UsbTraffic = 9.5f,
            Offset = 10,
            Gain = 11,
            PointX = 12,
            PointY = 13,
            Width = 640,
            Height = 480,
            SensorWidth = 9568,
            SensorHeight = 6380
        };

        JObject root = JObject.Parse(LocalCameraSession.BuildCameraConfigurationJson(config));
        JObject cameraCfg = Assert.IsType<JObject>(root["cameraCfg"]);

        Assert.Equal(4, cameraCfg.Value<int>("ob"));
        Assert.Equal(5, cameraCfg.Value<int>("obR"));
        Assert.Equal(6, cameraCfg.Value<int>("obT"));
        Assert.Equal(7, cameraCfg.Value<int>("obB"));
        Assert.True(cameraCfg.Value<bool>("tempCtlChecked"));
        Assert.Equal(8.5f, cameraCfg.Value<float>("targetTemp"));
        Assert.Equal(42, cameraCfg.Value<int>("TempSpanTime"));
        Assert.Equal(9.5f, cameraCfg.Value<float>("usbTraffic"));
        Assert.Equal(10, cameraCfg.Value<int>("offset"));
        Assert.Equal(11, cameraCfg.Value<int>("gain"));
        Assert.Equal(12, cameraCfg.Value<int>("ex"));
        Assert.Equal(13, cameraCfg.Value<int>("ey"));
        Assert.Equal(640, cameraCfg.Value<int>("ew"));
        Assert.Equal(480, cameraCfg.Value<int>("eh"));
        Assert.Equal(14, cameraCfg.Properties().Count());
    }

    [Fact]
    public void CameraConfigurationJson_PreservesZeroRoiForFullFrame()
    {
        PhyCameraCfg config = new()
        {
            PointX = 0,
            PointY = 0,
            Width = 0,
            Height = 0
        };

        JObject root = JObject.Parse(LocalCameraSession.BuildCameraConfigurationJson(config));
        JObject cameraCfg = Assert.IsType<JObject>(root["cameraCfg"]);

        Assert.Equal(0, cameraCfg.Value<int>("ex"));
        Assert.Equal(0, cameraCfg.Value<int>("ey"));
        Assert.Equal(0, cameraCfg.Value<int>("ew"));
        Assert.Equal(0, cameraCfg.Value<int>("eh"));
    }
}
