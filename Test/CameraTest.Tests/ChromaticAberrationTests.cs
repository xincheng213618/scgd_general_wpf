using CameraTest.Application;
using ColorVision.Core;

namespace CameraTest.Tests;

public sealed class ChromaticAberrationTests
{
    private static SfrChannelAnalysis Edge(string name, double intercept, bool rotated = false) => new()
    {
        Channel = name, Valid = true, FitAvailable = true, EdgeSlope = 0.1, EdgeIntercept = intercept, Rotated = rotated,
        EdgePositions = [-2, -1, 0, 1, 2], Esf = [0, 0.1, 0.5, 0.9, 1]
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IndependentlyCenteredEsfsRetainRealChannelOffsets(bool rotated)
    {
        var result = SfrChromaticAberration.Analyze(new() { Channels = [Edge("R", 41, rotated), Edge("G", 40, rotated), Edge("B", 39, rotated)] }, new(200, 300, 100, 80));
        Assert.Equal(rotated ? "Y" : "X", result.Axis);
        double cosine = 1 / Math.Sqrt(1.01);
        Assert.Equal(cosine, result.Pairs[0].NormalShiftPixels!.Value, 9);
        Assert.Equal(2 * cosine, result.Pairs[1].NormalShiftPixels!.Value, 9);
        Assert.Equal(cosine, result.Pairs[2].NormalShiftPixels!.Value, 9);
        Assert.Equal(cosine, result.Channels[0].CommonNormalPositions[2], 9);
        Assert.Equal(0, result.Channels[1].CommonNormalPositions[2], 9);
        Assert.Equal(-cosine, result.Channels[2].CommonNormalPositions[2], 9);
    }

    [Fact]
    public void InvalidColorDoesNotTurnIntoZeroAndOtherPairRemainsAvailable()
    {
        var result = SfrChromaticAberration.Analyze(new() { Channels = [Edge("R", 41) with { Valid = false, Reason = "low_contrast_or_no_edge" }, Edge("G", 40), Edge("B", 39)] }, new(0, 0, 100, 80));
        Assert.All(result.Pairs.Take(2), pair => { Assert.False(pair.Valid); Assert.Null(pair.NormalShiftPixels); });
        Assert.True(result.Pairs[2].Valid);
        var missingReason = SfrChromaticAberration.Analyze(new() { Channels = [Edge("R", 41) with { Valid = false, Reason = "" }, Edge("G", 40), Edge("B", 39)] }, new(0, 0, 100, 80));
        Assert.False(missingReason.Pairs[0].Valid);
        Assert.Null(missingReason.Pairs[0].NormalShiftPixels);
        var mono = SfrChromaticAberration.Analyze(new() { Channels = [Edge("L", 40)] }, new(0, 0, 100, 80));
        Assert.All(mono.Pairs, pair => { Assert.False(pair.Valid); Assert.Null(pair.NormalShiftPixels); });
    }

    [Fact]
    public void AmbiguousCrossingAndMismatchedOrientationAreRejected()
    {
        var result = SfrChromaticAberration.Analyze(new() { Channels = [Edge("R", 41) with { Esf = [0, 1, 0, 1, 1] }, Edge("G", 40), Edge("B", 39, true)] }, new(0, 0, 100, 80));
        Assert.Equal("ambiguous_esf_crossing", result.Channels[0].Reason);
        Assert.Equal("inconsistent_edge_orientation", result.Channels[2].Reason);
        Assert.All(result.Pairs, pair => Assert.Null(pair.NormalShiftPixels));
    }

    [Theory]
    [InlineData(8, false, false)]
    [InlineData(8, true, true)]
    [InlineData(16, false, true)]
    [InlineData(16, true, false)]
    public void NativeSfrRecoversKnownSubpixelShiftsForDepthOrientationAndPolarity(int depth, bool horizontal, bool reverse)
    {
        const int size = 128;
        int stride = size * 3 * (depth / 8);
        var pixels = new byte[stride * size];
        double slope = Math.Tan(5 * Math.PI / 180), cosine = 1 / Math.Sqrt(1 + slope * slope);
        double[] shifts = [-0.8, 0, 1.2]; // BGR, physical displacement along the edge normal.
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                for (int c = 0; c < 3; c++)
                {
                    double u = horizontal ? y : x, v = horizontal ? size - 1 - x : y;
                    double distance = (u - (size / 2.0 + slope * (v - (size - 1) / 2.0))) * cosine - shifts[c];
                    double signal = 0.1 + 0.8 * (0.5 + 0.5 * Math.Tanh(distance / 1.5));
                    if (reverse) signal = 1 - signal;
                    int value = (int)Math.Round(signal * (depth == 8 ? 255 : 65535));
                    int offset = y * stride + (x * 3 + c) * (depth / 8);
                    pixels[offset] = (byte)(value & 255);
                    if (depth == 16) pixels[offset + 1] = (byte)(value >> 8);
                }
        var frame = new TestFrame(new(pixels, size, size, depth, 3, stride, DateTimeOffset.Now), "synthetic-shift");
        var sfr = frame.Read(image => SfrAnalyzer.Analyze(image, new(0, 0, size, size), new() { InputEncoding = SfrInputEncoding.Linear }));
        Assert.All(sfr.Channels, channel => Assert.True(channel.Valid, $"{channel.Channel}: {channel.Reason}"));
        var result = SfrChromaticAberration.Analyze(sfr, new(0, 0, size, size));
        Assert.Equal(horizontal ? "Y" : "X", result.Axis);
        double[] expected = [1.2, 2.0, 0.8];
        for (int i = 0; i < 3; i++)
        {
            Assert.True(result.Pairs[i].Valid, result.Pairs[i].Reason);
            // 4x ESF sampling: half-bin plus 8-bit quantization budget, in input pixels.
            Assert.InRange(result.Pairs[i].NormalShiftPixels!.Value, expected[i] - 0.15, expected[i] + 0.15);
        }
    }
}
