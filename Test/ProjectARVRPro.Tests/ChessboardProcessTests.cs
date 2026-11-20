using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Newtonsoft.Json;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Chessboard;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class ChessboardProcessTests
{
    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, """{"y":0.1}""")]
    [InlineData(true, """{"Y":null}""")]
    [InlineData(false, """{"Y":false}""")]
    [InlineData(true, """{"Y":"NaN"}""")]
    public async Task MissingDarkMeasurementCannotRaiseLocalContrast(bool keyed, string? darkMeasurement)
    {
        using var inputs = new ChessboardInputs(darkMeasurement);
        var context = CreateContext();

        Assert.False(await inputs.CreateProcess(keyed).Execute(context));

        AssertRejected(context, keyed, "亮度 Y");
        Assert.Contains("first-dark", context.Result.Msg);
    }

    [Theory]
    [InlineData(false, """{"Y":3.5}""", false)]
    [InlineData(true, """{"Y":3.5}""", false)]
    [InlineData(false, """{"Y":0}""", true)]
    [InlineData(true, """{"Y":"0"}""", true)]
    public async Task CompleteLocalMeasurementsUseRecipeIncludingMeasuredZero(bool keyed, string darkMeasurement, bool expectedPass)
    {
        using var inputs = new ChessboardInputs(darkMeasurement);
        var context = CreateContext();
        IProcess process = inputs.CreateProcess(keyed);

        Assert.True(await process.Execute(context));

        var result = GetMeasurement(context, keyed);
        Assert.Equal(expectedPass, context.Result.Result);
        Assert.Equal(expectedPass, result.ChessboardContrast!.TestResult);
        Assert.Equal(50, result.ChessboardContrast.LowLimit);
        Assert.NotNull(result.AverageBlackLuminance);
        if (!expectedPass)
            Assert.Equal(100 / 3.5, result.ChessboardContrast.Value, 10);
        Assert.Contains(process.GetObjectiveCsvRows(context.Result), row => row.TestItem == "Chessboard_Contrast");
    }

    [Theory]
    [InlineData(false, """{"result":{"Content":"Chessboard_Contrast"}}""")]
    [InlineData(true, """{"result":{"Content":"Chessboard_Contrast"}}""")]
    [InlineData(false, """{"result":{"Value":null}}""")]
    [InlineData(true, """{"result":{"Value":false}}""")]
    [InlineData(false, """{"result":{"Value":"Infinity"}}""")]
    public async Task MatchingTemplateWithMissingValueCannotPassThroughCorrectionOrLocalFallback(bool keyed, string template)
    {
        using var inputs = new ChessboardInputs("""{"Y":0}""", template);
        var context = CreateContext();

        Assert.False(await inputs.CreateProcess(keyed, correction: true).Execute(context));

        AssertRejected(context, keyed, "Value");
    }

    [Theory]
    [InlineData(false, """{"result":{"Value":0}}""", 60)]
    [InlineData(true, """{"RESULT":{"value":"12.5"}}""", 85)]
    public async Task MeasuredTemplateWinsEvenWhenOptionalPoiIsIncomplete(bool keyed, string template, double expected)
    {
        using var inputs = new ChessboardInputs(null, template);
        var context = CreateContext();

        Assert.True(await inputs.CreateProcess(keyed, correction: true).Execute(context));

        Assert.True(context.Result.Result);
        var result = GetMeasurement(context, keyed);
        Assert.Equal(expected, result.ChessboardContrast!.Value);
        Assert.Null(result.AverageBlackLuminance);
        string snapshot = Assert.IsType<string>(context.Result.ViewResultJson);
        var view = keyed
            ? JsonConvert.DeserializeObject<ChessboardDynamicViewTestResult>(snapshot)!.ChessboardViewTestResult
            : JsonConvert.DeserializeObject<ChessboardViewTestResult>(snapshot)!;
        Assert.Equal(3, view.PoixyuvDatas.Count);
        Assert.DoesNotContain(view.PoixyuvDatas, poi => poi.Name == "first-dark");
    }

    [Fact]
    public async Task RejectedDefaultKeyRemovesOnlyItsOwnLegacyProjection()
    {
        using var inputs = new ChessboardInputs(null);
        IProcess process = inputs.CreateProcess(keyed: true, outputKey: " chessboard ");
        var context = CreateContext();
        var previous = new ChessboardTestResult();
        context.ObjectiveTestResult.ChessboardTestResults["Chessboard"] = previous;
        context.ObjectiveTestResult.ChessboardTestResult = previous;

        Assert.False(await process.Execute(context));

        Assert.False(context.ObjectiveTestResult.ChessboardTestResults.ContainsKey("Chessboard"));
        Assert.Null(context.ObjectiveTestResult.ChessboardTestResult);

        var independent = new ChessboardTestResult();
        context.ObjectiveTestResult.ChessboardTestResults["Chessboard"] = previous;
        context.ObjectiveTestResult.ChessboardTestResult = independent;

        Assert.False(await process.Execute(context));

        Assert.False(context.ObjectiveTestResult.ChessboardTestResults.ContainsKey("Chessboard"));
        Assert.Same(independent, context.ObjectiveTestResult.ChessboardTestResult);
    }

    private static IProcessExecutionContext CreateContext() => new(_ => [])
    {
        Batch = new MeasureBatchModel { Id = 1 },
        Result = new ProjectARVRReuslt { ViewResultJson = "stale result" },
        ObjectiveTestResult = new ObjectiveTestResult
        {
            ChessboardTestResult = new(),
            ChessboardTestResults = new()
            {
                ["near"] = new(),
                ["Far"] = new(),
            },
        },
    };

    private static ChessboardTestResult GetMeasurement(IProcessExecutionContext context, bool keyed) => keyed
        ? context.ObjectiveTestResult.ChessboardTestResults["near"]
        : context.ObjectiveTestResult.ChessboardTestResult;

    private static void AssertRejected(IProcessExecutionContext context, bool keyed, string reason)
    {
        Assert.False(context.Result.Result);
        Assert.False(context.ObjectiveTestResult.TotalResult);
        Assert.Empty(context.Result.ViewResultJson!);
        Assert.Contains(reason, context.Result.Msg);
        Assert.Contains("Far", context.ObjectiveTestResult.ChessboardTestResults.Keys);
        Assert.Equal(!keyed, context.ObjectiveTestResult.ChessboardTestResults.ContainsKey("near"));
        if (keyed)
            Assert.NotNull(context.ObjectiveTestResult.ChessboardTestResult);
        else
            Assert.Null(context.ObjectiveTestResult.ChessboardTestResult);
    }

    private sealed class ChessboardInputs : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("CV-chessboard-process-");
        private readonly List<AlgResultMasterModel> masters = [new() { Id = 1, ImgFileType = ViewResultAlgType.POI_XYZ }];
        private readonly List<PoiPointResultModel> points;
        private readonly List<DetailCommonModel> details = [];

        internal ChessboardInputs(string? darkMeasurement, string? template = null)
        {
            points =
            [
                Point("first-dark", 10, 10, darkMeasurement),
                Point("first-bright", 20, 10, """{"Y":100}"""),
                Point("second-bright", 10, 20, """{"Y":100}"""),
                Point("second-dark", 20, 20, """{"Y":3.5}"""),
            ];
            if (template == null) return;
            masters.Add(new AlgResultMasterModel { Id = 2, ImgFileType = ViewResultAlgType.PoiAnalysis, TName = "Chessboard_Contrast" });
            string path = Path.Combine(directory.FullName, "contrast.json");
            File.WriteAllText(path, template);
            details.Add(new DetailCommonModel { PId = 2, ResultJson = JsonConvert.SerializeObject(new ResultFile { ResultFileName = path }) });
        }

        internal IProcess CreateProcess(bool keyed, bool correction = false, string outputKey = "Near")
        {
            var batch = new ChessboardBatchResults
            {
                ReadMasters = _ => masters,
                ReadPoints = _ => points,
                ReadDetails = _ => details,
            };
            if (!keyed)
            {
                var fixedProcess = new ChessboardProcess { BatchResults = batch };
                fixedProcess.Config.SaveCsv = false;
                ConfigureRecipe(fixedProcess.Config.RecipeConfig, correction);
                return fixedProcess;
            }

            var keyedProcess = new ChessboardDynamicProcess { BatchResults = batch };
            keyedProcess.Config.Key = outputKey;
            keyedProcess.Config.SaveCsv = false;
            ConfigureRecipe(keyedProcess.Config.RecipeConfig, correction);
            return keyedProcess;
        }

        private static PoiPointResultModel Point(string name, int x, int y, string? measurement) => new()
        {
            PoiName = name,
            PoiX = x,
            PoiY = y,
            PoiWidth = 3,
            PoiHeight = 3,
            Value = measurement,
        };

        private static void ConfigureRecipe(ChessboardRecipeConfig recipe, bool correction)
        {
            if (!correction) return;
            recipe.ChessboardContrast.Fix = 2;
            recipe.ChessboardContrast.B = 60;
        }

        public void Dispose()
        {
            foreach (FileInfo file in directory.EnumerateFiles())
                file.Delete();
            directory.Delete();
        }
    }
}
