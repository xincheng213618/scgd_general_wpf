using ColorVision.Engine;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.Camera.Configs;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class DeviceCameraAssociationTests
{
    [Fact]
    public void SwitchingPhysicalCameraReplacesThePreviousLocalIdBeforeConnecting()
    {
        DeviceCamera device = CreateDevice(new ConfigCamera { CameraCode = "physical-1", CameraID = "camera-1" });
        PhyCamera first = CreatePhysicalCamera("physical-1", "camera-1");
        PhyCamera second = CreatePhysicalCamera("physical-2", "camera-2");
        device.PhyCamera = first;

        // Saving an edited/imported binding must synchronize its hardware identity too.
        device.Config.CameraCode = second.Code;
        device.UpdatePhyCameraBinding(second);

        Assert.Equal("camera-2", device.Config.CameraID);
        Assert.Same(second, device.PhyCamera);
        Assert.Same(device, second.DeviceCamera);
        Assert.Null(first.DeviceCamera);
    }

    [Fact]
    public void SavingTheSamePhysicalBindingPreservesItsLocalRuntimeId()
    {
        DeviceCamera device = CreateDevice(new ConfigCamera { CameraCode = "physical-1", CameraID = "local-runtime-id" });
        device.PhyCamera = CreatePhysicalCamera("physical-1", "physical-id");

        // A reloaded physical object with the same code is still the same binding.
        device.UpdatePhyCameraBinding(CreatePhysicalCamera("physical-1", "physical-id"));

        Assert.Equal("local-runtime-id", device.Config.CameraID);
    }

    [Theory]
    [InlineData("local")]
    [InlineData("video")]
    [InlineData("service")]
    public void ActiveCameraPreventsRebindingAndPreservesTheCurrentIdentity(string owner)
    {
        var backend = new CameraBackendState(true);
        DeviceCamera device = CreateDevice(new ConfigCamera { CameraCode = "physical-1", CameraID = "camera-1" }, backend);
        PhyCamera first = CreatePhysicalCamera("physical-1", "camera-1");
        device.PhyCamera = first;
        if (owner == "local") backend.SetLocalStatus(DeviceStatusType.Opened);
        else if (owner == "video") backend.BeginLocalOpen(video: true);
        else backend.ObserveService(DeviceStatusType.Opened);

        var error = Assert.Throws<InvalidOperationException>(() => device.UpdatePhyCameraBinding(CreatePhysicalCamera("physical-2", "camera-2")));

        Assert.Equal(EngineLocalization.Get("Camera_PhysicalBindingInUse"), error.Message);
        Assert.Same(first, device.PhyCamera);
        Assert.Equal("physical-1", device.Config.CameraCode);
        Assert.Equal("camera-1", device.Config.CameraID);
        device.EnsurePhyCameraBindingCanChange("physical-1");
    }

    private static DeviceCamera CreateDevice(ConfigCamera config, CameraBackendState? backend = null)
    {
        DeviceCamera device = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
        device.Config = config;
        typeof(DeviceCamera).GetField("<CameraBackend>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(device, backend ?? new CameraBackendState(true));
        return device;
    }

    private static PhyCamera CreatePhysicalCamera(string code, string cameraId)
    {
        PhyCamera camera = (PhyCamera)RuntimeHelpers.GetUninitializedObject(typeof(PhyCamera));
        camera.SysResourceModel = new SysResourceModel { Code = code };
        camera.Config = new ConfigPhyCamera { Code = code, CameraID = cameraId };
        return camera;
    }

    private static readonly MethodInfo AttachPhyCamera = typeof(DeviceCamera).GetMethod(
        "AttachPhyCamera",
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("DeviceCamera.AttachPhyCamera was not found.");

    private static readonly FieldInfo CameraLicenseModelField = typeof(PhyCamera).GetField(
        "_CameraLicenseModel",
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("PhyCamera license backing field was not found.");

    [Fact]
    public void AttachPhyCameraBindsWithoutPersistingLicenseAssociation()
    {
        DeviceCamera deviceCamera = (DeviceCamera)RuntimeHelpers.GetUninitializedObject(typeof(DeviceCamera));
        deviceCamera.SysResourceModel = new SysResourceModel { Id = 901 };

        PhyCamera phyCamera = (PhyCamera)RuntimeHelpers.GetUninitializedObject(typeof(PhyCamera));
        phyCamera.SysResourceModel = new SysResourceModel();
        LicenseModel license = new()
        {
            DevCameraId = 701,
            DevCaliId = 702,
        };
        CameraLicenseModelField.SetValue(phyCamera, license);

        AttachPhyCamera.Invoke(deviceCamera, [phyCamera]);

        Assert.Same(phyCamera, deviceCamera.PhyCamera);
        Assert.Same(deviceCamera, phyCamera.DeviceCamera);
        Assert.Equal(701, license.DevCameraId);
        Assert.Equal(702, license.DevCaliId);

        AttachPhyCamera.Invoke(deviceCamera, [null]);

        Assert.Null(deviceCamera.PhyCamera);
        Assert.Null(phyCamera.DeviceCamera);
        Assert.Equal(701, license.DevCameraId);
        Assert.Equal(702, license.DevCaliId);
    }
}
