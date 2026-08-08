using Newtonsoft.Json;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Black;
using ProjectARVRPro.Process.KeyedResults.FieldOfView;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using ProjectARVRPro.Process.MTF.MTFHV048;
using ProjectARVRPro.Process.W255;
using ProjectARVRPro.Process.W51;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Xunit;

namespace ProjectARVRPro.Tests;

public class ObjectiveTestResultBatchCsvExporterTests
{
    [Fact]
    public void Collector_CoversStaticListsKeyedDynamicAndPoiResults()
    {
        var result = new ObjectiveTestResult
        {
            BlackTestResult = new BlackTestResult
            {
                FOFOContrast = new ObjectiveTestItem { Name = "Contrast", TestValue = "12.50", Value = 12.5 }
            },
            W51TestResult = new W51TestResult
            {
                HorizontalFieldOfViewAngle = new ObjectiveTestItem { Name = "LegacyFov", Value = 1 }
            },
            W255TestResult = new W255TestResult
            {
                CenterLunimance = new ObjectiveTestItem { Name = "LegacyLuminance", Value = 2 }
            },
            MTFHV048TestResults =
            [
                new MTFHV048TestResult
                {
                    MTF_HV_H_Center_0F = new ObjectiveTestItem { Name = "Center", Value = 0.75 }
                }
            ]
        };
        result.FieldOfViewTestResults["White"] = new FieldOfViewTestResult
        {
            HorizontalFieldOfViewAngle = new ObjectiveTestItem { Name = "Fov", Value = 91.25 }
        };
        result.LuminanceChromaticityTestResults["White"] = new LuminanceChromaticityTestResult
        {
            CenterLuminance = new ObjectiveTestItem { Name = "Luminance", Value = 123.5 }
        };
        result.DynamicTestResults["Dynamic"] = new ObservableCollection<ObjectiveTestItem>
        {
            new() { Name = "Point", Value = 3.5 }
        };
        result.DynamicPoixyuvDatas["PoiTest"] = new ObservableCollection<PoixyuvData>
        {
            new() { Name = "P1", Y = 10.5, x = 0.1, y = 0.2, u = 0.3, v = 0.4 }
        };

        IReadOnlyList<ObjectiveTestResultMetric> metrics = ObjectiveTestResultMetricCollector.Collect(result);

        Assert.Contains(metrics, metric => metric.Key == $"Black{ObjectiveTestResultMetricCollector.KeySeparator}Contrast" && metric.Value == "12.50");
        Assert.Contains(metrics, metric => metric.Header == "MTF0481_Center" && metric.Value == "0.75");
        Assert.Contains(metrics, metric => metric.Header == "White_Fov" && metric.Value == "91.25");
        Assert.Contains(metrics, metric => metric.Header == "White_Luminance" && metric.Value == "123.5");
        Assert.Contains(metrics, metric => metric.Header == "Dynamic_Point" && metric.Value == "3.5");
        Assert.Equal(5, metrics.Count(metric => metric.Header.StartsWith("PoiTest_P1(", StringComparison.Ordinal)));
        Assert.DoesNotContain(metrics, metric => metric.Header.StartsWith("W51_", StringComparison.Ordinal));
        Assert.DoesNotContain(metrics, metric => metric.Header.StartsWith("W255_", StringComparison.Ordinal));
    }

    [Fact]
    public void ExportToCsv_UsesFirstRecordColumnsAndKeepsBadJsonMetadataRows()
    {
        DateTime start = new(2026, 8, 9, 10, 11, 12, 123, DateTimeKind.Local);
        string path = CreateTempPath();
        try
        {
            var records = new[]
            {
                CreateRecord("SN-1", start, CreateDynamicResult(("A", "first-a"), ("B", "first-b"))),
                CreateRecord("SN-2", start.AddMinutes(1), CreateDynamicResult(("B", "second-b"), ("A", "second-a"), ("C", "ignored"))),
                CreateRecord("SN-3", start.AddMinutes(2), CreateDynamicResult(("B", "third-b"))),
                new ObjectiveTestResultRecord
                {
                    SN = "SN-4",
                    CreateTime = start.AddMinutes(3),
                    UpdateTime = start.AddMinutes(3).AddSeconds(2),
                    ObjectiveTestResultJson = "{invalid json"
                }
            };

            ObjectiveTestResultBatchCsvExporter.ExportToCsv(records, path);

            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            Assert.Equal("SN,开始时间,结束时间,Screen_A,Screen_B", lines[0]);
            Assert.Equal("SN-1,2026-08-09 10:11:12.123,2026-08-09 10:11:14.123,first-a,first-b", lines[1]);
            Assert.Equal("SN-2,2026-08-09 10:12:12.123,2026-08-09 10:12:14.123,second-a,second-b", lines[2]);
            Assert.Equal("SN-3,2026-08-09 10:13:12.123,2026-08-09 10:13:14.123,,third-b", lines[3]);
            Assert.Equal("SN-4,2026-08-09 10:14:12.123,2026-08-09 10:14:14.123,,", lines[4]);
            Assert.DoesNotContain("Screen_C", lines[0], StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ExportToCsv_WritesUtf8BomAndRfc4180Escaping()
    {
        DateTime start = new(2026, 8, 9, 8, 0, 0, DateTimeKind.Local);
        string path = CreateTempPath();
        try
        {
            ObjectiveTestResult result = CreateDynamicResult(("Item,\"Q\"", "line1,\"quoted\"\r\nline2"));
            ObjectiveTestResultBatchCsvExporter.ExportToCsv(
                [CreateRecord("SN,\"1\"", start, result)],
                path);

            byte[] bytes = File.ReadAllBytes(path);
            Assert.True(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));

            string text = File.ReadAllText(path, Encoding.UTF8);
            Assert.Contains("\"Screen_Item,\"\"Q\"\"\"", text, StringComparison.Ordinal);
            Assert.Contains("\"SN,\"\"1\"\"\"", text, StringComparison.Ordinal);
            Assert.Contains("\"line1,\"\"quoted\"\"\r\nline2\"", text, StringComparison.Ordinal);
            Assert.Contains("\r\n", text, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ObjectiveTestResultRecord CreateRecord(string sn, DateTime start, ObjectiveTestResult result)
    {
        return new ObjectiveTestResultRecord
        {
            SN = sn,
            CreateTime = start,
            UpdateTime = start.AddSeconds(2),
            ObjectiveTestResultJson = JsonConvert.SerializeObject(result)
        };
    }

    private static ObjectiveTestResult CreateDynamicResult(params (string Name, string TestValue)[] items)
    {
        var result = new ObjectiveTestResult();
        result.DynamicTestResults["Screen"] = new ObservableCollection<ObjectiveTestItem>(items.Select(item =>
            new ObjectiveTestItem
            {
                Name = item.Name,
                TestValue = item.TestValue,
            }));
        return result;
    }

    private static string CreateTempPath()
    {
        return Path.Combine(Path.GetTempPath(), $"ProjectARVRPro_BatchCsv_{Guid.NewGuid():N}.csv");
    }
}
