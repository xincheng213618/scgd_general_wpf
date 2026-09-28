using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Types;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using Newtonsoft.Json;
using System.Globalization;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class WindowServiceConfigurationTests
{
    private static readonly DateTimeOffset CreationTime = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ServiceTypes.Camera, false)]
    [InlineData(ServiceTypes.Calibration, false)]
    [InlineData(ServiceTypes.Spectrum, false)]
    [InlineData(ServiceTypes.PG, true)]
    [InlineData(ServiceTypes.SMU, true)]
    [InlineData(ServiceTypes.Sensor, true)]
    [InlineData(ServiceTypes.FileServer, true)]
    [InlineData(ServiceTypes.Algorithm, true)]
    [InlineData(ServiceTypes.FilterWheel, true)]
    [InlineData(ServiceTypes.Motor, true)]
    [InlineData(ServiceTypes.LightingControl, true)]
    public void CreatingDevice_DefaultsLicenseOnlyForEligibleTypesAndPersistsIt(ServiceTypes serviceType, bool usesDefault)
    {
        Assert.True(DeviceServiceFactoryRegistry.TryGetFactory(serviceType, out var factory));
        var config = factory!.CreateConfig(new("DEV.test", "Test", "test/cmd", "test/status"));
        string initialSn = config.SN;

        DeviceServiceFactoryRegistry.ApplyDefaultLicense(serviceType, config,
            [License("short", CreationTime.AddDays(1)), License("long", CreationTime.AddYears(1))], CreationTime);

        Assert.Equal(usesDefault ? "long" : initialSn, config.SN);
        if (config is ConfigCamera camera) Assert.Null(camera.CameraCode);
        if (config is ConfigCalibration calibration) Assert.Null(calibration.CameraCode);
        var restored = (DeviceServiceConfig)JsonConvert.DeserializeObject(JsonConvert.SerializeObject(config), config.GetType())!;
        Assert.Equal(config.SN, restored.SN);
    }

    [Fact]
    public void DefaultLicenseUsesPayloadExpiryInsteadOfStaleDatabaseMetadata()
    {
        var longer = License("long", CreationTime.AddYears(1));
        longer.ExpiryDate = CreationTime.AddYears(-1).UtcDateTime;
        var shorter = License("short", CreationTime.AddDays(1));
        shorter.ExpiryDate = CreationTime.AddYears(10).UtcDateTime;

        Assert.Same(longer, PhyLicenseDao.FindUsableCameraLicense([shorter, longer], CreationTime));
        Assert.Same(longer, PhyLicenseDao.FindUsableCameraLicense([longer, shorter], CreationTime));
    }

    [Fact]
    public void DefaultLicenseExcludesOtherTypesExpiredAndMalformedPayloads()
    {
        var spectrum = License("spectrum", CreationTime.AddYears(10));
        spectrum.LiceType = 1;
        var missingContent = License("missing-content", CreationTime.AddYears(10));
        missingContent.LicenseValue = null;
        var invalidBase64 = License("invalid-base64", CreationTime.AddYears(10));
        invalidBase64.LicenseValue = "!invalid-base64!";
        var invalidJson = License("invalid-json", CreationTime.AddYears(10));
        invalidJson.LicenseValue = Encode("not JSON");
        var missingModel = License("missing-model", CreationTime.AddYears(10));
        missingModel.LicenseValue = Encode("{\"expiry_date\":\"4102444800\"}");
        var missingExpiry = License("missing-expiry", CreationTime.AddYears(10));
        missingExpiry.LicenseValue = Encode("{\"device_mode\":\"CV-test\"}");
        var invalidExpiry = License("invalid-expiry", CreationTime.AddYears(10));
        invalidExpiry.LicenseValue = Encode("{\"device_mode\":\"CV-test\",\"expiry_date\":\"9223372036854775807\"}");
        LicenseModel[] invalid = [spectrum, missingContent, invalidBase64, invalidJson, missingModel, missingExpiry, invalidExpiry,
            License(" ", CreationTime.AddYears(10)), License("expired", CreationTime.AddSeconds(-1)), License("boundary", CreationTime)];

        Assert.Null(PhyLicenseDao.FindUsableCameraLicense(invalid, CreationTime));
        var valid = License("valid", CreationTime.AddSeconds(1));
        Assert.Same(valid, PhyLicenseDao.FindUsableCameraLicense([.. invalid, valid], CreationTime));
    }

    [Fact]
    public void EqualExpiryUsesStableSnOrderingAndAcceptsExpiryAfter2038()
    {
        var expiry = new DateTimeOffset(2040, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var first = License("alpha", expiry);
        var second = License("BETA", expiry);

        Assert.Same(first, PhyLicenseDao.FindUsableCameraLicense([second, first], CreationTime));
        Assert.Same(first, PhyLicenseDao.FindUsableCameraLicense([first, second], CreationTime));
    }

    [Fact]
    public void DefaultLicensePreservesExplicitSnAndLeavesEmptyConfigWithoutUsableLicense()
    {
        var explicitConfig = new DeviceServiceConfig { SN = "user-selected" };
        DeviceServiceFactoryRegistry.ApplyDefaultLicense(ServiceTypes.Algorithm, explicitConfig,
            [License("automatic", CreationTime.AddYears(1))], CreationTime);
        Assert.Equal("user-selected", explicitConfig.SN);

        var emptyConfig = new DeviceServiceConfig();
        DeviceServiceFactoryRegistry.ApplyDefaultLicense(ServiceTypes.Algorithm, emptyConfig, [], CreationTime);
        Assert.Null(emptyConfig.SN);
        DeviceServiceFactoryRegistry.ApplyDefaultLicense(ServiceTypes.Algorithm, emptyConfig,
            [License("expired", CreationTime.AddSeconds(-1))], CreationTime);
        Assert.Null(emptyConfig.SN);
    }

    private static LicenseModel License(string sn, DateTimeOffset expiry) => new()
    {
        LiceType = 0,
        MacAddress = sn,
        ExpiryDate = expiry.UtcDateTime,
        LicenseValue = Encode(JsonConvert.SerializeObject(new
        {
            device_mode = "CV-test",
            expiry_date = expiry.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)
        }))
    };

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    [Fact]
    public void Snapshot_IgnoresTransientRuntimeStateButDetectsEditsAndReverts()
    {
        WpfTestHost.Invoke(() =>
        {
            var type = new TypeService { Name = "Camera" };
            var terminal = new TerminalService(new ColorVision.Engine.SysResourceModel { Name = "Service", Code = "test" });
            var device = new TestDevice();
            type.VisualChildren.Add(terminal);
            terminal.VisualChildren.Add(device);
            var types = new[] { type };
            var initial = WindowService.CaptureConfiguration(types);
            device.IsSelected = true;
            device.IsAlive = true;
            device.LastAliveTime = DateTime.Now;
            Assert.Equal(initial, WindowService.CaptureConfiguration(types));
            device.Configuration.Name = "Changed";
            Assert.NotEqual(initial, WindowService.CaptureConfiguration(types));
            device.Configuration.Name = "Camera";
            Assert.Equal(initial, WindowService.CaptureConfiguration(types));
            terminal.VisualChildren[0] = new TestDevice();
            Assert.NotEqual(initial, WindowService.CaptureConfiguration(types));
            terminal.VisualChildren.Clear();
            Assert.NotEqual(initial, WindowService.CaptureConfiguration(types));
        });
    }

    [Fact]
    public void DeviceContracts_DoNotExposeLegacyPerDeviceHeartbeat()
    {
        Assert.Null(typeof(DeviceServiceConfig).GetProperty("HeartbeatTime"));
        Assert.Null(typeof(DeviceService).GetProperty("HeartbeatTime"));
        Assert.Null(typeof(ColorVision.UI.CopilotDeviceContextSnapshot).GetProperty("HeartbeatTime"));
    }

    private sealed class TestDevice : DeviceService
    {
        public DeviceServiceConfig Configuration { get; } = new() { Name = "Camera" };
        public override object GetConfig() => Configuration;
        public override ColorVision.UI.CopilotBusinessContextBundle CaptureCopilotContext() => throw new NotSupportedException();
    }
}
