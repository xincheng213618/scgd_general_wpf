using ColorVision.Engine;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Licenses;
using ColorVision.Engine.Services.Types;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace ColorVision.UI.Tests;

public sealed class PhyCameraLicenseImportPolicyTests : IDisposable
{
    private readonly string _tempDirectory = Directory.CreateTempSubdirectory("ColorVision-PhysicalCamera-").FullName;

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

        string source = File.ReadAllText(FindPhyCameraSource());
        Assert.DoesNotContain("Task.Run(() => UploadLicenseNet())", source, StringComparison.Ordinal);
        Assert.Contains("await InvokeOnApplicationDispatcherAsync(() => SetLicense(fileName))", source, StringComparison.Ordinal);
    }

    private static string FindPhyCameraSource([CallerFilePath] string testSourcePath = "")
    {
        string testDirectory = Path.GetDirectoryName(testSourcePath)!;
        return Path.GetFullPath(Path.Combine(
            testDirectory,
            "..",
            "..",
            "Engine",
            "ColorVision.Engine",
            "Services",
            "PhyCameras",
            "PhyCamera.cs"));
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
