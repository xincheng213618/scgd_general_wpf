using ColorVision.Engine.Services.Devices.Sensor;
using Newtonsoft.Json.Linq;
using System.IO.Ports;

namespace ColorVision.UI.Tests;

// Protect the combined editor's cancel/validation boundary and the two existing storage contracts.
public sealed class SensorConfigurationDraftTests
{
    [Fact]
    public void CombinedDraftIsDetachedAndAppliesWithoutReplacingLiveInstancesOrMovingSettings()
    {
        var config = new ConfigSensor { Name = "Original", Addr = "COM7", Port = 9600, Category = "Sensor.Default" };
        var display = new DisplaySensorConfig();
        var draft = new SensorConfigurationDraft(config, display);
        draft.Config.Name = "Edited";
        draft.Config.IsNet = true;
        draft.Config.Addr = "device.internal";
        draft.Config.Port = 502;
        draft.DisplayConfig.UseLocalSensor = true;
        draft.DisplayConfig.ConnectTimeout = 2500;
        draft.DisplayConfig.DataBits = 7;
        draft.DisplayConfig.Parity = Parity.Even;
        draft.DisplayConfig.StopBits = StopBits.Two;
        draft.DisplayConfig.DtrEnable = true;
        draft.DisplayConfig.RtsEnable = true;
        Assert.Equal("Original", config.Name);
        Assert.False(display.UseLocalSensor);
        bool notified = false;
        display.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(DisplaySensorConfig.UseLocalSensor)) return;
            Assert.Equal(2500, display.ConnectTimeout);
            Assert.Equal("device.internal", config.Addr);
            notified = true;
        };
        Assert.True(draft.TryApply(config, display, out string error), error);
        Assert.True(notified);
        Assert.Equal("Edited", config.Name);
        Assert.True(config.IsNet);
        Assert.Equal(502, config.Port);
        Assert.Equal(Parity.Even, display.Parity);
        Assert.Equal(StopBits.Two, display.StopBits);
        Assert.Equal(7, display.DataBits);
        Assert.True(display.DtrEnable && display.RtsEnable);
        var payload = JObject.FromObject(config);
        foreach (string name in new[] { "UseLocalSensor", "ConnectTimeout", "DataBits", "Parity", "StopBits", "DtrEnable", "RtsEnable" })
            Assert.Null(payload[name]);
        draft.Config.Name = "Discarded";
        draft.DisplayConfig.UseLocalSensor = false;
        Assert.Equal("Edited", config.Name);
        Assert.True(display.UseLocalSensor);
    }

    [Theory]
    [InlineData(true, "", 502, 3000, 8)]
    [InlineData(true, "192.0.2.1", 65536, 3000, 8)]
    [InlineData(false, "COM7", 115200, 0, 8)]
    [InlineData(false, "COM7", 115200, 3000, 9)]
    public void InvalidLocalDraftDoesNotApplyEitherConfiguration(bool network, string address, int port, int timeout, int bits)
    {
        var config = new ConfigSensor { Name = "Original", Addr = "COM3", Port = 9600 };
        var display = new DisplaySensorConfig();
        var draft = new SensorConfigurationDraft(config, display);
        draft.Config.Name = "Edited";
        draft.Config.IsNet = network;
        draft.Config.Addr = address;
        draft.Config.Port = port;
        draft.DisplayConfig.UseLocalSensor = true;
        draft.DisplayConfig.ConnectTimeout = timeout;
        draft.DisplayConfig.DataBits = bits;
        Assert.False(draft.TryApply(config, display, out string error));
        Assert.NotEmpty(error);
        Assert.Equal("Original", config.Name);
        Assert.Equal("COM3", config.Addr);
        Assert.Equal(9600, config.Port);
        Assert.False(display.UseLocalSensor);
        Assert.Equal(3000, display.ConnectTimeout);
        Assert.Equal(8, display.DataBits);
    }
}
