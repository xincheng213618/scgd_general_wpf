using CameraTest.Application;
using CameraTest.Models;
using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Media;

namespace CameraTest.Tests;

public sealed class CameraTestContractsTests
{
    [Fact]
    public void ProfilesRejectDuplicateIdsAndCoordinatesFromDifferentImage()
    {
        var profile = new TestProfile { ImageWidth = 400, ImageHeight = 300, Regions = new() { new("P1", 0, 0, 100, 100), new("P1", 200, 100, 100, 100) } };
        Assert.Throws<ArgumentException>(profile.Validate);
        profile.Regions[1] = new("P2", 350, 100, 100, 100);
        Assert.Throws<ArgumentException>(profile.Validate);
        profile.Regions[1] = new("P2", 200, 100, 100, 100);
        profile.Validate();
        var frame = new TestFrame(new(new byte[200 * 100], 200, 100, 8, 1, 200, DateTimeOffset.Now), "different-resolution");
        Assert.Throws<InvalidOperationException>(() => FrameAnalysis.Run(frame, profile));
    }

    [Fact]
    public void ProfileRoundTripKeepsRegionIdentityAndInputEncoding()
    {
        var profile = new TestProfile { ImageWidth = 400, ImageHeight = 300, Regions = new() { new("Point_7", 20, 30, 100, 120) }, Sfr = new() { InputEncoding = SfrInputEncoding.Linear, WhiteLevel = 4095 } };
        var restored = JsonSerializer.Deserialize<TestProfile>(JsonSerializer.Serialize(profile, ProfileStore.JsonOptions), ProfileStore.JsonOptions)!;
        restored.Validate();
        Assert.Equal(profile.Regions[0], restored.Regions[0]);
        Assert.Equal(4095, restored.Sfr.WhiteLevel);
        Assert.Equal(SfrInputEncoding.Linear, restored.Sfr.InputEncoding);
    }

    [Fact]
    public void Bgr16DisplayDoesNotAlterAnalysisPixelsOrDropPrecision()
    {
        byte[] source = { 0x34, 0x12, 0x78, 0x56, 0xbc, 0x9a };
        var frame = new TestFrame(new(source, 1, 1, 16, 3, 6, DateTimeOffset.Now), "test");
        var bitmap = frame.CreateBitmap();
        Assert.Equal(PixelFormats.Rgb48, bitmap.Format);
        var displayed = new byte[6];
        bitmap.CopyPixels(displayed, 6, 0);
        Assert.Equal(new byte[] { 0xbc, 0x9a, 0x78, 0x56, 0x34, 0x12 }, displayed);
        var analysis = frame.Read(image => { var bytes = new byte[6]; Marshal.Copy(image.pData, bytes, 0, bytes.Length); return bytes; });
        Assert.Equal(source, analysis);
        Assert.Equal(0x34, source[0]);
    }

    [Fact]
    public void MissingTargetsRetainFourRowsAndExportNullValues()
    {
        var target = new BmwTargetAnalysis("Point_9", new(0, 0, 100, 100), false, "target_not_found", default, 0, 0,
            Enum.GetValues<BmwEdgeId>().Select(id => new BmwEdgeAnalysis(id, default, false, "target_not_found", null)).ToArray());
        var result = new FrameAnalysis(Guid.NewGuid(), "sample", DateTimeOffset.Now, 100, 100, 8, 3, 0.25, new(), 0, new[] { target });
        var rows = result.Rows();
        Assert.Equal(4, rows.Count);
        Assert.All(rows, row => { Assert.Equal("Point_9", row.Target); Assert.Null(row.Mtf50); Assert.Null(row.Response); });
        var json = JsonSerializer.Serialize(result, ProfileStore.JsonOptions);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(100, parsed.RootElement.GetProperty("Targets")[0].GetProperty("SearchRoi").GetProperty("Width").GetInt32());
    }

    [Theory]
    [InlineData(0, 10, 8, 1, 10)]
    [InlineData(10, 10, 12, 1, 20)]
    [InlineData(10, 10, 16, 3, 30)]
    [InlineData(10000, 10000, 16, 3, 60000)]
    public void InvalidOrExcessiveFramesAreRejectedBeforeBufferAccess(int w, int h, int depth, int channels, int stride)
        => Assert.Throws<ArgumentException>(() => StandaloneCameraFrame.RequiredBytes(w, h, depth, channels, stride));

    [Fact]
    public async Task SessionConstructionAndDisposalDoNotInitializeCameraSdk()
    {
        await using var session = new StandaloneCameraSession();
        Assert.False(session.IsConnected);
        Assert.Null(session.TakeLatestFrame());
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.CaptureAsync());
    }
}
