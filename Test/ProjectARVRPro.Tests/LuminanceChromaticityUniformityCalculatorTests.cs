using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Newtonsoft.Json;
using ProjectARVRPro.Process.KeyedResults;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using ProjectARVRPro.Process.Uniformity;
using ProjectARVRPro.Process.W255;
using ProjectARVRPro.Recipe;

namespace ProjectARVRPro.Tests;

public sealed class LuminanceChromaticityUniformityCalculatorTests
{
    [Fact]
    public void CalculatesFromCorrectedPoiValuesInCanonicalUnits()
    {
        var luminanceCorrection = new RecipeBase(0, 0, 1, 20);
        var uCorrection = new RecipeBase(0, 0, 2, 0.01);
        var vCorrection = new RecipeBase(0, 0, 0.5, -0.02);
        var points = new List<PoiResultCIExyuvData>
        {
            Correct(new PoiResultCIExyuvData { Y = 80, u = 0.10, v = 0.20 }, luminanceCorrection, uCorrection, vCorrection),
            Correct(new PoiResultCIExyuvData { Y = 100, u = 0.13, v = 0.24 }, luminanceCorrection, uCorrection, vCorrection)
        };

        var result = LuminanceChromaticityUniformityCalculator.Calculate(points);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, result.PointCount);
        Assert.Equal(110d, result.AverageLuminance, 12);
        Assert.Equal(100d / 120d, result.LuminanceUniformity, 12);
        Assert.Equal(Math.Sqrt(0.06 * 0.06 + 0.02 * 0.02), result.ColorUniformity, 12);
    }

    [Fact]
    public void RejectsInsufficientOrInvalidCorrectedPoiValues()
    {
        var insufficient = LuminanceChromaticityUniformityCalculator.Calculate(new List<PoiResultCIExyuvData>
        {
            new() { Y = 1, u = 0.1, v = 0.2 }
        });
        var invalid = LuminanceChromaticityUniformityCalculator.Calculate(new List<PoiResultCIExyuvData>
        {
            new() { Y = 1, u = 0.1, v = 0.2 },
            new() { Y = 0, u = 0.2, v = 0.3 }
        });

        Assert.False(insufficient.Success);
        Assert.False(invalid.Success);
    }

    [Fact]
    public void W255CalculatesColorCenterRmsToD65DirectlyFromPoiValues()
    {
        const double deltaU = 0.018;
        const double deltaV = 0.024;
        var testResult = new W255TestResult();
        var recipeConfig = new W255RecipeConfig();
        var points = new List<PoiResultCIExyuvData>
        {
            new() { u = ChromaticityCenterCalculator.D65UPrime - deltaU, v = ChromaticityCenterCalculator.D65VPrime - deltaV },
            new() { u = ChromaticityCenterCalculator.D65UPrime + deltaU, v = ChromaticityCenterCalculator.D65VPrime + deltaV }
        };

        bool success = White255Process.TryPopulateColorCenterRmsToD65(testResult, recipeConfig, points);

        Assert.True(success);
        Assert.Equal(0.03, testResult.ColorCenterRmsToD65.Value, 12);
        Assert.Equal("0.03000", testResult.ColorCenterRmsToD65.TestValue);
        Assert.Equal("Color_Center_RMS_To_D65(Δu'v')", testResult.ColorCenterRmsToD65.Name);
        Assert.Equal(0, testResult.ColorCenterRmsToD65.LowLimit);
        Assert.Equal(0, testResult.ColorCenterRmsToD65.UpLimit);
        Assert.True(testResult.ColorCenterRmsToD65.TestResult);
    }

    [Fact]
    public void ExistingResultJsonInitializesColorCenterMetric()
    {
        var result = JsonConvert.DeserializeObject<W255TestResult>("{\"ColorUniformity\":{\"Value\":0.012}}");

        Assert.NotNull(result);
        Assert.NotNull(result.ColorCenterRmsToD65);
        Assert.Equal("Color_Center_RMS_To_D65(Δu'v')", result.ColorCenterRmsToD65.Name);

        var config = JsonConvert.DeserializeObject<W255ProcessConfig>("{\"RecipeConfig\":{\"ColorUniformity\":{\"Min\":0,\"Max\":0.03}}}");
        Assert.NotNull(config);
        Assert.NotNull(config.RecipeConfig.ColorCenterRmsToD65);
        Assert.Equal(0, config.RecipeConfig.ColorCenterRmsToD65.Max);

        var keyedResult = JsonConvert.DeserializeObject<LuminanceChromaticityTestResult>("{\"ColorUniformity\":{\"Value\":0.012}}");
        var keyedConfig = JsonConvert.DeserializeObject<LuminanceChromaticityProcessConfig>("{\"RecipeConfig\":{\"ColorUniformity\":{\"Min\":0,\"Max\":0.03}}}");
        Assert.NotNull(keyedResult);
        Assert.NotNull(keyedConfig);
        Assert.Equal("Color_Center_RMS_To_D65(Δu'v')", keyedResult.ColorCenterRmsToD65.Name);
        Assert.Equal(0, keyedConfig.RecipeConfig.ColorCenterRmsToD65.Min);
        Assert.Equal(0, keyedConfig.RecipeConfig.ColorCenterRmsToD65.Max);
        Assert.Equal(1, keyedConfig.RecipeConfig.ColorCenterRmsToD65.Fix);
    }

    [Fact]
    public void W255ColorCenterMetricAppliesRecipeCorrectionAndLimits()
    {
        var testResult = new W255TestResult();
        var recipeConfig = new W255RecipeConfig
        {
            ColorCenterRmsToD65 = new RecipeBase(0, 0.02, 2, 0.001)
        };
        var points = new List<PoiResultCIExyuvData>
        {
            new() { u = ChromaticityCenterCalculator.D65UPrime + 0.006, v = ChromaticityCenterCalculator.D65VPrime + 0.008 }
        };

        bool success = White255Process.TryPopulateColorCenterRmsToD65(testResult, recipeConfig, points);

        Assert.True(success);
        Assert.Equal(0.021, testResult.ColorCenterRmsToD65.Value, 12);
        Assert.Equal(0, testResult.ColorCenterRmsToD65.LowLimit);
        Assert.Equal(0.02, testResult.ColorCenterRmsToD65.UpLimit);
        Assert.False(testResult.ColorCenterRmsToD65.TestResult);
    }

    [Theory]
    [InlineData("White", 1, 0, 0, 0.03, "0.03000", true)]
    [InlineData("White255", 2, 0.001, 0.06, 0.061, "0.06100", false)]
    public void KeyedColorCenterMetricPreservesRmsCorrectionLimitsAndOutput(string key, double fix, double offset, double upperLimit, double expected, string expectedText, bool pass)
    {
        var testResult = new LuminanceChromaticityTestResult();
        var recipeConfig = new LuminanceChromaticityRecipeConfig
        {
            ColorCenterRmsToD65 = new RecipeBase(0, upperLimit, fix, offset)
        };
        var points = new List<PoiResultCIExyuvData>
        {
            new() { u = ChromaticityCenterCalculator.D65UPrime - 0.018, v = ChromaticityCenterCalculator.D65VPrime - 0.024 },
            new() { u = ChromaticityCenterCalculator.D65UPrime + 0.018, v = ChromaticityCenterCalculator.D65VPrime + 0.024 },
            new() { u = double.NaN, v = ChromaticityCenterCalculator.D65VPrime }
        };

        Assert.True(LuminanceChromaticityProcess.TryPopulateColorCenterRmsToD65(testResult, recipeConfig, points));
        Assert.Equal(expected, testResult.ColorCenterRmsToD65.Value, 12);
        Assert.Equal(expectedText, testResult.ColorCenterRmsToD65.TestValue);
        Assert.Equal(upperLimit, testResult.ColorCenterRmsToD65.UpLimit);
        Assert.Equal(pass, testResult.ColorCenterRmsToD65.TestResult);

        var destination = new ObjectiveTestResult();
        KeyedTestResultWriter.Write(destination, key, testResult);
        var roundTrip = JsonConvert.DeserializeObject<ObjectiveTestResult>(JsonConvert.SerializeObject(destination));
        Assert.NotNull(roundTrip);
        Assert.Equal(expected, roundTrip.LuminanceChromaticityTestResults[key].ColorCenterRmsToD65.Value, 12);
        if (key == "White")
            Assert.Equal(expected, roundTrip.W255TestResult.ColorCenterRmsToD65.Value, 12);

        var rows = ObjectiveTestCsvRowCollector.FromJson<LuminanceChromaticityTestResult>(JsonConvert.SerializeObject(testResult), key);
        var row = Assert.Single(rows, item => item.TestItem == "Color_Center_RMS_To_D65(Δu'v')");
        Assert.Equal(key, row.TestScreen);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KeyedColorCenterMetricRequiresAtLeastOneFinitePoi(bool includeInvalidPoint)
    {
        var points = new List<PoiResultCIExyuvData>();
        if (includeInvalidPoint)
            points.Add(new() { u = double.NaN, v = double.PositiveInfinity });
        var testResult = new LuminanceChromaticityTestResult();

        Assert.False(LuminanceChromaticityProcess.TryPopulateColorCenterRmsToD65(testResult, new(), points));
        Assert.Null(testResult.ColorCenterRmsToD65.TestValue);
    }

    [Fact]
    public void ExistingProcessJsonKeepsTemplateModeAndOriginalResultNames()
    {
        var w255 = JsonConvert.DeserializeObject<W255ProcessConfig>("{\"Key_Center\":\"P_5\",\"SaveCsv\":false}");
        var luminance = JsonConvert.DeserializeObject<LuminanceChromaticityProcessConfig>("{\"Key\":\"White\",\"CenterKey\":\"P_5\",\"SaveCsv\":false}");

        Assert.NotNull(w255);
        Assert.False(w255.CalculateUniformityFromCorrectedPoi);
        Assert.Equal("Luminance_uniformity", w255.GetLuminanceUniformityResultName());
        Assert.Equal("Color_uniformity", w255.GetColorUniformityResultName());
        Assert.NotNull(luminance);
        Assert.False(luminance.CalculateUniformityFromCorrectedPoi);
        Assert.Equal("Luminance_uniformity", luminance.GetLuminanceUniformityResultName());
        Assert.Equal("Color_uniformity", luminance.GetColorUniformityResultName());
    }

    [Fact]
    public void ResultNamesAreTrimmedAndBlankValuesUseOriginalDefaults()
    {
        var luminanceConfig = new LuminanceChromaticityProcessConfig
        {
            LuminanceUniformityResultName = "  CustomLum  ",
            ColorUniformityResultName = "   "
        };
        var w255Config = new W255ProcessConfig
        {
            LuminanceUniformityResultName = "  W255Lum  ",
            ColorUniformityResultName = "  W255Color  "
        };

        Assert.Equal("CustomLum", luminanceConfig.GetLuminanceUniformityResultName());
        Assert.Equal("Color_uniformity", luminanceConfig.GetColorUniformityResultName());
        Assert.True(LuminanceChromaticityUniformityCalculator.MatchesResultName("White_CustomLum_Result", luminanceConfig.GetLuminanceUniformityResultName()));
        Assert.True(LuminanceChromaticityUniformityCalculator.MatchesResultName("WHITE_COLOR_UNIFORMITY_RESULT", luminanceConfig.GetColorUniformityResultName()));
        Assert.False(LuminanceChromaticityUniformityCalculator.MatchesResultName("White_Other_Result", luminanceConfig.GetLuminanceUniformityResultName()));
        Assert.Equal("W255Lum", w255Config.GetLuminanceUniformityResultName());
        Assert.Equal("W255Color", w255Config.GetColorUniformityResultName());
        Assert.True(LuminanceChromaticityUniformityCalculator.MatchesResultName("W255_W255Color_Result", w255Config.GetColorUniformityResultName()));
    }

    private static PoiResultCIExyuvData Correct(
        PoiResultCIExyuvData point,
        RecipeBase luminanceCorrection,
        RecipeBase uCorrection,
        RecipeBase vCorrection)
    {
        point.Y = luminanceCorrection.Apply(point.Y);
        point.u = uCorrection.Apply(point.u);
        point.v = vCorrection.Apply(point.v);
        return point;
    }
}
