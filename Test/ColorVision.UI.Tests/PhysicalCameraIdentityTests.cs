using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.Types;
using cvColorVision;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class PhysicalCameraIdentityTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("ColorVision-CameraIdentity-").FullName;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("old-name")]
    public void SavedPhysicalIdSurvivesAnEmptyOrStaleResourceName(string? name)
    {
        var resource = new SysResourceModel { Name = name, Value = "{\"CameraID\":\"saved-id\"}" };
        Assert.Equal("saved-id", PhysicalCameraIdentity.ReadCameraId(resource, "old-memory-id"));
    }

    [Fact]
    public void LegacyNameFillsOnlyAMissingPhysicalId()
    {
        var resource = new SysResourceModel { Name = "legacy-id", Value = "{}" };
        Assert.Equal("legacy-id", PhysicalCameraIdentity.ReadCameraId(resource));
        resource.Name = null;
        Assert.Equal("existing-id", PhysicalCameraIdentity.ReadCameraId(resource, "existing-id"));
    }

    [Fact]
    public void LegacyNameRepairsAnInvalidStoredIdOnlyWhenItMatchesTheUnchangedPhysicalCode()
    {
        string code = Tool.GetMD5("correct-id");
        var resource = new SysResourceModel { Code = code, Name = "correct-id", Value = "{\"CameraID\":\"stale-id\"}" };
        Assert.Equal("correct-id", PhysicalCameraIdentity.ReadCameraId(resource));
        resource.Name = code;
        resource.Value = "{}";
        Assert.Empty(PhysicalCameraIdentity.ReadCameraId(resource));
        Assert.Equal(code, resource.Code);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("{\"CameraID\":{}}")]
    public void InvalidPhysicalJsonDoesNotPreventRecoveringAValidLegacyIdentity(string value)
    {
        var resource = new SysResourceModel { Code = Tool.GetMD5("legacy-id"), Name = "legacy-id", Value = value };
        Assert.Equal("legacy-id", PhysicalCameraIdentity.ReadCameraId(resource));
        Assert.Equal(value, resource.Value);
    }

    [Fact]
    public void DiscoveryPersistsPhysicalAndLogicalIdsWithoutChangingOtherConfigurationOrRestartingDevices()
    {
        const string cameraId = "DB1868537";
        string code = Tool.GetMD5(cameraId);
        var dao = new SysResourceDao(new LocalTemplateStore(Path.Combine(directory, "identities.db")), () => false);
        var physicalRow = new SysResourceModel
        {
            Type = (int)ServiceTypes.PhyCamera, Code = code, Name = null,
            Value = "{\"CameraID\":\"DB1868536\",\"VendorExtension\":{\"Keep\":123},\"CameraCfg\":{\"gain\":10}}"
        };
        dao.Save(physicalRow);
        PhyCamera physical = CreatePhysical(physicalRow);
        SysResourceModel cameraRow = CreateLogical(dao, ServiceTypes.Camera, code);
        SysResourceModel calibrationRow = CreateLogical(dao, ServiceTypes.Calibration, code);
        SysResourceModel otherRow = CreateLogical(dao, ServiceTypes.Camera, "unrelated-code");
        var camera = new IdentityDevice(cameraRow, new ConfigCamera { CameraCode = code, CameraID = "DB1868536", ExpTimeMin = 7 });
        var calibration = new IdentityDevice(calibrationRow, new ConfigCalibration { CameraCode = code, CameraID = "DB1868536" });
        string otherValue = otherRow.Value!;
        int configEvents = 0;
        physical.ConfigChanged += (_, _) => configEvents++;
        int writes = 0;
        int Save(SysResourceModel row) { writes++; return dao.Save(row); }
        var summary = Discover(cameraId);

        PhysicalCameraIdentity.SynchronizeDiscovered(summary, [physical], dao.GetAll(), [camera, calibration], Save);

        Assert.Equal(code, physical.Code);
        Assert.Equal(cameraId, physical.Config.CameraID);
        var savedPhysical = dao.GetById(physicalRow.Id)!;
        Assert.Equal(cameraId, savedPhysical.Name);
        Assert.Equal(cameraId, JObject.Parse(savedPhysical.Value!).Value<string>("CameraID"));
        Assert.Equal(123, (int)JObject.Parse(savedPhysical.Value!)["VendorExtension"]!["Keep"]!);
        Assert.Equal(10, (int)JObject.Parse(savedPhysical.Value!)["CameraCfg"]!["gain"]!);
        foreach (var row in new[] { cameraRow, calibrationRow })
        {
            var json = JObject.Parse(dao.GetById(row.Id)!.Value!);
            Assert.Equal(cameraId, json.Value<string>("CameraID"));
            Assert.Equal(7, json.Value<int>("ExpTimeMin"));
            Assert.Equal("preserved", json.Value<string>("UnknownSetting"));
        }
        Assert.Equal(cameraId, ((ConfigCamera)camera.GetConfig()).CameraID);
        Assert.Equal(cameraId, ((ConfigCalibration)calibration.GetConfig()).CameraID);
        Assert.Equal(7, ((ConfigCamera)camera.GetConfig()).ExpTimeMin);
        Assert.Equal(otherValue, dao.GetById(otherRow.Id)!.Value);
        Assert.Equal(0, configEvents);
        Assert.Equal(3, writes);

        PhysicalCameraIdentity.SynchronizeDiscovered(summary, [physical], dao.GetAll(), [camera, calibration], Save);
        Assert.Equal(3, writes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void FailedOrNonMatchingDiscoveryDoesNotReplaceIdentity(bool success, bool incorrectMd5)
    {
        var row = new SysResourceModel { Id = -2, Code = Tool.GetMD5("bound-id"), Name = "bound-id", Value = "{\"CameraID\":\"bound-id\"}" };
        PhyCamera physical = CreatePhysical(row);
        var summary = Discover("other-id");
        summary.Models[0].Success = success;
        if (incorrectMd5) summary.Cameras[0].MD5Id = row.Code;

        PhysicalCameraIdentity.SynchronizeDiscovered(summary, [physical], [], [], _ => throw new InvalidOperationException("must not save"));

        Assert.Equal("bound-id", physical.Config.CameraID);
        Assert.Equal(Tool.GetMD5("bound-id"), row.Code);
    }

    [Fact]
    public void PersistenceFailurePreservesThePreviousPhysicalIdentityAndCanBeRetried()
    {
        var row = new SysResourceModel { Id = -2, Type = (int)ServiceTypes.PhyCamera, Code = Tool.GetMD5("new-id"), Name = null, Value = "{\"CameraID\":\"old-id\"}" };
        PhyCamera physical = CreatePhysical(row);
        var summary = Discover("new-id");
        string oldValue = row.Value;

        Assert.Throws<InvalidOperationException>(() => PhysicalCameraIdentity.SynchronizeDiscovered(summary, [physical], [row], [], _ => 0));
        Assert.Null(row.Name);
        Assert.Equal(oldValue, row.Value);
        Assert.Equal("old-id", physical.Config.CameraID);

        PhysicalCameraIdentity.SynchronizeDiscovered(summary, [physical], [row], [], _ => 1);
        Assert.Equal("new-id", physical.Config.CameraID);
    }

    [Fact]
    public void DiscoveryUsesTheLatestStoredConfigurationAndDoesNotResurrectADeletedCamera()
    {
        string code = Tool.GetMD5("bound-id");
        var cached = new SysResourceModel { Id = -2, Type = (int)ServiceTypes.PhyCamera, Code = code, Value = "{\"CameraID\":\"stale-id\",\"OtherSetting\":1}" };
        PhyCamera physical = CreatePhysical(cached);
        var latest = new SysResourceModel { Id = -2, Type = (int)ServiceTypes.PhyCamera, Code = code, Value = "{\"CameraID\":\"stale-id\",\"OtherSetting\":2}" };

        PhysicalCameraIdentity.SynchronizeDiscovered(Discover("bound-id"), [physical], [latest], [], _ => 1);
        Assert.Same(latest, physical.SysResourceModel);
        Assert.Equal(2, JObject.Parse(latest.Value!).Value<int>("OtherSetting"));
        latest.IsDelete = true;
        PhysicalCameraIdentity.SynchronizeDiscovered(Discover("bound-id"), [physical], [latest], [], _ => throw new InvalidOperationException("must not save"));
        Assert.True(latest.IsDelete);
    }

    private static cvCameraCSLib.CameraDiscoverySummary Discover(string cameraId)
    {
        var summary = new cvCameraCSLib.CameraDiscoverySummary();
        summary.Models.Add(new() { CameraModel = CameraModel.HK_USB, Success = true });
        summary.Cameras.Add(new() { CameraModel = CameraModel.HK_USB, CameraId = cameraId, MD5Id = Tool.GetMD5(cameraId) });
        return summary;
    }

    private static PhyCamera CreatePhysical(SysResourceModel row)
    {
        var physical = (PhyCamera)RuntimeHelpers.GetUninitializedObject(typeof(PhyCamera));
        physical.SysResourceModel = row;
        physical.Config = JsonConvert.DeserializeObject<ConfigPhyCamera>(row.Value!)!;
        return physical;
    }

    private static SysResourceModel CreateLogical(SysResourceDao dao, ServiceTypes type, string code)
    {
        var row = new SysResourceModel { Type = (int)type, Code = "DEV." + type,
            Value = new JObject { ["CameraCode"] = code, ["CameraID"] = "old-id", ["ExpTimeMin"] = 7, ["UnknownSetting"] = "preserved" }.ToString() };
        dao.Save(row);
        return row;
    }

    private sealed class IdentityDevice : DeviceService
    {
        private readonly DeviceServiceConfig config;
        public IdentityDevice(SysResourceModel row, DeviceServiceConfig config)
        {
            SysResourceModel = row;
            this.config = config;
        }
        public override object GetConfig() => config;
        public override void Save() => throw new InvalidOperationException("Identity synchronization must not restart a device.");
        public override ColorVision.UI.CopilotBusinessContextBundle CaptureCopilotContext() => throw new NotSupportedException();
    }

    public void Dispose()
    {
        foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
        Directory.Delete(directory);
    }
}
