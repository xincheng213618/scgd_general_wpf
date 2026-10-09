using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using cvColorVision;
using Newtonsoft.Json.Linq;

namespace ColorVision.UI.Tests;

// Protect cancel/apply isolation, separate configuration ownership and the service's IsNet + DevName contract.
public sealed class SmuConfigurationDraftTests
{
    [Theory]
    [InlineData(false, "COM7", "192.168.100.100")]
    [InlineData(true, "192.0.2.7", "192.0.2.7")]
    [InlineData(true, "", "192.168.100.100")]
    [InlineData(true, " ", "192.168.100.100")]
    public void LegacyConfigurationPreservesSelectedAddressAndDefaultsUnconfiguredIp(bool network, string address, string expectedIp)
    {
        var config = new JObject { ["IsNet"] = network, ["DevName"] = address, ["DevType"] = "Keithley_2600" }.ToObject<ConfigSMU>()!;
        var display = new DisplaySMUConfig();
        var draft = new SmuConfigurationDraft(config, display);
        Assert.Equal(network, draft.IsNet);
        Assert.Equal(!network, draft.IsSerialConnection);
        Assert.Equal(network ? string.Empty : address, draft.SerialPortName);
        Assert.Equal(expectedIp, draft.IpAddress);
        Assert.Equal(address, config.DevName);
        if (!network) draft.IsNet = true;
        Assert.True(draft.TryApply(config, display, out string error), error);
        Assert.True(config.IsNet);
        Assert.Equal(expectedIp, config.DevName);
    }

