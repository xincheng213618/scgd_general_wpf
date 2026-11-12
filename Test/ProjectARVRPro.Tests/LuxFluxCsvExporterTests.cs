using ColorVision.Engine.Services.Devices.Spectrum.Dao;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.UI;
using CsvHelper;
using Newtonsoft.Json;
using ProjectARVRPro.Exports.Lux;
using System.Globalization;
using System.IO;

namespace ProjectARVRPro.Tests;

[Collection(nameof(SocketControlTestGroup))]
public sealed class LuxFluxCsvExporterTests
{
    private const string Cct = "Correlated Color Temperature(CCT)（K）";

    [Fact]
    public void SparseReportUsesMinusOneForMissingValuesAndPreservesMeasuredZero()
    {
        var source = new SpectumResultEntity
        {
            Id = 3, LuminousFlux = 0, VResult = 12, fLp = float.PositiveInfinity, fy = .6f,
            fSpect1 = 500, fSpect2 = 503, fInterval = 1,
            fPL = JsonConvert.SerializeObject(new[] { .5f, float.NaN, 0f, -.25f }),
        };
        var (headers, values) = ReadReport(source);
        var fields = headers.Zip(values).ToDictionary(pair => pair.First, pair => pair.Second);

        Assert.Equal(842, fields.Count);
        Assert.DoesNotContain(values, string.IsNullOrWhiteSpace);
        Assert.DoesNotContain("NaN", values);
        Assert.DoesNotContain("Infinity", values);
        foreach (string field in new[] { "Lot", "IP", "EQE(%)", "RadiantFlux(W)", "LuminousEfficacy(lm/W)", "Cx", Cct,
            "Peak Wavelength(λp)(nm)", "Excitation Purity", "Dominant Wavelength Color", "Voltgage(V) (V)", "Current(I) (mA)",
            "Color Rendering (Ra)", "Mean Wavelength(nm)", "R1", "R15", "Duv", "Complementary Wavelength(nm)",
            "DW(Ld)(nm)", "Color Purity(%)", "380", "sp380", "500", "501", "sp501", "sp504" })
            Assert.Equal("-1", fields[field]);

        Assert.Equal("InvalidChromaticity", fields["Temperature Status"]);
        Assert.Equal("0", fields["LuminousFlux(lm)"]);
        Assert.Equal("0.5", fields["sp500"]);
        Assert.Equal("0", fields["sp502"]);
        Assert.Equal("0", fields["sp503"]);
    }

    [Fact]
    public void ReportKeepsExistingHeadersAndValuesForAvailableMeasurements()
    {
        var source = new SpectumResultEntity
        {
            Id = 4, BatchId = 42, DataType = true, Eqe = .125, LuminousFlux = 12.5f, RadiantFlux = 0,
            LuminousEfficacy = 10, VResult = 5, IResult = 0, fIp = 16384, fx = .3457f, fy = .3585f, fLp = 500,
            fSpect1 = 380, fSpect2 = 780, fInterval = .1f, fPlambda = 2,
            fPL = JsonConvert.SerializeObject(Enumerable.Range(0, 4001).Select(i => 1f + (i % 17) * .01f)),
        };
        var (headers, values) = ReadReport(source);
        var (standardHeaders, standardValues) = ReadReport(source, standard: true);

        Assert.Equal(standardHeaders, headers);
        Assert.Equal(standardValues.Select(value => string.IsNullOrWhiteSpace(value) ? "-1" : value), values);
        int cctIndex = Array.IndexOf(headers, Cct);
        Assert.True(double.Parse(values[cctIndex], CultureInfo.InvariantCulture) > 0);
    }

    private static (string[] Headers, string[] Values) ReadReport(SpectumResultEntity source, bool standard = false)
    {
        string path = Path.Combine(Path.GetTempPath(), $"D_{Guid.NewGuid():N}.csv");
        var previous = ConfigService.Instance;
        try
        {
            ConfigService.SetInstance(new ConfigHandler { IsAutoSave = false });
            var result = new ViewResultSpectrum(source);
            if (standard) SpectrumCsvExportHelper.ExportLuminousFluxMode(path, [result]);
            else LuxFluxCsvExporter.Export(path, source, result);
            using var stream = File.OpenText(path);
            using var csv = new CsvReader(stream, CultureInfo.InvariantCulture);
            Assert.True(csv.Read());
            Assert.True(csv.ReadHeader());
            Assert.True(csv.Read());
            Assert.Equal(csv.HeaderRecord!.Length, csv.Parser.Count);
            return (csv.HeaderRecord, csv.Parser.Record!);
        }
        finally
        {
            ConfigService.SetInstance(previous);
            File.Delete(path);
        }
    }
}
