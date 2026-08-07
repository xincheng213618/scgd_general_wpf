#pragma warning disable CA1707
using cvColorVision;
using Spectrum.Models;

namespace Spectrum.Tests;

public class ViewResultSpectrumTests
{
    [Theory]
    [InlineData(0.1f, 4001)]
    [InlineData(1f, 401)]
    public void Constructor_KeepsOnlyValidSpectrumRange(float interval, int expectedPointCount)
    {
        ViewResultSpectrum result = new(CreateColorParam(interval));

        Assert.Equal(expectedPointCount, result.SpectrumPointCount);
        Assert.Equal(380f, result.fSpect1);
        Assert.Equal(780f, result.fSpect2, 3);
    }

    [Fact]
    public void SpectralDatas_AreSampledAtOneNanometerWithRealEndpoints()
    {
        ViewResultSpectrum result = new(CreateColorParam(0.1f));

        Assert.Equal(401, result.SpectralDatas.Count);
        Assert.Equal(380f, result.SpectralDatas[0].Wavelength, 3);
        Assert.Equal(780f, result.SpectralDatas[^1].Wavelength, 3);
        Assert.Equal(1f, result.SpectralDatas[^1].RelativeSpectrum);
    }

    [Fact]
    public void Constructor_UsesSafeFallbackForLegacyMetadata()
    {
        COLOR_PARA colorParam = CreateColorParam(0.1f);
        colorParam.fSpect1 = 0;
        colorParam.fSpect2 = 0;
        colorParam.fInterval = 0;

        ViewResultSpectrum result = new(colorParam);

        Assert.Equal(4001, result.SpectrumPointCount);
        Assert.Equal(380f, result.fSpect1);
        Assert.Equal(780f, result.fSpect2, 3);
    }

    private static COLOR_PARA CreateColorParam(float interval)
    {
        float[] spectrum = Enumerable.Repeat(1f, 10000).ToArray();
        return new COLOR_PARA
        {
            fSpect1 = 380,
            fSpect2 = 780,
            fInterval = interval,
            fPL = spectrum,
            fRi = Array.Empty<float>()
        };
    }
}
