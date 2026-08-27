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
            Type session = typeof(RoiStatisticsEditorTool).Assembly.GetType(
                "ColorVision.ImageEditor.Algorithms.ImageAlgorithmAnalysisSession",
                throwOnError: true)!;
            MethodInfo begin = session.GetMethod("Begin", BindingFlags.Public | BindingFlags.Static)!;
            MethodInfo isCurrent = session.GetMethod("IsCurrent", BindingFlags.Public | BindingFlags.Static)!;
            ImageProcessingContext context = WpfTestHost.Invoke(() => imageView.EditorContext.ProcessingContext);
            Guid documentId = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long revision = WpfTestHost.Invoke(() => context.ImageRevision);
            Guid olderId = Guid.NewGuid();
            Guid latestId = Guid.NewGuid();
            using CancellationTokenSource older = Assert.IsType<CancellationTokenSource>(begin.Invoke(null, [context, olderId]));
            using CancellationTokenSource latest = Assert.IsType<CancellationTokenSource>(begin.Invoke(null, [context, latestId]));
            Assert.True(older.IsCancellationRequested);
            Assert.False(Assert.IsType<bool>(isCurrent.Invoke(null, [context, documentId, revision, olderId])));
            Assert.True(Assert.IsType<bool>(isCurrent.Invoke(null, [context, documentId, revision, latestId])));

            WpfTestHost.Invoke(context.NotifySourcePixelsChanged);
            Assert.False(Assert.IsType<bool>(isCurrent.Invoke(null, [context, documentId, revision, latestId])));
        }
        finally
        {
            WpfTestHost.Invoke(imageView.Dispose);
        }
    }

    private static readonly byte[] TestPixels = [0, 1, 2, 3, 4, 5, 6, 7];

    private static async Task<AlgorithmResult> RunAsync(AlgorithmImageBuffer input, AlgorithmRoi? roi)
        => await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(
                StandardAlgorithmIds.RoiStatistics,
                new RoiStatisticsParameters { DetectBadPixelCandidates = false, HistogramBins = 16 },
                roi),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

    private static double Measurement(AlgorithmResult result, string name, int? channel = null)
        => result.GetArtifact<AlgorithmMeasurementArtifact>()!.Measurements
            .Single(measurement => measurement.Name == name && measurement.Channel == channel).Value;

    private static AlgorithmImageBuffer Buffer(byte[] pixels, int width, int height)
        => new(width, height, width, AlgorithmImageFormat.Gray8, pixels.ToArray());

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
}
