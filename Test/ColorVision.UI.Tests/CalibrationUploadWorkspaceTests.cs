using ColorVision.Engine.Services.PhyCameras;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public sealed class CalibrationUploadWorkspaceTests : IDisposable
{
    private readonly string _tempDirectory = Directory.CreateTempSubdirectory("ColorVision-CalibrationUpload-").FullName;

    [Fact]
    public void ConcurrentUploadsUseIndependentCleanupDirectories()
    {
        CalibrationUploadWorkspace first = CalibrationUploadWorkspace.Create(_tempDirectory);
        CalibrationUploadWorkspace second = CalibrationUploadWorkspace.Create(_tempDirectory);
        try
        {
            Assert.NotEqual(first.DirectoryPath, second.DirectoryPath);
            string secondPayload = Path.Combine(second.DirectoryPath, "Calibration.cfg");
            File.WriteAllText(secondPayload, "second-upload");

            first.Dispose();

            Assert.False(Directory.Exists(first.DirectoryPath));
            Assert.True(File.Exists(secondPayload));
        }
        finally
        {
            first.Dispose();
            second.Dispose();
        }

        Assert.Empty(Directory.GetDirectories(_tempDirectory));
    }

    [Fact]
    public void UploadAsyncMethodIsTaskBasedAndLegacyEntryPointIsNotAsyncVoid()
    {
        MethodInfo uploadAsync = typeof(PhyCamera).GetMethod(nameof(PhyCamera.UploadDataAsync))!;
        MethodInfo legacyUpload = typeof(PhyCamera).GetMethod(nameof(PhyCamera.UploadData))!;

        Assert.Equal(typeof(Task), uploadAsync.ReturnType);
        Assert.Equal(typeof(void), legacyUpload.ReturnType);
        Assert.Null(legacyUpload.GetCustomAttribute<AsyncStateMachineAttribute>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
