using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Windows.Media.Imaging;
using Xunit.Abstractions;

namespace ColorVision.UI.Tests;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class ImageAlgorithmPerformanceGateCollection
{
    public const string CollectionName = "ImageAlgorithmPerformanceGate";
}

[Collection(ImageAlgorithmPerformanceGateCollection.CollectionName)]
[Trait("Category", "PerformanceProbe")]
public sealed class ImageAlgorithmPerformanceGateTests
{
    private const string OptInVariable = "COLORVISION_IMAGE_ALGORITHM_PERF";
    private readonly ITestOutputHelper _output;

    public ImageAlgorithmPerformanceGateTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void OpenCvBorrowBoundaryDoesNotAllocateAPixelSizedManagedCopy()
    {
        using AlgorithmImageBuffer input = CreateBuffer(2048, 2048, AlgorithmImageFormat.Bgra32);
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

        using AlgorithmImageMatLease lease = AlgorithmImageInterop.BorrowReadOnly(input);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        Assert.Equal(input.Width, lease.Mat.Cols);
        Assert.Equal(input.Height, lease.Mat.Rows);
        Assert.True(allocated < 1024 * 1024, $"Borrowing the input allocated {allocated:N0} managed bytes.");
    }

    [Fact]
    public void PreviewProjectionDoesNotAllocateAPixelSizedManagedCopy()
    {
        using AlgorithmImageBuffer input = CreateBuffer(2048, 2048, AlgorithmImageFormat.Bgra32);
        long allocated = RunInSta(() =>
        {
            using AlgorithmImageBuffer warmup = CreateBuffer(1, 1, AlgorithmImageFormat.Bgra32);
            _ = ImageAlgorithmInputFactory.ToWriteableBitmap(warmup);
            long before = GC.GetAllocatedBytesForCurrentThread();
            WriteableBitmap bitmap = ImageAlgorithmInputFactory.ToWriteableBitmap(input);
            Assert.Equal(input.Width, bitmap.PixelWidth);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        });

        Assert.True(allocated < 2 * 1024 * 1024, $"Projecting the preview allocated {allocated:N0} managed bytes.");
    }

    [Fact]
    public async Task ProfileResultBudgetRejectsBeforeLargeTableAllocation()
    {
        static async Task<AlgorithmResult> RunAsync()
        {
            using AlgorithmImageBuffer input = CreateBuffer(151, 1, AlgorithmImageFormat.Bgra32);
            return await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
            {
                Invocation = AlgorithmInvocation.Create(
                    StandardAlgorithmIds.ImageProfile,
                    new ImageProfileParameters
                    {
                        SampleSpacingPixels = 0.01,
                        MaximumSamples = ImageProfileParameters.AbsoluteMaximumSamples,
                    },
                    new PolylineAlgorithmRoi([new(0, 0), new(150, 0)])),
                Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
                RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
            });
        }

