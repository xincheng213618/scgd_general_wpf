using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Newtonsoft.Json;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.KeyedResults;
using ProjectARVRPro.Process.KeyedResults.LuminanceChromaticity;
using ProjectARVRPro.Process.W255;
using System.Globalization;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class LuminanceChromaticityProcessTests
{
    [Theory]
    [InlineData(false, "missing", "缺少已配置中心点")]
    [InlineData(true, "missing", "缺少已配置中心点")]
    [InlineData(false, "null", "缺少实测数据")]
    [InlineData(true, "uppercase-only", "实测字段 y")]
    [InlineData(false, "nonfinite", "实测字段 CCT")]
    public async Task PassingUniformityCannotSubstituteForAMissingCenter(bool keyed, string failure, string reason)
    {
        using var data = new BatchData();
        data.AddUniformityFiles();
        data.Points.AddRange([CreatePoint("corner-1", 95), CreatePoint("corner-2", 98)]);
        if (failure != "missing")
        {
            data.Points.Add(new PoiPointResultModel
            {
                PoiName = "P_5",
                Value = failure switch
                {
                    "uppercase-only" => """{"X":90,"Y":100,"Z":110,"x":0.32,"u":0.21,"v":0.47,"CCT":6500}""",
                    "nonfinite" => """{"X":90,"Y":100,"Z":110,"x":0.32,"y":0.33,"u":0.21,"v":0.47,"CCT":"NaN"}""",
                    _ => null,
                },
            });
        }

        IProcess process = keyed ? new LuminanceChromaticityProcess { BatchResults = data.Source } : new White255Process { BatchResults = data.Source };
        var ctx = CreateContext();
        ctx.Result.ViewResultJson = "previous success";
        var other = new LuminanceChromaticityTestResult();
        KeyedTestResultWriter.Write(ctx.ObjectiveTestResult, "Red", other);
        if (keyed)
            KeyedTestResultWriter.Write(ctx.ObjectiveTestResult, "white", new LuminanceChromaticityTestResult());
        else
            ctx.ObjectiveTestResult.W255TestResult = new();

        Assert.False(await process.Execute(ctx));
        Assert.False(ctx.Result.Result);
        Assert.False(ctx.ObjectiveTestResult.TotalResult);
        Assert.Contains("P_5", ctx.Result.Msg);
        Assert.Contains(reason, ctx.Result.Msg);
        Assert.Equal(string.Empty, ctx.Result.ViewResultJson);
        Assert.Empty(process.GetObjectiveCsvRows(ctx.Result));
        Assert.Null(ctx.ObjectiveTestResult.W255TestResult);
        Assert.Same(other, Assert.Single(ctx.ObjectiveTestResult.LuminanceChromaticityTestResults).Value);
    }

    [Theory]
    [InlineData(false, 0, true)]
    [InlineData(true, 0, true)]
    [InlineData(false, 100, false)]
    [InlineData(true, 100, false)]
    public async Task ActualCenterAndEitherUniformitySourcePreserveExistingOutput(bool keyed, double centerLuminance, bool useTemplate)
    {
        using var data = new BatchData();
        if (useTemplate)
            data.AddUniformityFiles();
        var center = CreatePoint(keyed ? "CENTER" : "center", centerLuminance);
        data.Points.AddRange([center, CreatePoint("corner", 95)]);
        var normalizedCenter = new PoiResultCIExyuvData(center);

        IProcess process;
        if (keyed)
        {
            var configured = new LuminanceChromaticityProcess { BatchResults = data.Source };
            configured.Config.CenterKey = "center";
            configured.Config.RecipeConfig.CenterCIE1931ChromaticCoordinatesx.B = 0.01;
            process = configured;
        }
        else
        {
            var configured = new White255Process { BatchResults = data.Source };
            configured.Config.Key_Center = "center";
            configured.Config.RecipeConfig.CenterCIE1931ChromaticCoordinatesx.B = 0.01;
            process = configured;
        }
        var ctx = CreateContext();

        Assert.True(await process.Execute(ctx));
        var white = Assert.IsType<W255TestResult>(ctx.ObjectiveTestResult.W255TestResult);
        Assert.Equal(white.CenterCorrelatedColorTemperature.TestResult, ctx.Result.Result);
        Assert.Equal(normalizedCenter.Y, white.CenterLunimance.Value);
        Assert.NotNull(white.CenterLunimance.TestValue);
        Assert.Equal(normalizedCenter.x + 0.01, white.CenterCIE1931ChromaticCoordinatesx.Value, 5);
        Assert.Equal(normalizedCenter.y, white.CenterCIE1931ChromaticCoordinatesy.Value, 5);
        Assert.Equal(normalizedCenter.CCT, white.CenterCorrelatedColorTemperature.Value);
        Assert.Equal(useTemplate ? 0.9 : 0.95, white.LuminanceUniformity.Value, 6);
        Assert.NotEmpty(process.GetObjectiveCsvRows(ctx.Result));
    }

    [Theory]
    [InlineData(5999, false, false)]
    [InlineData(6000, false, true)]
    [InlineData(7000, false, true)]
    [InlineData(7001, false, false)]
    [InlineData(7001, true, true)]
    public async Task FixedWhite255AppliesConfiguredCctLimitsToTheStepAndKeepsItsReport(int correctedCct, bool disableLimits, bool passed)
    {
        using var data = new BatchData();
        data.AddUniformityFiles();
        const int cctOffset = 50;
        data.Points.Add(CreatePoint("P_5", 100, correctedCct - cctOffset));

        var process = new White255Process { BatchResults = data.Source };
        var recipe = process.Config.RecipeConfig.CenterCorrelatedColorTemperature;
        recipe.B = cctOffset;
        if (disableLimits)
        {
            recipe.Min = 0;
            recipe.Max = 0;
        }
        var ctx = CreateContext();

        Assert.True(await process.Execute(ctx));
        Assert.Equal(passed, ctx.Result.Result);
        var cct = ctx.ObjectiveTestResult.W255TestResult.CenterCorrelatedColorTemperature;
        Assert.Equal(correctedCct, cct.Value);
        Assert.Equal(passed, cct.TestResult);

        var row = Assert.Single(process.GetObjectiveCsvRows(ctx.Result), item => item.TestItem == "Center_Correlated_Color_Temperature");
        Assert.Equal(correctedCct.ToString(CultureInfo.InvariantCulture), row.TestValue);
        Assert.Equal(recipe.Min.ToString(CultureInfo.InvariantCulture), row.LowerLimit);
        Assert.Equal(recipe.Max.ToString(CultureInfo.InvariantCulture), row.UpperLimit);
        Assert.Equal("K", row.Unit);
        Assert.Equal(passed ? "pass" : "fail", row.TestResult);
    }

    [Fact]
    public async Task RejectingWhitePreservesAnIndependentLegacyOrKeyedResult()
    {
        var ctx = CreateContext();
        var independent = new W255TestResult();
        var keyed = new LuminanceChromaticityTestResult();
        KeyedTestResultWriter.Write(ctx.ObjectiveTestResult, "White", keyed);
        ctx.ObjectiveTestResult.W255TestResult = independent;

        Assert.False(await new LuminanceChromaticityProcess().ExecuteFailure(ctx));
        Assert.Same(independent, ctx.ObjectiveTestResult.W255TestResult);
        Assert.Empty(ctx.ObjectiveTestResult.LuminanceChromaticityTestResults);

        KeyedTestResultWriter.Write(ctx.ObjectiveTestResult, "White", keyed);
        ctx.ObjectiveTestResult.W255TestResult = independent;
        Assert.False(await new White255Process().ExecuteFailure(ctx));
        Assert.Same(keyed, ctx.ObjectiveTestResult.LuminanceChromaticityTestResults["White"]);
        Assert.Same(keyed.CenterLuminance, ctx.ObjectiveTestResult.W255TestResult.CenterLunimance);
    }

    private static IProcessExecutionContext CreateContext() => new(_ => [])
    {
        Batch = new MeasureBatchModel { Id = 1 },
        Result = new ProjectARVRReuslt { Result = true },
        ObjectiveTestResult = new ObjectiveTestResult(),
    };

    private static PoiPointResultModel CreatePoint(string name, double luminance, int cct = 6500) => new()
    {
        PoiName = name,
        Value = FormattableString.Invariant($$"""{"X":90,"Y":"{{luminance}}","Z":110,"x":0.32,"y":0.34,"u":0.21,"v":0.47,"CCT":{{cct}}}"""),
    };

    private sealed class BatchData : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("CV-luminance-center-");
        private readonly List<AlgResultMasterModel> masters = [new() { Id = 1, ImgFileType = ViewResultAlgType.POI_XYZ }];
        private readonly Dictionary<int, DetailCommonModel> details = [];

        internal List<PoiPointResultModel> Points { get; } = [];
        internal LuminanceChromaticityBatchResults Source => new()
        {
            ReadMasters = _ => masters,
            ReadPoints = _ => Points,
            ReadDetails = id => [details[id]],
        };

        internal void AddUniformityFiles()
        {
            AddUniformity(2, "Luminance_uniformity", 0.9);
            AddUniformity(3, "Color_uniformity", 0.01);
        }

        private void AddUniformity(int id, string name, double value)
        {
            string path = Path.Combine(directory.FullName, name + ".json");
            File.WriteAllText(path, JsonConvert.SerializeObject(new { result = new { Content = name, Value = value } }));
            masters.Add(new AlgResultMasterModel { Id = id, ImgFileType = ViewResultAlgType.PoiAnalysis, TName = name });
            details.Add(id, new() { PId = id, ResultJson = JsonConvert.SerializeObject(new ResultFile { ResultFileName = path }) });
        }

        public void Dispose()
        {
            foreach (FileInfo file in directory.EnumerateFiles())
                file.Delete();
            directory.Delete();
        }
    }
}
