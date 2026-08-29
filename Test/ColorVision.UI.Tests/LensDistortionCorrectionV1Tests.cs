using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.LensDistortionCorrection;
using OpenCvSharp;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class LensDistortionCorrectionV1Tests
{
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

    [Fact]
    public void CatalogDefaultsAliasesHostsAndPresetBoundaryAreStable()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        AlgorithmDescriptor descriptor = Assert.Single(catalog.Descriptors, value => value.Id == StandardAlgorithmIds.LensDistortionCorrection);
        Assert.Equal(new AlgorithmVersion(1, 0, 0), descriptor.Version);
        Assert.Equal("primary=same-as-input; canvas=same-as-input; validity-mask=gray8", descriptor.OutputFormatPolicy);
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Interactive));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Batch));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Flow));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Headless));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Local));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Deterministic));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Copilot));
        Assert.True(catalog.TryResolveAlias("Undistort", out AlgorithmDescriptor? alias));
        Assert.Equal(descriptor.Id, alias!.Id);
        LensDistortionCorrectionParameters defaults = descriptor.ParameterSchema.Defaults.Deserialize<LensDistortionCorrectionParameters>(AlgorithmJson.Options)!;
        Assert.True(defaults.Validate().IsValid);
        Assert.Contains(descriptor.ParameterSchema.Fields, field => field.Name == nameof(LensDistortionCorrectionParameters.FxPixels)
            && field.Unit == "px" && field.Minimum == 0.000001);
        Assert.Contains(BatchImageAlgorithms.CreateAll(catalog), item => item.Descriptor?.Id == descriptor.Id);

        LensDistortionCorrectionParameters parameters = new()
        {
            FxPixels = 812.5,
            FyPixels = 809.25,
            K1 = -0.18,
            CalibrationSource = "camera-fixture-17",
            CalibrationVersion = "2026-08-28",
            CalibrationChecksum = "sha256:fixture",
        };
        string json = LensDistortionCorrectionPresetSerializer.Serialize("lens-fixture", parameters);
        (string presetId, LensDistortionCorrectionParameters restored) = LensDistortionCorrectionPresetSerializer.Deserialize(json);
        Assert.Equal("lens-fixture", presetId);
        Assert.Equal(parameters.FxPixels, restored.FxPixels);
        Assert.Equal(parameters.K1, restored.K1);
        Assert.Equal(parameters.CalibrationChecksum, restored.CalibrationChecksum);

        AlgorithmParameterPreset wrongVersion = new()
        {
            PresetId = "wrong-version",
            AlgorithmId = descriptor.Id,
            AlgorithmVersion = new AlgorithmVersion(2, 0, 0),
            Parameters = AlgorithmJson.ToElement(parameters),
        };
        Assert.Throws<InvalidOperationException>(() => LensDistortionCorrectionPresetSerializer.Deserialize(JsonSerializer.Serialize(wrongVersion, AlgorithmJson.Options)));
    }

    [Fact]
    public void ValidationRejectsInvalidIntrinsicsCoefficientsProvenanceAndQuality()
    {
        LensDistortionCorrectionParameters invalid = new()
        {
            FxPixels = 0,
            FyPixels = double.NaN,
            PrincipalPointMode = LensDistortionPrincipalPointMode.Explicit,
            PrincipalPointX = double.PositiveInfinity,
            K1 = 1_000_001,
            OptimalAlpha = -0.1,
            MinimumValidFraction = 1.1,
            CalibrationSource = string.Empty,
            CalibrationVersion = string.Empty,
            CalibrationChecksum = null!,
            HasCalibrationQuality = false,
            CalibrationRmsErrorPixels = 1,
        };
        AlgorithmValidationResult validation = invalid.Validate();
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(LensDistortionCorrectionParameters.FxPixels));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(LensDistortionCorrectionParameters.FyPixels));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(LensDistortionCorrectionParameters.PrincipalPointX));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(LensDistortionCorrectionParameters.K1));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(LensDistortionCorrectionParameters.OptimalAlpha));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(LensDistortionCorrectionParameters.MinimumValidFraction));
        Assert.Contains(validation.Issues, issue => issue.Code == "invalid_calibration_source");
        Assert.Contains(validation.Issues, issue => issue.Code == "invalid_calibration_version");
        Assert.Contains(validation.Issues, issue => issue.Code == "invalid_calibration_checksum");
        Assert.Contains(validation.Issues, issue => issue.Code == "calibration_quality_flag_required");
    }

    [Theory]
    [MemberData(nameof(CanonicalFormats))]
    public async Task ZeroDistortionGoldenPreservesCanonicalFormatBytesDpiAndInput(AlgorithmImageFormat format)
    {
        using AlgorithmImageBuffer input = Pattern(7, 5, format, 121, 119);
        byte[] before = input.Data.ToArray();
        using AlgorithmResult result = await RunAsync(input, new LensDistortionCorrectionParameters());

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmImageBuffer output = Image(result, "corrected-image");
        Assert.Equal(format, output.Format);
        Assert.Equal(input.Width, output.Width);
        Assert.Equal(input.Height, output.Height);
        Assert.Equal(121, output.DpiX);
        Assert.Equal(119, output.DpiY);
        Assert.Equal(before, output.Data.ToArray());
        Assert.Equal(before, input.Data.ToArray());
        Assert.All(Image(result, "valid-region-mask").Data.ToArray(), value => Assert.Equal(byte.MaxValue, value));
        Assert.Equal(1, Measurement(result, "lens-distortion.valid_fraction"));
        Assert.Equal(0, Measurement(result, "lens-distortion.maximum_displacement"));
        Assert.Equal("colorvision.geometry.lens-distortion-correction/v1", result.GetArtifact<AlgorithmStructuredDataArtifact>("lens-distortion-correction")!.Schema);
    }

    [Fact]
    public async Task BrownConradyNearestGoldenUsesPixelCenterCameraCoordinates()
    {
        const int width = 64;
        const int height = 48;
        byte[] bytes = new byte[width * height * sizeof(ushort)];
        for (int index = 0; index < width * height; index++)
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2, 2), (ushort)index);
        using AlgorithmImageBuffer input = new(width, height, width * 2, AlgorithmImageFormat.Gray16, bytes);
        LensDistortionCorrectionParameters parameters = new()
        {
            FxPixels = 50,
            FyPixels = 47,
            PrincipalPointMode = LensDistortionPrincipalPointMode.Explicit,
            PrincipalPointX = 31.5,
            PrincipalPointY = 23.5,
            K1 = -0.2,
            K2 = 0.035,
            P1 = 0.002,
            P2 = -0.0015,
            K3 = -0.004,
            Interpolation = GeometricTransformInterpolation.Nearest,
            MinimumValidFraction = 0,
        };
        using AlgorithmResult result = await RunAsync(input, parameters, presetId: "brown-golden");
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmImageBuffer output = Image(result, "corrected-image");
        foreach ((int x, int y) in new[] { (0, 0), (8, 10), (31, 23), (51, 36), (63, 47) })
        {
            (double sourceX, double sourceY) = BrownMap(parameters, x, y);
            int expectedX = (int)Math.Round(sourceX, MidpointRounding.AwayFromZero);
            int expectedY = (int)Math.Round(sourceY, MidpointRounding.AwayFromZero);
            ushort actual = BinaryPrimitives.ReadUInt16LittleEndian(output.Data.Span.Slice((y * width + x) * 2, 2));
            ushort expected = expectedX >= 0 && expectedY >= 0 && expectedX < width && expectedY < height
                ? (ushort)(expectedY * width + expectedX)
                : (ushort)0;
            Assert.Equal(expected, actual);
        }
        Assert.True(Measurement(result, "lens-distortion.maximum_displacement") > 1);
        AlgorithmStructuredDataArtifact structured = result.GetArtifact<AlgorithmStructuredDataArtifact>("lens-distortion-correction")!;
        Assert.Equal("brown-golden", structured.Data.GetProperty("presetId").GetString());
        Assert.Equal("pixel-center coordinates; undistorted output pixel maps through the Brown-Conrady model into distorted source pixels",
            structured.Data.GetProperty("matrixSemantics").GetString());
    }

    [Fact]
    public async Task OptimalCameraMatrixReturnsDistinctMatrixAndExplicitValidityDiagnostics()
    {
        using AlgorithmImageBuffer input = Pattern(160, 120, AlgorithmImageFormat.Bgr24);
        using AlgorithmResult result = await RunAsync(input, new LensDistortionCorrectionParameters
        {
            FxPixels = 110,
            FyPixels = 108,
            K1 = -0.28,
            K2 = 0.07,
            OutputCameraMode = LensDistortionOutputCameraMode.OptimalNewCameraMatrix,
            OptimalAlpha = 1,
            CenterOptimalPrincipalPoint = true,
            MinimumValidFraction = 0,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmTableArtifact matrices = result.GetArtifact<AlgorithmTableArtifact>("lens-distortion-camera-matrices")!;
        Assert.NotEqual(matrices.Rows[0]["InputM1"].GetDouble(), matrices.Rows[0]["OutputM1"].GetDouble());
        Assert.InRange(Measurement(result, "lens-distortion.valid_fraction"), 0.01, 1);
        Assert.NotNull(result.GetArtifact<AlgorithmGeometryArtifact>("lens-distortion-valid-region"));
        Assert.Equal(AlgorithmImageFormat.Gray8, Image(result, "valid-region-mask").Format);
    }

    [Fact]
    public async Task RoiAndDegenerateValidityAreStructuredFailures()
    {
        using AlgorithmImageBuffer roiInput = Pattern(32, 24, AlgorithmImageFormat.Gray8);
        using AlgorithmResult roi = await RunAsync(roiInput, new LensDistortionCorrectionParameters(), new RectangleAlgorithmRoi(0, 0, 10, 10));
        Assert.Contains(roi.Failures, failure => failure.Code == "roi_kind_unsupported");

        using AlgorithmImageBuffer invalidInput = Pattern(32, 24, AlgorithmImageFormat.Gray8);
        using AlgorithmResult invalid = await RunAsync(invalidInput, new LensDistortionCorrectionParameters
        {
            FxPixels = 1,
            FyPixels = 1,
            PrincipalPointMode = LensDistortionPrincipalPointMode.Explicit,
            PrincipalPointX = 1_000_000,
            PrincipalPointY = 1_000_000,
            K1 = 1,
            MinimumValidFraction = 0.5,
        });
        Assert.Equal(AlgorithmResultStatus.Failed, invalid.Status);
        Assert.Contains(invalid.Failures, failure => failure.Code == "lens_distortion_valid_fraction_too_low");
        Assert.Empty(invalid.Artifacts.OfType<AlgorithmImageArtifact>());
    }

    [Fact]
    public async Task CancellationAndTransferredOwnershipReleaseAllBuffers()
    {
        using CancellationTokenSource preCancellation = new();
        preCancellation.Cancel();
        using AlgorithmImageBuffer preCancelledInput = Pattern(16, 12, AlgorithmImageFormat.Gray8);
        using AlgorithmResult preCancelled = await RunAsync(
            preCancelledInput,
            new LensDistortionCorrectionParameters(),
            cancellationToken: preCancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, preCancelled.Status);

        AlgorithmImageBuffer cancelledInput = Pattern(512, 384, AlgorithmImageFormat.Bgra32);
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "lens-distortion.mask") cancellation.Cancel();
        });
        using AlgorithmResult cancelled = await RunTransferredAsync(cancelledInput, new LensDistortionCorrectionParameters
        {
            FxPixels = 350,
            FyPixels = 350,
            K1 = -0.2,
        }, progress, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, cancelled.Status);
        Assert.True(cancelledInput.IsDisposed);

        AlgorithmImageBuffer successInput = Pattern(32, 24, AlgorithmImageFormat.Gray16);
        AlgorithmResult success = await RunTransferredAsync(successInput, new LensDistortionCorrectionParameters());
        AlgorithmImageBuffer[] outputs = success.Artifacts.OfType<AlgorithmImageArtifact>().Select(value => value.Image).ToArray();
        Assert.True(successInput.IsDisposed);
        Assert.All(outputs, value => Assert.False(value.IsDisposed));
        success.Dispose();
        Assert.All(outputs, value => Assert.True(value.IsDisposed));

        AlgorithmImageBuffer failedInput = Pattern(16, 12, AlgorithmImageFormat.Gray8);
        using AlgorithmResult failed = await RunTransferredAsync(failedInput, new LensDistortionCorrectionParameters { FxPixels = 0 });
        Assert.Equal(AlgorithmResultStatus.Failed, failed.Status);
        Assert.True(failedInput.IsDisposed);
    }

    [Fact]
    public async Task BatchAndFlowUseTheSameCatalogParametersAndProvider()
    {
        BatchImageAlgorithmDefinition batch = BatchImageAlgorithms.CreateAll().Single(value => value.Descriptor?.Id == StandardAlgorithmIds.LensDistortionCorrection);
        LensDistortionCorrectionParameters batchParameters = Assert.IsType<LensDistortionCorrectionParameters>(batch.Options);
        batchParameters.FxPixels = 50;
        batchParameters.FyPixels = 50;
        batchParameters.K1 = -0.15;
        using Mat source = new(24, 32, MatType.CV_8UC1, Scalar.All(17));
        using Mat output = batch.Apply(source);
        Assert.Equal(source.Size(), output.Size());
        Assert.Equal(source.Type(), output.Type());

        LocalFrameMetadata metadata = new() { Width = 32, Height = 24, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
        using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, 32 * 24, 0);
        using LocalFlowFrameLease lease = frame.Acquire();
        byte[] pixels = Enumerable.Range(0, 32 * 24).Select(index => (byte)(index * 31 & 0xff)).ToArray();
        Marshal.Copy(pixels, 0, lease.RawPointer, pixels.Length);
        using AlgorithmResult flow = await LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(
            lease,
            AlgorithmInvocation.Create(StandardAlgorithmIds.LensDistortionCorrection, batchParameters));
        Assert.Equal(AlgorithmResultStatus.Succeeded, flow.Status);
        Assert.Equal(AlgorithmImageFormat.Gray8, Image(flow, "corrected-image").Format);
        Assert.Equal(AlgorithmImageFormat.Gray8, Image(flow, "valid-region-mask").Format);
    }

    [Fact]
    public async Task ImageViewEntryCommitsAndResultWindowDisplaysArtifactsAndReleasesThem()
    {
        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureResources();
            ImageView view = new();
            WriteableBitmap bitmap = new(32, 24, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, 32, 24), Enumerable.Range(0, 32 * 24).Select(index => (byte)(index & 0xff)).ToArray(), 32, 0);
            view.SetImageSource(bitmap, enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });
        AlgorithmResult? result = null;
        try
        {
            ImageProcessingContext context = imageView.EditorContext.ProcessingContext;
            WpfTestHost.Invoke(() => Assert.Single(new AlgorithmsContextMenu(context).GetContextMenuItems(), item => item.GuidId == "LensDistortionCorrection"));
            long revision = context.ImageRevision;
            result = await WpfTestHost.Invoke(() => ImageAlgorithmApplier.ApplyAsync(
                context,
                AlgorithmInvocation.Create(StandardAlgorithmIds.LensDistortionCorrection, new LensDistortionCorrectionParameters())));
            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
            Assert.Equal(revision + 1, context.ImageRevision);
            WpfTestHost.Invoke(() =>
            {
                LensDistortionCorrectionResultWindow window = new(result);
                window.Show();
                Assert.Equal(32, Assert.IsAssignableFrom<BitmapSource>(Assert.IsType<System.Windows.Controls.Image>(window.FindName("CorrectedPreview")).Source).PixelWidth);
                Assert.Equal(PixelFormats.Gray8, Assert.IsAssignableFrom<BitmapSource>(Assert.IsType<System.Windows.Controls.Image>(window.FindName("MaskPreview")).Source).Format);
                Assert.NotNull(Assert.IsType<DataGrid>(window.FindName("MatrixGrid")).ItemsSource);
                Assert.NotNull(Assert.IsType<DataGrid>(window.FindName("CoefficientGrid")).ItemsSource);
                window.Close();
                Assert.True(result.IsDisposed);
            });
        }
        finally
        {
            result?.Dispose();
            WpfTestHost.Invoke(imageView.Dispose);
        }
    }

    [Fact]
    public async Task StructuredResultExportsWithoutOverwriting()
    {
        using AlgorithmImageBuffer input = Pattern(20, 16, AlgorithmImageFormat.Gray8);
        using AlgorithmResult result = await RunAsync(input, new LensDistortionCorrectionParameters
        {
            CalibrationSource = "lab-camera-5",
            CalibrationVersion = "v7",
            CalibrationChecksum = "sha256:abc",
            HasCalibrationQuality = true,
            CalibrationRmsErrorPixels = 0.14,
            CalibrationConfidence = 0.97,
        });
        string path = Path.Combine(Path.GetTempPath(), $"colorvision-lens-distortion-{Guid.NewGuid():N}.json");
        try
        {
            Assert.Equal(path, AlgorithmResultExporter.ExportJson(result, path));
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(StandardAlgorithmIds.LensDistortionCorrection.ToString(), document.RootElement.GetProperty("algorithmId").GetString());
            Assert.Contains("lab-camera-5", File.ReadAllText(path), StringComparison.Ordinal);
            Assert.Throws<IOException>(() => AlgorithmResultExporter.ExportJson(result, path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static async Task<AlgorithmResult> RunAsync(
        AlgorithmImageBuffer input,
        LensDistortionCorrectionParameters parameters,
        AlgorithmRoi? roi = null,
        string? presetId = null,
        CancellationToken cancellationToken = default)
    {
        AlgorithmInvocation baseInvocation = AlgorithmInvocation.Create(StandardAlgorithmIds.LensDistortionCorrection, parameters);
        AlgorithmInvocation invocation = new()
        {
            InvocationId = baseInvocation.InvocationId,
            AlgorithmId = baseInvocation.AlgorithmId,
            ParameterSchemaVersion = baseInvocation.ParameterSchemaVersion,
            Parameters = baseInvocation.Parameters,
            Roi = roi,
            PresetId = presetId,
        };
        return await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = invocation,
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        }, cancellationToken);
    }

    private static async Task<AlgorithmResult> RunTransferredAsync(
        AlgorithmImageBuffer input,
        LensDistortionCorrectionParameters parameters,
        IProgress<AlgorithmProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.LensDistortionCorrection, parameters),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            Progress = progress,
        }, cancellationToken);

    private static (double X, double Y) BrownMap(LensDistortionCorrectionParameters parameters, double outputX, double outputY)
    {
        double x = (outputX - parameters.PrincipalPointX) / parameters.FxPixels;
        double y = (outputY - parameters.PrincipalPointY) / parameters.FyPixels;
        double r2 = x * x + y * y;
        double r4 = r2 * r2;
        double r6 = r4 * r2;
        double radial = (1 + parameters.K1 * r2 + parameters.K2 * r4 + parameters.K3 * r6)
            / (1 + parameters.K4 * r2 + parameters.K5 * r4 + parameters.K6 * r6);
        double distortedX = x * radial + 2 * parameters.P1 * x * y + parameters.P2 * (r2 + 2 * x * x);
        double distortedY = y * radial + parameters.P1 * (r2 + 2 * y * y) + 2 * parameters.P2 * x * y;
        return (parameters.FxPixels * distortedX + parameters.PrincipalPointX, parameters.FyPixels * distortedY + parameters.PrincipalPointY);
    }

    private static AlgorithmImageBuffer Image(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmImageArtifact>(name)!.Image;

    private static double Measurement(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmMeasurementArtifact>("lens-distortion-summary")!.Measurements.Single(value => value.Name == name).Value;

    private static AlgorithmImageBuffer Pattern(int width, int height, AlgorithmImageFormat format, double dpiX = 96, double dpiY = 96)
    {
        int stride = width * format.BytesPerPixel();
        byte[] data = new byte[stride * height];
        int samples = width * height * format.Channels();
        for (int index = 0; index < samples; index++)
        {
            int offset = index * format.BitsPerChannel() / 8;
            if (format.BitsPerChannel() == 8) data[offset] = (byte)(index * 17 % 251);
            else if (format.BitsPerChannel() == 16) BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), (ushort)(index * 1009 % 65521));
            else BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset, 4), index / 37f);
        }
        return new AlgorithmImageBuffer(width, height, stride, format, data, dpiX, dpiY);
    }

    private static void EnsureResources()
    {
        Application application = Application.Current ?? new Application();
        application.Resources["TextBox.Small"] = new Style(typeof(TextBox));
        application.Resources["ComboBox.Small"] = new Style(typeof(ComboBox));
        application.Resources["ToolBarBaseStyle"] = new Style(typeof(ToolBar));
        application.Resources["ToolBarImage"] = new Style(typeof(System.Windows.Controls.Image));
        application.Resources["BaseStyle"] = new Style(typeof(Control));
        application.Resources["RangeSliderBaseStyle"] = new Style(typeof(HandyControl.Controls.RangeSlider));
        application.Resources["bool2VisibilityConverter"] = new BooleanToVisibilityConverter();
    }

    private sealed class InlineProgress(Action<AlgorithmProgress> report) : IProgress<AlgorithmProgress>
    {
        public void Report(AlgorithmProgress value) => report(value);
    }
}
