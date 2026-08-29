using ColorVision.Algorithms;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageComparison;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageComparisonAdvancedV1Tests
{
    [Fact]
    public void AdvancedParametersValidateAndCatalogDeclaresVersionSchemaAndRoi()
    {
        ImageComparisonParameters invalid = new()
        {
            SsimWindowSize = 4,
            SsimK1 = 0,
            SsimK2 = 2,
            SsimMinimumValidFraction = 0,
            AlignmentSearchRadius = 33,
            AlignmentWarningThresholdPixels = -1,
            AlignmentMinimumOverlapFraction = 0,
            AlignmentMaximumSamples = 1,
        };
        Assert.Equal(8, invalid.Validate().Issues.Count);
        Assert.Equal(2, invalid.SchemaVersion);

        AlgorithmDescriptor descriptor = StandardAlgorithmCatalog.Create().Descriptors.Single(item => item.Id == StandardAlgorithmIds.ImageComparison);
        Assert.Equal(new AlgorithmVersion(1, 1, 0), descriptor.Version);
        Assert.Equal(2, descriptor.ParameterSchema.Version);
        Assert.True(descriptor.SupportsRectangleRoi);
        Assert.True(descriptor.SupportsCircleRoi);
        Assert.True(descriptor.SupportsPolygonRoi);
        Assert.False(descriptor.SupportsPolylineRoi);
        ImageComparisonParameters restored = AlgorithmJson.Deserialize<ImageComparisonParameters>(descriptor.ParameterSchema.Defaults);
        Assert.True(restored.Validate().IsValid);

        ImageComparisonParameters expected = new()
        {
            IncludeAlphaInMetrics = false,
            FloatPeakValue = 2,
            HeatmapMaximum = 3,
            EnableSsim = false,
            SsimWindowSize = 7,
            SsimK1 = 0.02,
            SsimK2 = 0.04,
            SsimMinimumValidFraction = 0.8,
            EnableAlignmentPrecheck = false,
            AlignmentSearchRadius = 5,
            AlignmentWarningThresholdPixels = 1.5,
            AlignmentMinimumOverlapFraction = 0.6,
            AlignmentMaximumSamples = 1024,
        };
        ImageComparisonParameters roundTrip = AlgorithmJson.Deserialize<ImageComparisonParameters>(AlgorithmJson.ToElement(expected));
        Assert.Equal(AlgorithmJson.ToElement(expected).GetRawText(), AlgorithmJson.ToElement(roundTrip).GetRawText());
    }

    [Fact]
    public async Task PersistedM3SchemaMigratesToM4DefaultsAndRetainsLegacyValues()
    {
        using AlgorithmImageBuffer reference = Bgra32(Enumerable.Repeat((byte)10, 8 * 8 * 4).ToArray(), 8, 8);
        using AlgorithmImageBuffer candidate = Bgra32(Enumerable.Repeat((byte)10, 8 * 8 * 4).ToArray(), 8, 8);
        AlgorithmInvocation legacy = new()
        {
            AlgorithmId = StandardAlgorithmIds.ImageComparison,
            ParameterSchemaVersion = 1,
            Parameters = JsonSerializer.SerializeToElement(new
            {
                includeAlphaInMetrics = false,
                floatPeakValue = 2.0,
                heatmapMaximum = 25.0,
            }, AlgorithmJson.Options),
        };
        using AlgorithmResult result = await RunAsync(reference, candidate, invocation: legacy);
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(new AlgorithmVersion(1, 1, 0), result.AlgorithmVersion);
        Assert.Equal(1, Measurement(result, "comparison.ssim"), 12);
        Assert.Equal(3, result.GetArtifact<AlgorithmTableArtifact>("image-comparison-channels")!.Rows.Count);
        AlgorithmStructuredDataArtifact structured = result.GetArtifact<AlgorithmStructuredDataArtifact>("image-comparison")!;
        Assert.Equal("colorvision.analysis.image-comparison/v2", structured.Schema);
        Assert.Equal(25, structured.Data.GetProperty("heatmapMaximum").GetDouble());
    }

    [Theory]
    [InlineData(AlgorithmImageFormat.Gray8)]
    [InlineData(AlgorithmImageFormat.Gray16)]
    [InlineData(AlgorithmImageFormat.Gray32Float)]
    [InlineData(AlgorithmImageFormat.Bgr24)]
    public async Task IdenticalImagesHaveGoldenSsimOneAcrossDepthsAndChannels(AlgorithmImageFormat format)
    {
        using AlgorithmImageBuffer reference = Pattern(format, 9, 7);
        using AlgorithmImageBuffer candidate = reference.Clone();
        using AlgorithmResult result = await RunAsync(reference, candidate, new ImageComparisonParameters { AlignmentSearchRadius = 2 });
        Assert.Equal(1, Measurement(result, "comparison.ssim"), 12);
        Assert.All(result.GetArtifact<AlgorithmTableArtifact>("image-comparison-channels")!.Rows,
            row => Assert.Equal(1, row["SSIM"].GetDouble(), 12));
    }

    [Fact]
    public async Task ConstantImageSsimMatchesClosedFormLuminanceTerm()
    {
        using AlgorithmImageBuffer reference = Gray8(Enumerable.Repeat((byte)10, 25).ToArray(), 5, 5);
        using AlgorithmImageBuffer candidate = Gray8(Enumerable.Repeat((byte)20, 25).ToArray(), 5, 5);
        ImageComparisonParameters parameters = new() { SsimWindowSize = 3, EnableAlignmentPrecheck = false };
        using AlgorithmResult result = await RunAsync(reference, candidate, parameters);
        double c1 = Math.Pow(parameters.SsimK1 * 255, 2);
        double expected = (2 * 10 * 20 + c1) / (10 * 10 + 20 * 20 + c1);
        Assert.Equal(expected, Measurement(result, "comparison.ssim"), 12);
    }

    [Fact]
    public async Task SsimIsScaleConsistentForEightSixteenAndFloatInputs()
    {
        using AlgorithmImageBuffer byteReference = Gray8([0, 32, 64, 96, 128, 160, 192, 224, 255], 3, 3);
        using AlgorithmImageBuffer byteCandidate = Gray8([0, 40, 60, 100, 120, 170, 180, 230, 250], 3, 3);
        using AlgorithmResult byteResult = await RunAsync(byteReference, byteCandidate, new ImageComparisonParameters { SsimWindowSize = 3, EnableAlignmentPrecheck = false });

        ushort[] reference16 = byteReference.Data.Span.ToArray().Select(value => (ushort)(value * 257)).ToArray();
        ushort[] candidate16 = byteCandidate.Data.Span.ToArray().Select(value => (ushort)(value * 257)).ToArray();
        using AlgorithmImageBuffer ushortReference = UShort(reference16, 3, 3, AlgorithmImageFormat.Gray16);
        using AlgorithmImageBuffer ushortCandidate = UShort(candidate16, 3, 3, AlgorithmImageFormat.Gray16);
        using AlgorithmResult ushortResult = await RunAsync(ushortReference, ushortCandidate, new ImageComparisonParameters { SsimWindowSize = 3, EnableAlignmentPrecheck = false });

        float[] referenceFloat = byteReference.Data.Span.ToArray().Select(value => value / 255f).ToArray();
        float[] candidateFloat = byteCandidate.Data.Span.ToArray().Select(value => value / 255f).ToArray();
        using AlgorithmImageBuffer floatReference = Floats(referenceFloat, 3, 3, AlgorithmImageFormat.Gray32Float);
        using AlgorithmImageBuffer floatCandidate = Floats(candidateFloat, 3, 3, AlgorithmImageFormat.Gray32Float);
        using AlgorithmResult floatResult = await RunAsync(floatReference, floatCandidate, new ImageComparisonParameters { SsimWindowSize = 3, EnableAlignmentPrecheck = false });

        double expected = Measurement(byteResult, "comparison.ssim");
        Assert.Equal(expected, Measurement(ushortResult, "comparison.ssim"), 10);
        Assert.Equal(expected, Measurement(floatResult, "comparison.ssim"), 6);
    }

    [Fact]
    public async Task RoiRestrictsMetricsSsimHeatmapAndProducesStructuredOverlay()
    {
        byte[] referencePixels = new byte[16];
        byte[] candidatePixels = Enumerable.Repeat((byte)100, 16).ToArray();
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++) candidatePixels[y * 4 + x] = 0;
        using AlgorithmImageBuffer reference = Gray8(referencePixels, 4, 4);
        using AlgorithmImageBuffer candidate = Gray8(candidatePixels, 4, 4);
        RectangleAlgorithmRoi roi = new(0, 0, 2, 2);
        using AlgorithmResult result = await RunAsync(reference, candidate, roi: roi,
            parameters: new ImageComparisonParameters { SsimWindowSize = 3, EnableAlignmentPrecheck = false });

        Assert.Equal(0, Measurement(result, "comparison.mse"));
        Assert.Equal(4, Measurement(result, "comparison.finite_sample_count"));
        Assert.Equal(1, Measurement(result, "comparison.ssim"), 12);
        Assert.Equal(100, Image(result, "absolute-difference").Data.Span[15]);
        AlgorithmImageBuffer heatmap = Image(result, "difference-heatmap");
        Assert.Equal(new byte[] { 0, 0, 0 }, heatmap.Data.Span.Slice((3 * 4 + 3) * 3, 3).ToArray());
        Assert.NotNull(result.GetArtifact<AlgorithmGeometryArtifact>("comparison-roi"));
        Assert.Equal(AlgorithmOverlayLifetime.Transient, result.GetArtifact<AlgorithmOverlayArtifact>("comparison-roi-overlay")!.Lifetime);
    }

    [Theory]
    [MemberData(nameof(RoiCases))]
    public async Task RectangleCirclePolygonAndPhysicalRoiSharePixelCenterRules(AlgorithmRoi roi, double expectedPixels)
    {
        using AlgorithmImageBuffer reference = new(5, 5, 5, AlgorithmImageFormat.Gray8, new byte[25], 254, 254);
        using AlgorithmImageBuffer candidate = new(5, 5, 5, AlgorithmImageFormat.Gray8, new byte[25], 254, 254);
        using AlgorithmResult result = await RunAsync(reference, candidate, roi: roi,
            parameters: new ImageComparisonParameters { EnableSsim = false, EnableAlignmentPrecheck = false });
        Assert.Equal(expectedPixels, Measurement(result, "comparison.finite_sample_count"));
    }

    public static TheoryData<AlgorithmRoi, double> RoiCases => new()
    {
        { new RectangleAlgorithmRoi(1, 1, 2, 2), 4 },
        { new CircleAlgorithmRoi(new AlgorithmPoint(2, 2), 1), 5 },
        { new PolygonAlgorithmRoi([new(0, 0), new(2, 0), new(0, 2)]), 6 },
        { new RectangleAlgorithmRoi(0.1, 0.1, 0.2, 0.2) { CoordinateSpace = AlgorithmCoordinateSpace.Physical }, 4 },
    };

    [Fact]
    public async Task EmptyAndClippedRoiHaveStructuredBehavior()
    {
        using AlgorithmImageBuffer reference = Gray8(new byte[16], 4, 4);
        using AlgorithmImageBuffer candidate = Gray8(new byte[16], 4, 4);
        using AlgorithmResult empty = await RunAsync(reference, candidate, roi: new RectangleAlgorithmRoi(10, 10, 2, 2));
        Assert.Contains(empty.Failures, failure => failure.Code == "comparison_roi_empty");

        using AlgorithmImageBuffer reference2 = Gray8(new byte[16], 4, 4);
        using AlgorithmImageBuffer candidate2 = Gray8(new byte[16], 4, 4);
        using AlgorithmResult clipped = await RunAsync(reference2, candidate2, roi: new RectangleAlgorithmRoi(-1, -1, 3, 3),
            parameters: new ImageComparisonParameters { EnableSsim = false, EnableAlignmentPrecheck = false });
        Assert.Equal(4, Measurement(clipped, "comparison.finite_sample_count"));
        Assert.Contains(clipped.Diagnostics.Messages, message => message.Code == "comparison_roi_clipped");
    }

    [Fact]
    public async Task AlignmentPrecheckFindsKnownCandidateOffsetWithoutTransformingImages()
    {
        const int width = 32;
        const int height = 24;
        byte[] left = PatternBytes(width, height);
        byte[] right = new byte[left.Length];
        const int shiftX = 2;
        const int shiftY = -1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int targetX = x + shiftX;
                int targetY = y + shiftY;
                if (targetX >= 0 && targetX < width && targetY >= 0 && targetY < height)
                    right[targetY * width + targetX] = left[y * width + x];
            }
        }
        using AlgorithmImageBuffer reference = Gray8(left, width, height);
        using AlgorithmImageBuffer candidate = Gray8(right, width, height);
        using AlgorithmResult result = await RunAsync(reference, candidate, new ImageComparisonParameters
        {
            EnableSsim = false,
            AlignmentSearchRadius = 4,
            AlignmentMinimumOverlapFraction = 0.7,
            AlignmentMaximumSamples = 4096,
        });
        AlgorithmTableArtifact alignment = result.GetArtifact<AlgorithmTableArtifact>("image-comparison-alignment")!;
        IReadOnlyDictionary<string, JsonElement> row = Assert.Single(alignment.Rows);
        Assert.Equal("ok", row["Status"].GetString());
        Assert.Equal(shiftX, row["EstimatedShiftX"].GetInt32());
        Assert.Equal(shiftY, row["EstimatedShiftY"].GetInt32());
        Assert.Equal(1, row["BestCorrelation"].GetDouble(), 12);
        Assert.Contains(result.Diagnostics.Messages, message => message.Code == "alignment_shift_suspected");
        Assert.NotNull(result.GetArtifact<AlgorithmGeometryArtifact>("alignment-precheck"));
        Assert.Equal(left, reference.Data.ToArray());
        Assert.Equal(right, candidate.Data.ToArray());
    }

    [Fact]
    public async Task FlatContentProducesInconclusiveAlignmentDiagnostic()
    {
        using AlgorithmImageBuffer reference = Gray8(Enumerable.Repeat((byte)10, 16 * 16).ToArray(), 16, 16);
        using AlgorithmImageBuffer candidate = Gray8(Enumerable.Repeat((byte)10, 16 * 16).ToArray(), 16, 16);
        using AlgorithmResult result = await RunAsync(reference, candidate, new ImageComparisonParameters { EnableSsim = false });
        IReadOnlyDictionary<string, JsonElement> row = Assert.Single(result.GetArtifact<AlgorithmTableArtifact>("image-comparison-alignment")!.Rows);
        Assert.Equal("low_texture", row["Status"].GetString());
        Assert.Contains(result.Diagnostics.Messages, message => message.Code == "alignment_precheck_inconclusive");
    }

    [Fact]
    public async Task AlignmentPrecheckHonorsDeterministicSampleBound()
    {
        const int width = 257;
        const int height = 257;
        using AlgorithmImageBuffer reference = Gray8(PatternBytes(width, height), width, height);
        using AlgorithmImageBuffer candidate = reference.Clone();
        using AlgorithmResult result = await RunAsync(reference, candidate, new ImageComparisonParameters
        {
            EnableSsim = false,
            AlignmentSearchRadius = 0,
            AlignmentMaximumSamples = 256,
        });
        IReadOnlyDictionary<string, JsonElement> row = Assert.Single(result.GetArtifact<AlgorithmTableArtifact>("image-comparison-alignment")!.Rows);
        Assert.InRange(row["SampleCount"].GetInt64(), 4, 256);
        Assert.Equal(17, row["SampleStep"].GetInt32());
    }

    [Fact]
    public async Task NonFiniteWindowsAreExcludedAndReportedWithoutLosingFinitePixelMetrics()
    {
        float[] values = Enumerable.Repeat(float.NaN, 9).ToArray();
        values[4] = 1;
        using AlgorithmImageBuffer reference = Floats(values, 3, 3, AlgorithmImageFormat.Gray32Float);
        using AlgorithmImageBuffer candidate = reference.Clone();
        using AlgorithmResult result = await RunAsync(reference, candidate, new ImageComparisonParameters
        {
            SsimWindowSize = 3,
            SsimMinimumValidFraction = 0.9,
            EnableAlignmentPrecheck = false,
        });

        Assert.Equal(1, Measurement(result, "comparison.finite_sample_count"));
        Assert.Null(result.GetArtifact<AlgorithmMeasurementArtifact>("image-comparison")!.Measurements
            .SingleOrDefault(value => value.Name == "comparison.ssim"));
        Assert.Equal(0, Measurement(result, "comparison.ssim.valid_window_count"));
        Assert.Equal(9, Measurement(result, "comparison.ssim.invalid_window_count"));
        Assert.Contains(result.Diagnostics.Messages, message => message.Code == "ssim_unavailable");
    }

    [Fact]
    public async Task CancellationDuringSsimReleasesTransferredInputs()
    {
        const int width = 512;
        const int height = 512;
        AlgorithmImageBuffer reference = Gray8(PatternBytes(width, height), width, height);
        AlgorithmImageBuffer candidate = reference.Clone();
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "comparison.ssim") cancellation.Cancel();
        });
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageComparison, new ImageComparisonParameters()),
            Inputs = [Input("reference", reference, AlgorithmInputOwnership.Transferred), Input("candidate", candidate, AlgorithmInputOwnership.Transferred)],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
            Progress = progress,
        }, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, result.Status);
        Assert.True(reference.IsDisposed);
        Assert.True(candidate.IsDisposed);
    }

    [Fact]
    public async Task CancellationDuringAlignmentPrecheckReleasesTransferredInputs()
    {
        const int width = 512;
        const int height = 512;
        AlgorithmImageBuffer reference = Gray8(PatternBytes(width, height), width, height);
        AlgorithmImageBuffer candidate = reference.Clone();
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "comparison.alignment-precheck") cancellation.Cancel();
        });
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageComparison, new ImageComparisonParameters { EnableSsim = false }),
            Inputs = [Input("reference", reference, AlgorithmInputOwnership.Transferred), Input("candidate", candidate, AlgorithmInputOwnership.Transferred)],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
            Progress = progress,
        }, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, result.Status);
        Assert.True(reference.IsDisposed);
        Assert.True(candidate.IsDisposed);
    }

    [Fact]
    public async Task ResultWindowShowsQualityTableAndOwnsTransientRoiOverlay()
    {
        using AlgorithmImageBuffer reference = Gray8(PatternBytes(8, 8), 8, 8);
        using AlgorithmImageBuffer candidate = reference.Clone();
        AlgorithmResult result = await RunAsync(reference, candidate,
            new ImageComparisonParameters { SsimWindowSize = 3, EnableAlignmentPrecheck = false },
            new RectangleAlgorithmRoi(1, 1, 4, 4));
        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureResources();
            ImageView created = new();
            created.SetImageSource(Bitmap(reference.Data.ToArray(), 8, 8), enableEditorImageServices: false, configureDefaultLayerController: false);
            return created;
        });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                ImageProcessingContext context = imageView.EditorContext.ProcessingContext;
                int visualCount = context.ImageShow.Visuals.Count;
                ImageComparisonResultWindow window = new(result, Bitmap(reference.Data.ToArray(), 8, 8), Bitmap(candidate.Data.ToArray(), 8, 8),
                    "candidate.png", context, imageView.EditorContext.DrawEditorContext);
                window.Show();
                Assert.NotNull(window.FindName("AlignmentGrid"));
                Assert.Equal(visualCount + 1, context.ImageShow.Visuals.Count);
                Assert.Single(context.AlgorithmOverlays.Snapshot());
                window.Close();
                Assert.Equal(visualCount, context.ImageShow.Visuals.Count);
                Assert.Empty(context.AlgorithmOverlays.Snapshot());
                Assert.True(result.IsDisposed);
            });
        }
        finally
        {
            if (!result.IsDisposed) result.Dispose();
            WpfTestHost.Invoke(imageView.Dispose);
        }
    }

    private static async Task<AlgorithmResult> RunAsync(
        AlgorithmImageBuffer reference,
        AlgorithmImageBuffer candidate,
        ImageComparisonParameters? parameters = null,
        AlgorithmRoi? roi = null,
        AlgorithmInvocation? invocation = null)
        => await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = invocation ?? AlgorithmInvocation.Create(StandardAlgorithmIds.ImageComparison, parameters ?? new ImageComparisonParameters(), roi),
            Inputs = [Input("reference", reference, AlgorithmInputOwnership.Borrowed), Input("candidate", candidate, AlgorithmInputOwnership.Borrowed)],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
        });

    private static AlgorithmInput Input(string name, AlgorithmImageBuffer image, AlgorithmInputOwnership ownership)
        => new() { Name = name, Image = image, Ownership = ownership, ColorSpace = "encoded-device-values" };

    private static double Measurement(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmMeasurementArtifact>("image-comparison")!.Measurements.Single(value => value.Name == name).Value;

    private static AlgorithmImageBuffer Image(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmImageArtifact>(name)!.Image;

    private static AlgorithmImageBuffer Pattern(AlgorithmImageFormat format, int width, int height)
    {
        int channels = format.Channels();
        if (format.BitsPerChannel() == 8)
        {
            byte[] values = new byte[width * height * channels];
            for (int index = 0; index < values.Length; index++) values[index] = (byte)((index * 37 + index / 7 * 13) % 251);
            return new AlgorithmImageBuffer(width, height, width * format.BytesPerPixel(), format, values);
        }
        if (format.BitsPerChannel() == 16)
        {
            ushort[] values = new ushort[width * height * channels];
            for (int index = 0; index < values.Length; index++) values[index] = (ushort)((index * 977 + index / 7 * 313) % 65521);
            return UShort(values, width, height, format);
        }
        float[] floats = new float[width * height * channels];
        for (int index = 0; index < floats.Length; index++) floats[index] = ((index * 37 + index / 7 * 13) % 251) / 250f;
        return Floats(floats, width, height, format);
    }

    private static byte[] PatternBytes(int width, int height)
    {
        byte[] values = new byte[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++) values[y * width + x] = (byte)((x * 31 + y * 47 + x * y * 7) % 251);
        return values;
    }

    private static AlgorithmImageBuffer Gray8(byte[] values, int width, int height)
        => new(width, height, width, AlgorithmImageFormat.Gray8, values.ToArray());

    private static AlgorithmImageBuffer Bgra32(byte[] values, int width, int height)
        => new(width, height, width * 4, AlgorithmImageFormat.Bgra32, values.ToArray());

    private static AlgorithmImageBuffer UShort(ushort[] values, int width, int height, AlgorithmImageFormat format)
        => new(width, height, width * format.BytesPerPixel(), format, MemoryMarshal.AsBytes(values.AsSpan()).ToArray());

    private static AlgorithmImageBuffer Floats(float[] values, int width, int height, AlgorithmImageFormat format)
        => new(width, height, width * format.BytesPerPixel(), format, MemoryMarshal.AsBytes(values.AsSpan()).ToArray());

    private static WriteableBitmap Bitmap(byte[] pixels, int width, int height)
    {
        WriteableBitmap bitmap = new(width, height, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width, 0);
        return bitmap;
    }

    private static void EnsureResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private sealed class InlineProgress(Action<AlgorithmProgress> report) : IProgress<AlgorithmProgress>
    {
        public void Report(AlgorithmProgress value) => report(value);
    }
}
