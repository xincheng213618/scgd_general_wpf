using cvColorVision;
using System.Text.Json;

namespace Spectrum.Tests;

public sealed class SpectrumColorRenderingTests
{
    public static IEnumerable<object[]> ReferenceSpectra()
    {
        using var stream = typeof(SpectrumColorRenderingTests).Assembly.GetManifestResourceStream("Spectrum.Tests.color-rendering.json")!;
        foreach (var fixture in JsonSerializer.Deserialize<Fixture[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!)
            yield return [fixture];
    }

    // Independent Colour 0.4.7 fixtures protect the standard equations, reference
    // selection and table data; these are not frozen outputs of our implementation.
    [Theory]
    [MemberData(nameof(ReferenceSpectra))]
    public void StandardColorRenderingMatchesIndependentReference(Fixture fixture)
    {
        var result = SpectrumColorRendering.Calculate(fixture.Spectrum, 380, 1);
        Assert.Equal(ColorRenderingStatus.Valid, result.Status);
        Assert.InRange(Math.Abs(result.ColorTemperature!.Value - fixture.Cct), 0, 0.1);
        Assert.InRange(Math.Abs(result.Duv!.Value - fixture.Duv), 0, 1e-7);
        Assert.InRange(Math.Abs(result.Ra!.Value - fixture.Ra), 0, 0.05);
        Assert.Equal(15, result.Ri.Length);
        for (int i = 0; i < 15; i++) Assert.InRange(Math.Abs(result.Ri[i] - fixture.Ri[i]), 0, 0.05);
        Assert.Equal(result.Ri.Take(8).Average(), result.Ra.Value, 10);
        Assert.InRange(Math.Abs(result.MeanWavelength!.Value - fixture.Mean), 0, 0.0001);
        if (fixture.Name == "FL4") Assert.True(result.Ri[8] < 0);
    }

    [Fact]
    public void InapplicableOrCorruptSpectraHaveNoFabricatedCri()
    {
        float[] narrow = Enumerable.Range(380, 401).Select(w => (float)Math.Exp(-Math.Pow((w - 555) / 5d, 2))).ToArray();
        var monochromatic = SpectrumColorRendering.Calculate(narrow, 380, 1);
        Assert.NotEqual(ColorRenderingStatus.Valid, monochromatic.Status);
        Assert.Null(monochromatic.Ra);
        Assert.Empty(monochromatic.Ri);
        Assert.InRange(monochromatic.MeanWavelength!.Value, 554.99, 555.01);

        foreach (float bad in new[] { float.NaN, float.PositiveInfinity, -0.01f })
        {
            float[] spectrum = Enumerable.Repeat(1f, 401).ToArray();
            spectrum[100] = bad;
            var result = SpectrumColorRendering.Calculate(spectrum, 380, 1);
            Assert.Equal(ColorRenderingStatus.InvalidSpectrum, result.Status);
            Assert.Null(result.Ra);
            Assert.Null(result.MeanWavelength);
        }
        Assert.Null(SpectrumColorRendering.Calculate(new float[401], 380, 1).Ra);
        Assert.Equal(ColorRenderingStatus.InsufficientWavelengthRange, SpectrumColorRendering.Calculate(new[] { 1f, 1f }, 500, 1).Status);
        Assert.Equal(ColorRenderingStatus.SamplingTooCoarse, SpectrumColorRendering.Calculate(Enumerable.Repeat(1f, 41).ToArray(), 380, 10).Status);
    }

    [Fact]
    public void SpectralCentroidUsesMeasuredBandAndIncludesEndpoints()
    {
        var result = SpectrumColorRendering.Calculate(new[] { 1f, 2f, 3f }, 400, 10);
        Assert.Equal(412.5, result.MeanWavelength!.Value, 8);
        var scaled = SpectrumColorRendering.Calculate(new[] { 10f, 20f, 30f }, 400, 10);
        Assert.Equal(result.MeanWavelength, scaled.MeanWavelength);
    }

    [Fact]
    public void AbiPaddingIsExcludedAndHistoricalValuesAreRecomputed()
    {
        var fixture = (Fixture)ReferenceSpectra().First()[0];
        float[] values = new float[10000];
        for (int i = 0; i <= 4000; i++)
        {
            int left = i / 10;
            values[i] = fixture.Spectrum[left] + (fixture.Spectrum[Math.Min(left + 1, 400)] - fixture.Spectrum[left]) * (i % 10 / 10f);
        }
        var result = SpectrumColorRendering.Apply(new COLOR_PARA
        {
            fPL = values, fSpect1 = 380, fSpect2 = 780, fInterval = 0.1f,
            fLav = 0, fRa = 43, fRi = new float[15]
        });
        Assert.InRange(Math.Abs(result.fLav - fixture.Mean), 0, 0.01);
        Assert.Equal(15, result.fRi.Length);
        Assert.InRange(Math.Abs(result.fRa - fixture.Ra), 0, 0.05);
        for (int i = 0; i < 15; i++) Assert.InRange(Math.Abs(result.fRi[i] - fixture.Ri[i]), 0, 0.05);
        Assert.Equal(values, result.fPL);
    }

    public sealed class Fixture
    {
        public string Name { get; set; } = "";
        public float[] Spectrum { get; set; } = [];
        public double[] Xy { get; set; } = [];
        public double Duv { get; set; }
        public double Cct { get; set; }
        public double Ra { get; set; }
        public double[] Ri { get; set; } = [];
        public double Mean { get; set; }
    }
}
