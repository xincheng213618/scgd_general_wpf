using CameraTest.Application;
using CameraTest.Models;
using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace CameraTest.Tests;

public sealed class ProductionArchiveTests
{
    private static ArchiveSettings Settings() => new()
    {
        DeviceSerial = "SN-测试/001", Operator = "tester", Batch = "batch-1",
        RootDirectory = Path.Combine(Path.GetTempPath(), "CameraTest-archive-tests", Guid.NewGuid().ToString("N"))
    };

    [Fact]
    public void ImportedFileDoesNotInheritCurrentCameraParametersAndArchivePreserves16BitPixels()
    {
        var settings = Settings();
        byte[] pixels = Enumerable.Range(0, 8 * 6 * 6).Select(i => (byte)(i % 256)).ToArray();
        var frame = new TestFrame(new(pixels, 8, 6, 16, 3, 48, DateTimeOffset.Now), "old-image.bmp", FrameSourceKind.ImageFile,
            new() { ExposureMilliseconds = 321, CameraId = "current-camera" });
        string folder = ProductionArchive.Save(frame, null, new TestProfile(), settings);
        Assert.Equal(Path.GetFullPath(settings.RootDirectory), Path.GetDirectoryName(folder));
        Assert.False(folder.EndsWith(".pending", StringComparison.Ordinal));
        Assert.Equal(pixels, TestFrame.Open(Path.Combine(folder, "image.png")).Data.Pixels);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "record.json")));
        Assert.Equal(frame.Id, doc.RootElement.GetProperty("Id").GetGuid());
        Assert.Equal(settings.DeviceSerial, doc.RootElement.GetProperty("DeviceSerial").GetString());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("requestedAcquisitionSettings").ValueKind);
        Assert.Equal("imported_at", doc.RootElement.GetProperty("imageTimeKind").GetString());
        Assert.Equal("not_run", doc.RootElement.GetProperty("analysisStatus").GetString());
        foreach (var hash in doc.RootElement.GetProperty("filesSha256").EnumerateObject())
            Assert.Equal(hash.Value.GetString(), Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder, hash.Name)))));
        Assert.False(File.Exists(Path.Combine(folder, "results.json")));
    }

    [Fact]
    public void CaptureKeepsAcquisitionSnapshotWhenSettingsChangeAndArchivesMatchingResult()
    {
        var settings = Settings();
        var camera = new StandaloneCameraOptions { CameraId = "sdk-123", ExposureMilliseconds = 42, Gain = 2 };
        var frame = new TestFrame(new(new byte[100], 10, 10, 8, 1, 10, DateTimeOffset.Now), "camera", FrameSourceKind.Capture, camera);
        camera.ExposureMilliseconds = 999;
        var result = new FrameAnalysis(frame.Id, frame.Source, frame.Data.CapturedAt, 10, 10, 8, 1, 0.25, new(), 0, []);
        string folder = ProductionArchive.Save(frame, result, new TestProfile { Camera = camera }, settings);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "record.json")));
        Assert.Equal(42, doc.RootElement.GetProperty("requestedAcquisitionSettings").GetProperty("ExposureMilliseconds").GetDouble());
        Assert.Equal("captured_at", doc.RootElement.GetProperty("imageTimeKind").GetString());
        Assert.Equal("completed", doc.RootElement.GetProperty("analysisStatus").GetString());
        using var analysis = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "results.json")));
        Assert.Equal(frame.Id, analysis.RootElement.GetProperty("measurement").GetProperty("FrameId").GetGuid());
        Assert.True(File.Exists(Path.Combine(folder, "metrics.csv")));
    }

    [Fact]
    public void ArchiveIncludesFocusHistoryAndItsIntegrityHash()
    {
        var frame = new TestFrame(new(new byte[100], 10, 10, 8, 1, 10, DateTimeOffset.Now), "live", FrameSourceKind.Live, new());
        var history = new FocusHistory();
        var edges = Enum.GetValues<BmwEdgeId>().Select(id => new BmwEdgeAnalysis(id, default, false, "target_not_found", null)).ToArray();
        var result = new FrameAnalysis(frame.Id, frame.Source, frame.Data.CapturedAt, 10, 10, 8, 1, 0.25, new(), 0,
            [new("A", default, false, "target_not_found", default, 0, 0, edges)]);
        history.Add(result);
        string folder = ProductionArchive.Save(frame, result, new(), Settings(), history);
        using var record = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "record.json")));
        Assert.Equal(1, record.RootElement.GetProperty("focusFrames").GetInt32());
        Assert.Contains(frame.Id.ToString(), File.ReadAllText(Path.Combine(folder, "focus.csv")));
        Assert.Contains("target_not_found", File.ReadAllText(Path.Combine(folder, "focus.csv")));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder, "focus.csv")))),
            record.RootElement.GetProperty("filesSha256").GetProperty("focus.csv").GetString());
    }

    [Fact]
    public void MissingSerialOrMismatchedFrameCannotCreateAnArchive()
    {
        var settings = Settings();
        var frame = new TestFrame(new(new byte[100], 10, 10, 8, 1, 10, DateTimeOffset.Now), "file");
        Assert.Throws<ArgumentException>(() => ProductionArchive.Save(frame, null, new(), settings with { DeviceSerial = "" }));
        var result = new FrameAnalysis(Guid.NewGuid(), "different", DateTimeOffset.Now, 10, 10, 8, 1, 0.25, new(), 0, []);
        Assert.Throws<InvalidOperationException>(() => ProductionArchive.Save(frame, result, new(), settings));
        Assert.False(Directory.Exists(settings.RootDirectory));
    }
}
