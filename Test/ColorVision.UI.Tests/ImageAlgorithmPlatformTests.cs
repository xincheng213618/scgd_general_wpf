using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using FlowEngineLib.Algorithm;
using OpenCvSharp;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ImageAlgorithmPlatformTests
{
    [Fact]
    public void CatalogHasUniqueStableIdentitiesVersionsAndCompatibilityAliases()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        Assert.Equal(28, catalog.Descriptors.Count);
        Assert.Equal(catalog.Descriptors.Count, catalog.Descriptors.Select(item => item.Id).Distinct().Count());
        IReadOnlyDictionary<AlgorithmId, AlgorithmVersion> expectedVersions = new Dictionary<AlgorithmId, AlgorithmVersion>
        {
            [StandardAlgorithmIds.Invert] = new(1, 0, 0),
            [StandardAlgorithmIds.Canny] = new(1, 0, 0),
            [StandardAlgorithmIds.BasicAdjustment] = new(1, 0, 0),
            [StandardAlgorithmIds.Threshold] = new(1, 1, 0),
            [StandardAlgorithmIds.Sharpen] = new(1, 0, 0),
            [StandardAlgorithmIds.GaussianBlur] = new(1, 0, 0),
            [StandardAlgorithmIds.MedianBlur] = new(1, 0, 0),
            [StandardAlgorithmIds.Morphology] = new(1, 0, 0),
            [StandardAlgorithmIds.Denoise] = new(1, 1, 0),
            [StandardAlgorithmIds.AutoLevels] = new(1, 0, 0),
            [StandardAlgorithmIds.WhiteBalance] = new(1, 0, 0),
            [StandardAlgorithmIds.HistogramEqualization] = new(1, 0, 0),
            [StandardAlgorithmIds.RemoveMoire] = new(1, 0, 0),
            [StandardAlgorithmIds.PseudoColor] = new(1, 0, 0),
            [StandardAlgorithmIds.RoiStatistics] = new(1, 0, 0),
            [StandardAlgorithmIds.ImageProfile] = new(1, 1, 0),
            [StandardAlgorithmIds.ImageComparison] = new(1, 1, 0),
            [StandardAlgorithmIds.BlobComponents] = new(1, 0, 0),
            [StandardAlgorithmIds.Contours] = new(1, 0, 0),
            [StandardAlgorithmIds.SubpixelEdge] = new(1, 0, 0),
            [StandardAlgorithmIds.LineFit] = new(1, 0, 0),
            [StandardAlgorithmIds.CircleFit] = new(1, 0, 0),
            [StandardAlgorithmIds.GeometricTransform] = new(1, 0, 0),
            [StandardAlgorithmIds.ImageRegistration] = new(1, 0, 0),
            [StandardAlgorithmIds.LensDistortionCorrection] = new(1, 0, 0),
            [StandardAlgorithmIds.ImagingCorrection] = new(1, 0, 0),
            [StandardAlgorithmIds.FrequencySpectrum] = new(1, 0, 0),
            [StandardAlgorithmIds.MoireAnalysis] = new(1, 0, 0),
        };
        Assert.Equal(expectedVersions.Keys.OrderBy(id => id.Value), catalog.Descriptors.Select(item => item.Id).OrderBy(id => id.Value));
        Assert.All(catalog.Descriptors, descriptor => Assert.Equal(expectedVersions[descriptor.Id], descriptor.Version));
        Assert.All(catalog.Descriptors, descriptor =>
        {
            Assert.NotNull(descriptor.OutputFormats);
            if (descriptor.OutputFormatPolicy == "no-image-output") Assert.Empty(descriptor.OutputFormats!);
            else Assert.NotEmpty(descriptor.OutputFormats!);
            Assert.False(string.IsNullOrWhiteSpace(descriptor.OutputFormatPolicy));
        });

        string[] aliases =
        [
            "InvertImage", "EdgeDetection", "BasicAdjustment", "Threshold", "Sharpen", "GaussianBlur",
            "MedianBlur", "Erode", "Dilate", "MorphologyEx", "BilateralFilter", "Blur", "AutoLevelsAdjust",
            "WhiteBalance", "HistogramEqualization", "RemoveMoire", "PseudoColor", "RoiStatistics", "ImageProfile", "ImageComparison", "ConnectedComponents", "FindContours", "SubpixelEdge", "LineFit", "CircleFit", "GeometricTransform", "ImageRegistration", "LensDistortionCorrection", "ImagingCorrection", "FrequencySpectrum", "MoireAnalysis",
        ];
        Assert.All(aliases, alias => Assert.True(catalog.TryResolveAlias(alias, out _), alias));

        AlgorithmDescriptor[] copilot = catalog.Descriptors
            .Where(descriptor => descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Copilot))
            .ToArray();
        Assert.NotEmpty(copilot);
        Assert.All(copilot, descriptor =>
        {
            Assert.True(StandardAlgorithmCatalog.IsExplicitlyAllowedForCopilot(descriptor.Id));
            const AlgorithmHostCapabilities required = AlgorithmHostCapabilities.Headless
                | AlgorithmHostCapabilities.Local
                | AlgorithmHostCapabilities.Deterministic;
            Assert.Equal(required, descriptor.Capabilities & required);
        });
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.RemoveMoire);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.RoiStatistics);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.ImageProfile);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.ImageComparison);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.BlobComponents);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.Contours);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.SubpixelEdge);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.LineFit);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.CircleFit);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.GeometricTransform);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.ImageRegistration);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.LensDistortionCorrection);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.ImagingCorrection);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.FrequencySpectrum);
        Assert.DoesNotContain(copilot, descriptor => descriptor.Id == StandardAlgorithmIds.MoireAnalysis);

        AlgorithmId[] expectedBatchOrder =
        [
            StandardAlgorithmIds.Invert,
            StandardAlgorithmIds.PseudoColor,
            StandardAlgorithmIds.AutoLevels,
            StandardAlgorithmIds.WhiteBalance,
            StandardAlgorithmIds.BasicAdjustment,
            StandardAlgorithmIds.Threshold,
            StandardAlgorithmIds.Sharpen,
            StandardAlgorithmIds.GaussianBlur,
            StandardAlgorithmIds.MedianBlur,
            StandardAlgorithmIds.Canny,
            StandardAlgorithmIds.HistogramEqualization,
            StandardAlgorithmIds.Morphology,
            StandardAlgorithmIds.Denoise,
            StandardAlgorithmIds.GeometricTransform,
            StandardAlgorithmIds.LensDistortionCorrection,
            StandardAlgorithmIds.ImagingCorrection,
        ];
        Assert.Equal(expectedBatchOrder, BatchImageAlgorithms.CreateAll().Skip(1).Select(item => item.Descriptor!.Id));
    }

    [Fact]
    public void PublicContractAssemblyHasNoUiNativeDeviceOrFlowDependency()
    {
        string[] forbidden = ["Presentation", "WindowsBase", "OpenCvSharp", "ColorVision.Core", "MQTT", "ST.Library", "FlowEngineLib"];
        string[] references = typeof(AlgorithmId).Assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty).ToArray();
        Assert.DoesNotContain(references, reference => forbidden.Any(item => reference.Contains(item, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void CatalogDefaultsValidateAndRoundTripThroughJson()
    {
        foreach (AlgorithmDescriptor descriptor in StandardAlgorithmCatalog.Create().Descriptors)
        {
            IAlgorithmParameters defaults = Assert.IsAssignableFrom<IAlgorithmParameters>(
                descriptor.ParameterSchema.Defaults.Deserialize(descriptor.ParameterType, AlgorithmJson.Options));
            Assert.True(defaults.Validate().IsValid, descriptor.Id.Value);

            JsonElement roundTrip = JsonSerializer.SerializeToElement(defaults, descriptor.ParameterType, AlgorithmJson.Options);
            IAlgorithmParameters restored = Assert.IsAssignableFrom<IAlgorithmParameters>(
                roundTrip.Deserialize(descriptor.ParameterType, AlgorithmJson.Options));
            Assert.True(restored.Validate().IsValid, descriptor.Id.Value);
            Assert.Equal(descriptor.ParameterSchema.Defaults.ToString(), roundTrip.ToString());
            Assert.False(descriptor.ParameterSchema.Defaults.TryGetProperty("schemaVersion", out _));
        }

        AlgorithmDescriptor canny = StandardAlgorithmCatalog.Create().Descriptors.Single(item => item.Id == StandardAlgorithmIds.Canny);
        AlgorithmParameterField low = canny.ParameterSchema.Fields.Single(field => field.Name == nameof(CannyParameters.LowThreshold));
        AlgorithmParameterField aperture = canny.ParameterSchema.Fields.Single(field => field.Name == nameof(CannyParameters.ApertureSize));
        Assert.Equal(0, low.Minimum);
        Assert.Equal(255, low.Maximum);
        Assert.Equal(new[] { "3", "5", "7" }, aperture.AllowedValues);

        AlgorithmDescriptor adjustment = StandardAlgorithmCatalog.Create().Descriptors.Single(item => item.Id == StandardAlgorithmIds.BasicAdjustment);
        AlgorithmParameterField exposure = adjustment.ParameterSchema.Fields.Single(field => field.Name == nameof(BasicAdjustmentParameters.Exposure));
        Assert.Equal("EV", exposure.Unit);
        Assert.Equal(-5, exposure.Minimum);
        Assert.Equal(5, exposure.Maximum);

        Assert.True(BatchImageAlgorithms.TryCreateForCopilot(
            StandardAlgorithmIds.Invert.Value,
            parameters: null,
            out BatchImageAlgorithmDefinition? defaulted,
            out string error), error);
        Assert.IsType<NoAlgorithmParameters>(defaulted!.Options);
    }

    [Fact]
    public void StableIdentityJsonAcceptsLegacyObjectShapesAndWritesCanonicalStrings()
    {
        AlgorithmId id = JsonSerializer.Deserialize<AlgorithmId>("{\"Value\":\"ColorVision.Image.Invert\"}", AlgorithmJson.Options);
        AlgorithmVersion version = JsonSerializer.Deserialize<AlgorithmVersion>("{\"Major\":1,\"Minor\":2,\"Patch\":3}", AlgorithmJson.Options);

        Assert.Equal(StandardAlgorithmIds.Invert, id);
        Assert.Equal(new AlgorithmVersion(1, 2, 3), version);
        Assert.Equal("\"colorvision.image.invert\"", JsonSerializer.Serialize(id, AlgorithmJson.Options));
        Assert.Equal("\"1.2.3\"", JsonSerializer.Serialize(version, AlgorithmJson.Options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AlgorithmVersion(-1, 0, 0));
    }

    [Fact]
    public void InvocationRoiAndPhysicalCoordinatesRoundTrip()
    {
        AlgorithmInvocation invocation = new()
        {
            AlgorithmId = StandardAlgorithmIds.Canny,
            AlgorithmVersion = new AlgorithmVersion(1, 0, 0),
            ParameterSchemaVersion = 1,
            Parameters = AlgorithmJson.ToElement(new CannyParameters()),
            Roi = new PolygonAlgorithmRoi([new(1, 2), new(8, 3), new(4, 9)])
            {
                CoordinateSpace = AlgorithmCoordinateSpace.Physical,
            },
            Inputs = [new AlgorithmInputReference("source", "image.cvraw", "42", "abc")],
        };

        AlgorithmInvocation restored = AlgorithmJson.Deserialize<AlgorithmInvocation>(AlgorithmJson.ToElement(invocation));
        PolygonAlgorithmRoi roi = Assert.IsType<PolygonAlgorithmRoi>(restored.Roi);
        Assert.Equal(AlgorithmCoordinateSpace.Physical, roi.CoordinateSpace);
        Assert.Equal(invocation.InvocationId, restored.InvocationId);
        Assert.Equal(invocation.AlgorithmId, restored.AlgorithmId);

        AlgorithmPoint pixels = AlgorithmCoordinates.ToPixel(new AlgorithmPoint(25.4, 12.7), AlgorithmCoordinateSpace.Physical, 100, 200);
        Assert.Equal(100, pixels.X, 8);
        Assert.Equal(100, pixels.Y, 8);
        AlgorithmPoint physical = AlgorithmCoordinates.FromPixel(pixels, AlgorithmCoordinateSpace.Physical, 100, 200);
        Assert.Equal(25.4, physical.X, 8);
        Assert.Equal(12.7, physical.Y, 8);
    }

    [Fact]
    public async Task InvertSmokeIsExactAndDoesNotMutateInput()
    {
        byte[] pixels = [0, 1, 127, 254, 255, 33];
        using AlgorithmImageBuffer input = new(3, 2, 3, AlgorithmImageFormat.Gray8, pixels.ToArray());
        byte[] original = input.Data.ToArray();
        using AlgorithmResult result = await RunStandardAsync(StandardAlgorithmIds.Invert, new NoAlgorithmParameters(), input);

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(new byte[] { 255, 254, 128, 1, 0, 222 }, result.GetArtifact<AlgorithmImageArtifact>()!.Image.Data.ToArray());
        Assert.Equal(original, input.Data.ToArray());
    }

    [Fact]
    public async Task CannyHasOnePixelContractAcrossGray8Gray16Bgr24AndBgra32()
    {
        CannyParameters parameters = new() { LowThreshold = 25, HighThreshold = 75 };
        byte[]? expected = null;
        foreach (AlgorithmImageFormat format in new[]
                 {
                     AlgorithmImageFormat.Gray8,
                     AlgorithmImageFormat.Gray16,
                     AlgorithmImageFormat.Bgr24,
                     AlgorithmImageFormat.Bgra32,
                 })
        {
            using AlgorithmImageBuffer input = CreateStepImage(format);
            byte[] original = input.Data.ToArray();
            using AlgorithmResult result = await RunStandardAsync(StandardAlgorithmIds.Canny, parameters, input);
            AlgorithmImageBuffer output = result.GetArtifact<AlgorithmImageArtifact>()!.Image;
            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
            Assert.Equal(AlgorithmImageFormat.Gray8, output.Format);
            Assert.Equal((16, 12), (output.Width, output.Height));
            Assert.True(output.Data.Span.ContainsAnyExcept((byte)0));
            expected ??= output.Data.ToArray();
            Assert.Equal(expected, output.Data.ToArray());
            Assert.Equal(original, input.Data.ToArray());
        }
    }

    [Fact]
    public async Task RoiStatisticsRectangleProducesGoldenMeasurementsPercentilesAndHistogram()
    {
        byte[] pixels = Enumerable.Range(0, 12).Select(value => (byte)value).ToArray();
        using AlgorithmImageBuffer input = new(4, 3, 4, AlgorithmImageFormat.Gray8, pixels);
        byte[] original = input.Data.ToArray();
        RoiStatisticsParameters parameters = new() { DetectBadPixelCandidates = false };
        using AlgorithmResult result = await RunStandardAsync(
            StandardAlgorithmIds.RoiStatistics,
            parameters,
            input,
            new RectangleAlgorithmRoi(1, 0, 2, 3));

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(6, Measurement(result, "roi.pixel_count"));
        Assert.Equal(1, Measurement(result, "channel.minimum", 0));
        Assert.Equal(10, Measurement(result, "channel.maximum", 0));
        Assert.Equal(5.5, Measurement(result, "channel.mean", 0), 12);
        Assert.Equal(Math.Sqrt(65.5 / 6), Measurement(result, "channel.stddev.population", 0), 12);
        Assert.Equal(5.5, Percentile(result, 0, "50"), 12);
        AlgorithmTableArtifact histogram = result.GetArtifact<AlgorithmTableArtifact>("roi-histogram")!;
        Assert.Equal(1, histogram.Rows.Single(row => row["BinIndex"].GetInt32() == 1)["Count"].GetInt64());
        Assert.Equal(1, histogram.Rows.Single(row => row["BinIndex"].GetInt32() == 10)["Count"].GetInt64());
        Assert.Equal(original, input.Data.ToArray());
    }

    [Fact]
    public async Task RoiStatisticsCirclePolygonAndPhysicalCoordinatesUsePixelCenterRules()
    {
        using AlgorithmImageBuffer pixels = new(5, 5, 5, AlgorithmImageFormat.Gray8, Enumerable.Range(0, 25).Select(value => (byte)value).ToArray(), 254, 254);
        RoiStatisticsParameters parameters = new() { DetectBadPixelCandidates = false, HistogramBins = 16 };

        using AlgorithmResult circle = await RunStandardAsync(
            StandardAlgorithmIds.RoiStatistics,
            parameters,
            pixels,
            new CircleAlgorithmRoi(new AlgorithmPoint(2, 2), 1));
        Assert.Equal(5, Measurement(circle, "roi.pixel_count"));
        Assert.Equal(12, Measurement(circle, "channel.mean", 0), 12);

        using AlgorithmResult polygon = await RunStandardAsync(
            StandardAlgorithmIds.RoiStatistics,
            parameters,
            pixels,
            new PolygonAlgorithmRoi([new(0, 0), new(2, 0), new(0, 2)]));
        Assert.Equal(6, Measurement(polygon, "roi.pixel_count"));
        Assert.Equal(4, Measurement(polygon, "channel.mean", 0), 12);

        using AlgorithmResult physical = await RunStandardAsync(
            StandardAlgorithmIds.RoiStatistics,
            parameters,
            pixels,
            new RectangleAlgorithmRoi(0, 0, 0.2, 0.2) { CoordinateSpace = AlgorithmCoordinateSpace.Physical });
        Assert.Equal(4, Measurement(physical, "roi.pixel_count"));
        Assert.Equal(3, Measurement(physical, "channel.mean", 0), 12);
    }

    [Fact]
    public async Task RoiStatisticsTracksFloatInvalidValuesSaturationAndColorChannels()
    {
        float[] floats = [float.NaN, float.PositiveInfinity, float.NegativeInfinity, 0.5f];
        byte[] floatBytes = MemoryMarshal.AsBytes(floats.AsSpan()).ToArray();
        using AlgorithmImageBuffer floatInput = new(2, 2, 2 * sizeof(float), AlgorithmImageFormat.Gray32Float, floatBytes);
        RoiStatisticsParameters parameters = new() { DetectBadPixelCandidates = false, HistogramBins = 8 };
        using AlgorithmResult floatResult = await RunStandardAsync(
            StandardAlgorithmIds.RoiStatistics,
            parameters,
            floatInput,
            new RectangleAlgorithmRoi(0, 0, 2, 2));

        Assert.Equal(1, Measurement(floatResult, "channel.valid_count", 0));
        Assert.Equal(3, Measurement(floatResult, "channel.invalid_count", 0));
        Assert.Equal(1, Measurement(floatResult, "channel.nan_count", 0));
        Assert.Equal(1, Measurement(floatResult, "channel.positive_infinity_count", 0));
        Assert.Equal(1, Measurement(floatResult, "channel.negative_infinity_count", 0));
        Assert.Equal(0.5, Measurement(floatResult, "channel.mean", 0), 12);

        using AlgorithmImageBuffer saturated = new(2, 1, 2, AlgorithmImageFormat.Gray8, [0, 255]);
        using AlgorithmResult saturatedResult = await RunStandardAsync(
            StandardAlgorithmIds.RoiStatistics,
            parameters,
            saturated,
            new RectangleAlgorithmRoi(0, 0, 2, 1));
        Assert.Equal(1, Measurement(saturatedResult, "channel.low_saturated_count", 0));
        Assert.Equal(1, Measurement(saturatedResult, "channel.high_saturated_count", 0));

        using AlgorithmImageBuffer color = new(1, 1, 3, AlgorithmImageFormat.Bgr24, [1, 2, 3]);
        using AlgorithmResult colorResult = await RunStandardAsync(
            StandardAlgorithmIds.RoiStatistics,
            parameters,
            color,
            new RectangleAlgorithmRoi(0, 0, 1, 1));
        Assert.Equal(1, Measurement(colorResult, "channel.mean", 0));
        Assert.Equal(2, Measurement(colorResult, "channel.mean", 1));
        Assert.Equal(3, Measurement(colorResult, "channel.mean", 2));
    }

    [Fact]
    public async Task RoiStatisticsReturnsBadPixelTableGeometryAndTransientOverlay()
    {
        byte[] pixels = Enumerable.Repeat((byte)10, 25).ToArray();
        pixels[12] = 255;
        using AlgorithmImageBuffer input = new(5, 5, 5, AlgorithmImageFormat.Gray8, pixels);
        RoiStatisticsParameters parameters = new()
        {
            HistogramBins = 16,
            BadPixelMinimumDeviationFraction = 0.01,
            MaximumBadPixelCandidates = 10,
        };
        using AlgorithmResult result = await RunStandardAsync(
            StandardAlgorithmIds.RoiStatistics,
            parameters,
            input,
            new RectangleAlgorithmRoi(0, 0, 5, 5));

        Assert.Equal(1, Measurement(result, "roi.bad_pixel_candidate_count"));
        AlgorithmTableArtifact table = result.GetArtifact<AlgorithmTableArtifact>("bad-pixel-candidates")!;
        IReadOnlyDictionary<string, JsonElement> candidate = Assert.Single(table.Rows);
        Assert.Equal(2, candidate["X"].GetInt32());
        Assert.Equal(2, candidate["Y"].GetInt32());
        Assert.Equal(255, candidate["Value"].GetDouble());
        AlgorithmGeometryArtifact geometry = result.GetArtifact<AlgorithmGeometryArtifact>()!;
        Assert.Contains(geometry.Geometries, item => item.Id == "bad-pixel-0" && item.Kind == AlgorithmGeometryKind.Point);
        Assert.Equal(AlgorithmOverlayLifetime.Transient, result.GetArtifact<AlgorithmOverlayArtifact>()!.Lifetime);
    }

    [Fact]
    public async Task RoiStatisticsCancellationIsStructuredAndReleasesTransferredInput()
    {
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "roi.scan") cancellation.Cancel();
        });
        AlgorithmImageBuffer input = new(512, 512, 512, AlgorithmImageFormat.Gray8, new byte[512 * 512]);
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(
            StandardAlgorithmIds.RoiStatistics,
            new RoiStatisticsParameters(),
            new RectangleAlgorithmRoi(0, 0, 512, 512));

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
    public async Task EightBitBatchAndRunnerUseIdenticalCannyParametersAndPixels()
    {
        using Mat source = new(31, 37, MatType.CV_8UC3);
        Cv2.Randu(source, Scalar.All(0), Scalar.All(byte.MaxValue + 1d));
        BatchImageAlgorithmDefinition batch = BatchImageAlgorithms.CreateAll().Single(item => item.Descriptor?.Id == StandardAlgorithmIds.Canny);
        CannyParameters parameters = Assert.IsType<CannyParameters>(batch.Options);
        parameters.LowThreshold = 41;
        parameters.HighThreshold = 123;
        using Mat batchOutput = batch.Apply(source);

        using AlgorithmImageBuffer input = BufferFromMat(source, AlgorithmImageFormat.Bgr24);
        using AlgorithmResult result = await RunStandardAsync(StandardAlgorithmIds.Canny, parameters, input);
        using Mat runnerOutput = MatFromBuffer(result.GetArtifact<AlgorithmImageArtifact>()!.Image);
        Assert.Equal(0, Cv2.Norm(batchOutput, runnerOutput));
    }

    [Fact]
    public async Task UnsupportedFormatReturnsStructuredFailureAndReleasesTransferredInput()
    {
        AlgorithmCatalog catalog = new();
        AlgorithmDescriptor descriptor = Descriptor(new AlgorithmId("test.gray-only"), new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new();
        AlgorithmRunner runner = new(catalog, [new TestProvider("unused", 1)], scheduler);
        AlgorithmImageBuffer input = new(1, 1, 3, AlgorithmImageFormat.Bgr24, [1, 2, 3]);

        using AlgorithmResult result = await runner.RunAsync(Request(descriptor.Id, input, AlgorithmInputOwnership.Transferred));

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "unsupported_format");
        Assert.True(input.IsDisposed);
    }

    [Fact]
    public async Task MedianBlurReportsParameterFormatCombinationInsteadOfThrowing()
    {
        using AlgorithmImageBuffer input = CreateStepImage(AlgorithmImageFormat.Gray16);
        using AlgorithmResult result = await RunStandardAsync(
            StandardAlgorithmIds.MedianBlur,
            new MedianBlurParameters { KernelSize = 7 },
            input);

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "parameter_format_unsupported");
    }

    [Fact]
    public async Task RunnerReleasesTransferredInputsForSuccessExceptionAndCancellationButNotBorrowed()
    {
        AlgorithmDescriptor descriptor = Descriptor(new AlgorithmId("test.ownership"), new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

        AlgorithmImageBuffer success = Buffer(1);
        using (AlgorithmResult result = await RunWithProvider(descriptor, new TestProvider("success", 1), success, AlgorithmInputOwnership.Transferred))
            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.True(success.IsDisposed);

        AlgorithmImageBuffer failure = Buffer(2);
        using (AlgorithmResult result = await RunWithProvider(descriptor, new TestProvider("throws", 1, throws: true), failure, AlgorithmInputOwnership.Transferred))
            Assert.Contains(result.Failures, item => item.Code == "execution_exception");
        Assert.True(failure.IsDisposed);

        AlgorithmImageBuffer cancelled = Buffer(3);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        using (AlgorithmResult result = await RunWithProvider(descriptor, new TestProvider("cancel", 1), cancelled, AlgorithmInputOwnership.Transferred, cancellation.Token))
            Assert.Equal(AlgorithmResultStatus.Cancelled, result.Status);
        Assert.True(cancelled.IsDisposed);

        using AlgorithmImageBuffer borrowed = Buffer(4);
        using (AlgorithmResult result = await RunWithProvider(descriptor, new TestProvider("borrowed", 1), borrowed, AlgorithmInputOwnership.Borrowed))
            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.False(borrowed.IsDisposed);
    }

    [Fact]
    public async Task CancellationAfterProviderCompletionReleasesProducedArtifacts()
    {
        AlgorithmDescriptor descriptor = Descriptor(new AlgorithmId("test.cancel-output"), new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new();
        using CancellationTokenSource cancellation = new();
        CancellingOutputProvider provider = new(cancellation);
        AlgorithmRunner runner = new(catalog, [provider], scheduler);
        AlgorithmImageBuffer input = Buffer(5);

        using AlgorithmResult result = await runner.RunAsync(Request(descriptor.Id, input, AlgorithmInputOwnership.Transferred), cancellation.Token);

        Assert.Equal(AlgorithmResultStatus.Cancelled, result.Status);
        Assert.True(input.IsDisposed);
        Assert.True(provider.Output.IsDisposed);
    }

    [Fact]
    public async Task ProviderOutputOutsideDescriptorContractFailsAndReleasesArtifact()
    {
        AlgorithmId id = new("test.output-contract");
        TestParameters defaults = new();
        AlgorithmDescriptor descriptor = new(
            id,
            new AlgorithmVersion(1, 0, 0),
            "output-contract",
            "test",
            "test",
            typeof(TestParameters),
            new AlgorithmParameterSchema(1, [], AlgorithmJson.ToElement(defaults)),
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 },
            AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            OutputFormats: new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 },
            OutputFormatPolicy: "always-gray8");
        InvalidOutputProvider provider = new();
        using AlgorithmImageBuffer input = Buffer(9);

        using AlgorithmResult result = await RunWithProvider(descriptor, provider, input, AlgorithmInputOwnership.Borrowed);

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, item => item.Code == "provider_output_format_violation");
        Assert.True(provider.Output.IsDisposed);
        Assert.False(input.IsDisposed);
    }

    [Fact]
    public async Task DisposingSuccessfulResultReleasesOwnedImageArtifact()
    {
        using AlgorithmImageBuffer input = Buffer(0);
        AlgorithmResult result = await RunStandardAsync(StandardAlgorithmIds.Invert, new NoAlgorithmParameters(), input);
        AlgorithmImageBuffer output = result.GetArtifact<AlgorithmImageArtifact>()!.Image;
        Assert.False(output.IsDisposed);
        result.Dispose();
        Assert.True(result.IsDisposed);
        Assert.True(output.IsDisposed);
    }

    [Fact]
    public async Task PreferredCompatibleProviderIsSelectedAndDiagnosed()
    {
        AlgorithmDescriptor descriptor = Descriptor(new AlgorithmId("test.provider-selection"), new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new();
        AlgorithmRunner runner = new(catalog, [new TestProvider("high", 100), new TestProvider("preferred", 1)], scheduler);
        using AlgorithmImageBuffer input = Buffer(1);
        AlgorithmRunRequest request = Request(descriptor.Id, input, AlgorithmInputOwnership.Borrowed, "preferred");

        using AlgorithmResult result = await runner.RunAsync(request);

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal("preferred", result.Diagnostics.ProviderId);
    }

    [Fact]
    public async Task ParameterSchemaMigrationIsExplicitAndNewerOrMissingVersionsFailStructurally()
    {
        AlgorithmId id = new("test.parameter-migration");
        MigratedTestParameters defaults = new();
        AlgorithmDescriptor descriptor = new(
            id,
            new AlgorithmVersion(1, 0, 0),
            "migration",
            "test",
            "test",
            typeof(MigratedTestParameters),
            new AlgorithmParameterSchema(2, [], AlgorithmJson.ToElement(defaults)),
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 },
            AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local);
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new();
        TestProvider provider = new("migration", 1);
        AlgorithmImageBuffer missingInput = Buffer(1);
        AlgorithmInvocation oldInvocation = new()
        {
            AlgorithmId = id,
            ParameterSchemaVersion = 1,
            Parameters = JsonSerializer.SerializeToElement(new { value = 9 }, AlgorithmJson.Options),
        };
        AlgorithmRunner missingRunner = new(catalog, [provider], scheduler);
        using (AlgorithmResult missing = await missingRunner.RunAsync(new AlgorithmRunRequest
               {
                   Invocation = oldInvocation,
                   Inputs = [new AlgorithmInput { Name = "source", Image = missingInput, Ownership = AlgorithmInputOwnership.Transferred }],
                   RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
               }))
        {
            Assert.Contains(missing.Failures, item => item.Code == "parameter_migration_missing");
        }
        Assert.True(missingInput.IsDisposed);

        AlgorithmRunner migratedRunner = new(catalog, [provider], scheduler, [new TestMigrator(id)]);
        using AlgorithmImageBuffer defaultInput = Buffer(0);
        using (AlgorithmResult currentDefaults = await missingRunner.RunAsync(new AlgorithmRunRequest
               {
                   Invocation = new AlgorithmInvocation { AlgorithmId = id },
                   Inputs = [new AlgorithmInput { Name = "source", Image = defaultInput }],
                   RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
               }))
        {
            Assert.Equal(AlgorithmResultStatus.Succeeded, currentDefaults.Status);
            Assert.Equal(defaults.Value, Assert.IsType<MigratedTestParameters>(provider.LastParameters).Value);
        }

        using AlgorithmImageBuffer migratedInput = Buffer(2);
        using (AlgorithmResult migrated = await migratedRunner.RunAsync(new AlgorithmRunRequest
               {
                   Invocation = oldInvocation,
                   Inputs = [new AlgorithmInput { Name = "source", Image = migratedInput }],
                   RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
               }))
        {
            Assert.Equal(AlgorithmResultStatus.Succeeded, migrated.Status);
            Assert.Equal(9, Assert.IsType<MigratedTestParameters>(provider.LastParameters).Value);
        }

        using AlgorithmImageBuffer newerInput = Buffer(3);
        using AlgorithmResult newer = await migratedRunner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = new AlgorithmInvocation { AlgorithmId = id, ParameterSchemaVersion = 3, Parameters = AlgorithmJson.ToElement(defaults) },
            Inputs = [new AlgorithmInput { Name = "source", Image = newerInput }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        Assert.Contains(newer.Failures, item => item.Code == "parameter_schema_newer");
    }

    [Fact]
    public void PreviewValidityRejectsOlderInvocationChangedRevisionChangedDocumentAndClosedHost()
    {
        Guid document = Guid.NewGuid();
        Guid current = Guid.NewGuid();
        Guid older = Guid.NewGuid();
        Assert.True(ImageAlgorithmPreviewValidity.IsCurrent(document, 7, current, document, 7, current, false));
        Assert.False(ImageAlgorithmPreviewValidity.IsCurrent(document, 7, older, document, 7, current, false));
        Assert.False(ImageAlgorithmPreviewValidity.IsCurrent(document, 7, current, document, 8, current, false));
        Assert.False(ImageAlgorithmPreviewValidity.IsCurrent(document, 7, current, Guid.NewGuid(), 7, current, false));
        Assert.False(ImageAlgorithmPreviewValidity.IsCurrent(document, 7, current, document, 7, current, true));
    }

    [Fact]
    public void OverlayStoreSeparatesTransientAndPersistentLifetimes()
    {
        AlgorithmOverlayStore store = new();
        store.Apply(Overlay("preview", AlgorithmOverlayLifetime.Transient));
        store.Apply(Overlay("accepted", AlgorithmOverlayLifetime.Persistent));
        Assert.Equal(2, store.Snapshot().Count);

        store.ClearTransient();
        AlgorithmOverlayArtifact remaining = Assert.Single(store.Snapshot());
        Assert.Equal("accepted", remaining.Name);

        store.Apply(Overlay("accepted", AlgorithmOverlayLifetime.Transient));
        remaining = Assert.Single(store.Snapshot());
        Assert.Equal(AlgorithmOverlayLifetime.Transient, remaining.Lifetime);
        store.Clear();
        Assert.Empty(store.Snapshot());
    }

    [Fact]
    public async Task ImageViewPreviewSessionsAreHostWideAndCommitRevisionExactlyOnce()
    {
        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView view = new();
            view.SetImageSource(CreateGrayBitmap(), enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });

        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => imageView.EditorContext.ProcessingContext);
            object older = WpfTestHost.Invoke(() => StartPreviewSession(context));
            object current = WpfTestHost.Invoke(() => StartPreviewSession(context));
            WriteableBitmap currentBitmap = GetPreviewBitmap(current);

            WpfTestHost.Invoke(() => ((IDisposable)older).Dispose());

            Assert.Same(currentBitmap, WpfTestHost.Invoke(() => context.FunctionImage));
            Assert.Same(currentBitmap, WpfTestHost.Invoke(() => context.ImageShow.Source));
            WpfTestHost.Invoke(() => ((IDisposable)current).Dispose());

            long beforeCommit = WpfTestHost.Invoke(() => imageView.ImageRevision);
            object committing = WpfTestHost.Invoke(() => StartPreviewSession(context));
            using (AlgorithmResult preview = await InvokePreviewAsync(
                       committing,
                       AlgorithmInvocation.Create(StandardAlgorithmIds.Invert, new NoAlgorithmParameters())))
            {
                Assert.Equal(AlgorithmResultStatus.Succeeded, preview.Status);
                WriteableBitmap committedBitmap = GetPreviewBitmap(committing);
                Assert.True(WpfTestHost.Invoke(() => InvokeCommit(committing)));
                Assert.Same(committedBitmap, WpfTestHost.Invoke(() => context.ViewBitmapSource));
                Assert.Same(committedBitmap, WpfTestHost.Invoke(() => context.ImageShow.Source));
                Assert.Null(WpfTestHost.Invoke(() => context.FunctionImage));
                Assert.Equal(byte.MaxValue, WpfTestHost.Invoke(() =>
                {
                    byte[] pixel = new byte[1];
                    committedBitmap.CopyPixels(new Int32Rect(0, 0, 1, 1), pixel, 1, 0);
                    return pixel[0];
                }));
            }
            Assert.Equal(beforeCommit + 1, WpfTestHost.Invoke(() => imageView.ImageRevision));
            WpfTestHost.Invoke(() => ((IDisposable)committing).Dispose());

            object stale = WpfTestHost.Invoke(() => StartPreviewSession(context));
            using (AlgorithmResult preview = await InvokePreviewAsync(
                       stale,
                       AlgorithmInvocation.Create(StandardAlgorithmIds.Invert, new NoAlgorithmParameters())))
            {
                Assert.Equal(AlgorithmResultStatus.Succeeded, preview.Status);
            }
            WpfTestHost.Invoke(imageView.NotifySourcePixelsChanged);
            long externallyChangedRevision = WpfTestHost.Invoke(() => imageView.ImageRevision);
            Assert.False(WpfTestHost.Invoke(() => InvokeCommit(stale)));
            Assert.Equal(externallyChangedRevision, WpfTestHost.Invoke(() => imageView.ImageRevision));
            WpfTestHost.Invoke(() => ((IDisposable)stale).Dispose());
        }
        finally
        {
            WpfTestHost.Invoke(imageView.Dispose);
        }
    }

    [Fact]
    public void ImageDocumentMutationEntrypointsAdvanceRevisionOnceAndInvalidateAnalysis()
    {
        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView view = new();
            view.SetImageSource(CreateGrayBitmap(), enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });

        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => imageView.EditorContext.ProcessingContext);
            AssertMutation(context.NotifySourcePixelsChanged);
            AssertMutation(() => imageView.SetImageSource(
                CreateGrayBitmap(),
                enableEditorImageServices: false,
                configureDefaultLayerController: false));
            AssertMutation(imageView.Clear);

            void AssertMutation(Action mutation)
            {
                Guid documentId = WpfTestHost.Invoke(() => context.DocumentInstanceId);
                long sourceRevision = WpfTestHost.Invoke(() => context.ImageRevision);
                Guid invocationId = Guid.NewGuid();
                using CancellationTokenSource cancellation = WpfTestHost.Invoke(() =>
                    ImageAlgorithmAnalysisSession.Begin(
                        context,
                        documentId,
                        sourceRevision,
                        Guid.NewGuid(),
                        invocationId));

                WpfTestHost.Invoke(mutation);

                Assert.Equal(sourceRevision + 1, WpfTestHost.Invoke(() => context.ImageRevision));
                Assert.True(cancellation.IsCancellationRequested);
                Assert.False(ImageAlgorithmAnalysisSession.IsCurrent(
                    context,
                    documentId,
                    sourceRevision,
                    invocationId));
            }
        }
        finally
        {
            WpfTestHost.Invoke(imageView.Dispose);
        }
    }

    [Fact]
    public void AcquireImageFrame_WhenCurrentSourceObjectChanged_RebuildsStaleFrameCache()
    {
        WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            using ImageView imageView = new();
            WriteableBitmap firstBitmap = CreateSolidGrayBitmap(8, 6, 10);
            WriteableBitmap currentBitmap = CreateSolidGrayBitmap(5, 4, 200);
            imageView.SetImageSource(firstBitmap, enableEditorImageServices: false, configureDefaultLayerController: false);

            using ImageFrameLease staleLease = Assert.IsType<ImageFrameLease>(imageView.AcquireImageFrame());
            long staleRevision = staleLease.Revision;

            imageView.ViewBitmapSource = currentBitmap;
            imageView.ImageShow.Source = currentBitmap;

            using ImageFrameLease currentLease = Assert.IsType<ImageFrameLease>(imageView.AcquireImageFrame());

            Assert.False(imageView.IsCurrentImageRevision(staleRevision));
            Assert.Equal(5, currentLease.Width);
            Assert.Equal(4, currentLease.Height);
            Assert.Equal(200, Marshal.ReadByte(currentLease.Image.pData));
            Assert.Equal(10, Marshal.ReadByte(staleLease.Image.pData));
        });
    }

    [Fact]
    public async Task ClearDuringInFlightAlgorithmWorkCancelsRunsAndRejectsLateResults()
    {
        ImageView imageView = WpfTestHost.Invoke(() =>
        {
            EnsureImageViewTestResources();
            ImageView view = new();
            view.SetImageSource(CreateGrayBitmap(), enableEditorImageServices: false, configureDefaultLayerController: false);
            return view;
        });
        object? preview = null;

        try
        {
            ImageProcessingContext context = WpfTestHost.Invoke(() => imageView.EditorContext.ProcessingContext);
            preview = WpfTestHost.Invoke(() => StartPreviewSession(context));
            Guid previewSessionId = (Guid)preview.GetType()
                .GetField("_sessionId", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(preview)!;
            Guid previewInvocationId = Guid.NewGuid();
            using CancellationTokenSource previewCancellation = new();
            Guid previewDocumentId = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long previewRevision = WpfTestHost.Invoke(() => context.ImageRevision);
            Assert.True(WpfTestHost.Invoke(() => context.TryBeginAlgorithmPreviewInvocation(
                previewSessionId,
                previewDocumentId,
                previewRevision,
                previewInvocationId,
                previewCancellation,
                out _)));
            Task previewCancelled = WaitForCancellationAsync(previewCancellation.Token);
            long beforeClear = WpfTestHost.Invoke(() => imageView.ImageRevision);
            WpfTestHost.Invoke(imageView.Clear);
            await previewCancelled.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(beforeClear + 1, WpfTestHost.Invoke(() => imageView.ImageRevision));
            Assert.True(previewCancellation.IsCancellationRequested);
            Assert.False(WpfTestHost.Invoke(() => InvokeCommit(preview)));
            Assert.Equal(beforeClear + 1, WpfTestHost.Invoke(() => imageView.ImageRevision));
            Assert.Null(WpfTestHost.Invoke(() => context.ViewBitmapSource));
            Assert.Null(WpfTestHost.Invoke(() => context.ImageShow.Source));

            WpfTestHost.Invoke(() =>
            {
                ((IDisposable)preview).Dispose();
                imageView.SetImageSource(
                    CreateGrayBitmap(),
                    enableEditorImageServices: false,
                    configureDefaultLayerController: false);
            });
            preview = null;

            Guid analysisDocumentId = WpfTestHost.Invoke(() => context.DocumentInstanceId);
            long analysisRevision = WpfTestHost.Invoke(() => context.ImageRevision);
            Guid analysisInvocationId = Guid.NewGuid();
            using CancellationTokenSource analysisCancellation = WpfTestHost.Invoke(() =>
                ImageAlgorithmAnalysisSession.Begin(
                    context,
                    analysisDocumentId,
                    analysisRevision,
                    Guid.NewGuid(),
                    analysisInvocationId));
            Task analysisCancelled = WaitForCancellationAsync(analysisCancellation.Token);
            long beforeAnalysisClear = WpfTestHost.Invoke(() => imageView.ImageRevision);
            WpfTestHost.Invoke(imageView.Clear);
            await analysisCancelled.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.Equal(beforeAnalysisClear + 1, WpfTestHost.Invoke(() => imageView.ImageRevision));
            Assert.False(ImageAlgorithmAnalysisSession.IsCurrent(
                context,
                analysisDocumentId,
                analysisRevision,
                analysisInvocationId));
            Assert.False(ImageAlgorithmAnalysisSession.CanPresent(
                context,
                analysisDocumentId,
                analysisRevision,
                analysisInvocationId,
                out System.Windows.Window? previous));
            Assert.Null(previous);
        }
        finally
        {
            if (preview != null) WpfTestHost.Invoke(() => ((IDisposable)preview).Dispose());
            WpfTestHost.Invoke(imageView.Dispose);
        }
    }

    [Fact]
    public async Task LocalFlowAdapterUsesCatalogControlPlaneWithoutOwningTheFrameLease()
    {
        LocalFrameMetadata metadata = new() { Width = 3, Height = 2, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
        using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, 6, 0);
        using LocalFlowFrameLease lease = frame.Acquire();
        Marshal.Copy(new byte[] { 0, 1, 2, 253, 254, 255 }, 0, lease.RawPointer, 6);
        using AlgorithmResult result = await LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(
            lease,
            AlgorithmInvocation.Create(StandardAlgorithmIds.Invert, new NoAlgorithmParameters()));

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(new byte[] { 255, 254, 253, 2, 1, 0 }, result.GetArtifact<AlgorithmImageArtifact>()!.Image.Data.ToArray());
        Assert.Equal(6, lease.CopyRawToArray().Length);
        Assert.NotNull(typeof(AlgorithmNode).GetConstructor(Type.EmptyTypes));
    }

    private static async Task<AlgorithmResult> RunStandardAsync(
        AlgorithmId id,
        IAlgorithmParameters parameters,
        AlgorithmImageBuffer input,
        AlgorithmRoi? roi = null)
    {
        return await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = new AlgorithmInvocation
            {
                AlgorithmId = id,
                ParameterSchemaVersion = parameters.SchemaVersion,
                Parameters = AlgorithmJson.ToElement(parameters),
                Roi = roi,
            },
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
    }

    private static double Measurement(AlgorithmResult result, string name, int? channel = null)
        => result.GetArtifact<AlgorithmMeasurementArtifact>()!.Measurements
            .Single(measurement => measurement.Name == name && measurement.Channel == channel)
            .Value;

    private static double Percentile(AlgorithmResult result, int channel, string percentile)
        => result.GetArtifact<AlgorithmMeasurementArtifact>()!.Measurements
            .Single(measurement => measurement.Name == "channel.percentile"
                && measurement.Channel == channel
                && measurement.Qualifiers!["percentile"] == percentile)
            .Value;

    private static AlgorithmImageBuffer CreateStepImage(AlgorithmImageFormat format)
    {
        const int width = 16;
        const int height = 12;
        int stride = width * format.BytesPerPixel();
        byte[] data = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool high = x >= width / 2;
                int offset = y * stride + x * format.BytesPerPixel();
                if (format.BitsPerChannel() == 16)
                {
                    ushort value = high ? ushort.MaxValue : (ushort)0;
                    for (int channel = 0; channel < format.Channels(); channel++)
                    {
                        data[offset + channel * 2] = (byte)value;
                        data[offset + channel * 2 + 1] = (byte)(value >> 8);
                    }
                }
                else
                {
                    byte value = high ? byte.MaxValue : (byte)0;
                    for (int channel = 0; channel < format.Channels(); channel++) data[offset + channel] = value;
                    if (format == AlgorithmImageFormat.Bgra32) data[offset + 3] = 73;
                }
            }
        }
        return new AlgorithmImageBuffer(width, height, stride, format, data);
    }

    private static AlgorithmImageBuffer BufferFromMat(Mat mat, AlgorithmImageFormat format)
    {
        int stride = mat.Cols * format.BytesPerPixel();
        byte[] data = new byte[stride * mat.Rows];
        byte[] row = new byte[stride];
        for (int y = 0; y < mat.Rows; y++)
        {
            Marshal.Copy(mat.Ptr(y), row, 0, stride);
            System.Buffer.BlockCopy(row, 0, data, y * stride, stride);
        }
        return new AlgorithmImageBuffer(mat.Cols, mat.Rows, stride, format, data);
    }

    private static Mat MatFromBuffer(AlgorithmImageBuffer image)
    {
        Mat result = new(image.Height, image.Width, MatType.CV_8UC1);
        int stride = image.Width;
        byte[] data = image.Data.ToArray();
        for (int y = 0; y < image.Height; y++) Marshal.Copy(data, y * image.Stride, result.Ptr(y), stride);
        return result;
    }

    private static AlgorithmOverlayArtifact Overlay(string name, AlgorithmOverlayLifetime lifetime)
        => new(name, lifetime, [new AlgorithmOverlayItem("g1", new AlgorithmOverlayStyle())]);

    private static AlgorithmImageBuffer Buffer(byte value) => new(1, 1, 1, AlgorithmImageFormat.Gray8, [value]);

    private static WriteableBitmap CreateGrayBitmap()
    {
        const int width = 8;
        const int height = 6;
        byte[] pixels = Enumerable.Range(0, width * height).Select(value => (byte)value).ToArray();
        WriteableBitmap bitmap = new(width, height, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width, 0);
        return bitmap;
    }

    private static WriteableBitmap CreateSolidGrayBitmap(int width, int height, byte value)
    {
        byte[] pixels = Enumerable.Repeat(value, width * height).ToArray();
        WriteableBitmap bitmap = new(width, height, 96, 96, PixelFormats.Gray8, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width, 0);
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

    private static object StartPreviewSession(ImageProcessingContext context)
    {
        Type type = typeof(ImageProcessingContext).Assembly.GetType(
            "ColorVision.ImageEditor.Algorithms.ImageAlgorithmPreviewSession",
            throwOnError: true)!;
        return type.GetMethod("Start", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [context])!;
    }

    private static WriteableBitmap GetPreviewBitmap(object session)
        => (WriteableBitmap)session.GetType().GetProperty("PreviewBitmap", BindingFlags.Public | BindingFlags.Instance)!.GetValue(session)!;

    private static Task<AlgorithmResult> InvokePreviewAsync(object session, AlgorithmInvocation invocation)
        => WpfTestHost.Invoke(() => (Task<AlgorithmResult>)session.GetType()
            .GetMethod("PreviewAsync", BindingFlags.Public | BindingFlags.Instance)!
            .Invoke(session, [invocation, AlgorithmHostCapabilities.Interactive, CancellationToken.None])!);

    private static bool InvokeCommit(object session)
        => (bool)session.GetType().GetMethod("Commit", BindingFlags.Public | BindingFlags.Instance)!.Invoke(session, null)!;

    private static async Task WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static AlgorithmDescriptor Descriptor(AlgorithmId id, IReadOnlySet<AlgorithmImageFormat> formats)
    {
        TestParameters defaults = new();
        return new AlgorithmDescriptor(
            id,
            new AlgorithmVersion(1, 0, 0),
            id.Value,
            "test",
            "test",
            typeof(TestParameters),
            new AlgorithmParameterSchema(1, [], AlgorithmJson.ToElement(defaults)),
            formats,
            AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local);
    }

    private static AlgorithmRunRequest Request(
        AlgorithmId id,
        AlgorithmImageBuffer image,
        AlgorithmInputOwnership ownership,
        string? preferredProvider = null)
        => new()
        {
            Invocation = AlgorithmInvocation.Create(id, new TestParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = image, Ownership = ownership }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            PreferredProviderId = preferredProvider,
        };

    private static async Task<AlgorithmResult> RunWithProvider(
        AlgorithmDescriptor descriptor,
        IImageAlgorithmProvider provider,
        AlgorithmImageBuffer image,
        AlgorithmInputOwnership ownership,
        CancellationToken cancellationToken = default)
    {
        AlgorithmCatalog catalog = new();
        catalog.Register(descriptor);
        using AlgorithmExecutionScheduler scheduler = new();
        AlgorithmRunner runner = new(catalog, [provider], scheduler);
        return await runner.RunAsync(Request(descriptor.Id, image, ownership), cancellationToken);
    }

    public sealed class TestParameters : IAlgorithmParameters
    {
        public int SchemaVersion => 1;

        public AlgorithmValidationResult Validate() => AlgorithmValidationResult.Valid();
    }

    public sealed class MigratedTestParameters : IAlgorithmParameters
    {
        public int SchemaVersion => 2;

        public int Value { get; set; }

        public AlgorithmValidationResult Validate() => AlgorithmValidationResult.Valid();
    }

    private sealed class TestMigrator(AlgorithmId id) : IAlgorithmParameterMigrator
    {
        public AlgorithmId AlgorithmId => id;

        public int FromSchemaVersion => 1;

        public int ToSchemaVersion => 2;

        public JsonElement Migrate(JsonElement parameters) => parameters.Clone();
    }

    private sealed class TestProvider(string id, int priority, bool throws = false) : IImageAlgorithmProvider
    {
        public IAlgorithmParameters? LastParameters { get; private set; }

        public AlgorithmProviderMetadata Metadata { get; } = new(
            id,
            id,
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            priority,
            AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = null;
            return true;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (throws) throw new InvalidOperationException("test provider failure");
            LastParameters = context.Parameters;
            return ValueTask.FromResult(new AlgorithmResult
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Succeeded,
            });
        }
    }

    private sealed class CancellingOutputProvider(CancellationTokenSource cancellation) : IImageAlgorithmProvider
    {
        public AlgorithmImageBuffer Output { get; } = Buffer(200);

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "cancel-output",
            "cancel-output",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            1,
            AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = null;
            return true;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellation.Cancel();
            return ValueTask.FromResult(new AlgorithmResult
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [new AlgorithmImageArtifact("image", "primary", Output)],
            });
        }
    }

    private sealed class InvalidOutputProvider : IImageAlgorithmProvider
    {
        public AlgorithmImageBuffer Output { get; } = new(1, 1, 3, AlgorithmImageFormat.Bgr24, [1, 2, 3]);

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "invalid-output",
            "invalid-output",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            1,
            AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 });

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = null;
            return true;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(new AlgorithmResult
            {
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = [new AlgorithmImageArtifact("image", "primary", Output)],
            });
    }

    private sealed class InlineProgress(Action<AlgorithmProgress> report) : IProgress<AlgorithmProgress>
    {
        public void Report(AlgorithmProgress value) => report(value);
    }
}
