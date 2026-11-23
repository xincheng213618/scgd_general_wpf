using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using Newtonsoft.Json;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Distortion;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class DistortionProcessTests
{
    [Theory]
    [InlineData(false, null, "没有可判定")]
    [InlineData(true, null, "没有可判定")]
    [InlineData(false, """{"TV_distortion":{}}""", "没有可判定")]
    [InlineData(true, """{"TV_distortion":{}}""", "没有可判定")]
    [InlineData(false, """{"TV_distortion":{"horizontalRatio":0}}""", "缺少水平或垂直")]
    [InlineData(true, """{"TV_distortion":{"verticalRatio":0},"Point9_distortion":{"keyStoneHoriRatio":0}}""", "缺少水平或垂直")]
    [InlineData(false, """{"Optic_Distortion":{"opticRatio":"NaN"}}""", "opticRatio")]
    [InlineData(true, """{"Point9_distortion":{"leftRatio":"Infinity"}}""", "leftRatio")]
    public async Task NoOrInvalidMeasurementsFailAndClearOnlyTheAffectedOutput(bool dynamic, string? source, string reason)
    {
        using var files = new DistortionFiles(source);
        IProcess process = files.CreateProcess(dynamic);
        var ctx = CreateContext();
        var fixedOutput = ctx.ObjectiveTestResult.DistortionTestResult;
        var dynamicOutput = ctx.ObjectiveTestResult.DynamicTestResults["Panel"];
        var other = ctx.ObjectiveTestResult.DynamicTestResults["panel"];
        ctx.Result.ViewResultJson = "previous snapshot";

        Assert.False(await process.Execute(ctx));
        Assert.False(ctx.Result.Result);
        Assert.False(ctx.ObjectiveTestResult.TotalResult);
        Assert.Contains(reason, ctx.Result.Msg);
        Assert.Equal(string.Empty, ctx.Result.ViewResultJson);
        Assert.Empty(process.GetObjectiveCsvRows(ctx.Result));
        Assert.Same(other, ctx.ObjectiveTestResult.DynamicTestResults["panel"]);
        if (dynamic)
        {
            Assert.Same(fixedOutput, ctx.ObjectiveTestResult.DistortionTestResult);
            Assert.False(ctx.ObjectiveTestResult.DynamicTestResults.ContainsKey("Panel"));
        }
        else
        {
            Assert.Same(dynamicOutput, ctx.ObjectiveTestResult.DynamicTestResults["Panel"]);
            Assert.Null(ctx.ObjectiveTestResult.DistortionTestResult);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task RealTvZerosAndCorrectionsKeepExistingReportFormats(bool dynamic, bool outsideLimit)
    {
        const string source = """{"tv_DISTORTION":{"horizontalRatio":"0","verticalRatio":0}}""";
        using var files = new DistortionFiles(source);
        IProcess process = files.CreateProcess(dynamic);
        var recipe = Assert.IsType<DistortionRecipeConfig>(process.GetRecipeConfig());
        recipe.VerticalTVDistortion.B = outsideLimit ? 3 : 0;
        var ctx = CreateContext();

        Assert.True(await process.Execute(ctx));
        Assert.Equal(!outsideLimit, ctx.Result.Result);
        var rows = process.GetObjectiveCsvRows(ctx.Result);
        Assert.Equal(new[] { "HorizontalTVDistortion", "VerticalTVDistortion" }, rows.Select(row => row.TestItem));
        Assert.Equal(new[] { "0", outsideLimit ? "3" : "0" }, rows.Select(row => row.TestValue));
        Assert.Equal(new[] { "pass", outsideLimit ? "fail" : "pass" }, rows.Select(row => row.TestResult));
        Assert.All(rows, row => Assert.Equal(dynamic ? "%" : string.Empty, row.Unit));
        string formattedZero = 0d.ToString(dynamic ? "F2" : "F5");
        Assert.Equal(dynamic ? formattedZero : formattedZero + "%", rows[0].MetricValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialLegacyRatiosAndGeometryKeepOnlyReportedItemsAndOverlayPoints(bool dynamic)
    {
        const string source = """
            {"TV_distortion":{"finalPoints":[{"id":1,"x":2,"y":3}]},"Optic_Distortion":{},
             "Point9_distortion":{"keyStoneHoriRatio":-0.5,"keyStoneVercRatio":0},
             "Analysis":{"Geometry":{"MaximumTiltDegrees":0}}}
            """;
        using var files = new DistortionFiles(source);
        IProcess process = files.CreateProcess(dynamic);
        var ctx = CreateContext();

        Assert.True(await process.Execute(ctx));
        Assert.True(ctx.Result.Result);
        var rows = process.GetObjectiveCsvRows(ctx.Result);
        Assert.Equal(new[] { "KeystoneHoriz", "KeystoneVert", "MaximumTiltDegrees" }, rows.Select(row => row.TestItem));
        Assert.Equal(new[] { "-0.5", "0", "0" }, rows.Select(row => row.TestValue));
        Assert.Equal("°", rows[2].Unit);

        string snapshot = Assert.IsType<string>(ctx.Result.ViewResultJson);
        var view = dynamic
            ? JsonConvert.DeserializeObject<DistortionDynamicViewTestResult>(snapshot)!.DistortionViewTestResult
            : JsonConvert.DeserializeObject<DistortionViewTestResult>(snapshot)!;
        Assert.Equal(new System.Windows.Point(2, 3), Assert.Single(view.Points));
    }

    private static IProcessExecutionContext CreateContext() => new(_ => [])
    {
        Batch = new MeasureBatchModel { Id = 7 },
        Result = new ProjectARVRReuslt { Result = true },
        ObjectiveTestResult = new ObjectiveTestResult
        {
            DistortionTestResult = new(),
            DynamicTestResults = new()
            {
                ["Panel"] = [new ObjectiveTestItem { Name = "previous" }],
                ["panel"] = [new ObjectiveTestItem { Name = "unrelated" }],
            },
        },
    };

    private sealed class DistortionFiles : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("CV-distortion-process-");
        private readonly List<DetailCommonModel> details = [];

        internal DistortionFiles(string? json)
        {
            if (json == null) return;
            string path = Path.Combine(directory.FullName, "distortion.json");
            File.WriteAllText(path, json);
            details.Add(new DetailCommonModel { PId = 1, ResultJson = JsonConvert.SerializeObject(new ResultFile { ResultFileName = path }) });
        }

        internal IProcess CreateProcess(bool dynamic)
        {
            var data = new DistortionBatchResults
            {
                ReadMasters = _ => [new AlgResultMasterModel { Id = 1, ImgFileType = ViewResultAlgType.Distortion, version = "2.0" }],
                ReadDetails = _ => details,
            };
            if (!dynamic) return new DistortionProcess { BatchResults = data };
            var process = new DistortionDynamicProcess { BatchResults = data };
            process.Config.Name = " Panel ";
            process.Config.ShowConfig = "F2";
            return process;
        }

        public void Dispose()
        {
            foreach (FileInfo file in directory.EnumerateFiles())
                file.Delete();
            directory.Delete();
        }
    }
}
