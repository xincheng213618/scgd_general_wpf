using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed partial class DisplayMetrologyTests
{
    private static async Task<AlgorithmResult> RunCrossWithRoi(AlgorithmImageBuffer image, AlgorithmRoi roi)
        => await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.RgbCrossRegistration, new RgbCrossRegistrationParameters(), roi),
            Inputs = [new AlgorithmInput { Name = "source", Image = image, Ownership = AlgorithmInputOwnership.Borrowed, ColorSpace = "linear-device-values" }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });

    [Fact]
    public async Task RgbCrossSearchRectangleIgnoresExternalTargetsAndKeepsGlobalCoordinates()
    {
        using var image = Image(800, 600, (x, y, c) => Math.Max(ArrayCross(x, y, c, 300, 180), ArrayCross(x, y, c, 50, 410)), AlgorithmImageFormat.Bgr48);
        using var result = await RunCrossWithRoi(image, new RectangleAlgorithmRoi(250, 130, 230, 230));
        Success(result);
        Assert.Equal(9, Metric(result, "valid_crosses"));
        Assert.Equal(230 * 230, Metric(result, "searched_pixels"));
        var rows = result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation")!.Rows;
        for (int i = 0; i < 9; i++)
        {
            Assert.Equal(300 + i % 3 * 64, rows[i]["gVerticalAxisX_px"].GetDouble(), 6);
            Assert.Equal(180 + i / 3 * 64, rows[i]["gHorizontalAxisY_px"].GetDouble(), 6);
        }
        Assert.Equal("250", result.Artifacts.OfType<AlgorithmImageArtifact>().First().Metadata!["sourceOriginX"]);
        Assert.Equal("130", result.Artifacts.OfType<AlgorithmImageArtifact>().First().Metadata!["sourceOriginY"]);
    }

    [Fact]
    public async Task RgbCrossSearchRectangleRejectsClippedTargets()
    {
        using var image = Image(800, 600, (x, y, c) => ArrayCross(x, y, c, 300, 180), AlgorithmImageFormat.Bgr48);
        using var result = await RunCrossWithRoi(image, new RectangleAlgorithmRoi(290, 130, 200, 230));
        Success(result);
        Assert.Contains(result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation")!.Rows,
            row => row["reason"].GetString()!.Contains("cross_clipped"));
    }

    [Theory]
    [InlineData(-1, 10, 200, 200, "roi_out_of_bounds")]
    [InlineData(700, 10, 200, 200, "roi_out_of_bounds")]
    [InlineData(10, 10, 20, 20, "roi_too_small")]
    public async Task RgbCrossRejectsInvalidSearchBounds(double x, double y, double width, double height, string code)
    {
        using var image = Image(800, 600, (x, y, c) => ArrayCross(x, y, c, 300, 180), AlgorithmImageFormat.Bgr48);
        using var result = await RunCrossWithRoi(image, new RectangleAlgorithmRoi(x, y, width, height));
        Failure(result, code);
    }

    [Fact]
    public void RgbCrossEntriesBelongToAlgorithmCallsAndAdvertiseRoi()
    {
        Assert.True(ImageAlgorithmPlatform.Catalog.TryResolve(DisplayMetrologyIds.RgbCrossRegistration, out var descriptor));
        Assert.True(descriptor!.Capabilities.HasFlag(AlgorithmHostCapabilities.Roi));
        Assert.Equal(2, descriptor.Presentation!.InteractiveEntries!.Count);
        Assert.All(descriptor.Presentation.InteractiveEntries, entry => Assert.Equal("AlgorithmsCall", entry.Group!.Id));
    }

    [Theory]
    [InlineData(70, 60)]
    [InlineData(300, 180)]
    public async Task RgbCrossLocatesCompactTranslatedArray(int originX, int originY)
    {
        using var image = Image(800, 600, (x, y, c) => ArrayCross(x, y, c, originX, originY), AlgorithmImageFormat.Bgr48);
        using var result = await Run(DisplayMetrologyIds.RgbCrossRegistration, new RgbCrossRegistrationParameters(), image);
        Success(result);
        var rows = result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation")!.Rows;
        Assert.Equal(9, Metric(result, "valid_crosses"));
        for (int i = 0; i < 9; i++)
        {
            Assert.Equal("MEASURED", rows[i]["result"].GetString());
            Assert.Equal(originX + i % 3 * 64, rows[i]["gVerticalAxisX_px"].GetDouble(), 6);
            Assert.Equal(originY + i / 3 * 64, rows[i]["gHorizontalAxisY_px"].GetDouble(), 6);
            Assert.Equal(0, rows[i]["maximumEdgeSeparation_px"].GetDouble(), 6);
            Assert.Equal(JsonValueKind.Null, rows[i]["limit_px"].ValueKind);
        }
    }

    [Theory]
    [InlineData("missing", "cross_missing")]
    [InlineData("channel", "low_contrast")]
    [InlineData("duplicate", "duplicate_or_extra_candidates")]
    [InlineData("ambiguous", "array_order_ambiguous")]
    [InlineData("clipped", "cross_clipped")]
    [InlineData("flat", "low_contrast")]
    public async Task RgbCrossKeepsFailureReasonsAndNullMeasurements(string defect, string reason)
    {
        using var image = Image(400, 340, (x, y, c) =>
        {
            if (defect == "flat") return 0.01;
            if (defect == "missing" && x < 95 && y < 95) return 0.01;
            if (defect == "channel" && c == 0 && x < 95 && y < 95) return 0.01;
            if (defect == "duplicate" && Math.Abs(x - 320) <= 16 && Math.Abs(y - 60) <= 16)
                return Math.Abs(x - 320) <= 1 || Math.Abs(y - 60) <= 1 ? 0.8 : 0.01;
            if (defect == "ambiguous" && x >= 95 && x < 155)
                return ArrayCross(x, y - 34, c, 60, 60);
            return ArrayCross(x, y, c, defect == "clipped" ? 8 : 60, 60);
        }, AlgorithmImageFormat.Bgr48);
        using var result = await Run(DisplayMetrologyIds.RgbCrossRegistration, new RgbCrossRegistrationParameters(), image);
        Success(result);
        var rows = result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation")!.Rows;
        Assert.Contains(rows, r => !r["valid"].GetBoolean() && r["reason"].GetString()!.Contains(reason));
        foreach (var row in rows.Where(r => !r["valid"].GetBoolean()))
        {
            Assert.Equal("INVALID", row["result"].GetString());
            Assert.Equal(JsonValueKind.Null, row["maximumEdgeSeparation_px"].ValueKind);
        }
        if (defect is "missing" or "channel") Assert.Equal(8, Metric(result, "valid_crosses"));
    }

    [Fact]
    public async Task RgbCrossMeasuresDimVerticalArmIndependentlyFromBrightHorizontalArm()
    {
        using var image = Image(400, 340, (x, y, c) =>
        {
            double value = ArrayCross(x, y, c, 60, 60);
            bool horizontal = Enumerable.Range(0, 3).Any(r => Math.Abs(y - 60 - r * 64) <= 1);
            return value > 0.1 && c == 0 && !horizontal ? 0.12 : value;
        }, AlgorithmImageFormat.Bgr48);
        using var result = await Run(DisplayMetrologyIds.RgbCrossRegistration, new RgbCrossRegistrationParameters(), image);
        Success(result);
        Assert.Equal(9, Metric(result, "valid_crosses"));
        Assert.InRange(Metric(result, "maximum_cross_edge_separation"), 0, 0.01);
    }

    [Fact]
    public async Task RgbCrossPoolingOnlyLocatesAndKeepsSourceResolutionOffsets()
    {
        using var image = Image(1800, 400, (x, y, c) => ArrayCross(x - (c == 2 ? 2 : c == 0 ? -3 : 0), y, c, 777, 95), AlgorithmImageFormat.Bgr48);
        using var result = await Run(DisplayMetrologyIds.RgbCrossRegistration, new RgbCrossRegistrationParameters(), image);
        Success(result);
        Assert.Equal(9, Metric(result, "valid_crosses"));
        foreach (var row in result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation")!.Rows)
        {
            Assert.Equal(2, row["rMinusG_dx_px"].GetDouble(), 6);
            Assert.Equal(-3, row["bMinusG_dx_px"].GetDouble(), 6);
            Assert.Equal(5, row["maximumEdgeSeparation_px"].GetDouble(), 6);
        }
        Assert.Equal("2", result.Artifacts.OfType<AlgorithmImageArtifact>().First().Metadata!["sourcePixelsPerPreviewPixel"]);
    }

    [Fact]
    public void RgbCrossStoredNumericThresholdRemainsCompatibleAndDefaultDoesNotInventALimit()
    {
        var defaults = new RgbCrossRegistrationParameters();
        var stored = JsonSerializer.Deserialize<RgbCrossRegistrationParameters>("{\"MaximumEdgeSeparationPixels\":2.5}", AlgorithmJson.Options)!;
        Assert.Null(defaults.MaximumEdgeSeparationPixels);
        Assert.Equal(2.5, stored.MaximumEdgeSeparationPixels);
        Assert.True(defaults.Validate().IsValid);
        Assert.True(stored.Validate().IsValid);
        stored.MaximumEdgeSeparationPixels = double.NaN;
        Assert.False(stored.Validate().IsValid);
    }

    private static double ArrayCross(int x, int y, int channel, int originX, int originY)
    {
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                int u = x - originX - c * 64, v = y - originY - r * 64;
                if (Math.Abs(u) <= 1 && Math.Abs(v) <= 16 || Math.Abs(v) <= 1 && Math.Abs(u) <= 16) return 0.8;
            }
        return 0.01;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RgbCrossRejectsAmbiguousProfilesAndRequiresCoverageOnEveryHalfArm(bool entireSampleBand)
    {
        using var image = Image(400, 340, (x, y, c) =>
        {
            if (c == 0 && x == 66 && (entireSampleBand ? y >= 47 && y <= 51 : y == 48)) return 0.8;
            return ArrayCross(x, y, c, 60, 60);
        }, AlgorithmImageFormat.Bgr48);
        using var result = await Run(DisplayMetrologyIds.RgbCrossRegistration, new RgbCrossRegistrationParameters(), image);
        Success(result);
        var first = result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation")!.Rows[0];
        Assert.Equal(!entireSampleBand, first["valid"].GetBoolean());
        Assert.True(first["bRejectedProfiles"].GetInt32() > 0);
        if (entireSampleBand)
        {
            Assert.Contains("ambiguous_arm_edges", first["reason"].GetString());
            Assert.Equal(JsonValueKind.Null, first["maximumEdgeSeparation_px"].ValueKind);
        }
        else
        {
            Assert.Equal(0, first["maximumEdgeSeparation_px"].GetDouble(), 6);
            Assert.InRange(first["bVerticalCoverage"].GetDouble(), 0.5, 0.99);
        }
    }
    [Fact]
    public async Task RgbCrossWindowCloseRetainsOverlayUntilUserClear()
    {
        using var input = Image(400,340,(x,y,c) => ArrayCross(x,y,c,60,60),AlgorithmImageFormat.Bgr48);
        var result = await Run(DisplayMetrologyIds.RgbCrossRegistration,new RgbCrossRegistrationParameters(),input);
        Success(result);
        WpfTestHost.Invoke(() =>
        {
            EnsureResources();
            using var view = new ColorVision.ImageEditor.ImageView(ImageAlgorithmPlatform.Runtime);
            view.SetImageSource(System.Windows.Media.Imaging.BitmapSource.Create(400,340,96,96,System.Windows.Media.PixelFormats.Gray8,null,new byte[400*340],400),enableEditorImageServices:false,configureDefaultLayerController:false);
            var context = view.EditorContext.ProcessingContext;
            using var window = new ColorVision.ImageEditor.EditorTools.Algorithms.DisplayMetrologyResultWindow(result,"九点关闭保留",context,view.EditorContext.DrawEditorContext);
            window.Show(); window.Close();
            var registration = Assert.Single(context.SnapshotAlgorithmOverlayRegistrations());
            Assert.Equal(AlgorithmOverlayLifetime.Persistent,registration.Lifetime);
            Assert.True(view.ImageShow.ContainsVisual(registration.Visual));
            view.Clear();Assert.Empty(context.SnapshotAlgorithmOverlayRegistrations());Assert.Empty(context.AlgorithmOverlays.Snapshot());
        });
    }

}
