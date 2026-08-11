using ColorVision.Engine.Services.PhyCameras;
using System;
using System.IO;
using System.IO.Compression;

namespace ColorVision.UI.Tests;

public sealed class PhyCameraRestoreArchiveTests : IDisposable
{
    private readonly string _tempDirectory = Directory.CreateTempSubdirectory("ColorVision-PhyCameraRestore-").FullName;

    [Fact]
    public void CompressionFailurePreservesExistingRestorePoint()
    {
        string destinationPath = Path.Combine(_tempDirectory, "camera.cvcal");
        byte[] existingArchive = [1, 2, 3, 4, 5];
        File.WriteAllBytes(destinationPath, existingArchive);

        Assert.Throws<DirectoryNotFoundException>(() => PhyCameraRestoreArchive.CreateOrReplace(
            Path.Combine(_tempDirectory, "missing-source"),
            destinationPath));

        Assert.Equal(existingArchive, File.ReadAllBytes(destinationPath));
        Assert.Empty(Directory.GetFiles(_tempDirectory, ".camera.cvcal.*.tmp"));
    }

    [Fact]
    public void SuccessfulCompressionReplacesExistingRestorePoint()
    {
        string sourceDirectory = Directory.CreateDirectory(Path.Combine(_tempDirectory, "source")).FullName;
        File.WriteAllText(Path.Combine(sourceDirectory, "CameraConfig.cfg"), "new-config");
        string destinationPath = Path.Combine(_tempDirectory, "camera.cvcal");
        File.WriteAllText(destinationPath, "old-restore-point");

        PhyCameraRestoreArchive.CreateOrReplace(sourceDirectory, destinationPath);

        using ZipArchive archive = ZipFile.OpenRead(destinationPath);
        ZipArchiveEntry entry = Assert.Single(archive.Entries);
        Assert.Equal("CameraConfig.cfg", entry.FullName);
        using var reader = new StreamReader(entry.Open());
        Assert.Equal("new-config", reader.ReadToEnd());
        Assert.Empty(Directory.GetFiles(_tempDirectory, ".camera.cvcal.*.tmp"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}
