using ColorVision.Algorithms;
using ColorVision.Engine.FlowProcessing.Algorithms;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.MoireAnalysis;
using OpenCvSharp;
using System.Buffers.Binary;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class MoireAnalysisV1Tests
{
    public static TheoryData<AlgorithmImageFormat> CanonicalFormats => new()
    {
        AlgorithmImageFormat.Gray8, AlgorithmImageFormat.Gray16, AlgorithmImageFormat.Gray32Float,
        AlgorithmImageFormat.Bgr24, AlgorithmImageFormat.Bgr48, AlgorithmImageFormat.Bgr96Float,
        AlgorithmImageFormat.Bgra32, AlgorithmImageFormat.Bgra64, AlgorithmImageFormat.Bgra128Float,
    };

    [Fact]
    public void CatalogDefaultsAliasesSchemaAndHostPolicyAreStable()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        AlgorithmDescriptor descriptor = Assert.Single(catalog.Descriptors, value => value.Id == StandardAlgorithmIds.MoireAnalysis);
        Assert.Equal(new AlgorithmVersion(1, 0, 0), descriptor.Version);
        Assert.Equal("spectrum-and-heatmap=gray8-display; optional-filtered-luminance=gray32float", descriptor.OutputFormatPolicy);
        Assert.Equal(new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8, AlgorithmImageFormat.Gray32Float }, descriptor.OutputFormats);
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Interactive));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Batch));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Flow));
        Assert.True(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Headless));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.Copilot));
        Assert.False(descriptor.Capabilities.HasFlag(AlgorithmHostCapabilities.MultiInput));
        Assert.True(catalog.TryResolveAlias("MoireNotchAnalysis", out AlgorithmDescriptor? alias));
        Assert.Equal(descriptor.Id, alias!.Id);
        MoireAnalysisParameters defaults = descriptor.ParameterSchema.Defaults.Deserialize<MoireAnalysisParameters>(AlgorithmJson.Options)!;
        Assert.True(defaults.Validate().IsValid);
        Assert.Equal(FrequencyWindowFunction.Hann, defaults.WindowFunction);
        Assert.False(defaults.EnableNotchFilter);
        Assert.Contains(descriptor.ParameterSchema.Fields, value => value.Name == nameof(MoireAnalysisParameters.MinimumProminenceRatio)
            && value.Minimum == 1 && value.Maximum == 1_000_000 && value.Unit == "ratio");

        string json = JsonSerializer.Serialize(defaults, AlgorithmJson.Options);
        MoireAnalysisParameters roundTrip = JsonSerializer.Deserialize<MoireAnalysisParameters>(json, AlgorithmJson.Options)!;
        Assert.Equal(defaults.NotchSigmaCyclesPerPixel, roundTrip.NotchSigmaCyclesPerPixel);
        Assert.Equal(defaults.MaximumPixels, roundTrip.MaximumPixels);
    }

    [Fact]
    public void ValidationRejectsEnumRangesOrderingAndResourceLimits()
    {
        MoireAnalysisParameters parameters = new()
        {
            WindowFunction = (FrequencyWindowFunction)999,
            MinimumFrequencyCyclesPerPixel = 0.4,
            MaximumFrequencyCyclesPerPixel = 0.3,
            RelativePowerThreshold = 1.1,
            MinimumProminenceRatio = 0.9,
            PeakNeighborhoodRadius = 0,
            MaximumSuggestions = 0,
            NotchSigmaCyclesPerPixel = 0,
            NotchAttenuation = 1.1,
            MaximumPixels = 0,
        };
        AlgorithmValidationResult validation = parameters.Validate();
        Assert.Contains(validation.Issues, value => value.Path == nameof(parameters.WindowFunction));
        Assert.Contains(validation.Issues, value => value.Code == "frequency_range_order");
        Assert.Contains(validation.Issues, value => value.Path == nameof(parameters.MinimumProminenceRatio));
        Assert.Contains(validation.Issues, value => value.Path == nameof(parameters.NotchAttenuation));
        Assert.Contains(validation.Issues, value => value.Path == nameof(parameters.MaximumPixels));
    }

    [Fact]
    public async Task FlatImageHasNoCandidateAndZeroScore()
    {
        using AlgorithmImageBuffer source = ConstantGray8(32, 24, 100);
        using AlgorithmResult result = await RunAsync(source, new MoireAnalysisParameters { WindowFunction = FrequencyWindowFunction.Rectangular });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(0, Measurement(result, "moire.score"), 12);
        Assert.Equal(0, Measurement(result, "moire.candidate_count"), 12);
        Assert.Empty(Table(result).Rows);
        Assert.All(Image(result, "moire-frequency-heatmap").Data.Span.ToArray(), value => Assert.Equal(0, value));
        Assert.Contains(result.Diagnostics.Messages, value => value.Code == "moire_no_periodic_candidate");
    }

    [Fact]
    public async Task SinusoidGoldenReportsEvidencePeriodDirectionAndConjugateSuggestion()
    {
        const int width = 64;
        const int cycles = 8;
        using AlgorithmImageBuffer source = FloatSinusoid(width, 32, cycles);
        using AlgorithmResult result = await RunAsync(source, DetectionParameters());
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.InRange(Measurement(result, "moire.score"), 80, 100);
        Assert.Equal(1, Measurement(result, "moire.candidate_count"), 12);
        Assert.Equal(cycles / (double)width, Measurement(result, "moire.dominant.cycles_per_pixel"), 8);
        Assert.Equal(width / (double)cycles, Measurement(result, "moire.dominant.period_pixels"), 8);
        Assert.Equal(0, Measurement(result, "moire.dominant.frequency_direction_degrees"), 8);
        Assert.Equal(90, Measurement(result, "moire.dominant.spatial_direction_degrees"), 8);
        IReadOnlyDictionary<string, JsonElement> suggestion = Assert.Single(Table(result).Rows);
        Assert.Equal(-suggestion["FrequencyX"].GetDouble(), suggestion["ConjugateX"].GetDouble(), 12);
        Assert.Equal(-suggestion["FrequencyY"].GetDouble(), suggestion["ConjugateY"].GetDouble(), 12);
        Assert.Contains(Image(result, "moire-frequency-heatmap").Data.Span.ToArray(), value => value > 0);
        AlgorithmStructuredDataArtifact structured = result.GetArtifact<AlgorithmStructuredDataArtifact>("moire-analysis")!;
        Assert.Equal(MoireAnalysisAlgorithmProvider.ResultSchema, structured.Schema);
        Assert.Equal("one canonical peak; suggestion/filter always applies symmetric pair",
            structured.Data.GetProperty("detection").GetProperty("conjugatePolicy").GetString());
    }

    [Fact]
    public async Task SymmetricNotchFilterAttenuatesTheDominantPeriodicComponent()
    {
        const int width = 64;
        const int height = 32;
        const int cycles = 8;
        using AlgorithmImageBuffer source = FloatSinusoid(width, height, cycles);
        MoireAnalysisParameters parameters = DetectionParameters();
        parameters.EnableNotchFilter = true;
        parameters.NotchSigmaCyclesPerPixel = 0.012;
        parameters.NotchAttenuation = 0.95;
        using AlgorithmResult result = await RunAsync(source, parameters);
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmImageBuffer filtered = Image(result, "moire-filtered-luminance");
        Assert.Equal(AlgorithmImageFormat.Gray32Float, filtered.Format);
        Assert.InRange(Measurement(result, "moire.filtered_candidate_power_retention"), 0, 0.01);
        double inputAmplitude = HarmonicAmplitude(source, cycles);
        double filteredAmplitude = HarmonicAmplitude(filtered, cycles);
        Assert.True(filteredAmplitude < inputAmplitude * 0.1, $"Filtered amplitude {filteredAmplitude:G6} did not attenuate input {inputAmplitude:G6}.");
        Assert.InRange(Mean(filtered), 0.49, 0.51);
    }

    [Fact]
    public async Task BroadbandNoiseDoesNotProduceAHighMoireScore()
    {
        using AlgorithmImageBuffer source = DeterministicNoise(96, 64);
        using AlgorithmResult result = await RunAsync(source, new MoireAnalysisParameters
        {
            WindowFunction = FrequencyWindowFunction.Hann,
            RelativePowerThreshold = 0.05,
            MinimumProminenceRatio = 6,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.InRange(Measurement(result, "moire.score"), 0, 35);
    }

    [Theory]
    [MemberData(nameof(CanonicalFormats))]
    public async Task EveryCanonicalFormatIsReadOnlyAndProducesExplicitDisplayArtifacts(AlgorithmImageFormat format)
    {
        using AlgorithmImageBuffer source = Pattern(16, 12, format);
        byte[] before = source.Data.ToArray();
        using AlgorithmResult result = await RunAsync(source, new MoireAnalysisParameters { WindowFunction = FrequencyWindowFunction.Hamming });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(before, source.Data.ToArray());
        AlgorithmImageArtifact[] images = result.Artifacts.OfType<AlgorithmImageArtifact>().ToArray();
        Assert.Equal(2, images.Length);
        Assert.All(images, artifact =>
        {
            Assert.Equal(AlgorithmImageFormat.Gray8, artifact.Image.Format);
            Assert.Equal(source.Width, artifact.Image.Width);
            Assert.Equal(source.Height, artifact.Image.Height);
        });
    }

    [Fact]
    public async Task InvalidFloatPixelLimitAndRoiFailuresAreStructured()
    {
        using AlgorithmImageBuffer nonfinite = FloatBuffer(2, 1, [0.1f, float.NaN]);
        using AlgorithmResult invalid = await RunAsync(nonfinite, new MoireAnalysisParameters());
        Assert.Contains(invalid.Failures, value => value.Code == "moire_float_out_of_nominal_range");

        using AlgorithmImageBuffer outOfRange = FloatBuffer(2, 1, [0.1f, 1.01f]);
        using AlgorithmResult range = await RunAsync(outOfRange, new MoireAnalysisParameters());
        Assert.Contains(range.Failures, value => value.Code == "moire_float_out_of_nominal_range");

        using AlgorithmImageBuffer limitedSource = ConstantGray8(4, 4, 1);
        using AlgorithmResult limited = await RunAsync(limitedSource, new MoireAnalysisParameters { MaximumPixels = 4 });
        Assert.Contains(limited.Failures, value => value.Code == "moire_pixel_limit_exceeded");

        using AlgorithmImageBuffer roiSource = ConstantGray8(4, 4, 1);
        using AlgorithmResult roi = await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.MoireAnalysis, new MoireAnalysisParameters(), new RectangleAlgorithmRoi(0, 0, 2, 2)),
            Inputs = [Input(roiSource)],
        });
        Assert.Contains(roi.Failures, value => value.Code == "roi_kind_unsupported");
    }

    [Fact]
    public async Task SuccessFailureCancellationAndResultDisposalReleaseOwnership()
    {
        AlgorithmImageBuffer successSource = Pattern(64, 48, AlgorithmImageFormat.Gray16);
        AlgorithmResult success = await RunTransferredAsync(successSource, new MoireAnalysisParameters());
        AlgorithmImageBuffer[] outputs = success.Artifacts.OfType<AlgorithmImageArtifact>().Select(value => value.Image).ToArray();
        Assert.True(successSource.IsDisposed);
        Assert.All(outputs, value => Assert.False(value.IsDisposed));
        success.Dispose();
        Assert.All(outputs, value => Assert.True(value.IsDisposed));

        AlgorithmImageBuffer failedSource = ConstantGray8(8, 8, 1);
        using AlgorithmResult failed = await RunTransferredAsync(failedSource, new MoireAnalysisParameters { MaximumPixels = 1 });
        Assert.True(failedSource.IsDisposed);
        Assert.Equal(AlgorithmResultStatus.Failed, failed.Status);

        AlgorithmImageBuffer cancelledSource = Pattern(2048, 1024, AlgorithmImageFormat.Bgra32);
        using CancellationTokenSource cancellation = new();
        InlineProgress progress = new(value =>
        {
            if (value.Stage == "moire.window") cancellation.Cancel();
        });
        using AlgorithmResult cancelled = await RunTransferredAsync(cancelledSource, new MoireAnalysisParameters(), progress, cancellation.Token);
        Assert.Equal(AlgorithmResultStatus.Cancelled, cancelled.Status);
        Assert.True(cancelledSource.IsDisposed);
    }

    [Fact]
    public async Task BatchAndFlowReuseTheSameCatalogInvocationAndArtifacts()
    {
        AlgorithmInvocation invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.MoireAnalysis, DetectionParameters());
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-moire-{Guid.NewGuid():N}");
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
            Assert.Contains("moire.dominant.period_pixels", File.ReadAllText(Assert.Single(file.OutputPaths)), StringComparison.Ordinal);

            byte[] pixels = ByteSinusoid(64, 32, 8);
            LocalFrameMetadata metadata = new() { Width = 64, Height = 32, SourceBpp = 8, Channels = 1, PrimaryBufferKind = LocalFrameBufferKind.CvRaw };
            using LocalFlowFrame frame = LocalFlowFrame.Allocate(metadata, pixels.Length, 0);
            using LocalFlowFrameLease lease = frame.Acquire();
            Marshal.Copy(pixels, 0, lease.RawPointer, pixels.Length);
            using AlgorithmResult flow = await LocalFlowImageAlgorithmAdapter.ExecuteRawAsync(ExperimentalAlgorithmTestRuntime.Runtime, lease, invocation);
            Assert.Equal(AlgorithmResultStatus.Succeeded, flow.Status);
            Assert.Equal(8, Measurement(flow, "moire.dominant.period_pixels"), 6);
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
        MoireAnalysisParameters parameters = DetectionParameters();
        parameters.EnableNotchFilter = true;
        AlgorithmResult result = await RunAsync(source, parameters);
        WpfTestHost.Invoke(() =>
        {
            MoireAnalysisResultWindow window = new(result);
            window.Show();
            Assert.NotNull(window.FindName("SpectrumPreview"));
            Assert.NotNull(window.FindName("HeatmapPreview"));
            Assert.NotNull(window.FindName("FilteredPreview"));
            Assert.NotNull(window.FindName("SuggestionsGrid"));
            window.Close();
        });
        Assert.True(result.IsDisposed);
    }

    [Fact]
    public async Task ExportProducesStructuredEvidenceAndRefusesOverwrite()
    {
        using AlgorithmImageBuffer source = FloatSinusoid(32, 16, 4);
        using AlgorithmResult result = await RunAsync(source, DetectionParameters());
        string directory = Path.Combine(Path.GetTempPath(), $"colorvision-moire-export-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            string json = AlgorithmResultExporter.ExportJson(result, Path.Combine(directory, "moire.json"));
            IReadOnlyList<string> csv = AlgorithmResultExporter.ExportCsvBundle(result, Path.Combine(directory, "moire.csv"));
            Assert.Contains("moire-analysis", File.ReadAllText(json), StringComparison.Ordinal);
            Assert.Contains(csv, path => File.ReadAllText(path).Contains("Prominence", StringComparison.Ordinal));
            Assert.Throws<IOException>(() => AlgorithmResultExporter.ExportJson(result, json));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static MoireAnalysisParameters DetectionParameters() => new()
    {
        WindowFunction = FrequencyWindowFunction.Rectangular,
        RemoveMean = true,
        MinimumFrequencyCyclesPerPixel = 0.02,
        RelativePowerThreshold = 0.5,
        MinimumProminenceRatio = 4,
        PeakNeighborhoodRadius = 1,
        MaximumSuggestions = 4,
    };

    private static async Task<AlgorithmResult> RunAsync(AlgorithmImageBuffer source, MoireAnalysisParameters parameters)
        => await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.MoireAnalysis, parameters),
            Inputs = [Input(source)],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

    private static async Task<AlgorithmResult> RunTransferredAsync(
        AlgorithmImageBuffer source,
        MoireAnalysisParameters parameters,
        IProgress<AlgorithmProgress>? progress = null,
        CancellationToken cancellationToken = default)
        => await ExperimentalAlgorithmTestRuntime.Runtime.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.MoireAnalysis, parameters),
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
        => result.GetArtifact<AlgorithmMeasurementArtifact>("moire-analysis-summary")!.Measurements.Single(value => value.Name == name).Value;

    private static AlgorithmImageBuffer Image(AlgorithmResult result, string name)
        => result.GetArtifact<AlgorithmImageArtifact>(name)!.Image;

    private static AlgorithmTableArtifact Table(AlgorithmResult result)
        => result.GetArtifact<AlgorithmTableArtifact>("moire-notch-suggestions")!;

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

    private static AlgorithmImageBuffer DeterministicNoise(int width, int height)
    {
        byte[] data = new byte[width * height];
        uint state = 0x9e3779b9;
        for (int index = 0; index < data.Length; index++)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            data[index] = (byte)(state >> 24);
        }
        return new AlgorithmImageBuffer(width, height, width, AlgorithmImageFormat.Gray8, data);
    }

    private static AlgorithmImageBuffer FloatBuffer(int width, int height, float[] values)
    {
        byte[] data = new byte[checked(width * height * 4)];
        for (int index = 0; index < values.Length; index++) BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(index * 4, 4), values[index]);
        return new AlgorithmImageBuffer(width, height, width * 4, AlgorithmImageFormat.Gray32Float, data);
    }

    private static double HarmonicAmplitude(AlgorithmImageBuffer image, int cycles)
    {
        double real = 0;
        double imaginary = 0;
        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                double value = ReadNormalized(image, x, y);
                double phase = 2 * Math.PI * cycles * x / image.Width;
                real += value * Math.Cos(phase);
                imaginary -= value * Math.Sin(phase);
            }
        }
        return 2 * Math.Sqrt(real * real + imaginary * imaginary) / (image.Width * image.Height);
    }

    private static double Mean(AlgorithmImageBuffer image)
    {
        double sum = 0;
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++) sum += ReadNormalized(image, x, y);
        return sum / (image.Width * image.Height);
    }

    private static double ReadNormalized(AlgorithmImageBuffer image, int x, int y)
    {
        int offset = y * image.Stride + x * image.Format.BytesPerPixel();
        return image.Format switch
        {
            AlgorithmImageFormat.Gray8 => image.Data.Span[offset] / 255d,
            AlgorithmImageFormat.Gray32Float => BinaryPrimitives.ReadSingleLittleEndian(image.Data.Span.Slice(offset, 4)),
            _ => throw new NotSupportedException(image.Format.ToString()),
        };
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
