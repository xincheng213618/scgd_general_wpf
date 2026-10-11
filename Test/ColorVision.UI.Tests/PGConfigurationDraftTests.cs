using ColorVision.Engine.Services.Devices.PG;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

// Protect cancellation, failed validation and the service's existing Addr / Port storage contract.
public sealed class PGConfigurationDraftTests
{
    [Fact]
    public void SeparateTransportDraftsApplyOnlyTheSelectedEndpointWithoutReplacingTheLiveConfiguration()
    {
        var config = new ConfigPG { Name = "Original", Code = "pg", SN = "serial", Category = "COMM.GXCLCM", Addr = "COM7", Port = 9600, SendTopic = "send", SubscribeTopic = "receive" };
        string original = JsonConvert.SerializeObject(config);
        var draft = new PGConfigurationDraft(config);
        draft.Config.Name = "Edited";
        draft.NetworkAddress = "pg.internal";
        draft.NetworkPort = 5555;
        draft.I2CDeviceIndex = "2";
        draft.I2CDeviceAddress = 64;
        draft.Config.IsNet = true;
        draft.Config.Category = "CH431.I2C";
        draft.Config.Category = "COMM.GXCLCM";
        draft.Config.IsNet = false;
        Assert.Equal("COM7", draft.SerialPortName);
        Assert.Equal(9600, draft.SerialBaudRate);
        draft.Config.IsNet = true;
        Assert.Equal("pg.internal", draft.NetworkAddress);
        Assert.Equal(5555, draft.NetworkPort);
        Assert.Equal(original, JsonConvert.SerializeObject(config));
        bool notified = false;
        config.PropertyChanged += (_, _) => notified = true;
        Assert.True(draft.TryApply(config, out string error), error);
        Assert.True(notified);
        Assert.Equal("Edited", config.Name);
        Assert.Equal("pg.internal", config.Addr);
        Assert.Equal(5555, config.Port);
        Assert.True(config.IsNet);
        Assert.Equal("send", config.SendTopic);
        Assert.Equal("receive", config.SubscribeTopic);
        var payload = JObject.FromObject(config);
        foreach (string name in new[] { "NetworkAddress", "NetworkPort", "SerialPortName", "SerialBaudRate", "I2CDeviceIndex", "I2CDeviceAddress" }) Assert.Null(payload[name]);
        draft.Config.Name = "Discarded";
        Assert.Equal("Edited", config.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExistingI2CValuesAreNotResetWhenOpeningOrChangingConnectionMode(bool isNet)
    {
        var config = new ConfigPG { Category = "CH431.I2C", IsNet = isNet, Addr = "3", Port = 64, RegisterAddress = 42, BaudRate = CH341_Stream_Speed.CH341_400KHZ };
        var draft = new PGConfigurationDraft(config);
        draft.Config.Category = "GECS.V2.4";
        Assert.True(draft.ShowRegisterAddress);
        Assert.Equal(ColorVision.Engine.Properties.Resources.Channel, draft.RegisterAddressLabel);
        draft.Config.IsNet = !isNet;
        draft.Config.Category = "CH431.I2C";
        Assert.True(draft.IsI2C);
        Assert.False(draft.IsNetworkConnection || draft.IsSerialConnection);
        Assert.Equal("3", draft.I2CDeviceIndex);
        Assert.Equal(64, draft.I2CDeviceAddress);
        Assert.True(draft.TryApply(config, out string error), error);
        Assert.Equal("3", config.Addr);
        Assert.Equal(64, config.Port);
        Assert.Equal(42, config.RegisterAddress);
        Assert.Equal(CH341_Stream_Speed.CH341_400KHZ, config.BaudRate);
    }

    [Theory]
    [InlineData("COMM.GXCLCM", true, "", 5555)]
    [InlineData("COMM.GXCLCM", true, "http://pg.internal:5555", 5555)]
    [InlineData("COMM.GXCLCM", true, "pg.internal", 65536)]
    [InlineData("COMM.GXCLCM", true, "pg.internal", 0)]
    [InlineData("COMM.GXCLCM", false, "COM7", 0)]
    [InlineData("COMM.GXCLCM", false, "", 9600)]
    [InlineData("CH431.I2C", false, "-1", 8)]
    [InlineData("CH431.I2C", false, "0", 256)]
    public void InvalidSelectedTransportDoesNotWriteAnyLiveField(string category, bool isNet, string address, int port)
    {
        var config = new ConfigPG { Name = "Original", Category = "COMM.GXCLCM", Addr = "COM7", Port = 9600 };
        string original = JsonConvert.SerializeObject(config);
        var draft = new PGConfigurationDraft(config);
        draft.Config.Name = "Edited";
        draft.Config.Category = category;
        draft.Config.IsNet = isNet;
        draft.NetworkAddress = draft.SerialPortName = draft.I2CDeviceIndex = address;
        draft.NetworkPort = draft.SerialBaudRate = draft.I2CDeviceAddress = port;
        Assert.False(draft.TryApply(config, out string error));
        Assert.NotEmpty(error);
        Assert.Equal(original, JsonConvert.SerializeObject(config));
    }

    [Theory]
    [InlineData(" 192.0.2.8 ")]
    [InlineData("pg.internal")]
    [InlineData("::1")]
    public void NetworkModeRetainsIpAndHostCompatibility(string address)
    {
        var config = new ConfigPG { Category = "COMM.GXCLCM", IsNet = true, Addr = address, Port = 5555 };
        var draft = new PGConfigurationDraft(config);
        Assert.True(draft.TryApply(config, out string error), error);
        Assert.Equal(address.Trim(), config.Addr);
    }
}
