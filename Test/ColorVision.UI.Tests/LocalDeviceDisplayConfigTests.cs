using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.Spectrum;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace ColorVision.UI.Tests;

public class LocalDeviceDisplayConfigTests
{
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
