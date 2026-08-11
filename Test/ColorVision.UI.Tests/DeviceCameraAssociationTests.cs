using ColorVision.Engine;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class DeviceCameraAssociationTests
{
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
