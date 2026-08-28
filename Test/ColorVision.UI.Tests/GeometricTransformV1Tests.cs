using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.GeometricTransform;
using OpenCvSharp;
using System.Buffers.Binary;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class GeometricTransformV1Tests
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
    public void CatalogDefaultsSchemaAliasesAndPresetRoundTripAreStable()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        AlgorithmDescriptor descriptor = Assert.Single(catalog.Descriptors, value => value.Id == StandardAlgorithmIds.GeometricTransform);
        Assert.Equal(new AlgorithmVersion(1, 0, 0), descriptor.Version);
        Assert.Equal("primary=same-as-input; validity-mask=gray8", descriptor.OutputFormatPolicy);
        Assert.Equal(AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow | AlgorithmHostCapabilities.Headless
            | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic,
            descriptor.Capabilities & (AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow | AlgorithmHostCapabilities.Headless
                | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Copilot));
        Assert.True(catalog.TryResolveAlias("WarpAffine", out AlgorithmDescriptor? alias));
        Assert.Equal(descriptor.Id, alias!.Id);
        GeometricTransformParameters defaults = descriptor.ParameterSchema.Defaults.Deserialize<GeometricTransformParameters>(AlgorithmJson.Options)!;
        Assert.True(defaults.Validate().IsValid);
        Assert.Equal(new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1 }, defaults.Matrix);
        Assert.Contains(descriptor.ParameterSchema.Fields, field => field.Name == nameof(GeometricTransformParameters.MaximumOutputPixels)
            && field.Unit == "px" && field.Minimum == 1);

        GeometricTransformParameters parameters = new() { Kind = GeometricTransformKind.Perspective, M13 = 2.5, M31 = 0.001 };
        AlgorithmParameterPreset preset = AlgorithmParameterPreset.Create("fixture-perspective", descriptor.Id, descriptor.Version, parameters,
            new Dictionary<string, string> { ["source"] = "test" });
        string json = JsonSerializer.Serialize(preset, AlgorithmJson.Options);
        AlgorithmParameterPreset restored = JsonSerializer.Deserialize<AlgorithmParameterPreset>(json, AlgorithmJson.Options)!;
        Assert.True(restored.Validate().IsValid);
        AlgorithmInvocation invocation = restored.ToInvocation();
        Assert.Equal("fixture-perspective", invocation.PresetId);
        Assert.Equal(descriptor.Id, invocation.AlgorithmId);
        Assert.Equal(parameters.M13, invocation.Parameters.Deserialize<GeometricTransformParameters>(AlgorithmJson.Options)!.M13);
        Assert.Equal("test", invocation.Metadata["source"]);
    }

    [Fact]
    public void ParameterValidationRejectsInvalidAffineCanvasAndBudgets()
    {
        GeometricTransformParameters invalid = new()
        {
            Kind = GeometricTransformKind.Affine,
            M31 = 0.1,
            Canvas = GeometricTransformCanvas.ExplicitSize,
            OutputWidth = 0,
            OutputHeight = -1,
            BorderChannel2 = 1.1,
            MaximumOutputPixels = 0,
            MaximumConditionNumber = double.NaN,
        };
        AlgorithmValidationResult validation = invalid.Validate();
        Assert.Contains(validation.Issues, issue => issue.Code == "affine_bottom_row_invalid");
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(GeometricTransformParameters.OutputWidth));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(GeometricTransformParameters.OutputHeight));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(GeometricTransformParameters.BorderChannel2));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(GeometricTransformParameters.MaximumOutputPixels));
        Assert.Contains(validation.Issues, issue => issue.Path == nameof(GeometricTransformParameters.MaximumConditionNumber));
        Assert.Contains(new GeometricTransformParameters { M31 = 1e-15 }.Validate().Issues, issue => issue.Code == "affine_bottom_row_invalid");
    }

    [Fact]
    public void PresetBoundaryRejectsOtherAlgorithmsVersionsAndSchemaVersions()
    {
        JsonElement parameters = AlgorithmJson.ToElement(new GeometricTransformParameters());
        AlgorithmParameterPreset wrongAlgorithm = new()
        {
            PresetId = "wrong-algorithm",
            AlgorithmId = StandardAlgorithmIds.Invert,
            AlgorithmVersion = new AlgorithmVersion(1, 0, 0),
            Parameters = parameters,
        };
        AlgorithmParameterPreset wrongVersion = new()
        {
            PresetId = "wrong-version",
            AlgorithmId = StandardAlgorithmIds.GeometricTransform,
            AlgorithmVersion = new AlgorithmVersion(2, 0, 0),
            Parameters = parameters,
        };
        AlgorithmParameterPreset wrongSchema = new()
        {
            PresetId = "wrong-schema",
            AlgorithmId = StandardAlgorithmIds.GeometricTransform,
            AlgorithmVersion = new AlgorithmVersion(1, 0, 0),
            ParameterSchemaVersion = 2,
            Parameters = parameters,
        };
        AlgorithmParameterPreset missingVersion = new()
        {
            PresetId = "missing-version",
            AlgorithmId = StandardAlgorithmIds.GeometricTransform,
            Parameters = parameters,
        };

        Assert.Throws<InvalidOperationException>(() => GeometricTransformPresetSerializer.Deserialize(JsonSerializer.Serialize(wrongAlgorithm, AlgorithmJson.Options)));
        Assert.Throws<InvalidOperationException>(() => GeometricTransformPresetSerializer.Deserialize(JsonSerializer.Serialize(wrongVersion, AlgorithmJson.Options)));
        Assert.Throws<InvalidOperationException>(() => GeometricTransformPresetSerializer.Deserialize(JsonSerializer.Serialize(wrongSchema, AlgorithmJson.Options)));
        Assert.Throws<InvalidOperationException>(() => GeometricTransformPresetSerializer.Deserialize(JsonSerializer.Serialize(missingVersion, AlgorithmJson.Options)));
    }

    [Theory]
    [MemberData(nameof(CanonicalFormats))]
    public async Task IdentityGoldenPreservesFormatBytesDpiInputAndFullValidity(AlgorithmImageFormat format)
    {
        using AlgorithmImageBuffer input = CreatePattern(4, 3, format, 123, 117);
        byte[] original = input.Data.ToArray();
        using AlgorithmResult result = await RunAsync(input, new GeometricTransformParameters { Interpolation = GeometricTransformInterpolation.Nearest });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmImageBuffer output = Image(result, "transformed-image");
        Assert.Equal(format, output.Format);
        Assert.Equal(4, output.Width);
        Assert.Equal(3, output.Height);
        Assert.Equal(123, output.DpiX);
        Assert.Equal(117, output.DpiY);
        Assert.Equal(original, output.Data.ToArray());
        Assert.Equal(Enumerable.Repeat(byte.MaxValue, 12), Image(result, "valid-region-mask").Data.ToArray());
        Assert.Equal(1, Measurement(result, "transform.valid_fraction"));
        Assert.Equal(0, Measurement(result, "transform.inverse_residual"));
        Assert.Equal(original, input.Data.ToArray());
    }

    [Fact]
    public async Task AffineTranslationUsesExplicitCanvasBorderAndReportsForwardInverseMatrices()
    {
        using AlgorithmImageBuffer input = new(3, 2, 3, AlgorithmImageFormat.Gray8, [1, 2, 3, 4, 5, 6]);
        GeometricTransformParameters parameters = new()
        {
            M13 = 1,
            M23 = 1,
            Canvas = GeometricTransformCanvas.ExplicitSize,
            OutputWidth = 5,
            OutputHeight = 4,
            Interpolation = GeometricTransformInterpolation.Nearest,
            BorderChannel0 = 0.5,
        };
        using AlgorithmResult result = await RunAsync(input, parameters, presetId: "translation");

        Assert.Equal(new byte[]
        {
            128, 128, 128, 128, 128,
            128, 1, 2, 3, 128,
            128, 4, 5, 6, 128,
            128, 128, 128, 128, 128,
        }, Image(result, "transformed-image").Data.ToArray());
        Assert.Equal(new byte[]
        {
            0, 0, 0, 0, 0,
            0, 255, 255, 255, 0,
            0, 255, 255, 255, 0,
            0, 0, 0, 0, 0,
        }, Image(result, "valid-region-mask").Data.ToArray());
        AlgorithmGeometry transform = result.GetArtifact<AlgorithmGeometryArtifact>("geometric-transform")!.Geometries.Single(value => value.Kind == AlgorithmGeometryKind.Transform);
        Assert.Equal(new double[] { 1, 0, 1, 0, 1, 1, 0, 0, 1 }, transform.Matrix);
        AlgorithmTableArtifact table = result.GetArtifact<AlgorithmTableArtifact>("geometric-transform-matrix")!;
        Assert.Equal(-1, table.Rows[0]["InverseM3"].GetDouble());
        Assert.Equal(-1, table.Rows[1]["InverseM3"].GetDouble());
        Assert.InRange(transform.Residual!.Value, 0, 1e-14);
        AlgorithmStructuredDataArtifact structured = result.GetArtifact<AlgorithmStructuredDataArtifact>("geometric-transform")!;
        Assert.Equal("translation", structured.Data.GetProperty("presetId").GetString());
    }

    [Fact]
    public async Task FitBoundsRotationProducesTightCanvasAndInvertibleEffectiveTransform()
    {
        using AlgorithmImageBuffer input = new(3, 2, 3, AlgorithmImageFormat.Gray8, [1, 2, 3, 4, 5, 6]);
        GeometricTransformParameters parameters = new()
        {
            M11 = 0,
            M12 = -1,
            M21 = 1,
            M22 = 0,
            Canvas = GeometricTransformCanvas.FitTransformedBounds,
            Interpolation = GeometricTransformInterpolation.Nearest,
        };
        using AlgorithmResult result = await RunAsync(input, parameters);
        AlgorithmImageBuffer output = Image(result, "transformed-image");
        Assert.Equal(2, output.Width);
        Assert.Equal(3, output.Height);
        Assert.Equal(new byte[] { 4, 1, 5, 2, 6, 3 }, output.Data.ToArray());
        Assert.All(Image(result, "valid-region-mask").Data.ToArray(), value => Assert.Equal(byte.MaxValue, value));
        Assert.InRange(Measurement(result, "transform.inverse_residual"), 0, 1e-14);
        AlgorithmGeometry footprint = result.GetArtifact<AlgorithmGeometryArtifact>("geometric-transform")!.Geometries.Single(value => value.Kind == AlgorithmGeometryKind.Polygon);
        Assert.Equal(new[] { new AlgorithmPoint(1, 0), new AlgorithmPoint(1, 2), new AlgorithmPoint(0, 2), new AlgorithmPoint(0, 0) }, footprint.Points);
    }

    [Fact]
    public async Task PerspectiveMatrixRoundTripsPointsAndMaskUsesInverseMappedPixelCenters()
    {
        using AlgorithmImageBuffer input = CreatePattern(5, 4, AlgorithmImageFormat.Gray16);
        GeometricTransformParameters parameters = new()
        {
            Kind = GeometricTransformKind.Perspective,
            M11 = 1.1,
            M12 = 0.05,
            M13 = 0.4,
            M21 = -0.03,
            M22 = 0.95,
            M23 = 0.2,
            M31 = 0.01,
            M32 = -0.015,
            Canvas = GeometricTransformCanvas.ExplicitSize,
            OutputWidth = 7,
            OutputHeight = 6,
        };
        using AlgorithmResult result = await RunAsync(input, parameters);
        AlgorithmTableArtifact table = result.GetArtifact<AlgorithmTableArtifact>("geometric-transform-matrix")!;
        double[] forward = ReadMatrix(table, "M");
        double[] inverse = ReadMatrix(table, "InverseM");
        AlgorithmPoint point = new(2.25, 1.5);
        AlgorithmPoint mapped = Transform(forward, point);
        AlgorithmPoint restored = Transform(inverse, mapped);
        Assert.InRange(restored.X, point.X - 1e-10, point.X + 1e-10);
        Assert.InRange(restored.Y, point.Y - 1e-10, point.Y + 1e-10);
        Assert.InRange(Measurement(result, "transform.inverse_residual"), 0, 1e-12);
        Assert.InRange(Measurement(result, "transform.valid_fraction"), 0.1, 0.99);
        Assert.Equal("colorvision.geometry.transform/v1", result.GetArtifact<AlgorithmStructuredDataArtifact>("geometric-transform")!.Schema);
    }

    [Theory]
    [InlineData(1e-6)]
    [InlineData(1e-15)]
    public async Task HomogeneousScaleAndLargeAffineTranslationRemainNumericallyValid(double homogeneousScale)
    {
        using AlgorithmImageBuffer perspectiveInput = new(3, 2, 3, AlgorithmImageFormat.Gray8, [1, 2, 3, 4, 5, 6]);
        using AlgorithmResult perspective = await RunAsync(perspectiveInput, new GeometricTransformParameters
        {
            Kind = GeometricTransformKind.Perspective,
            M11 = homogeneousScale,
            M22 = homogeneousScale,
            M33 = homogeneousScale,
            Interpolation = GeometricTransformInterpolation.Nearest,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, perspective.Status);
        Assert.Equal(perspectiveInput.Data.ToArray(), Image(perspective, "transformed-image").Data.ToArray());
        Assert.InRange(Measurement(perspective, "transform.condition_number"), 0.999999, 1.000001);

        using AlgorithmImageBuffer affineInput = new(3, 2, 3, AlgorithmImageFormat.Gray8, [1, 2, 3, 4, 5, 6]);
        using AlgorithmResult affine = await RunAsync(affineInput, new GeometricTransformParameters
        {
            M13 = 100_000,
            M23 = -100_000,
            Canvas = GeometricTransformCanvas.FitTransformedBounds,
            Interpolation = GeometricTransformInterpolation.Nearest,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, affine.Status);
        Assert.Equal(affineInput.Data.ToArray(), Image(affine, "transformed-image").Data.ToArray());
        Assert.InRange(Measurement(affine, "transform.inverse_residual"), 0, 1e-10);
    }

    [Theory]
    [InlineData("singular", "transform_singular")]
    [InlineData("horizon", "transform_crosses_projective_horizon")]
    [InlineData("limit", "transform_output_limit_exceeded")]
    public async Task InvalidRuntimeGeometryReturnsStructuredFailure(string scenario, string expectedCode)
    {
        using AlgorithmImageBuffer input = CreatePattern(4, 3, AlgorithmImageFormat.Gray8);
        GeometricTransformParameters parameters = scenario switch
        {
            "singular" => new GeometricTransformParameters { M11 = 0 },
            "horizon" => new GeometricTransformParameters { Kind = GeometricTransformKind.Perspective, M31 = -0.5, M33 = 1 },
            _ => new GeometricTransformParameters
            {
                Canvas = GeometricTransformCanvas.ExplicitSize,
                OutputWidth = 20,
                OutputHeight = 20,
                MaximumOutputPixels = 100,
            },
        };
        using AlgorithmResult result = await RunAsync(input, parameters);
        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Equal(expectedCode, Assert.Single(result.Failures).Code);
        Assert.Empty(result.Artifacts.OfType<AlgorithmImageArtifact>());
    }

    [Fact]
    public async Task CancellationSuccessAndResultDisposalReleaseOwnedBuffers()
    {
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "transform.mask") cancellation.Cancel();
        });
        AlgorithmImageBuffer cancelledInput = CreatePattern(512, 512, AlgorithmImageFormat.Bgra32);
        using AlgorithmResult cancelled = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.GeometricTransform, new GeometricTransformParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = cancelledInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            Progress = progress,
        }, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, cancelled.Status);
        Assert.True(cancelledInput.IsDisposed);

        AlgorithmImageBuffer successInput = CreatePattern(8, 6, AlgorithmImageFormat.Gray8);
        AlgorithmResult success = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.GeometricTransform, new GeometricTransformParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = successInput, Ownership = AlgorithmInputOwnership.Transferred }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        AlgorithmImageBuffer[] outputs = success.Artifacts.OfType<AlgorithmImageArtifact>().Select(value => value.Image).ToArray();
        Assert.True(successInput.IsDisposed);
        Assert.All(outputs, value => Assert.False(value.IsDisposed));
        success.Dispose();
        Assert.All(outputs, value => Assert.True(value.IsDisposed));
    }

    [Fact]
    public async Task BatchAndLocalFlowReuseTheCatalogInvocation()
    {
        BatchImageAlgorithmDefinition batch = BatchImageAlgorithms.CreateAll().Single(value => value.Descriptor?.Id == StandardAlgorithmIds.GeometricTransform);
        GeometricTransformParameters options = Assert.IsType<GeometricTransformParameters>(batch.Options);
        options.M13 = 1;
        options.Canvas = GeometricTransformCanvas.ExplicitSize;
        options.OutputWidth = 4;
        options.OutputHeight = 2;
        options.Interpolation = GeometricTransformInterpolation.Nearest;
        using Mat source = new(2, 3, MatType.CV_8UC1, Scalar.All(7));
        using Mat output = batch.Apply(source);
        Assert.Equal(4, output.Cols);
        Assert.Equal(2, output.Rows);
        Assert.Equal(0, output.At<byte>(0, 0));
        Assert.Equal(7, output.At<byte>(0, 1));

        LocalFrameMetadata metadata = new() { Width = 3, Height = 2, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
        using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, 6, 0);
        using LocalFlowFrameLease lease = frame.Acquire();
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.GeometricTransform, options);
        using AlgorithmResult flow = await LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(lease, invocation);
        Assert.Equal(4, Image(flow, "transformed-image").Width);
        Assert.Equal(AlgorithmImageFormat.Gray8, Image(flow, "valid-region-mask").Format);
    }

    [Fact]
    public async Task ImageViewCatalogEntryCommitsThroughSessionAndResultWindowDisplaysAndReleasesArtifacts()
    {
        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureResources();
            ImageView view = new();
            WriteableBitmap bitmap = new(3, 2, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new Int32Rect(0, 0, 3, 2), new byte[] { 1, 2, 3, 4, 5, 6 }, 3, 0);
            view.SetImageSource(bitmap, enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });
        AlgorithmResult? result = null;
        try
        {
            ImageProcessingContext context = imageView.EditorContext.ProcessingContext;
            WpfTestHost.Invoke(() =>
            {
                string?[] ids = new AlgorithmsContextMenu(context).GetContextMenuItems().Select(value => value.GuidId).ToArray();
                Assert.Single(ids, value => value == "GeometricTransform");
            });
            long revision = context.ImageRevision;
            GeometricTransformParameters parameters = new()
            {
                M13 = 1,
                Canvas = GeometricTransformCanvas.ExplicitSize,
                OutputWidth = 4,
                OutputHeight = 2,
                Interpolation = GeometricTransformInterpolation.Nearest,
            };
            Task<AlgorithmResult> apply = WpfTestHost.Invoke(() => ImageAlgorithmApplier.ApplyAsync(
                context,
                AlgorithmInvocation.Create(StandardAlgorithmIds.GeometricTransform, parameters)));
            result = await apply;
            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
            Assert.Equal(revision + 1, context.ImageRevision);
            WpfTestHost.Invoke(() =>
            {
                BitmapSource committed = Assert.IsAssignableFrom<BitmapSource>(context.ViewBitmapSource);
                Assert.Equal(4, committed.PixelWidth);
                Assert.Equal(2, committed.PixelHeight);
                GeometricTransformResultWindow window = new(result);
                window.Show();
                System.Windows.Controls.Image transformed = Assert.IsType<System.Windows.Controls.Image>(window.FindName("TransformedPreview"));
                System.Windows.Controls.Image mask = Assert.IsType<System.Windows.Controls.Image>(window.FindName("MaskPreview"));
                DataGrid grid = Assert.IsType<DataGrid>(window.FindName("MatrixGrid"));
                Assert.Equal(4, Assert.IsAssignableFrom<BitmapSource>(transformed.Source).PixelWidth);
                Assert.Equal(PixelFormats.Gray8, Assert.IsAssignableFrom<BitmapSource>(mask.Source).Format);
                Assert.NotNull(grid.ItemsSource);
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

    private static async Task<AlgorithmResult> RunAsync(AlgorithmImageBuffer input, GeometricTransformParameters parameters, string? presetId = null)
    {
        AlgorithmInvocation baseInvocation = AlgorithmInvocation.Create(StandardAlgorithmIds.GeometricTransform, parameters);
        AlgorithmInvocation invocation = new()
        {
            InvocationId = baseInvocation.InvocationId,
            AlgorithmId = baseInvocation.AlgorithmId,
            ParameterSchemaVersion = baseInvocation.ParameterSchemaVersion,
            Parameters = baseInvocation.Parameters,
            PresetId = presetId,
        };
        return await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = invocation,
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
    }

    private static AlgorithmImageBuffer Image(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmImageArtifact>(name)!.Image;

    private static double Measurement(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmMeasurementArtifact>("geometric-transform-summary")!.Measurements.Single(value => value.Name == name).Value;

    private static AlgorithmImageBuffer CreatePattern(int width, int height, AlgorithmImageFormat format, double dpiX = 96, double dpiY = 96)
    {
        int stride = width * format.BytesPerPixel();
        byte[] data = new byte[stride * height];
        int samples = width * height * format.Channels();
        for (int index = 0; index < samples; index++)
        {
            int offset = index * format.BitsPerChannel() / 8;
            if (format.BitsPerChannel() == 8) data[offset] = (byte)(index * 17 % 251);
            else if (format.BitsPerChannel() == 16) BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), (ushort)(index * 1009 % 65521));
            else BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset, 4), index / 17f);
        }
        return new AlgorithmImageBuffer(width, height, stride, format, data, dpiX, dpiY);
    }

    private static double[] ReadMatrix(AlgorithmTableArtifact table, string prefix)
        => table.Rows.SelectMany(row => Enumerable.Range(1, 3).Select(column => row[$"{prefix}{column}"].GetDouble())).ToArray();

    private static AlgorithmPoint Transform(double[] matrix, AlgorithmPoint point)
    {
        double denominator = matrix[6] * point.X + matrix[7] * point.Y + matrix[8];
        return new AlgorithmPoint(
            (matrix[0] * point.X + matrix[1] * point.Y + matrix[2]) / denominator,
            (matrix[3] * point.X + matrix[4] * point.Y + matrix[5]) / denominator);
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
