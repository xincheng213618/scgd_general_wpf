using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.PG;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Engine.Services.Types;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public class LocalDeviceDisplayConfigTests
{
    [Theory]
    [InlineData(ServiceTypes.Camera, "IsAutoOpen")]
    [InlineData(ServiceTypes.Spectrum, "IsAutoOpen")]
    [InlineData(ServiceTypes.SMU, "IsAutoStart")]
    [InlineData(ServiceTypes.Sensor, "IsAutoOpen")]
    [InlineData(ServiceTypes.PG, "IsAutoOpen")]
    [InlineData(ServiceTypes.FilterWheel, "IsAutoOpen")]
    public void NewDeviceResourceDisablesAutoConnect(ServiceTypes serviceType, string field)
    {
        Assert.True(DeviceServiceFactoryRegistry.TryGetFactory(serviceType, out var factory));
        var config = factory!.CreateConfig(new DeviceServiceCreateContext("new-device", "New device", "cmd", "status"));
        Assert.Equal(false, (bool?)JObject.FromObject(config)[field]);
    }

    [Fact]
    public void NewPgAndFilterWheelSeedOnlyMissingDisplayConfigs()
    {
        VerifyCreationPreference<DisplayPGConfig>(ServiceTypes.PG, nameof(DisplayPGConfig.UseLocalPG));
        VerifyCreationPreference<DisplayCfwPortConfig>(ServiceTypes.FilterWheel, nameof(DisplayCfwPortConfig.UseLocalCfwPort));
    }

    [Fact]
    public void SpectrumBackendAndFluxModeAreEditableInDisplayConfiguration()
    {
        var properties = PropertyEditorHelper.GetEditableProperties(typeof(DisplaySpectrumConfig));
        Assert.Contains(properties, property => property.Name == nameof(DisplaySpectrumConfig.UseLocalSpectrum));
        Assert.Contains(properties, property => property.Name == nameof(DisplaySpectrumConfig.IsLuminousFluxMode));
    }

    [Fact]
    public void CameraAcquisitionDraftPersistsEditedPreferencesAndPreservesLiveState()
    {
        WpfTestHost.Invoke(() =>
        {
            string[] editableProperties =
            [
                nameof(DisplayCameraConfig.UseLocalCamera),
                nameof(DisplayCameraConfig.HikBayerQuality),
                nameof(DisplayCameraConfig.UseMotorReadbackForVid),
                nameof(DisplayCameraConfig.SaveLocalAutoFocusChart),
                nameof(DisplayCameraConfig.LocalAutoFocusChartDirectory)
            ];
            Assert.Equal(editableProperties, CameraAcquisitionSettingsDraft.EditableProperties);

            var manager = new DisplayConfigManager();
            var config = manager.GetDisplayConfig<DisplayCameraConfig>("camera");
            config.LocalAutoFocusChartDirectory = @"C:\FocusResults";
            config.UseHikMvs = false;
            config.HikOutputBgr = false;
            config.SaveLocalCaptureFiles = false;
            var draft = new CameraAcquisitionSettingsDraft(config);
            Assert.False(draft.Apply(config));
            Assert.Equal(config.LocalAutoFocusChartDirectory, draft.Settings.LocalAutoFocusChartDirectory);
            Assert.Equal(HikBayerQuality.OptimalPlus, draft.Settings.HikBayerQuality);
            Assert.Equal(HikBayerQuality.OptimalPlus, JsonConvert.DeserializeObject<DisplayCameraConfig>("{}")!.HikBayerQuality);

            draft.Settings.UseLocalCamera = false;
            draft.Settings.HikBayerQuality = HikBayerQuality.Fast;
            draft.Settings.SaveLocalAutoFocusChart = true;
            draft.Settings.LocalAutoFocusChartDirectory = string.Empty;
            Assert.True(config.UseLocalCamera);
            Assert.False(config.SaveLocalAutoFocusChart);

            config.UseMotorReadbackForVid = false;
            config.TakePictureDelay = 75;

            var beforeFailedSave = JObject.FromObject(config);
            Assert.Throws<InvalidOperationException>(() => draft.Apply(config, () => throw new InvalidOperationException()));
            Assert.True(JToken.DeepEquals(beforeFailedSave, JObject.FromObject(config)));

            Assert.True(draft.Apply(config));
            Assert.Equal(75, config.TakePictureDelay);
            Assert.False(config.UseMotorReadbackForVid);

            var restoredManager = JsonConvert.DeserializeObject<DisplayConfigManager>(JsonConvert.SerializeObject(manager))!;
            var restored = restoredManager.GetDisplayConfig<DisplayCameraConfig>("camera");
            Assert.False(restored.UseLocalCamera);
            Assert.False(restored.UseHikMvs);
            Assert.Equal(HikBayerQuality.Fast, restored.HikBayerQuality);
            Assert.False(restored.HikOutputBgr);
            Assert.False(restored.SaveLocalCaptureFiles);
            Assert.False(restored.UseMotorReadbackForVid);
            Assert.True(restored.SaveLocalAutoFocusChart);
            Assert.Empty(restored.LocalAutoFocusChartDirectory);
            Assert.Equal(75, restored.TakePictureDelay);
            Assert.Equal(HikBayerQuality.Fast, new CameraAcquisitionSettingsDraft(restored).Settings.HikBayerQuality);
        });
    }

    [Fact]
    public void CameraEditorShowsFocusPreferencesOnlyForTheDraftLocalBackend()
    {
        WpfTestHost.Invoke(() =>
        {
            var previousConfig = ConfigService.Instance;
            var dictionaries = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
                .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.RelativeOrAbsolute) }).ToArray();
            foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Add(dictionary);
            TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
            try
            {
                using var camera = new DeviceCamera(new SysResourceModel { Id = -2, Code = "camera-focus-visibility", Value = "{}" });
                var window = new EditCamera(camera) { Left = -10000, Top = -10000, ShowActivated = false, ShowInTaskbar = false };
                try
                {
                    window.Show();
                    var section = Assert.IsType<StackPanel>(window.FindName("AcquisitionFields"));
                    var fields = section.Children.OfType<DockPanel>().ToDictionary(field => Assert.IsAssignableFrom<PropertyInfo>(field.Tag).Name);
                    var toggle = Assert.Single(fields[nameof(DisplayCameraConfig.UseLocalCamera)].Children.OfType<ToggleSwitch>());
                    DockPanel[] focusFields =
                    [
                        fields[nameof(DisplayCameraConfig.UseMotorReadbackForVid)],
                        fields[nameof(DisplayCameraConfig.SaveLocalAutoFocusChart)],
                        fields[nameof(DisplayCameraConfig.LocalAutoFocusChartDirectory)]
                    ];
                    var binding = toggle.GetBindingExpression(ToggleSwitch.IsCheckedProperty)!;
                    Assert.All(focusFields, field => Assert.Equal(Visibility.Visible, field.Visibility));

                    toggle.SetCurrentValue(ToggleSwitch.IsCheckedProperty, false);
                    binding.UpdateSource();
                    Assert.All(focusFields, field => Assert.Equal(Visibility.Collapsed, field.Visibility));
                    Assert.True(camera.DisplayConfig.UseLocalCamera);

                    toggle.SetCurrentValue(ToggleSwitch.IsCheckedProperty, true);
                    binding.UpdateSource();
                    Assert.All(focusFields, field => Assert.Equal(Visibility.Visible, field.Visibility));
                }
                finally { window.Close(); }
            }
            finally
            {
                TestEnvironment.SetConfig(previousConfig);
                foreach (var dictionary in dictionaries) Application.Current.Resources.MergedDictionaries.Remove(dictionary);
            }
        });
    }

    [Theory]
    [InlineData("4", ReferenceLineMode.SimpleCross)]
    [InlineData("5", ReferenceLineMode.SimpleCross)]
    [InlineData("\"CrossMask\"", ReferenceLineMode.SimpleCross)]
    [InlineData("\"GridLines\"", ReferenceLineMode.SimpleCross)]
    [InlineData("3", ReferenceLineMode.DiagonalCross)]
    public void CameraReferenceLineModeReloadPreservesTheOtherSettings(string modeJson, ReferenceLineMode expected)
    {
        WpfTestHost.Invoke(() =>
        {
            var manager = new DisplayConfigManager
            {
                Object = new JObject
                {
                    ["camera"] = new JObject
                    {
                        [nameof(DisplayCameraConfig.UseLocalCamera)] = false,
                        [nameof(DisplayCameraConfig.ReferenceLineParam)] = new JObject
                        {
                            [nameof(ReferenceLineParam.Mode)] = JToken.Parse(modeJson),
                            [nameof(ReferenceLineParam.PointX)] = 123.5,
                            [nameof(ReferenceLineParam.IsMapping)] = true,
                            ["MaskSize"] = 75
                        }
                    }
                }
            };

            var config = manager.GetDisplayConfig<DisplayCameraConfig>("camera");
            Assert.False(config.UseLocalCamera);
            Assert.Equal(expected, config.ReferenceLineParam.Mode);
            Assert.Equal(123.5, config.ReferenceLineParam.PointX);
            Assert.True(config.ReferenceLineParam.IsMapping);
            Assert.Equal((int)expected, (int?)JObject.FromObject(config.ReferenceLineParam)[nameof(ReferenceLineParam.Mode)]);
        });
    }

    private static void VerifyCreationPreference<T>(ServiceTypes serviceType, string propertyName) where T : IDisplayConfig, new()
    {
        var manager = new DisplayConfigManager
        {
            Object = new JObject { ["saved"] = new JObject { [propertyName] = false } }
        };
        var property = TypeDescriptor.GetProperties(typeof(T))[propertyName]!;
        Assert.False(DeviceServiceFactoryRegistry.TryInitializeNewDeviceDisplayConfig(serviceType, "saved", manager));
        Assert.False((bool)property.GetValue(manager.GetDisplayConfig<T>("saved"))!);
        var loadedLegacy = manager.GetDisplayConfig<T>("legacy");
        Assert.False(DeviceServiceFactoryRegistry.TryInitializeNewDeviceDisplayConfig(serviceType, "legacy", manager));
        Assert.False((bool)property.GetValue(loadedLegacy)!);

        Assert.True(DeviceServiceFactoryRegistry.TryInitializeNewDeviceDisplayConfig(serviceType, "new", manager));
        var restored = JsonConvert.DeserializeObject<DisplayConfigManager>(JsonConvert.SerializeObject(manager))!;
        Assert.True((bool)property.GetValue(restored.GetDisplayConfig<T>("new"))!);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void DefaultsEnableLocalDevicesAndReloadPreservesExplicitChoices(bool? savedChoice)
    {
        VerifyPreference<DisplayCameraConfig>(nameof(DisplayCameraConfig.UseLocalCamera), savedChoice);
        VerifyPreference<DisplayCalibrationConfig>(nameof(DisplayCalibrationConfig.UseLocalCalibration), savedChoice);
        VerifyPreference<DisplaySpectrumConfig>(nameof(DisplaySpectrumConfig.UseLocalSpectrum), savedChoice);
        VerifyPreference<DisplaySMUConfig>(nameof(DisplaySMUConfig.UseLocalSmu), savedChoice);
        VerifyPreference<DisplaySensorConfig>(nameof(DisplaySensorConfig.UseLocalSensor), savedChoice);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void LocalResourceInitializationHonorsSavedPreference(bool? savedChoice)
    {
        WpfTestHost.Invoke(() =>
        {
            var previousConfig = ConfigService.Instance;
            try
            {
                TestEnvironment.SetConfig(new ConfigHandler { IsAutoSave = false });
                foreach (var (kind, propertyName) in new[]
                {
                    ("camera", nameof(DisplayCameraConfig.UseLocalCamera)),
                    ("calibration", nameof(DisplayCalibrationConfig.UseLocalCalibration)),
                    ("spectrum", nameof(DisplaySpectrumConfig.UseLocalSpectrum)),
                    ("smu", nameof(DisplaySMUConfig.UseLocalSmu)),
                    ("sensor", nameof(DisplaySensorConfig.UseLocalSensor))
                })
                {
                    var resource = new SysResourceModel { Id = -2, Code = $"local-default-{kind}", Value = "{}" };
                    DisplayConfigManager.Instance.Object[resource.Code] = savedChoice.HasValue
                        ? new JObject { [propertyName] = savedChoice.Value } : new JObject();
                    using DeviceService device = kind switch
                    {
                        "camera" => new DeviceCamera(resource),
                        "calibration" => new DeviceCalibration(resource),
                        "spectrum" => new DeviceSpectrum(resource),
                        "smu" => new DeviceSMU(resource),
                        "sensor" => new DeviceSensor(resource),
                        _ => throw new ArgumentException(kind)
                    };
                    bool usesLocal = device switch
                    {
                        DeviceCamera camera => camera.CameraBackend.OpensLocally,
                        DeviceCalibration calibration => calibration.DisplayConfig.UseLocalCalibration,
                        DeviceSpectrum spectrum => spectrum.SpectrumBackend.OpensLocally,
                        DeviceSMU smu => smu.SmuBackend.OpensLocally,
                        DeviceSensor sensor => sensor.SensorBackend.OpensLocally,
                        _ => throw new ArgumentException(kind)
                    };
                    Assert.Equal(savedChoice ?? true, usesLocal);
                }
            }
            finally { TestEnvironment.SetConfig(previousConfig); }
        });
    }

    private static void VerifyPreference<T>(string propertyName, bool? savedChoice) where T : IDisplayConfig, new()
    {
        var property = TypeDescriptor.GetProperties(typeof(T))[propertyName]!;
        var manager = new DisplayConfigManager
        {
            Object = new JObject
            {
                ["saved"] = savedChoice.HasValue ? new JObject { [propertyName] = savedChoice.Value } : new JObject()
            }
        };

        Assert.True((bool)property.GetValue(manager.GetDisplayConfig<T>("new"))!);
        var config = manager.GetDisplayConfig<T>("saved");
        Assert.Equal(savedChoice ?? true, (bool)property.GetValue(config)!);

        var restored = JsonConvert.DeserializeObject<DisplayConfigManager>(JsonConvert.SerializeObject(manager))!;
        Assert.Equal(savedChoice ?? true, (bool)property.GetValue(restored.GetDisplayConfig<T>("saved"))!);

        property.SetValue(config, false);
        restored = JsonConvert.DeserializeObject<DisplayConfigManager>(JsonConvert.SerializeObject(manager))!;
        Assert.False((bool)property.GetValue(restored.GetDisplayConfig<T>("saved"))!);
    }
}
