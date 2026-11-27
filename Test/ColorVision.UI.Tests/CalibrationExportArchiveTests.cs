using ColorVision.Engine;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.PhyCameras.Configs;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Types;
using cvColorVision;
using Newtonsoft.Json;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;

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
    public void SameTitleResourcesKeepBothFilesAndTheirGroupReferences()
    {
        const string sharedTitle = "four-color.dat";
        const string revisedFileName = "four-color_20250715171119.dat";
        var camera = (PhyCamera)RuntimeHelpers.GetUninitializedObject(typeof(PhyCamera));
        camera.SysResourceModel = new SysResourceModel { Code = "TEST-CAMERA" };
        camera.Config = new ConfigPhyCamera();
        camera.Config.FileServerCfg.FileBasePath = _tempDirectory;

        var original = CreateCalibrationResource(70, sharedTitle, sharedTitle);
        var revised = CreateCalibrationResource(90, sharedTitle, revisedFileName);
        camera.VisualChildren = [original, revised, CreateGroup("Original", original), CreateGroup("F8", revised)];

        string cameraDirectory = Path.Combine(_tempDirectory, camera.Code, "cfg");
        Directory.CreateDirectory(cameraDirectory);
        File.WriteAllText(Path.Combine(cameraDirectory, sharedTitle), "original coefficients");
        File.WriteAllText(Path.Combine(cameraDirectory, revisedFileName), "revised coefficients");
        string archivePath = Path.Combine(_tempDirectory, "same-title.cvcal");

        CalibrationExportArchive.CreateOrReplace(archivePath, CalibrationArchivePlanBuilder.Create(camera));

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        Assert.Equal("original coefficients", ReadEntry(archive, $"Calibration/LumFourColor/{sharedTitle}"));
        Assert.Equal("revised coefficients", ReadEntry(archive, $"Calibration/LumFourColor/{revisedFileName}"));
        var index = JsonConvert.DeserializeObject<Dictionary<string, List<ZipCalibrationItem>>>(ReadEntry(archive, "Calibration.cfg"))!;
        CalibrationPackageManifest manifest = new(index.Values.SelectMany(items => items));
        var originalRef = Assert.Single(JsonConvert.DeserializeObject<List<ZipCalibrationItem>>(ReadEntry(archive, "Calibration/Original.cfg"))!);
        var revisedRef = Assert.Single(JsonConvert.DeserializeObject<List<ZipCalibrationItem>>(ReadEntry(archive, "Calibration/F8.cfg"))!);
        Assert.Equal(2, manifest.Items.Count);
        Assert.Equal(sharedTitle, manifest.ResolveGroupItem(originalRef)!.FileName);
        Assert.Equal(revisedFileName, manifest.ResolveGroupItem(revisedRef)!.FileName);
        Assert.Null(manifest.ResolveGroupItem(new ZipCalibrationItem { Title = sharedTitle, CalibrationType = CalibrationType.LumFourColor }));
        Assert.Same(revised.SysResourceModel, manifest.FindExistingResource(revisedRef, [original.SysResourceModel, revised.SysResourceModel], revisedFileName));
        Assert.Null(manifest.FindExistingResource(revisedRef, [original.SysResourceModel], revisedFileName));
    }

    [Fact]
    public void LegacyTitleOnlyGroupAndSingleResourceReplacementRemainSupported()
    {
        ZipCalibrationItem item = new() { Title = "four-color", FileName = "four-color.dat", CalibrationType = CalibrationType.LumFourColor };
        CalibrationPackageManifest manifest = new([item]);
        SysResourceModel existing = new() { Value = "old-storage-name.dat" };

        Assert.Same(item, manifest.ResolveGroupItem(new ZipCalibrationItem { Title = item.Title }));
        Assert.Same(existing, manifest.FindExistingResource(item, [existing], item.FileName));
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

    private static CalibrationResource CreateCalibrationResource(int id, string title, string storageName)
    {
        var resource = (CalibrationResource)RuntimeHelpers.GetUninitializedObject(typeof(CalibrationResource));
        resource.SysResourceModel = new SysResourceModel { Id = id, Type = (int)ServiceTypes.LumFourColor, Name = title, Value = storageName };
        resource.Config = new CalibrationFileConfig { FileName = title, Title = title };
        return resource;
    }

    private static GroupResource CreateGroup(string name, CalibrationResource resource)
    {
        var group = (GroupResource)RuntimeHelpers.GetUninitializedObject(typeof(GroupResource));
        group.Name = name;
        group.VisualChildren = [resource];
        return group;
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
