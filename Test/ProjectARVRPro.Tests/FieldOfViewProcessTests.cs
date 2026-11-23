using ColorVision.Engine;
using ColorVision.Engine.Services.Results.LightArea;
using ColorVision.Engine.Templates.Jsons;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.KeyedResults;
using ProjectARVRPro.Process.KeyedResults.FieldOfView;
using ProjectARVRPro.Process.W51;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class FieldOfViewProcessTests
{
    private const string CompleteResult = """
        {"Result":{"D_Fov":30,"clolorVisionH_Fov":0,"ClolorVisionV_Fov":"22","H_Fov":99,"V_FOV":88}}
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SavedDirectionalAnglesPreserveMeasuredZeroAndExistingOutput(bool keyed)
    {
        using var files = new FovFiles();
        files.Add(CompleteResult);
        var ctx = CreateContext();
        IProcess process;
        if (keyed)
        {
            var fieldOfView = new FieldOfViewProcess { BatchResults = files.Source };
            fieldOfView.Config.RecipeConfig.HorizontalFieldOfViewAngle.Min = 0;
            fieldOfView.Config.RecipeConfig.DiagonalFieldOfViewAngle.Min = 29;
            fieldOfView.Config.RecipeConfig.DiagonalFieldOfViewAngle.Max = 31;
            fieldOfView.Config.RecipeConfig.VerticalFieldOfViewAngle.B = 0.25;
            process = fieldOfView;
        }
        else
        {
            var white51 = new White51Process { BatchResults = files.Source };
            white51.Config.RecipeConfig.HorizontalFieldOfViewAngle.Min = 0;
            white51.Config.RecipeConfig.VerticalFieldOfViewAngle.Min = 21;
            white51.Config.RecipeConfig.VerticalFieldOfViewAngle.Max = 23;
            white51.Config.RecipeConfig.VerticalFieldOfViewAngle.B = 0.25;
            process = white51;
        }

        Assert.True(await process.Execute(ctx));
        Assert.True(ctx.Result.Result);
        Assert.Equal(3, process.GetObjectiveCsvRows(ctx.Result).Count);
        var snapshot = JObject.Parse(Assert.IsType<string>(ctx.Result.ViewResultJson));
        Assert.Single(Assert.IsType<JArray>(snapshot["AlgResultLightAreaModels"]));

        W51TestResult result = ctx.ObjectiveTestResult.W51TestResult;
        Assert.Equal(0, result.HorizontalFieldOfViewAngle.Value);
        Assert.NotNull(result.HorizontalFieldOfViewAngle.TestValue);
        Assert.Equal(22.25, result.VerticalFieldOfViewAngle.Value);
        Assert.Equal(30, result.DiagonalFieldOfViewAngle.Value);
        if (keyed)
            Assert.Same(result.HorizontalFieldOfViewAngle, ctx.ObjectiveTestResult.FieldOfViewTestResults["White"].HorizontalFieldOfViewAngle);
        else
            Assert.Empty(ctx.ObjectiveTestResult.FieldOfViewTestResults);
    }

    [Theory]
    [InlineData(false, "master-only", "水平、垂直和对角")]
    [InlineData(true, "master-only", "水平、垂直和对角")]
    [InlineData(false, "missing-directional", "ClolorVisionH_Fov")]
    [InlineData(true, "missing-directional", "ClolorVisionH_Fov")]
    [InlineData(false, "nonfinite", "ClolorVisionV_Fov")]
    [InlineData(true, "boolean", "D_Fov")]
    public async Task IncompleteAnglesFailAndClearOnlyTheFailedOutput(bool keyed, string failure, string expectedError)
    {
        using var files = new FovFiles();
        if (failure != "master-only")
            files.Add(CompleteResult);
        files.Add(failure switch
        {
            "missing-directional" => """{"result":{"D_Fov":30,"H_Fov":24,"V_FOV":22,"clolorVisionV_Fov":22}}""",
            "nonfinite" => """{"result":{"D_Fov":30,"clolorVisionH_Fov":24,"clolorVisionV_Fov":"NaN"}}""",
            "boolean" => """{"result":{"D_Fov":true,"clolorVisionH_Fov":24,"clolorVisionV_Fov":22}}""",
            _ => null,
        });

        IProcess process = keyed ? new FieldOfViewProcess { BatchResults = files.Source } : new White51Process { BatchResults = files.Source };
        var ctx = CreateContext();
        ctx.Result.ViewResultJson = "previous snapshot";
        ctx.ObjectiveTestResult.FieldOfViewTestResults["Other"] = new();
        if (keyed)
            KeyedTestResultWriter.Write(ctx.ObjectiveTestResult, "white", new FieldOfViewTestResult());
        else
            ctx.ObjectiveTestResult.W51TestResult = new();

        Assert.False(await process.Execute(ctx));
        Assert.False(ctx.Result.Result);
        Assert.False(ctx.ObjectiveTestResult.TotalResult);
        Assert.Equal(string.Empty, ctx.Result.ViewResultJson);
        Assert.Contains(expectedError, ctx.Result.Msg);
        Assert.Empty(process.GetObjectiveCsvRows(ctx.Result));
        Assert.Null(ctx.ObjectiveTestResult.W51TestResult);
        Assert.Equal("Other", Assert.Single(ctx.ObjectiveTestResult.FieldOfViewTestResults).Key);
    }

    [Fact]
    public async Task FailurePreservesOtherAuthorsOfTheCompatibilityResult()
    {
        var ctx = CreateContext();
        var keyedWhite = new FieldOfViewTestResult();
        KeyedTestResultWriter.Write(ctx.ObjectiveTestResult, "White", keyedWhite);
        var standalone = new W51TestResult();
        ctx.ObjectiveTestResult.W51TestResult = standalone;

        var keyed = new FieldOfViewProcess();
        keyed.Config.Key = "Panel";
        KeyedTestResultWriter.Write(ctx.ObjectiveTestResult, "panel", new FieldOfViewTestResult());
        Assert.False(await keyed.ExecuteFailure(ctx));
        Assert.Same(standalone, ctx.ObjectiveTestResult.W51TestResult);
        Assert.Same(keyedWhite, Assert.Single(ctx.ObjectiveTestResult.FieldOfViewTestResults).Value);

        var white51 = new White51Process();
        Assert.False(await white51.ExecuteFailure(ctx));
        Assert.Same(keyedWhite.HorizontalFieldOfViewAngle, ctx.ObjectiveTestResult.W51TestResult.HorizontalFieldOfViewAngle);

        ctx.ObjectiveTestResult.W51TestResult = standalone;
        keyed.Config.Key = "White";
        Assert.False(await keyed.ExecuteFailure(ctx));
        Assert.Same(standalone, ctx.ObjectiveTestResult.W51TestResult);
        Assert.Empty(ctx.ObjectiveTestResult.FieldOfViewTestResults);
    }

    private static IProcessExecutionContext CreateContext() => new(_ => [])
    {
        Batch = new MeasureBatchModel { Id = 31 },
        Result = new ProjectARVRReuslt { Result = true },
        ObjectiveTestResult = new ObjectiveTestResult(),
    };

    private sealed class FovFiles : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("CV-fov-data-");
        private readonly List<AlgResultMasterModel> masters = [new() { Id = 1, ImgFileType = ViewResultAlgType.FindLightArea }];
        private readonly Dictionary<int, List<DetailCommonModel>> details = [];

        internal FovBatchResults Source => new()
        {
            ReadMasters = _ => masters,
            ReadDetails = masterId => details[masterId],
            ReadLightAreas = _ => [new AlgResultLightAreaModel()],
        };

        internal void Add(string? json)
        {
            int id = masters.Count + 1;
            masters.Add(new AlgResultMasterModel { Id = id, ImgFileType = ViewResultAlgType.FOV, ResultCode = json == null ? -1 : 0 });
            if (json == null)
            {
                details[id] = [];
                return;
            }

            string path = Path.Combine(directory.FullName, $"fov-{id}.json");
            File.WriteAllText(path, json);
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
