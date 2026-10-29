using ColorVision.Engine;
using ColorVision.Database;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.Types;
using cvColorVision;
using Newtonsoft.Json;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class PhyCameraLicenseImportPolicyTests : IDisposable
{
    private readonly string _tempDirectory = Directory.CreateTempSubdirectory("ColorVision-PhysicalCamera-").FullName;

    [Theory]
    [InlineData(false, "scanned-id")]
    [InlineData(true, "scanned-id")]
    [InlineData(false, "")]
    [InlineData(true, "")]
    public void FirstPhysicalCameraBindsPreinstalledDevicesWithOrWithoutLicense(bool hasLicense, string cameraId)
    {
        var store = new LocalTemplateStore(Path.Combine(_tempDirectory, "bindings.db"));
        var resources = new SysResourceDao(store, () => false);
        var physical = new SysResourceModel { Code = "CAMERA-01", Type = (int)ServiceTypes.PhyCamera };
        resources.Save(physical);
        if (hasLicense)
        {
            new PhyLicenseDao(store, () => false).Save(new LicenseModel { MacAddress = physical.Code, LicenseValue = "not-needed-for-binding" });
        }
        var physicalConfig = new ConfigPhyCamera { Code = physical.Code, CameraID = cameraId, CameraModel = CameraModel.HK_USB };
        var camera = new BindingDevice(ServiceTypes.Camera, new ConfigCamera());
        var algorithm = new BindingDevice(ServiceTypes.Algorithm, new ConfigAlgorithm());
        var calibration = new BindingDevice(ServiceTypes.Calibration, new ConfigCalibration());
        BindingDevice[] devices = [camera, algorithm, calibration];
        foreach (var device in devices)
        {
            // Database initialization creates these devices before any physical camera exists.
            resources.Save(device.SysResourceModel);
            device.Persist = row => Assert.Equal(1, resources.Save(row));
        }

        PhysicalCameraInitialBinding.BindDevices(true, 1, physical, physicalConfig, devices);

        var reopened = new SysResourceDao(new LocalTemplateStore(store.DatabasePath), () => false);
        foreach (var device in devices)
        {
            Assert.Equal(1, device.SaveCount);
            var saved = JsonConvert.DeserializeObject<DeviceServiceConfig>(reopened.GetById(device.SysResourceModel.Id)!.Value!)!;
            Assert.Equal("CAMERA-01", saved.SN);
        }
        var savedCamera = JsonConvert.DeserializeObject<ConfigCamera>(reopened.GetById(camera.SysResourceModel.Id)!.Value!)!;
        Assert.Equal("CAMERA-01", savedCamera.CameraCode);
        Assert.Equal(CameraModel.HK_USB, savedCamera.CameraModel);
        Assert.Equal(cameraId, savedCamera.CameraID);
        Assert.Equal("CAMERA-01", JsonConvert.DeserializeObject<ConfigCalibration>(reopened.GetById(calibration.SysResourceModel.Id)!.Value!)!.CameraCode);

        PhysicalCameraInitialBinding.BindDevices(true, 1, physical, physicalConfig, devices);
        Assert.All(devices, device => Assert.Equal(1, device.SaveCount));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public void ExistingOrAdditionalPhysicalCameraDoesNotRebindDevices(bool requiresCreation, int cameraCount)
    {
        var device = new BindingDevice(ServiceTypes.Algorithm, new ConfigAlgorithm());
        PhysicalCameraInitialBinding.BindDevices(requiresCreation, cameraCount,
            new SysResourceModel { Id = 1, Code = "CAMERA-01" }, new ConfigPhyCamera(), [device]);
        Assert.Null(device.Config.SN);
        Assert.Equal(0, device.SaveCount);
    }

    [Fact]
    public void FirstPhysicalCameraPreservesAssignedIdentitySpectrumAndOtherStorage()
    {
        BindingDevice[] devices =
        [
            new(ServiceTypes.Algorithm, new ConfigAlgorithm { SN = "explicit-sn" }),
            new(ServiceTypes.Camera, new ConfigCamera { CameraCode = "other-camera" }),
            new(ServiceTypes.Calibration, new ConfigCalibration { SN = "other-camera" }),
            new(ServiceTypes.Spectrum, new ConfigSpectrum()),
            new(ServiceTypes.Algorithm, new ConfigAlgorithm())
        ];
        devices[4].SysResourceModel.Id = -2;
        var before = devices.Select(device => JsonConvert.SerializeObject(device.Config)).ToArray();
        PhysicalCameraInitialBinding.BindDevices(true, 1, new SysResourceModel { Id = 1, Code = "CAMERA-01" },
            new ConfigPhyCamera { CameraID = "scanned-id" }, devices);
        Assert.Equal(before, devices.Select(device => JsonConvert.SerializeObject(device.Config)).ToArray());
        Assert.All(devices, device => Assert.Equal(0, device.SaveCount));
    }

    [Fact]
    public void InitialBindingReplacesOldCameraIdAndCompletesMatchingPartialBinding()
    {
        var config = new ConfigCamera { SN = "camera-01", CameraID = "user-id" };
        Assert.True(PhysicalCameraInitialBinding.Apply(config, "CAMERA-01", new ConfigPhyCamera { CameraID = "scanned-id" }));
        Assert.Equal("CAMERA-01", config.CameraCode);
        Assert.Equal("camera-01", config.SN);
        Assert.Equal("scanned-id", config.CameraID);
        Assert.False(PhysicalCameraInitialBinding.Apply(config, "CAMERA-01", new ConfigPhyCamera { CameraID = "scanned-id" }));
    }

    private sealed class BindingDevice : DeviceService
    {
        internal DeviceServiceConfig Config { get; }
        internal int SaveCount { get; private set; }
        internal Action<SysResourceModel>? Persist { get; set; }

        internal BindingDevice(ServiceTypes type, DeviceServiceConfig config)
        {
            Config = config;
            SysResourceModel = new SysResourceModel { Id = 0, Code = $"DEV.{type}.Default", Type = (int)type, Pid = 1 };
        }

        public override object GetConfig() => Config;
        public override void Save()
        {
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            Persist?.Invoke(SysResourceModel);
            SaveCount++;
        }
        public override ColorVision.UI.CopilotBusinessContextBundle CaptureCopilotContext() => throw new NotSupportedException();
    }

    [Fact]
    public void CreatingPhysicalCameraEnsuresItsLocalCalibrationDirectoryExists()
    {
        string basePath = Path.Combine(_tempDirectory, "CVTest");

        PhyCameraManager.EnsurePhysicalCameraDirectory(basePath, "CAMERA-01");

        Assert.True(Directory.Exists(Path.Combine(basePath, "CAMERA-01", "cfg")));
    }

    [Fact]
    public void RepeatedPhysicalCameraCreationPreservesLargeUniformityAndFourColorFiles()
    {
        string cameraDirectory = Path.Combine(_tempDirectory, "CAMERA-01");
        string cfgDirectory = Directory.CreateDirectory(Path.Combine(cameraDirectory, "cfg")).FullName;
        string uniformityPath = Path.Combine(cfgDirectory, "Uniformity.cal");
        byte[] uniformity = new byte[11 * 1024 * 1024];
        new Random(42).NextBytes(uniformity);
        File.WriteAllBytes(uniformityPath, uniformity);
        string fourColorPath = Path.Combine(cfgDirectory, "FourColor.cal");
        File.WriteAllText(fourColorPath, "existing-four-color-calibration");
        string siblingPath = Path.Combine(cameraDirectory, "Camera.cfg");
        File.WriteAllText(siblingPath, "existing-camera-config");

        PhyCameraManager.EnsurePhysicalCameraDirectory(_tempDirectory, "CAMERA-01");
        PhyCameraManager.EnsurePhysicalCameraDirectory(_tempDirectory, "CAMERA-01");

        Assert.Equal(SHA256.HashData(uniformity), SHA256.HashData(File.ReadAllBytes(uniformityPath)));
        Assert.Equal("existing-four-color-calibration", File.ReadAllText(fourColorPath));
        Assert.Equal("existing-camera-config", File.ReadAllText(siblingPath));
    }

    [Fact]
    public void DirectoryCreationReportsPathConflictWithoutReplacingExistingFile()
    {
        string cameraDirectory = Directory.CreateDirectory(Path.Combine(_tempDirectory, "CAMERA-01")).FullName;
        string conflictingPath = Path.Combine(cameraDirectory, "cfg");
        File.WriteAllText(conflictingPath, "must-not-be-replaced");

        Assert.Throws<IOException>(() => PhyCameraManager.EnsurePhysicalCameraDirectory(_tempDirectory, "CAMERA-01"));

        Assert.Equal("must-not-be-replaced", File.ReadAllText(conflictingPath));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../other-camera")]
    [InlineData("D:\\other-camera")]
    public void CameraCodeMustBeASingleDirectoryName(string cameraCode)
    {
        Assert.Throws<ArgumentException>(() => PhyCameraManager.EnsurePhysicalCameraDirectory(_tempDirectory, cameraCode));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_tempDirectory));
    }

    [Fact]
    public void EmptyBasePathDoesNotCreateFilesInTheWorkingDirectory()
    {
        Assert.Throws<ArgumentException>(() => PhyCameraManager.EnsurePhysicalCameraDirectory(string.Empty, "CAMERA-01"));
    }

    [Fact]
    public void ConfiguredPhysicalCameraOnlyRequiresLicenseReplacement()
    {
        SysResourceModel existing = new()
        {
            Code = "CAMERA-01",
            Type = (int)ServiceTypes.PhyCamera,
            Value = "{\"CameraID\":\"现场配置\"}"
        };

        SysResourceModel? match = PhyCameraManager.FindPhysicalCameraResource([existing], "camera-01");

        Assert.Same(existing, match);
        Assert.False(PhyCameraManager.RequiresPhysicalCameraCreation(match));
        Assert.Equal("{\"CameraID\":\"现场配置\"}", existing.Value);
    }

    [Fact]
    public void EmptyPhysicalCameraCandidateStillRequiresCreation()
    {
        SysResourceModel candidate = new()
        {
            Code = "CAMERA-01",
            Type = (int)ServiceTypes.PhyCamera,
            Value = string.Empty
        };

        SysResourceModel? match = PhyCameraManager.FindPhysicalCameraResource([candidate], "CAMERA-01");

        Assert.Same(candidate, match);
        Assert.True(PhyCameraManager.RequiresPhysicalCameraCreation(match));
    }

    [Fact]
    public void RepeatedLicenseInOneImportReusesExistingModelIgnoringCase()
    {
        LicenseModel existing = new() { MacAddress = "CAMERA-01" };
        List<LicenseModel> licenses = [existing];

        LicenseModel match = PhyCameraManager.GetOrCreateLicenseModel("camera-01", licenses);

        Assert.Same(existing, match);
        Assert.Single(licenses);
    }

    private static string ImportLicenseValue => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        """{"device_mode":"CAM-TEST","expiry_date":"2200000000","licensee":"Test customer"}"""));

    [Fact]
    public void CameraLicenseUsesHardwareMd5AfterTheFileIsRenamed()
    {
        const string cameraId = "hardware-camera-id";
        string key = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(cameraId)));
        string file = Path.Combine(_tempDirectory, "改过的许可证名称.lic");
        File.WriteAllText(file, ImportLicenseValue);

        var identity = DeviceLicenseImport.Camera(cameraId, key.ToLowerInvariant());
        var license = DeviceLicenseImport.ReadFile(file, identity);

        Assert.Equal(key, license.MacAddress);
        Assert.Equal(0, license.LiceType);
        Assert.Equal("CAM-TEST", license.Model);
        Assert.Equal("Test customer", license.CusTomerName);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(2200000000).LocalDateTime, license.ExpiryDate);
        Assert.Equal(ImportLicenseValue, license.LicenseValue);
        Assert.Throws<InvalidDataException>(() => DeviceLicenseImport.Camera(cameraId, new string('0', 32)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void RenamedUnboundLicenseReusesTheIdentityOfItsExistingContent(int licenseType)
    {
        string key = licenseType == 0 ? new string('A', 32) : "SPECTRUM-SN-01";
        var existing = new LicenseModel { LiceType = licenseType, MacAddress = key, LicenseValue = ImportLicenseValue };
        string file = Path.Combine(_tempDirectory, "任意文件名.lic");
        File.WriteAllText(file, ImportLicenseValue + "\r\n");
        var devices = DeviceLicenseImport.KnownDevices([], [existing]);

        var license = Assert.Single(DeviceLicenseImport.ReadFiles([file], devices, [existing]));

        Assert.Equal(key, license.MacAddress);
        Assert.Equal(licenseType, license.LiceType);
    }

    [Fact]
    public void UnknownRenamedLicenseRequiresATargetAndCannotCreateADeviceFromItsName()
    {
        string file = Path.Combine(_tempDirectory, "随意改名.lic");
        File.WriteAllText(file, ImportLicenseValue);
        Assert.Throws<InvalidDataException>(() => DeviceLicenseImport.ReadFiles([file], [], []));
    }

    [Fact]
    public void BatchImportRecognizesHardwareIdentifiersAndKeepsLegacyMd5Packages()
    {
        const string cameraId = "hardware-camera-id";
        var identity = DeviceLicenseImport.Camera(cameraId, null);
        string file = Path.Combine(_tempDirectory, cameraId + ".lic");
        File.WriteAllText(file, ImportLicenseValue);
        var devices = DeviceLicenseImport.KnownDevices(
            [new SysResourceModel { Type = (int)ServiceTypes.PhyCamera, Code = identity.Key }], []);
        Assert.Equal(identity.Key, Assert.Single(DeviceLicenseImport.ReadFiles([file], devices, [])).MacAddress);

        string legacyFile = Path.Combine(_tempDirectory, identity.Key + ".lic");
        File.WriteAllText(legacyFile, ImportLicenseValue);
        Assert.Equal(identity.Key, Assert.Single(DeviceLicenseImport.ReadFiles([legacyFile], [], [])).MacAddress);
        Assert.Throws<InvalidDataException>(() => DeviceLicenseImport.ReadFiles([file, legacyFile], devices, []));
    }

    [Theory]
    [InlineData(12)]
    [InlineData(-12)]
    public void LicenseRenewalPreservesDatabaseIdentityAndDeviceAssociations(int id)
    {
        var identity = DeviceLicenseImport.Camera("hardware-camera-id", null);
        DateTime created = new(2020, 1, 1);
        var existing = new LicenseModel
        {
            Id = id, MacAddress = identity.Key.ToLowerInvariant(), LiceType = 0,
            DevCameraId = 31, DevCaliId = 32, CreateDate = created, LicenseValue = "old"
        };
        var updated = DeviceLicenseImport.PreserveAssociation(DeviceLicenseImport.Parse(ImportLicenseValue, identity), existing);
        Assert.Equal(id, updated.Id);
        Assert.Equal(31, updated.DevCameraId);
        Assert.Equal(32, updated.DevCaliId);
        Assert.Equal(created, updated.CreateDate);
        Assert.Equal(ImportLicenseValue, updated.LicenseValue);
        existing.LiceType = 1;
        Assert.Throws<InvalidDataException>(() => DeviceLicenseImport.PreserveAssociation(updated, existing));
    }

    [Theory]
    [InlineData("old-license", "new-license", 1, true)]
    [InlineData("same-license", "same-license", 1, false)]
    [InlineData("same-license\r\n", " same-license ", 1, false)]
    [InlineData("old-license", "new-license", 0, false)]
    [InlineData("old-license", "new-license", -1, false)]
    public void ServiceRestartIsOfferedOnlyAfterAChangedLicenseWasSaved(string? previousValue, string? currentValue, int saveResult, bool expected)
    {
        Assert.Equal(expected, PhyCamera.ShouldRestartServicesAfterLicenseUpdate(previousValue, currentValue, saveResult));
    }

    [Fact]
    public async Task OnlineLicenseCompletionReturnsToTheApplicationDispatcher()
    {
        int dispatcherThreadId = WpfTestHost.Invoke(() => Environment.CurrentManagedThreadId);

        int actionThreadId = await Task.Run(() =>
            PhyCamera.InvokeOnApplicationDispatcherAsync(() => Environment.CurrentManagedThreadId));

        Assert.Equal(dispatcherThreadId, actionThreadId);
    }

    public void Dispose()
    {
        string fullPath = Path.GetFullPath(_tempDirectory);
        if (!string.Equals(Path.GetDirectoryName(fullPath), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase)
            || !Path.GetFileName(fullPath).StartsWith("ColorVision-PhysicalCamera-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Refusing to remove a directory outside the test workspace.");
        }
        Directory.Delete(fullPath, true);
    }
}
