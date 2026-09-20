using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using Newtonsoft.Json.Linq;

namespace Spectrum.Tests;

public sealed class SpectrumConfigurationDraftTests
{
    [Theory]
    [InlineData("0", false, "")]
    [InlineData("3", true, "COM3")]
    [InlineData("256", true, "COM256")]
    public void ExistingPortDeterminesInitialConnectionMode(string port, bool serial, string name)
    {
        var draft = new SpectrumConfigurationDraft(new ConfigSpectrum { ComPort = port });
        Assert.Equal(serial, draft.IsSerialConnection);
        Assert.Equal(name, draft.SerialPortName);
    }

    [Fact]
    public void DisabledSerialAlwaysSavesZeroAndKeepsSelectedPortInDraft()
    {
        var config = new ConfigSpectrum { ComPort = "7" };
        var draft = new SpectrumConfigurationDraft(config) { IsSerialConnection = false };
        Assert.True(draft.TryGetPort(out int port));
        Assert.Equal(0, port);
        Assert.True(draft.TryApply(config, out _));
        Assert.Equal("0", config.ComPort);
        Assert.Equal("COM7", draft.SerialPortName);
        draft.IsSerialConnection = true;
        Assert.True(draft.TryApply(config, out _));
        Assert.Equal("7", config.ComPort);
        Assert.Null(JObject.FromObject(config)["IsSerialConnection"]);
        Assert.Null(JObject.FromObject(config)["SerialPortName"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("COM0")]
    [InlineData("COM257")]
    [InlineData("COM-1")]
    [InlineData("COMabc")]
    public void EnabledSerialRequiresAnExplicitValidPort(string name)
    {
        var config = new ConfigSpectrum { Name = "before" };
        var draft = new SpectrumConfigurationDraft(config) { IsSerialConnection = true, SerialPortName = name };
        draft.Config.Name = "after";
        Assert.False(draft.TryApply(config, out string error));
        Assert.NotEmpty(error);
        Assert.Equal("before", config.Name);
        Assert.Equal("0", config.ComPort);
    }

    [Theory]
    [InlineData("COM3", "3")]
    [InlineData(" com12 ", "12")]
    [InlineData("256", "256")]
    public void EnabledSerialSavesLegacyNumericPort(string input, string expected)
    {
        var config = new ConfigSpectrum();
        var draft = new SpectrumConfigurationDraft(config) { IsSerialConnection = true, SerialPortName = input };
        draft.Config.BaudRate = 115200;
        Assert.True(draft.TryApply(config, out _));
        Assert.Equal(expected, config.ComPort);
        Assert.Equal(115200, config.BaudRate);
    }

    [Fact]
    public void InvalidBaudRateIsRejectedOnlyForSerialConnections()
    {
        var config = new ConfigSpectrum();
        var draft = new SpectrumConfigurationDraft(config) { IsSerialConnection = true, SerialPortName = "COM3" };
        draft.Config.BaudRate = 0;
        Assert.False(draft.TryApply(config, out _));
        Assert.Equal(9600, config.BaudRate);
        draft.IsSerialConnection = false;
        Assert.True(draft.TryApply(config, out _));
        Assert.Equal("0", config.ComPort);
    }

    [Fact]
    public void DraftEditsDoNotLeakThroughNestedObjectsOrCollections()
    {
        var config = new ConfigSpectrum { SN = "SN-1", WavelengthFile = "original.dat" };
        config.EnsureCalibrationGroups();
        config.NDConfig.NDRate.Add(2);
        config.NDConfig.NDCaliNameGroups.Add("ND1");
        var draft = new SpectrumConfigurationDraft(config);
        draft.Config.SN = "SN-2";
        draft.Config.WavelengthFile = "edited.dat";
        draft.Config.ShutterCfg.BaudRate = 115200;
        draft.Config.NDConfig.NDRate[0] = 9;
        draft.Config.NDConfig.NDCaliNameGroups.Clear();
        draft.Config.CalibrationGroups.Add(new SpectrumCalibrationGroup { GroupName = "Other" });
        Assert.Equal("SN-1", config.SN);
        Assert.Equal("original.dat", config.ActiveCalibrationGroup.WavelengthFile);
        Assert.Equal(9600, config.ShutterCfg.BaudRate);
        Assert.Equal(2, Assert.Single(config.NDConfig.NDRate));
        Assert.Single(config.NDConfig.NDCaliNameGroups);
        Assert.Single(config.CalibrationGroups);
    }

    [Fact]
    public void ApplyPreservesLiveSubscribersAndDoesNotShareDraftCollections()
    {
        var config = new ConfigSpectrum { SN = "SN-1" };
        config.EnsureCalibrationGroups();
        var draft = new SpectrumConfigurationDraft(config);
        draft.Config.SN = " SN-2 ";
        draft.Config.CalibrationGroups.Add(new SpectrumCalibrationGroup { GroupName = "Other" });
        int notifications = 0;
        config.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(config.SN)) notifications++; };
        Assert.True(draft.TryApply(config, out _));
        Assert.Equal("SN-2", config.SN);
        Assert.True(notifications > 0);
        Assert.Equal(2, config.CalibrationGroups.Count);
        draft.Config.CalibrationGroups.Clear();
        Assert.Equal(2, config.CalibrationGroups.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ApplyKeepsOnlyTheSelectedNdConnection(bool bound)
    {
        var config = new ConfigSpectrum();
        var draft = new SpectrumConfigurationDraft(config);
        draft.Config.NDConfig.IsBingNDDevice = bound;
        draft.Config.NDConfig.NDBindDeviceCode = "ND-1";
        draft.Config.NDConfig.SzComName = "COM5";
        Assert.True(draft.TryApply(config, out _));
        Assert.Equal(bound ? "ND-1" : "", config.NDConfig.NDBindDeviceCode);
        Assert.Equal(bound ? "" : "COM5", config.NDConfig.SzComName);
    }
}
