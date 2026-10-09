using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.UI;
using System.Globalization;
using System.IO;

namespace Spectrum.Tests;

[Collection("LocalSpectrumDriver")]
public sealed class EngineSpectrumResultTests
{
    [Theory]
    [InlineData(1f)]
    [InlineData(10f)]
    public void FluxResultPreservesMeasuredValuesInDisplayAndExport(float aFactor)
    {
        var previousConfig = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
        string path = Path.Combine(Path.GetTempPath(), $"engine-spectrum-flux-{Guid.NewGuid():N}.csv");
        try
        {
            SpectumResultEntity source = CreateResult(35f * aFactor);
            source.AFactor = aFactor;
            source.RadiantFlux = 0.5d * aFactor;

            ViewResultSpectrum result = new(source);
            Assert.Equal(35f, float.Parse(result.Lv, CultureInfo.CurrentCulture));
            Assert.Equal(source.LuminousFlux, result.LuminousFlux);
            Assert.Equal(source.RadiantFlux, result.RadiantFlux);

            SpectrumCsvExportHelper.ExportLuminousFluxMode(path, [result]);
            string[] lines = File.ReadAllLines(path);
            string[] headers = lines[0].Split(',');
            string[] values = lines[1].Split(',');
            Assert.Equal(source.LuminousFlux?.ToString(), values[Array.IndexOf(headers, "LuminousFlux(lm)")]);
            Assert.Equal(source.RadiantFlux?.ToString(), values[Array.IndexOf(headers, "RadiantFlux(W)")]);
        }
        finally
        {
            ConfigService.SetInstance(previousConfig);
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0f)]
    public void MissingOrZeroFluxIsNotReplacedWithLuminance(float? flux)
    {
        var previousConfig = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
        try
        {
            ViewResultSpectrum result = new(CreateResult(flux));
            Assert.Equal(flux, result.LuminousFlux);
            Assert.Equal(35f, float.Parse(result.Lv, CultureInfo.CurrentCulture));
        }
        finally { ConfigService.SetInstance(previousConfig); }
    }

    [Fact]
    public void LuminanceGuardDoesNotClampMeasuredFlux()
    {
        var previousConfig = ConfigService.Instance;
        ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
        try
        {
            ViewSpectrumConfig.Instance.EnableNegativeLuminanceGuard = true;
            ViewSpectrumConfig.Instance.MinLuminanceValue = 0.01;
            SpectumResultEntity source = CreateResult(-0.5f);
            source.fPh = -1f;

            ViewResultSpectrum result = new(source);
            Assert.Equal(0.01f, float.Parse(result.Lv, CultureInfo.CurrentCulture));
            Assert.Equal(-0.5f, result.LuminousFlux);
            Assert.Equal(-1f, result.fPh);
        }
        finally { ConfigService.SetInstance(previousConfig); }
    }

    private static SpectumResultEntity CreateResult(float? flux) => new()
    {
        DataType = true,
        fPh = 35f,
        LuminousFlux = flux,
        fPL = "[1,1]",
        fSpect1 = 380f,
        fSpect2 = 381f,
        fInterval = 1f,
        fPlambda = 0.001f,
    };
}
