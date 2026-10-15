using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.Types;
using cvColorVision;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class CameraIdentityOpenPersistenceTests : IDisposable
{
    private const string CameraId = "DB1868537";
    private const string CameraCode = "0280C45DAC75920FD4498F1FB3D53C89";
    private readonly string directory = Directory.CreateTempSubdirectory("ColorVision-CameraIdentityOpen-").FullName;
    private readonly SysResourceDao previousDao = SysResourceDao.Instance;
    private readonly SysResourceDao dao;

    public CameraIdentityOpenPersistenceTests()
    {
        // The test assembly disables parallel execution; restore the singleton after each isolated local-store test.
        dao = new SysResourceDao(new LocalTemplateStore(Path.Combine(directory, "identities.db")), () => false);
        SysResourceDao.Instance = dao;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("stale-physical-id")]
    public void ServiceOpenMigratesAnMd5VerifiedLegacyIdWithoutOpeningTheLocalManager(string? physicalId)
    {
        DeviceCamera device = CreateDevice(CameraId, physicalId, CameraCode.ToLowerInvariant());
        var mqtt = CreateMqtt(device);
        Assert.Equal(CameraId, device.PhyCamera!.Config.CameraID);
        Assert.Equal(physicalId, ReadId(device.PhyCamera.Id)); // Association has no database side effects.

        mqtt.Open("stale-call-id", TakeImageMode.Measure_Normal, 16);

        var parameters = Assert.IsType<Dictionary<string, object>>((object)mqtt.Sent!.Params);
        Assert.Equal(CameraId, parameters["CameraID"]);
        AssertPersisted(device);
        Assert.Equal("Offline", dao.GetById(device.PhyCamera.Id)!.Remark); // Migration is not an SDK discovery.
        Assert.Equal(CameraCode, Tool.GetMD5(CameraId).ToUpperInvariant());
    }

    [Theory]
    [InlineData("different-camera", false)]
    [InlineData("db1868537", false)]
    [InlineData(CameraId, true)]
    public void ServiceOpenRejectsLegacyIdsWithDifferentOrPartialMd5Bindings(string legacyId, bool partialCode)
    {
        string code = partialCode ? CameraCode[..8] : CameraCode;
        DeviceCamera device = CreateDevice(legacyId, "", code);
        var mqtt = CreateMqtt(device);

        Assert.Throws<InvalidOperationException>(() => mqtt.Open(legacyId, TakeImageMode.Measure_Normal, 16));

        Assert.Null(mqtt.Sent);
        Assert.Empty(ReadId(device.PhyCamera!.Id)!);
        Assert.Equal(legacyId, ReadId(device.SysResourceModel.Id));
        Assert.Equal(code, device.PhyCamera.Code);
        Assert.Equal(code, device.Config.CameraCode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ReconnectingRetriesPhysicalOrLogicalIdentityPersistence(bool hasValidLegacyId, bool failAfterPhysicalSave)
    {
        DeviceCamera device = CreateDevice(hasValidLegacyId ? CameraId : "old-logical-id", "", CameraCode);
        var native = new LocalCameraSessionTests.FakeNative { CameraIds = ["unrelated-id", CameraId] };
        int attempts = 0;
        using var session = new LocalCameraSession(native, new CameraBackendState(true), device.Config, () =>
        {
            attempts++;
            if (attempts == 1)
            {
                if (failAfterPhysicalSave)
                    PhysicalCameraIdentity.SynchronizeResolvedIdentity(device.PhyCamera!, CameraId, dao.GetAll(), [], row =>
                        row.Id == device.SysResourceModel.Id ? throw new IOException("temporary logical identity save failure") : dao.Save(row));
                throw new IOException("temporary physical identity save failure");
            }
            device.SaveResolvedCameraId(device.Config.CameraID);
        }, resolveCameraId: id => device.ResolveCameraId(id));

        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("", TakeImageMode.Measure_Normal, 16));
        Assert.True(session.UseOpened(_ => true));
        Assert.Equal(1, attempts);
        Assert.Equal(failAfterPhysicalSave ? CameraId : "", ReadId(device.PhyCamera!.Id));
        session.Close(true);

        Assert.Equal(cvErrorDefine.CV_ERR_SUCCESS, session.Open("", TakeImageMode.Measure_Normal, 16));

        Assert.Equal(2, attempts);
        AssertPersisted(device);
        Assert.Equal(CameraId, native.OpenedId);
        Assert.Equal(hasValidLegacyId ? 0 : failAfterPhysicalSave ? 1 : 2, native.Scans);
        Assert.True(session.UseOpened(_ => true));
        Assert.Equal(CameraCode, device.PhyCamera.Code);
        Assert.Equal(CameraCode, device.Config.CameraCode);
    }

    private DeviceCamera CreateDevice(string logicalId, string? physicalId, string code)
    {
        var physicalRow = new SysResourceModel { Type = (int)ServiceTypes.PhyCamera, Code = code, Name = null, Remark = "Offline",
            Value = new JObject { ["CameraID"] = physicalId, ["PhysicalExtension"] = 123 }.ToString() };
        dao.Save(physicalRow);
        var physical = (PhyCamera)RuntimeHelpers.GetUninitializedObject(typeof(PhyCamera));
        physical.SysResourceModel = physicalRow;
        physical.Config = JsonConvert.DeserializeObject<ConfigPhyCamera>(physicalRow.Value!)!;
        var logicalRow = new SysResourceModel { Type = (int)ServiceTypes.Camera, Code = "DEV.IdentityTest",
            Value = new JObject { ["CameraCode"] = code, ["CameraID"] = logicalId, ["ExpTimeMin"] = 7, ["LogicalExtension"] = "preserved" }.ToString() };
        dao.Save(logicalRow);
        var device = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
        device.SysResourceModel = logicalRow;
        device.Config = JsonConvert.DeserializeObject<ConfigCamera>(logicalRow.Value!)!;
        typeof(DeviceCamera).GetField("<CameraBackend>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(device, new CameraBackendState(false));
        device.PhyCamera = physical;
        return device;
    }

    private string? ReadId(int id) => JObject.Parse(dao.GetById(id)!.Value!).Value<string>("CameraID");

    private void AssertPersisted(DeviceCamera device)
    {
        var physical = dao.GetById(device.PhyCamera!.Id)!;
        var logical = dao.GetById(device.SysResourceModel.Id)!;
        Assert.Equal(CameraId, physical.Name);
        Assert.Equal(CameraId, ReadId(physical.Id));
        Assert.Equal(CameraId, ReadId(logical.Id));
        Assert.Equal(CameraId, device.Config.CameraID);
        Assert.Equal(123, JObject.Parse(physical.Value!).Value<int>("PhysicalExtension"));
        Assert.Equal("preserved", JObject.Parse(logical.Value!).Value<string>("LogicalExtension"));
        Assert.Equal(7, JObject.Parse(logical.Value!).Value<int>("ExpTimeMin"));
        Assert.Equal(device.Config.CameraCode, physical.Code);
    }

    private static CapturingMqttCamera CreateMqtt(DeviceCamera device)
    {
        var mqtt = (CapturingMqttCamera)RuntimeHelpers.GetUninitializedObject(typeof(CapturingMqttCamera));
        mqtt.Device = device;
        mqtt.Config = device.Config;
        return mqtt;
    }

    private sealed class CapturingMqttCamera : MQTTCamera
    {
        private CapturingMqttCamera() : base(null!) { }
        public MsgSend? Sent { get; private set; }
        internal override MsgRecord PublishAsyncClient(MsgSend msg, double timeout = 30000)
        {
            Sent = msg;
            return new MsgRecord { MsgSend = msg };
        }
    }

    public void Dispose()
    {
        SysResourceDao.Instance = previousDao;
        foreach (string file in Directory.EnumerateFiles(directory)) File.Delete(file);
        Directory.Delete(directory);
    }
}
