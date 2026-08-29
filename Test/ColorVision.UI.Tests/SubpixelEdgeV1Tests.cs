using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SubpixelEdge;
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

public sealed class SubpixelEdgeV1Tests
{
    private const int Width = 64;
    private const int Height = 5;
    private const double ExpectedEdge = 31.35;

    [Fact]
    public void CatalogParametersAliasesAndCopilotBoundaryAreStable()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        Assert.True(catalog.TryResolveAlias("CaliperEdge", out AlgorithmDescriptor? alias));
        AlgorithmDescriptor descriptor = Assert.Single(catalog.Descriptors, item => item.Id == StandardAlgorithmIds.SubpixelEdge);
        Assert.Same(descriptor, alias);
        Assert.Equal(new AlgorithmVersion(1, 0, 0), descriptor.Version);
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Interactive));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Batch));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Flow));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Copilot));
        Assert.True(descriptor.SupportsPolylineRoi);
        Assert.False(descriptor.SupportsRectangleRoi || descriptor.SupportsCircleRoi || descriptor.SupportsPolygonRoi);
        Assert.Empty(descriptor.OutputFormats!);
        Assert.Equal("no-image-output", descriptor.OutputFormatPolicy);
        Assert.Null(descriptor.Presentation);

        SubpixelEdgeParameters defaults = descriptor.ParameterSchema.Defaults.Deserialize<SubpixelEdgeParameters>(AlgorithmJson.Options)!;
        Assert.True(defaults.Validate().IsValid);
        string json = JsonSerializer.Serialize(defaults, AlgorithmJson.Options);
        Assert.True(JsonSerializer.Deserialize<SubpixelEdgeParameters>(json, AlgorithmJson.Options)!.Validate().IsValid);
        Assert.Contains(descriptor.ParameterSchema.Fields, field => field.Name == nameof(SubpixelEdgeParameters.MinimumGradient)
            && field.Minimum == 0 && field.Maximum == 255 && field.Unit == "nominal-8bit-DN/px");

        SubpixelEdgeParameters invalid = new()
        {
            Polarity = (SubpixelEdgePolarity)99,
            BoundaryMode = (SubpixelEdgeBoundaryMode)99,
            SampleSpacingPixels = 0,
            NormalAveragingRadiusPixels = 33,
            SmoothingSigmaPixels = 11,
            MinimumGradient = 256,
            MaximumCalipers = 0,
            MaximumSamplesPerCaliper = 5,
            MaximumTotalSamples = 5,
            MaximumOverlayCalipers = 5001,
        };
        Assert.Equal(10, invalid.Validate().Issues.Count);
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
    public async Task LogisticEdgeGoldenIsFormatInvariantAndInputIsReadOnly(AlgorithmImageFormat format)
    {
        using AlgorithmImageBuffer input = CreateLogistic(format);
        byte[] original = input.Data.ToArray();
        using AlgorithmResult result = await RunAsync(input, Calipers(new(4, 2), new(58, 2)), new SubpixelEdgeParameters());

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        IReadOnlyDictionary<string, JsonElement> row = Assert.Single(Edges(result).Rows);
        Assert.True(row["Accepted"].GetBoolean());
        Assert.InRange(row["EdgeX"].GetDouble(), ExpectedEdge - 0.18, ExpectedEdge + 0.18);
        Assert.InRange(row["EdgeY"].GetDouble(), 1.999999, 2.000001);
        Assert.Equal("Rising", row["DetectedPolarity"].GetString());
        Assert.True(row["Confidence"].GetDouble() is > 0 and <= 1);
        Assert.True(row["LocalizationUncertainty"].GetDouble() > 0);
        Assert.Equal(original, input.Data.ToArray());

        AlgorithmGeometry edge = result.GetArtifact<AlgorithmGeometryArtifact>("subpixel-edge-geometry")!
            .Geometries.Single(item => item.Id == "edge-0");
        Assert.Equal(AlgorithmGeometryKind.Point, edge.Kind);
        Assert.Equal(row["EdgeX"].GetDouble(), edge.Points[0].X, 12);
        Assert.Equal(row["Confidence"].GetDouble(), edge.Confidence);
        Assert.Equal(row["LocalizationUncertainty"].GetDouble(), edge.Residual);
        Assert.Equal("colorvision.measurement.subpixel-edge/v1", result.GetArtifact<AlgorithmStructuredDataArtifact>("subpixel-edge-provenance")!.Schema);
    }

    [Fact]
    public async Task DirectedPolarityMultipleCalipersAndPhysicalCoordinatesAreExplicit()
    {
        using AlgorithmImageBuffer risingInput = CreateLogistic(AlgorithmImageFormat.Gray8);
        using AlgorithmResult rising = await RunAsync(risingInput, Calipers(new(4, 1), new(58, 1)), new SubpixelEdgeParameters { Polarity = SubpixelEdgePolarity.Rising });
        Assert.True(Edges(rising).Rows[0]["Accepted"].GetBoolean());

        using AlgorithmImageBuffer wrongInput = CreateLogistic(AlgorithmImageFormat.Gray8);
        using AlgorithmResult wrong = await RunAsync(wrongInput, Calipers(new(4, 1), new(58, 1)), new SubpixelEdgeParameters { Polarity = SubpixelEdgePolarity.Falling });
        Assert.False(Edges(wrong).Rows[0]["Accepted"].GetBoolean());
        Assert.Equal("gradient_below_minimum", Edges(wrong).Rows[0]["RejectionReason"].GetString());

        using AlgorithmImageBuffer reverseInput = CreateLogistic(AlgorithmImageFormat.Gray8);
        using AlgorithmResult reverse = await RunAsync(reverseInput, Calipers(new(58, 1), new(4, 1)), new SubpixelEdgeParameters { Polarity = SubpixelEdgePolarity.Falling });
        Assert.True(Edges(reverse).Rows[0]["Accepted"].GetBoolean());
        Assert.InRange(Edges(reverse).Rows[0]["EdgeX"].GetDouble(), ExpectedEdge - 0.18, ExpectedEdge + 0.18);
        Assert.Equal("Falling", Edges(reverse).Rows[0]["DetectedPolarity"].GetString());

        using AlgorithmImageBuffer multipleInput = CreateLogistic(AlgorithmImageFormat.Gray8);
        using AlgorithmResult multiple = await RunAsync(multipleInput, Calipers(new(4, 1), new(58, 1), new(4, 3)), new SubpixelEdgeParameters());
        Assert.Equal(2, Measurement(multiple, "subpixel_edge.accepted_count"));
        Assert.Equal(4, multiple.GetArtifact<AlgorithmGeometryArtifact>("subpixel-edge-geometry")!.Geometries.Count);

        using AlgorithmImageBuffer physicalInput = new(Width, Height, Width, AlgorithmImageFormat.Gray8, CreateLogisticBytes(), 254, 254);
        PolylineAlgorithmRoi physical = new([new(0.4, 0.2), new(5.8, 0.2)]) { CoordinateSpace = AlgorithmCoordinateSpace.Physical };
        using AlgorithmResult physicalResult = await RunAsync(physicalInput, physical, new SubpixelEdgeParameters());
        Assert.InRange(Edges(physicalResult).Rows[0]["EdgeX"].GetDouble(), ExpectedEdge - 0.18, ExpectedEdge + 0.18);
    }

    [Fact]
    public async Task BoundaryInvalidSamplesWeakEdgesAndLimitsHaveStructuredReasons()
    {
        using AlgorithmImageBuffer rejectInput = CreateLogistic(AlgorithmImageFormat.Gray8);
        using AlgorithmResult rejected = await RunAsync(rejectInput, Calipers(new(4, 0), new(58, 0)), new SubpixelEdgeParameters { NormalAveragingRadiusPixels = 1 });
        Assert.False(Edges(rejected).Rows[0]["Accepted"].GetBoolean());
        Assert.Equal("sample_out_of_bounds", Edges(rejected).Rows[0]["RejectionReason"].GetString());

        using AlgorithmImageBuffer clampInput = CreateLogistic(AlgorithmImageFormat.Gray8);
        using AlgorithmResult clamped = await RunAsync(clampInput, Calipers(new(4, 0), new(58, 0)), new SubpixelEdgeParameters
        {
            NormalAveragingRadiusPixels = 1,
            BoundaryMode = SubpixelEdgeBoundaryMode.Clamp,
        });
        Assert.True(Edges(clamped).Rows[0]["Accepted"].GetBoolean());
        Assert.True(Measurement(clamped, "subpixel_edge.clamped_sample_count") > 0);

        float[] invalidPixels = Enumerable.Repeat(0f, Width * Height).ToArray();
        invalidPixels[2 * Width + 30] = float.NaN;
        using AlgorithmImageBuffer invalidInput = new(Width, Height, Width * 4, AlgorithmImageFormat.Gray32Float, MemoryMarshal.AsBytes(invalidPixels.AsSpan()).ToArray());
        using AlgorithmResult invalid = await RunAsync(invalidInput, Calipers(new(4, 2), new(58, 2)), new SubpixelEdgeParameters());
        Assert.Equal("invalid_sample", Edges(invalid).Rows[0]["RejectionReason"].GetString());

        using AlgorithmImageBuffer flatInput = new(Width, Height, Width, AlgorithmImageFormat.Gray8, new byte[Width * Height]);
        using AlgorithmResult flat = await RunAsync(flatInput, Calipers(new(4, 2), new(58, 2)), new SubpixelEdgeParameters());
        Assert.Equal("gradient_below_minimum", Edges(flat).Rows[0]["RejectionReason"].GetString());

        using AlgorithmImageBuffer shortInput = CreateLogistic(AlgorithmImageFormat.Gray8);
        using AlgorithmResult shortLine = await RunAsync(shortInput, Calipers(new(4, 2), new(7, 2)), new SubpixelEdgeParameters());
        Assert.Equal("insufficient_samples", Edges(shortLine).Rows[0]["RejectionReason"].GetString());

        using AlgorithmImageBuffer limitedInput = CreateLogistic(AlgorithmImageFormat.Gray8);
        using AlgorithmResult limited = await RunAsync(limitedInput, Calipers(new(4, 2), new(58, 2)), new SubpixelEdgeParameters { MaximumSamplesPerCaliper = 10 });
        Assert.Equal("sample_limit_exceeded", Edges(limited).Rows[0]["RejectionReason"].GetString());

        using AlgorithmImageBuffer totalLimitedInput = CreateLogistic(AlgorithmImageFormat.Gray8);
        using AlgorithmResult totalLimited = await RunAsync(
            totalLimitedInput,
            Calipers(new(4, 1), new(58, 1), new(4, 3)),
            new SubpixelEdgeParameters { MaximumTotalSamples = 220 });
        Assert.True(Edges(totalLimited).Rows[0]["Accepted"].GetBoolean());
        Assert.Equal("total_sample_limit_exceeded", Edges(totalLimited).Rows[1]["RejectionReason"].GetString());

        using AlgorithmImageBuffer tooManyInput = CreateLogistic(AlgorithmImageFormat.Gray8);
        using AlgorithmResult tooMany = await RunAsync(tooManyInput, Calipers(new(4, 1), new(58, 1), new(4, 3)), new SubpixelEdgeParameters { MaximumCalipers = 1 });
        Assert.Equal(AlgorithmResultStatus.Failed, tooMany.Status);
        Assert.Equal("subpixel_edge_caliper_limit_exceeded", Assert.Single(tooMany.Failures).Code);
    }

    [Fact]
    public async Task UnsupportedRoiCancellationAndEveryTerminalPathReleaseTransferredInput()
    {
        AlgorithmImageBuffer wrongRoiInput = CreateLogistic(AlgorithmImageFormat.Gray8);
        using AlgorithmResult wrongRoi = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.SubpixelEdge, new SubpixelEdgeParameters(), new RectangleAlgorithmRoi(0, 0, 10, 3)),
            Inputs = [new AlgorithmInput { Name = "source", Image = wrongRoiInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        Assert.Equal(AlgorithmResultStatus.Failed, wrongRoi.Status);
        Assert.True(wrongRoiInput.IsDisposed);

        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "subpixel-edge.sample") cancellation.Cancel();
        });
        AlgorithmImageBuffer cancelledInput = CreateLogistic(AlgorithmImageFormat.Gray8);
        using AlgorithmResult cancelled = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.SubpixelEdge, new SubpixelEdgeParameters(), Calipers(new(4, 2), new(58, 2))),
            Inputs = [new AlgorithmInput { Name = "source", Image = cancelledInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            Progress = progress,
        }, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, cancelled.Status);
        Assert.True(cancelledInput.IsDisposed);

        AlgorithmImageBuffer successInput = CreateLogistic(AlgorithmImageFormat.Gray8);
        using AlgorithmResult success = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.SubpixelEdge, new SubpixelEdgeParameters(), Calipers(new(4, 2), new(58, 2))),
            Inputs = [new AlgorithmInput { Name = "source", Image = successInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, success.Status);
        Assert.True(successInput.IsDisposed);
    }

    [Fact]
    public async Task BatchAndFlowReuseInvocationAndStructuredArtifacts()
    {
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(
            StandardAlgorithmIds.SubpixelEdge,
            new SubpixelEdgeParameters(),
            Calipers(new(4, 2), new(58, 2)));
        string directory = Path.Combine(Path.GetTempPath(), $"ColorVision-SubpixelEdge-{Guid.NewGuid():N}");
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
            Assert.Contains("subpixel-edge-geometry", File.ReadAllText(Assert.Single(batch.Files[0].OutputPaths)), StringComparison.Ordinal);

            LocalFrameMetadata metadata = new() { Width = Width, Height = Height, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, Width * Height, 0);
            using LocalFlowFrameLease lease = frame.Acquire();
            Marshal.Copy(CreateLogisticBytes(), 0, lease.RawPointer, Width * Height);
            using AlgorithmResult flow = await LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(ExperimentalAlgorithmTestRuntime.Runtime, lease, invocation);
            Assert.Equal(1, Measurement(flow, "subpixel_edge.accepted_count"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ImageViewMenuTableOverlayAndReleaseAreUsable()
    {
        using AlgorithmImageBuffer input = CreateLogistic(AlgorithmImageFormat.Gray8);
        AlgorithmResult result = await RunAsync(input, Calipers(new(4, 2), new(58, 2)), new SubpixelEdgeParameters());
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
                string?[] menuIds = new SubpixelEdgeContextMenu(context, imageView.EditorContext.DrawEditorContext)
                    .GetContextMenuItems().Select(item => item.GuidId).ToArray();
                string[] expectedMenuIds = ["SubpixelEdge", "SubpixelEdgeHorizontal", "SubpixelEdgeVertical", "SubpixelEdgePolyline"];
                Assert.Equal(expectedMenuIds, menuIds);
                int before = context.ImageShow.Visuals.Count;
                SubpixelEdgeResultWindow window = new(result, context, imageView.EditorContext.DrawEditorContext);
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

    private static async Task<AlgorithmResult> RunAsync(AlgorithmImageBuffer input, PolylineAlgorithmRoi roi, SubpixelEdgeParameters parameters)
        => await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.SubpixelEdge, parameters, roi),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
        });

    private static PolylineAlgorithmRoi Calipers(params AlgorithmPoint[] points) => new(points);

    private static AlgorithmTableArtifact Edges(AlgorithmResult result)
        => result.GetArtifact<AlgorithmTableArtifact>("subpixel-edges")!;

    private static double Measurement(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmMeasurementArtifact>("subpixel-edge-summary")!.Measurements.Single(value => value.Name == name).Value;

    private static AlgorithmImageBuffer CreateLogistic(AlgorithmImageFormat format)
    {
        int channels = format.Channels();
        int bytesPerChannel = format.BitsPerChannel() / 8;
        int stride = Width * channels * bytesPerChannel;
        byte[] data = new byte[stride * Height];
        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                double nominal = 255 / (1 + Math.Exp(-(x - ExpectedEdge) / 0.8));
                for (int channel = 0; channel < channels; channel++)
                {
                    double value = channel == 3 ? 255 : nominal;
                    int offset = y * stride + (x * channels + channel) * bytesPerChannel;
                    if (format.IsFloatingPoint())
                        MemoryMarshal.Write(data.AsSpan(offset, 4), (float)(value / 255));
                    else if (format.BitsPerChannel() == 16)
                        MemoryMarshal.Write(data.AsSpan(offset, 2), (ushort)Math.Round(value / 255 * ushort.MaxValue));
                    else
                        data[offset] = (byte)Math.Round(value);
                }
            }
        }
        return new AlgorithmImageBuffer(Width, Height, stride, format, data);
    }

    private static byte[] CreateLogisticBytes()
    {
        using AlgorithmImageBuffer buffer = CreateLogistic(AlgorithmImageFormat.Gray8);
        return buffer.Data.ToArray();
    }

    private static WriteableBitmap CreateBitmap()
    {
        WriteableBitmap bitmap = new(Width, Height, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, Width, Height), CreateLogisticBytes(), Width, 0);
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
            Mat mat = new(Height, Width, MatType.CV_8UC1);
            Marshal.Copy(CreateLogisticBytes(), 0, mat.Data, Width * Height);
            return mat;
        }
    }

    private sealed class InlineProgress(Action<AlgorithmProgress> report) : IProgress<AlgorithmProgress>
    {
        public void Report(AlgorithmProgress value) => report(value);
    }
}
