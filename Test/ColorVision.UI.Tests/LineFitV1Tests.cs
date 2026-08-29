using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.LineFit;
using OpenCvSharp;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class LineFitV1Tests
{
    private const int Width = 100;
    private const int Height = 100;

    [Fact]
    public void CatalogDefaultsValidationJsonAliasesAndCopilotBoundaryAreStable()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        Assert.True(catalog.TryResolveAlias("FitLine", out AlgorithmDescriptor? alias));
        AlgorithmDescriptor descriptor = Assert.Single(catalog.Descriptors, item => item.Id == StandardAlgorithmIds.LineFit);
        Assert.Same(descriptor, alias);
        Assert.Equal(new AlgorithmVersion(1, 0, 0), descriptor.Version);
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Interactive));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Batch));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Flow));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Copilot));
        Assert.True(descriptor.SupportsPolylineRoi);
        Assert.Empty(descriptor.OutputFormats!);
        Assert.Equal("no-image-output", descriptor.OutputFormatPolicy);

        LineFitParameters defaults = descriptor.ParameterSchema.Defaults.Deserialize<LineFitParameters>(AlgorithmJson.Options)!;
        Assert.True(defaults.Validate().IsValid);
        string json = JsonSerializer.Serialize(defaults, AlgorithmJson.Options);
        Assert.True(JsonSerializer.Deserialize<LineFitParameters>(json, AlgorithmJson.Options)!.Validate().IsValid);
        Assert.Contains(descriptor.ParameterSchema.Fields, field => field.Name == nameof(LineFitParameters.ResidualThresholdPixels)
            && field.Minimum == 0.000001 && field.Maximum == 1_000_000 && field.Unit == "px");

        LineFitParameters invalid = new()
        {
            Mode = (LineFitMode)99,
            OutputExtent = (LineFitOutputExtent)99,
            ResidualThresholdPixels = 0,
            HuberTuningConstant = 11,
            MaximumIterations = 0,
            ConvergenceTolerance = 1,
            MinimumInlierCount = 1,
            MaximumPoints = 1,
            MaximumOverlayPoints = 10_001,
        };
        Assert.Equal(9, invalid.Validate().Issues.Count);
    }

    public static TheoryData<AlgorithmImageFormat> CanonicalFormats => new()
    {
        AlgorithmImageFormat.Gray8,
        AlgorithmImageFormat.Gray16,
        AlgorithmImageFormat.Gray32Float,
        AlgorithmImageFormat.Bgr24,
        AlgorithmImageFormat.Bgr48,
        AlgorithmImageFormat.Bgr96Float,
        AlgorithmImageFormat.Bgra32,
        AlgorithmImageFormat.Bgra64,
        AlgorithmImageFormat.Bgra128Float,
    };

    [Theory]
    [MemberData(nameof(CanonicalFormats))]
    public async Task RobustGoldenIsFormatInvariantRejectsOutlierAndLeavesInputReadOnly(AlgorithmImageFormat format)
    {
        using AlgorithmImageBuffer input = CreateInput(format);
        byte[] original = input.Data.ToArray();
        using AlgorithmResult result = await RunAsync(input, PointsWithOutlier(), new LineFitParameters());

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(1, Measurement(result, "line_fit.accepted"));
        Assert.Equal(9, Measurement(result, "line_fit.inlier_count"));
        Assert.Equal(1, Measurement(result, "line_fit.rejected_count"));
        Assert.InRange(Measurement(result, "line_fit.angle_degrees"), 26.45, 26.68);
        Assert.InRange(Measurement(result, "line_fit.rms_residual"), 0, 0.12);
        Assert.InRange(Measurement(result, "line_fit.confidence"), 0.8, 1);
        AlgorithmTableArtifact table = Points(result);
        IReadOnlyDictionary<string, JsonElement> outlier = table.Rows.Single(row => row["PointIndex"].GetInt32() == 9);
        Assert.False(outlier["Accepted"].GetBoolean());
        Assert.Equal("residual_above_threshold", outlier["RejectionReason"].GetString());
        AlgorithmGeometry line = result.GetArtifact<AlgorithmGeometryArtifact>("line-fit-geometry")!.Geometries.Single(item => item.Id == "fitted-line");
        Assert.Equal(AlgorithmGeometryKind.Line, line.Kind);
        Assert.Equal(2, line.Points.Count);
        Assert.Equal(Measurement(result, "line_fit.rms_residual"), line.Residual);
        Assert.Equal("colorvision.measurement.line-fit/v1", result.GetArtifact<AlgorithmStructuredDataArtifact>("line-fit-provenance")!.Schema);
        Assert.Equal(original, input.Data.ToArray());
    }

    [Fact]
    public async Task VerticalTlsPhysicalCoordinatesAndInlierExtentAreExplicit()
    {
        using AlgorithmImageBuffer input = new(Width, Height, Width, AlgorithmImageFormat.Gray8, new byte[Width * Height], 254, 254);
        PolylineAlgorithmRoi physical = new([
            new(1.2, 1), new(1.19, 3), new(1.21, 5), new(1.2, 7),
        ]) { CoordinateSpace = AlgorithmCoordinateSpace.Physical };
        using AlgorithmResult result = await RunAsync(input, physical, new LineFitParameters
        {
            Mode = LineFitMode.TotalLeastSquares,
            ResidualThresholdPixels = 0.2,
            OutputExtent = LineFitOutputExtent.InlierSpan,
        });

        Assert.Equal(1, Measurement(result, "line_fit.accepted"));
        Assert.InRange(Math.Abs(Measurement(result, "line_fit.angle_degrees")), 89.8, 90.0);
        AlgorithmGeometry line = result.GetArtifact<AlgorithmGeometryArtifact>("line-fit-geometry")!.Geometries.Single(item => item.Id == "fitted-line");
        Assert.All(line.Points, point => Assert.InRange(point.X, 11.8, 12.2));
        Assert.InRange(line.Points.Min(point => point.Y), 9.9, 10.1);
        Assert.InRange(line.Points.Max(point => point.Y), 69.9, 70.1);
    }

    [Fact]
    public async Task DegenerateInsufficientWrongRoiAndPointLimitHaveStructuredReasons()
    {
        using AlgorithmImageBuffer degenerateInput = CreateInput(AlgorithmImageFormat.Gray8);
        using AlgorithmResult degenerate = await RunAsync(degenerateInput, new PolylineAlgorithmRoi([new(4, 4), new(4, 4), new(4, 4)]), new LineFitParameters());
        Assert.Equal(AlgorithmResultStatus.Succeeded, degenerate.Status);
        Assert.Equal(0, Measurement(degenerate, "line_fit.accepted"));
        Assert.All(Points(degenerate).Rows, row => Assert.Equal("degenerate_point_distribution", row["RejectionReason"].GetString()));

        using AlgorithmImageBuffer isotropicInput = CreateInput(AlgorithmImageFormat.Gray8);
        using AlgorithmResult isotropic = await RunAsync(isotropicInput, new PolylineAlgorithmRoi([new(2, 2), new(8, 2), new(8, 8), new(2, 8)]), new LineFitParameters());
        Assert.Equal(0, Measurement(isotropic, "line_fit.accepted"));
        Assert.All(Points(isotropic).Rows, row => Assert.Equal("degenerate_point_distribution", row["RejectionReason"].GetString()));

        using AlgorithmImageBuffer insufficientInput = CreateInput(AlgorithmImageFormat.Gray8);
        using AlgorithmResult insufficient = await RunAsync(insufficientInput, new PolylineAlgorithmRoi([new(1, 1), new(2, 2), new(3, 3)]), new LineFitParameters { MinimumInlierCount = 4 });
        Assert.Equal(0, Measurement(insufficient, "line_fit.accepted"));
        Assert.All(Points(insufficient).Rows, row => Assert.Equal("insufficient_inliers", row["RejectionReason"].GetString()));

        AlgorithmImageBuffer wrongRoiInput = CreateInput(AlgorithmImageFormat.Gray8);
        using AlgorithmResult wrongRoi = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.LineFit, new LineFitParameters(), new RectangleAlgorithmRoi(0, 0, 10, 10)),
            Inputs = [new AlgorithmInput { Name = "source", Image = wrongRoiInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        Assert.Equal(AlgorithmResultStatus.Failed, wrongRoi.Status);
        Assert.Contains(wrongRoi.Failures, failure => failure.Code == "roi_kind_unsupported");
        Assert.True(wrongRoiInput.IsDisposed);

        AlgorithmImageBuffer limitedInput = CreateInput(AlgorithmImageFormat.Gray8);
        using AlgorithmResult limited = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.LineFit, new LineFitParameters { MaximumPoints = 2 }, new PolylineAlgorithmRoi([new(1, 1), new(2, 2), new(3, 3)])),
            Inputs = [new AlgorithmInput { Name = "source", Image = limitedInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });
        Assert.Equal("line_fit_point_limit_exceeded", Assert.Single(limited.Failures).Code);
        Assert.True(limitedInput.IsDisposed);
    }

    [Fact]
    public async Task CancellationSuccessAndFailureReleaseTransferredInput()
    {
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "line-fit.solve") cancellation.Cancel();
        });
        AlgorithmImageBuffer cancelledInput = CreateInput(AlgorithmImageFormat.Gray8);
        using AlgorithmResult cancelled = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.LineFit, new LineFitParameters(), PointsWithOutlier()),
            Inputs = [new AlgorithmInput { Name = "source", Image = cancelledInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
            Progress = progress,
        }, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, cancelled.Status);
        Assert.True(cancelledInput.IsDisposed);

        AlgorithmImageBuffer successInput = CreateInput(AlgorithmImageFormat.Gray8);
        using AlgorithmResult success = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.LineFit, new LineFitParameters(), PointsWithOutlier()),
            Inputs = [new AlgorithmInput { Name = "source", Image = successInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, success.Status);
        Assert.True(successInput.IsDisposed);
    }

    [Fact]
    public async Task BatchAndFlowReuseInvocationAndStructuredArtifacts()
    {
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.LineFit, new LineFitParameters(), PointsWithOutlier());
        string directory = Path.Combine(Path.GetTempPath(), $"ColorVision-LineFit-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            BatchAlgorithmAnalysisResult batch = await new BatchAlgorithmAnalysisProcessor([new TestLoader()], ExperimentalAlgorithmTestRuntime.Runtime).ProcessAsync(new BatchAlgorithmAnalysisRequest
            {
                Items = [new BatchImageItem(Path.Combine(directory, "source.fake"), directory)],
                Invocation = invocation,
                OutputDirectory = directory,
                PreserveFolderStructure = false,
            });
            Assert.Equal(AlgorithmResultStatus.Succeeded, Assert.Single(batch.Files).Status);
            Assert.Contains("line-fit-geometry", File.ReadAllText(Assert.Single(batch.Files[0].OutputPaths)), StringComparison.Ordinal);

            LocalFrameMetadata metadata = new() { Width = Width, Height = Height, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, Width * Height, 0);
            using LocalFlowFrameLease lease = frame.Acquire();
            using AlgorithmResult flow = await LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(ExperimentalAlgorithmTestRuntime.Runtime, lease, invocation);
            Assert.Equal(9, Measurement(flow, "line_fit.inlier_count"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImageViewMenuTableOverlayAndReleaseAreUsable()
    {
        using AlgorithmImageBuffer input = CreateInput(AlgorithmImageFormat.Gray8);
        AlgorithmResult result = await RunAsync(input, PointsWithOutlier(), new LineFitParameters());
        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureResources();
            ImageView view = new();
            view.SetImageSource(CreateBitmap(), enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                ImageProcessingContext context = imageView.EditorContext.ProcessingContext;
                string?[] menuIds = new LineFitContextMenu(context, imageView.EditorContext.DrawEditorContext)
                    .GetContextMenuItems().Select(item => item.GuidId).ToArray();
                string[] expectedMenuIds = ["LineFit"];
                Assert.Equal(expectedMenuIds, menuIds);
                int before = context.ImageShow.Visuals.Count;
                LineFitResultWindow window = new(result, context, imageView.EditorContext.DrawEditorContext);
                window.Show();
                Assert.Equal(before + 1, context.ImageShow.Visuals.Count);
                Assert.Single(context.AlgorithmOverlays.Snapshot());
                window.Close();
                Assert.Equal(before, context.ImageShow.Visuals.Count);
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

    private static async Task<AlgorithmResult> RunAsync(AlgorithmImageBuffer input, PolylineAlgorithmRoi roi, LineFitParameters parameters)
        => await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.LineFit, parameters, roi),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });

    private static PolylineAlgorithmRoi PointsWithOutlier()
    {
        List<AlgorithmPoint> points = new();
        double[] noise = [-0.08, 0.04, 0.09, -0.05, 0.02, -0.1, 0.06, 0.01, -0.03];
        for (int index = 0; index < noise.Length; index++)
        {
            double x = 10 + index * 10;
            points.Add(new AlgorithmPoint(x, 0.5 * x + 10 + noise[index]));
        }
        points.Add(new AlgorithmPoint(50, 85));
        return new PolylineAlgorithmRoi(points);
    }

    private static AlgorithmTableArtifact Points(AlgorithmResult result)
        => result.GetArtifact<AlgorithmTableArtifact>("line-fit-points")!;

    private static double Measurement(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmMeasurementArtifact>("line-fit-summary")!.Measurements.Single(value => value.Name == name).Value;

    private static AlgorithmImageBuffer CreateInput(AlgorithmImageFormat format)
    {
        int stride = Width * format.Channels() * format.BitsPerChannel() / 8;
        return new AlgorithmImageBuffer(Width, Height, stride, format, new byte[stride * Height]);
    }

    private static WriteableBitmap CreateBitmap()
        => new(Width, Height, 96, 96, PixelFormats.Gray8, null);

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

    private sealed class TestLoader : IBatchImageLoader
    {
        public IReadOnlyCollection<string> Extensions { get; } = [".fake"];

        public Mat Load(string filePath) => new(Height, Width, MatType.CV_8UC1, Scalar.Black);
    }

    private sealed class InlineProgress(Action<AlgorithmProgress> report) : IProgress<AlgorithmProgress>
    {
        public void Report(AlgorithmProgress value) => report(value);
    }
}
