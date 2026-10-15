using ColorVision.Core;
using ColorVision.Engine.Templates.Jsons.Distortion2;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process.Distortion;
using ProjectARVRPro.Recipe;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class DistortionResultJsonTests
{
    [Fact]
    public void LocalGridGeometryReachesStaticAndDynamicOutputsWithCorrectUnitsAndCsvRows()
    {
        var points = Enumerable.Range(0, 35).Select(id =>
        {
            int row = id / 7, col = id % 7;
            double u = col / 6.0, v = row / 4.0;
            return new GridDistortionPoint { Id = id, Row = row, Col = col, X = 100 + 600 * u + 80 * v, Y = 100 + 400 * v + 60 * u * v };
        }).ToArray();
        GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(new GridDistortionResult
        {
            Success = true, ExpectedRows = 5, ExpectedCols = 7, Points = points
        });
        var source = JsonConvert.DeserializeObject<DistortionReslut>(JsonConvert.SerializeObject(new { Analysis = analysis }))!;
        var view = new DistortionViewTestResult();
        var dynamicProcess = new DistortionDynamicProcess();
        dynamicProcess.Config.ShowConfig = "F2";
        dynamicProcess.Config.Unit = "custom";
        DistortionGeometryResultBuilder.Apply(source, dynamicProcess.Config.RecipeConfig, view, dynamicProcess.Config.ShowConfig);

        Assert.Equal(analysis.Geometry.MaximumTiltDegrees, view.MaximumTiltDegrees!.Value);
        Assert.Equal(analysis.Geometry.MaximumEdgeLengthDifferencePercent, view.MaximumEdgeLengthDifferencePercent!.Value);
        Assert.Equal("°", view.MaximumTiltDegrees.Unit);
        Assert.Equal("%", view.MaximumEdgeLengthDifferencePercent.Unit);
        Assert.True(view.MaximumTiltDegrees.TestResult);
        Assert.True(view.MaximumEdgeLengthDifferencePercent.TestResult);

        var record = new ProjectARVRReuslt { ViewResultJson = JsonConvert.SerializeObject(view) };
        var staticRows = new DistortionProcess().GetObjectiveCsvRows(record);
        Assert.Equal(2, staticRows.Count);
        Assert.Equal("°", Assert.Single(staticRows, row => row.TestItem == nameof(view.MaximumTiltDegrees)).Unit);
        Assert.Equal("%", Assert.Single(staticRows, row => row.TestItem == nameof(view.MaximumEdgeLengthDifferencePercent)).Unit);
        Assert.Equal(2, ObjectiveTestItemCollector.CollectFromJson(record.ViewResultJson).Count);

        var dynamicView = new DistortionDynamicViewTestResult { DistortionViewTestResult = view, Items = DistortionDynamicProcess.CollectItems(view) };
        record.ViewResultJson = JsonConvert.SerializeObject(dynamicView);
        var dynamicRows = dynamicProcess.GetObjectiveCsvRows(record);
        Assert.Equal(2, dynamicRows.Count);
        Assert.Equal(staticRows.Select(row => (row.TestItem, row.Unit)), dynamicRows.Select(row => (row.TestItem, row.Unit)));
        var objective = new ObjectiveTestResult { DistortionTestResult = view };
        objective.DynamicTestResults["Geometry"] = dynamicView.Items;
        var metrics = ObjectiveTestResultMetricCollector.Collect(objective);
        Assert.Contains(metrics, metric => metric.Header == "Distortion_MaximumTiltDegrees");
        Assert.Contains(metrics, metric => metric.Header == "Geometry_MaximumEdgeLengthDifferencePercent");
    }

    [Fact]
    public void GeometryRecipesKeepLegacyDefaultsAndApplyCorrectionsBeforeInclusiveLimits()
    {
        var config = JsonConvert.DeserializeObject<DistortionProcessConfig>("""{"RecipeConfig":{"KeystoneHoriz":{"Max":2}}}""")!;
        var source = JsonConvert.DeserializeObject<DistortionReslut>("""{"Analysis":{"Geometry":{"MaximumTiltDegrees":2.5,"MaximumEdgeLengthDifferencePercent":7.28}}}""")!;
        var target = new DistortionTestResult();
        DistortionGeometryResultBuilder.Apply(source, config.RecipeConfig, target, "F5");
        Assert.True(target.MaximumTiltDegrees!.TestResult);
        Assert.True(target.MaximumEdgeLengthDifferencePercent!.TestResult);
        config.RecipeConfig.MaximumTiltDegrees = new RecipeBase(0, 6, 2, 1);
        config.RecipeConfig.MaximumEdgeLengthDifferencePercent = new RecipeBase(0, 4, 0.5, 1);
        DistortionGeometryResultBuilder.Apply(source, config.RecipeConfig, target, "F5");
        Assert.Equal(6, target.MaximumTiltDegrees!.Value);
        Assert.True(target.MaximumTiltDegrees.TestResult);
        Assert.Equal(4.64, target.MaximumEdgeLengthDifferencePercent!.Value, 10);
        Assert.False(target.MaximumEdgeLengthDifferencePercent.TestResult);
        Assert.Equal(2.5, source.Analysis!.Geometry!.MaximumTiltDegrees);
        Assert.Equal(7.28, source.Analysis.Geometry.MaximumEdgeLengthDifferencePercent);
        var restored = JsonConvert.DeserializeObject<DistortionProcessConfig>(JsonConvert.SerializeObject(config))!;
        Assert.Equal(6, restored.RecipeConfig.MaximumTiltDegrees.Max);
        Assert.Equal(0.5, restored.RecipeConfig.MaximumEdgeLengthDifferencePercent.Fix);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"Analysis":{}}""")]
    [InlineData("""{"Analysis":{"Geometry":{}}}""")]
    [InlineData("""{"Point9_distortion":{"keyStoneHoriRatio":1,"keyStoneVercRatio":2}}""")]
    public void MissingGeometryDoesNotCreateZeroItemsOrChangeHistoricalOutput(string json)
    {
        var source = JsonConvert.DeserializeObject<DistortionReslut>(json)!;
        var target = new DistortionViewTestResult { MaximumTiltDegrees = new() { Value = 5 } };
        DistortionGeometryResultBuilder.Apply(source, new(), target, "F5");
        Assert.Null(target.MaximumTiltDegrees);
        Assert.Null(target.MaximumEdgeLengthDifferencePercent);
        Assert.Empty(DistortionDynamicProcess.CollectItems(target));
        JObject output = JObject.Parse(JsonConvert.SerializeObject(target));
        Assert.Null(output.Property(nameof(target.MaximumTiltDegrees)));
        Assert.Null(output.Property(nameof(target.MaximumEdgeLengthDifferencePercent)));
        Assert.Empty(new DistortionProcess().GetObjectiveCsvRows(new() { ViewResultJson = output.ToString() }));
    }

    [Fact]
    public void PartialGeometryPreservesMeasuredZeroWithoutInventingTheOtherMetric()
    {
        var source = JsonConvert.DeserializeObject<DistortionReslut>("""{"analysis":{"geometry":{"maximumTiltDegrees":0}}}""")!;
        var target = new DistortionTestResult();
        DistortionGeometryResultBuilder.Apply(source, new(), target, "F5");
        Assert.Equal(0, target.MaximumTiltDegrees!.Value);
        Assert.Null(target.MaximumEdgeLengthDifferencePercent);
        Assert.Single(DistortionDynamicProcess.CollectItems(target));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(91, 1)]
    [InlineData(double.NaN, 1)]
    [InlineData(1, double.PositiveInfinity)]
    [InlineData(1, -1)]
    public void InvalidGeometryCannotBecomeSuccessfulZeroResults(double tilt, double edgeDifference)
    {
        var source = new DistortionReslut { Analysis = new() { Geometry = new() { MaximumTiltDegrees = tilt, MaximumEdgeLengthDifferencePercent = edgeDifference } } };
        Assert.Throws<InvalidDataException>(() => DistortionGeometryResultBuilder.Apply(source, new(), new(), "F5"));
    }

    [Fact]
    public void ProjectOutput_UsesStandardOpticalFieldWhileAlgorithmInputKeepsSourceMapping()
    {
        var algorithm = JsonConvert.DeserializeObject<DistortionReslut>(
            """{"Optic_Distortion":{"opticRatio":-0.36}}""")!;
        var result = new ObjectiveTestResult
        {
            DistortionTestResult = new DistortionTestResult
            {
                OpticDistortion = new ObjectiveTestItem
                {
                    Name = nameof(DistortionTestResult.OpticDistortion),
                    Value = algorithm.OpticDistortion.OpticRatio
                }
            }
        };

        var json = JObject.Parse(JsonConvert.SerializeObject(result));
        var distortion = (JObject)json[nameof(ObjectiveTestResult.DistortionTestResult)]!;
        Assert.Equal(-0.36, distortion["OpticDistortion"]!["Value"]!.Value<double>());
        Assert.Equal("OpticDistortion", distortion["OpticDistortion"]!["Name"]!.Value<string>());
        Assert.Null(distortion.Property("Optic_Distortion"));
        Assert.Null(distortion.Property("LegacyOpticDistortion"));
    }

    [Theory]
    [InlineData("""{"Optic_Distortion":{"Value":1}}""", 1)]
    [InlineData("""{"OpticDistortion":{"Value":2}}""", 2)]
    [InlineData("""{"Optic_Distortion":{"Value":1},"OpticDistortion":{"Value":2}}""", 2)]
    [InlineData("""{"OpticDistortion":{"Value":2},"Optic_Distortion":{"Value":1}}""", 2)]
    public void HistoricalResults_ReadBothFieldNamesAndWriteOnlyStandardField(string input, double expected)
    {
        var result = JsonConvert.DeserializeObject<DistortionViewTestResult>(input)!;

        Assert.NotNull(result.OpticDistortion);
        Assert.Equal(expected, result.OpticDistortion.Value);
        var output = JObject.Parse(JsonConvert.SerializeObject(result));
        Assert.Equal(expected, output["OpticDistortion"]!["Value"]!.Value<double>());
        Assert.Null(output.Property("Optic_Distortion"));
    }
}
