using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.BlobAnalysis;
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

public sealed class BlobAnalysisV1Tests
{
    [Fact]
    public void CatalogContractParametersAliasesAndCopilotBoundaryAreStable()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        Assert.True(catalog.TryResolveAlias("ConnectedComponents", out AlgorithmDescriptor? alias));
        AlgorithmDescriptor descriptor = Assert.Single(catalog.Descriptors, item => item.Id == StandardAlgorithmIds.BlobComponents);
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

        BlobAnalysisParameters defaults = descriptor.ParameterSchema.Defaults.Deserialize<BlobAnalysisParameters>(AlgorithmJson.Options)!;
        Assert.True(defaults.Validate().IsValid);
        Assert.Equal(128, defaults.Threshold);
        Assert.Equal(BlobConnectivity.Eight, defaults.Connectivity);
        Assert.Equal(descriptor.ParameterSchema.Defaults.ToString(), AlgorithmJson.ToElement(defaults).ToString());

        BlobAnalysisParameters invalid = new()
        {
            Threshold = double.NaN,
            Connectivity = (BlobConnectivity)6,
            MinimumArea = 0,
            MaximumWidth = -1,
            MaximumCandidates = 0,
            MaximumOverlayComponents = 5001,
        };
        AlgorithmValidationResult validation = invalid.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(invalid.Threshold));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(invalid.Connectivity));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(invalid.MinimumArea));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(invalid.MaximumWidth));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(invalid.MaximumCandidates));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(invalid.MaximumOverlayComponents));
    }

    [Fact]
    public async Task GoldenComponentsReturnMeasurementsFilteringGeometryAndDoNotMutateInput()
    {
        byte[] pixels =
        [
            0, 0, 0, 0, 0, 0,
            0, 255, 255, 0, 255, 0,
            0, 255, 255, 0, 255, 0,
            0, 0, 0, 0, 255, 0,
            0, 0, 0, 0, 0, 0,
        ];
        using AlgorithmImageBuffer input = Buffer(pixels, 6, 5);
        byte[] original = input.Data.ToArray();
        using AlgorithmResult result = await RunAsync(input, new BlobAnalysisParameters { Threshold = 128, MinimumArea = 4 });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(2, Measurement(result, "blob.candidate_count"));
        Assert.Equal(1, Measurement(result, "blob.accepted_count"));
        Assert.Equal(1, Measurement(result, "blob.rejected_count"));
        Assert.Equal(7, Measurement(result, "blob.foreground_pixel_count"));
        Assert.Equal(original, input.Data.ToArray());

        AlgorithmTableArtifact table = Components(result);
        Assert.Equal(2, table.Rows.Count);
        IReadOnlyDictionary<string, JsonElement> accepted = Assert.Single(table.Rows, row => row["Accepted"].GetBoolean());
        Assert.Equal(4, accepted["Area"].GetInt32());
        Assert.Equal(1, accepted["Left"].GetInt32());
        Assert.Equal(1.5, accepted["CentroidX"].GetDouble(), 12);
        IReadOnlyDictionary<string, JsonElement> rejected = Assert.Single(table.Rows, row => !row["Accepted"].GetBoolean());
        Assert.Equal("area_below_minimum", rejected["FilterReason"].GetString());

        AlgorithmGeometryArtifact geometry = result.GetArtifact<AlgorithmGeometryArtifact>("blob-geometry")!;
        Assert.Equal(3, geometry.Geometries.Count);
        AlgorithmGeometry acceptedGeometry = geometry.Geometries.Single(item => item.Id == $"blob-{accepted["Label"].GetInt32()}");
        Assert.Null(acceptedGeometry.FilterReason);
        Assert.Equal(1, acceptedGeometry.Confidence);
        Assert.Equal(1.5, acceptedGeometry.Measurements!["centroidX"], 12);
        AlgorithmOverlayArtifact overlay = result.GetArtifact<AlgorithmOverlayArtifact>("blob-overlay")!;
        Assert.Equal(AlgorithmOverlayLifetime.Transient, overlay.Lifetime);
        Assert.Equal(2, overlay.Items.Count);
        Assert.Equal("colorvision.analysis.blob-components/v1", result.GetArtifact<AlgorithmStructuredDataArtifact>("blob-provenance")!.Schema);
    }

    [Fact]
    public async Task FourAndEightConnectivityHaveDeterministicDiagonalSemantics()
    {
        byte[] pixels = [255, 0, 0, 255];
        using AlgorithmImageBuffer fourInput = Buffer(pixels, 2, 2);
        using AlgorithmResult four = await RunAsync(fourInput, new BlobAnalysisParameters { Connectivity = BlobConnectivity.Four });
        Assert.Equal(2, Measurement(four, "blob.candidate_count"));

        using AlgorithmImageBuffer eightInput = Buffer(pixels, 2, 2);
        using AlgorithmResult eight = await RunAsync(eightInput, new BlobAnalysisParameters { Connectivity = BlobConnectivity.Eight });
        Assert.Equal(1, Measurement(eight, "blob.candidate_count"));
        Assert.Equal(2, Components(eight).Rows[0]["Area"].GetInt32());
    }

    [Fact]
    public async Task NominalThresholdPreservesGray8Gray16FloatAndColorSemantics()
    {
        using AlgorithmImageBuffer gray8 = new(2, 1, 2, AlgorithmImageFormat.Gray8, [127, 128]);
        using AlgorithmResult gray8Result = await RunAsync(gray8, new BlobAnalysisParameters());
        Assert.Equal(1, Measurement(gray8Result, "blob.foreground_pixel_count"));

        ushort[] words = [32767, 32896];
        using AlgorithmImageBuffer gray16 = new(2, 1, 4, AlgorithmImageFormat.Gray16, MemoryMarshal.AsBytes(words.AsSpan()).ToArray());
        using AlgorithmResult gray16Result = await RunAsync(gray16, new BlobAnalysisParameters());
        Assert.Equal(1, Measurement(gray16Result, "blob.foreground_pixel_count"));

        float[] floats = [0.49f, 128f / 255f, float.NaN];
        using AlgorithmImageBuffer grayFloat = new(3, 1, 12, AlgorithmImageFormat.Gray32Float, MemoryMarshal.AsBytes(floats.AsSpan()).ToArray());
        using AlgorithmResult floatResult = await RunAsync(grayFloat, new BlobAnalysisParameters());
        Assert.Equal(1, Measurement(floatResult, "blob.foreground_pixel_count"));
        Assert.Equal(1, Measurement(floatResult, "blob.invalid_pixel_count"));

        using AlgorithmImageBuffer color = new(2, 1, 6, AlgorithmImageFormat.Bgr24, [255, 0, 0, 255, 255, 255]);
        using AlgorithmResult colorResult = await RunAsync(color, new BlobAnalysisParameters());
        Assert.Equal(1, Measurement(colorResult, "blob.foreground_pixel_count"));

        using AlgorithmImageBuffer bgra = new(2, 1, 8, AlgorithmImageFormat.Bgra32, [0, 0, 0, 255, 255, 255, 255, 0]);
        using AlgorithmResult bgraResult = await RunAsync(bgra, new BlobAnalysisParameters());
        Assert.Equal(1, Measurement(bgraResult, "blob.foreground_pixel_count"));

        using AlgorithmImageBuffer darkInput = Buffer([0, 128, 255], 3, 1);
        using AlgorithmResult dark = await RunAsync(darkInput, new BlobAnalysisParameters
        {
            Threshold = 128,
            ForegroundPolarity = BlobForegroundPolarity.Dark,
        });
        Assert.Equal(2, Measurement(dark, "blob.foreground_pixel_count"));
    }

    [Fact]
    public async Task RectangleCirclePolygonRoiUseFullImageCoordinatesAndClipMask()
    {
        byte[] pixels = Enumerable.Repeat(byte.MaxValue, 25).ToArray();
        using AlgorithmImageBuffer rectangleInput = Buffer(pixels, 5, 5);
        using AlgorithmResult rectangle = await RunAsync(rectangleInput, new BlobAnalysisParameters { ExcludeImageBorder = true }, new RectangleAlgorithmRoi(1, 1, 2, 2));
        Assert.Equal(4, Measurement(rectangle, "blob.foreground_pixel_count"));
        Assert.Equal(1, Components(rectangle).Rows[0]["Left"].GetInt32());
        Assert.Equal(1, Components(rectangle).Rows[0]["Top"].GetInt32());
        Assert.True(Components(rectangle).Rows[0]["Accepted"].GetBoolean());

        using AlgorithmImageBuffer circleInput = Buffer(pixels, 5, 5);
        using AlgorithmResult circle = await RunAsync(circleInput, new BlobAnalysisParameters(), new CircleAlgorithmRoi(new AlgorithmPoint(2, 2), 1));
        Assert.Equal(5, Measurement(circle, "blob.foreground_pixel_count"));

        using AlgorithmImageBuffer polygonInput = Buffer(pixels, 5, 5);
        using AlgorithmResult polygon = await RunAsync(polygonInput, new BlobAnalysisParameters(), new PolygonAlgorithmRoi([new(0, 0), new(2, 0), new(0, 2)]));
        Assert.Equal(6, Measurement(polygon, "blob.foreground_pixel_count"));
        Assert.Equal(AlgorithmGeometryKind.Polygon, resultRoi(polygon).Kind);

        using AlgorithmImageBuffer physicalInput = new(5, 5, 5, AlgorithmImageFormat.Gray8, pixels.ToArray(), 254, 254);
        RectangleAlgorithmRoi physicalRoi = new(0.1, 0.1, 0.2, 0.2) { CoordinateSpace = AlgorithmCoordinateSpace.Physical };
        using AlgorithmResult physical = await RunAsync(physicalInput, new BlobAnalysisParameters(), physicalRoi);
        Assert.Equal(4, Measurement(physical, "blob.foreground_pixel_count"));
        Assert.Equal(1, Components(physical).Rows[0]["Left"].GetInt32());

        static AlgorithmGeometry resultRoi(AlgorithmResult result)
            => result.GetArtifact<AlgorithmGeometryArtifact>("blob-geometry")!.Geometries.Single(item => item.Id == "roi");
    }

    [Fact]
    public async Task FilteringReasonsBorderPolicyOverlayLimitAndCandidateLimitAreStructured()
    {
        byte[] pixels =
        [
            255, 0, 255, 0, 255,
            0, 0, 0, 0, 0,
            0, 255, 255, 0, 0,
        ];
        using AlgorithmImageBuffer filteredInput = Buffer(pixels, 5, 3);
        using AlgorithmResult filtered = await RunAsync(filteredInput, new BlobAnalysisParameters
        {
            MinimumArea = 2,
            ExcludeImageBorder = true,
            MaximumOverlayComponents = 0,
        });
        Assert.Equal(4, Measurement(filtered, "blob.candidate_count"));
        Assert.All(Components(filtered).Rows, row => Assert.False(row["Accepted"].GetBoolean()));
        Assert.Contains(Components(filtered).Rows, row => row["FilterReason"].GetString()!.Contains("area_below_minimum", StringComparison.Ordinal));
        Assert.Contains(Components(filtered).Rows, row => row["FilterReason"].GetString()!.Contains("touches_image_border", StringComparison.Ordinal));
        Assert.Single(filtered.GetArtifact<AlgorithmOverlayArtifact>("blob-overlay")!.Items);

        using AlgorithmImageBuffer limitedInput = Buffer([255, 0, 255, 0, 255], 5, 1);
        using AlgorithmResult limited = await RunAsync(limitedInput, new BlobAnalysisParameters { Connectivity = BlobConnectivity.Four, MaximumCandidates = 2 });
        Assert.Equal(AlgorithmResultStatus.Failed, limited.Status);
        AlgorithmFailure failure = Assert.Single(limited.Failures);
        Assert.Equal("component_limit_exceeded", failure.Code);
        Assert.Equal("3", failure.Details!["detected"]);
    }

    [Fact]
    public async Task CancellationIsStructuredAndReleasesTransferredInput()
    {
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "blob.mask") cancellation.Cancel();
        });
        AlgorithmImageBuffer input = Buffer(new byte[4096 * 64], 4096, 64);
        using AlgorithmResult result = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.BlobComponents, new BlobAnalysisParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            Progress = progress,
        }, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, result.Status);
        Assert.True(input.IsDisposed);
    }

    [Fact]
    public async Task TransferredInputsAreReleasedAfterSuccessAndStructuredFailure()
    {
        AlgorithmImageBuffer successfulInput = Buffer([0, 255], 2, 1);
        using AlgorithmResult successful = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.BlobComponents, new BlobAnalysisParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = successfulInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, successful.Status);
        Assert.True(successfulInput.IsDisposed);

        AlgorithmImageBuffer failedInput = Buffer([255, 0, 255], 3, 1);
        using AlgorithmResult failed = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.BlobComponents, new BlobAnalysisParameters
            {
                Connectivity = BlobConnectivity.Four,
                MaximumCandidates = 1,
            }),
            Inputs = [new AlgorithmInput { Name = "source", Image = failedInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        Assert.Equal(AlgorithmResultStatus.Failed, failed.Status);
        Assert.True(failedInput.IsDisposed);
    }

    [Fact]
    public async Task BatchAndFlowReuseTheSameInvocationAndStructuredArtifacts()
    {
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.BlobComponents, new BlobAnalysisParameters { MinimumArea = 2 });
        string directory = Path.Combine(Path.GetTempPath(), $"ColorVision-Blob-{Guid.NewGuid():N}");
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
            Assert.Contains("blob-components", File.ReadAllText(Assert.Single(batch.Files[0].OutputPaths)), StringComparison.Ordinal);

            LocalFrameMetadata metadata = new() { Width = 4, Height = 1, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, 4, 0);
            using LocalFlowFrameLease lease = frame.Acquire();
            Marshal.Copy(new byte[] { 255, 255, 0, 255 }, 0, lease.RawPointer, 4);
            using AlgorithmResult flow = await LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(ExperimentalAlgorithmTestRuntime.Runtime, lease, invocation);
            Assert.Equal(2, Measurement(flow, "blob.candidate_count"));
            Assert.Equal(1, Measurement(flow, "blob.accepted_count"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImageViewMenuResultTableAndVisualOverlayAreUsableAndReleased()
    {
        using AlgorithmImageBuffer input = Buffer([255, 255, 0, 0, 255, 255], 3, 2);
        AlgorithmResult result = await RunAsync(input, new BlobAnalysisParameters());
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
                string?[] menuIds = new BlobAnalysisContextMenu(context, imageView.EditorContext.DrawEditorContext)
                    .GetContextMenuItems().Select(item => item.GuidId).ToArray();
                Assert.Equal(new[] { "BlobAnalysis", "BlobAnalysisWholeImage", "BlobAnalysisRectangle", "BlobAnalysisCircle", "BlobAnalysisPolygon" }, menuIds);
                int before = context.ImageShow.Visuals.Count;
                BlobAnalysisResultWindow window = new(result, context, imageView.EditorContext.DrawEditorContext);
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

    private static async Task<AlgorithmResult> RunAsync(AlgorithmImageBuffer input, BlobAnalysisParameters parameters, AlgorithmRoi? roi = null)
        => await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.BlobComponents, parameters, roi),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

    private static AlgorithmTableArtifact Components(AlgorithmResult result)
        => result.GetArtifact<AlgorithmTableArtifact>("blob-components")!;

    private static double Measurement(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmMeasurementArtifact>("blob-summary")!.Measurements.Single(value => value.Name == name).Value;

    private static AlgorithmImageBuffer Buffer(byte[] pixels, int width, int height)
        => new(width, height, width, AlgorithmImageFormat.Gray8, pixels.ToArray());

    private static WriteableBitmap CreateBitmap()
    {
        WriteableBitmap bitmap = new(3, 2, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, 3, 2), new byte[] { 255, 255, 0, 0, 255, 255 }, 3, 0);
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
            Mat mat = new(1, 4, MatType.CV_8UC1);
            Marshal.Copy(new byte[] { 255, 255, 0, 255 }, 0, mat.Data, 4);
            return mat;
        }
    }

    private sealed class InlineProgress(Action<AlgorithmProgress> report) : IProgress<AlgorithmProgress>
    {
        public void Report(AlgorithmProgress value) => report(value);
    }
}
