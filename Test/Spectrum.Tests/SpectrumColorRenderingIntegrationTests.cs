using cvColorVision;
using System.Globalization;
using System.IO;
using EngineResult = ColorVision.Engine.Services.Devices.Spectrum.Views.ViewResultSpectrum;
using PluginResult = Spectrum.Models.ViewResultSpectrum;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.Engine.Services.Devices.Spectrum.Local;
using CsvHelper;

namespace Spectrum.Tests;

[Collection("LocalSpectrumDriver")]
public sealed class SpectrumColorRenderingIntegrationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void StandardMetricsReachBothApplicationsAndBothCsvModes(bool eqe, bool includeNormal)
    {
        var previousConfig = ColorVision.UI.ConfigService.Instance;
        var previousCulture = CultureInfo.CurrentCulture;
        ColorVision.UI.ConfigService.SetInstance(new ColorVision.UI.ConfigHandler { IsAutoSave = false });
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var fixture = SpectrumColorRenderingTests.ReferenceSpectra().Select(row => (SpectrumColorRenderingTests.Fixture)row[0]).Single(item => item.Name == "FL4");
            COLOR_PARA data = new() { fPL = fixture.Spectrum, fSpect1 = 380, fSpect2 = 780, fInterval = 1, fRa = 43, fRi = new float[15] };
            PluginResult plugin = new(data);
            EngineResult engine = new(data);
            plugin.AcquisitionFluxMode = engine.AcquisitionFluxMode = eqe;
            plugin.V = 4.2f; plugin.I = 20f;
            engine.V = 4.2f; engine.I = 20f;
            var engineResults = includeNormal ? new[] { engine, new EngineResult(data) { V = 9, I = 99, AcquisitionFluxMode = false } } : new[] { engine };
            var pluginResults = includeNormal ? new[] { plugin, new PluginResult(data) { V = 9, I = 99, AcquisitionFluxMode = false } } : new[] { plugin };
            Assert.Equal(15, plugin.fRi.Length);
            Assert.Equal(plugin.fRi, engine.fRi);
            Assert.Equal(plugin.fRa, engine.fRa);
            Assert.Equal(plugin.fLav, engine.fLav);
            Assert.InRange(Math.Abs(plugin.fLav - fixture.Mean), 0, 0.0001);
            Assert.True(plugin.fRi[8] < 0);

            string path = Path.Combine(Path.GetTempPath(), $"cri-export-{Guid.NewGuid():N}.csv");
            try
            {
                if (eqe) SpectrumCsvExportHelper.ExportLuminousFluxMode(path, engineResults);
                else SpectrumCsvExportHelper.ExportLuminanceMode(path, engineResults);
                foreach (string csv in new[] { SpectrumCsvExporter.CreateCsv(pluginResults, eqe), File.ReadAllText(path) })
                {
                    using CsvReader reader = new(new StringReader(csv), CultureInfo.InvariantCulture);
                    reader.Read();
                    reader.ReadHeader();
                    string[] headers = reader.HeaderRecord!;
                    reader.Read();
                    string[] values = reader.Parser.Record!;
                    Assert.Equal(headers.Length, values.Length);
                    string? voltageHeader = headers.SingleOrDefault(header => header.StartsWith("Voltage", StringComparison.Ordinal) || header.StartsWith("Voltgage", StringComparison.Ordinal));
                    string? currentHeader = headers.SingleOrDefault(header => header.StartsWith("Current", StringComparison.Ordinal));
                    if (eqe)
                    {
                        Assert.NotNull(voltageHeader); Assert.NotNull(currentHeader);
                        Assert.Equal("4.2", values[Array.IndexOf(headers, voltageHeader)]);
                        Assert.Equal("20", values[Array.IndexOf(headers, currentHeader)]);
                    }
                    else { Assert.Null(voltageHeader); Assert.Null(currentHeader); }
                    for (int i = 0; i < 15; i++)
                    {
                        double actual = double.Parse(values[Array.IndexOf(headers, $"R{i + 1}")], CultureInfo.InvariantCulture);
                        Assert.InRange(Math.Abs(actual - fixture.Ri[i]), 0, 0.05);
                    }
                    Assert.Equal("Valid", values[Array.IndexOf(headers, "Color Rendering Status")]);
                    Assert.InRange(Math.Abs(double.Parse(values[Array.IndexOf(headers, "Mean Wavelength(nm)")], CultureInfo.InvariantCulture) - fixture.Mean), 0, 0.0001);
                    if (includeNormal)
                    {
                        Assert.True(reader.Read());
                        Assert.Empty(reader.GetField(voltageHeader!)!);
                        Assert.Empty(reader.GetField(currentHeader!)!);
                    }
                }
            }
            finally { File.Delete(path); }
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            ColorVision.UI.ConfigService.SetInstance(previousConfig);
        }
    }

    [Fact]
    public void UnavailableRenderingIsBlankInCsvAndDoesNotReuseOldValues()
    {
        PluginResult result = new(new COLOR_PARA
        {
            fPL = [1, 1], fSpect1 = 500, fSpect2 = 501, fInterval = 1,
            fRa = 99, fRi = Enumerable.Repeat(99f, 15).ToArray()
        });
        Assert.Null(result.ColorRendering.Ra);
        Assert.All(result.RenderingIndices, value => Assert.Null(value));
        string[] lines = SpectrumCsvExporter.CreateCsv([result], false).Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        string[] headers = lines[0].Split(',');
        string[] values = lines[1].Split(',');
        Assert.Empty(values[Array.IndexOf(headers, "Color Rendering(Ra)")]);
        Assert.Empty(values[Array.IndexOf(headers, "R9")]);
        Assert.Equal("InsufficientWavelengthRange", values[Array.IndexOf(headers, "Color Rendering Status")]);
        Assert.Equal(500.5, result.ColorRendering.MeanWavelength!.Value);
    }

    [Fact]
    public void UnavailableCriIsStoredAsNullInNumericHistoryColumns()
    {
        COLOR_PARA data = SpectrumColorRendering.Apply(new COLOR_PARA
        {
            fPL = [1, 1], fSpect1 = 500, fSpect2 = 501, fInterval = 1
        });
        var model = LocalSpectrumResultService.CreateModel(new LocalSpectrumCapture { Data = data }, new());
        Assert.Null(model.fRa);
        Assert.Null(model.fRi);
        Assert.Equal(500.5f, model.fLav);
    }
}
