using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Buffers.Binary;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class RingGhostMeasurementTests
{
    // Protect weak-signal photometry and the distinction between geometric candidates and physical orders.
    [Theory]
    [InlineData(100, 1, 1, 0)]
    [InlineData(180, .25, .8, .5)]
    [InlineData(180, 1, .8, .5)]
    public async Task AutomaticMeasurementHandlesScaleExposureAndEllipticity(int radius, double exposure, double aspect, double rotation)
    {
        using var image = Pattern(radius, exposure, aspect, rotation);
        using var result = await Run(image);
        Assert.True(result.Status == AlgorithmResultStatus.Succeeded, string.Join(";", result.Failures.Select(f => f.Message)));
        var data = result.GetArtifact<AlgorithmStructuredDataArtifact>("ring-ghost-measurement")!.Data;
        Assert.Equal("two_candidates_resolved", data.GetProperty("status").GetString());
        Assert.False(data.GetProperty("physicalOrderConfirmed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("gmr1Percent").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("gmr2Percent").ValueKind);
        Assert.Equal(JsonValueKind.Null, data.GetProperty("productPass").ValueKind);
        var rows = result.GetArtifact<AlgorithmTableArtifact>("ring-ghost-candidates")!.Rows;
        Assert.Equal(2, rows.Count);
        // Gaussian rings share their shape; half-height annulus means should preserve their known amplitude ratios.
        Assert.InRange(rows[0]["平均信号比_%"].GetDouble(), .70, .92);
        Assert.InRange(rows[1]["平均信号比_%"].GetDouble(), .24, .39);
        Assert.All(rows, row => Assert.Equal(JsonValueKind.Null, row["阶次"].ValueKind));
        Assert.InRange(data.GetProperty("centerX").GetDouble(), image.Width * .53 - 2, image.Width * .53 + 2);
        Assert.InRange(data.GetProperty("centerY").GetDouble(), image.Height * .47 - 2, image.Height * .47 + 2);
    }

    [Fact]
    public async Task ExplicitMappingChangesOnlyOrderAndNeverManufacturesAPass()
    {
        using var image = Pattern(160);
        using var first = await Run(image, RingGhostOrderMapping.OuterIsFirst);
        using var second = await Run(image, RingGhostOrderMapping.InnerIsFirst);
        var a = first.GetArtifact<AlgorithmStructuredDataArtifact>("ring-ghost-measurement")!.Data;
        var b = second.GetArtifact<AlgorithmStructuredDataArtifact>("ring-ghost-measurement")!.Data;
        Assert.True(a.GetProperty("physicalOrderConfirmed").GetBoolean());
        Assert.Equal(a.GetProperty("gmr1Percent").GetDouble(), b.GetProperty("gmr2Percent").GetDouble());
        Assert.Equal(a.GetProperty("gmr2Percent").GetDouble(), b.GetProperty("gmr1Percent").GetDouble());
        Assert.Equal(JsonValueKind.Null, b.GetProperty("productPass").ValueKind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrPartialWeakRingRemainsUnresolvedEvenWithMapping(bool partial)
    {
        using var image = Pattern(160, second: partial, partial: partial);
        using var result = await Run(image, RingGhostOrderMapping.OuterIsFirst);
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Single(result.GetArtifact<AlgorithmTableArtifact>("ring-ghost-candidates")!.Rows);
        var data = result.GetArtifact<AlgorithmStructuredDataArtifact>("ring-ghost-measurement")!.Data;
        Assert.Equal("unresolved_ring_count", data.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, data.GetProperty("gmr2Percent").ValueKind);
    }

    [Theory]
    [InlineData("blank", "primary_ring_missing")]
    [InlineData("disk", "primary_ring_missing")]
    [InlineData("clipped", "primary_ring_missing")]
    [InlineData("saturated", "saturated_primary_ring")]
    public async Task InvalidReferenceCannotProduceARatio(string mode, string reason)
    {
        using var image = Pattern(160, mode: mode);
        using var result = await Run(image);
        Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
        Assert.Contains(result.Failures, f => f.Code == reason);
        Assert.Empty(result.Artifacts);
    }

    // Separate radial peaks must survive candidate de-duplication even near the former 6% radius cutoff.
    [Theory]
    [InlineData(17)]
    [InlineData(18)]
    public async Task IndependentNearbyGhostRingsRemainTwoCandidates(int separation)
    {
        using var image = RadialPattern(300, r => 65535 * (.6 * Gaussian(r - 300, 7.5)
            + .0048 * Gaussian(r - 195, 3) + .0036 * Gaussian(r - (195 - separation), 3)));
        using var result = await Run(image, RingGhostOrderMapping.OuterIsFirst);
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(2, result.GetArtifact<AlgorithmTableArtifact>("ring-ghost-candidates")!.Rows.Count);
        var data = result.GetArtifact<AlgorithmStructuredDataArtifact>("ring-ghost-measurement")!.Data;
        Assert.True(data.GetProperty("gmr2Percent").GetDouble() > .5);
    }

    // A broad, complete annulus has a plateau rather than a single sharp maximum.
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task BroadPlateauGhostRingsAreNotDiscardedAsZeroProminence(double noiseAmplitudeDn)
    {
        using var image = RadialPattern(280, r => Math.Abs(r - 280) <= 6 ? 40000
            : Math.Abs(r - 196) <= 20 ? 200 : Math.Abs(r - 126) <= 20 ? 100 : 0,
            noiseAmplitudeDn: noiseAmplitudeDn);
        using var result = await Run(image, RingGhostOrderMapping.OuterIsFirst);
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Equal(2, result.GetArtifact<AlgorithmTableArtifact>("ring-ghost-candidates")!.Rows.Count);
        var data = result.GetArtifact<AlgorithmStructuredDataArtifact>("ring-ghost-measurement")!.Data;
        Assert.True(data.GetProperty("gmr1Percent").GetDouble() > 0);
        Assert.True(data.GetProperty("gmr2Percent").GetDouble() > 0);
    }

    [Theory]
    [InlineData(65535, 1, 1, "saturated_primary_ring")]
    [InlineData(65280, 256, 1, "suspected_saturated_primary_ring")]
    [InlineData(65472, 64, 1, "suspected_saturated_primary_ring")]
    [InlineData(65520, 1, 1, "suspected_saturated_primary_ring")]
    [InlineData(65520, 16, 5, "suspected_saturated_primary_ring")]
    [InlineData(65532, 4, 1, "suspected_saturated_primary_ring")]
    [InlineData(65279, 1, 1, null)]
    [InlineData(60000, 1, 1, null)]
    [InlineData(16000, 16, 1, null)]
    [InlineData(4095, 1, 1, null)]
    public async Task RawPrimaryHeadroomIsCheckedBeforeSignalDecoding(int primaryDn, int quantizationStep,
        double decodeExponent, string? expectedFailure)
    {
        // The power-of-two steps represent lower-bit-depth images stored left-aligned in Gray16.
        using var image = RadialPattern(280, r => Math.Abs(r - 280) <= 6 ? primaryDn
            : Math.Abs(r - 196) <= 6 ? 200 : Math.Abs(r - 126) <= 6 ? 100 : 0,
            quantizationStep: quantizationStep);
        using var result = await Run(image, decodeExponent: decodeExponent);
        if (expectedFailure is null)
        {
            Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
            Assert.Equal(2, result.GetArtifact<AlgorithmTableArtifact>("ring-ghost-candidates")!.Rows.Count);
        }
        else
        {
            Assert.Equal(AlgorithmResultStatus.Failed, result.Status);
            Assert.Contains(result.Failures, failure => failure.Code == expectedFailure);
            Assert.Empty(result.Artifacts);
        }
    }

    private static ValueTask<AlgorithmResult> Run(AlgorithmImageBuffer image,
        RingGhostOrderMapping mapping = RingGhostOrderMapping.Unconfirmed, double decodeExponent = 1)
        => ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.RingGhost, new RingGhostMeasurementParameters { OrderMapping = mapping, DecodeExponent = decodeExponent }),
            Inputs = [new AlgorithmInput { Name = "source", Image = image, Ownership = AlgorithmInputOwnership.Borrowed, ColorSpace = "linear-device-values" }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

    private static double Gaussian(double distance, double sigma) => Math.Exp(-distance * distance / (2 * sigma * sigma));

    private static AlgorithmImageBuffer RadialPattern(int radius, Func<double, double> signal,
        int quantizationStep = 1, double noiseAmplitudeDn = 0)
    {
        int width = radius * 4, height = radius * 4;
        var data = new byte[width * height * 2];
        var random = new Random(731);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                double r = Math.Sqrt(Math.Pow(x - width / 2.0, 2) + Math.Pow(y - height / 2.0, 2));
                double value = signal(r) + (random.NextDouble() * 2 - 1) * noiseAmplitudeDn;
                ushort sample = (ushort)Math.Clamp(Math.Round(value / quantizationStep) * quantizationStep, 0, 65535);
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan((y * width + x) * 2, 2), sample);
            }
        return new AlgorithmImageBuffer(width, height, width * 2, AlgorithmImageFormat.Gray16, data);
    }

    private static AlgorithmImageBuffer Pattern(int radius, double exposure = 1, double aspect = 1, double rotation = 0,
        bool second = true, bool partial = false, string mode = "rings")
    {
        int width = radius * 4, height = radius * 4;
        var data = new byte[width * height * 2];
        var random = new Random(731);
        double cx = mode == "clipped" ? radius * .5 : width * .53, cy = height * .47;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                double dx = x - cx, dy = y - cy;
                double u = dx * Math.Cos(rotation) + dy * Math.Sin(rotation), v = (-dx * Math.Sin(rotation) + dy * Math.Cos(rotation)) / aspect;
                double r = Math.Sqrt(u * u + v * v), sigma = radius * .025;
                double Gaussian(double target) => Math.Exp(-Math.Pow((r - target) / sigma, 2) / 2);
                double signal = .6 * Gaussian(radius) + .0048 * Gaussian(radius * .65)
                    + (second && (!partial || Math.Abs(Math.Atan2(v, u)) < Math.PI / 4) ? .0018 * Gaussian(radius * .38) : 0);
                if (mode == "blank") signal = 0;
                if (mode == "disk") signal = r < radius ? .6 : 0;
                signal = Math.Max(0, signal + .00005 + (random.NextDouble() - .5) * .0001) * exposure;
                if (mode == "saturated" && Math.Abs(r - radius) < sigma / 2) signal = 1;
                BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan((y * width + x) * 2, 2), (ushort)Math.Clamp(Math.Round(signal * 65535), 0, 65535));
            }
        return new AlgorithmImageBuffer(width, height, width * 2, AlgorithmImageFormat.Gray16, data);
    }
}
