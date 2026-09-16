#pragma warning disable CA1707
using Spectrum.Models;

namespace Spectrum.Tests;

public class SpectrumPlotFillTests
{
    [Fact]
    public void CreateSegments_UsesSpectrumValuesAndVisibleWavelengthColors()
    {
        SpectralData[] samples =
        [
            new() { Wavelength = 380, RelativeSpectrum = 0.25, AbsoluteSpectrum = 2 },
            new() { Wavelength = 381, RelativeSpectrum = 0.5, AbsoluteSpectrum = 4 },
            new() { Wavelength = 382, RelativeSpectrum = -0.1, AbsoluteSpectrum = -2 },
        ];

        IReadOnlyList<SpectrumFillSegment> relative = SpectrumPlotFill.CreateSegments(samples, useAbsoluteSpectrum: false);
        IReadOnlyList<SpectrumFillSegment> absolute = SpectrumPlotFill.CreateSegments(samples, useAbsoluteSpectrum: true);

        Assert.Equal(2, relative.Count);
        Assert.Equal(380, relative[0].StartWavelength);
        Assert.Equal(381, relative[0].EndWavelength);
        Assert.Equal(0.25, relative[0].StartValue);
        Assert.Equal(0.5, relative[0].EndValue);
        Assert.Equal(0, relative[1].EndValue);
        Assert.NotEqual(0, relative[0].Color.A);
        Assert.Equal(2, absolute[0].StartValue);
        Assert.Equal(4, absolute[0].EndValue);
        Assert.Equal(0, absolute[1].EndValue);
    }

    [Fact]
    public void CreateSegments_ClipsToVisibleSpectrumAndInterpolatesEdges()
    {
        SpectralData[] samples =
        [
            new() { Wavelength = 379, RelativeSpectrum = 0.2 },
            new() { Wavelength = 381, RelativeSpectrum = 0.6 },
            new() { Wavelength = 779, RelativeSpectrum = 0.6 },
            new() { Wavelength = 781, RelativeSpectrum = 0.2 },
        ];

        IReadOnlyList<SpectrumFillSegment> segments = SpectrumPlotFill.CreateSegments(samples, useAbsoluteSpectrum: false);

        Assert.Equal(3, segments.Count);
        Assert.Equal(380, segments[0].StartWavelength);
        Assert.Equal(0.4, segments[0].StartValue, 12);
        Assert.Equal(780, segments[^1].EndWavelength);
        Assert.Equal(0.4, segments[^1].EndValue, 12);
    }

    [Fact]
    public void CreateSegments_SkipsInvalidAndReversedSamples()
    {
        SpectralData[] samples =
        [
            new() { Wavelength = 400, RelativeSpectrum = double.NaN },
            new() { Wavelength = 401, RelativeSpectrum = 0.5 },
            new() { Wavelength = 400, RelativeSpectrum = 0.5 },
        ];

        Assert.Empty(SpectrumPlotFill.CreateSegments(samples, useAbsoluteSpectrum: false));
        Assert.Empty(SpectrumPlotFill.CreateSegments([], useAbsoluteSpectrum: false));
    }
}
