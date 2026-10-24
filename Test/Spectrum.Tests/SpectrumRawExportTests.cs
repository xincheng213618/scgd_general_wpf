using cvColorVision;
using CsvHelper;
using System.Globalization;
using System.IO;
using EngineResult = ColorVision.Engine.Services.Devices.Spectrum.Views.ViewResultSpectrum;
using PluginResult = Spectrum.Models.ViewResultSpectrum;

namespace Spectrum.Tests;

// These protect the full-resolution measurement/export contract independently of display sampling.
[Collection("LocalSpectrumDriver")]
public sealed class SpectrumRawExportTests
{
    [Fact]
    public void BothClientsExportAllMeasuredSamplesWithNegativesAndExcludeAbiPadding()
    {
        var previous = ColorVision.UI.ConfigService.Instance;
        ColorVision.UI.ConfigService.SetInstance(new ColorVision.UI.ConfigHandler { IsAutoSave = false });
        try
        {
            float[] values = new float[10000];
            for (int i = 0; i <= 4000; i++) values[i] = (float)(0.5 + Math.Sin(i / 53d));
            values[577] = -0.12345679f;
            values[578] = 0.9876543f;
            COLOR_PARA data = new() { fPL = values, fSpect1 = 380, fSpect2 = 780, fInterval = 0.1f, fPlambda = 10, fx = 0.3127f, fy = 0.329f };
            var plugin = new PluginResult(data);
            var engine = new EngineResult(data);
            Assert.Equal(401, plugin.SpectralDatas.Count);
            foreach (var record in new[] { plugin.CreateRawExportRecord(false), engine.CreateRawExportRecord(false) })
            {
                Assert.Equal(4001, record.Samples.Length);
                Assert.Equal(10000, record.StoredArrayLength);
                var rows = Read(SpectrumRawExporter.CreateCsv([record]));
                Assert.Equal(4001, rows.Count);
                Assert.Equal("380", rows[0]["Wavelength(nm)"]);
                Assert.Equal("780", rows[^1]["Wavelength(nm)"]);
                Assert.Equal("437.7", rows[577]["Wavelength(nm)"]);
                Assert.Equal(values[577], float.Parse(rows[577]["RelativeSpectrum"], CultureInfo.InvariantCulture));
                Assert.Equal(values[578], float.Parse(rows[578]["RelativeSpectrum"], CultureInfo.InvariantCulture));
                Assert.Equal((double)values[577] * 10, double.Parse(rows[577]["AbsoluteSpectrum"], CultureInfo.InvariantCulture));
                Assert.Equal("4000", rows[^1]["SampleIndex"]);
                Assert.Equal("W/(sr*m^2*nm)", rows[0]["AbsoluteSpectrumUnit"]);
                Assert.Equal("StoredSpectrumAfterAcquisitionProcessing", rows[0]["SpectrumSource"]);
            }
        }
        finally { ColorVision.UI.ConfigService.SetInstance(previous); }
    }

    [Fact]
    public void DifferentGridsAndNonFiniteSamplesAreNotInterpolatedOrHidden()
    {
        var first = SpectrumRawExporter.CaptureStoredSpectrum([-0.5f, float.NaN, float.PositiveInfinity], 400, 405, 2.5f, 2) with
        {
            MeasurementId = 3, AcquisitionParametersJson = "{\"name\":\"a,b\",\n\"FilterBW\":5}", Ra = double.NaN
        };
        var second = SpectrumRawExporter.CaptureStoredSpectrum([1, 2], 500, 501, 1, 3) with { MeasurementId = 4, AbsoluteUnit = "W/nm", Mode = "LuminousFlux" };
        var rows = Read(SpectrumRawExporter.CreateCsv([first, second]));
        Assert.Equal(5, rows.Count);
        Assert.Equal(new[] { "400", "402.5", "405", "500", "501" }, rows.Select(row => row["Wavelength(nm)"]));
        Assert.Equal("NaN", rows[1]["RelativeSpectrum"]);
        Assert.Equal("Infinity", rows[2]["AbsoluteSpectrum"]);
        Assert.Empty(rows[0]["Ra"]);
        Assert.Equal(first.AcquisitionParametersJson, rows[0]["AcquisitionParametersJson"]);
        Assert.Equal("W/nm", rows[^1]["AbsoluteSpectrumUnit"]);
    }

