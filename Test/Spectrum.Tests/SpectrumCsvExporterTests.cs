using cvColorVision;
using Spectrum.Models;
using System.Globalization;
using System.IO;

namespace Spectrum.Tests;

public sealed class SpectrumCsvExporterTests
{
    [Fact]
    public void NormalExportUsesNormalColumnsAndActualSpectrumPairs()
    {
        ViewResultSpectrum result = CreateResult(500, 2, [0.25f, 0.5f, 1f]);
        result.Id = 42;

        string csv = SpectrumCsvExporter.CreateCsv([result], isEqeMode: false);
        string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("No,IP,Luminance(Lv)(cd/m2),Blue Light Intensity", lines[0]);
        Assert.EndsWith(",500,502,504,sp500,sp502,sp504", lines[0]);
        Assert.DoesNotContain("EQE(%)", lines[0]);
        Assert.EndsWith(",2.5,5,10,0.25,0.5,1", lines[1]);
    }

    [Fact]
    public void EqeExportUsesEqeColumns()
    {
        ViewResultSpectrum result = CreateResult(500, 2, [1f, 0.5f]);
        result.Eqe = 0.1234;
        result.LuminousFlux = 12.5f;
        result.RadiantFlux = 2.5;
        result.LuminousEfficacy = 5;
        result.V = 4.2f;
        result.I = 20f;

        string csv = SpectrumCsvExporter.CreateCsv([result], isEqeMode: true);
        string header = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[0];

        Assert.StartsWith("No,IP,EQE(%),LuminousFlux(lm),RadiantFlux(W),LuminousEfficacy(lm/W)", header);
        Assert.Contains("Voltage(V),Current(mA)", header);
        Assert.DoesNotContain("Luminance(Lv)(cd/m2)", header);
        Assert.Contains(",12.34,12.5,2.5,5,", csv);
    }

    [Fact]
    public void ExportUsesNonOneNanometerSamplingWithoutInventingColumns()
    {
        ViewResultSpectrum result = CreateResult(400, 2.5f, [1f, 0.8f, 0.6f]);

        string header = SpectrumCsvExporter.CreateCsv([result], isEqeMode: false)
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries)[0];

        Assert.EndsWith(",400,402.5,405,sp400,sp402.5,sp405", header);
        Assert.DoesNotContain(",401,", header);
    }

    [Fact]
    public void ExportUsesInvariantNumbersAndEscapesCsvText()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            ViewResultSpectrum result = CreateResult(500, 2, [1.25f]);
            result.IP = "sample,\"quoted\"\nnext";
            result.Lv = "1,25";
            result.fx = 1.25f;

            string csv = SpectrumCsvExporter.CreateCsv([result], isEqeMode: false);

            Assert.Contains("\"sample,\"\"quoted\"\"\nnext\"", csv);
            Assert.Contains(",\"1,25\",", csv);
            Assert.Contains(",1.25,", csv);
            Assert.DoesNotContain(",1,25,", csv);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task WriteAsyncUsesTheSnapshotCapturedAtCallTime()
    {
        ViewResultSpectrum result = CreateResult(500, 2, [1f, 0.5f]);
        result.IP = "before-export";
        string filePath = Path.Combine(Path.GetTempPath(), $"spectrum-export-{Guid.NewGuid():N}.csv");
        try
        {
            Task writeTask = SpectrumCsvExporter.WriteAsync(filePath, [result], isEqeMode: false);
            result.IP = "after-export";
            await writeTask;

            string csv = await File.ReadAllTextAsync(filePath);
            Assert.Contains("before-export", csv);
            Assert.DoesNotContain("after-export", csv);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void EqeRecalculationClearsDerivedValuesWhenInputsBecomeUnavailable()
    {
        ViewResultSpectrum result = CreateResult(500, 2, [1f, 0.5f]);
        result.CalculateEqeParams(5, 10);
        Assert.NotNull(result.LuminousEfficacy);
        Assert.NotEqual(0, result.RadiantFlux);

        result.CalculateEqeParams(0, 0);
        Assert.Null(result.LuminousEfficacy);

        result.fPL = [];
        result.CalculateEqeParams(5, 10);
        Assert.Equal(0, result.RadiantFlux);
    }

    private static ViewResultSpectrum CreateResult(float start, float interval, float[] values)
    {
        return new ViewResultSpectrum(new COLOR_PARA
        {
            fSpect1 = start,
            fSpect2 = start + interval * (values.Length - 1),
            fInterval = interval,
            fPL = values,
            fPlambda = 10,
            fPh = 100,
            fx = 0.25f,
            fy = 0.3f
        });
    }
}
