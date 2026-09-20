using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class OfflineDeviceConfigurationTests
{
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
