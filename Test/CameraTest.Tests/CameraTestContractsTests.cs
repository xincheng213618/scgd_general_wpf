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
    public void OldProfilesDefaultToReadableMtfLabelsAndVideoSettingsRoundTrip()
    {
        var old = JsonSerializer.Deserialize<TestProfile>("{\"SchemaVersion\":1}", ProfileStore.JsonOptions)!;
        old.Validate();
        Assert.Equal(SfrChartType.Bmw, old.MeasurementRoi.ChartType);
        Assert.True(old.Display.ShowValues);
        Assert.True(old.Display.FixedScreenSize);
        old.Display.FontSize = 18;
        old.Display.Metric = ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR.BmwSfrDisplayMetric.AtFrequency;
        old.Display.Frequency = .3;
        old.Display.ShowTargetCenter = false;
        old.Display.ShowCenterCoordinates = old.Display.ShowRoiDimensions = old.Display.ShowCenterDistance = true;
        old.MeasurementRoi = new() { ChartType = SfrChartType.Checkerboard, AlongEdgePixels = 80, AcrossEdgePixels = 60, CenterDistancePixels = 100 };
        old.Video = new() { Mode = VideoAnalysisMode.Sharpness, Algorithm = FocusAlgorithm.Tenengrad, X = 20, Y = 30, Width = 80, Height = 90 };
        var restored = JsonSerializer.Deserialize<TestProfile>(JsonSerializer.Serialize(old, ProfileStore.JsonOptions), ProfileStore.JsonOptions)!;
        Assert.Equal(18, restored.Display.FontSize);
        Assert.Equal(.3, restored.Display.Frequency);
        Assert.Equal(old.Display.Metric, restored.Display.Metric);
        Assert.False(restored.Display.ShowTargetCenter);
        Assert.True(restored.Display.ShowCenterCoordinates && restored.Display.ShowRoiDimensions && restored.Display.ShowCenterDistance);
        Assert.Equal(old.MeasurementRoi, restored.MeasurementRoi);
        Assert.Equal(FocusAlgorithm.Tenengrad, restored.Video.Algorithm);
        Assert.Equal(new RoiRect(20, 30, 80, 90), restored.Video.ResolveRoi(200, 200));
        Assert.Throws<ArgumentException>(() => restored.Video.ResolveRoi(90, 100));
        restored.Display.FontSize = double.NaN;
        Assert.Throws<ArgumentException>(restored.Validate);
    }

    [Fact]
    public void RoiGeometryPreservesAutomaticCoordinatesAndAppliesDimensionsAlongEachEdge()
    {
        var auto = new RoiRect(20, 80, 40, 40);
        Assert.Equal(auto, new BmwSfrRoiSettings().Resolve(BmwEdgeId.Left, auto, 100, 100));
        var settings = new BmwSfrRoiSettings { AlongEdgePixels = 80, AcrossEdgePixels = 60, CenterDistancePixels = 60 };
        Assert.Equal(new RoiRect(0, 70, 80, 60), settings.Resolve(BmwEdgeId.Left, auto, 100, 100));
        Assert.Equal(new RoiRect(70, 0, 60, 80), settings.Resolve(BmwEdgeId.Top, new(80, 20, 40, 40), 100, 100));
        settings.CenterDistancePixels = 100;
        var outside = settings.Resolve(BmwEdgeId.Left, auto, 100, 100);
        Assert.Equal(-40, outside.X); // Preserve the requested geometry; never silently clip a measurement ROI.
        Assert.False(BmwSfrRoiSettings.IsInside(outside, new(0, 0, 200, 200)));
        Assert.Throws<ArgumentException>(() => (settings with { AlongEdgePixels = 39 }).Validate());
        Assert.Throws<ArgumentException>(() => (settings with { CenterDistancePixels = double.NaN }).Validate());
        var display = new ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR.BmwSfrOverlaySettings { Frequency = .51 };
        Assert.Throws<ArgumentException>(display.Validate);
    }

    [Fact]
    public async Task AcquisitionChangesValidateWithoutInitializingSdk()
    {
        await using var camera = new StandaloneCameraSession();
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => camera.SetAcquisitionParameterAsync(true, 0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => camera.SetAcquisitionParameterAsync(false, 2));
        Assert.False(camera.IsConnected);
        Assert.Null(camera.RequestedSettings);
    }

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
