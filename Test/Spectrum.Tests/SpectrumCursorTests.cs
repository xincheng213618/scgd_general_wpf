using ColorVision.ImageEditor.Draw;

namespace Spectrum.Tests;

public sealed class SpectrumCursorTests
{
    [Fact]
    public void CursorReadsSubNanometerOriginalSamplesWithoutClippingOrInterpolation()
    {
        float[] samples = new float[4001];
        samples[577] = -0.375f;
        Assert.True(SpectrumCursorSamples.TryRead(samples, 380, 0.1f, 10, 437.71, out var reading));
        Assert.Equal(577, reading.SampleIndex);
        Assert.Equal(437.7, reading.Wavelength, 8);
        Assert.Equal(-0.375f, reading.Relative);
        Assert.Equal(-3.75, reading.Absolute);
        Assert.True(SpectrumCursorSamples.TryRead(samples, 380, 0.1f, 10, 780, out reading));
        Assert.Equal(4000, reading.SampleIndex);
        Assert.False(SpectrumCursorSamples.TryRead(samples, 380, 0.1f, 10, 780.01, out _));
    }

    [Theory]
    [InlineData(379)]
    [InlineData(781)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void CursorDoesNotExtrapolate(double wavelength)
        => Assert.False(SpectrumCursorSamples.TryRead([1, 2, 3], 400, 2.5f, 1, wavelength, out _));

    [Fact]
    public void EmptyInvalidGridsAndNonFiniteSamplesHaveExplicitBehaviour()
    {
        Assert.False(SpectrumCursorSamples.TryRead([], 380, 1, 1, 400, out _));
        Assert.False(SpectrumCursorSamples.TryRead([1], 0, 0, 1, 380, out _));
        Assert.True(SpectrumCursorSamples.TryRead([1, 2, float.NaN], 400, 2.5f, 3, 405, out var reading));
        Assert.True(float.IsNaN(reading.Relative));
        Assert.Contains("—", reading.Format("W/nm"));
        Assert.True(SpectrumCursorSamples.TryRead([1, 2], 400, 2.5f, 3, 401.25, out reading));
        Assert.Equal(1, reading.SampleIndex);
        Assert.Equal(402.5, reading.Wavelength);
    }
}