        using (AlgorithmResult warmup = await RunAsync())
            Assert.Contains(warmup.Failures, failure => failure.Code == "profile_result_budget_exceeded");
        Collect();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);

        using AlgorithmResult result = await RunAsync();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;

        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, failure => failure.Code == "profile_result_budget_exceeded");
        Assert.Empty(result.Artifacts);
        Assert.True(allocated < 8L * 1024 * 1024,
            $"Rejecting the oversized profile allocated {allocated:N0} managed bytes.");
    }

    [Fact]
    public async Task RoiBadPixelTopKCapBoundsManagedCandidateStorage()
    {
        static AlgorithmImageBuffer Checkerboard(int size)
        {
            byte[] pixels = new byte[checked(size * size)];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = (byte)(((x + y) & 1) == 0 ? 0 : 255);
            return new AlgorithmImageBuffer(size, size, size, AlgorithmImageFormat.Gray8, pixels);
        }

        static async Task<AlgorithmResult> RunAsync(AlgorithmImageBuffer input)
            => await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
            {
                Invocation = AlgorithmInvocation.Create(
                    StandardAlgorithmIds.RoiStatistics,
                    new RoiStatisticsParameters
                    {
                        HistogramBins = 16,
                        DetectBadPixelCandidates = true,
                        BadPixelSigmaThreshold = 0.1,
                        BadPixelMinimumDeviationFraction = 0,
                        MaximumBadPixelCandidates = 1,
                    },
                    new RectangleAlgorithmRoi(0, 0, input.Width, input.Height)),
                Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
                RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Roi,
            });

        using (AlgorithmImageBuffer warmupInput = Checkerboard(32))
        using (AlgorithmResult warmup = await RunAsync(warmupInput))
            Assert.Single(warmup.GetArtifact<AlgorithmTableArtifact>("bad-pixel-candidates")!.Rows);

        using AlgorithmImageBuffer input = Checkerboard(1_024);
        Collect();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        Stopwatch stopwatch = Stopwatch.StartNew();

        using AlgorithmResult result = await RunAsync(input);
        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Single(result.GetArtifact<AlgorithmTableArtifact>("bad-pixel-candidates")!.Rows);
        Assert.True(allocated < 16L * 1024 * 1024,
            $"Bounded ROI bad-pixel detection allocated {allocated:N0} managed bytes.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15),
            $"Bounded ROI bad-pixel detection took {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData(3840, 2160, AlgorithmImageFormat.Gray16)]
    [InlineData(3840, 2160, AlgorithmImageFormat.Bgra32)]
    [InlineData(7680, 4320, AlgorithmImageFormat.Gray16)]
    [InlineData(7680, 4320, AlgorithmImageFormat.Bgra32)]
    public void PreviewPipelineProbe(int width, int height, AlgorithmImageFormat format)
    {
        if (!Enabled()) return;
        using AlgorithmImageBuffer input = CreateBuffer(width, height, format);
        Collect();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        long privateBefore = PrivateBytes();
        Stopwatch stopwatch = Stopwatch.StartNew();

        using AlgorithmResult result = ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Invert, new NoAlgorithmParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local,
        }).AsTask().GetAwaiter().GetResult();
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmImageArtifact image = Assert.IsType<AlgorithmImageArtifact>(result.GetArtifact<AlgorithmImageArtifact>());
        (int bitmapWidth, int bitmapHeight) = RunInSta(() =>
        {
            WriteableBitmap bitmap = ImageAlgorithmInputFactory.ToWriteableBitmap(image.Image);
            return (bitmap.PixelWidth, bitmap.PixelHeight);
        });

        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        long privateDelta = Math.Max(0, PrivateBytes() - privateBefore);
        _output.WriteLine(
            $"preview {width}x{height} {format}: elapsed={stopwatch.Elapsed.TotalMilliseconds:F1}ms, "
            + $"managed-allocated={allocated:N0}B, private-delta={privateDelta:N0}B, frame={input.Data.Length:N0}B");
        long managedBudget = checked(input.Data.Length + 16L * 1024 * 1024);
        Assert.Equal(width, bitmapWidth);
        Assert.Equal(height, bitmapHeight);
        Assert.True(allocated <= managedBudget, $"Managed allocation {allocated:N0} exceeded budget {managedBudget:N0}.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), $"Preview latency was {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData(3840, 2160, AlgorithmImageFormat.Gray16)]
    [InlineData(3840, 2160, AlgorithmImageFormat.Bgra32)]
    [InlineData(7680, 4320, AlgorithmImageFormat.Gray16)]
    [InlineData(7680, 4320, AlgorithmImageFormat.Bgra32)]
    public void ComparisonPipelineProbe(int width, int height, AlgorithmImageFormat format)
    {
        if (!Enabled()) return;
        using AlgorithmImageBuffer reference = CreateBuffer(width, height, format);
        using AlgorithmImageBuffer candidate = CreateBuffer(width, height, format);
        ImageComparisonParameters parameters = new()
        {
            EnableSsim = false,
            EnableAlignmentPrecheck = false,
        };
        Collect();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        long privateBefore = PrivateBytes();
        Stopwatch stopwatch = Stopwatch.StartNew();

        using AlgorithmResult result = ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = new AlgorithmInvocation
            {
                AlgorithmId = StandardAlgorithmIds.ImageComparison,
                ParameterSchemaVersion = parameters.SchemaVersion,
                Parameters = AlgorithmJson.ToElement(parameters),
                Metadata = ImageComparisonOutputPlan.CreateMetadata(ImageComparisonArtifactOutputs.Heatmap),
            },
            Inputs =
            [
                new AlgorithmInput { Name = "reference", Image = reference, ColorSpace = "encoded-device-values" },
                new AlgorithmInput { Name = "candidate", Image = candidate, ColorSpace = "encoded-device-values" },
            ],
            RequiredCapabilities = AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
        }).AsTask().GetAwaiter().GetResult();
        AlgorithmImageArtifact heatmap = Assert.IsType<AlgorithmImageArtifact>(result.GetArtifact<AlgorithmImageArtifact>("difference-heatmap"));
        (int bitmapWidth, int bitmapHeight) = RunInSta(() =>
        {
            WriteableBitmap bitmap = ImageAlgorithmInputFactory.ToWriteableBitmap(heatmap.Image);
            return (bitmap.PixelWidth, bitmap.PixelHeight);
        });

        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        long privateDelta = Math.Max(0, PrivateBytes() - privateBefore);
        long pixels = checked((long)width * height);
        long retainedArtifactBytes = checked(pixels * 3L);
        _output.WriteLine(
            $"comparison {width}x{height} {format}: elapsed={stopwatch.Elapsed.TotalMilliseconds:F1}ms, "
            + $"managed-allocated={allocated:N0}B, private-delta={privateDelta:N0}B, "
            + $"retained-image-artifacts={retainedArtifactBytes:N0}B");
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Single(result.Artifacts.OfType<AlgorithmImageArtifact>());
        Assert.Equal(width, bitmapWidth);
        Assert.Equal(height, bitmapHeight);
        Assert.True(allocated >= retainedArtifactBytes);
        Assert.True(allocated <= retainedArtifactBytes + 32L * 1024 * 1024,
            $"Comparison allocation {allocated:N0} exceeded artifact budget {retainedArtifactBytes:N0} + 32 MiB.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(60), $"Comparison latency was {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData(3840, 2160, AlgorithmImageFormat.Gray16)]
    [InlineData(3840, 2160, AlgorithmImageFormat.Bgra32)]
    public void ContourPipelineProbe(int width, int height, AlgorithmImageFormat format)
    {
        if (!Enabled()) return;
        using AlgorithmImageBuffer input = CreateBuffer(width, height, format);
        Collect();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        long privateBefore = PrivateBytes();
        Stopwatch stopwatch = Stopwatch.StartNew();

        using AlgorithmResult result = ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.Contours, new ContourAnalysisParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        }).AsTask().GetAwaiter().GetResult();

        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        long privateDelta = Math.Max(0, PrivateBytes() - privateBefore);
        long maskBytes = checked((long)width * height);
        _output.WriteLine(
            $"contours {width}x{height} {format}: elapsed={stopwatch.Elapsed.TotalMilliseconds:F1}ms, "
            + $"managed-allocated={allocated:N0}B, private-delta={privateDelta:N0}B, mask={maskBytes:N0}B");
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmMeasurementArtifact summary = Assert.IsType<AlgorithmMeasurementArtifact>(result.GetArtifact<AlgorithmMeasurementArtifact>("contour-summary"));
        Assert.Equal(0, summary.Measurements.Single(item => item.Name == "contour.candidate_count").Value);
        Assert.True(allocated <= maskBytes + 16L * 1024 * 1024,
            $"Contour allocation {allocated:N0} exceeded one-mask budget {maskBytes:N0} + 16 MiB.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), $"Contour latency was {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData(3840, 2160, AlgorithmImageFormat.Gray16)]
    [InlineData(3840, 2160, AlgorithmImageFormat.Bgra32)]
    public void GeometricTransformPipelineProbe(int width, int height, AlgorithmImageFormat format)
    {
        if (!Enabled()) return;
        using AlgorithmImageBuffer input = CreateBuffer(width, height, format);
        GeometricTransformParameters parameters = new()
        {
            Kind = GeometricTransformKind.Perspective,
            M11 = 0.999,
            M12 = 0.002,
            M13 = 3,
            M21 = -0.001,
            M22 = 1.001,
            M23 = 2,
            M31 = 0.0000005,
            M32 = -0.0000003,
        };
        Collect();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        long privateBefore = PrivateBytes();
        Stopwatch stopwatch = Stopwatch.StartNew();

        using AlgorithmResult result = ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.GeometricTransform, parameters),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        }).AsTask().GetAwaiter().GetResult();

        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        long privateDelta = Math.Max(0, PrivateBytes() - privateBefore);
        long retainedBytes = checked(input.Data.Length + (long)width * height);
        _output.WriteLine(
            $"geometric-transform {width}x{height} {format}: elapsed={stopwatch.Elapsed.TotalMilliseconds:F1}ms, "
            + $"managed-allocated={allocated:N0}B, private-delta={privateDelta:N0}B, retained-images={retainedBytes:N0}B");
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(2, result.Artifacts.OfType<AlgorithmImageArtifact>().Count());
        Assert.True(allocated <= retainedBytes + 16L * 1024 * 1024,
            $"Geometric-transform allocation {allocated:N0} exceeded retained image budget {retainedBytes:N0} + 16 MiB.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), $"Geometric-transform latency was {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData(3840, 2160, AlgorithmImageFormat.Gray16)]
    [InlineData(3840, 2160, AlgorithmImageFormat.Bgra32)]
    public void ImageRegistrationPipelineProbe(int width, int height, AlgorithmImageFormat format)
    {
        if (!Enabled()) return;
        using AlgorithmImageBuffer reference = CreatePatternBuffer(width, height, format);
        byte[] movingBytes = reference.Data.ToArray();
        movingBytes[0] ^= 0x5A;
        using AlgorithmImageBuffer moving = new(width, height, reference.Stride, format, movingBytes);
        Collect();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        long privateBefore = PrivateBytes();
        Stopwatch stopwatch = Stopwatch.StartNew();

        using AlgorithmResult result = ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageRegistration, new ImageRegistrationParameters { MinimumPhaseResponse = 0 }),
            Inputs =
            [
                new AlgorithmInput { Name = "reference", Image = reference, Ownership = AlgorithmInputOwnership.Borrowed, ColorSpace = "encoded-device-values" },
                new AlgorithmInput { Name = "moving", Image = moving, Ownership = AlgorithmInputOwnership.Borrowed, ColorSpace = "encoded-device-values" },
            ],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
        }).AsTask().GetAwaiter().GetResult();

        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        long privateDelta = Math.Max(0, PrivateBytes() - privateBefore);
        long retainedBytes = checked(reference.Data.Length + (long)width * height);
        _output.WriteLine(
            $"image-registration {width}x{height} {format}: elapsed={stopwatch.Elapsed.TotalMilliseconds:F1}ms, "
            + $"managed-allocated={allocated:N0}B, private-delta={privateDelta:N0}B, retained-images={retainedBytes:N0}B");
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(2, result.Artifacts.OfType<AlgorithmImageArtifact>().Count());
        Assert.True(allocated <= retainedBytes + 32L * 1024 * 1024,
            $"Image-registration allocation {allocated:N0} exceeded retained image budget {retainedBytes:N0} + 32 MiB.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Image-registration latency was {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData(3840, 2160, AlgorithmImageFormat.Gray16)]
    [InlineData(3840, 2160, AlgorithmImageFormat.Bgra32)]
    public void LensDistortionCorrectionPipelineProbe(int width, int height, AlgorithmImageFormat format)
    {
        if (!Enabled()) return;
        using AlgorithmImageBuffer input = CreatePatternBuffer(width, height, format);
        LensDistortionCorrectionParameters parameters = new()
        {
            FxPixels = 2_500,
            FyPixels = 2_480,
            K1 = -0.18,
            K2 = 0.035,
            P1 = 0.0005,
            P2 = -0.0004,
            MinimumValidFraction = 0,
        };
        Collect();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        long privateBefore = PrivateBytes();
        Stopwatch stopwatch = Stopwatch.StartNew();

        using AlgorithmResult result = ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.LensDistortionCorrection, parameters),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        }).AsTask().GetAwaiter().GetResult();

        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        long privateDelta = Math.Max(0, PrivateBytes() - privateBefore);
        long retainedBytes = checked(input.Data.Length + (long)width * height);
        _output.WriteLine(
            $"lens-distortion {width}x{height} {format}: elapsed={stopwatch.Elapsed.TotalMilliseconds:F1}ms, "
            + $"managed-allocated={allocated:N0}B, private-delta={privateDelta:N0}B, retained-images={retainedBytes:N0}B");
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(2, result.Artifacts.OfType<AlgorithmImageArtifact>().Count());
        Assert.True(allocated <= retainedBytes + 16L * 1024 * 1024,
            $"Lens-distortion allocation {allocated:N0} exceeded retained image budget {retainedBytes:N0} + 16 MiB.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Lens-distortion latency was {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData(3840, 2160, AlgorithmImageFormat.Gray16)]
    [InlineData(3840, 2160, AlgorithmImageFormat.Bgra32)]
    public void ImagingCorrectionPipelineProbe(int width, int height, AlgorithmImageFormat format)
    {
        if (!Enabled()) return;
        using AlgorithmImageBuffer source = CreatePatternBuffer(width, height, format);
        using AlgorithmImageBuffer dark = CreateBuffer(width, height, format);
        ImagingCorrectionParameters parameters = new() { EnableDarkFrame = true };
        Collect();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        long privateBefore = PrivateBytes();
        Stopwatch stopwatch = Stopwatch.StartNew();

        using AlgorithmResult result = ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImagingCorrection, parameters),
            Inputs =
            [
                new AlgorithmInput { Name = "source", Image = source, Ownership = AlgorithmInputOwnership.Borrowed, ColorSpace = "encoded-device-values" },
                new AlgorithmInput { Name = "dark-frame", Image = dark, Ownership = AlgorithmInputOwnership.Borrowed, ColorSpace = "encoded-device-values" },
            ],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
        }).AsTask().GetAwaiter().GetResult();

        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        long privateDelta = Math.Max(0, PrivateBytes() - privateBefore);
        long retainedBytes = checked(source.Data.Length + (long)width * height);
        _output.WriteLine(
            $"imaging-correction {width}x{height} {format}: elapsed={stopwatch.Elapsed.TotalMilliseconds:F1}ms, "
            + $"managed-allocated={allocated:N0}B, private-delta={privateDelta:N0}B, retained-images={retainedBytes:N0}B");
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(2, result.Artifacts.OfType<AlgorithmImageArtifact>().Count());
        Assert.True(allocated <= retainedBytes + 16L * 1024 * 1024,
            $"Imaging-correction allocation {allocated:N0} exceeded retained image budget {retainedBytes:N0} + 16 MiB.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Imaging-correction latency was {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData(3840, 2160, AlgorithmImageFormat.Gray16)]
    [InlineData(3840, 2160, AlgorithmImageFormat.Bgra32)]
    [InlineData(7680, 4320, AlgorithmImageFormat.Gray16)]
    public void FrequencySpectrumPipelineProbe(int width, int height, AlgorithmImageFormat format)
    {
        if (!Enabled()) return;
        using AlgorithmImageBuffer source = CreatePatternBuffer(width, height, format);
        FrequencySpectrumParameters parameters = new()
        {
            WindowFunction = FrequencyWindowFunction.Hann,
            MaximumPeaks = 16,
            MaximumPixels = 100_000_000,
        };
        Collect();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        long privateBefore = PrivateBytes();
        Stopwatch stopwatch = Stopwatch.StartNew();

        using AlgorithmResult result = ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.FrequencySpectrum, parameters),
            Inputs = [new AlgorithmInput { Name = "source", Image = source, Ownership = AlgorithmInputOwnership.Borrowed, ColorSpace = "encoded-device-values" }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        }).AsTask().GetAwaiter().GetResult();

        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        long privateDelta = Math.Max(0, PrivateBytes() - privateBefore);
        long retainedBytes = checked(2L * width * height);
        long nativeWorkingBudget = checked(32L * width * height + 256L * 1024 * 1024);
        _output.WriteLine(
            $"frequency-spectrum {width}x{height} {format}: elapsed={stopwatch.Elapsed.TotalMilliseconds:F1}ms, "
            + $"managed-allocated={allocated:N0}B, private-delta={privateDelta:N0}B, retained-images={retainedBytes:N0}B");
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(2, result.Artifacts.OfType<AlgorithmImageArtifact>().Count());
        Assert.True(allocated <= retainedBytes + 64L * 1024 * 1024,
            $"Frequency-spectrum managed allocation {allocated:N0} exceeded retained image budget {retainedBytes:N0} + 64 MiB.");
        Assert.True(privateDelta <= nativeWorkingBudget,
            $"Frequency-spectrum native/private delta {privateDelta:N0} exceeded {nativeWorkingBudget:N0}.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(120), $"Frequency-spectrum latency was {stopwatch.Elapsed}.");
    }

    [Theory]
    [InlineData(3840, 2160, AlgorithmImageFormat.Gray16)]
    [InlineData(3840, 2160, AlgorithmImageFormat.Bgra32)]
    [InlineData(7680, 4320, AlgorithmImageFormat.Gray16)]
    public void MoireAnalysisPipelineProbe(int width, int height, AlgorithmImageFormat format)
    {
        if (!Enabled()) return;
        using AlgorithmImageBuffer source = CreatePatternBuffer(width, height, format);
        MoireAnalysisParameters parameters = new()
        {
            WindowFunction = FrequencyWindowFunction.Hann,
            MaximumSuggestions = 4,
            EnableNotchFilter = true,
            MaximumPixels = 100_000_000,
        };
        Collect();
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        long privateBefore = PrivateBytes();
        Stopwatch stopwatch = Stopwatch.StartNew();

        using AlgorithmResult result = ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.MoireAnalysis, parameters),
            Inputs = [new AlgorithmInput { Name = "source", Image = source, Ownership = AlgorithmInputOwnership.Borrowed, ColorSpace = "encoded-device-values" }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        }).AsTask().GetAwaiter().GetResult();

        stopwatch.Stop();
        long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        long privateDelta = Math.Max(0, PrivateBytes() - privateBefore);
        long retainedBytes = checked(6L * width * height);
        long nativeWorkingBudget = checked(48L * width * height + 256L * 1024 * 1024);
        _output.WriteLine(
            $"moire-analysis {width}x{height} {format}: elapsed={stopwatch.Elapsed.TotalMilliseconds:F1}ms, "
            + $"managed-allocated={allocated:N0}B, private-delta={privateDelta:N0}B, retained-images={retainedBytes:N0}B");
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(3, result.Artifacts.OfType<AlgorithmImageArtifact>().Count());
        Assert.True(allocated <= retainedBytes + 64L * 1024 * 1024,
            $"Moire-analysis managed allocation {allocated:N0} exceeded retained image budget {retainedBytes:N0} + 64 MiB.");
        Assert.True(privateDelta <= nativeWorkingBudget,
            $"Moire-analysis native/private delta {privateDelta:N0} exceeded {nativeWorkingBudget:N0}.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(180), $"Moire-analysis latency was {stopwatch.Elapsed}.");
    }

    private bool Enabled()
    {
        bool enabled = string.Equals(Environment.GetEnvironmentVariable(OptInVariable), "1", StringComparison.Ordinal);
        if (!enabled) _output.WriteLine($"Set {OptInVariable}=1 to run the 4K/8K performance probe.");
        return enabled;
    }

    private static AlgorithmImageBuffer CreateBuffer(int width, int height, AlgorithmImageFormat format)
    {
        int stride = checked(width * format.BytesPerPixel());
        return new AlgorithmImageBuffer(width, height, stride, format, new byte[checked(stride * height)]);
    }

    private static AlgorithmImageBuffer CreatePatternBuffer(int width, int height, AlgorithmImageFormat format)
    {
        int stride = checked(width * format.BytesPerPixel());
        byte[] data = new byte[checked(stride * height)];
        for (int index = 0; index < data.Length; index++) data[index] = unchecked((byte)(index * 31 + index / 97));
        return new AlgorithmImageBuffer(width, height, stride, format, data);
    }

    private static long PrivateBytes()
    {
        using Process process = Process.GetCurrentProcess();
        process.Refresh();
        return process.PrivateMemorySize64;
    }

    private static void Collect()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private static T RunInSta<T>(Func<T> action)
    {
        T? result = default;
        Exception? failure = null;
        Thread thread = new(() =>
        {
            try { result = action(); }
            catch (Exception exception) { failure = exception; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        return result!;
    }
}
