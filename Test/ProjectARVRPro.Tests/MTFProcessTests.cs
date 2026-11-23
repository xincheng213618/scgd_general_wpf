using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using Newtonsoft.Json;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.MTF;
using ProjectARVRPro.Process.MTF.MTFHVDynamic;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class MTFProcessTests
{
    private const string GroupOnly = """
        {"resultChild":[{"name":"HV","horizontalAverage":0.5,"verticalAverage":0.75}]}
        """;

    [Theory]
    [InlineData("no-match", "没有可判定")]
    [InlineData("no-detail", "没有可判定")]
    [InlineData("no-file", "没有可判定")]
    [InlineData("empty", "没有可判定")]
    [InlineData("group-only", "没有可判定")]
    [InlineData("null", "缺少有限的测量值")]
    [InlineData("nonfinite", "缺少有限的测量值")]
    public async Task NoOrInvalidFlatMeasurementsFailWithoutPublishingPartialData(string source, string reason)
    {
        using var files = new MtfFiles();
        switch (source)
        {
            case "no-match":
                files.Add("""{"result":[{"name":"legacy","mtfValue":1}]}""", version: "1.0");
                break;
            case "no-detail":
                files.Add(null, withDetail: false);
                break;
            case "no-file":
                files.Add(null);
                break;
            case "empty":
                files.Add("""{"result":[]}""");
                break;
            case "group-only":
                files.Add(GroupOnly);
                break;
            default:
                files.Add("""{"result":[{"name":"real","mtfValue":0.5}]}""");
                string value = source == "null" ? "null" : "\"NaN\"";
                files.Add($$"""{"result":[{"name":"bad","mtfValue":{{value}}}]}""");
                break;
        }

        MTFProcess process = files.CreateProcess();
        // A missing sample must fail even when the recipe accepts an actual zero.
        process.Config.RecipeConfig.UnifiedRecipe.Min = 0;
        var ctx = CreateContext();
        ctx.Result.ViewResultJson = "old snapshot";
        var other = ctx.ObjectiveTestResult.DynamicTestResults["panel"];

        Assert.False(await process.Execute(ctx));
        Assert.False(ctx.Result.Result);
        Assert.False(ctx.ObjectiveTestResult.TotalResult);
        Assert.Contains(reason, ctx.Result.Msg);
        Assert.Empty(process.GetObjectiveCsvRows(ctx.Result));
        Assert.Equal(string.Empty, ctx.Result.ViewResultJson);
        Assert.Same(other, Assert.Single(ctx.ObjectiveTestResult.DynamicTestResults).Value);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0.1, false)]
    public async Task ActualZeroAndMeasuredResultsAcrossMastersUseExistingRecipeAndCsv(double lowerLimit, bool passed)
    {
        using var files = new MtfFiles();
        files.Add("""{"result":[{"name":"zero","mtfValue":"0"}]}""");
        files.Add("""{"result":[{"name":"rectangle","mtfValue":0.25}],"resultChild":[{"name":"HV","horizontalAverage":0.75}]}""");
        files.Add(GroupOnly);

        var process = files.CreateProcess();
        var recipe = process.Config.RecipeConfig.UnifiedRecipe;
        recipe.Min = lowerLimit;
        recipe.Max = 0.5;
        recipe.Fix = 2;
        var ctx = CreateContext();

        Assert.True(await process.Execute(ctx));
        Assert.Equal(passed, ctx.Result.Result);
        var items = ctx.ObjectiveTestResult.DynamicTestResults["Panel"];
        Assert.Equal(new[] { "zero", "rectangle" }, items.Select(item => item.Name));
        Assert.Equal(new[] { 0d, 0.5d }, items.Select(item => item.Value));
        var rows = process.GetObjectiveCsvRows(ctx.Result);
        Assert.Equal(new[] { "0", "0.5" }, rows.Select(row => row.TestValue));
        Assert.Equal(new[] { passed ? "pass" : "fail", "pass" }, rows.Select(row => row.TestResult));
        Assert.All(rows, row => Assert.Equal("Panel", row.TestScreen));
        var snapshot = Assert.IsType<MTFViewTestResult>(JsonConvert.DeserializeObject<MTFViewTestResult>(ctx.Result.ViewResultJson!));
        Assert.Equal("rectangle", Assert.Single(snapshot.MTFDetailViewReslut.MTFResult!.result).name);

        ctx.Result.Msg = "capture failed";
        Assert.False(await process.ExecuteFailure(ctx));
        Assert.Equal("capture failed", ctx.Result.Msg);
        Assert.False(ctx.ObjectiveTestResult.TotalResult);
        Assert.Empty(process.GetObjectiveCsvRows(ctx.Result));
        Assert.Equal("panel", Assert.Single(ctx.ObjectiveTestResult.DynamicTestResults).Key);
    }

    [Theory]
    [InlineData("""{"result":[{"name":"flat","mtfValue":0.8}]}""", "没有可判定")]
    [InlineData("""{"resultChild":[]}""", "没有可判定")]
    [InlineData("""{"resultChild":[{"name":" ","horizontalAverage":0.8,"verticalAverage":0.8}]}""", "没有可判定")]
    [InlineData("""{"resultChild":[{"name":"bad","verticalAverage":0.8}]}""", "horizontalAverage")]
    [InlineData("""{"resultChild":[{"name":"bad","horizontalAverage":0.8}]}""", "verticalAverage")]
    [InlineData("""{"resultChild":[{"name":"bad","horizontalAverage":0.8,"verticalAverage":null}]}""", "verticalAverage")]
    [InlineData("""{"resultChild":[{"name":"bad","horizontalAverage":true,"verticalAverage":0.8}]}""", "horizontalAverage")]
    [InlineData("""{"resultChild":[{"name":"bad","horizontalAverage":0.8,"verticalAverage":"NaN"}]}""", "verticalAverage")]
    public async Task MissingOrInvalidHvAveragesFailAndRemoveOnlyTheAffectedKey(string source, string reason)
    {
        using var files = new MtfFiles();
        if (reason != "没有可判定")
            files.Add(GroupOnly);
        files.Add(source);

        var process = files.CreateHvProcess();
        process.Config.RecipeConfig.HRecipe.Min = 0;
        process.Config.RecipeConfig.VRecipe.Min = 0;
        var ctx = CreateContext();
        ctx.Result.ViewResultJson = "old snapshot";
        var other = ctx.ObjectiveTestResult.DynamicTestResults["panel"];

        Assert.False(await process.Execute(ctx));
        Assert.False(ctx.Result.Result);
        Assert.False(ctx.ObjectiveTestResult.TotalResult);
        Assert.Contains(reason, ctx.Result.Msg);
        Assert.Equal(string.Empty, ctx.Result.ViewResultJson);
        Assert.Empty(process.GetObjectiveCsvRows(ctx.Result));
        Assert.Same(other, Assert.Single(ctx.ObjectiveTestResult.DynamicTestResults).Value);
    }

    [Fact]
    public async Task GroupOnlyHvDataPreservesActualZeroAndAppliesIndependentRecipes()
    {
        using var files = new MtfFiles();
        files.Add("""{"ResultChild":[{"Name":"0.7F_MTF_HV_LeftUp","HorizontalAverage":"0","VerticalAverage":"0.25"}]}""");
        files.Add("""{"result":[{"name":"flat","mtfValue":0.8}]}""");

        var process = files.CreateHvProcess();
        process.Config.RecipeConfig.HRecipe.Min = 0;
        process.Config.RecipeConfig.VRecipe.Fix = 2;
        process.Config.RecipeConfig.VRecipe.B = 0.125;
        process.Config.RecipeConfig.VRecipe.Max = 0.625;
        var ctx = CreateContext();

        Assert.True(await process.Execute(ctx));
        Assert.True(ctx.Result.Result);
        var items = ctx.ObjectiveTestResult.DynamicTestResults["Panel"];
        Assert.Equal(new[] { "MTF_HV_H_LeftUp_0_7F", "MTF_HV_V_LeftUp_0_7F" }, items.Select(item => item.Name));
        Assert.Equal(new[] { 0d, 0.625d }, items.Select(item => item.Value));
        var rows = process.GetObjectiveCsvRows(ctx.Result);
        Assert.Equal(new[] { "0", "0.625" }, rows.Select(row => row.TestValue));
        Assert.All(rows, row => Assert.Equal("pass", row.TestResult));
        var snapshot = Assert.IsType<MTFHVDynamicViewTestResult>(JsonConvert.DeserializeObject<MTFHVDynamicViewTestResult>(ctx.Result.ViewResultJson!));
        Assert.Equal("0.7F_MTF_HV_LeftUp", Assert.Single(snapshot.MTFDetailViewReslut.MTFResult!.resultChild).name);

        ctx.Result.Msg = "capture failed";
        Assert.False(await process.ExecuteFailure(ctx));
        Assert.Equal("capture failed", ctx.Result.Msg);
        Assert.False(ctx.ObjectiveTestResult.TotalResult);
        Assert.Empty(process.GetObjectiveCsvRows(ctx.Result));
        Assert.Equal("panel", Assert.Single(ctx.ObjectiveTestResult.DynamicTestResults).Key);
    }

    [Fact]
    public async Task DuplicateHvGroupPublishesTheLatestPairButCannotUndoAnEarlierLimitFailure()
    {
        using var files = new MtfFiles();
        files.Add("""{"resultChild":[{"name":"0.7F_MTF_HV_LeftUp","horizontalAverage":0.25,"verticalAverage":0.75}]}""");
        files.Add("""{"resultChild":[{"name":"leftup_0_7f","horizontalAverage":0.75,"verticalAverage":0.875}]}""");
        var process = files.CreateHvProcess();
        var ctx = CreateContext();

        Assert.True(await process.Execute(ctx));
        Assert.False(ctx.Result.Result);
        var items = ctx.ObjectiveTestResult.DynamicTestResults["Panel"];
        Assert.Equal(new[] { 0.75d, 0.875d }, items.Select(item => item.Value));
        Assert.All(process.GetObjectiveCsvRows(ctx.Result), row => Assert.Equal("pass", row.TestResult));
    }

    private static IProcessExecutionContext CreateContext() => new(_ => [])
    {
        Batch = new MeasureBatchModel { Id = 1 },
        Result = new ProjectARVRReuslt { Result = true },
        ObjectiveTestResult = new ObjectiveTestResult
        {
            DynamicTestResults = new()
            {
                ["Panel"] = [new ObjectiveTestItem { Name = "previous" }],
                ["panel"] = [new ObjectiveTestItem { Name = "unrelated" }],
            },
        },
    };

    private sealed class MtfFiles : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("CV-generic-mtf-");
        private readonly List<AlgResultMasterModel> masters = [];
        private readonly Dictionary<int, List<DetailCommonModel>> details = [];

        internal MTFProcess CreateProcess()
        {
            var process = new MTFProcess { ReadMasters = _ => masters, ReadDetails = id => details[id] };
            process.Config.Name = "Panel";
            return process;
        }

        internal MTFHVDynamicProcess CreateHvProcess()
        {
            var process = new MTFHVDynamicProcess { ReadMasters = _ => masters, ReadDetails = id => details[id] };
            process.Config.Name = " Panel ";
            return process;
        }

        internal void Add(string? contents, bool withDetail = true, string version = "2.0")
        {
            int id = masters.Count + 1;
            masters.Add(new AlgResultMasterModel
            {
                Id = id, ImgFileType = ViewResultAlgType.MTF, version = version,
                Params = """{"Algorithm":"StripeMtf","Parameters":{"PercentageDisplay":false}}""",
            });
            if (!withDetail)
            {
                details[id] = [];
                return;
            }
            string path = Path.Combine(directory.FullName, $"mtf-{id}.json");
            if (contents != null)
                File.WriteAllText(path, contents);
            details[id] = [new DetailCommonModel { PId = id, ResultJson = JsonConvert.SerializeObject(new ResultFile { ResultFileName = path }) }];
        }

        public void Dispose()
        {
            foreach (FileInfo file in directory.EnumerateFiles())
                file.Delete();
            directory.Delete();
        }
    }
}
