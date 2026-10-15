using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Services.Types;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.PhySpectrums;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class OfflineDeviceConfigurationTests
{
    [Fact]
    public void TerminalRenamePersistsOnlyNameAndPreservesUnknownConfiguration()
    {
        WithStore(store =>
        {
            var previousResources = SysResourceDao.Instance;
            try
            {
                SysResourceDao.Instance = new SysResourceDao(store, () => false);
                const string originalJson = """{"Name":"Original","Code":"SVR.Camera.Default","ServiceType":1,"SendTopic":"camera/CMD","SubscribeTopic":"camera/STATUS","ServiceToken":"token","Future":{"Timestamp":"2026-10-02T12:34:56.1234567+08:00","Enabled":true}}""";
                var resource = new SysResourceModel { Name = "Original", Code = "SVR.Camera.Default", Type = 1, Pid = -42, Value = originalJson, Remark = "keep", TenantId = 7 };
                SysResourceDao.Instance.Save(resource);
                var terminal = (TerminalService)RuntimeHelpers.GetUninitializedObject(typeof(TerminalService));
                terminal.SysResourceModel = resource;
                terminal.Config = new TerminalServiceConfig { Name = "Original", Code = resource.Code, ServiceType = ServiceTypes.Camera, SendTopic = "camera/CMD", SubscribeTopic = "camera/STATUS", ServiceToken = "token" };
                var changed = new List<string?>();
                terminal.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

                terminal.Rename("重命名服务");

                var saved = new SysResourceDao(new LocalTemplateStore(store.DatabasePath), () => false).GetById(resource.Id)!;
                Assert.Equal("重命名服务", saved.Name);
                Assert.Equal(saved.Name, terminal.Name);
                Assert.Equal(saved.Name, terminal.Config.Name);
                Assert.Contains(nameof(TerminalService.Name), changed);
                Assert.Equal(resource.Code, saved.Code);
                Assert.Equal(1, saved.Type);
                Assert.Equal(-42, saved.Pid);
                Assert.Equal("keep", saved.Remark);
                Assert.Equal(7, saved.TenantId);
                var expected = JObject.Parse(originalJson);
                expected["Name"] = saved.Name;
                Assert.True(JToken.DeepEquals(expected, JObject.Parse(saved.Value!)));
                Assert.Contains("2026-10-02T12:34:56.1234567+08:00", saved.Value);
                Assert.Equal("camera/CMD", terminal.Config.SendTopic);
                Assert.Equal("camera/STATUS", terminal.Config.SubscribeTopic);
                Assert.Equal("token", terminal.Config.ServiceToken);
                Assert.Equal(ServiceTypes.Camera, terminal.Config.ServiceType);
            }
            finally { SysResourceDao.Instance = previousResources; }
        });
    }

    [Fact]
    public void FailedTerminalRenameKeepsTheOriginalNamesAndConfiguration()
    {
        WithStore(store =>
        {
            var previousResources = SysResourceDao.Instance;
            try
            {
                SysResourceDao.Instance = new SysResourceDao(store, () => false);
                var resource = new SysResourceModel { Name = "Original", Code = "SVR.Camera.Default", Type = 1, Value = "{\"Name\":\"Original\",\"Future\":42}" };
                SysResourceDao.Instance.Save(resource);
                SysResourceDao.Instance.DeleteById(resource.Id);
                var terminal = (TerminalService)RuntimeHelpers.GetUninitializedObject(typeof(TerminalService));
                terminal.SysResourceModel = resource;
                terminal.Config = new TerminalServiceConfig { Name = "Original", Code = resource.Code };
                string? originalValue = resource.Value;

                Assert.Throws<InvalidDataException>(() => terminal.Rename("Changed"));

                Assert.Equal("Original", terminal.Name);
                Assert.Equal("Original", terminal.Config.Name);
                Assert.Equal(originalValue, resource.Value);
                Assert.Null(SysResourceDao.Instance.GetById(resource.Id));
            }
            finally { SysResourceDao.Instance = previousResources; }
        });
    }

    [Fact]
    public void PhysicalSpectrumRegistrationAndLicenseRenewalUseLocalConfiguration()
    {
        WithStore(store =>
        {
            var previousResources = SysResourceDao.Instance;
            var previousLicenses = PhyLicenseDao.Instance;
            try
            {
                bool connected = false;
                SysResourceDao.Instance = new SysResourceDao(store, () => connected);
                PhyLicenseDao.Instance = new PhyLicenseDao(store, () => connected);
                PhySpectrumStore.Register(" spectrum-1 ");
                PhySpectrumStore.Register("SPECTRUM-1");
                Assert.Single(SysResourceDao.Instance.GetAll());
                var license = new LicenseModel { MacAddress = "spectrum-1", LiceType = 1, ExpiryDate = DateTime.Today.AddDays(2) };
                PhySpectrumStore.SaveLicense(license);
                PhySpectrumStore.SaveLicense(new LicenseModel { MacAddress = "SPECTRUM-1", LiceType = 1, ExpiryDate = DateTime.Today.AddDays(20) });
                var spectrum = Assert.Single(PhySpectrumStore.Load());
                Assert.True(spectrum.ResourceId < -1);
                Assert.Equal(license.Id, spectrum.License!.Id);
                Assert.Equal(DateTime.Today.AddDays(20), spectrum.License.ExpiryDate);
                PhyLicenseDao.Instance.Save(new LicenseModel { MacAddress = "camera-1", LiceType = 0 });
                Assert.Throws<InvalidOperationException>(() => PhySpectrumStore.SaveLicense(new LicenseModel { MacAddress = "camera-1", LiceType = 1 }));
                Assert.Throws<InvalidOperationException>(() => PhySpectrumStore.SaveLicense(new LicenseModel { Id = 9, MacAddress = "spectrum-1", LiceType = 1 }));
                Assert.Equal(0, PhyLicenseDao.Instance.GetByMAC("camera-1")!.LiceType);
                connected = true;
                PhySpectrumStore.SaveLicense(new LicenseModel { MacAddress = "SPECTRUM-1", LiceType = 1, ExpiryDate = DateTime.Today.AddDays(30) }, useLocal: true);
                PhySpectrumStore.Register("SPECTRUM-1", useLocal: true);
                Assert.Equal(DateTime.Today.AddDays(30), Assert.Single(PhySpectrumStore.Load(useLocal: true)).License!.ExpiryDate);
            }
            finally
            {
                SysResourceDao.Instance = previousResources;
                PhyLicenseDao.Instance = previousLicenses;
            }
        });
    }

    [Fact]
    public void DeviceHierarchyAndLicenseSurviveReopeningWithoutMySql()
    {
        WithStore(store =>
        {
            var resources = new SysResourceDao(store, () => false);
            var terminal = new SysResourceModel { Name = "本地相机", Type = 1, Code = "local.camera" };
            Assert.Equal(1, resources.Save(terminal));
            var camera = new SysResourceModel { Name = "Camera", Type = 1, Pid = terminal.Id, Value = "{\"CameraID\":\"test-camera\"}" };
            resources.Save(camera);
            var licenses = new PhyLicenseDao(store, () => false);
            var license = new LicenseModel { MacAddress = "test-camera", ExpiryDate = DateTime.Today.AddDays(10) };
            licenses.Save(license);

            var reopened = new SysResourceDao(new LocalTemplateStore(store.DatabasePath), () => false);
            Assert.True(terminal.Id <= -2);
            Assert.Equal(camera.Value, Assert.Single(reopened.GetAllByPid(terminal.Id)).Value);
            Assert.Equal(license.Id, new PhyLicenseDao(new LocalTemplateStore(store.DatabasePath), () => false).GetByMAC("TEST-CAMERA")!.Id);
            Assert.Equal(2, reopened.GetAllType(1).Count);
            Assert.Null(JObject.Parse(Assert.Single(store.List("device-license")).Payload)["LicenseContent"]);
            Assert.Empty(store.List("flow"));
        });
    }

    [Fact]
    public void ExistingLocalObjectsStayLocalAfterReconnectAndDeletedObjectsCannotReappear()
    {
        WithStore(store =>
        {
            bool connected = false;
            var resources = new SysResourceDao(store, () => connected);
            var camera = new SysResourceModel { Name = "initial", Type = 1 };
            resources.Save(camera);
            connected = true;
            camera.Name = "edited";
            resources.Save(camera);
            Assert.Equal("edited", resources.GetById(camera.Id)!.Name);
            var child = new SysResourceModel { Name = "local child", Pid = camera.Id, Type = 31 };
            resources.Save(child);
            Assert.Single(resources.GetAllByPid(camera.Id));
            resources.DeleteById(camera.Id);
            Assert.Throws<InvalidDataException>(() => resources.Save(camera));
            connected = false;
            Assert.Throws<InvalidOperationException>(() => resources.Save(new SysResourceModel { Id = 42 }));
            Assert.Throws<InvalidOperationException>(() => resources.DeleteById(42));
        });
    }

    [Fact]
    public void GroupLinksRemainLocalAndExpiredCleanupRechecksRenewals()
    {
        WithStore(store =>
        {
            var resources = new SysResourceDao(store, () => false);
            var group = new SysResourceModel { Name = "group", Type = 1000 };
            var calibration = new SysResourceModel { Name = "dark", Type = 31 };
            resources.Save(group);
            resources.Save(calibration);
            resources.ReplaceGroupResources(group.Id, [calibration.Id]);
            Assert.Equal(calibration.Id, Assert.Single(resources.GetGroupResourceItems(group.Id)).Id);
            Assert.Throws<InvalidOperationException>(() => resources.ReplaceGroupResources(group.Id, [42]));

            var licenses = new PhyLicenseDao(store, () => false);
            DateTime cutoff = DateTime.Today;
            var expired = new LicenseModel { MacAddress = "expired", ExpiryDate = cutoff.AddDays(-1) };
            var renewed = new LicenseModel { MacAddress = "renewed", ExpiryDate = cutoff.AddDays(-1) };
            licenses.Save(expired);
            licenses.Save(renewed);
            int[] confirmedIds = [expired.Id, renewed.Id];
            renewed.ExpiryDate = cutoff.AddDays(30);
            licenses.Save(renewed);
            Assert.Equal(1, licenses.DeleteExpiredAsync(confirmedIds, cutoff).GetAwaiter().GetResult());
            Assert.Equal(renewed.Id, Assert.Single(licenses.GetAll()).Id);
        });
    }

    private static void WithStore(Action<LocalTemplateStore> action)
    {
        string parent = Path.Combine(Path.GetTempPath(), "ColorVision-offline-device-tests");
        string directory = Path.GetFullPath(Path.Combine(parent, Guid.NewGuid().ToString("N")));
        try { action(new LocalTemplateStore(Path.Combine(directory, "ColorVision.Local.db"))); }
        finally
        {
            Assert.StartsWith(Path.GetFullPath(parent) + Path.DirectorySeparatorChar, directory, StringComparison.OrdinalIgnoreCase);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
