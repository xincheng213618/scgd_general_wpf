#pragma warning disable CA1707
using ColorVision.UI;
using cvColorVision;
using Spectrum.Data;
using Spectrum.Models;
using System.Globalization;

namespace Spectrum.Tests;

[Collection("LocalSpectrumDriver")]
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

    [Theory]
    [InlineData(350f)]
    [InlineData(0f)]
    public void EqeRecalculation_PreservesPersistedFluxInResultAndExport(float flux)
    {
        COLOR_PARA colorParam = CreateColorParam(1f);
        colorParam.fPh = 35f;
        ViewResultSpectrum result = new(new SprectrumModel
        {
            ColorParam = colorParam,
            LuminousFlux = flux
        });

        result.CalculateEqeParams(5f, 10f);

        Assert.Equal(flux, result.LuminousFlux);
        Assert.Equal(flux / 0.05d, result.LuminousEfficacy!.Value, 8);

        string[] lines = SpectrumCsvExporter.CreateCsv([result], isEqeMode: true)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        string[] headers = lines[0].Split(',');
        string[] values = lines[1].Split(',');
        Assert.Equal(flux, float.Parse(values[Array.IndexOf(headers, "LuminousFlux(lm)")], CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(-1f)]
    [InlineData(0f)]
    [InlineData(35f)]
    public void EqeCalculation_UsesRawFluxWithoutLuminanceGuard(float flux)
    {
        var previousConfig = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
        try
        {
            ViewResultManagerConfig.Instance.EnableNegativeLuminanceGuard = true;
            ViewResultManagerConfig.Instance.MinLuminanceValue = 0.01;
            COLOR_PARA colorParam = CreateColorParam(1f);
            colorParam.fPh = flux;
            ViewResultSpectrum result = new(colorParam);

            result.CalculateEqeParams(5f, 10f);

            Assert.Equal(Math.Max(0.01f, flux), float.Parse(result.Lv, CultureInfo.CurrentCulture));
            Assert.Equal(flux, result.LuminousFlux);
            Assert.Equal(flux / 0.05d, result.LuminousEfficacy!.Value, 8);
        }
        finally { ConfigService.SetInstance(previousConfig); }
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
