using ColorVision.Algorithms;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.ImageComparison;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageComparisonV1Tests
{
    [Fact]
    public void ParametersDescriptorAndInvocationHaveStableMultiInputContract()
    {
        ImageComparisonParameters invalid = new() { FloatPeakValue = 0, HeatmapMaximum = -1 };
        Assert.Equal(2, invalid.Validate().Issues.Count);

        AlgorithmDescriptor descriptor = StandardAlgorithmCatalog.Create().Descriptors.Single(item => item.Id == StandardAlgorithmIds.ImageComparison);
        Assert.Equal(2, descriptor.MinimumInputCount);
        Assert.Equal(2, descriptor.MaximumInputCount);
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.MultiInput));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Batch));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Flow));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Copilot));
        Assert.True(StandardAlgorithmCatalog.Create().TryResolveAlias("ImageDiff", out AlgorithmDescriptor? alias));
        Assert.Equal(descriptor.Id, alias!.Id);

        AlgorithmInvocation invocation = new()
        {
            AlgorithmId = StandardAlgorithmIds.ImageComparison,
            Parameters = AlgorithmJson.ToElement(new ImageComparisonParameters()),
            Inputs = [new("reference", Revision: "7"), new("candidate", "candidate.png", Checksum: "abc")],
        };
        AlgorithmInvocation restored = AlgorithmJson.Deserialize<AlgorithmInvocation>(AlgorithmJson.ToElement(invocation));
        Assert.Equal(new[] { "reference", "candidate" }, restored.Inputs.Select(input => input.Name));
        Assert.Equal("abc", restored.Inputs[1].Checksum);
    }

    [Fact]
    public async Task Gray8GoldenPreservesExactAbsoluteSignedMetricsAndInputs()
    {
        using AlgorithmImageBuffer reference = Gray8([0, 10, 20, 30], 4, 1);
        using AlgorithmImageBuffer candidate = Gray8([0, 8, 25, 10], 4, 1);
        byte[] leftBefore = reference.Data.ToArray();
        byte[] rightBefore = candidate.Data.ToArray();
        using AlgorithmResult result = await RunAsync(reference, candidate);

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(new byte[] { 0, 2, 5, 20 }, Image(result, "absolute-difference").Data.ToArray());
        Assert.Equal(new float[] { 0, 2, -5, 20 }, Floats(Image(result, "signed-difference")));
        Assert.Equal(107.25, Measurement(result, "comparison.mse"), 12);
        Assert.Equal(Math.Sqrt(107.25), Measurement(result, "comparison.rmse"), 12);
        Assert.Equal(20 * Math.Log10(255 / Math.Sqrt(107.25)), Measurement(result, "comparison.psnr_db"), 12);
        Assert.Equal(20, Measurement(result, "comparison.max_abs_difference"));
        Assert.Equal(leftBefore, reference.Data.ToArray());
        Assert.Equal(rightBefore, candidate.Data.ToArray());
        Assert.All(new[] { "absolute-difference-visualization", "signed-difference-visualization", "difference-heatmap" },
            name => Assert.Equal(AlgorithmImageFormat.Bgr24, Image(result, name).Format));
    }

    [Fact]
    public async Task SixteenBitAndColorComparisonsPreserveDepthChannelsAndAlphaPolicy()
    {
        ushort[] leftValues = [1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000];
        ushort[] rightValues = [900, 2200, 3000, 1000, 4500, 6000, 7500, 9000];
        using AlgorithmImageBuffer reference = UShort(leftValues, 2, 1, AlgorithmImageFormat.Bgra64);
        using AlgorithmImageBuffer candidate = UShort(rightValues, 2, 1, AlgorithmImageFormat.Bgra64);
        using AlgorithmResult result = await RunAsync(reference, candidate, new ImageComparisonParameters { IncludeAlphaInMetrics = false });

        Assert.Equal(AlgorithmImageFormat.Bgra64, Image(result, "absolute-difference").Format);
        Assert.Equal(new ushort[] { 100, 200, 0, 3000, 500, 0, 500, 1000 }, UShorts(Image(result, "absolute-difference")));
        Assert.Equal(AlgorithmImageFormat.Bgra128Float, Image(result, "signed-difference").Format);
        Assert.Equal(new float[] { 100, -200, 0, 3000, 500, 0, -500, -1000 }, Floats(Image(result, "signed-difference")));
        Assert.Equal(6, Measurement(result, "comparison.finite_sample_count"));
        AlgorithmTableArtifact table = result.GetArtifact<AlgorithmTableArtifact>("image-comparison-channels")!;
        Assert.Equal(new[] { "B", "G", "R" }, table.Rows.Select(row => row["Channel"].GetString()));
    }

    [Fact]
    public async Task FloatComparisonPreservesNaNAndUsesExplicitPeak()
    {
        using AlgorithmImageBuffer reference = Floats([0f, 0.5f, float.NaN, 1f], 4, 1, AlgorithmImageFormat.Gray32Float);
        using AlgorithmImageBuffer candidate = Floats([0.25f, 0.25f, 0f, 0.5f], 4, 1, AlgorithmImageFormat.Gray32Float);
        using AlgorithmResult result = await RunAsync(reference, candidate, new ImageComparisonParameters { FloatPeakValue = 2 });

        float[] signed = Floats(Image(result, "signed-difference"));
        Assert.Equal(new float[] { -0.25f, 0.25f }, signed[..2]);
        Assert.True(float.IsNaN(signed[2]));
        Assert.Equal(0.5f, signed[3]);
        Assert.Equal(0.125, Measurement(result, "comparison.mse"), 12);
        Assert.Equal(3, Measurement(result, "comparison.finite_sample_count"));
        Assert.Equal(1, Measurement(result, "comparison.invalid_sample_count"));
        Assert.Contains(result.Diagnostics.Messages, message => message.Code == "nonfinite_samples_excluded");
    }

    [Fact]
    public async Task FloatDifferenceOverflowKeepsIeeeArtifactAndIsExcludedFromMetrics()
    {
        using AlgorithmImageBuffer reference = Floats([float.MaxValue, 1f], 2, 1, AlgorithmImageFormat.Gray32Float);
        using AlgorithmImageBuffer candidate = Floats([-float.MaxValue, 0f], 2, 1, AlgorithmImageFormat.Gray32Float);
        using AlgorithmResult result = await RunAsync(reference, candidate);

        float[] signed = Floats(Image(result, "signed-difference"));
        Assert.True(float.IsPositiveInfinity(signed[0]));
        Assert.Equal(1f, signed[1]);
        Assert.Equal(1, Measurement(result, "comparison.finite_sample_count"));
        Assert.Equal(1, Measurement(result, "comparison.invalid_sample_count"));
        Assert.Equal(1, Measurement(result, "comparison.mse"));
    }

    [Fact]
    public async Task ExcludedNonFiniteAlphaDoesNotContaminateMetricHeatmap()
    {
        using AlgorithmImageBuffer reference = Floats([10, 20, 30, 1], 1, 1, AlgorithmImageFormat.Bgra128Float);
        using AlgorithmImageBuffer candidate = Floats([10, 20, 30, float.NaN], 1, 1, AlgorithmImageFormat.Bgra128Float);
        using AlgorithmResult result = await RunAsync(reference, candidate, new ImageComparisonParameters
        {
            IncludeAlphaInMetrics = false,
            EnableSsim = false,
            EnableAlignmentPrecheck = false,
        });

        Assert.Equal(0, Measurement(result, "comparison.mse"));
        Assert.Equal(new byte[] { 128, 0, 0 }, Image(result, "difference-heatmap").Data.ToArray());
        Assert.Equal(new byte[] { 255, 0, 255 }, Image(result, "absolute-difference-visualization").Data.ToArray());
    }

    [Theory]
    [InlineData("dimension", "dimension_mismatch")]
    [InlineData("format", "format_mismatch")]
    [InlineData("color", "color_space_mismatch")]
    [InlineData("unknown-color", "color_space_unspecified")]
    public async Task IncompatibleInputsReturnStructuredFailures(string mismatch, string code)
    {
        using AlgorithmImageBuffer reference = Gray8([1, 2], 2, 1);
        using AlgorithmImageBuffer candidate = mismatch switch
        {
            "dimension" => Gray8([1], 1, 1),
            "format" => UShort([1, 2], 2, 1, AlgorithmImageFormat.Gray16),
            _ => Gray8([1, 2], 2, 1),
        };
        string? leftSpace = mismatch == "unknown-color" ? null : "encoded-device-values";
        string? rightSpace = mismatch == "color" ? "linear-rgb" : leftSpace;
        using AlgorithmResult result = await RunAsync(reference, candidate, leftSpace: leftSpace, rightSpace: rightSpace);
        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == code);
    }

    [Fact]
    public async Task PerfectMatchHasInfinitePsnrAndExportsJsonWithoutLosingMeaning()
    {
        using AlgorithmImageBuffer reference = Gray8([1, 2, 3], 3, 1);
        using AlgorithmImageBuffer candidate = Gray8([1, 2, 3], 3, 1);
        using AlgorithmResult result = await RunAsync(reference, candidate);
        Assert.True(double.IsPositiveInfinity(Measurement(result, "comparison.psnr_db")));

        string directory = Path.Combine(Path.GetTempPath(), $"ColorVision-ImageComparison-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string path = AlgorithmResultExporter.ExportJson(result, Path.Combine(directory, "comparison.json"));
            string json = File.ReadAllText(path);
            Assert.Contains("Infinity", json, StringComparison.Ordinal);
            Assert.Throws<IOException>(() => AlgorithmResultExporter.ExportJson(result, path));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task DpiMismatchIsDiagnosticButPixelArraysRemainComparable()
    {
        using AlgorithmImageBuffer reference = new(2, 1, 2, AlgorithmImageFormat.Gray8, [1, 2], 96, 96);
        using AlgorithmImageBuffer candidate = new(2, 1, 2, AlgorithmImageFormat.Gray8, [1, 2], 192, 192);
        using AlgorithmResult result = await RunAsync(reference, candidate);
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Contains(result.Diagnostics.Messages, message => message.Code == "dpi_mismatch" && message.Severity == "warning");
    }

    [Fact]
    public async Task StableInputNamesDefineSignedDirectionIndependentOfCollectionOrder()
    {
        using AlgorithmImageBuffer reference = Gray8([10], 1, 1);
        using AlgorithmImageBuffer candidate = Gray8([3], 1, 1);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageComparison, new ImageComparisonParameters()),
            Inputs = [Input("candidate", candidate, AlgorithmInputOwnership.Borrowed), Input("reference", reference, AlgorithmInputOwnership.Borrowed)],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
        });
        Assert.Equal(7, Assert.Single(Floats(Image(result, "signed-difference"))));

        using AlgorithmImageBuffer first = Gray8([1], 1, 1);
        using AlgorithmImageBuffer second = Gray8([1], 1, 1);
        using AlgorithmResult invalid = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageComparison, new ImageComparisonParameters()),
            Inputs = [Input("source", first, AlgorithmInputOwnership.Borrowed), Input("candidate", second, AlgorithmInputOwnership.Borrowed)],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
        });
        Assert.Contains(invalid.Failures, failure => failure.Code == "invalid_input_names");
    }

    [Fact]
    public async Task CancellationReleasesBothTransferredInputsAndProducesNoResultImages()
    {
        const int size = 1024;
        AlgorithmImageBuffer reference = Gray8(new byte[size * size], size, size);
        AlgorithmImageBuffer candidate = Gray8(new byte[size * size], size, size);
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "comparison.scan") cancellation.Cancel();
        });
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageComparison, new ImageComparisonParameters()),
            Inputs = [Input("reference", reference, AlgorithmInputOwnership.Transferred), Input("candidate", candidate, AlgorithmInputOwnership.Transferred)],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
            Progress = progress,
        }, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, result.Status);
        Assert.Empty(result.Artifacts.OfType<AlgorithmImageArtifact>());
        Assert.True(reference.IsDisposed);
        Assert.True(candidate.IsDisposed);
    }

    [Fact]
    public async Task StructuredValidationFailureAlsoReleasesBothTransferredInputs()
    {
        AlgorithmImageBuffer reference = Gray8([1, 2], 2, 1);
        AlgorithmImageBuffer candidate = Gray8([1], 1, 1);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageComparison, new ImageComparisonParameters()),
            Inputs = [Input("reference", reference, AlgorithmInputOwnership.Transferred), Input("candidate", candidate, AlgorithmInputOwnership.Transferred)],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
        });
        Assert.Contains(result.Failures, failure => failure.Code == "dimension_mismatch");
        Assert.True(reference.IsDisposed);
        Assert.True(candidate.IsDisposed);
    }

    [Fact]
    public async Task ResultOwnsEveryGeneratedImageAndDisposesThemTogether()
    {
        using AlgorithmImageBuffer reference = Gray8([1, 2], 2, 1);
        using AlgorithmImageBuffer candidate = Gray8([0, 4], 2, 1);
        AlgorithmResult result = await RunAsync(reference, candidate);
        AlgorithmImageBuffer[] images = result.Artifacts.OfType<AlgorithmImageArtifact>().Select(artifact => artifact.Image).ToArray();
        Assert.Equal(5, images.Length);
        result.Dispose();
        Assert.All(images, image => Assert.True(image.IsDisposed));
    }

    [Fact]
    public async Task ResultWindowProvidesDifferenceBlinkSplitMetricsAndReleasesResult()
    {
        using AlgorithmImageBuffer reference = Gray8([1, 2, 3], 3, 1);
        using AlgorithmImageBuffer candidate = Gray8([1, 4, 1], 3, 1);
        AlgorithmResult result = await RunAsync(reference, candidate);
        WpfTestHost.Invoke(() =>
        {
            EnsureResources();
            WriteableBitmap left = Bitmap([1, 2, 3]);
            WriteableBitmap right = Bitmap([1, 4, 1]);
            ImageView view = new();
            try
            {
                view.SetImageSource(left, enableEditorImageServices: false, configureDefaultLayerController: false);
                ImageComparisonResultWindow window = new(result, left, right, "candidate.png",
                    view.EditorContext.ProcessingContext, view.EditorContext.DrawEditorContext);
                window.Show();
                Assert.NotNull(window.FindName("DifferenceImage"));
                Assert.NotNull(window.FindName("BlinkImage"));
                Assert.NotNull(window.FindName("SplitViewport"));
                Assert.NotNull(window.FindName("MetricsGrid"));
                Assert.NotNull(window.FindName("AlignmentGrid"));
                window.Close();
                Assert.True(result.IsDisposed);
            }
            finally
            {
                view.Dispose();
            }
        });
    }

    [Fact]
    public void ImageViewMenuExposesWholeImageAndM4RoiEntries()
    {
        ImageView view = WpfTestHost.Invoke(() =>
        {
            EnsureResources();
            ImageView created = new();
            created.SetImageSource(Bitmap([1, 2, 3]), enableEditorImageServices: false, configureDefaultLayerController: false);
            return created;
        });
        try
        {
            WpfTestHost.Invoke(() => Assert.Equal(
                new[] { "ImageComparison", "ImageComparisonWhole", "ImageComparisonRectangle", "ImageComparisonCircle", "ImageComparisonPolygon" },
                new ImageComparisonContextMenu(view.EditorContext.ProcessingContext, view.EditorContext.DrawEditorContext)
                    .GetContextMenuItems().Select(item => item.GuidId).ToArray()));
        }
        finally
        {
            WpfTestHost.Invoke(view.Dispose);
        }
    }

    private static async Task<AlgorithmResult> RunAsync(
        AlgorithmImageBuffer reference,
        AlgorithmImageBuffer candidate,
        ImageComparisonParameters? parameters = null,
        string? leftSpace = "encoded-device-values",
        string? rightSpace = "encoded-device-values")
        => await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageComparison, parameters ?? new ImageComparisonParameters()),
            Inputs =
            [
                Input("reference", reference, AlgorithmInputOwnership.Borrowed, leftSpace),
                Input("candidate", candidate, AlgorithmInputOwnership.Borrowed, rightSpace),
            ],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
        });

    private static AlgorithmInput Input(string name, AlgorithmImageBuffer image, AlgorithmInputOwnership ownership, string? colorSpace = "encoded-device-values")
        => new() { Name = name, Image = image, Ownership = ownership, ColorSpace = colorSpace };

    private static AlgorithmImageBuffer Image(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmImageArtifact>(name)!.Image;

    private static double Measurement(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmMeasurementArtifact>("image-comparison")!.Measurements.Single(value => value.Name == name).Value;

    private static AlgorithmImageBuffer Gray8(byte[] values, int width, int height)
        => new(width, height, width, AlgorithmImageFormat.Gray8, values.ToArray());

    private static AlgorithmImageBuffer UShort(ushort[] values, int width, int height, AlgorithmImageFormat format)
        => new(width, height, checked(width * format.BytesPerPixel()), format, MemoryMarshal.AsBytes(values.AsSpan()).ToArray());

    private static AlgorithmImageBuffer Floats(float[] values, int width, int height, AlgorithmImageFormat format)
        => new(width, height, checked(width * format.BytesPerPixel()), format, MemoryMarshal.AsBytes(values.AsSpan()).ToArray());

    private static float[] Floats(AlgorithmImageBuffer image)
        => MemoryMarshal.Cast<byte, float>(image.Data.Span).ToArray();

    private static ushort[] UShorts(AlgorithmImageBuffer image)
        => MemoryMarshal.Cast<byte, ushort>(image.Data.Span).ToArray();

    private static WriteableBitmap Bitmap(byte[] pixels)
    {
        WriteableBitmap bitmap = new(pixels.Length, 1, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, pixels.Length, 1), pixels, pixels.Length, 0);
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
