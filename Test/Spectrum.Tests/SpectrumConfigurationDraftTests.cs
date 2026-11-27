using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Calibration;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Tests;
using Newtonsoft.Json.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

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
        draft.Config.ShutterCfg.UseFilterWheel = true;
        draft.Config.ShutterCfg.FilterWheelDeviceCode = "ND-1";
        draft.Config.ShutterCfg.DarkPort = 2;
        draft.Config.NDConfig.NDRate[0] = 9;
        draft.Config.NDConfig.NDCaliNameGroups.Clear();
        draft.Config.CalibrationGroups.Add(new SpectrumCalibrationGroup { GroupName = "Other" });
        Assert.Equal("SN-1", config.SN);
        Assert.Equal("original.dat", config.ActiveCalibrationGroup.WavelengthFile);
        Assert.Equal(9600, config.ShutterCfg.BaudRate);
        Assert.False(config.ShutterCfg.UseFilterWheel);
        Assert.True(config.ShutterCfg.OpenForDark);
        Assert.Equal(string.Empty, config.ShutterCfg.FilterWheelDeviceCode);
        Assert.Equal(0, config.ShutterCfg.DarkPort);
        Assert.Equal(2, Assert.Single(config.NDConfig.NDRate));
        Assert.Single(config.NDConfig.NDCaliNameGroups);
        Assert.Single(config.CalibrationGroups);
    }

    [Theory]
    [InlineData(false, null, null)]
    [InlineData(true, 100, 1000)]
    public void SavingServiceChangesWritesFixedExposureLimitsOnlyForEnabledNd(bool enabled, int? minimum, int? maximum)
    {
        var json = JObject.Parse("""{"NDConfig":{"NDMinExpTime":10,"NDMaxExpTime":300,"LocalNDRates":{"2":64},"LocalNDPortCount":12}}""");
        var config = json.ToObject<ConfigSpectrum>()!;
        config.NDConfig.EnableResetND = true;
        config.NDConfig.IsNDPort = enabled;
        var draft = new SpectrumConfigurationDraft(config);
        draft.Config.Name = "Renamed";

        Assert.True(draft.TryApply(config, out string error), error);
        Assert.True(config.NDConfig.EnableResetND);
        var nd = Assert.IsType<JObject>(JObject.FromObject(config)[nameof(ConfigSpectrum.NDConfig)]);
        Assert.Equal(minimum, (int?)nd["NDMinExpTime"]);
        Assert.Equal(maximum, (int?)nd["NDMaxExpTime"]);
        Assert.Equal(64, config.NDConfig.LocalNDRates![2]);
        Assert.Equal(12, config.NDConfig.LocalNDPortCount);
        Assert.DoesNotContain(PropertyEditorHelper.GetEditableProperties(typeof(NDConfig)), property =>
            property.Name is "NDMinExpTime" or "NDMaxExpTime");
    }

    [Fact]
    public void CalibrationEditorUsesTheRegisteredWheelsHolesAndUpdatesOnlyTheSelectedAttenuation()
    {
        WithCalibrationEditorTheme(() =>
        {
            const string newWheel = "ND.SparseWheel";
            const string savedWheel = "SAVED-WHEEL";
            var camera = (PhyCamera)RuntimeHelpers.GetUninitializedObject(typeof(PhyCamera));
            camera.SysResourceModel = new SysResourceModel { Code = "PHY.CFW.SPECTRUM" };
            camera.Config = new ConfigPhyCamera();
            camera.Config.FilterWheelConfig.HoleMapping = [
                new HoleMap { HoleIndex = 2, HoleName = "ND64" },
                new HoleMap { HoleIndex = 5, HoleName = "ND1000" }];

            var cameraManagerField = typeof(PhyCameraManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
            object? previousCameraManager = cameraManagerField.GetValue(null);
            var cameraManager = (PhyCameraManager)RuntimeHelpers.GetUninitializedObject(typeof(PhyCameraManager));
            cameraManager.PhyCameras = [camera];
            var services = ServiceManager.GetInstance();
            var previousDevices = services.DeviceServices;
            var wheel = (DeviceCfwPort)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCfwPort));
            wheel.SysResourceModel = new SysResourceModel { Code = newWheel, Name = "Sparse wheel" };
            wheel.Config = new ConfigCfwPort { SN = camera.Code, Ports = 6 };
            try
            {
                cameraManagerField.SetValue(null, cameraManager);
                services.DeviceServices = [wheel];
                var config = new ConfigSpectrum { WavelengthFile = "original-wave.dat", MaguideFile = "original-mag.dat" };
                config.EnsureCalibrationGroups();
                config.CalibrationGroups.Add(new SpectrumCalibrationGroup
                {
                    GroupName = "ND1000", NDHoleIndex = 5, WavelengthFile = "nd1000-wave.dat", MaguideFile = "nd1000-mag.dat"
                });
                config.NDConfig.IsBingNDDevice = true;
                config.NDConfig.NDBindDeviceCode = savedWheel;
                config.NDConfig.NDRate = [0, 8];
                config.NDConfig.LocalNDRates = new Dictionary<int, int> { [5] = 1000 };

                var editor = new EditSpectrum(config, new DisplaySpectrumConfig { UseLocalSpectrum = false });
                try
                {
                    Assert.Null(editor.FindName("NdTab"));
                    Assert.Null(editor.FindName("AdvancedButton"));
                    Assert.IsType<Button>(editor.FindName("ManageCalibrationGroupsButton"));
                    var connectionFields = Assert.IsType<StackPanel>(editor.FindName("ConnectionFields")).Children.OfType<DockPanel>()
                        .Select(panel => panel.Tag).OfType<PropertyInfo>().Select(property => property.Name);
                    Assert.Equal(new[] { nameof(ConfigSpectrum.SpectrometerType), nameof(ConfigSpectrum.IsAutoOpen),
                        nameof(DisplaySpectrumConfig.UseLocalSpectrum), nameof(DisplaySpectrumConfig.IsLuminousFluxMode) }, connectionFields);
                    var acquisitionFields = Assert.IsType<StackPanel>(editor.FindName("AcquisitionFields")).Children.OfType<DockPanel>();
                    Assert.DoesNotContain(acquisitionFields, panel => panel.Tag is PropertyInfo property && property.DeclaringType == typeof(DisplaySpectrumConfig));
                    editor.Draft.DisplayConfig.UseLocalSpectrum = true;

                    var window = ShowCalibrationEditor(editor.EditConfig);
                    try { EditGroup(window); }
                    finally { window.Close(); }
                    Assert.Equal("Default", editor.EditConfig.ActiveCalibrationGroupName);
                    Assert.Equal("original-wave.dat", editor.EditConfig.WavelengthFile);
                    Assert.Equal(savedWheel, editor.EditConfig.NDConfig.NDBindDeviceCode);
                    Assert.Equal(5, Assert.Single(editor.EditConfig.NDConfig.LocalNDRates!).Key);
                    Assert.False(editor.EditConfig.NDConfig.IsNDPort);

                    window = ShowCalibrationEditor(editor.EditConfig);
                    try
                    {
                        EditGroup(window);
                        Assert.True(window.TrySaveChanges(out string error), error);
                    }
                    finally { window.Close(); }

                    Assert.Equal("ND2", editor.EditConfig.ActiveCalibrationGroupName);
                    Assert.Equal("new-wave.dat", editor.EditConfig.WavelengthFile);
                    Assert.Equal("new-mag.dat", editor.EditConfig.MaguideFile);
                    Assert.Equal(newWheel, editor.EditConfig.NDConfig.NDBindDeviceCode);
                    Assert.True(editor.EditConfig.NDConfig.IsBingNDDevice);
                    Assert.Equal("Default", config.ActiveCalibrationGroupName);
                    Assert.Equal(savedWheel, config.NDConfig.NDBindDeviceCode);
                    Assert.True(editor.Draft.TryApply(config, out string applyError), applyError);
                    Assert.Equal("ND2", config.ActiveCalibrationGroupName);
                    Assert.Equal(2, config.ActiveCalibrationGroup.NDHoleIndex);
                    Assert.True(config.NDConfig.IsNDPort);
                    Assert.Equal(newWheel, config.NDConfig.NDBindDeviceCode);
                    var rates = Assert.IsType<Dictionary<int, int>>(config.NDConfig.LocalNDRates);
                    Assert.Equal(new[] { 2, 5 }, rates.Keys.Order());
                    Assert.Equal(64, rates[2]);
                    Assert.Equal(1000, rates[5]);
                    Assert.Equal(new[] { 0, 8 }, config.NDConfig.NDRate);
                }
                finally { editor.Close(); }
            }
            finally
            {
                services.DeviceServices = previousDevices;
                cameraManagerField.SetValue(null, previousCameraManager);
            }

            static void EditGroup(SpectrumCalibrationGroupWindow window)
            {
                Assert.Null(window.FindName("NdRateInputs"));
                Assert.Null(window.FindName("DirectConnectionFields"));
                Assert.Null(window.FindName("ComboBoxDirectPortCount"));
                var wheelChoice = Assert.IsType<ComboBox>(window.FindName("ComboBoxCfwService"));
                var holes = Assert.IsType<ComboBox>(window.FindName("ComboBoxNDHole"));
                var enabled = Assert.IsType<CheckBox>(window.FindName("LocalNdEnabled"));
                var rate = Assert.IsType<TextBox>(window.FindName("NdAttenuationInput"));
                Assert.Single(holes.Items);

                wheelChoice.SelectedValue = newWheel;
                Assert.True(enabled.IsEnabled);
                Assert.False(rate.IsVisible);
                Assert.Equal(3, holes.Items.Count);
                holes.SelectedIndex = 2;
                Assert.Equal(5, holes.SelectedValue);
                Assert.False(window.TrySaveChanges(out string duplicateHoleError));
                Assert.NotEmpty(duplicateHoleError);
                holes.SelectedIndex = 0;
                enabled.SetCurrentValue(CheckBox.IsCheckedProperty, true);
                Assert.False(rate.IsVisible);
                holes.SelectedIndex = 1;
                Assert.Equal(2, holes.SelectedValue);
                Assert.True(rate.IsVisible);
                rate.SetCurrentValue(TextBox.TextProperty, "64");

                var groups = Assert.IsType<ComboBox>(window.FindName("ComboBoxGroups"));
                var selected = Assert.IsType<SpectrumCalibrationGroup>(groups.SelectedItem);
                Assert.Equal(2, selected.NDHoleIndex);
                selected.GroupName = "ND2";
                selected.WavelengthFile = "new-wave.dat";
                selected.MaguideFile = "new-mag.dat";
            }
        });
    }

    [Fact]
    public void AddAndRenameCalibrationGroupsUseValidatedNamesAndKeepChangesInTheDraft()
    {
        WithCalibrationEditorTheme(() =>
        {
            var config = new ConfigSpectrum();
            config.EnsureCalibrationGroups();
            var draft = new SpectrumConfigurationDraft(config);
            var window = ShowCalibrationEditor(draft.Config);
            try
            {
                var groups = Assert.IsType<ComboBox>(window.FindName("ComboBoxGroups"));
                EditName(window, "BtnAddGroup", "   ", "  Custom ND  ");
                Assert.Equal("Custom ND", Assert.IsType<SpectrumCalibrationGroup>(groups.SelectedItem).GroupName);
                Assert.Single(draft.Config.CalibrationGroups);

                EditName(window, "BtnRenameGroup", "  dEfAuLt  ", "  Renamed ND  ", "Custom ND");
                Assert.Equal("Renamed ND", Assert.IsType<SpectrumCalibrationGroup>(groups.SelectedItem).GroupName);
                Assert.Single(draft.Config.CalibrationGroups);
                Assert.True(window.TrySaveChanges(out string saveError), saveError);
                Assert.Equal("Renamed ND", draft.Config.ActiveCalibrationGroupName);
                Assert.Equal("Default", Assert.Single(config.CalibrationGroups).GroupName);
                Assert.True(draft.TryApply(config, out string applyError), applyError);
                Assert.Equal(new[] { "Default", "Renamed ND" }, config.CalibrationGroups.Select(group => group.GroupName));
                Assert.Equal("Renamed ND", config.ActiveCalibrationGroupName);
            }
            finally { window.Close(); }
        });

        static void EditName(Window owner, string buttonName, string invalid, string valid, string? initial = null)
        {
            var open = Assert.IsType<Button>(owner.FindName(buttonName));
            var interaction = owner.Dispatcher.InvokeAsync(() =>
            {
                var dialogs = owner.OwnedWindows.Cast<Window>().ToArray();
                try
                {
                    var dialog = Assert.Single(dialogs);
                    var children = Descendants(dialog).ToArray();
                    var input = Assert.Single(children.OfType<TextBox>());
                    var error = Assert.Single(children.OfType<TextBlock>(), text => text.Name == "NameError");
                    var confirm = Assert.Single(children.OfType<Button>(), button => button.IsDefault);
                    if (initial != null) Assert.Equal(initial, input.Text);

                    input.Text = invalid;
                    Click(confirm);
                    Assert.True(dialog.IsVisible);
                    Assert.Equal(Visibility.Visible, error.Visibility);

                    input.Text = valid;
                    Click(confirm);
                    Assert.False(dialog.IsVisible);
                }
                finally
                {
                    foreach (var dialog in dialogs.Where(dialog => dialog.IsVisible)) dialog.Close();
                }
            }, DispatcherPriority.ApplicationIdle);
            Click(open);
            owner.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            interaction.Task.GetAwaiter().GetResult();
        }

        static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
            {
                yield return child;
                foreach (var descendant in Descendants(child)) yield return descendant;
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CalibrationSavePreservesTheLegacyDirectWheel(bool disableAutoSwitch)
    {
        WithCalibrationEditorTheme(() =>
        {
            var config = new ConfigSpectrum { WavelengthFile = "original-wave.dat", MaguideFile = "original-mag.dat" };
            config.EnsureCalibrationGroups();
            config.ActiveCalibrationGroup.NDHoleIndex = 8;
            config.NDConfig.IsNDPort = true;
            config.NDConfig.IsBingNDDevice = false;
            config.NDConfig.SzComName = "COM5";
            config.NDConfig.BaudRate = 19200;
            config.NDConfig.LocalNDPortCount = disableAutoSwitch ? null : 12;
            config.NDConfig.DarkNDPort = 1;
            config.NDConfig.EnableResetND = true;
            config.NDConfig.LocalNDRates = new Dictionary<int, int> { [8] = 256 };
            JObject originalNd = JObject.FromObject(config.NDConfig);

            var window = ShowCalibrationEditor(config);
            try
            {
                var groups = Assert.IsType<ComboBox>(window.FindName("ComboBoxGroups"));
                var group = Assert.IsType<SpectrumCalibrationGroup>(groups.SelectedItem);
                var holes = Assert.IsType<ComboBox>(window.FindName("ComboBoxNDHole"));
                Assert.Equal(8, holes.SelectedValue);
                if (disableAutoSwitch)
                {
                    var enabled = Assert.IsType<CheckBox>(window.FindName("LocalNdEnabled"));
                    enabled.SetCurrentValue(CheckBox.IsCheckedProperty, false);
                }
                group.MaguideFile = "new-mag.dat";
                Assert.True(window.TrySaveChanges(out string error), error);
                Assert.Equal("new-mag.dat", config.MaguideFile);
                if (disableAutoSwitch)
                {
                    originalNd[nameof(NDConfig.IsNDPort)] = false;
                    originalNd[nameof(NDConfig.LocalNDPortCount)] = 12;
                    originalNd.Remove("NDMinExpTime");
                    originalNd.Remove("NDMaxExpTime");
                    Assert.True(config.NDConfig.HasConfiguredWheel);
                }
                Assert.True(JToken.DeepEquals(originalNd, JObject.FromObject(config.NDConfig)));
            }
            finally { window.Close(); }
        });
    }

    private static SpectrumCalibrationGroupWindow ShowCalibrationEditor(ConfigSpectrum config)
    {
        var window = new SpectrumCalibrationGroupWindow(config, opensLocally: true)
        {
            Left = -10000, Top = -10000, ShowInTaskbar = false, ShowActivated = false,
        };
        window.Show();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        return window;
    }

    private static void WithCalibrationEditorTheme(Action action)
    {
        WpfTestHost.Invoke(() =>
        {
            ResourceDictionary[] dictionaries = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
                .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
            foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Add(dictionary);
            try { action(); }
            finally
            {
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            }
        });
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
    [InlineData(" 0 ", null)]
    [InlineData(" 0 ", " LEGACY ")]
    [InlineData("999", " LEGACY ")]
    public void DisplayOnlyChangesStayLocalAndNotifyTheLiveSpectrum(string savedPort, string? savedSerial)
    {
        var config = new ConfigSpectrum { ComPort = savedPort, SN = savedSerial! };
        var display = new DisplaySpectrumConfig { UseLocalSpectrum = false, Divisor = 3 };
        var draft = new SpectrumConfigurationDraft(config, display);
        bool? notifiedFluxMode = null;
        display.IsIsLuminousFluxModeChanged += (_, value) => notifiedFluxMode = value;
        var originalConfig = JObject.FromObject(config);
        Assert.False(draft.DisplayConfig.UseLocalSpectrum);
        Assert.False(draft.DisplayConfig.IsLuminousFluxMode);

        draft.DisplayConfig.UseLocalSpectrum = true;
        draft.DisplayConfig.IsLuminousFluxMode = true;
        draft.Config.SN = savedSerial?.Trim() ?? string.Empty;
        Assert.False(display.UseLocalSpectrum);
        Assert.False(display.IsLuminousFluxMode);
        Assert.Null(notifiedFluxMode);

        Assert.True(draft.TryApply(config, out _, display));
        Assert.True(draft.DisplaySettingsChanged);
        Assert.False(draft.ServiceSettingsChanged);
        Assert.True(JToken.DeepEquals(originalConfig, JObject.FromObject(config)));
        Assert.True(display.UseLocalSpectrum);
        Assert.True(display.IsLuminousFluxMode);
        Assert.True(notifiedFluxMode);
        Assert.Equal(3, display.Divisor);
        var restored = JObject.FromObject(display).ToObject<DisplaySpectrumConfig>()!;
        Assert.True(restored.UseLocalSpectrum);
        Assert.True(restored.IsLuminousFluxMode);
    }

    [Fact]
    public void InvalidServiceEditDoesNotApplyLocalDisplaySettings()
    {
        var config = new ConfigSpectrum();
        var display = new DisplaySpectrumConfig();
        var draft = new SpectrumConfigurationDraft(config, display) { IsSerialConnection = true };
        draft.DisplayConfig.UseLocalSpectrum = false;
        draft.DisplayConfig.IsLuminousFluxMode = true;

        Assert.False(draft.TryApply(config, out _, display));
        Assert.False(draft.DisplaySettingsChanged);
        Assert.False(draft.ServiceSettingsChanged);
        Assert.True(display.UseLocalSpectrum);
        Assert.False(display.IsLuminousFluxMode);
    }

    [Fact]
    public void SpectrumDeviceEditsKeepTheLatestSharedDataPath()
    {
        var config = new ConfigSpectrum { FileServerCfg = { DataBasePath = @"D:\Original" } };
        var draft = new SpectrumConfigurationDraft(config);
        config.FileServerCfg.DataBasePath = @"D:\Unified";
        draft.Config.Name = "Renamed";

        Assert.True(draft.TryApply(config, out string error), error);
        Assert.Equal("Renamed", config.Name);
        Assert.Equal(@"D:\Unified", config.FileServerCfg.DataBasePath);
    }

    [Fact]
    public void ApplyKeepsLiveChangesToUntouchedDisplaySettings()
    {
        var config = new ConfigSpectrum();
        var display = new DisplaySpectrumConfig();
        var draft = new SpectrumConfigurationDraft(config, display);
        draft.DisplayConfig.IsLuminousFluxMode = true;
        display.UseLocalSpectrum = false;

        Assert.True(draft.TryApply(config, out _, display));
        Assert.True(draft.DisplaySettingsChanged);
        Assert.False(display.UseLocalSpectrum);
        Assert.True(display.IsLuminousFluxMode);
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

    [Theory]
    [InlineData("", 5, 0)]
    [InlineData("ND-1", 5, 5)]
    [InlineData("ND-1", -1, 0)]
    [InlineData("ND-1", 5, -1)]
    public void EnabledFilterWheelRequiresBindingAndDistinctNonNegativePositions(string code, int measurementPort, int darkPort)
    {
        var config = new ConfigSpectrum();
        var draft = new SpectrumConfigurationDraft(config);
        draft.Config.IsShutterEnable = true;
        draft.Config.ShutterCfg.UseFilterWheel = true;
        draft.Config.ShutterCfg.FilterWheelDeviceCode = code;
        draft.Config.ShutterCfg.MeasurementPort = measurementPort;
        draft.Config.ShutterCfg.DarkPort = darkPort;
        Assert.False(draft.TryApply(config, out string error));
        Assert.NotEmpty(error);
        Assert.False(config.IsShutterEnable);
        // Disabled configurations can retain incomplete settings without affecting shutter behavior.
        draft.Config.IsShutterEnable = false;
        Assert.True(draft.TryApply(config, out _));
    }

    [Fact]
    public void FilterWheelSettingsStayInsideShutterAndRoundTripWithLegacyDefaults()
    {
        var config = JObject.Parse("{\"ShutterCfg\":{\"Addr\":\"COM7\",\"OpenCmd\":\"OPEN\"}}").ToObject<ConfigSpectrum>()!;
        Assert.False(config.ShutterCfg.UseFilterWheel);
        Assert.Equal(5, config.ShutterCfg.MeasurementPort);
        Assert.Equal(0, config.ShutterCfg.DarkPort);
        var draft = new SpectrumConfigurationDraft(config);
        draft.Config.IsShutterEnable = true;
        draft.Config.ShutterCfg.UseFilterWheel = true;
        draft.Config.ShutterCfg.FilterWheelDeviceCode = "ND-1";
        draft.Config.ShutterCfg.MeasurementPort = 3;
        draft.Config.ShutterCfg.DarkPort = 2;
        draft.Config.ShutterCfg.OpenForDark = false;
        Assert.True(draft.TryApply(config, out _));
        var payload = JObject.FromObject(config);
        Assert.Null(payload["UseFilterWheel"]);
        var restored = payload.ToObject<ConfigSpectrum>()!;
        Assert.True(restored.ShutterCfg.UseFilterWheel);
        Assert.Equal("ND-1", restored.ShutterCfg.FilterWheelDeviceCode);
        Assert.Equal(3, restored.ShutterCfg.MeasurementPort);
        Assert.Equal(2, restored.ShutterCfg.DarkPort);
        Assert.Equal("COM7", restored.ShutterCfg.Addr);
        Assert.Equal("OPEN", restored.ShutterCfg.OpenCmd);
        Assert.False(restored.ShutterCfg.OpenForDark);
    }

    [Theory]
    [InlineData("a", "b", "turn on", "turn off")]
    [InlineData("b", "a", "turn off", "turn on")]
    [InlineData("OPEN", "CLOSE", "turn on", "turn off")]
    public void MainAndPluginAcknowledgeTheConfiguredCommand(string openCommand, string closeCommand, string openReply, string closeReply)
    {
        var config = new ShutterConfig { OpenCmd = openCommand, CloseCmd = closeCommand };
        var pluginConfig = new global::Spectrum.Configs.ShutterConfig { OpenCmd = openCommand, CloseCmd = closeCommand, OpenForDark = false };
        Assert.Equal(openReply, config.GetExpectedResponse(true));
        Assert.Equal(closeReply, config.GetExpectedResponse(false));
        Assert.Equal(openReply, pluginConfig.GetExpectedResponse(true));
        Assert.Equal(closeReply, pluginConfig.GetExpectedResponse(false));
        var restored = JObject.FromObject(pluginConfig).ToObject<global::Spectrum.Configs.ShutterConfig>()!;
        Assert.False(restored.OpenForDark);
        Assert.Equal(openCommand, restored.OpenCmd);
        Assert.Equal(closeCommand, restored.CloseCmd);
        Assert.True(new JObject().ToObject<global::Spectrum.Configs.ShutterConfig>()!.OpenForDark);
    }
}
