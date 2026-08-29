using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.RoiStatistics;
using OpenCvSharp;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class RoiStatisticsV1Tests
{
    [Fact]
    public void ParameterValidationRejectsEveryOutOfContractFamily()
    {
        RoiStatisticsParameters parameters = new()
        {
            HistogramBins = 1,
            Percentiles = [50, 50, double.NaN],
            BadPixelNeighborhoodRadius = 0,
            BadPixelSigmaThreshold = 0,
            BadPixelMinimumDeviationFraction = 2,
            MaximumBadPixelCandidates = -1,
        };
        AlgorithmValidationResult validation = parameters.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(RoiStatisticsParameters.HistogramBins));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(RoiStatisticsParameters.Percentiles));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(RoiStatisticsParameters.BadPixelNeighborhoodRadius));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(RoiStatisticsParameters.BadPixelSigmaThreshold));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(RoiStatisticsParameters.BadPixelMinimumDeviationFraction));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(RoiStatisticsParameters.MaximumBadPixelCandidates));
    }

    [Fact]
    public async Task Gray16GoldenStatisticsPreserveBitDepthAndSaturationSemantics()
    {
        ushort[] values = [0, 1000, 32768, ushort.MaxValue];
        byte[] bytes = MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
        using AlgorithmImageBuffer input = new(2, 2, 4, AlgorithmImageFormat.Gray16, bytes);
        using AlgorithmResult result = await RunAsync(input, new RectangleAlgorithmRoi(0, 0, 2, 2));

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(4, Measurement(result, "roi.pixel_count"));
        Assert.Equal(0, Measurement(result, "channel.minimum", 0));
        Assert.Equal(ushort.MaxValue, Measurement(result, "channel.maximum", 0));
        Assert.Equal(values.Average(value => (double)value), Measurement(result, "channel.mean", 0), 10);
        Assert.Equal(1, Measurement(result, "channel.low_saturated_count", 0));
        Assert.Equal(1, Measurement(result, "channel.high_saturated_count", 0));
    }

    [Fact]
    public async Task Gray32FloatPercentilesRemainExactForOrdinaryRois()
    {
        float[] values = [9, 1, 5, 3];
        using AlgorithmImageBuffer input = new(
            values.Length,
            1,
            values.Length * sizeof(float),
            AlgorithmImageFormat.Gray32Float,
            MemoryMarshal.AsBytes(values.AsSpan()).ToArray());
        RoiStatisticsParameters parameters = new()
        {
            DetectBadPixelCandidates = false,
            HistogramBins = 4,
            Percentiles = [25, 50, 75],
        };

        using AlgorithmResult result = await RunAsync(
            input,
            new RectangleAlgorithmRoi(0, 0, values.Length, 1),
            parameters);

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        double[] percentiles = result.GetArtifact<AlgorithmMeasurementArtifact>()!.Measurements
            .Where(measurement => measurement.Name == "channel.percentile" && measurement.Channel == 0)
            .Select(measurement => measurement.Value)
            .ToArray();
        Assert.Equal([2.5, 4, 6], percentiles);
    }

    [Fact]
    public async Task FullFrame4KGray32FloatExceedingExactPercentileBudgetIsRejectedBeforeScan()
    {
        const int width = 3840;
        const int height = 2160;
        using AlgorithmImageBuffer input = new(
            width,
            height,
            checked(width * sizeof(float)),
            AlgorithmImageFormat.Gray32Float,
            new byte[checked(width * height * sizeof(float))]);
        List<string> stages = [];

        using AlgorithmResult result = await RunAsync(
            input,
            new RectangleAlgorithmRoi(0, 0, width, height),
            new RoiStatisticsParameters { DetectBadPixelCandidates = false, HistogramBins = 16 },
            new InlineProgress(progress => stages.Add(progress.Stage)));

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "roi_exact_float_statistics_budget_exceeded");
        Assert.DoesNotContain("roi.scan", stages);
    }

    [Theory]
    [InlineData(7680, 4320, 1)]
    [InlineData(3840, 2160, 4)]
    public void EightKGrayAndFourChannelFourKExceedTheExactFloatSampleBudgetWithoutAllocatingFrames(
        int width,
        int height,
        int channels)
    {
        long pixels = checked((long)width * height);

        Assert.True(RoiStatisticsAlgorithmProvider.ExceedsExactFloatingStatisticsBudget(pixels, channels, out long requiredBytes));
        Assert.True(requiredBytes > RoiStatisticsAlgorithmProvider.MaximumExactFloatingValueBytes);
    }

    [Fact]
    public async Task CancellationBeforeExactFloatOrderingSkipsTheSortAndArtifacts()
    {
        float[] values = Enumerable.Range(0, 100_000).Select(index => (float)(index % 997)).ToArray();
        using AlgorithmImageBuffer input = new(
            values.Length,
            1,
            values.Length * sizeof(float),
            AlgorithmImageFormat.Gray32Float,
            MemoryMarshal.AsBytes(values.AsSpan()).ToArray());
        using CancellationTokenSource cancellation = new();

        using AlgorithmResult result = await RunAsync(
            input,
            new RectangleAlgorithmRoi(0, 0, values.Length, 1),
            new RoiStatisticsParameters { DetectBadPixelCandidates = false, HistogramBins = 16 },
            new InlineProgress(progress =>
            {
                if (progress.Stage == "roi.percentiles") cancellation.Cancel();
            }),
            cancellation.Token);

        Assert.Equal(AlgorithmResultStatus.Cancelled, result.Status);
        Assert.Empty(result.Artifacts);
    }

    [Fact]
    public async Task ZeroBadPixelCandidateCapDisablesDetectionAndStorage()
    {
        const int width = 64;
        const int height = 64;
        byte[] checkerboard = Enumerable.Range(0, width * height)
            .Select(index => (byte)(((index / width + index % width) & 1) == 0 ? 0 : 255))
            .ToArray();
        using AlgorithmImageBuffer input = Buffer(checkerboard, width, height);
        List<string> stages = [];
        RoiStatisticsParameters parameters = new()
        {
            HistogramBins = 16,
            DetectBadPixelCandidates = true,
            BadPixelSigmaThreshold = 0.1,
            BadPixelMinimumDeviationFraction = 0,
            MaximumBadPixelCandidates = 0,
        };

        using AlgorithmResult result = await RunAsync(
            input,
            new RectangleAlgorithmRoi(0, 0, width, height),
            parameters,
            new InlineProgress(progress => stages.Add(progress.Stage)));

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(0, Measurement(result, "roi.bad_pixel_candidate_count"));
        Assert.Equal(0, Measurement(result, "roi.bad_pixel_channel_candidate_count"));
        Assert.Empty(result.GetArtifact<AlgorithmTableArtifact>("bad-pixel-candidates")!.Rows);
        Assert.DoesNotContain("roi.bad-pixels", stages);
    }

    [Fact]
    public async Task BoundedBadPixelCandidatesKeepTheStrongestDeterministically()
    {
        const int width = 7;
        byte[] pixels = Enumerable.Repeat((byte)100, width * 3).ToArray();
        pixels[width + 2] = 200;
        pixels[width + 4] = 255;
        RoiStatisticsParameters parameters = new()
        {
            HistogramBins = 16,
            DetectBadPixelCandidates = true,
            BadPixelSigmaThreshold = 0.1,
            BadPixelMinimumDeviationFraction = 0,
            MaximumBadPixelCandidates = 1,
        };

        using AlgorithmImageBuffer input = Buffer(pixels, width, 3);
        using AlgorithmResult result = await RunAsync(input, new RectangleAlgorithmRoi(0, 0, width, 3), parameters);

        Assert.Equal(2, Measurement(result, "roi.bad_pixel_candidate_count"));
        Assert.Equal(2, Measurement(result, "roi.bad_pixel_channel_candidate_count"));
        IReadOnlyDictionary<string, JsonElement> candidate = Assert.Single(
            result.GetArtifact<AlgorithmTableArtifact>("bad-pixel-candidates")!.Rows);
        Assert.Equal(4, candidate["X"].GetInt32());
        Assert.Equal(1, candidate["Y"].GetInt32());
        Assert.Equal(255, candidate["Value"].GetDouble());

        parameters.MaximumBadPixelCandidates = 10;
        using AlgorithmImageBuffer allInput = Buffer(pixels, width, 3);
        using AlgorithmResult all = await RunAsync(allInput, new RectangleAlgorithmRoi(0, 0, width, 3), parameters);
        AlgorithmTableArtifact allCandidates = all.GetArtifact<AlgorithmTableArtifact>("bad-pixel-candidates")!;
        Assert.Equal(2, allCandidates.Rows.Count);
        Assert.Equal(new[] { 255d, 200d }, allCandidates.Rows.Select(row => row["Value"].GetDouble()));
    }

    [Theory]
    [InlineData(AlgorithmImageFormat.Gray8, 0d, false)]
    [InlineData(AlgorithmImageFormat.Gray16, 1234d, false)]
    [InlineData(AlgorithmImageFormat.Gray32Float, -3.5d, false)]
    [InlineData(AlgorithmImageFormat.Gray32Float, 0d, false)]
    [InlineData(AlgorithmImageFormat.Gray32Float, -2d, true)]
    public async Task ConstantHistogramsContainEveryFiniteSampleInAConsistentInterval(
        AlgorithmImageFormat format,
        double value,
        bool includeNaN)
    {
        using AlgorithmImageBuffer input = ConstantBuffer(format, value, includeNaN);
        using AlgorithmResult result = await RunAsync(input, new RectangleAlgorithmRoi(0, 0, input.Width, 1));

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmTableArtifact histogram = result.GetArtifact<AlgorithmTableArtifact>("roi-histogram")!;
        long finiteCount = (long)Measurement(result, "channel.valid_count", 0);
        Assert.Equal(finiteCount, histogram.Rows.Sum(row => row["Count"].GetInt64()));
        IReadOnlyDictionary<string, JsonElement> occupied = Assert.Single(
            histogram.Rows.Where(row => row["Count"].GetInt64() > 0));
        double lower = occupied["LowerInclusive"].GetDouble();
        double upper = occupied["Upper"].GetDouble();
        bool upperInclusive = occupied["UpperInclusive"].GetBoolean();
        Assert.True(value >= lower && (value < upper || upperInclusive && value <= upper),
            $"Value {value:R} is not contained by {(upperInclusive ? "[lower, upper]" : "[lower, upper)")} = [{lower:R}, {upper:R}{(upperInclusive ? "]" : ")")}.");
        if (format.IsFloatingPoint())
        {
            Assert.Single(histogram.Rows);
            Assert.Equal(value, lower);
            Assert.Equal(value, upper);
            Assert.True(upperInclusive);
        }
        Assert.Equal(includeNaN ? 1 : 0, Measurement(result, "channel.nan_count", 0));
    }

    [Fact]
    public async Task MissingAndOutOfBoundsRoiReturnStructuredFailures()
    {
        using AlgorithmImageBuffer missingInput = Buffer([1, 2, 3, 4], 2, 2);
        using AlgorithmResult missing = await RunAsync(missingInput, roi: null);
        Assert.Equal(AlgorithmResultStatus.Failed, missing.Status);
        Assert.Contains(missing.Failures, failure => failure.Code == "roi_required" && failure.Path == "roi");

        using AlgorithmImageBuffer outsideInput = Buffer([1, 2, 3, 4], 2, 2);
        using AlgorithmResult outside = await RunAsync(outsideInput, new RectangleAlgorithmRoi(10, 10, 3, 3));
        Assert.Equal(AlgorithmResultStatus.Failed, outside.Status);
        Assert.Contains(outside.Failures, failure => failure.Code == "roi_empty_after_clip" && failure.Path == "roi");
    }

    [Fact]
    public async Task JsonAndCsvExportsContainReusableArtifactsAndRefuseOverwrite()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            using AlgorithmImageBuffer input = Buffer([0, 1, 2, 3], 2, 2);
            using AlgorithmResult result = await RunAsync(input, new RectangleAlgorithmRoi(0, 0, 2, 2));
            string jsonPath = AlgorithmResultExporter.ExportJson(result, Path.Combine(directory, "stats.json"));
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(jsonPath));
            Assert.Equal(StandardAlgorithmIds.RoiStatistics.Value, document.RootElement.GetProperty("algorithmId").GetString());
            Assert.Equal("Succeeded", document.RootElement.GetProperty("status").GetString());
            Assert.True(document.RootElement.GetProperty("artifacts").GetArrayLength() >= 7);
            Assert.Throws<IOException>(() => AlgorithmResultExporter.ExportJson(result, jsonPath));

            IReadOnlyList<string> csv = AlgorithmResultExporter.ExportCsvBundle(result, Path.Combine(directory, "stats.csv"));
            Assert.Equal(6, csv.Count);
            Assert.All(csv, path => Assert.True(File.Exists(path), path));
            Assert.StartsWith("Artifact,Name,Value", File.ReadAllText(csv[0]).TrimStart('\uFEFF'));
            Assert.Throws<IOException>(() => AlgorithmResultExporter.ExportCsvBundle(result, csv[0]));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task BatchAndFlowAdaptersReturnTheSameStructuredMeasurements()
    {
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(
            StandardAlgorithmIds.RoiStatistics,
            new RoiStatisticsParameters { DetectBadPixelCandidates = false, HistogramBins = 16 },
            new RectangleAlgorithmRoi(1, 0, 2, 2));
        string directory = CreateTemporaryDirectory();
        try
        {
            string sourcePath = Path.Combine(directory, "source.fake");
            BatchAlgorithmAnalysisProcessor processor = new([new TestBatchLoader()]);
            BatchAlgorithmAnalysisResult batch = await processor.ProcessAsync(new BatchAlgorithmAnalysisRequest
            {
                Items = [new BatchImageItem(sourcePath, directory)],
                Invocation = invocation,
                OutputDirectory = directory,
                PreserveFolderStructure = false,
            });
            BatchAlgorithmAnalysisFileResult file = Assert.Single(batch.Files);
            Assert.Equal(AlgorithmResultStatus.Succeeded, file.Status);
            string batchJson = File.ReadAllText(Assert.Single(file.OutputPaths));
            Assert.Contains("channel.mean", batchJson, StringComparison.Ordinal);

            LocalFrameMetadata metadata = new() { Width = 4, Height = 2, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, 8, 0);
            using LocalFlowFrameLease lease = frame.Acquire();
            Marshal.Copy(TestPixels, 0, lease.RawPointer, TestPixels.Length);
            using AlgorithmResult flow = await LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(lease, invocation);
            Assert.Equal(AlgorithmResultStatus.Succeeded, flow.Status);
            Assert.Equal(3.5, Measurement(flow, "channel.mean", 0), 12);
            Assert.Equal(TestPixels, lease.CopyRawToArray());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ResultWindowRendersAndReleasesTransientOverlayWithOwnedResult()
    {
        using AlgorithmImageBuffer input = Buffer(TestPixels, 4, 2);
        AlgorithmResult result = await RunAsync(input, new RectangleAlgorithmRoi(1, 0, 2, 2));
        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView view = new();
            view.SetImageSource(CreateBitmap(), enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });

        try
        {
            WpfTestHost.Invoke(() =>
            {
                ImageProcessingContext image = imageView.EditorContext.ProcessingContext;
                int visualCount = image.ImageShow.Visuals.Count;
                RoiStatisticsResultWindow window = new(result, image, imageView.EditorContext.DrawEditorContext);
                window.Show();
                Assert.Equal(visualCount + 1, image.ImageShow.Visuals.Count);
                Assert.Single(image.AlgorithmOverlays.Snapshot());
                window.Close();
                Assert.Equal(visualCount, image.ImageShow.Visuals.Count);
                Assert.Empty(image.AlgorithmOverlays.Snapshot());
                Assert.True(result.IsDisposed);
            });
        }
        finally
        {
            if (!result.IsDisposed) result.Dispose();
            WpfTestHost.Invoke(imageView.Dispose);
        }
    }

    [Fact]
    public void ImageViewMenuExposesOnlyTheThreeSupportedM1RoiShapes()
    {
        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            return new ImageView();
        });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                RoiStatisticsContextMenu menu = new(
                    imageView.EditorContext.ProcessingContext,
                    imageView.EditorContext.DrawEditorContext);
                string?[] ids = menu.GetContextMenuItems().Select(item => item.GuidId).ToArray();
                Assert.Equal(new[] { "RoiStatistics", "RoiStatisticsRectangle", "RoiStatisticsCircle", "RoiStatisticsPolygon" }, ids);
            });
        }
        finally
        {
            WpfTestHost.Invoke(imageView.Dispose);
        }
    }

    [Fact]
    public void ImageViewSessionIsLatestWinsAndDpiAdapterPreservesAnisotropicCircleGeometry()
    {
        MethodInfo circleMethod = typeof(RoiStatisticsEditorTool).GetMethod("Circle", BindingFlags.Static | BindingFlags.NonPublic)!;
        AlgorithmRoi circle = Assert.IsAssignableFrom<AlgorithmRoi>(circleMethod.Invoke(null, [new System.Windows.Point(10, 10), 4d, 2d, 1d]));
        PolygonAlgorithmRoi ellipse = Assert.IsType<PolygonAlgorithmRoi>(circle);
        Assert.Equal(64, ellipse.Points.Count);
        Assert.Equal(28, ellipse.Points.Max(point => point.X), 10);
        Assert.Equal(12, ellipse.Points.Min(point => point.X), 10);
        Assert.Equal(14, ellipse.Points.Max(point => point.Y), 10);
        Assert.Equal(6, ellipse.Points.Min(point => point.Y), 10);

        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView view = new();
            view.SetImageSource(CreateBitmap(), enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });
        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => imageView.EditorContext.ProcessingContext);
            Guid documentId = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            Guid olderId = Guid.NewGuid();
            Guid latestId = Guid.NewGuid();
            using CancellationTokenSource older = ImageAlgorithmAnalysisSession.Begin(
                context,
                documentId,
                revision,
                Guid.NewGuid(),
                olderId);
            using CancellationTokenSource latest = ImageAlgorithmAnalysisSession.Begin(
                context,
                documentId,
                revision,
                Guid.NewGuid(),
                latestId);
            Assert.True(older.IsCancellationRequested);
            Assert.False(ImageAlgorithmAnalysisSession.IsCurrent(context, documentId, revision, olderId));
            Assert.True(ImageAlgorithmAnalysisSession.IsCurrent(context, documentId, revision, latestId));

            WpfTestHost.Invoke(context.NotifySourcePixelsChanged);
            Assert.False(ImageAlgorithmAnalysisSession.IsCurrent(context, documentId, revision, latestId));
        }
        finally
        {
            WpfTestHost.Invoke(imageView.Dispose);
        }
    }

    private static readonly byte[] TestPixels = [0, 1, 2, 3, 4, 5, 6, 7];

    private static async Task<AlgorithmResult> RunAsync(AlgorithmImageBuffer input, AlgorithmRoi? roi)
        => await RunAsync(
            input,
            roi,
            new RoiStatisticsParameters { DetectBadPixelCandidates = false, HistogramBins = 16 });

    private static async Task<AlgorithmResult> RunAsync(
        AlgorithmImageBuffer input,
        AlgorithmRoi? roi,
        RoiStatisticsParameters parameters,
        IProgress<AlgorithmProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(
                StandardAlgorithmIds.RoiStatistics,
                parameters,
                roi),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            Progress = progress,
        }, cancellationToken);

    private static double Measurement(AlgorithmResult result, string name, int? channel = null)
        => result.GetArtifact<AlgorithmMeasurementArtifact>()!.Measurements
            .Single(measurement => measurement.Name == name && measurement.Channel == channel).Value;

    private static AlgorithmImageBuffer Buffer(byte[] pixels, int width, int height)
        => new(width, height, width, AlgorithmImageFormat.Gray8, pixels.ToArray());

    private static AlgorithmImageBuffer ConstantBuffer(AlgorithmImageFormat format, double value, bool includeNaN)
    {
        int count = includeNaN ? 4 : 3;
        if (format == AlgorithmImageFormat.Gray8)
            return new AlgorithmImageBuffer(count, 1, count, format, Enumerable.Repeat((byte)value, count).ToArray());
        if (format == AlgorithmImageFormat.Gray16)
        {
            ushort[] samples = Enumerable.Repeat((ushort)value, count).ToArray();
            return new AlgorithmImageBuffer(count, 1, count * sizeof(ushort), format, MemoryMarshal.AsBytes(samples.AsSpan()).ToArray());
        }
        float[] floats = Enumerable.Repeat((float)value, count).ToArray();
        if (includeNaN) floats[^1] = float.NaN;
        return new AlgorithmImageBuffer(count, 1, count * sizeof(float), format, MemoryMarshal.AsBytes(floats.AsSpan()).ToArray());
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ColorVision-RoiStatistics-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static WriteableBitmap CreateBitmap()
    {
        WriteableBitmap bitmap = new(4, 2, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, 4, 2), TestPixels, 4, 0);
        return bitmap;
    }

    private static void EnsureImageViewTestResources()
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

    private sealed class TestBatchLoader : IBatchImageLoader
    {
        public IReadOnlyCollection<string> Extensions { get; } = [".fake"];

        public Mat Load(string filePath)
        {
            Mat mat = new(2, 4, MatType.CV_8UC1);
            Marshal.Copy(TestPixels, 0, mat.Data, TestPixels.Length);
            return mat;
        }
    }

    private sealed class InlineProgress(Action<AlgorithmProgress> report) : IProgress<AlgorithmProgress>
    {
        public void Report(AlgorithmProgress value) => report(value);
    }
}
