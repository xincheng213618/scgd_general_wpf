using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Newtonsoft.Json;
using ProjectARVRPro.Process.KeyedResults;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using Xunit;

namespace ProjectARVRPro.Tests;

public sealed class LuminanceChromaticityYWProcessTests
{
    [Fact]
    public void DefaultsProvideOutputKeyAndIndependentThresholds()
    {
        var config = new LuminanceChromaticityYWProcessConfig();

        Assert.Equal("YW", config.GetOutputKey());
        Assert.Equal(750, config.RecipeConfig.AverageLuminance12X7.Min);
        Assert.Equal(0.20, config.RecipeConfig.LuminanceUniformity12X7.Min);
        Assert.Equal(0.05, config.RecipeConfig.ColorUniformity12X7.Max);
        Assert.Equal(750, config.RecipeConfig.AverageLuminance8X7.Min);
        Assert.Equal(0.20, config.RecipeConfig.LuminanceUniformity8X7.Min);
        Assert.Equal(0.05, config.RecipeConfig.ColorUniformity8X7.Max);
    }

    [Fact]
    public void BuildsIndependentPoiGroupsAndLocalStatistics()
    {
        var result = new LuminanceChromaticityYWViewTestResult
        {
            ViewPoixyuvDatas12X7 = CreatePoints(LuminanceChromaticityYWProcess.Expected12X7PointCount, 100, 0.03, 0.04),
            ViewPoixyuvDatas8X7 = CreatePoints(LuminanceChromaticityYWProcess.Expected8X7PointCount, 200, 0.06, 0.08)
        };

        bool success = LuminanceChromaticityYWProcess.TryPopulateCalculatedResults(result, new(), out string errorMessage);

        Assert.True(success, errorMessage);
        Assert.Equal(84, result.PoixyuvDatas12X7.Count);
        Assert.Equal(56, result.PoixyuvDatas8X7.Count);
        Assert.Equal(141.5, result.AverageLuminance12X7.Value, 12);
        Assert.Equal(100d / 183d, result.LuminanceUniformity12X7.Value, 12);
        Assert.Equal(0.05, result.ColorUniformity12X7.Value, 12);
        Assert.Equal(227.5, result.AverageLuminance8X7.Value, 12);
        Assert.Equal(200d / 255d, result.LuminanceUniformity8X7.Value, 12);
        Assert.Equal(0.10, result.ColorUniformity8X7.Value, 12);
        Assert.Equal("54.6448", result.LuminanceUniformity12X7.TestValue);
        Assert.Equal("78.4314", result.LuminanceUniformity8X7.TestValue);
    }

    [Fact]
    public void RejectsIncompletePoiGroups()
    {
        var result = new LuminanceChromaticityYWViewTestResult
        {
            ViewPoixyuvDatas12X7 = CreatePoints(83, 100, 0.03, 0.04),
            ViewPoixyuvDatas8X7 = CreatePoints(56, 200, 0.06, 0.08)
        };

        Assert.False(LuminanceChromaticityYWProcess.TryPopulateCalculatedResults(result, new(), out string errorMessage));
        Assert.Contains("12X7 POI数量应为84", errorMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void LuminanceDisplayPointCentersYWithoutChangingTheSavedPointName()
    {
        var poi = new PoiResultCIExyuvData
        {
            Y = 123.456,
            Point = new PoiPoint(7, 8, "P_7", PoiShape.Circle, 100, 200, 32, 32)
        };

        PoiPoint displayPoint = LuminanceChromaticityYWProcess.CreateLuminanceDisplayPoint(poi);

        Assert.Equal("Y:123.46", displayPoint.Name);
        Assert.Equal("P_7", poi.Point.Name);
        Assert.Equal(poi.Point.PixelX, displayPoint.PixelX);
        Assert.Equal(poi.Point.PixelY, displayPoint.PixelY);
        Assert.Equal(poi.Point.Width, displayPoint.Width);
        Assert.Equal(poi.Point.Height, displayPoint.Height);
    }

    [Fact]
    public void ResultTextShowsSummaryBeforePoiDetails()
    {
        var result = new LuminanceChromaticityYWViewTestResult
        {
            AverageLuminance12X7 = new() { Name = "AverageLuminance_12X7", TestValue = "100" },
            LuminanceUniformity12X7 = new() { Name = "LuminanceUniformity_12X7", TestValue = "90" },
            ViewPoixyuvDatas12X7 = [new() { Name = "P_1", Y = 100 }]
        };

        string text = LuminanceChromaticityYWProcess.BuildResultText("White_E1", result);

        Assert.True(text.IndexOf("[汇总]", StringComparison.Ordinal) < text.IndexOf("[12X7 POI]", StringComparison.Ordinal));
        Assert.True(text.IndexOf("AverageLuminance_12X7", StringComparison.Ordinal) < text.IndexOf("P_1 X:", StringComparison.Ordinal));
    }

    [Fact]
    public void KeyedResultRoundTripsAndExportsBothPoiGroups()
    {
        var yw = new LuminanceChromaticityYWTestResult
        {
            PoixyuvDatas12X7 = [new() { Name = "P_1", Y = 10, x = 0.1, y = 0.2, u = 0.3, v = 0.4 }],
            PoixyuvDatas8X7 = [new() { Name = "P_1", Y = 20, x = 0.5, y = 0.6, u = 0.7, v = 0.8 }],
            AverageLuminance12X7 = new() { Name = "AverageLuminance_12X7", Value = 10 },
            AverageLuminance8X7 = new() { Name = "AverageLuminance_8X7", Value = 20 }
        };
        var destination = new ObjectiveTestResult();

        KeyedTestResultWriter.Write(destination, "YW", yw);
        string json = JsonConvert.SerializeObject(destination);
        ObjectiveTestResult roundTrip = Assert.IsType<ObjectiveTestResult>(JsonConvert.DeserializeObject<ObjectiveTestResult>(json));
        LuminanceChromaticityYWTestResult restored = Assert.Single(roundTrip.LuminanceChromaticityYWTestResults).Value;
        IReadOnlyList<ObjectiveTestResultMetric> metrics = ObjectiveTestResultMetricCollector.Collect(roundTrip);

        Assert.Single(restored.PoixyuvDatas12X7);
        Assert.Single(restored.PoixyuvDatas8X7);
        Assert.Contains(metrics, metric => metric.Header == "YW_AverageLuminance_12X7" && metric.Value == "10");
        Assert.Contains(metrics, metric => metric.Header == "YW_AverageLuminance_8X7" && metric.Value == "20");
        Assert.Equal(5, metrics.Count(metric => metric.Header.StartsWith("YW_12X7_P_1(", StringComparison.Ordinal)));
        Assert.Equal(5, metrics.Count(metric => metric.Header.StartsWith("YW_8X7_P_1(", StringComparison.Ordinal)));
    }

    private static List<PoiResultCIExyuvData> CreatePoints(int count, double firstLuminance, double lastU, double lastV)
    {
        return Enumerable.Range(0, count)
            .Select(index => new PoiResultCIExyuvData
            {
                Id = index,
                Name = $"P_{index + 1}",
                Y = firstLuminance + index,
                u = index == count - 1 ? lastU : 0,
                v = index == count - 1 ? lastV : 0
            })
            .ToList();
    }
}
