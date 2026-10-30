using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.SMU.Configs;
using ColorVision.Themes;
using cvColorVision;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

// Protect cancel/apply isolation, separate configuration ownership and the service's IsNet + DevName contract.
public sealed class SmuConfigurationDraftTests
{
    [Theory]
    [InlineData(Pss_Type.Keithley_2400, 0.01)]
    [InlineData(Pss_Type.Keithley_2600, 1)]
    public void LegacyNplcKeepsDriverDefaultAndExplicitValuesRoundTrip(Pss_Type type, double expected)
    {
        var legacy = new JObject { [nameof(ConfigSMU.DevType)] = type.ToString(), [nameof(ConfigSMU.DevName)] = "COM7" }.ToObject<ConfigSMU>()!;
        Assert.Equal(expected, legacy.Nplc);
        Assert.Equal(expected, new SmuConfigurationDraft(legacy, new()).Config.Nplc);
        legacy.Nplc = 0.2;
        var payload = JObject.FromObject(legacy);
        Assert.Equal(0.2, payload.Value<double>(nameof(ConfigSMU.Nplc)));
        Assert.Null(payload[nameof(ConfigSMU.SupportsNplc)]);
        Assert.Equal(0.2, payload.ToObject<ConfigSMU>()!.Nplc);
    }

    [Fact]
    public void DriverChangesNotifyNplcAndKeepExplicitSettings()
    {
        var config = new ConfigSMU();
        List<string?> changed = [];
        config.PropertyChanged += (_, args) => changed.Add(args.PropertyName);
        config.DevType = Pss_Type.Keithley_2600;
        Assert.Equal(1, config.Nplc);
        Assert.Contains(nameof(ConfigSMU.Nplc), changed);
        Assert.Contains(nameof(ConfigSMU.SupportsNplc), changed);
        config.Nplc = 0.2;
        config.DevType = Pss_Type.Keithley_2400;
        Assert.Equal(0.2, config.Nplc);
    }

    [Theory]
    [InlineData(Pss_Type.Keithley_2400, 0.001)]
    [InlineData(Pss_Type.Keithley_2400, 10.1)]
    [InlineData(Pss_Type.Keithley_2400, double.NaN)]
    [InlineData(Pss_Type.Keithley_2600, 0.0009)]
    [InlineData(Pss_Type.Keithley_2600, 10.1)]
    [InlineData(Pss_Type.Keithley_2600, double.PositiveInfinity)]
    public void InvalidLocalNplcDoesNotApplyDeviceOrDisplayEdits(Pss_Type type, double nplc)
    {
        var config = new ConfigSMU { DevType = type, DevName = "COM7", Name = "Original" };
        var display = new DisplaySMUConfig { UseLocalSmu = false };
        string original = JsonConvert.SerializeObject(config);
        var draft = new SmuConfigurationDraft(config, display);
        draft.Config.Nplc = nplc;
        draft.Config.Name = "Edited";
        draft.DisplayConfig.UseLocalSmu = true;
        Assert.False(draft.TryApply(config, display, out string error));
        Assert.Contains("NPLC", error);
        Assert.Contains("10", error);
        Assert.Equal(original, JsonConvert.SerializeObject(config));
        Assert.False(display.UseLocalSmu);
    }

