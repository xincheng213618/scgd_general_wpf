using System.Collections.ObjectModel;
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

    private static ObservableCollection<ObjectiveTestItem> Items(string name, double value) =>
        new() { new ObjectiveTestItem { Name = name, Value = value } };
}
