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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

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
                ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
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
            finally { ConfigService.SetInstance(previousConfig); }
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
