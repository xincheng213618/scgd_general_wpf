using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.BatchProcessing;
using ColorVision.ImageEditor.EditorTools.Algorithms;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class FormatDependentParameterContractTests
{
    [Fact]
    public void CatalogDeclaresCurrentNominalContractsAndDefaults()
    {
        AlgorithmCatalog catalog = StandardAlgorithmCatalog.Create();
        AlgorithmDescriptor threshold = catalog.Descriptors.Single(item => item.Id == StandardAlgorithmIds.Threshold);
        Assert.Equal(new AlgorithmVersion(1, 1, 0), threshold.Version);
        Assert.Equal(2, threshold.ParameterSchema.Version);
        ThresholdParameters thresholdDefaults = Assert.IsType<ThresholdParameters>(
            threshold.ParameterSchema.Defaults.Deserialize(threshold.ParameterType, AlgorithmJson.Options));
        Assert.True(thresholdDefaults.UseNominalRange);
        Assert.Equal(128, thresholdDefaults.Threshold);
        AlgorithmParameterField thresholdField = threshold.ParameterSchema.Fields.Single(field => field.Name == nameof(ThresholdParameters.Threshold));
        Assert.Equal(0, thresholdField.Minimum);
        Assert.Equal(ushort.MaxValue, thresholdField.Maximum);
        Assert.Equal("conditional-DN", thresholdField.Unit);

        AlgorithmDescriptor denoise = catalog.Descriptors.Single(item => item.Id == StandardAlgorithmIds.Denoise);
        Assert.Equal(new AlgorithmVersion(1, 1, 0), denoise.Version);
        Assert.Equal(2, denoise.ParameterSchema.Version);
        DenoiseParameters denoiseDefaults = Assert.IsType<DenoiseParameters>(
            denoise.ParameterSchema.Defaults.Deserialize(denoise.ParameterType, AlgorithmJson.Options));
        Assert.True(denoiseDefaults.UseNominalColorSigma);
        Assert.Equal(75, denoiseDefaults.SigmaColor);
        AlgorithmParameterField sigmaField = denoise.ParameterSchema.Fields.Single(field => field.Name == nameof(DenoiseParameters.SigmaColor));
        Assert.Equal(byte.MaxValue, sigmaField.Maximum);
        Assert.Equal("nominal-8bit-DN", sigmaField.Unit);
    }

    [Fact]
    public void ConditionalValidationSeparatesCurrentNominalAndLegacyAbsoluteRanges()
    {
        ThresholdParameters threshold = new() { Threshold = 256 };
        Assert.Contains(threshold.Validate().Issues, issue => issue.Path == nameof(ThresholdParameters.Threshold));
        threshold.UseNominalRange = false;
        Assert.True(threshold.Validate().IsValid);

        DenoiseParameters denoise = new() { SigmaColor = 256 };
        Assert.Contains(denoise.Validate().Issues, issue => issue.Path == nameof(DenoiseParameters.SigmaColor));
        denoise.UseNominalColorSigma = false;
        Assert.True(denoise.Validate().IsValid);
    }

    [Fact]
    public void ImageViewAndBatchProjectTheSameCurrentThresholdDefaults()
    {
        ThresholdParameters catalogDefaults = new();
        ThresholdParameters interactive = ThresholdWindow.CreateParameters(catalogDefaults.Threshold);
        ThresholdParameters batch = Assert.IsType<ThresholdParameters>(BatchImageAlgorithms.CreateAll()
            .Single(item => item.Descriptor?.Id == StandardAlgorithmIds.Threshold)
            .Options);

        Assert.Equal(AlgorithmJson.ToElement(catalogDefaults).ToString(), AlgorithmJson.ToElement(interactive).ToString());
        Assert.Equal(AlgorithmJson.ToElement(catalogDefaults).ToString(), AlgorithmJson.ToElement(batch).ToString());
    }

    [Fact]
    public void SchemaOneMigratorsRetainAbsoluteDnCompatibilityMode()
    {
        ThresholdParameters migratedThreshold = AlgorithmJson.Deserialize<ThresholdParameters>(
            new ThresholdParametersV1ToV2Migrator().Migrate(JsonSerializer.SerializeToElement(new { threshold = 4096 })));
        Assert.False(migratedThreshold.UseNominalRange);
        Assert.Equal(4096, migratedThreshold.Threshold);

        DenoiseParameters migratedDenoise = AlgorithmJson.Deserialize<DenoiseParameters>(
            new DenoiseParametersV1ToV2Migrator().Migrate(JsonSerializer.SerializeToElement(new
            {
                operation = StandardDenoiseOperation.Bilateral,
                kernelSize = 5,
                sigmaColor = 75,
                sigmaSpace = 3,
            })));
        Assert.False(migratedDenoise.UseNominalColorSigma);
        Assert.Equal(75, migratedDenoise.SigmaColor);
    }

    [Fact]
    public async Task DefaultThresholdHasTheSameNominalGoldenAcrossGray8Gray16AndGray32Float()
    {
        double[] normalized = [0, 127d / 255, 128d / 255, 129d / 255, 1];
        double[] expected = [0, 0, 0, 1, 1];
        foreach (AlgorithmImageFormat format in new[]
                 {
                     AlgorithmImageFormat.Gray8,
                     AlgorithmImageFormat.Gray16,
                     AlgorithmImageFormat.Gray32Float,
                 })
        {
            using AlgorithmImageBuffer input = Buffer(format, normalized);
            byte[] original = input.Data.ToArray();
            using AlgorithmResult result = await RunAsync(
                AlgorithmInvocation.Create(StandardAlgorithmIds.Threshold, new ThresholdParameters()),
                input);

            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
            AlgorithmImageBuffer output = result.GetArtifact<AlgorithmImageArtifact>()!.Image;
            Assert.Equal(format, output.Format);
            Assert.Equal(expected, NormalizedValues(output), new DoubleToleranceComparer(1e-6));
            Assert.Equal(original, input.Data.ToArray());
        }
    }

    [Fact]
    public async Task SchemaOneIntegerThresholdKeepsRawDnWhileFloatOverflowIsStructuredFailure()
    {
        AlgorithmInvocation legacy = new()
        {
            AlgorithmId = StandardAlgorithmIds.Threshold,
            AlgorithmVersion = new AlgorithmVersion(1, 0, 0),
            ParameterSchemaVersion = 1,
            Parameters = JsonSerializer.SerializeToElement(new { threshold = 83 }),
        };
        using AlgorithmImageBuffer gray16 = Buffer(AlgorithmImageFormat.Gray16, [82d / ushort.MaxValue, 83d / ushort.MaxValue, 84d / ushort.MaxValue]);
        using AlgorithmResult integerResult = await RunAsync(legacy, gray16);
        Assert.Equal(AlgorithmResultStatus.Succeeded, integerResult.Status);
        Assert.Equal(new[] { 0d, 0d, 1d }, NormalizedValues(integerResult.GetArtifact<AlgorithmImageArtifact>()!.Image), new DoubleToleranceComparer(1e-12));

        AlgorithmInvocation invalidFloatLegacy = new()
        {
            InvocationId = Guid.NewGuid(),
            AlgorithmId = legacy.AlgorithmId,
            AlgorithmVersion = legacy.AlgorithmVersion,
            ParameterSchemaVersion = legacy.ParameterSchemaVersion,
            Parameters = JsonSerializer.SerializeToElement(new { threshold = 128 }),
        };
        using AlgorithmImageBuffer gray32 = Buffer(AlgorithmImageFormat.Gray32Float, [0, 0.5, 1]);
        using AlgorithmResult floatResult = await RunAsync(invalidFloatLegacy, gray32);
        Assert.Equal(AlgorithmResultStatus.Failed, floatResult.Status);
        Assert.Contains(floatResult.Failures, failure => failure.Code == "parameter_format_unsupported"
            && failure.Path == nameof(ThresholdParameters.Threshold));
    }

    [Fact]
    public async Task BilateralNominalSigmaProducesCrossDepthNumericalAgreement()
    {
        double[] normalized = Enumerable.Range(0, 49)
            .Select(index => ((index * 37 + index / 7 * 19) % 256) / 255d)
            .ToArray();
        Dictionary<AlgorithmImageFormat, double[]> outputs = new();
        foreach (AlgorithmImageFormat format in new[]
                 {
                     AlgorithmImageFormat.Gray8,
                     AlgorithmImageFormat.Gray16,
                     AlgorithmImageFormat.Gray32Float,
                 })
        {
            using AlgorithmImageBuffer input = Buffer(format, normalized, width: 7);
            using AlgorithmResult result = await RunAsync(
                AlgorithmInvocation.Create(StandardAlgorithmIds.Denoise, new DenoiseParameters
                {
                    Operation = StandardDenoiseOperation.Bilateral,
                    KernelSize = 5,
                    SigmaColor = 75,
                    SigmaSpace = 3,
                }),
                input);
            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
            outputs.Add(format, NormalizedValues(result.GetArtifact<AlgorithmImageArtifact>()!.Image));
        }

        Assert.Equal(outputs[AlgorithmImageFormat.Gray32Float], outputs[AlgorithmImageFormat.Gray16], new DoubleToleranceComparer(2d / ushort.MaxValue));
        Assert.Equal(outputs[AlgorithmImageFormat.Gray32Float], outputs[AlgorithmImageFormat.Gray8], new DoubleToleranceComparer(3d / byte.MaxValue));
    }

    [Theory]
    [InlineData(255, 75)]
    [InlineData(65535, 19275)]
    [InlineData(1, 0.29411764705882354)]
    public void NominalEightBitMappingIsExplicit(double formatMaximum, double expected)
    {
        Assert.Equal(expected, OpenCvAlgorithmProvider.ResolveNominal8BitValue(75, true, formatMaximum), 12);
        Assert.Equal(75, OpenCvAlgorithmProvider.ResolveNominal8BitValue(75, false, formatMaximum));
    }

    private static ValueTask<AlgorithmResult> RunAsync(AlgorithmInvocation invocation, AlgorithmImageBuffer input)
        => ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = invocation,
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

    private static AlgorithmImageBuffer Buffer(AlgorithmImageFormat format, IReadOnlyList<double> normalized, int? width = null)
    {
        int actualWidth = width ?? normalized.Count;
        int height = normalized.Count / actualWidth;
        return format switch
        {
            AlgorithmImageFormat.Gray8 => new AlgorithmImageBuffer(
                actualWidth,
                height,
                actualWidth,
                format,
                normalized.Select(value => (byte)Math.Round(value * byte.MaxValue, MidpointRounding.AwayFromZero)).ToArray()),
            AlgorithmImageFormat.Gray16 => UInt16Buffer(actualWidth, height, normalized),
            AlgorithmImageFormat.Gray32Float => FloatBuffer(actualWidth, height, normalized),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };
    }

    private static AlgorithmImageBuffer UInt16Buffer(int width, int height, IReadOnlyList<double> normalized)
    {
        ushort[] values = normalized.Select(value => (ushort)Math.Round(value * ushort.MaxValue, MidpointRounding.AwayFromZero)).ToArray();
        return new AlgorithmImageBuffer(width, height, width * sizeof(ushort), AlgorithmImageFormat.Gray16, MemoryMarshal.AsBytes(values.AsSpan()).ToArray());
    }

    private static AlgorithmImageBuffer FloatBuffer(int width, int height, IReadOnlyList<double> normalized)
    {
        float[] values = normalized.Select(value => (float)value).ToArray();
        return new AlgorithmImageBuffer(width, height, width * sizeof(float), AlgorithmImageFormat.Gray32Float, MemoryMarshal.AsBytes(values.AsSpan()).ToArray());
    }

    private static double[] NormalizedValues(AlgorithmImageBuffer buffer)
    {
        return buffer.Format switch
        {
            AlgorithmImageFormat.Gray8 => buffer.Data.ToArray().Select(value => value / (double)byte.MaxValue).ToArray(),
            AlgorithmImageFormat.Gray16 => MemoryMarshal.Cast<byte, ushort>(buffer.Data.Span).ToArray().Select(value => value / (double)ushort.MaxValue).ToArray(),
            AlgorithmImageFormat.Gray32Float => MemoryMarshal.Cast<byte, float>(buffer.Data.Span).ToArray().Select(value => (double)value).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(buffer)),
        };
    }

    private sealed class DoubleToleranceComparer(double tolerance) : IEqualityComparer<double>
    {
        public bool Equals(double x, double y) => Math.Abs(x - y) <= tolerance;

        public int GetHashCode(double obj) => 0;
    }
}
