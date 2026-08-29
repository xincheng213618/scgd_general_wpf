using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ContourAnalysis;
using OpenCvSharp;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ContourAnalysisV1Tests
{
    [Fact]
    public void CatalogContractDefaultsValidationAliasesAndCopilotBoundaryAreStable()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        Assert.True(catalog.TryResolveAlias("FindContours", out AlgorithmDescriptor? alias));
        AlgorithmDescriptor descriptor = Assert.Single(catalog.Descriptors, item => item.Id == StandardAlgorithmIds.Contours);
        Assert.Same(descriptor, alias);
        Assert.Equal(new AlgorithmVersion(1, 0, 0), descriptor.Version);
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Interactive));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Batch));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Flow));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Copilot));
        Assert.True(descriptor.SupportsRectangleRoi && descriptor.SupportsCircleRoi && descriptor.SupportsPolygonRoi);
        Assert.Empty(descriptor.OutputFormats!);
        Assert.Equal("no-image-output", descriptor.OutputFormatPolicy);
        Assert.Null(descriptor.Presentation);

        ContourAnalysisParameters defaults = descriptor.ParameterSchema.Defaults.Deserialize<ContourAnalysisParameters>(AlgorithmJson.Options)!;
        Assert.True(defaults.Validate().IsValid);
        Assert.Equal(128, defaults.Threshold);
        Assert.Equal(ContourRetrievalMode.External, defaults.RetrievalMode);
        Assert.Equal(ContourApproximationMode.Simple, defaults.ApproximationMode);
        Assert.Equal(descriptor.ParameterSchema.Defaults.ToString(), AlgorithmJson.ToElement(defaults).ToString());

        ContourAnalysisParameters invalid = new()
        {
            Threshold = double.NaN,
            ForegroundPolarity = (ContourForegroundPolarity)5,
            RetrievalMode = (ContourRetrievalMode)5,
            ApproximationMode = (ContourApproximationMode)5,
            SimplificationEpsilon = -1,
            MinimumArea = -1,
            MaximumPerimeter = -1,
            MinimumPointCount = 0,
            MinimumCircularity = 2,
            MinimumSolidity = -1,
            MaximumCandidates = 0,
            MaximumTotalPoints = 0,
            MaximumOverlayContours = 5001,
        };
        AlgorithmValidationResult validation = invalid.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(invalid.Threshold));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(invalid.RetrievalMode));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(invalid.ApproximationMode));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(invalid.MaximumTotalPoints));
    }

    [Fact]
    public async Task GoldenRectangleReturnsGeometryMeasurementsFilteringAndReadOnlyInput()
    {
        byte[] pixels = new byte[7 * 6];
        for (int y = 1; y <= 3; y++)
            for (int x = 2; x <= 4; x++)
                pixels[y * 7 + x] = byte.MaxValue;
        using AlgorithmImageBuffer input = Buffer(pixels, 7, 6);
        byte[] original = input.Data.ToArray();
        using AlgorithmResult result = await RunAsync(input, new ContourAnalysisParameters());

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(1, Measurement(result, "contour.candidate_count"));
        Assert.Equal(1, Measurement(result, "contour.accepted_count"));
        Assert.Equal(9, Measurement(result, "contour.foreground_pixel_count"));
        Assert.Equal(original, input.Data.ToArray());
        IReadOnlyDictionary<string, JsonElement> row = Assert.Single(Contours(result).Rows);
        Assert.True(row["Accepted"].GetBoolean());
        Assert.Equal(4, row["Area"].GetDouble(), 12);
        Assert.Equal(8, row["Perimeter"].GetDouble(), 12);
        Assert.Equal(4, row["PointCount"].GetInt32());
        Assert.Equal(2, row["Left"].GetInt32());
        Assert.Equal(1, row["Top"].GetInt32());
        Assert.Equal(3, row["CentroidX"].GetDouble(), 12);
        Assert.Equal(2, row["CentroidY"].GetDouble(), 12);
        Assert.Equal(1, row["Solidity"].GetDouble(), 12);

        AlgorithmGeometryArtifact geometry = result.GetArtifact<AlgorithmGeometryArtifact>("contour-geometry")!;
        AlgorithmGeometry contour = geometry.Geometries.Single(item => item.Id == "contour-0");
        Assert.Equal(AlgorithmGeometryKind.Polygon, contour.Kind);
        Assert.Equal(4, contour.Points.Count);
        Assert.Equal(1, contour.Confidence);
        Assert.Null(contour.FilterReason);
        Assert.Equal(4, contour.Measurements!["area"], 12);
        Assert.Equal(2, result.GetArtifact<AlgorithmOverlayArtifact>("contour-overlay")!.Items.Count);
        Assert.Equal("colorvision.analysis.contours/v1", result.GetArtifact<AlgorithmStructuredDataArtifact>("contour-provenance")!.Schema);
    }

    [Fact]
    public async Task RetrievalHierarchyAndApproximationHaveDeterministicSemantics()
    {
        byte[] ring = new byte[7 * 7];
        for (int y = 1; y <= 5; y++)
            for (int x = 1; x <= 5; x++)
                ring[y * 7 + x] = byte.MaxValue;
        for (int y = 2; y <= 4; y++)
            for (int x = 2; x <= 4; x++)
                ring[y * 7 + x] = 0;

        using AlgorithmImageBuffer externalInput = Buffer(ring, 7, 7);
        using AlgorithmResult external = await RunAsync(externalInput, new ContourAnalysisParameters { RetrievalMode = ContourRetrievalMode.External });
        Assert.Equal(1, Measurement(external, "contour.candidate_count"));

        using AlgorithmImageBuffer treeInput = Buffer(ring, 7, 7);
        using AlgorithmResult tree = await RunAsync(treeInput, new ContourAnalysisParameters { RetrievalMode = ContourRetrievalMode.Tree });
        Assert.Equal(2, Measurement(tree, "contour.candidate_count"));
        Assert.Contains(Contours(tree).Rows, row => row["Parent"].GetInt32() >= 0);
        Assert.Contains(Contours(tree).Rows, row => row["Child"].GetInt32() >= 0);

        using AlgorithmImageBuffer listInput = Buffer(ring, 7, 7);
        using AlgorithmResult list = await RunAsync(listInput, new ContourAnalysisParameters { RetrievalMode = ContourRetrievalMode.List });
        Assert.Equal(2, Measurement(list, "contour.candidate_count"));
        Assert.All(Contours(list).Rows, row => Assert.Equal(-1, row["Parent"].GetInt32()));

        byte[] rectangle = new byte[8 * 8];
        for (int y = 1; y <= 6; y++)
            for (int x = 1; x <= 6; x++)
                rectangle[y * 8 + x] = byte.MaxValue;
        using AlgorithmImageBuffer noneInput = Buffer(rectangle, 8, 8);
        using AlgorithmResult none = await RunAsync(noneInput, new ContourAnalysisParameters { ApproximationMode = ContourApproximationMode.None });
        using AlgorithmImageBuffer simpleInput = Buffer(rectangle, 8, 8);
        using AlgorithmResult simple = await RunAsync(simpleInput, new ContourAnalysisParameters { ApproximationMode = ContourApproximationMode.Simple });
        using AlgorithmImageBuffer simplifiedInput = Buffer(rectangle, 8, 8);
        using AlgorithmResult simplified = await RunAsync(simplifiedInput, new ContourAnalysisParameters
        {
            ApproximationMode = ContourApproximationMode.None,
            SimplificationEpsilon = 0.5,
        });
        Assert.True(Contours(none).Rows[0]["PointCount"].GetInt32() > Contours(simple).Rows[0]["PointCount"].GetInt32());
        Assert.Equal(4, Contours(simple).Rows[0]["PointCount"].GetInt32());
        Assert.Equal(4, Contours(simplified).Rows[0]["PointCount"].GetInt32());
    }

    [Fact]
    public async Task FormatNormalizationAndRoiUseSharedNominalIntensityAndFullImageCoordinates()
    {
        ushort[] words = [0, 0, 0, 0, 0, 0, 0, 65535, 65535, 0, 0, 65535, 65535, 0, 0, 0, 0, 0];
        using AlgorithmImageBuffer gray16 = new(6, 3, 12, AlgorithmImageFormat.Gray16, MemoryMarshal.AsBytes(words.AsSpan()).ToArray());
        using AlgorithmResult gray16Result = await RunAsync(gray16, new ContourAnalysisParameters());
        Assert.Equal(4, Measurement(gray16Result, "contour.foreground_pixel_count"));

        float[] floats = [float.NaN, 0, 0, 0, 1, 1, 0, 1, 1];
        using AlgorithmImageBuffer grayFloat = new(3, 3, 12, AlgorithmImageFormat.Gray32Float, MemoryMarshal.AsBytes(floats.AsSpan()).ToArray());
        using AlgorithmResult floatResult = await RunAsync(grayFloat, new ContourAnalysisParameters());
        Assert.Equal(4, Measurement(floatResult, "contour.foreground_pixel_count"));
        Assert.Equal(1, Measurement(floatResult, "contour.invalid_pixel_count"));

        byte[] white = Enumerable.Repeat(byte.MaxValue, 25).ToArray();
        using AlgorithmImageBuffer roiInput = Buffer(white, 5, 5);
        using AlgorithmResult roi = await RunAsync(roiInput, new ContourAnalysisParameters(), new RectangleAlgorithmRoi(1, 1, 3, 3));
        IReadOnlyDictionary<string, JsonElement> row = Assert.Single(Contours(roi).Rows);
        Assert.Equal(1, row["Left"].GetInt32());
        Assert.Equal(1, row["Top"].GetInt32());
        Assert.False(row["TouchesImageBorder"].GetBoolean());
        Assert.Equal(AlgorithmGeometryKind.Rectangle, roi.GetArtifact<AlgorithmGeometryArtifact>("contour-geometry")!.Geometries.Single(item => item.Id == "roi").Kind);

        using AlgorithmImageBuffer circleInput = Buffer(white, 5, 5);
        using AlgorithmResult circle = await RunAsync(circleInput, new ContourAnalysisParameters { MinimumArea = 0 }, new CircleAlgorithmRoi(new AlgorithmPoint(2, 2), 1));
        Assert.Equal(5, Measurement(circle, "contour.foreground_pixel_count"));
        Assert.Equal(AlgorithmGeometryKind.Circle, circle.GetArtifact<AlgorithmGeometryArtifact>("contour-geometry")!.Geometries.Single(item => item.Id == "roi").Kind);

        using AlgorithmImageBuffer polygonInput = Buffer(white, 5, 5);
        using AlgorithmResult polygon = await RunAsync(polygonInput, new ContourAnalysisParameters { MinimumArea = 0 }, new PolygonAlgorithmRoi([new(0, 0), new(2, 0), new(0, 2)]));
        Assert.Equal(6, Measurement(polygon, "contour.foreground_pixel_count"));
        Assert.Equal(AlgorithmGeometryKind.Polygon, polygon.GetArtifact<AlgorithmGeometryArtifact>("contour-geometry")!.Geometries.Single(item => item.Id == "roi").Kind);

        using AlgorithmImageBuffer physicalInput = new(5, 5, 5, AlgorithmImageFormat.Gray8, white.ToArray(), 254, 254);
        using AlgorithmResult physical = await RunAsync(physicalInput, new ContourAnalysisParameters(), new RectangleAlgorithmRoi(0.1, 0.1, 0.3, 0.3)
        {
            CoordinateSpace = AlgorithmCoordinateSpace.Physical,
        });
        Assert.Equal(1, Contours(physical).Rows[0]["Left"].GetInt32());
        Assert.Equal(1, Contours(physical).Rows[0]["Top"].GetInt32());
    }

    [Fact]
    public async Task FiltersOverlayAndCandidateAndPointLimitsAreStructured()
    {
        byte[] pixels = new byte[8 * 5];
        pixels[0] = byte.MaxValue;
        for (int y = 1; y <= 3; y++)
            for (int x = 3; x <= 5; x++)
                pixels[y * 8 + x] = byte.MaxValue;
        using AlgorithmImageBuffer filteredInput = Buffer(pixels, 8, 5);
        using AlgorithmResult filtered = await RunAsync(filteredInput, new ContourAnalysisParameters
        {
            MinimumArea = 2,
            ExcludeImageBorder = true,
            MaximumOverlayContours = 0,
        });
        Assert.Equal(2, Measurement(filtered, "contour.candidate_count"));
        Assert.Contains(Contours(filtered).Rows, row => row["FilterReason"].GetString()?.Contains("area_below_minimum", StringComparison.Ordinal) == true);
        Assert.Contains(Contours(filtered).Rows, row => row["FilterReason"].GetString()?.Contains("touches_image_border", StringComparison.Ordinal) == true);
        Assert.Single(filtered.GetArtifact<AlgorithmOverlayArtifact>("contour-overlay")!.Items);

        using AlgorithmImageBuffer metricFilterInput = Buffer(pixels, 8, 5);
        using AlgorithmResult metricFiltered = await RunAsync(metricFilterInput, new ContourAnalysisParameters
        {
            MinimumArea = 0,
            MaximumPerimeter = 7,
            MaximumPointCount = 3,
            MinimumCircularity = 0.9,
        });
        string metricReasons = Contours(metricFiltered).Rows.Single(row => row["Left"].GetInt32() == 3)["FilterReason"].GetString()!;
        Assert.Contains("perimeter_above_maximum", metricReasons, StringComparison.Ordinal);
        Assert.Contains("point_count_above_maximum", metricReasons, StringComparison.Ordinal);
        Assert.Contains("circularity_below_minimum", metricReasons, StringComparison.Ordinal);

        using AlgorithmImageBuffer solidityInput = Buffer([
            0, 255, 0, 0, 0,
            0, 255, 0, 0, 0,
            0, 255, 255, 255, 0,
            0, 0, 0, 0, 0,
        ], 5, 4);
        using AlgorithmResult solidityFiltered = await RunAsync(solidityInput, new ContourAnalysisParameters { MinimumArea = 0, MinimumSolidity = 0.9 });
        Assert.Contains("solidity_below_minimum", Contours(solidityFiltered).Rows[0]["FilterReason"].GetString(), StringComparison.Ordinal);

        using AlgorithmImageBuffer candidateInput = Buffer([255, 0, 255, 0, 255], 5, 1);
        using AlgorithmResult candidateLimit = await RunAsync(candidateInput, new ContourAnalysisParameters { MinimumArea = 0, MaximumCandidates = 2 });
        Assert.Equal(AlgorithmResultStatus.Failed, candidateLimit.Status);
        Assert.Equal("contour_limit_exceeded", Assert.Single(candidateLimit.Failures).Code);

        byte[] block = new byte[6 * 6];
        for (int y = 1; y <= 4; y++)
            for (int x = 1; x <= 4; x++)
                block[y * 6 + x] = byte.MaxValue;
        using AlgorithmImageBuffer pointInput = Buffer(block, 6, 6);
        using AlgorithmResult pointLimit = await RunAsync(pointInput, new ContourAnalysisParameters
        {
            ApproximationMode = ContourApproximationMode.None,
            MaximumTotalPoints = 3,
        });
        Assert.Equal(AlgorithmResultStatus.Failed, pointLimit.Status);
        Assert.Equal("contour_point_limit_exceeded", Assert.Single(pointLimit.Failures).Code);
    }

    [Fact]
    public async Task CancellationAndEveryTerminalPathReleaseTransferredInput()
    {
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "contour.mask") cancellation.Cancel();
        });
        AlgorithmImageBuffer cancelledInput = Buffer(new byte[4096 * 64], 4096, 64);
        using AlgorithmResult cancelled = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Contours, new ContourAnalysisParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = cancelledInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            Progress = progress,
        }, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, cancelled.Status);
        Assert.True(cancelledInput.IsDisposed);

        AlgorithmImageBuffer successfulInput = Buffer(new byte[9], 3, 3);
        using AlgorithmResult successful = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Contours, new ContourAnalysisParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = successfulInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, successful.Status);
        Assert.True(successfulInput.IsDisposed);

        AlgorithmImageBuffer failedInput = Buffer([255, 0, 255], 3, 1);
        using AlgorithmResult failed = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Contours, new ContourAnalysisParameters { MinimumArea = 0, MaximumCandidates = 1 }),
            Inputs = [new AlgorithmInput { Name = "source", Image = failedInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        Assert.Equal(AlgorithmResultStatus.Failed, failed.Status);
        Assert.True(failedInput.IsDisposed);
    }

    [Fact]
    public async Task BatchAndFlowReuseTheSameInvocationAndStructuredArtifacts()
    {
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Contours, new ContourAnalysisParameters { MinimumArea = 0 });
        string directory = Path.Combine(Path.GetTempPath(), $"ColorVision-Contours-{Guid.NewGuid():N}");
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
            Assert.Contains("contour-geometry", File.ReadAllText(Assert.Single(batch.Files[0].OutputPaths)), StringComparison.Ordinal);

            LocalFrameMetadata metadata = new() { Width = 4, Height = 3, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, 12, 0);
            using LocalFlowFrameLease lease = frame.Acquire();
            Marshal.Copy(new byte[] { 0, 0, 0, 0, 0, 255, 255, 0, 0, 255, 255, 0 }, 0, lease.RawPointer, 12);
            using AlgorithmResult flow = await LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(ExperimentalAlgorithmTestRuntime.Runtime, lease, invocation);
            Assert.Equal(1, Measurement(flow, "contour.candidate_count"));
            Assert.Equal(1, Measurement(flow, "contour.accepted_count"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImageViewMenuResultTableAndVisualOverlayAreUsableAndReleased()
    {
        using AlgorithmImageBuffer input = Buffer([0, 0, 0, 0, 255, 255, 0, 255, 255], 3, 3);
        AlgorithmResult result = await RunAsync(input, new ContourAnalysisParameters());
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
                string?[] menuIds = new ContourAnalysisContextMenu(context, imageView.EditorContext.DrawEditorContext)
                    .GetContextMenuItems().Select(item => item.GuidId).ToArray();
                string[] expectedMenuIds = ["ContourAnalysis", "ContourAnalysisWholeImage", "ContourAnalysisRectangle", "ContourAnalysisCircle", "ContourAnalysisPolygon"];
                Assert.Equal(expectedMenuIds, menuIds);
                int before = context.ImageShow.Visuals.Count;
                ContourAnalysisResultWindow window = new(result, context, imageView.EditorContext.DrawEditorContext);
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

    private static async Task<AlgorithmResult> RunAsync(AlgorithmImageBuffer input, ContourAnalysisParameters parameters, AlgorithmRoi? roi = null)
        => await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Contours, parameters, roi),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

    private static AlgorithmTableArtifact Contours(AlgorithmResult result)
        => result.GetArtifact<AlgorithmTableArtifact>("contours")!;

    private static double Measurement(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmMeasurementArtifact>("contour-summary")!.Measurements.Single(value => value.Name == name).Value;

    private static AlgorithmImageBuffer Buffer(byte[] pixels, int width, int height)
        => new(width, height, width, AlgorithmImageFormat.Gray8, pixels.ToArray());

    private static WriteableBitmap CreateBitmap()
    {
        WriteableBitmap bitmap = new(3, 3, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, 3, 3), new byte[] { 0, 0, 0, 0, 255, 255, 0, 255, 255 }, 3, 0);
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

    private sealed class TestLoader : IBatchImageLoader
    {
        public IReadOnlyCollection<string> Extensions { get; } = [".fake"];

        public Mat Load(string filePath)
        {
            Mat mat = new(3, 4, MatType.CV_8UC1);
            Marshal.Copy(new byte[] { 0, 0, 0, 0, 0, 255, 255, 0, 0, 255, 255, 0 }, 0, mat.Data, 12);
            return mat;
        }
    }

    private sealed class InlineProgress(Action<AlgorithmProgress> report) : IProgress<AlgorithmProgress>
    {
        public void Report(AlgorithmProgress value) => report(value);
    }
}
