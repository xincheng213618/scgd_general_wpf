using System.Collections.ObjectModel;
using Newtonsoft.Json;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using ProjectARVRPro.Process.MTF.MTFH;
using ProjectARVRPro.Process.MTF.MTFV;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class MTFHV058DynamicExportTests
{
    [Fact]
    public void ProcessConfigs_ExposeIndependentNormalizedExportGroupNames()
    {
        var h = new MTFHProcessConfig { Name = " LPH_1 " };
        var v = new MTFVProcessConfig { Name = " LPV_1 " };

        Assert.Equal("LPH_1", h.GetOutputName());
        Assert.Equal("LPV_1", v.GetOutputName());
        Assert.Equal("MTFH", new MTFHProcessConfig { Name = " " }.GetOutputName());
        Assert.Equal("MTFV", new MTFVProcessConfig { Name = " " }.GetOutputName());
    }

    [Fact]
    public void DynamicResults_ExportEveryConfiguredMtfGroup()
    {
        var result = new ObjectiveTestResult();
        result.DynamicTestResults["LPH_1"] = Items("MTF_H_Center_0F", 0.51);
        result.DynamicTestResults["LPH_2"] = Items("MTF_H_Center_0F", 0.62);
        result.DynamicTestResults["LPV_1"] = Items("MTF_V_Center_0F", 0.73);

        IReadOnlyList<ObjectiveTestResultMetric> metrics = ObjectiveTestResultMetricCollector.Collect(result);

        Assert.Contains(metrics, metric => metric.Header == "LPH_1_MTF_H_Center_0F" && metric.Value == "0.51");
        Assert.Contains(metrics, metric => metric.Header == "LPH_2_MTF_H_Center_0F" && metric.Value == "0.62");
        Assert.Contains(metrics, metric => metric.Header == "LPV_1_MTF_V_Center_0F" && metric.Value == "0.73");
    }

    [Fact]
    public void ProjectResults_ExportInDatabaseOrderWithoutAggregateResultShape()
    {
        ProjectARVRReuslt white = Result(
            10,
            new LuminanceChromaticityProcess { Config = new() { Key = "White" } },
            LuminanceResult("White_Luminance", 100));
        ProjectARVRReuslt mtfh = Result(
            20,
            new MTFHProcess { Config = new() { Name = "MTFH1" } },
            new MTFHTestResult { MTF_H_Center_0F = new ObjectiveTestItem { Name = "MTF_H_Center_0F", Value = 0.51 } });
        ProjectARVRReuslt mtfv = Result(
            30,
            new MTFVProcess { Config = new() { Name = "MTFV1" } },
            new MTFVTestResult { MTF_V_Center_0F = new ObjectiveTestItem { Name = "MTF_V_Center_0F", Value = 0.61 } });

        IReadOnlyList<ObjectiveTestCsvRow> rows = ProjectARVRResultCsvExporter.CollectRows([mtfv, white, mtfh]);

        Assert.Equal(["White", "MTFH1", "MTFV1"], rows.Select(row => row.TestScreen).Distinct());
        Assert.Contains(rows, row => row.TestScreen == "White" && row.TestItem == "White_Luminance");
    }

    [Fact]
    public void ProjectResults_IgnoreLegacyRowsWithoutProcessSnapshot()
    {
        var result = new ProjectARVRReuslt
        {
            Id = 10,
            Model = "CurrentGroupTemplate",
            ViewResultJson = JsonConvert.SerializeObject(LuminanceResult("Legacy", 100))
        };

        Assert.Empty(ProjectARVRResultCsvExporter.CollectRows([result]));
    }

    [Fact]
    public void RowCollector_PrefersDynamicItemsOverCompatibilityProperties()
    {
        var result = new ResultWithItems
        {
            Items = new() { new ObjectiveTestItem { Name = "Primary", Value = 1 } },
            Compatibility = new ObjectiveTestItem { Name = "Duplicate", Value = 2 }
        };

        IReadOnlyList<ObjectiveTestCsvRow> rows = ObjectiveTestCsvRowCollector.FromJson<ResultWithItems>(
            JsonConvert.SerializeObject(result),
            "Dynamic");

        ObjectiveTestCsvRow row = Assert.Single(rows);
        Assert.Equal("Primary", row.TestItem);
    }

    private static LuminanceChromaticityTestResult LuminanceResult(string name, double value) =>
        new() { CenterLuminance = new ObjectiveTestItem { Name = name, Value = value } };

    private static ProjectARVRReuslt Result(int id, IProcess process, object result)
    {
        var record = new ProjectARVRReuslt
        {
            Id = id,
            ViewResultJson = JsonConvert.SerializeObject(result)
        };
        ResultProcessResolver.Capture(record, process);
        return record;
    }

    private static ObservableCollection<ObjectiveTestItem> Items(string name, double value) =>
        new() { new ObjectiveTestItem { Name = name, Value = value } };

    public sealed class ResultWithItems
    {
        public ObservableCollection<ObjectiveTestItem> Items { get; set; } = new();
        public ObjectiveTestItem Compatibility { get; set; } = new();
    }
}
