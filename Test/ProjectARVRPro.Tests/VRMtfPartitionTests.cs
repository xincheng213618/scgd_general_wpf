using ColorVision.Engine.Templates.Jsons.MTF2;
using Newtonsoft.Json;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.MTF;
using ProjectARVRPro.Process.MTF.VRPartition;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace ProjectARVRPro.Tests;

public sealed class VRMtfPartitionTests
{
    [Theory]
    [InlineData(false, new[] { 4, 48, 204, 460 }, new[] { 434d, 343d, 192d, 11d }, new[] { 465d, 556d, 707d, 888d })]
    [InlineData(true, new[] { 4, 8, 40, 120 }, new[] { 434d, 404d, 343d, 251d }, new[] { 465d, 495d, 556d, 648d })]
    public void LegacyLayoutsKeepRegionalCountsAndNumericScale(bool vertical, int[] counts, double[] minima, double[] maxima)
    {
        var config = new VRMtfPartitionProcessConfig();
        if (vertical)
        {
            config.Key = "VRMTFV";
            config.RB = 2;
            config.RC = 4;
            config.RD = 7.5;
        }
        var source = Enumerable.Range(0, 900).Select(index => new MTFItem
        {
            name = $"P_{index + 1}", mtfValue = index
        }).Reverse().ToArray();

        VRMtfPartitionViewTestResult result = VRMtfPartitionCalculator.Calculate(source, config, default);

        Assert.Equal(900, result.PointCount);
        Assert.Equal(912, result.Items.Count);
        Assert.Equal("P_1", result.Items[0].Name);
        Assert.Equal("P_900", result.Items[899].Name);
        for (int region = 0; region < 4; region++)
        {
            string name = ((char)('A' + region)).ToString();
            Assert.Equal(counts[region], result.RegionPointCounts[name]);
            Assert.Equal(counts[region], result.PointRegions.Count(item => item == region));
            Assert.Equal(minima[region], Find(result, $"Region_{name}_Min").Value);
            Assert.Equal(maxima[region], Find(result, $"Region_{name}_Max").Value);
            Assert.Equal(449.5, Find(result, $"Region_{name}_Average").Value);
        }
    }

    [Fact]
    public void RegionalRecipesAreIndependentAndSurviveHistoryAndBothCsvCollectors()
    {
        var config = SmallGrid();
        config.Key = " VRMTFV ";
        config.RecipeConfig.Region_A_Min.Min = 2;
        config.RecipeConfig.Region_A_Min.Fix = 2;
        config.RecipeConfig.Region_A_Min.B = 0.1;
        var source = Enumerable.Range(0, 25).Select(index => new MTFItem { name = $"P_{index + 1}", mtfValue = 0.5 }).ToArray();
        var result = VRMtfPartitionCalculator.Calculate(source, config, default);
        var configured = Find(result, "Region_A_Min");

        Assert.Equal(1.1, configured.Value, 10);
        Assert.False(configured.TestResult);
        Assert.Equal(0.5, Find(result, "Region_B_Min").Value);
        Assert.Equal(string.Empty, configured.Unit);

        var process = new VRMtfPartitionProcess();
        process.SetProcessConfig(JsonConvert.SerializeObject(config));
        var history = new ProjectARVRReuslt { ViewResultJson = JsonConvert.SerializeObject(result) };
        IReadOnlyList<ObjectiveTestCsvRow> rows = process.GetObjectiveCsvRows(history);
        ObjectiveTestCsvRow row = Assert.Single(rows, item => item.TestItem == "Region_A_Min");
        Assert.Equal("VRMTFV", row.TestScreen);
        Assert.Equal("fail", row.TestResult);
        Assert.Equal("1.100", row.MetricValue);
        Assert.Equal(37, rows.Count);

        var destination = new ObjectiveTestResult();
        destination.DynamicTestResults[process.Config.GetOutputKey()] = result.Items;
        string path = Path.Combine(Path.GetTempPath(), $"vr-mtf-{Guid.NewGuid():N}.csv");
        try
        {
            ObjectiveTestResultCsvExporter.ExportToCsv(destination, path);
            Assert.Contains("VRMTFV,Region_A_Min,1.1,,2,0,fail", File.ReadAllLines(path));
        }
        finally { File.Delete(path); }

        Assert.Equal(2, process.Config.RecipeConfig.Region_A_Min.Fix);
        Assert.Equal(1, process.Config.RecipeConfig.Region_B_Min.Fix);
    }