    [Theory]
    [InlineData(false, Pss_Type.Keithley_2400)]
    [InlineData(true, Pss_Type.Precise_S100)]
    public void ServiceAndUnsupportedDriversDoNotValidateInactiveNplc(bool local, Pss_Type type)
    {
        var config = new ConfigSMU { DevType = type, DevName = "COM7" };
        var display = new DisplaySMUConfig { UseLocalSmu = local };
        var draft = new SmuConfigurationDraft(config, display);
        draft.Config.Nplc = 0;
        Assert.True(draft.TryApply(config, display, out string error), error);
        Assert.Equal(0, config.Nplc);
    }

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
        var display = new DisplaySMUConfig { UseLocalSmu = false, LocalBaudRate = SMUSerialBaudRate.Baud115200 };
        var channel = display.ChannelA;
        var source = channel.VoltageSource;
        var draft = new SmuConfigurationDraft(config, display);
        draft.SerialPortName = "COM23";
        draft.DisplayConfig.UseLocalSmu = true;
        draft.DisplayConfig.LocalBaudRate = SMUSerialBaudRate.Baud19200;
        draft.DisplayConfig.IsUseLimitSigned = false;
        draft.DisplayConfig.IsSourceV = false;
        draft.DisplayConfig.Channel = ColorVision.Engine.Services.Devices.SMU.Dao.SMUChannelType.B;
        draft.Config.Nplc = 0.2;
        Assert.False(display.UseLocalSmu);
        Assert.Equal(SMUSerialBaudRate.Baud115200, display.LocalBaudRate);
        Assert.True(display.IsUseLimitSigned);
        Assert.Equal("COM7", config.DevName);
        Assert.Equal(1, config.Nplc);
        // Incoming measurements can change while the draft is open.
        channel.V = 12;
        source.MeasureVal = 17;
        Assert.True(draft.TryApply(config, display, out string error), error);
        Assert.Equal("COM23", config.DevName);
        Assert.Equal(0.2, config.Nplc);
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
        Assert.Equal(0.2, devicePayload.Value<double>(nameof(ConfigSMU.Nplc)));
        Assert.Null(JObject.FromObject(display)[nameof(ConfigSMU.Nplc)]);
        var restored = JObject.FromObject(display).ToObject<DisplaySMUConfig>()!;
        Assert.True(restored.UseLocalSmu);
        Assert.Equal(SMUSerialBaudRate.Baud19200, restored.LocalBaudRate);
        Assert.False(restored.IsUseLimitSigned);
        draft.DisplayConfig.UseLocalSmu = false;
        draft.Config.Nplc = 0.3;
        Assert.True(display.UseLocalSmu);
        Assert.Equal(0.2, config.Nplc);
    }

    [Fact]
    public void NplcEditorUsesMetadataAndOnlyAppearsForSupportedLocalDevices()
    {
        WpfTestHost.Invoke(() =>
        {
            List<ResourceDictionary> dictionaries = [];
            EditSMU? window = null;
            try
            {
                foreach (string uri in ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase))
                {
                    var dictionary = new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) };
                    dictionaries.Add(dictionary);
                    Application.Current.Resources.MergedDictionaries.Add(dictionary);
                }
                var config = new ConfigSMU { DevType = Pss_Type.Keithley_2400, DevName = "COM7" };
                var display = new DisplaySMUConfig { UseLocalSmu = true };
                window = new EditSMU(config, display);
                window.ShowActivated = false; window.ShowInTaskbar = false; window.Left = -10000; window.Top = -10000;
                window.Show(); window.UpdateLayout();
                var section = Assert.IsType<StackPanel>(window.FindName("NplcFields"));
                var field = Assert.IsType<DockPanel>(Assert.Single(section.Children.Cast<UIElement>()));
                Assert.Equal(Visibility.Visible, section.Visibility);
                Assert.Equal(Visibility.Visible, field.Visibility);
                var input = Assert.Single(field.Children.OfType<TextBox>());
                input.Text = 0.2.ToString();
                input.GetBindingExpression(TextBox.TextProperty)!.UpdateSource();
                Assert.Equal(0.2, window.EditConfig.Nplc);
                Assert.Equal(0.01, config.Nplc);
                window.Draft.DisplayConfig.UseLocalSmu = false;
                BindingOperations.GetBindingExpression(section, UIElement.VisibilityProperty)!.UpdateTarget();
                Assert.Equal(Visibility.Collapsed, section.Visibility);
                window.Draft.DisplayConfig.UseLocalSmu = true;
                BindingOperations.GetBindingExpression(section, UIElement.VisibilityProperty)!.UpdateTarget();
                window.EditConfig.DevType = Pss_Type.Precise_S100;
                BindingOperations.GetBindingExpression(field, UIElement.VisibilityProperty)!.UpdateTarget();
                Assert.Equal(Visibility.Collapsed, field.Visibility);
                window.EditConfig.DevType = Pss_Type.Keithley_2600;
                BindingOperations.GetBindingExpression(field, UIElement.VisibilityProperty)!.UpdateTarget();
                Assert.Equal(Visibility.Visible, field.Visibility);
                string? preview = Environment.GetEnvironmentVariable("CV_SMU_CONFIG_PREVIEW_PATH");
                if (!string.IsNullOrEmpty(preview))
                {
                    window.UpdateLayout();
                    var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(window);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var output = File.Create(preview); encoder.Save(output);
                }
            }
            finally
            {
                window?.Close();
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            }
        });
    }

    [Fact]
    public void InvalidDeviceAddressDoesNotApplyDisplayEdits()
    {
        var config = new ConfigSMU { DevName = "COM7", DevType = Pss_Type.Keithley_2600 };
        var display = new DisplaySMUConfig { UseLocalSmu = false };
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
