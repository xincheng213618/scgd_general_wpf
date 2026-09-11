using ColorVision.Engine;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.Types;

namespace ColorVision.UI.Tests;

public sealed class PhyCameraLicenseImportPolicyTests
{
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
}