    [Theory]
    [InlineData(false, "COM23")]
    [InlineData(true, "192.0.2.17")]
    public void ModeSwitchRemembersInputsAndAppliesOnlySelectedAddressToLegacyFields(bool network, string expected)
    {
        var config = new ConfigSMU { Name = "Original", DevType = Pss_Type.Keithley_2600, DevName = "COM7", Code = "SMU-1", SN = "SN-1" };
        var display = new DisplaySMUConfig();
        var draft = new SmuConfigurationDraft(config, display) { SerialPortName = " com23 " };
        draft.IsNet = true;
        draft.IpAddress = " 192.0.2.17 ";
        draft.IsSerialConnection = true;
        Assert.Equal(" com23 ", draft.SerialPortName);
        draft.IsNet = network;
        draft.Config.Name = "Edited";
        draft.Config.Is4Wire = true;
        Assert.Equal("Original", config.Name);
        Assert.False(config.Is4Wire);
        Assert.False(config.IsNet);
        Assert.Equal("COM7", config.DevName);

        int notifications = 0;
        config.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(ConfigSMU.DevName)) notifications++; };
        Assert.True(draft.TryApply(config, display, out string error), error);
        Assert.True(notifications > 0);
        Assert.Equal(network, config.IsNet);
        Assert.Equal(expected, config.DevName);
        Assert.Equal("SMU-1", config.Code);
        Assert.Equal("SN-1", config.SN);
        Assert.Equal("Edited", config.Name);
        Assert.True(config.Is4Wire);

        var payload = JObject.FromObject(config);
        Assert.Equal(network, payload.Value<bool>("IsNet"));
        Assert.Equal(expected, payload.Value<string>("DevName"));
        Assert.Null(payload[nameof(ConfigSMU.SerialPort)]);
        Assert.Null(payload[nameof(ConfigSMU.IpAddress)]);
        Assert.Null(payload[nameof(SmuConfigurationDraft.SerialPortName)]);
        draft.Config.Name = "Discarded";
        Assert.Equal("Edited", config.Name);
    }

    [Theory]
    [InlineData(true, "COM3")]
    [InlineData(true, "192.0.2.17:5025")]
    [InlineData(true, "http://192.0.2.17")]
    [InlineData(true, "192.0.2.300")]
    [InlineData(false, "192.0.2.17")]
    [InlineData(false, "")]
    public void InvalidSelectedAddressDoesNotApplyAnyDraftEdits(bool network, string address)
    {
        var config = new ConfigSMU { Name = "Original", DevType = Pss_Type.Keithley_2400, DevName = "COM7" };
        var display = new DisplaySMUConfig();
        var draft = new SmuConfigurationDraft(config, display) { IsNet = network, SerialPortName = address, IpAddress = address };
        draft.Config.Name = "Edited";
        Assert.False(draft.TryApply(config, display, out string error));
        Assert.NotEmpty(error);
        Assert.False(config.IsNet);
        Assert.Equal("COM7", config.DevName);
        Assert.Equal("Original", config.Name);
    }

    [Fact]
    public void CombinedEditorAppliesBothClassesWithoutMovingStorageOrReplacingLiveChannelState()
    {
        var config = new ConfigSMU { DevName = "COM7", DevType = Pss_Type.Keithley_2600 };
        var display = new DisplaySMUConfig { LocalBaudRate = SMUSerialBaudRate.Baud115200 };
        var channel = display.ChannelA;
        var source = channel.VoltageSource;
        var draft = new SmuConfigurationDraft(config, display);
        draft.SerialPortName = "COM23";
        draft.DisplayConfig.UseLocalSmu = true;
        draft.DisplayConfig.LocalBaudRate = SMUSerialBaudRate.Baud19200;
        draft.DisplayConfig.IsUseLimitSigned = false;
        draft.DisplayConfig.IsSourceV = false;
        draft.DisplayConfig.Channel = ColorVision.Engine.Services.Devices.SMU.Dao.SMUChannelType.B;
        Assert.False(display.UseLocalSmu);
        Assert.Equal(SMUSerialBaudRate.Baud115200, display.LocalBaudRate);
        Assert.True(display.IsUseLimitSigned);
        Assert.Equal("COM7", config.DevName);
        // Incoming measurements can change while the draft is open.
        channel.V = 12;
        source.MeasureVal = 17;
        Assert.True(draft.TryApply(config, display, out string error), error);
        Assert.Equal("COM23", config.DevName);
        Assert.True(display.UseLocalSmu);
        Assert.Equal(SMUSerialBaudRate.Baud19200, display.LocalBaudRate);
        Assert.False(display.IsUseLimitSigned);
        Assert.False(display.IsSourceV);
        Assert.Equal(ColorVision.Engine.Services.Devices.SMU.Dao.SMUChannelType.B, display.Channel);
        Assert.Same(channel, display.ChannelA);
        Assert.Same(source, display.ChannelA.VoltageSource);
        Assert.Equal(12d, display.ChannelA.V);
        Assert.Equal(17d, display.ChannelA.VoltageSource.MeasureVal);
        var devicePayload = JObject.FromObject(config);
        Assert.Null(devicePayload[nameof(DisplaySMUConfig.UseLocalSmu)]);
        Assert.Null(devicePayload[nameof(DisplaySMUConfig.LocalBaudRate)]);
        var restored = JObject.FromObject(display).ToObject<DisplaySMUConfig>()!;
        Assert.True(restored.UseLocalSmu);
        Assert.Equal(SMUSerialBaudRate.Baud19200, restored.LocalBaudRate);
        Assert.False(restored.IsUseLimitSigned);
        draft.DisplayConfig.UseLocalSmu = false;
        Assert.True(display.UseLocalSmu);
    }

    [Fact]
    public void InvalidDeviceAddressDoesNotApplyDisplayEdits()
    {
        var config = new ConfigSMU { DevName = "COM7", DevType = Pss_Type.Keithley_2600 };
        var display = new DisplaySMUConfig();
        var draft = new SmuConfigurationDraft(config, display) { SerialPortName = "invalid" };
        draft.DisplayConfig.UseLocalSmu = true;
        draft.DisplayConfig.LocalBaudRate = SMUSerialBaudRate.Baud115200;
        Assert.False(draft.TryApply(config, display, out string error));
        Assert.NotEmpty(error);
        Assert.False(display.UseLocalSmu);
        Assert.Equal(SMUSerialBaudRate.Baud9600, display.LocalBaudRate);
        Assert.Equal("COM7", config.DevName);
    }
}
