using cvColorVision;
using System.Globalization;
using System.IO;
using System.Text.Json;
using CsvHelper;
using EngineResult = ColorVision.Engine.Services.Devices.Spectrum.Views.ViewResultSpectrum;
using PluginResult = Spectrum.Models.ViewResultSpectrum;

namespace Spectrum.Tests;

// Independent fixtures and geometric boundaries protect the shared scientific contract.
[Collection("LocalSpectrumDriver")]
public sealed class SpectrumChromaticityTests
{
    [Theory]
    [MemberData(nameof(SpectrumColorRenderingTests.ReferenceSpectra), MemberType = typeof(SpectrumColorRenderingTests))]
    public void CctAndSignedDuvMatchRobertsonReference(SpectrumColorRenderingTests.Fixture fixture)
    {
        var result = SpectrumChromaticity.Calculate(fixture.Xy[0], fixture.Xy[1]);
        Assert.Equal(SpectrumTemperatureStatus.Valid, result.TemperatureStatus);
        Assert.InRange(Math.Abs(result.ColorTemperature!.Value - fixture.Cct), 0, 1e-7);
        Assert.InRange(Math.Abs(result.Duv!.Value - fixture.Duv), 0, 1e-12);
    }

    [Fact]
    public void DominantComplementaryWavelengthAndPurityMatchIndependentReference()
    {
        using var stream = typeof(SpectrumChromaticityTests).Assembly.GetManifestResourceStream("Spectrum.Tests.chromaticity.json")!;
        var samples = JsonSerializer.Deserialize<Reference[]>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        foreach (var sample in samples)
        {
            var result = SpectrumChromaticity.Calculate(sample.X, sample.Y);
            bool complementary = sample.Wavelength < 0;
            Assert.Equal(complementary ? SpectrumWavelengthStatus.Complementary : SpectrumWavelengthStatus.Valid, result.WavelengthStatus);
            double actual = (complementary ? -result.ComplementaryWavelength : result.DominantWavelength)!.Value;
            // Colour rounds to its 1nm locus index; production interpolates the intersected segment.
            Assert.InRange(Math.Abs(actual - sample.Wavelength), 0, 0.51);
            Assert.InRange(Math.Abs(result.ExcitationPurity!.Value - sample.Purity), 0, 1e-8);
        }
    }

    [Fact]
    public void WhitePurpleAndInvalidColoursHaveDistinctSemantics()
    {
        var white = SpectrumChromaticity.Calculate(0.3127f, 0.3290f);
        Assert.Equal(SpectrumWavelengthStatus.Achromatic, white.WavelengthStatus);
        Assert.Equal(0, white.ExcitationPurity);
        Assert.Null(white.DominantWavelength);
        Assert.Null(white.ComplementaryWavelength);
        var purple = SpectrumChromaticity.Calculate(0.37605506, 0.24452225);
        Assert.Equal(SpectrumWavelengthStatus.Complementary, purple.WavelengthStatus);
        Assert.Null(purple.DominantWavelength);
        Assert.InRange(purple.ComplementaryWavelength!.Value, 508.5, 509.5);
        Assert.InRange(purple.ExcitationPurity!.Value, 0, 1);
        foreach (var xy in new[] { (double.NaN, 0.3), (0.3, double.PositiveInfinity), (-0.1, 0.3), (0.8, 0.4), (0.8, 0.2), (0d, 0d) })
        {
            var result = SpectrumChromaticity.Calculate(xy.Item1, xy.Item2);
            Assert.Null(result.DominantWavelength);
            Assert.Null(result.ComplementaryWavelength);
            Assert.Null(result.ExcitationPurity);
        }
        var green = SpectrumChromaticity.Calculate(0.3, 0.6);
        Assert.Equal(SpectrumTemperatureStatus.FarFromPlanckianLocus, green.TemperatureStatus);
        Assert.Null(green.ColorTemperature);
        Assert.True(green.Duv > 0.05);
        Assert.Equal(SpectrumWavelengthStatus.InvalidReferenceWhite, SpectrumChromaticity.Calculate(0.3, 0.4, 0.8, 0.2).WavelengthStatus);
    }

    [Theory]
    [InlineData(0.37605506, 0.24452225)]
    [InlineData(0.3127, 0.3290)]
    [InlineData(0.54369557, 0.32107944)]
    [InlineData(0.8, 0.2)]
    public void BothClientsAbiAndBothCsvModesAgree(double x, double y)
    {
        COLOR_PARA data = new() { fx = (float)x, fy = (float)y, fPL = [1, 1], fSpect1 = 500, fSpect2 = 501, fInterval = 1 };
        var plugin = new PluginResult(data);
        var previousConfig = ColorVision.UI.ConfigService.Instance;
        EngineResult engine;
        ColorVision.UI.ConfigService.SetInstance(new ColorVision.UI.ConfigHandler { IsAutoSave = false });
        try { engine = new EngineResult(data); }
        finally { ColorVision.UI.ConfigService.SetInstance(previousConfig); }
        var applied = SpectrumColorRendering.Apply(data);
        Assert.Equal(plugin.fCCT, engine.fCCT);
        Assert.Equal(plugin.dC, engine.dC);
        Assert.Equal(plugin.fLd, engine.fLd);
        Assert.Equal(plugin.fPur, engine.fPur);
        Assert.Equal(applied.fCCT, plugin.fCCT);
        Assert.Equal(applied.dC, plugin.dC);
        Assert.Equal(applied.fLd, plugin.fLd);
        Assert.Equal(applied.fPur, plugin.fPur);
        string path = Path.Combine(Path.GetTempPath(), $"spectrum-chromaticity-{Guid.NewGuid():N}.csv");
        try
        {
            foreach (bool eqe in new[] { false, true })
            {
                if (eqe) ColorVision.Engine.Services.Devices.Spectrum.Views.SpectrumCsvExportHelper.ExportLuminousFluxMode(path, [engine]);
                else ColorVision.Engine.Services.Devices.Spectrum.Views.SpectrumCsvExportHelper.ExportLuminanceMode(path, [engine]);
                foreach (string csv in new[] { Spectrum.SpectrumCsvExporter.CreateCsv([plugin], eqe), File.ReadAllText(path) })
                {
                    using var reader = new CsvReader(new StringReader(csv), CultureInfo.InvariantCulture);
                    reader.Read(); reader.ReadHeader(); reader.Read();
                    Assert.Equal(reader.HeaderRecord!.Length, reader.Parser.Record!.Length);
                    Assert.Equal(plugin.Chromaticity.WavelengthStatus.ToString(), reader.GetField("Wavelength Status"));
                    Assert.Equal(plugin.Chromaticity.TemperatureStatus.ToString(), reader.GetField("Temperature Status"));
                    Assert.Equal(plugin.Chromaticity.Duv?.ToString(CultureInfo.InvariantCulture) ?? "", reader.GetField("Duv"));
                    Assert.Equal(plugin.Chromaticity.ComplementaryWavelength?.ToString(CultureInfo.InvariantCulture) ?? "", reader.GetField("Complementary Wavelength(nm)"));
                    Assert.Equal(plugin.PurityPercent?.ToString(CultureInfo.InvariantCulture) ?? "", reader.GetField("Color Purity(%)"));
                    Assert.DoesNotContain("NaN", csv);
                    Assert.DoesNotContain("Infinity", csv);
                }
            }
        }
        finally { File.Delete(path); }
    }

    private sealed record Reference(double X, double Y, double Wavelength, double Purity);
}
