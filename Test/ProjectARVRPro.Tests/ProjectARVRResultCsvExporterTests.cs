using CsvHelper;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Exports;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.OpticCenter;
using System.Globalization;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class ProjectARVRResultCsvExporterTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void StandardCsv_KeepsSevenColumnsForCustomNamesAndDecimalValues(bool useSnapshot)
    {
        const string screen = "Near, \"left\"";
        const string itemName = "Brightness\r\ncenter";
        const string unit = "cd/m², corrected";
        const string poiName = "P, \"one\"";
        ObjectiveTestItem[] items =
        [
            new() { Name = itemName, Value = 1.25, LowLimit = 0.5, UpLimit = 2.75, Unit = unit }
        ];
        PoixyuvData[] points = [new() { Name = poiName, Y = 0, x = 0.25, y = 0.5, u = 0.125, v = 0.75 }];
        string path = Path.GetTempFileName();
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            if (useSnapshot)
            {
                var rows = ObjectiveTestCsvRowCollector.FromJson<ObjectiveTestItem[]>(JsonConvert.SerializeObject(items), screen)
                    .Concat(ObjectiveTestCsvRowCollector.FromJson<PoixyuvData[]>(JsonConvert.SerializeObject(points), screen));
                ProjectARVRResultCsvExporter.ExportRows(rows, path);
            }
            else
            {
                var aggregate = new ObjectiveTestResult();
                aggregate.DynamicTestResults[screen] = new(items);
                aggregate.DynamicPoixyuvDatas[screen] = new(points);
                ObjectiveTestResultCsvExporter.ExportToCsv(aggregate, path);
            }

            using var csv = new CsvParser(File.OpenText(path), CultureInfo.InvariantCulture);
            Assert.True(csv.Read());
            Assert.Equal(ProjectARVRResultCsvExporter.Header.Split(','), csv.Record);
            Assert.True(csv.Read());
            Assert.Equal([screen, itemName, "1.25", unit, "0.5", "2.75", "pass"], csv.Record!);
            Assert.True(csv.Read());
            Assert.Equal([screen, $"{poiName}(Lv)", "0", "cd/m2", "0", "0", "None"], csv.Record!);
            Assert.True(csv.Read());
            Assert.Equal([screen, $"{poiName}(Cx)", "0.25", "None", "0", "0", "None"], csv.Record!);
            Assert.True(csv.Read());
            Assert.Equal([screen, $"{poiName}(Cy)", "0.5", "None", "0", "0", "None"], csv.Record!);
            Assert.True(csv.Read());
            Assert.Equal([screen, $"{poiName}(u')", "0.125", "None", "0", "0", "None"], csv.Record!);
            Assert.True(csv.Read());
            Assert.Equal([screen, $"{poiName}(v')", "0.75", "None", "0", "0", "None"], csv.Record!);
            Assert.False(csv.Read());
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData("{}", "Optical_Center")]
    [InlineData("{\"Key\":\" Center_A \"}", "Center_A")]
    [InlineData("{\"Key\":\"  \"}", "Optical_Center")]
    public void OpticCenterOutputKeyStaysConsistentAcrossSnapshotCsvMetricsAndReportLookup(string configJson, string expectedKey)
    {
        var process = new OpticCenterProcess();
        process.SetProcessConfig(configJson);
        var center = new OpticCenterTestResult
        {
            Key = process.Config.GetOutputKey(),
            OptCenterRotation = new() { Name = "OptCenterRotation", Value = 1 }
        };
        var snapshot = JObject.FromObject(center);
        if (configJson == "{}") snapshot.Remove("Key"); // Old result snapshots have no output-key field.
        var result = new ProjectARVRReuslt { ViewResultJson = snapshot.ToString() };
        ResultProcessResolver.Capture(result, process);
        process.Config.Key = "ChangedLater";
        IProcess restored = Assert.IsType<OpticCenterProcess>(ResultProcessResolver.Resolve(result, [process], []));
        Assert.Equal(expectedKey, Assert.Single(restored.GetObjectiveCsvRows(result)).TestScreen);

        var aggregate = new ObjectiveTestResult { OpticCenterTestResult = JsonConvert.DeserializeObject<OpticCenterTestResult>(result.ViewResultJson)! };
        aggregate = JsonConvert.DeserializeObject<ObjectiveTestResult>(JsonConvert.SerializeObject(aggregate))!;
        ObjectiveTestResultMetric metric = Assert.Single(ObjectiveTestResultMetricCollector.Collect(aggregate));
        Assert.Equal(expectedKey + ObjectiveTestResultMetricCollector.KeySeparator + "OptCenterRotation", metric.Key);
        Assert.Equal(1, new ObjectiveTestResultValueResolver(aggregate).Find(expectedKey, "OptCenterRotation")!.Value);

        string path = Path.GetTempFileName();
        try
        {
            ObjectiveTestResultCsvExporter.ExportToCsv(aggregate, path);
            string row = Assert.Single(File.ReadAllLines(path).Skip(1));
            Assert.StartsWith(expectedKey + ",OptCenterRotation,", row, StringComparison.Ordinal);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("SN-123", "TestResults_SN-123_20261023_190236_.csv")]
    [InlineData("P5QD0L??GLJ002Q", "TestResults_P5QD0L__GLJ002Q_20261023_190236_.csv")]
    public void ProductSerialNumberIsFormattedOnlyForTheOutputFile(string serialNumber, string expected)
    {
        string directory = Path.Combine(Path.GetTempPath(), "ArvrSerialNumberExport", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string name = ProjectARVRResultCsvExporter.BuildFileName(serialNumber, new DateTime(2026, 10, 23, 19, 2, 36));
            string path = Path.Combine(directory, name);
            var row = new ObjectiveTestCsvRow(serialNumber, "Luminance", "1", "1", "nit", "0", "0", "PASS");

            ProjectARVRResultCsvExporter.ExportRows([row], path);

            Assert.Equal(expected, name);
            Assert.True(File.Exists(path));
            Assert.Contains(serialNumber, File.ReadAllText(path), StringComparison.Ordinal);
        }
        finally
        {
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