    [Fact]
    public void InvalidLegacyGridIsExplicitAndDoesNotReuseDisplayFallback()
    {
        var previous = ColorVision.UI.ConfigService.Instance;
        ColorVision.UI.ConfigService.SetInstance(new ColorVision.UI.ConfigHandler { IsAutoSave = false });
        try
        {
            COLOR_PARA data = new() { fPL = [-0.2f, 0.5f, 1], fSpect1 = 0, fSpect2 = 0, fInterval = 0 };
            var plugin = new PluginResult(data);
            var engine = new EngineResult(data);
            plugin.Gen(); engine.Gen(); // Re-rendering must preserve original provenance.
            foreach (var record in new[] { plugin.CreateRawExportRecord(false), engine.CreateRawExportRecord(false) })
            {
                var rows = Read(SpectrumRawExporter.CreateCsv([record]));
                Assert.Equal(3, rows.Count);
                Assert.All(rows, row => Assert.Empty(row["Wavelength(nm)"]));
                Assert.Equal("InvalidStoredMetadata", rows[0]["GridSource"]);
                Assert.Equal("-0.2", rows[0]["RelativeSpectrum"]);
            }
        }
        finally { ColorVision.UI.ConfigService.SetInstance(previous); }
    }

    [Fact]
    public async Task AsyncExportCapturesValuesAndCancellationPreservesExistingFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"spectrum-raw-{Guid.NewGuid():N}.csv");
        var record = SpectrumRawExporter.CaptureStoredSpectrum([1, -2], 400, 401, 1, 10);
        try
        {
            Task write = SpectrumRawExporter.WriteAsync(path, [record]);
            record.Samples[1] = 99;
            await write;
            Assert.Equal("-2", Read(await File.ReadAllTextAsync(path))[1]["RelativeSpectrum"]);
            string before = await File.ReadAllTextAsync(path);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SpectrumRawExporter.WriteAsync(path, [record], cancellation.Token));
            Assert.Equal(before, await File.ReadAllTextAsync(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task PluginRawSnapshotUsesStoredProfileEvenWhenExportModeChanged()
    {
        var previousConfig = ColorVision.UI.ConfigService.Instance;
        var managerType = typeof(Spectrum.Data.ViewResultManager);
        var initialized = managerType.GetField("_dbInitialized", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        object? previousInitialized = initialized.GetValue(null);
        string previousDirectory = Spectrum.Data.ViewResultManager.DirectoryPath;
        string previousDatabase = Spectrum.Data.ViewResultManager.SqliteDbPath;
        string path = Path.Combine(Path.GetTempPath(), $"spectrum-raw-profile-{Guid.NewGuid():N}.db");
        ColorVision.UI.ConfigService.SetInstance(new ColorVision.UI.ConfigHandler { IsAutoSave = false });
        try
        {
            Spectrum.Data.ViewResultManager.DirectoryPath = Path.GetTempPath();
            Spectrum.Data.ViewResultManager.SqliteDbPath = path;
            initialized.SetValue(null, false);
            var manager = new Spectrum.Data.ViewResultManager();
            var model = new Spectrum.Data.SprectrumModel
            {
                ColorParam = new COLOR_PARA { fPL = [1, -0.5f, 0.25f], fSpect1 = 500, fSpect2 = 502, fInterval = 1, fPlambda = 2 }
            };
            var profile = new Spectrum.Data.SpectrumMeasurementProfile
            {
                InputParametersJson = "{\"RequestedIntTime\":10,\"SmoothingMethod\":1}",
                StepDetailsJson = "{\"ActualIntTime\":12,\"EqeMeasurement\":true}", MeasurementMode = "LuminousFlux"
            };
            var result = manager.SaveMeasurement(model, 5, 10, profile);
            Assert.NotNull(result.AcquisitionParametersJson);
            // A historical result has no in-memory profile, so export must query its own ID.
            var historical = new PluginResult(model);
            var record = Assert.Single(await manager.CreateRawExportSnapshotAsync([historical], false));
            Assert.Equal("MeasurementProfile", record.ModeSource);
            Assert.Equal("LuminousFlux", record.Mode);
            Assert.Equal("W/nm", record.AbsoluteUnit);
            Assert.Contains("SmoothingMethod", record.AcquisitionParametersJson!);
            Assert.Contains("ActualIntTime", record.AcquisitionParametersJson!);
            Assert.Equal(new float[] { 1, -0.5f, 0.25f }, record.Samples);
        }
        finally
        {
            Spectrum.Data.ViewResultManager.DirectoryPath = previousDirectory;
            Spectrum.Data.ViewResultManager.SqliteDbPath = previousDatabase;
            initialized.SetValue(null, previousInitialized);
            ColorVision.UI.ConfigService.SetInstance(previousConfig);
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ElectricalParametersAreExportedOnlyForEqeRecords(bool eqe)
    {
        var previous = ColorVision.UI.ConfigService.Instance;
        ColorVision.UI.ConfigService.SetInstance(new ColorVision.UI.ConfigHandler { IsAutoSave = false });
        try
        {
            const string original = "{\"Parameters\":{\"Voltage\":4.2,\"Current\":20,\"IntegralTime\":100},\"StepDetailsJson\":\"{\\\"EqeVoltage\\\":4.2,\\\"EqeCurrentMA\\\":20,\\\"ActualIntTime\\\":120}\",\"StoredParameters\":\"{\\\"V\\\":4.2,\\\"I\\\":20,\\\"Average\\\":3}\"}";
            COLOR_PARA data = new() { fPL = [1, 0.5f], fSpect1 = 500, fSpect2 = 501, fInterval = 1 };
            var plugin = new PluginResult(data) { AcquisitionFluxMode = eqe, AcquisitionParametersJson = original };
            var engine = new EngineResult(data) { AcquisitionFluxMode = eqe, AcquisitionParametersJson = original };
            foreach (var record in new[] { plugin.CreateRawExportRecord(!eqe), engine.CreateRawExportRecord(!eqe) })
            {
                string parameters = Read(SpectrumRawExporter.CreateCsv([record]))[0]["AcquisitionParametersJson"];
                Assert.Equal(eqe, parameters.Contains("Voltage", StringComparison.Ordinal));
                Assert.Equal(eqe, parameters.Contains("Current", StringComparison.Ordinal));
                Assert.Equal(eqe, parameters.Contains("\\\"V\\\"", StringComparison.Ordinal));
                Assert.Equal(eqe, parameters.Contains("\\\"I\\\"", StringComparison.Ordinal));
                Assert.Contains("IntegralTime", parameters);
                Assert.Contains("ActualIntTime", parameters);
                Assert.Contains("Average", parameters);
                Assert.Equal(original, record.AcquisitionParametersJson);
            }
            Assert.Equal(original, plugin.AcquisitionParametersJson);
            Assert.Equal(original, engine.AcquisitionParametersJson);
        }
        finally { ColorVision.UI.ConfigService.SetInstance(previous); }
    }

    private static List<Dictionary<string, string>> Read(string text)
    {
        using CsvReader reader = new(new StringReader(text), CultureInfo.InvariantCulture);
        reader.Read(); reader.ReadHeader();
        var rows = new List<Dictionary<string, string>>();
        while (reader.Read()) rows.Add(reader.HeaderRecord!.ToDictionary(header => header, header => reader.GetField(header)!));
        return rows;
    }
}
