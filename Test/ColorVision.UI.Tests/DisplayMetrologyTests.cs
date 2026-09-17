using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using System.Buffers.Binary;
using System.Text.Json;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using ColorVision.ImageEditor;

namespace ColorVision.UI.Tests;

public sealed partial class DisplayMetrologyTests
{
    [Fact]
    public void CatalogIsExecutableAnalysisOnlyAndDoesNotJoinImageTransformBatch()
    {
        foreach (var id in DisplayMetrologyIds.All)
        {
            Assert.True(ImageAlgorithmPlatform.Catalog.TryResolve(id, out var descriptor));
            Assert.True(ImageAlgorithmPlatform.Runtime.CanExecuteDescriptor(descriptor!, AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Interactive));
            Assert.Equal(AlgorithmResultSemantics.Analysis, descriptor!.ResultSemantics);
            Assert.Null(descriptor.Presentation!.BatchImageProcessingOrder);
            var defaults = (IAlgorithmParameters)descriptor.ParameterSchema.Defaults.Deserialize(descriptor.ParameterType, AlgorithmJson.Options)!;
            Assert.True(defaults.Validate().IsValid);
            Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Copilot));
        }
    }

    [Theory]
    [InlineData(AlgorithmImageFormat.Bgr24)]
    [InlineData(AlgorithmImageFormat.Bgr48)]
    [InlineData(AlgorithmImageFormat.Bgr96Float)]
    [InlineData(AlgorithmImageFormat.Bgra32)]
    public async Task RgbMeasuresChannelDisplacementsInOriginalPixelCoordinates(AlgorithmImageFormat format)
    {
        using var image = Image(192, 192, (x, y, c) => Dot(x, y, c == 2 ? 2 : c == 0 ? -3 : 0, c == 2 ? -1 : c == 0 ? 1 : 0), format);
        byte[] before = image.Data.ToArray();
        using var result = await Run(DisplayMetrologyIds.RgbRegistration, new RgbRegistrationParameters(), image);
        Success(result);
        var table = result.GetArtifact<AlgorithmTableArtifact>("RGB-displacement")!;
        Assert.Equal(18, table.Rows.Count);
        foreach (var row in table.Rows)
        {
            bool red = row["pair"].GetString() == "R-G";
            Assert.True(row["valid"].GetBoolean());
            Assert.InRange(row["dx_px"].GetDouble(), (red ? 2 : -3) - 0.04, (red ? 2 : -3) + 0.04);
            Assert.InRange(row["dy_px"].GetDouble(), (red ? -1 : 1) - 0.04, (red ? -1 : 1) + 0.04);
        }
        Assert.Equal(before, image.Data.ToArray());
        Assert.Empty(result.Artifacts.OfType<AlgorithmImageArtifact>().Where(a => a.Role == "primary"));
    }

    [Fact]
    public async Task MultipleTargetsAndFlatFieldsAreRejectedForRegistration()
    {
        using var ambiguous = Image(192, 192, (x, y, _) => Math.Max(Dot(x, y, -10, 0), Dot(x, y, 10, 0)), AlgorithmImageFormat.Bgr24);
        using var flat = Image(192, 192, (_, _, _) => 0.3, AlgorithmImageFormat.Bgr24);
        using var first = await Run(DisplayMetrologyIds.RgbRegistration, new RgbRegistrationParameters(), ambiguous);
        using var second = await Run(DisplayMetrologyIds.RgbRegistration, new RgbRegistrationParameters(), flat);
        Failure(first, "no_valid_targets"); Failure(second, "no_valid_targets");
    }

    [Fact]
    public async Task NinePointRgbCrossReportsChannelAxesEdgesAndThresholdResult()
    {
        using var image = Image(192, 192, (x, y, channel) => Cross(x, y,
            channel == 2 ? 2 : channel == 0 ? -3 : 0,
            channel == 2 ? -1 : channel == 0 ? 2 : 0), AlgorithmImageFormat.Bgr96Float);
        using var result = await Run(DisplayMetrologyIds.RgbCrossRegistration,
            new RgbCrossRegistrationParameters { MaximumEdgeSeparationPixels = 4 }, image);

        Success(result);
        var table = result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation")!;
        Assert.Equal(9, table.Rows.Count);
        foreach (var row in table.Rows)
        {
            Assert.True(row["valid"].GetBoolean());
            Assert.Equal("NG", row["result"].GetString());
            Assert.InRange(row["rMinusG_dx_px"].GetDouble(), 1.99, 2.01);
            Assert.InRange(row["rMinusG_dy_px"].GetDouble(), -1.01, -0.99);
            Assert.InRange(row["bMinusG_dx_px"].GetDouble(), -3.01, -2.99);
            Assert.InRange(row["bMinusG_dy_px"].GetDouble(), 1.99, 2.01);
            Assert.Equal(5, row["maximumEdgeSeparation_px"].GetDouble(), 6);
        }
        Assert.Equal(5, Metric(result, "maximum_cross_axis_separation"), 6);
        Assert.Equal(5, Metric(result, "maximum_cross_edge_separation"), 6);
        Assert.Equal(0, Metric(result, "overall_threshold_result"));
        Assert.Equal(new[] { "R", "G", "B" }, result.Artifacts.OfType<AlgorithmImageArtifact>()
            .Select(artifact => artifact.Metadata!["channel"]).ToArray());
    }

    [Fact]
    public async Task CrossArmSeparationIsDetectedWhenOuterCrossBoundsRemainEqual()
    {
        using var image = Image(192, 192, (x, y, channel) => SplitHorizontalArmCross(x, y,
            channel == 2 ? -2 : channel == 0 ? 3 : 0), AlgorithmImageFormat.Bgr24);
        using var result = await Run(DisplayMetrologyIds.RgbCrossRegistration,
            new RgbCrossRegistrationParameters { MaximumEdgeSeparationPixels = 5 }, image);

        Success(result);
        foreach (var row in result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation")!.Rows)
        {
            Assert.Equal("OK", row["result"].GetString());
            Assert.Equal(0, row["verticalAxisXSpread_px"].GetDouble(), 6);
            Assert.Equal(5, row["horizontalAxisYSpread_px"].GetDouble(), 6);
            Assert.Equal(5, row["maximumEdgeSeparation_px"].GetDouble(), 6);
        }
        Assert.Equal(1, Metric(result, "overall_threshold_result"));
    }

    [Fact]
    public async Task BinocularPreservesMeasuredTranslationAndReportsSignalRatio()
    {
        using var left = Image(192, 192, (x, y, _) => Dot(x, y, 0, 0));
        using var right = Image(192, 192, (x, y, _) => Dot(x, y, 3, -2) * 0.8);
        using var result = await Run(DisplayMetrologyIds.Binocular, new BinocularQualityParameters(), left, right);
        Success(result);
        Assert.InRange(Metric(result, "mean_horizontal_disparity"), 2.99, 3.01);
        Assert.InRange(Metric(result, "mean_vertical_disparity"), -2.01, -1.99);
        Assert.InRange(Metric(result, "right_over_left_scale"), 0.999, 1.001);
        Assert.InRange(Metric(result, "mean_right_over_left_signal"), 0.799, 0.801);
        Assert.InRange(Metric(result, "similarity_residual_rms"), 0, 0.01);
    }

    [Fact]
    public async Task BinocularMeasuresRotationAndScaleFromIndependentTransformedCenters()
    {
        const double angle = 1.5 * Math.PI / 180, scale = 1.02;
        using var left = Image(192, 192, (x, y, _) => Dot(x, y, 0, 0));
        using var right = Image(192, 192, (x, y, _) =>
        {
            double value = 0.02;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    double a = c * 64 + 31.5 - 95.5, b = r * 64 + 31.5 - 95.5;
                    double cx = scale * (a * Math.Cos(angle) - b * Math.Sin(angle)) + 95.5 + 1;
                    double cy = scale * (a * Math.Sin(angle) + b * Math.Cos(angle)) + 95.5 - 2;
                    value += 0.7 * Math.Exp(-((x - cx) * (x - cx) + (y - cy) * (y - cy)) / 32);
                }
            return value;
        });
        using var result = await Run(DisplayMetrologyIds.Binocular, new BinocularQualityParameters(), left, right);
        Success(result);
        Assert.InRange(Metric(result, "right_rotation_clockwise"), 1.4, 1.6);
        Assert.InRange(Metric(result, "right_over_left_scale"), 1.018, 1.022);
    }

    [Fact]
    public async Task UniformBinocularFieldsRequireDisablingAlignment()
    {
        using var left = Image(96, 96, (_, _, _) => 0.4);
        using var right = Image(96, 96, (_, _, _) => 0.3);
        using var aligned = await Run(DisplayMetrologyIds.Binocular, new BinocularQualityParameters(), left, right);
        Failure(aligned, "insufficient_binocular_targets");
        using var signal = await Run(DisplayMetrologyIds.Binocular, new BinocularQualityParameters { MeasureAlignment = false }, left, right);
        Success(signal); Assert.InRange(Metric(signal, "mean_right_over_left_signal"), 0.7499, 0.7501);
    }

    [Fact]
    public async Task GhostReportsBackgroundSubtractedPeakAndEnergyRatios()
    {
        using var image = Image(128, 128, (x, y, _) => 0.02 + (x >= 55 && x < 65 && y >= 55 && y < 65 ? 0.8 : 0)
            + (x >= 95 && x < 105 && y >= 55 && y < 65 ? 0.08 : 0));
        using var result = await Run(DisplayMetrologyIds.Ghost, new GhostMeasurementParameters { Background = 0.02 }, image);
        Success(result);
        var candidate = Assert.Single(result.GetArtifact<AlgorithmTableArtifact>("stray-light-candidates")!.Rows);
        Assert.InRange(candidate["peakOverPrimaryPeak"].GetDouble(), 0.0999, 0.1001);
        Assert.InRange(candidate["energyOverPrimaryRegion"].GetDouble(), 0.0999, 0.1001);
        Assert.InRange(Metric(result, "outside_integral_over_primary_region"), 0.0999, 0.1001);
    }

    [Fact]
    public async Task SaturatedOrMissingGhostPrimaryNeverProducesAValidRatio()
    {
        using var saturated = Image(96, 96, (_, _, _) => 1);
        using var empty = Image(96, 96, (_, _, _) => 0);
        using var a = await Run(DisplayMetrologyIds.Ghost, new GhostMeasurementParameters(), saturated);
        using var b = await Run(DisplayMetrologyIds.Ghost, new GhostMeasurementParameters(), empty);
        Failure(a, "saturated_primary"); Failure(b, "primary_missing");
    }

    [Fact]
    public async Task FlatAndLinearGradientDoNotBecomePointOrMuraDefects()
    {
        using var image = Image(256, 256, (x, _, _) => 0.15 + x * 0.0001);
        using var result = await Run(DisplayMetrologyIds.Defects, new DisplayDefectParameters(), image);
        Success(result);
        Assert.Empty(result.GetArtifact<AlgorithmTableArtifact>("defect-candidates")!.Rows);
    }

    [Fact]
    public async Task DetectsBrightDarkPointsLinesAndLowGrayMuraAtKnownLocations()
    {
        using var image = Image(256, 256, (x, y, _) =>
        {
            double value = 0.08 - 0.025 * Math.Exp(-((x - 175.0) * (x - 175) + (y - 175.0) * (y - 175)) / 200);
            if (x == 65 && y == 65) value += 0.2;
            if (x == 90 && y == 65) value -= 0.06;
            if (x == 110 && y >= 80 && y < 145) value += 0.15;
            return value;
        });
        using var result = await Run(DisplayMetrologyIds.Defects, new DisplayDefectParameters(), image);
        Success(result);
        var rows = result.GetArtifact<AlgorithmTableArtifact>("defect-candidates")!.Rows;
        Assert.Contains(rows, r => r["kind"].GetString() == "bright_point_candidate" && r["x_px"].GetInt32() == 65);
        Assert.Contains(rows, r => r["kind"].GetString() == "dark_point_candidate" && r["x_px"].GetInt32() == 90);
        Assert.Contains(rows, r => r["kind"].GetString() == "bright_line_candidate" && r["height_px"].GetInt32() >= 60);
        Assert.Contains(rows, r => r["kind"].GetString() == "dark_mura_candidate" && r["x_px"].GetInt32() > 130);
    }

    [Fact]
    public async Task EyeboxHoleDoesNotBecomeFilledBoundingBoxArea()
    {
        var images = Enumerable.Range(0, 9).Select(i => Image(48, 48, (_, _, _) => i == 4 ? 0.1 : 0.8)).ToArray();
        try
        {
            using var result = await Run(DisplayMetrologyIds.Eyebox, new EyeboxScanParameters { ReferenceIndex = 0 }, images);
            Success(result);
            Assert.Equal(8, Metric(result, "accepted_sample_count"));
            Assert.Equal(0, Metric(result, "four_corner_accepted_mesh_area"));
            Assert.Equal(2, Metric(result, "sampled_span_x"));
        }
        finally { foreach (var image in images) image.Dispose(); }
    }

    [Fact]
    public async Task EyeboxFullGridUsesIntervalsNotPointCountForArea()
    {
        var images = Enumerable.Range(0, 9).Select(i => Image(32, 32, (_, _, _) => 0.5)).ToArray();
        try
        {
            using var result = await Run(DisplayMetrologyIds.Eyebox, new EyeboxScanParameters { StepXMillimeters = 2, StepYMillimeters = 3 }, images);
            Success(result); Assert.Equal(24, Metric(result, "four_corner_accepted_mesh_area"));
        }
        finally { foreach (var image in images) image.Dispose(); }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task SfrMatchesAnalyticalGaussianMtfAndRetainsPerFieldCurves(bool horizontal, bool inverse)
    {
        using var image = Image(288, 288, (x, y, _) =>
        {
            int cell = x / 96;
            double sigma = 1 + cell * 0.5;
            double u = horizontal ? y % 96 : x % 96, v = horizontal ? x % 96 : y % 96;
            double edge = (u - 47.5 - 0.1 * (v - 47.5)) / Math.Sqrt(1.01);
            double value = 0.2 + 0.6 * 0.5 * (1 + Erf(edge / (Math.Sqrt(2) * sigma)));
            return inverse ? 1 - value : value;
        });
        using var result = await Run(DisplayMetrologyIds.FieldSfr, new FieldSfrParameters { HorizontalEdge = horizontal }, image);
        Success(result);
        foreach (var row in result.GetArtifact<AlgorithmTableArtifact>("field-sfr")!.Rows)
        {
            Assert.True(row["valid"].GetBoolean(), row["reason"].GetString());
            double sigma = 1 + row["cell"].GetInt32() % 3 * 0.5;
            double expected = Math.Sqrt(2 * Math.Log(2)) / (2 * Math.PI * sigma);
            // Sampling/binning/windowing have a finite bias; this is an analytical accuracy contract, not a runtime threshold.
            Assert.InRange(row["mtf50_cyclesPerPixel"].GetDouble(), expected - 0.015, expected + 0.015);
        }
        Assert.Equal(9 * 65, result.GetArtifact<AlgorithmTableArtifact>("sfr-curves")!.Rows.Count);
    }

    [Fact]
    public async Task SfrRejectsTexturelessFramesInsteadOfReportingZeroSharpness()
    {
        using var image = Image(192, 192, (_, _, _) => 0.5);
        using var result = await Run(DisplayMetrologyIds.FieldSfr, new FieldSfrParameters(), image);
        Failure(result, "no_valid_slanted_edges");
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(-0.1f)]
    [InlineData(1.1f)]
    public async Task InvalidFloatSamplesFailWithoutPartialArtifacts(float value)
    {
        using var image = Image(96, 96, (_, _, _) => 0.2);
        byte[] data = image.Data.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(data, BitConverter.SingleToInt32Bits(value));
        using var invalid = new AlgorithmImageBuffer(96, 96, 384, AlgorithmImageFormat.Gray32Float, data);
        using var result = await Run(DisplayMetrologyIds.Defects, new DisplayDefectParameters(), invalid);
        Failure(result, "invalid_signal"); Assert.Empty(result.Artifacts);
    }

    [Fact]
    public async Task TransferredInputsAreDisposedOnCancellation()
    {
        var image = Image(192, 192, (_, _, _) => 0.5);
        using var cts = new CancellationTokenSource(); cts.Cancel();
        using var result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.Defects, new DisplayDefectParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = image, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Headless,
        }, cts.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, result.Status);
        Assert.True(image.IsDisposed);
    }

    [Fact]
    public void ManifestRejectsDuplicateFrames()
    {
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(new { schemaVersion = 1, parameters = new EyeboxScanParameters(), frames = Enumerable.Repeat("same.png", 9) }, AlgorithmJson.Options));
            Assert.Throws<InvalidDataException>(() => DisplayMetrologyEditorTool.ReadManifest(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task MissingEyeboxFrameFailsAndBlankReferenceCannotDefineAnEyebox()
    {
        var images = Enumerable.Range(0, 8).Select(_ => Image(32, 32, (_, _, _) => 0.5)).ToArray();
        try
        {
            using var missing = await Run(DisplayMetrologyIds.Eyebox, new EyeboxScanParameters(), images);
            Failure(missing, "scan_count_mismatch");
            using var black = Image(32, 32, (_, _, _) => 0);
            using var blank = await Run(DisplayMetrologyIds.Eyebox, new EyeboxScanParameters { ReferenceIndex = 8 }, images.Concat([black]).ToArray());
            Failure(blank, "invalid_scan_reference");
        }
        finally { foreach (var image in images) image.Dispose(); }
    }

    [Fact]
    public async Task FrameBudgetRejectsBeforeAllocatingWorkingImages()
    {
        using var image = new AlgorithmImageBuffer(4096, 2049, 4096, AlgorithmImageFormat.Gray8, new byte[4096 * 2049]);
        using var result = await Run(DisplayMetrologyIds.Defects, new DisplayDefectParameters(), image);
        Failure(result, "image_budget_exceeded");
    }

    [Fact]
    public async Task MaximumSupportedFrameCompletesWithoutInventedDefects()
    {
        using var image = new AlgorithmImageBuffer(4096, 2048, 4096, AlgorithmImageFormat.Gray8, Enumerable.Repeat((byte)80, 4096 * 2048).ToArray());
        using var result = await Run(DisplayMetrologyIds.Defects, new DisplayDefectParameters(), image);
        Success(result);
        Assert.Empty(result.GetArtifact<AlgorithmTableArtifact>("defect-candidates")!.Rows);
        Assert.Equal(4096 * 2048, Assert.Single(result.Artifacts.OfType<AlgorithmImageArtifact>()).Image.Data.Length);
    }

    [Fact]
    public async Task ColorConsistencyUsesSeparateBgrChannelsWithoutInventingChromaticity()
    {
        using var left = Image(96, 96, (_, _, _) => 0.4, AlgorithmImageFormat.Bgr96Float);
        using var right = Image(96, 96, (_, _, c) => c == 2 ? 0.2 : 0.4, AlgorithmImageFormat.Bgr96Float);
        using var result = await Run(DisplayMetrologyIds.Binocular, new BinocularQualityParameters { MeasureAlignment = false }, left, right);
        Success(result);
        foreach (var row in result.GetArtifact<AlgorithmTableArtifact>("binocular-channel-consistency")!.Rows)
            Assert.Equal(row["channel"].GetString() == "R" ? 0.5 : 1, row["rightOverLeft"].GetDouble(), 5);
    }

    [Fact]
    public async Task PngImportPreserves16BitSignalAndResultWindowReleasesOwnedImages()
    {
        string file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        try
        {
            WpfTestHost.Invoke(() =>
            {
                ushort[] pixels = Enumerable.Repeat((ushort)10000, 128 * 128).ToArray();
                var bitmap = BitmapSource.Create(128, 128, 96, 96, PixelFormats.Gray16, null, pixels, 256);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(file); encoder.Save(stream);
            });
            using var input = DisplayMetrologyEditorTool.LoadBounded(file, 128 * 128);
            Assert.Equal(AlgorithmImageFormat.Gray16, input.Format);
            Assert.Equal(10000, BinaryPrimitives.ReadUInt16LittleEndian(input.Data.Span));
            AlgorithmResult result = await Run(DisplayMetrologyIds.Defects, new DisplayDefectParameters(), input);
            Success(result);
            var output = Assert.Single(result.Artifacts.OfType<AlgorithmImageArtifact>()).Image;
            WpfTestHost.Invoke(() =>
            {
                EnsureResources();
                using var view = new ImageView(ImageAlgorithmPlatform.Runtime);
                var menu = new AlgorithmsContextMenu(view.EditorContext.ProcessingContext);
                var entries = menu.GetContextMenuItems();
                foreach (var id in DisplayMetrologyIds.All) Assert.Contains(entries, e => e.GuidId == id.Value);
                using var window = new DisplayMetrologyResultWindow(result, "显示计量结果测试", view.EditorContext.ProcessingContext, null);
                window.Show();
                Assert.NotNull(window.Content);
                var root = Assert.IsType<DockPanel>(window.Content);
                var tabs = Assert.Single(root.Children.OfType<TabControl>());
                Assert.Equal("测量汇总", Assert.IsType<TabItem>(tabs.Items[0]).Header);
                Assert.IsType<DataGrid>(Assert.IsType<TabItem>(tabs.Items[0]).Content);
                string? outputDirectory = Environment.GetEnvironmentVariable("COLORVISION_DISPLAY_QA_DIRECTORY");
                if (!string.IsNullOrEmpty(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                    window.UpdateLayout();
                    var capture = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    capture.Render(root);
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(capture));
                    using var outputFile = File.Create(Path.Combine(outputDirectory, "display-metrology-result.png")); encoder.Save(outputFile);
                }
                window.Close();
            });
            Assert.True(result.IsDisposed); Assert.True(output.IsDisposed);
        }
        finally { File.Delete(file); }
    }

    private static void EnsureResources()
    {
        var application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(System.Windows.Controls.Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private static async Task<AlgorithmResult> Run(AlgorithmId id, IAlgorithmParameters parameters, params AlgorithmImageBuffer[] images)
        => await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(id, parameters),
            Inputs = images.Select((image, index) => new AlgorithmInput
            {
                Name = id == DisplayMetrologyIds.Binocular ? index == 0 ? "left" : "right" : id == DisplayMetrologyIds.Eyebox ? $"sample-{index}" : "source",
                Image = image, Ownership = AlgorithmInputOwnership.Borrowed, ColorSpace = "linear-device-values",
            }).ToArray(),
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | (images.Length > 1 ? AlgorithmHostCapabilities.MultiInput : 0),
        });

    private static void Success(AlgorithmResult result) => Assert.True(result.Status == AlgorithmResultStatus.Succeeded, string.Join(";", result.Failures));
    private static void Failure(AlgorithmResult result, string code)
    { Assert.Equal(AlgorithmResultStatus.Failed, result.Status); Assert.Contains(result.Failures, f => f.Code == code); }
    private static double Metric(AlgorithmResult result, string name) => result.Artifacts.OfType<AlgorithmMeasurementArtifact>().SelectMany(a => a.Measurements).Single(m => m.Name == name).Value;

    private static double Dot(double x, double y, double dx, double dy)
    {
        double u = x % 64 - 31.5 - dx, v = y % 64 - 31.5 - dy;
        return 0.02 + 0.7 * Math.Exp(-(u * u + v * v) / 32);
    }

    private static double Cross(double x, double y, double dx, double dy)
    {
        double u = x % 64 - 31.5 - dx;
        double v = y % 64 - 31.5 - dy;
        bool foreground = Math.Abs(u) <= 1.5 && Math.Abs(v) <= 18.5 || Math.Abs(v) <= 1.5 && Math.Abs(u) <= 18.5;
        return foreground ? 0.8 : 0.02;
    }

    private static double SplitHorizontalArmCross(double x, double y, double horizontalDy)
    {
        double u = x % 64 - 31.5;
        double v = y % 64 - 31.5;
        bool vertical = Math.Abs(u) <= 1.5 && Math.Abs(v) <= 18.5;
        bool horizontal = Math.Abs(v - horizontalDy) <= 1.5 && Math.Abs(u) <= 18.5;
        return vertical || horizontal ? 0.8 : 0.02;
    }

    private static AlgorithmImageBuffer Image(int width, int height, Func<int, int, int, double> pixel, AlgorithmImageFormat format = AlgorithmImageFormat.Gray32Float)
    {
        int channels = format.Channels(), bytes = format.BitsPerChannel() / 8;
        byte[] data = new byte[width * height * channels * bytes];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                for (int c = 0; c < channels; c++)
                {
                    double value = c == 3 ? 1 : pixel(x, y, c);
                    int offset = ((y * width + x) * channels + c) * bytes;
                    if (bytes == 1) data[offset] = (byte)Math.Round(value * 255);
                    else if (bytes == 2) BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), (ushort)Math.Round(value * 65535));
                    else BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(offset, 4), BitConverter.SingleToInt32Bits((float)value));
                }
        return new AlgorithmImageBuffer(width, height, width * channels * bytes, format, data);
    }

    private static double Erf(double x)
    {
        double sign = Math.Sign(x); x = Math.Abs(x);
        double t = 1 / (1 + 0.3275911 * x);
        return sign * (1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x));
    }
}
