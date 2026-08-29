using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageProfile;
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

public sealed class ImageProfileV1Tests
{
    [Fact]
    public void SchemaV1PreservesTheHistoricalDefaultAndMaximumSampleContract()
    {
        ImageProfileParameters defaults = new();
        Assert.Equal(1, defaults.SchemaVersion);
        Assert.Equal(100_000, defaults.MaximumSamples);

        ImageProfileParameters historical = new() { MaximumSamples = 1_000_000 };
        Assert.True(historical.Validate().IsValid);
        string json = JsonSerializer.Serialize(historical, AlgorithmJson.Options);
        ImageProfileParameters restored = JsonSerializer.Deserialize<ImageProfileParameters>(json, AlgorithmJson.Options)!;
        Assert.Equal(1_000_000, restored.MaximumSamples);
        Assert.True(restored.Validate().IsValid);
    }

    [Fact]
    public void ParameterValidationRejectsInvalidSpacingEnumsAndLimit()
    {
        ImageProfileParameters parameters = new()
        {
            SampleSpacingPixels = 0,
            Interpolation = (ImageProfileInterpolation)99,
            BoundaryMode = (ImageProfileBoundaryMode)99,
            MaximumSamples = 1_000_001,
        };
        AlgorithmValidationResult validation = parameters.Validate();
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(parameters.SampleSpacingPixels));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(parameters.Interpolation));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(parameters.BoundaryMode));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(parameters.MaximumSamples));
        Assert.Contains(new ImageProfileParameters { MaximumSamples = 1 }.Validate().Issues,
            issue => issue.Path == nameof(parameters.MaximumSamples));
    }

    [Fact]
    public async Task ColorProfileResultBudgetRejectsBeforeSamplingOrAllocatingRows()
    {
        using AlgorithmImageBuffer input = new(151, 1, 604, AlgorithmImageFormat.Bgra32, new byte[604]);
        List<string> stages = [];
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(
            StandardAlgorithmIds.ImageProfile,
            new ImageProfileParameters { SampleSpacingPixels = 0.01, MaximumSamples = 50_000 },
            new PolylineAlgorithmRoi([new(0, 0), new(150, 0)]));

        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = invocation,
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
            Progress = new InlineProgress(value => stages.Add(value.Stage)),
        });

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "profile_result_budget_exceeded");
        Assert.Null(result.GetArtifact<AlgorithmTableArtifact>("image-profile-samples"));
        Assert.DoesNotContain("profile.sample", stages);
    }

    [Theory]
    [InlineData(4_096)]
    [InlineData(8_192)]
    public async Task FourAndEightKSubpixelProfilesAreRejectedBeforeSampling(int width)
    {
        using AlgorithmImageBuffer input = Buffer(new byte[width], width, 1);
        List<string> stages = [];
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(
            StandardAlgorithmIds.ImageProfile,
            new ImageProfileParameters { SampleSpacingPixels = 0.01, MaximumSamples = ImageProfileParameters.AbsoluteMaximumSamples },
            new PolylineAlgorithmRoi([new(0, 0), new(width - 1, 0)]));

        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = invocation,
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
            Progress = new InlineProgress(value => stages.Add(value.Stage)),
        });

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "profile_execution_sample_budget_exceeded");
        Assert.DoesNotContain("profile.sample", stages);
    }

    [Fact]
    public async Task OversizedPolylinePointCountIsRejectedBeforeSegmentMaterialization()
    {
        using AlgorithmImageBuffer input = Buffer([0, 1], 2, 1);
        AlgorithmPoint[] points = Enumerable.Range(0, ImageProfileParameters.MaximumPathPoints + 1)
            .Select(index => new AlgorithmPoint(index & 1, 0))
            .ToArray();

        using AlgorithmResult result = await RunAsync(
            input,
            new PolylineAlgorithmRoi(points),
            new ImageProfileParameters { MaximumSamples = ImageProfileParameters.AbsoluteMaximumSamples });

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "profile_path_point_limit_exceeded");
    }

    [Fact]
    public async Task HorizontalNearestProfileIsGoldenAndDoesNotMutateInput()
    {
        using AlgorithmImageBuffer input = Buffer([10, 20, 30], 3, 1);
        byte[] original = input.Data.ToArray();
        using AlgorithmResult result = await RunAsync(
            input,
            new PolylineAlgorithmRoi([new(0, 0), new(2, 0)]),
            new ImageProfileParameters { Interpolation = ImageProfileInterpolation.Nearest });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmTableArtifact table = Samples(result);
        Assert.Equal(new double[] { 10, 20, 30 }, Values(table, "Gray"));
        Assert.Equal(new double[] { 0, 1, 2 }, Values(table, "DistancePixels"));
        Assert.Equal(3, Measurement(result, "profile.sample_count"));
        Assert.Equal(2, Measurement(result, "profile.path_length_pixels"));
        Assert.Equal(original, input.Data.ToArray());
        Assert.Equal(AlgorithmGeometryKind.Polyline, Assert.Single(result.GetArtifact<AlgorithmGeometryArtifact>()!.Geometries).Kind);
    }

    [Fact]
    public async Task VerticalBilinearProfileUsesPiecewiseCoordinatesAndDpiAwareDistance()
    {
        using AlgorithmImageBuffer input = new(2, 2, 2, AlgorithmImageFormat.Gray8, [0, 10, 20, 30], 254, 127);
        using AlgorithmResult result = await RunAsync(
            input,
            new PolylineAlgorithmRoi([new(0.5, 0), new(0.5, 1)]),
            new ImageProfileParameters { SampleSpacingPixels = 0.5, Interpolation = ImageProfileInterpolation.Bilinear });

        AlgorithmTableArtifact table = Samples(result);
        Assert.Equal(new double[] { 5, 15, 25 }, Values(table, "Gray"));
        Assert.Equal(new double[] { 0, 0.5, 1 }, Values(table, "DistancePixels"));
        Assert.Equal(0.2, Measurement(result, "profile.path_length_millimetres"), 12);
    }

    [Fact]
    public async Task ArbitraryPolylineIncludesOpenEndpointAndTracksSegmentDistance()
    {
        using AlgorithmImageBuffer input = Buffer(Enumerable.Range(0, 25).Select(value => (byte)value).ToArray(), 5, 5);
        using AlgorithmResult result = await RunAsync(
            input,
            new PolylineAlgorithmRoi([new(0, 0), new(3, 0), new(3, 4)]),
            new ImageProfileParameters { SampleSpacingPixels = 2, Interpolation = ImageProfileInterpolation.Nearest });

        AlgorithmTableArtifact table = Samples(result);
        Assert.Equal(new double[] { 0, 2, 8, 18, 23 }, Values(table, "Gray"));
        Assert.Equal(new double[] { 0, 2, 4, 6, 7 }, Values(table, "DistancePixels"));
        Assert.Equal(new[] { 0, 0, 1, 1, 1 }, table.Rows.Select(row => row["SegmentIndex"].GetInt32()));
    }

    [Fact]
    public async Task SegmentIndexPreservesTheOriginalPathIndexAfterDegenerateSegmentsAreFiltered()
    {
        using AlgorithmImageBuffer input = Buffer([10, 20, 30], 3, 1);
        using AlgorithmResult result = await RunAsync(
            input,
            new PolylineAlgorithmRoi([new(0, 0), new(0, 0), new(2, 0)]),
            new ImageProfileParameters { Interpolation = ImageProfileInterpolation.Nearest });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.All(Samples(result).Rows, row => Assert.Equal(1, row["SegmentIndex"].GetInt32()));
    }

    [Fact]
    public async Task ColorAndFloatProfilesPreserveChannelsLuminanceAndInvalidClassification()
    {
        using AlgorithmImageBuffer color = new(2, 1, 6, AlgorithmImageFormat.Bgr24, [10, 20, 30, 40, 50, 60]);
        using AlgorithmResult colorResult = await RunAsync(color, new PolylineAlgorithmRoi([new(0, 0), new(1, 0)]), new ImageProfileParameters());
        AlgorithmTableArtifact colorTable = Samples(colorResult);
        Assert.Equal(new double[] { 10, 40 }, Values(colorTable, "B"));
        Assert.Equal(new double[] { 20, 50 }, Values(colorTable, "G"));
        Assert.Equal(new double[] { 30, 60 }, Values(colorTable, "R"));
        Assert.Equal(new[] { 0.114 * 10 + 0.587 * 20 + 0.299 * 30, 0.114 * 40 + 0.587 * 50 + 0.299 * 60 }, Values(colorTable, "Luminance"), new DoubleToleranceComparer(1e-12));

        float[] floats = [float.NaN, float.PositiveInfinity];
        using AlgorithmImageBuffer floatInput = new(2, 1, 8, AlgorithmImageFormat.Gray32Float, MemoryMarshal.AsBytes(floats.AsSpan()).ToArray());
        using AlgorithmResult floatResult = await RunAsync(floatInput, new PolylineAlgorithmRoi([new(0, 0), new(1, 0)]),
            new ImageProfileParameters { Interpolation = ImageProfileInterpolation.Nearest });
        AlgorithmTableArtifact floatTable = Samples(floatResult);
        Assert.All(floatTable.Rows, row => Assert.Equal(JsonValueKind.Null, row["Gray"].ValueKind));
        Assert.Equal(new[] { "NaN", "+Infinity" }, floatTable.Rows.Select(row => row["GrayStatus"].GetString()));
        Assert.Equal(2, Measurement(floatResult, "channel.invalid_count", channel: 0));
    }

    [Fact]
    public async Task SixteenBitBgraAndPhysicalPolylinePreservePrecisionAlphaAndCoordinates()
    {
        ushort[] pixels = [1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000];
        using AlgorithmImageBuffer input = new(
            2,
            1,
            16,
            AlgorithmImageFormat.Bgra64,
            MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray(),
            254,
            127);
        PolylineAlgorithmRoi physical = new([new(0, 0), new(0.1, 0)]) { CoordinateSpace = AlgorithmCoordinateSpace.Physical };
        using AlgorithmResult result = await RunAsync(input, physical, new ImageProfileParameters
        {
            SampleSpacingPixels = 1,
            Interpolation = ImageProfileInterpolation.Nearest,
            IncludeAlpha = true,
        });
        AlgorithmTableArtifact table = Samples(result);
        Assert.Equal(new double[] { 1000, 5000 }, Values(table, "B"));
        Assert.Equal(new double[] { 2000, 6000 }, Values(table, "G"));
        Assert.Equal(new double[] { 3000, 7000 }, Values(table, "R"));
        Assert.Equal(new double[] { 4000, 8000 }, Values(table, "A"));
        Assert.Equal(0.1, Measurement(result, "profile.path_length_millimetres"), 12);
        AlgorithmGeometry geometry = Assert.Single(result.GetArtifact<AlgorithmGeometryArtifact>()!.Geometries);
        Assert.Equal(1, geometry.Points[1].X, 12);
    }

    [Fact]
    public async Task MissingPolylineReturnsStructuredFailure()
    {
        using AlgorithmImageBuffer input = Buffer([1, 2], 2, 1);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageProfile, new ImageProfileParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "profile_path_required");
    }

    [Fact]
    public async Task BoundaryModesRejectClampOrSkipDeterministically()
    {
        PolylineAlgorithmRoi path = new([new(-1, 0), new(1, 0)]);
        using AlgorithmImageBuffer rejectedInput = Buffer([10, 20], 2, 1);
        using AlgorithmResult rejected = await RunAsync(rejectedInput, path, new ImageProfileParameters
        {
            Interpolation = ImageProfileInterpolation.Nearest,
            BoundaryMode = ImageProfileBoundaryMode.Reject,
        });
        Assert.Equal(AlgorithmResultStatus.Failed, rejected.Status);
        Assert.Contains(rejected.Failures, failure => failure.Code == "profile_sample_out_of_bounds");

        using AlgorithmImageBuffer clampedInput = Buffer([10, 20], 2, 1);
        using AlgorithmResult clamped = await RunAsync(clampedInput, path, new ImageProfileParameters
        {
            Interpolation = ImageProfileInterpolation.Nearest,
            BoundaryMode = ImageProfileBoundaryMode.Clamp,
        });
        Assert.Equal(new double[] { 10, 10, 20 }, Values(Samples(clamped), "Gray"));
        Assert.Contains(clamped.Diagnostics.Messages, message => message.Code == "profile_samples_clamped");

        using AlgorithmImageBuffer skippedInput = Buffer([10, 20], 2, 1);
        using AlgorithmResult skipped = await RunAsync(skippedInput, path, new ImageProfileParameters
        {
            Interpolation = ImageProfileInterpolation.Nearest,
            BoundaryMode = ImageProfileBoundaryMode.Skip,
        });
        Assert.Equal(new double[] { 10, 20 }, Values(Samples(skipped), "Gray"));
        Assert.Equal(new[] { 1, 2 }, Samples(skipped).Rows.Select(row => row["RequestedIndex"].GetInt32()));
        Assert.Contains(skipped.Diagnostics.Messages, message => message.Code == "profile_samples_skipped");
    }

    [Fact]
    public async Task ClosedPathDoesNotRepeatFirstPointAndSampleLimitIsStructured()
    {
        using AlgorithmImageBuffer input = Buffer([10, 20, 30, 40], 2, 2);
        PolylineAlgorithmRoi square = new([new(0, 0), new(1, 0), new(1, 1), new(0, 1)]);
        using AlgorithmResult closed = await RunAsync(input, square, new ImageProfileParameters
        {
            ClosePath = true,
            Interpolation = ImageProfileInterpolation.Nearest,
        });
        Assert.Equal(new double[] { 10, 20, 40, 30 }, Values(Samples(closed), "Gray"));
        Assert.Equal(AlgorithmGeometryKind.Polygon, Assert.Single(closed.GetArtifact<AlgorithmGeometryArtifact>()!.Geometries).Kind);

        using AlgorithmImageBuffer limitedInput = Buffer([10, 20, 30, 40], 2, 2);
        using AlgorithmResult limited = await RunAsync(limitedInput, new PolylineAlgorithmRoi([new(0, 0), new(1, 0)]), new ImageProfileParameters
        {
            SampleSpacingPixels = 0.01,
            MaximumSamples = 2,
        });
        Assert.Equal(AlgorithmResultStatus.Failed, limited.Status);
        Assert.Contains(limited.Failures, failure => failure.Code == "profile_sample_limit_exceeded");
    }

    [Fact]
    public async Task CancellationIsStructuredAndReleasesTransferredInput()
    {
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "profile.sample") cancellation.Cancel();
        });
        AlgorithmImageBuffer input = Buffer(new byte[4096], 4096, 1);
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(
            StandardAlgorithmIds.ImageProfile,
            new ImageProfileParameters { SampleSpacingPixels = 0.2, MaximumSamples = ImageProfileParameters.AbsoluteMaximumSamples },
            new PolylineAlgorithmRoi([new(0, 0), new(4095, 0)]));
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = invocation,
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
            Progress = progress,
        }, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, result.Status);
        Assert.True(input.IsDisposed);
    }

    [Fact]
    public async Task BatchAndFlowUseTheSameProfileInvocationAndArtifacts()
    {
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(
            StandardAlgorithmIds.ImageProfile,
            new ImageProfileParameters { Interpolation = ImageProfileInterpolation.Nearest },
            new PolylineAlgorithmRoi([new(0, 0), new(2, 0)]));
        string directory = Path.Combine(Path.GetTempPath(), $"ColorVision-ImageProfile-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            BatchAlgorithmAnalysisResult batch = await new BatchAlgorithmAnalysisProcessor([new TestLoader()]).ProcessAsync(new BatchAlgorithmAnalysisRequest
            {
                Items = [new BatchImageItem(Path.Combine(directory, "source.fake"), directory)],
                Invocation = invocation,
                OutputDirectory = directory,
                PreserveFolderStructure = false,
            });
            Assert.Equal(AlgorithmResultStatus.Succeeded, Assert.Single(batch.Files).Status);
            Assert.Contains("image-profile-samples", File.ReadAllText(Assert.Single(batch.Files[0].OutputPaths)), StringComparison.Ordinal);

            LocalFrameMetadata metadata = new() { Width = 3, Height = 1, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, 3, 0);
            using LocalFlowFrameLease lease = frame.Acquire();
            Marshal.Copy(new byte[] { 10, 20, 30 }, 0, lease.RawPointer, 3);
            using AlgorithmResult flow = await LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(lease, invocation);
            Assert.Equal(new double[] { 10, 20, 30 }, Values(Samples(flow), "Gray"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ResultWindowDisplaysChartTableAndReleasesTransientOverlay()
    {
        using AlgorithmImageBuffer input = Buffer([10, 20, 30], 3, 1);
        AlgorithmResult result = await RunAsync(input, new PolylineAlgorithmRoi([new(0, 0), new(2, 0)]), new ImageProfileParameters());
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
                string?[] menuIds = new ImageProfileContextMenu(context, imageView.EditorContext.DrawEditorContext)
                    .GetContextMenuItems().Select(item => item.GuidId).ToArray();
                Assert.Equal(new[] { "ImageProfile", "ImageProfileHorizontal", "ImageProfileVertical", "ImageProfilePolyline" }, menuIds);
                int before = context.ImageShow.Visuals.Count;
                ImageProfileResultWindow window = new(result, context, imageView.EditorContext.DrawEditorContext);
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

    [Fact]
    public async Task ResultWindowDownsamplesLargeTablesInsteadOfCopyingEveryRowToWpfControls()
    {
        const int samples = 3_001;
        using AlgorithmImageBuffer input = Buffer(new byte[samples], samples, 1);
        AlgorithmResult result = await RunAsync(
            input,
            new PolylineAlgorithmRoi([new(0, 0), new(samples - 1, 0)]),
            new ImageProfileParameters { Interpolation = ImageProfileInterpolation.Nearest });
        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureResources();
            ImageView view = new();
            WriteableBitmap bitmap = new(samples, 1, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, samples, 1), new byte[samples], samples, 0);
            view.SetImageSource(bitmap, enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });
        try
        {
            WpfTestHost.Invoke(() =>
            {
                ImageProfileResultWindow window = new(result, imageView.EditorContext.ProcessingContext, imageView.EditorContext.DrawEditorContext);
                DataGrid grid = Assert.IsType<DataGrid>(window.FindName("SamplesGrid"));
                TextBlock summary = Assert.IsType<TextBlock>(window.FindName("SummaryText"));
                Assert.InRange(grid.Items.Count, 2, 2_000);
                Assert.Contains("3,001", summary.Text, StringComparison.Ordinal);
                Assert.Contains("2,000", summary.Text, StringComparison.Ordinal);
                window.Close();
            });
        }
        finally
        {
            if (!result.IsDisposed) result.Dispose();
            WpfTestHost.Invoke(imageView.Dispose);
        }
    }

    [Fact]
    public async Task AsyncCsvExportKeepsEveryProfileRowBeyondTheWpfPreviewLimit()
    {
        const int samples = 2_501;
        string directory = Path.Combine(Path.GetTempPath(), $"ColorVision-ProfileExport-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            using AlgorithmImageBuffer input = Buffer(new byte[samples], samples, 1);
            using AlgorithmResult result = await RunAsync(
                input,
                new PolylineAlgorithmRoi([new(0, 0), new(samples - 1, 0)]),
                new ImageProfileParameters { Interpolation = ImageProfileInterpolation.Nearest });

            IReadOnlyList<string> outputs = await AlgorithmResultExporter.ExportCsvBundleAsync(
                result,
                Path.Combine(directory, "profile.csv"),
                cancellationToken: CancellationToken.None);
            string samplesPath = Assert.Single(outputs, path => Path.GetFileName(path).Contains("image-profile-samples", StringComparison.Ordinal));

            Assert.Equal(samples + 1, File.ReadLines(samplesPath).Count());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<AlgorithmResult> RunAsync(AlgorithmImageBuffer input, AlgorithmRoi roi, ImageProfileParameters parameters)
        => await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageProfile, parameters, roi),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

    private static AlgorithmTableArtifact Samples(AlgorithmResult result)
        => result.GetArtifact<AlgorithmTableArtifact>("image-profile-samples")!;

    private static double[] Values(AlgorithmTableArtifact table, string name)
        => table.Rows.Select(row => row[name].GetDouble()).ToArray();

    private static double Measurement(AlgorithmResult result, string name, int? channel = null)
        => result.GetArtifact<AlgorithmMeasurementArtifact>()!.Measurements.Single(value => value.Name == name && value.Channel == channel).Value;

    private static AlgorithmImageBuffer Buffer(byte[] pixels, int width, int height)
        => new(width, height, width, AlgorithmImageFormat.Gray8, pixels.ToArray());

    private static WriteableBitmap CreateBitmap()
    {
        WriteableBitmap bitmap = new(3, 1, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, 3, 1), new byte[] { 10, 20, 30 }, 3, 0);
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
            Mat mat = new(1, 3, MatType.CV_8UC1);
            Marshal.Copy(new byte[] { 10, 20, 30 }, 0, mat.Data, 3);
            return mat;
        }
    }

    private sealed class InlineProgress(Action<AlgorithmProgress> report) : IProgress<AlgorithmProgress>
    {
        public void Report(AlgorithmProgress value) => report(value);
    }

    private sealed class DoubleToleranceComparer(double tolerance) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;
        public int GetHashCode(double obj) => 0;
    }
}