    [Fact]
    public void CircleBoundariesBelongToTheInnerRegionAndOuterPointsRemainInTheGrid()
    {
        var result = VRMtfPartitionCalculator.Calculate(Points(25), SmallGrid(), default);

        Assert.Equal(1, result.RegionPointCounts["A"]);
        Assert.Equal(4, result.RegionPointCounts["B"]);
        Assert.Equal(8, result.RegionPointCounts["C"]);
        Assert.Equal(12, result.RegionPointCounts["D"]);
        Assert.Equal(25, result.PointCount);
        Assert.Equal(0, result.PointRegions[12]);
        Assert.Equal(1, result.PointRegions[13]);
        Assert.Equal(2, result.PointRegions[14]);
        Assert.Equal(3, result.PointRegions[0]);
        Assert.Same(Brushes.Gray, VRMtfPartitionProcess.GetRegionBrush(result.PointRegions[12]));
        Assert.Same(Brushes.Green, VRMtfPartitionProcess.GetRegionBrush(result.PointRegions[13]));
        Assert.Same(Brushes.Blue, VRMtfPartitionProcess.GetRegionBrush(result.PointRegions[14]));
        Assert.Same(Brushes.Yellow, VRMtfPartitionProcess.GetRegionBrush(result.PointRegions[0]));
        Assert.Same(Brushes.Red, VRMtfPartitionProcess.GetRegionBrush(-1));
    }

    [Theory]
    [InlineData("count")]
    [InlineData("missing")]
    [InlineData("nan")]
    [InlineData("duplicate")]
    [InlineData("radii")]
    [InlineData("empty-region")]
    public void IncompleteOrInvalidInputsCannotProducePlausibleRegionalResults(string invalid)
    {
        var config = SmallGrid();
        var points = Points(invalid == "count" ? 24 : 25);
        if (invalid == "missing") points[0].mtfValue = null;
        if (invalid == "nan") points[0].mtfValue = double.NaN;
        if (invalid == "duplicate") points[0].name = points[1].name;
        if (invalid == "radii") config.RB = config.RA;
        if (invalid == "empty-region") config.RA = 0.1;
        if (invalid == "empty-region") config.Center = 2.5;

        Assert.Throws<ArgumentException>(() => VRMtfPartitionCalculator.Calculate(points, config, default));
    }

    [Fact]
    public async Task FailedMeasurementClearsItsOldOutputWithoutRemovingOtherInstances()
    {
        var process = new VRMtfPartitionProcess();
        var destination = new ObjectiveTestResult();
        destination.DynamicTestResults["vrmtfh"] = new ObservableCollection<ObjectiveTestItem> { new() { Name = "Region_A_Min", Value = 1 } };
        destination.DynamicTestResults["VRMTFV"] = new ObservableCollection<ObjectiveTestItem> { new() { Value = 2 } };
        // Avoid live ProcessManager/DAO initialization: this failure must close before either is used.
        var context = (IProcessExecutionContext)RuntimeHelpers.GetUninitializedObject(typeof(IProcessExecutionContext));
        context.ObjectiveTestResult = destination;
        context.Result = new ProjectARVRReuslt { ViewResultJson = "old", Result = true };

        Assert.False(await process.Execute(context));
        Assert.False(destination.DynamicTestResults.ContainsKey("vrmtfh"));
        Assert.Single(destination.DynamicTestResults);
        Assert.False(context.Result.Result);
        Assert.Equal(string.Empty, context.Result.ViewResultJson);
    }

    [Fact]
    public void PartitionIsDiscoverableUnderTheMtfCategory()
    {
        ProcessTypeOption option = Assert.Single(ProcessTypeCatalog.CreateOptions([new VRMtfPartitionProcess()]));

        Assert.Equal(ProcessTypeCatalog.MtfSubcategory, option.Subcategory);
        Assert.Equal("VR MTF 分区", option.DisplayName);
    }

    private static ObjectiveTestItem Find(VRMtfPartitionTestResult result, string name) => Assert.Single(result.Items, item => item.Name == name);
    private static MTFItem[] Points(int count) => Enumerable.Range(0, count).Select(index => new MTFItem { name = $"P_{index + 1}", mtfValue = index }).ToArray();
    private static VRMtfPartitionProcessConfig SmallGrid() => new() { Size = 5, Center = 2, RA = 0, RB = 1, RC = 2, RD = 3 };
}
