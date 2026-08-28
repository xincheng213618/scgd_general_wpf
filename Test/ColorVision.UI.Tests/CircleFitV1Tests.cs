using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.CircleFit;
using OpenCvSharp;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class CircleFitV1Tests
{
    private const int Width = 120;
    private const int Height = 110;
    private const double CenterX = 52.25;
    private const double CenterY = 47.75;
    private const double Radius = 25.5;

    [Fact]
    public void CatalogDefaultsValidationJsonAliasesAndCopilotBoundaryAreStable()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        Assert.True(catalog.TryResolveAlias("FitCircle", out AlgorithmDescriptor? alias));
        AlgorithmDescriptor descriptor = Assert.Single(catalog.Descriptors, item => item.Id == StandardAlgorithmIds.CircleFit);
        Assert.Same(descriptor, alias);
        Assert.Equal(new AlgorithmVersion(1, 0, 0), descriptor.Version);
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Interactive));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Batch));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Flow));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Copilot));
        Assert.True(descriptor.SupportsPolylineRoi);
        Assert.Empty(descriptor.OutputFormats!);
        Assert.Equal("no-image-output", descriptor.OutputFormatPolicy);

        CircleFitParameters defaults = descriptor.ParameterSchema.Defaults.Deserialize<CircleFitParameters>(AlgorithmJson.Options)!;
        Assert.True(defaults.Validate().IsValid);
        string json = JsonSerializer.Serialize(defaults, AlgorithmJson.Options);
        Assert.True(JsonSerializer.Deserialize<CircleFitParameters>(json, AlgorithmJson.Options)!.Validate().IsValid);
        Assert.Contains(descriptor.ParameterSchema.Fields, field => field.Name == nameof(CircleFitParameters.MinimumAngularCoverageDegrees)
            && field.Minimum == 0 && field.Maximum == 360 && field.Unit == "degree");

        CircleFitParameters invalid = new()
        {
            Mode = (CircleFitMode)99,
            ResidualThresholdPixels = 0,
            HuberTuningConstant = 11,
            MaximumIterations = 0,
            ConvergenceTolerance = 1,
            MinimumInlierCount = 2,
            MinimumRadiusPixels = -1,
            MaximumRadiusPixels = -1,
            MinimumAngularCoverageDegrees = 361,
            MaximumPoints = 2,
            MaximumConsensusCandidates = 0,
            MaximumConsensusEvaluations = 2,
            MaximumOverlayPoints = 10_001,
        };
        Assert.Equal(13, invalid.Validate().Issues.Count);
        Assert.Contains(new CircleFitParameters { MinimumRadiusPixels = 10, MaximumRadiusPixels = 5 }.Validate().Issues,
            issue => issue.Code == "range_order");
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
        using AlgorithmResult result = await RunAsync(input, PointsWithOutlier(), new CircleFitParameters());

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.True(Measurement(result, "circle_fit.accepted") == 1,
            string.Join("; ", result.GetArtifact<AlgorithmMeasurementArtifact>("circle-fit-summary")!.Measurements.Select(item => $"{item.Name}={item.Value:R}")));
        Assert.Equal(12, Measurement(result, "circle_fit.inlier_count"));
        Assert.Equal(1, Measurement(result, "circle_fit.rejected_count"));
        Assert.InRange(Measurement(result, "circle_fit.center_x"), CenterX - 0.2, CenterX + 0.2);
        Assert.InRange(Measurement(result, "circle_fit.center_y"), CenterY - 0.2, CenterY + 0.2);
        Assert.InRange(Measurement(result, "circle_fit.radius"), Radius - 0.2, Radius + 0.2);
        Assert.InRange(Measurement(result, "circle_fit.rms_residual"), 0, 0.12);
        Assert.InRange(Measurement(result, "circle_fit.angular_coverage"), 329, 331);
        Assert.InRange(Measurement(result, "circle_fit.confidence"), 0.75, 1);
        IReadOnlyDictionary<string, JsonElement> outlier = Points(result).Rows.Single(row => row["PointIndex"].GetInt32() == 12);
        Assert.False(outlier["Accepted"].GetBoolean());
        Assert.Equal("residual_above_threshold", outlier["RejectionReason"].GetString());
        Assert.True(outlier["SignedRadialResidual"].GetDouble() > 0);
        AlgorithmGeometry circle = result.GetArtifact<AlgorithmGeometryArtifact>("circle-fit-geometry")!.Geometries.Single(item => item.Id == "fitted-circle");
        Assert.Equal(AlgorithmGeometryKind.Circle, circle.Kind);
        Assert.Equal(Measurement(result, "circle_fit.radius"), circle.Radius);
        Assert.Equal(Measurement(result, "circle_fit.rms_residual"), circle.Residual);
        Assert.Equal("colorvision.measurement.circle-fit/v1", result.GetArtifact<AlgorithmStructuredDataArtifact>("circle-fit-provenance")!.Schema);
        Assert.Equal(original, input.Data.ToArray());
    }

    [Fact]
    public async Task LeastSquaresPhysicalCoordinatesAndCoverageAreExplicit()
    {
        using AlgorithmImageBuffer input = new(Width, Height, Width, AlgorithmImageFormat.Gray8, new byte[Width * Height], 254, 254);
        AlgorithmPoint[] pixelPoints = CirclePoints(8, includeNoise: false);
        PolylineAlgorithmRoi physical = new(pixelPoints.Select(point => new AlgorithmPoint(point.X / 10, point.Y / 10)).ToArray())
        {
            CoordinateSpace = AlgorithmCoordinateSpace.Physical,
        };
        using AlgorithmResult result = await RunAsync(input, physical, new CircleFitParameters
        {
            Mode = CircleFitMode.LeastSquares,
            ResidualThresholdPixels = 0.01,
            MinimumAngularCoverageDegrees = 300,
        });

        Assert.Equal(1, Measurement(result, "circle_fit.accepted"));
        Assert.Equal(CenterX, Measurement(result, "circle_fit.center_x"), 8);
        Assert.Equal(CenterY, Measurement(result, "circle_fit.center_y"), 8);
        Assert.Equal(Radius, Measurement(result, "circle_fit.radius"), 8);
        Assert.InRange(Measurement(result, "circle_fit.angular_coverage"), 314.9, 315.1);
    }

    [Fact]
    public async Task DegenerateFiltersWrongRoiAndPointLimitHaveStructuredReasons()
    {
        using AlgorithmImageBuffer degenerateInput = CreateInput(AlgorithmImageFormat.Gray8);
        using AlgorithmResult degenerate = await RunAsync(degenerateInput, new PolylineAlgorithmRoi([new(1, 1), new(2, 2), new(3, 3), new(4, 4)]), new CircleFitParameters());
        Assert.Equal(0, Measurement(degenerate, "circle_fit.accepted"));
        Assert.All(Points(degenerate).Rows, row => Assert.Equal("degenerate_point_distribution", row["RejectionReason"].GetString()));

        using AlgorithmImageBuffer insufficientInput = CreateInput(AlgorithmImageFormat.Gray8);
        using AlgorithmResult insufficient = await RunAsync(insufficientInput, new PolylineAlgorithmRoi(CirclePoints(4, false)), new CircleFitParameters { MinimumInlierCount = 5 });
        Assert.Equal(0, Measurement(insufficient, "circle_fit.accepted"));
        Assert.All(Points(insufficient).Rows, row => Assert.Equal("insufficient_inliers", row["RejectionReason"].GetString()));

        await AssertGlobalRejection(new CircleFitParameters { MinimumRadiusPixels = 30 }, "radius_below_minimum");
        await AssertGlobalRejection(new CircleFitParameters { MaximumRadiusPixels = 20 }, "radius_above_maximum");
        await AssertGlobalRejection(new CircleFitParameters { MinimumAngularCoverageDegrees = 340 }, "angular_coverage_below_minimum");

        AlgorithmImageBuffer wrongRoiInput = CreateInput(AlgorithmImageFormat.Gray8);
        using AlgorithmResult wrongRoi = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.CircleFit, new CircleFitParameters(), new RectangleAlgorithmRoi(0, 0, 10, 10)),
            Inputs = [new AlgorithmInput { Name = "source", Image = wrongRoiInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        Assert.Contains(wrongRoi.Failures, failure => failure.Code == "roi_kind_unsupported");
        Assert.True(wrongRoiInput.IsDisposed);

        AlgorithmImageBuffer limitedInput = CreateInput(AlgorithmImageFormat.Gray8);
        using AlgorithmResult limited = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.CircleFit, new CircleFitParameters { MaximumPoints = 3 }, new PolylineAlgorithmRoi(CirclePoints(4, false))),
            Inputs = [new AlgorithmInput { Name = "source", Image = limitedInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });
        Assert.Equal("circle_fit_point_limit_exceeded", Assert.Single(limited.Failures).Code);
        Assert.True(limitedInput.IsDisposed);

        async Task AssertGlobalRejection(CircleFitParameters parameters, string reason)
        {
            using AlgorithmImageBuffer filterInput = CreateInput(AlgorithmImageFormat.Gray8);
            using AlgorithmResult result = await RunAsync(filterInput, new PolylineAlgorithmRoi(CirclePoints(12, true)), parameters);
            Assert.Equal(0, Measurement(result, "circle_fit.accepted"));
            Assert.All(Points(result).Rows, row => Assert.Equal(reason, row["RejectionReason"].GetString()));
        }
    }

    [Fact]
    public async Task CancellationAndEveryTerminalPathReleaseTransferredInput()
    {
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "circle-fit.solve") cancellation.Cancel();
        });
        AlgorithmImageBuffer cancelledInput = CreateInput(AlgorithmImageFormat.Gray8);
        using AlgorithmResult cancelled = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.CircleFit, new CircleFitParameters(), PointsWithOutlier()),
            Inputs = [new AlgorithmInput { Name = "source", Image = cancelledInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
            Progress = progress,
        }, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, cancelled.Status);
        Assert.True(cancelledInput.IsDisposed);

        AlgorithmImageBuffer successInput = CreateInput(AlgorithmImageFormat.Gray8);
        using AlgorithmResult success = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.CircleFit, new CircleFitParameters(), PointsWithOutlier()),
            Inputs = [new AlgorithmInput { Name = "source", Image = successInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, success.Status);
        Assert.True(successInput.IsDisposed);
    }

    [Fact]
    public async Task BatchAndFlowReuseInvocationAndStructuredArtifacts()
    {
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.CircleFit, new CircleFitParameters(), PointsWithOutlier());
        string directory = Path.Combine(Path.GetTempPath(), $"ColorVision-CircleFit-{Guid.NewGuid():N}");
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
            Assert.Contains("circle-fit-geometry", File.ReadAllText(Assert.Single(batch.Files[0].OutputPaths)), StringComparison.Ordinal);

            LocalFrameMetadata metadata = new() { Width = Width, Height = Height, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, Width * Height, 0);
            using LocalFlowFrameLease lease = frame.Acquire();
            using AlgorithmResult flow = await LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(ExperimentalAlgorithmTestRuntime.Runtime, lease, invocation);
            Assert.Equal(12, Measurement(flow, "circle_fit.inlier_count"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImageViewMenuTableCircleOverlayAndReleaseAreUsable()
    {
        using AlgorithmImageBuffer input = CreateInput(AlgorithmImageFormat.Gray8);
        AlgorithmResult result = await RunAsync(input, PointsWithOutlier(), new CircleFitParameters());
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
                string?[] menuIds = new CircleFitContextMenu(context, imageView.EditorContext.DrawEditorContext)
                    .GetContextMenuItems().Select(item => item.GuidId).ToArray();
                string[] expectedMenuIds = ["CircleFit"];
                Assert.Equal(expectedMenuIds, menuIds);
                int before = context.ImageShow.Visuals.Count;
                CircleFitResultWindow window = new(result, context, imageView.EditorContext.DrawEditorContext);
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

    private static async Task<AlgorithmResult> RunAsync(AlgorithmImageBuffer input, PolylineAlgorithmRoi roi, CircleFitParameters parameters)
        => await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.CircleFit, parameters, roi),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });

    private static PolylineAlgorithmRoi PointsWithOutlier()
    {
        List<AlgorithmPoint> points = CirclePoints(12, true).ToList();
        points.Add(new AlgorithmPoint(105, 100));
        return new PolylineAlgorithmRoi(points);
    }

    private static AlgorithmPoint[] CirclePoints(int count, bool includeNoise)
    {
        double[] noise = [0.04, -0.08, 0.03, 0.09, -0.02, -0.06, 0.08, -0.03, 0.01, -0.09, 0.05, -0.01];
        return Enumerable.Range(0, count).Select(index =>
        {
            double angle = 2 * Math.PI * index / count;
            double radius = Radius + (includeNoise ? noise[index % noise.Length] : 0);
            return new AlgorithmPoint(CenterX + radius * Math.Cos(angle), CenterY + radius * Math.Sin(angle));
        }).ToArray();
    }

    private static AlgorithmTableArtifact Points(AlgorithmResult result)
        => result.GetArtifact<AlgorithmTableArtifact>("circle-fit-points")!;

    private static double Measurement(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmMeasurementArtifact>("circle-fit-summary")!.Measurements.Single(value => value.Name == name).Value;

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
