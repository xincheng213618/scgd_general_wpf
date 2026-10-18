using cvColorVision;
using System.Text.Json;

namespace Spectrum.Tests;

// Independent Colour plotting fixtures protect a consistent wavelength/colour scale across all three views.
public sealed class SpectrumDisplayColorTests
{
    [Fact]
    public void EveryVisibleNanometreMatchesColourPlottingReference()
    {
        using var stream = typeof(SpectrumDisplayColorTests).Assembly.GetManifestResourceStream("Spectrum.Tests.spectrum-display-rgb.json")!;
        int[][] samples = JsonSerializer.Deserialize<int[][]>(stream)!;
        Assert.Equal(401, samples.Length);
        foreach (int[] sample in samples)
        {
            var actual = SpectrumChromaticity.DisplayRgb(sample[0]);
            Assert.Equal((sample[1], sample[2], sample[3]), ((int)actual.red, (int)actual.green, (int)actual.blue));
        }
    }

    [Theory]
    [InlineData(379.9)]
    [InlineData(780.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void NoVisibleColourIsInventedOutsideTheDisplayDomain(double wavelength)
        => Assert.Equal(((byte)0, (byte)0, (byte)0), SpectrumChromaticity.DisplayRgb(wavelength));
}
