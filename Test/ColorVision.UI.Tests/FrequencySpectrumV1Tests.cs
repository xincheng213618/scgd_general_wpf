using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.FrequencySpectrum;
using OpenCvSharp;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class FrequencySpectrumV1Tests
{
    public static TheoryData<AlgorithmImageFormat> CanonicalFormats => new()
    {
        AlgorithmImageFormat.Gray8, AlgorithmImageFormat.Gray16, AlgorithmImageFormat.Gray32Float,
        AlgorithmImageFormat.Bgr24, AlgorithmImageFormat.Bgr48, AlgorithmImageFormat.Bgr96Float,
        AlgorithmImageFormat.Bgra32, AlgorithmImageFormat.Bgra64, AlgorithmImageFormat.Bgra128Float,
    };

    [Fact]
    public void CatalogDefaultsAliasesFieldsAndHostPolicyAreStable()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        AlgorithmDescriptor descriptor = Assert.Single(catalog.Descriptors, value => value.Id == StandardAlgorithmIds.FrequencySpectrum);
        Assert.Equal(new AlgorithmVersion(1, 0, 0), descriptor.Version);
        Assert.Equal("magnitude-and-power=gray8-display; quantitative-values=measurement/table/structured-data", descriptor.OutputFormatPolicy);
        Assert.Equal(new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 }, descriptor.OutputFormats);
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Interactive));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Batch));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Flow));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Headless));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Copilot));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.MultiInput));
        Assert.True(catalog.TryResolveAlias("FFTAnalysis", out AlgorithmDescriptor? alias));
        Assert.Equal(descriptor.Id, alias!.Id);
        FrequencySpectrumParameters defaults = descriptor.ParameterSchema.Defaults.Deserialize<FrequencySpectrumParameters>(AlgorithmJson.Options)!;
        Assert.True(defaults.Validate().IsValid);
        Assert.Equal(FrequencyWindowFunction.Hann, defaults.WindowFunction);
        Assert.Contains(descriptor.ParameterSchema.Fields, value => value.Name == nameof(FrequencySpectrumParameters.PeakRelativePowerThreshold)
            && value.Minimum == 0 && value.Maximum == 1 && value.Unit == "ratio");

        string json = JsonSerializer.Serialize(defaults, AlgorithmJson.Options);
        FrequencySpectrumParameters roundTrip = JsonSerializer.Deserialize<FrequencySpectrumParameters>(json, AlgorithmJson.Options)!;
        Assert.Equal(defaults.DirectionBinWidthDegrees, roundTrip.DirectionBinWidthDegrees);
        Assert.Equal(defaults.MaximumPixels, roundTrip.MaximumPixels);
    }

    [Fact]
    public void ValidationRejectsEnumRangesOrderingAndResourceLimits()
    {
        FrequencySpectrumParameters parameters = new()
        {
            WindowFunction = (FrequencyWindowFunction)999,
            VisualizationScale = (FrequencySpectrumVisualizationScale)999,
            RadialBinWidthCyclesPerPixel = 0,
            DirectionBinWidthDegrees = 181,
            MinimumPeakFrequencyCyclesPerPixel = 0.4,
            MaximumPeakFrequencyCyclesPerPixel = 0.3,
            PeakRelativePowerThreshold = 1.1,
            PeakNeighborhoodRadius = 0,
            MaximumPeaks = 0,
            MaximumPixels = 0,
        };
        AlgorithmValidationResult validation = parameters.Validate();
        Assert.Contains(validation.Issues, value => value.Path == nameof(parameters.WindowFunction));
        Assert.Contains(validation.Issues, value => value.Path == nameof(parameters.VisualizationScale));
        Assert.Contains(validation.Issues, value => value.Code == "frequency_range_order");
        Assert.Contains(validation.Issues, value => value.Path == nameof(parameters.MaximumPixels));
    }

    [Fact]
    public async Task ConstantRectangularGoldenPreservesQuantitativeRangeAndInverse()
    {
        using AlgorithmImageBuffer source = ConstantGray8(16, 8, 100);
        using AlgorithmResult result = await RunAsync(source, new FrequencySpectrumParameters
        {
            WindowFunction = FrequencyWindowFunction.Rectangular,
            RemoveMean = false,
            CenterSpectrum = false,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(100, Measurement(result, "frequency.maximum_magnitude"), 5);
        Assert.Equal(10_000, Measurement(result, "frequency.maximum_power"), 3);
        Assert.InRange(Measurement(result, "frequency.inverse_rmse"), 0, 1e-5);
        Assert.Equal(255, Image(result, "magnitude-spectrum").Data.Span[0]);
        Assert.Equal(255, Image(result, "power-spectrum").Data.Span[0]);
        Assert.All(Image(result, "magnitude-spectrum").Data.Span[1..].ToArray(), value => Assert.Equal(0, value));
        Assert.Empty(Table(result, "frequency-peaks").Rows);
        AlgorithmStructuredDataArtifact structured = result.GetArtifact<AlgorithmStructuredDataArtifact>("frequency-spectrum")!;
        Assert.Equal(FrequencySpectrumAlgorithmProvider.ResultSchema, structured.Schema);
        Assert.Equal("sqrt(re^2+im^2)/windowSum", structured.Data.GetProperty("spectrum").GetProperty("magnitudeDefinition").GetString());
    }

    [Fact]
    public async Task CenteringMovesDcDisplayWithoutChangingQuantitativeSpectrum()
    {
        using AlgorithmImageBuffer rawSource = ConstantGray8(8, 6, 10);
        using AlgorithmResult raw = await RunAsync(rawSource, new FrequencySpectrumParameters
        {
            WindowFunction = FrequencyWindowFunction.Rectangular,
            RemoveMean = false,
            CenterSpectrum = false,
        });
        using AlgorithmImageBuffer centeredSource = ConstantGray8(8, 6, 10);
        using AlgorithmResult centered = await RunAsync(centeredSource, new FrequencySpectrumParameters
        {
            WindowFunction = FrequencyWindowFunction.Rectangular,
            RemoveMean = false,
            CenterSpectrum = true,
        });
        Assert.Equal(255, Image(raw, "magnitude-spectrum").Data.Span[0]);
        Assert.Equal(255, Image(centered, "magnitude-spectrum").Data.Span[3 * 8 + 4]);
        Assert.Equal(Measurement(raw, "frequency.maximum_power"), Measurement(centered, "frequency.maximum_power"), 12);
    }

    [Fact]
    public async Task SinusoidGoldenReportsFrequencyPeriodAndOrthogonalSpatialDirection()
    {
        const int width = 64;
        const int height = 32;
        const int cycles = 8;
        using AlgorithmImageBuffer source = FloatSinusoid(width, height, cycles);
        using AlgorithmResult result = await RunAsync(source, new FrequencySpectrumParameters
        {
            WindowFunction = FrequencyWindowFunction.Rectangular,
            RemoveMean = true,
            MinimumPeakFrequencyCyclesPerPixel = 0.02,
            PeakRelativePowerThreshold = 0.5,
            PeakNeighborhoodRadius = 1,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(cycles / (double)width, Measurement(result, "frequency.dominant.cycles_per_pixel"), 8);
        Assert.Equal(width / (double)cycles, Measurement(result, "frequency.dominant.period_pixels"), 8);
        Assert.Equal(0, Measurement(result, "frequency.dominant.frequency_direction_degrees"), 8);
        Assert.Equal(90, Measurement(result, "frequency.dominant.spatial_direction_degrees"), 8);
        Assert.Single(Table(result, "frequency-peaks").Rows);
        Assert.Contains(Table(result, "frequency-radial-spectrum").Rows, row => row["MaximumPower"].GetDouble() > 0);
        Assert.Contains(Table(result, "frequency-directional-spectrum").Rows, row => row["TotalPower"].GetDouble() > 0);
        Assert.InRange(Measurement(result, "frequency.inverse_rmse"), 0, 1e-4);
    }

    [Fact]
    public async Task EvenGridNyquistPeakIsNotLostByConjugatePairCanonicalization()
    {
        byte[] pixels = new byte[32 * 16];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 32; x++) pixels[y * 32 + x] = x % 2 == 0 ? (byte)32 : (byte)224;
        using AlgorithmImageBuffer source = new(32, 16, 32, AlgorithmImageFormat.Gray8, pixels);
        using AlgorithmResult result = await RunAsync(source, new FrequencySpectrumParameters
        {
            WindowFunction = FrequencyWindowFunction.Rectangular,
            RemoveMean = true,
            MinimumPeakFrequencyCyclesPerPixel = 0.49,
            MaximumPeakFrequencyCyclesPerPixel = 0.51,
            PeakRelativePowerThreshold = 0.5,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(0.5, Measurement(result, "frequency.dominant.cycles_per_pixel"), 12);
        Assert.Equal(2, Measurement(result, "frequency.dominant.period_pixels"), 12);
        Assert.Single(Table(result, "frequency-peaks").Rows);
    }

    [Theory]
    [InlineData(FrequencyWindowFunction.Rectangular)]
    [InlineData(FrequencyWindowFunction.Hann)]
    [InlineData(FrequencyWindowFunction.Hamming)]
    [InlineData(FrequencyWindowFunction.Blackman)]
    public async Task EveryWindowHasFiniteSpectrumAndNumericallyVerifiedInverse(FrequencyWindowFunction window)
    {
        using AlgorithmImageBuffer source = FloatSinusoid(48, 36, 6);
        using AlgorithmResult result = await RunAsync(source, new FrequencySpectrumParameters { WindowFunction = window });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.True(double.IsFinite(Measurement(result, "frequency.maximum_power")));
        Assert.InRange(Measurement(result, "frequency.inverse_rmse"), 0, 2e-5);
        Assert.InRange(Measurement(result, "frequency.inverse_maximum_error"), 0, 2e-4);
    }

    [Theory]
    [MemberData(nameof(CanonicalFormats))]
    public async Task EveryCanonicalFormatIsReadOnlyAndProducesGray8DisplayArtifacts(AlgorithmImageFormat format)
    {
        using AlgorithmImageBuffer source = Pattern(12, 10, format);
        byte[] before = source.Data.ToArray();
        using AlgorithmResult result = await RunAsync(source, new FrequencySpectrumParameters { WindowFunction = FrequencyWindowFunction.Hamming });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(before, source.Data.ToArray());
        Assert.Equal(2, result.Artifacts.OfType<AlgorithmImageArtifact>().Count());
        Assert.All(result.Artifacts.OfType<AlgorithmImageArtifact>(), artifact =>
        {
            Assert.Equal(AlgorithmImageFormat.Gray8, artifact.Image.Format);
            Assert.Equal(source.Width, artifact.Image.Width);
            Assert.Equal(source.Height, artifact.Image.Height);
            Assert.Contains("normalized", artifact.Metadata!["valueSemantics"], StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task NonFinitePixelLimitAndRoiFailuresAreStructured()
    {
        using AlgorithmImageBuffer nonfinite = FloatBuffer(2, 1, [0.1f, float.NaN]);
        using AlgorithmResult invalid = await RunAsync(nonfinite, new FrequencySpectrumParameters());
        Assert.Contains(invalid.Failures, value => value.Code == "frequency_float_out_of_nominal_range");

        using AlgorithmImageBuffer outOfRange = FloatBuffer(2, 1, [0.1f, -0.01f]);
        using AlgorithmResult range = await RunAsync(outOfRange, new FrequencySpectrumParameters());
        Assert.Contains(range.Failures, value => value.Code == "frequency_float_out_of_nominal_range");

        using AlgorithmImageBuffer limitedSource = ConstantGray8(4, 4, 1);
        using AlgorithmResult limited = await RunAsync(limitedSource, new FrequencySpectrumParameters { MaximumPixels = 4 });
        Assert.Contains(limited.Failures, value => value.Code == "frequency_pixel_limit_exceeded");

        using AlgorithmImageBuffer roiSource = ConstantGray8(4, 4, 1);
        using AlgorithmResult roi = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.FrequencySpectrum, new FrequencySpectrumParameters(), new RectangleAlgorithmRoi(0, 0, 2, 2)),
            Inputs = [Input(roiSource)],
        });
        Assert.Contains(roi.Failures, value => value.Code == "roi_kind_unsupported");
    }

    [Fact]
    public async Task SuccessFailureCancellationAndResultDisposalReleaseOwnership()
    {
        AlgorithmImageBuffer successSource = Pattern(64, 48, AlgorithmImageFormat.Gray16);
        AlgorithmResult success = await RunTransferredAsync(successSource, new FrequencySpectrumParameters());
        AlgorithmImageBuffer[] outputs = success.Artifacts.OfType<AlgorithmImageArtifact>().Select(value => value.Image).ToArray();
        Assert.True(successSource.IsDisposed);
        Assert.All(outputs, value => Assert.False(value.IsDisposed));
        success.Dispose();
        Assert.All(outputs, value => Assert.True(value.IsDisposed));

        AlgorithmImageBuffer failedSource = ConstantGray8(8, 8, 1);
        using AlgorithmResult failed = await RunTransferredAsync(failedSource, new FrequencySpectrumParameters { MaximumPixels = 1 });
        Assert.True(failedSource.IsDisposed);
        Assert.Equal(AlgorithmResultStatus.Failed, failed.Status);

        AlgorithmImageBuffer cancelledSource = Pattern(2048, 1024, AlgorithmImageFormat.Bgra32);
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "frequency.window") cancellation.Cancel();
        });
        using AlgorithmResult cancelled = await RunTransferredAsync(cancelledSource, new FrequencySpectrumParameters(), progress, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, cancelled.Status);
        Assert.True(cancelledSource.IsDisposed);
    }

    [Fact]
    public async Task BatchAndFlowUseTheSameCatalogInvocationAndQuantitativeArtifacts()
    {
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.FrequencySpectrum, new FrequencySpectrumParameters
        {
            WindowFunction = FrequencyWindowFunction.Rectangular,
            RemoveMean = true,
            MinimumPeakFrequencyCyclesPerPixel = 0.02,
            PeakRelativePowerThreshold = 0.5,
        });
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-frequency-{Guid.NewGuid():N}");
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
            BatchAlgorithmAnalysisFileResult file = Assert.Single(batch.Files);
            Assert.True(file.Status == AlgorithmResultStatus.Succeeded, file.ErrorMessage);
            Assert.Contains("frequency.dominant.period_pixels", File.ReadAllText(Assert.Single(file.OutputPaths)), StringComparison.Ordinal);

            byte[] pixels = ByteSinusoid(64, 32, 8);
            LocalFrameMetadata metadata = new() { Width = 64, Height = 32, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, pixels.Length, 0);
            using LocalFlowFrameLease lease = frame.Acquire();
            Marshal.Copy(pixels, 0, lease.RawPointer, pixels.Length);
            using AlgorithmResult flow = await LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(ExperimentalAlgorithmTestRuntime.Runtime, lease, invocation);
            Assert.Equal(AlgorithmResultStatus.Succeeded, flow.Status);
            Assert.Equal(8, Measurement(flow, "frequency.dominant.period_pixels"), 6);
            Assert.Equal(pixels, lease.CopyRawToArray());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ResultWindowDisplaysArtifactsAndDisposesOwnedResult()
    {
        using AlgorithmImageBuffer source = FloatSinusoid(32, 16, 4);
        AlgorithmResult result = await RunAsync(source, new FrequencySpectrumParameters { WindowFunction = FrequencyWindowFunction.Rectangular });
        WpfTestHost.Invoke(() =>
        {
            FrequencySpectrumResultWindow window = new(result);
            window.Show();
            Assert.NotNull(window.FindName("MagnitudePreview"));
            Assert.NotNull(window.FindName("PowerPreview"));
            Assert.NotNull(window.FindName("PeaksGrid"));
            window.Close();
        });
        Assert.True(result.IsDisposed);
    }

    [Fact]
    public async Task ExportProducesTablesAndRefusesOverwrite()
    {
        using AlgorithmImageBuffer source = FloatSinusoid(32, 16, 4);
        using AlgorithmResult result = await RunAsync(source, new FrequencySpectrumParameters { WindowFunction = FrequencyWindowFunction.Rectangular });
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-frequency-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string json = AlgorithmResultExporter.ExportJson(result, Path.Combine(directory, "frequency.json"));
            IReadOnlyList<string> csv = AlgorithmResultExporter.ExportCsvBundle(result, Path.Combine(directory, "frequency.csv"));
            Assert.True(File.Exists(json));
            Assert.Contains(csv, path => File.ReadAllText(path).Contains("Frequency", StringComparison.Ordinal));
            Assert.Throws<IOException>(() => AlgorithmResultExporter.ExportJson(result, json));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<AlgorithmResult> RunAsync(AlgorithmImageBuffer source, FrequencySpectrumParameters parameters)
        => await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.FrequencySpectrum, parameters),
            Inputs = [Input(source)],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

    private static async Task<AlgorithmResult> RunTransferredAsync(
        AlgorithmImageBuffer source,
        FrequencySpectrumParameters parameters,
        IProgress<AlgorithmProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.FrequencySpectrum, parameters),
            Inputs = [new AlgorithmInput { Name = "source", Image = source, Ownership = AlgorithmInputOwnership.Transferred, ColorSpace = "encoded-device-values" }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
            Progress = progress,
        }, cancellationToken);

    private static AlgorithmInput Input(AlgorithmImageBuffer source) => new()
    {
        Name = "source",
        Image = source,
        Ownership = AlgorithmInputOwnership.Borrowed,
        ColorSpace = "encoded-device-values",
    };

    private static double Measurement(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmMeasurementArtifact>("frequency-spectrum-summary")!.Measurements.Single(value => value.Name == name).Value;

    private static AlgorithmImageBuffer Image(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmImageArtifact>(name)!.Image;

    private static AlgorithmTableArtifact Table(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmTableArtifact>(name)!;

    private static AlgorithmImageBuffer ConstantGray8(int width, int height, byte value)
        => new(width, height, width, AlgorithmImageFormat.Gray8, Enumerable.Repeat(value, width * height).ToArray());

    private static AlgorithmImageBuffer FloatSinusoid(int width, int height, int cycles)
    {
        float[] values = new float[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                values[y * width + x] = (float)(0.5 + 0.25 * Math.Cos(2 * Math.PI * cycles * x / width));
        return FloatBuffer(width, height, values);
    }

    private static byte[] ByteSinusoid(int width, int height, int cycles)
    {
        byte[] values = new byte[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                values[y * width + x] = (byte)Math.Round(128 + 60 * Math.Cos(2 * Math.PI * cycles * x / width));
        return values;
    }

    private static AlgorithmImageBuffer FloatBuffer(int width, int height, IReadOnlyList<float> values)
    {
        byte[] data = new byte[checked(width * height * 4)];
        for (int index = 0; index < values.Count; index++) BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(index * 4, 4), values[index]);
        return new AlgorithmImageBuffer(width, height, width * 4, AlgorithmImageFormat.Gray32Float, data);
    }

    private static AlgorithmImageBuffer Pattern(int width, int height, AlgorithmImageFormat format)
    {
        int stride = checked(width * format.BytesPerPixel());
        byte[] data = new byte[checked(stride * height)];
        int bytes = format.BitsPerChannel() / 8;
        int channels = format.Channels();
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                for (int channel = 0; channel < channels; channel++)
                {
                    int offset = y * stride + (x * channels + channel) * bytes;
                    double normalized = channel == 3 ? 1 : ((x * 17 + y * 13 + channel * 23) % 251) / 255d;
                    if (bytes == 1) data[offset] = (byte)Math.Round(normalized * 255);
                    else if (bytes == 2) BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), (ushort)Math.Round(normalized * 65535));
                    else BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(offset, 4), (float)normalized);
                }
            }
        }
        return new AlgorithmImageBuffer(width, height, stride, format, data, 123, 117);
    }

    private sealed class InlineProgress(Action<AlgorithmProgress> report) : IProgress<AlgorithmProgress>
    {
        public void Report(AlgorithmProgress value) => report(value);
    }

    private sealed class TestLoader : IBatchImageLoader
    {
        public IReadOnlyCollection<string> Extensions { get; } = [".fake"];

        public Mat Load(string filePath)
        {
            byte[] pixels = ByteSinusoid(64, 32, 8);
            Mat mat = new(32, 64, MatType.CV_8UC1);
            Marshal.Copy(pixels, 0, mat.Data, pixels.Length);
            return mat;
        }
    }
}
