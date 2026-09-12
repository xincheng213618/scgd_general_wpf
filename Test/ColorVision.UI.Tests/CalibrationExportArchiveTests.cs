using ColorVision.Engine.Services.PhyCameras.Group;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace ColorVision.UI.Tests;

public sealed class CalibrationExportArchiveTests : IDisposable
{
    private readonly string _tempDirectory = Directory.CreateTempSubdirectory("ColorVision-CalibrationExport-").FullName;

    [Fact]
    public void SuccessfulExportReplacesDestinationAndReportsEveryEntry()
    {
        string sourcePath = Path.Combine(_tempDirectory, "source.dat");
        File.WriteAllText(sourcePath, "calibration-data");
        string destinationPath = Path.Combine(_tempDirectory, "camera.zip");
        File.WriteAllText(destinationPath, "old-export");
        List<CalibrationExportProgress> progress = new();
        CalibrationExportPlan plan = new(
            [new CalibrationExportFile(sourcePath, "Calibration/Uniformity/source.dat")],
            [new CalibrationExportText("Camera.cfg", "camera-config")]);

        CalibrationExportArchive.CreateOrReplace(destinationPath, plan, new InlineProgress<CalibrationExportProgress>(progress.Add));

        using ZipArchive archive = ZipFile.OpenRead(destinationPath);
        Assert.Equal(2, archive.Entries.Count);
        Assert.Equal("calibration-data", ReadEntry(archive, "Calibration/Uniformity/source.dat"));
        Assert.Equal("camera-config", ReadEntry(archive, "Camera.cfg"));
        Assert.NotEmpty(progress);
        Assert.Equal(0, progress.First().Percent);
        Assert.Equal(100, progress.Last().Percent);
        Assert.Empty(Directory.GetFiles(_tempDirectory, ".camera.zip.*.tmp"));
    }

    [Fact]
    public void ExportFailurePreservesExistingDestination()
    {
        string destinationPath = Path.Combine(_tempDirectory, "camera.zip");
        byte[] existingExport = [1, 2, 3, 4, 5];
        File.WriteAllBytes(destinationPath, existingExport);
        CalibrationExportPlan plan = new(
            [new CalibrationExportFile(Path.Combine(_tempDirectory, "missing.dat"), "Calibration/missing.dat")],
            []);

        Assert.Throws<FileNotFoundException>(() => CalibrationExportArchive.CreateOrReplace(destinationPath, plan));

        Assert.Equal(existingExport, File.ReadAllBytes(destinationPath));
        Assert.Empty(Directory.GetFiles(_tempDirectory, ".camera.zip.*.tmp"));
    }

    [Fact]
    public void CvcalUsesZipContainerAndCanBeExtractedBySharedArchiveBackend()
    {
        string archivePath = Path.Combine(_tempDirectory, "camera.cvcal");
        string extractionPath = Path.Combine(_tempDirectory, "extracted");
        List<CalibrationExportProgress> progress = new();
        CalibrationExportPlan plan = new(
            [],
            [
                new CalibrationExportText("Camera.cfg", "camera-config"),
                new CalibrationExportText("Calibration/group.cfg", "group-config")
            ]);

        CalibrationExportArchive.CreateOrReplace(archivePath, plan);
        CalibrationExportArchive.ExtractToDirectory(
            archivePath,
            extractionPath,
            new InlineProgress<CalibrationExportProgress>(progress.Add));

        Assert.Equal("camera-config", File.ReadAllText(Path.Combine(extractionPath, "Camera.cfg")));
        Assert.Equal("group-config", File.ReadAllText(Path.Combine(extractionPath, "Calibration", "group.cfg")));
        Assert.Equal(0, progress.First().Percent);
        Assert.Equal(100, progress.Last().Percent);
    }

    [Fact]
    public void ExtractionRejectsEntriesOutsideWorkspace()
    {
        string archivePath = Path.Combine(_tempDirectory, "unsafe.cvcal");
        using (ZipArchive archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            ZipArchiveEntry entry = archive.CreateEntry("../outside.txt");
            using StreamWriter writer = new(entry.Open());
            writer.Write("unsafe");
        }

        string extractionPath = Path.Combine(_tempDirectory, "unsafe-extracted");
        Assert.Throws<IOException>(() => CalibrationExportArchive.ExtractToDirectory(archivePath, extractionPath));
        Assert.False(File.Exists(Path.Combine(_tempDirectory, "outside.txt")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }

    private static string ReadEntry(ZipArchive archive, string entryPath)
    {
        using StreamReader reader = new(archive.GetEntry(entryPath)!.Open());
        return reader.ReadToEnd();
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
