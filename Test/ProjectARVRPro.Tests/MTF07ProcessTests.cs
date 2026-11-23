using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using ColorVision.Engine.Templates.Jsons.MTF2;
using Newtonsoft.Json;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.MTF.MTF07;
using ProjectARVRPro.Process.MTF.MTF07.MTFH;
using ProjectARVRPro.Process.MTF.MTF07.MTFV;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class MTF07ProcessTests
{
    [Theory]
    [InlineData("no-result", "MTF_H_Center_0F")]
    [InlineData("missing-corner", "MTF_H_RightDown_0_7F")]
    [InlineData("missing-file", "MTF_H_RightDown_0_7F")]
    [InlineData("null", "RightDown_0.7F_H")]
    [InlineData("nonfinite", "RightDown_0.7F_H")]
    public async Task IncompleteMeasurementsFailAndRemoveOnlyThisOutput(string failure, string missingPoint)
    {
        using var source = new MeasurementFiles();
        MTFItem[] points = CreatePoints("H", "Center_0F_H");
        if (failure != "no-result")
            source.Add(1, points[..^1]);
        if (failure == "missing-file")
            source.Add(2, points[^1..], writeFile: false);
        else if (failure is "null" or "nonfinite")
        {
            points[^1].mtfValue = failure == "null" ? null : double.NaN;
            source.Add(2, points[^1..]);
        }

        var process = new MTFH07Process { ReadMasters = source.ReadMasters, ReadDetails = source.ReadDetails };
        process.Config.Key = "Panel";
        var ctx = CreateContext();
        ctx.Result.ViewResultJson = "previous snapshot";
        ctx.ObjectiveTestResult.MTFH07TestResults["panel"] = new();
        ctx.ObjectiveTestResult.MTFH07TestResults["Other H"] = new();
        ctx.ObjectiveTestResult.MTFV07TestResults["Panel"] = new();

        Assert.False(await process.Execute(ctx));

        Assert.False(ctx.Result.Result);
        Assert.False(ctx.ObjectiveTestResult.TotalResult);
        Assert.Equal(string.Empty, ctx.Result.ViewResultJson);
        Assert.Contains("[Panel]", ctx.Result.Msg);
        Assert.Contains(missingPoint, ctx.Result.Msg);
        Assert.Empty(process.GetObjectiveCsvRows(ctx.Result));
        Assert.Equal("Other H", Assert.Single(ctx.ObjectiveTestResult.MTFH07TestResults).Key);
        Assert.Equal("Panel", Assert.Single(ctx.ObjectiveTestResult.MTFV07TestResults).Key);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FiveActualMeasurementsAcrossMastersPreserveZeroAndRecipe(bool vertical)
    {
        using var source = new MeasurementFiles();
        string direction = vertical ? "V" : "H";
        MTFItem[] points = CreatePoints(direction, $"center_custom_{direction.ToLowerInvariant()}");
        // A second master supplies the last two points. An unrelated point with no value is ignored.
        source.Add(1, [.. points[..3], new MTFItem { name = "another picture" }]);
        source.Add(2, points[3..]);
        IProcess process;
        if (vertical)
        {
            var v = new MTFV07Process { ReadMasters = source.ReadMasters, ReadDetails = source.ReadDetails };
            v.Config.Key = "Panel";
            v.Config.Key_Center_0F = " Center_Custom_V ";
            v.Config.RecipeConfig.MTF_V_RightDown_0_7F.B = 0.1;
            process = v;
        }
        else
        {
            var h = new MTFH07Process { ReadMasters = source.ReadMasters, ReadDetails = source.ReadDetails };
            h.Config.Key = "Panel";
            h.Config.Key_Center_0F = " Center_Custom_H ";
            h.Config.RecipeConfig.MTF_H_Center_0F.Min = 0;
            h.Config.RecipeConfig.MTF_H_RightDown_0_7F.B = 0.1;
            process = h;
        }

        var ctx = CreateContext();
        Assert.True(await process.Execute(ctx));
        Assert.Equal(!vertical, ctx.Result.Result);

        IMTF07TestResult stored = vertical
            ? ctx.ObjectiveTestResult.MTFV07TestResults["Panel"]
            : ctx.ObjectiveTestResult.MTFH07TestResults["Panel"];
        Assert.Equal(5, stored.Items.Count);
        Assert.Equal(0, stored.Items[0].Value);
        Assert.NotNull(stored.Items[0].TestValue);
        Assert.Equal(0.8, stored.Items[^1].Value, 10);
        Assert.Equal(5, process.GetObjectiveCsvRows(ctx.Result).Count);

        ctx.Result.Msg = "capture failed";
        Assert.False(await process.ExecuteFailure(ctx));
        Assert.Equal("capture failed", ctx.Result.Msg);
        Assert.False(ctx.Result.Result);
        Assert.False(ctx.ObjectiveTestResult.TotalResult);
        Assert.Empty(process.GetObjectiveCsvRows(ctx.Result));
        Assert.Empty(ctx.ObjectiveTestResult.MTFH07TestResults);
        Assert.Empty(ctx.ObjectiveTestResult.MTFV07TestResults);
    }

    private static IProcessExecutionContext CreateContext() => new(_ => [])
    {
        Batch = new MeasureBatchModel { Id = 41 },
        Result = new ProjectARVRReuslt { Result = true },
        ObjectiveTestResult = new ObjectiveTestResult(),
    };

    private static MTFItem[] CreatePoints(string direction, string center) =>
    [
        new() { name = center, mtfValue = 0 },
        new() { name = $"LeftUp_0.7F_{direction}", mtfValue = 0.7 },
        new() { name = $"RightUp_0.7F_{direction}", mtfValue = 0.7 },
        new() { name = $"LeftDown_0.7F_{direction}", mtfValue = 0.7 },
        new() { name = $"RightDown_0.7F_{direction}", mtfValue = 0.7 },
    ];

    private sealed class MeasurementFiles : IDisposable
    {
        private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("CV-mtf07-");
        private readonly List<AlgResultMasterModel> masters = [];
        private readonly Dictionary<int, DetailCommonModel> details = [];

        internal void Add(int id, MTFItem[] points, bool writeFile = true)
        {
            string resultFile = Path.Combine(directory.FullName, $"mtf-{id}.json");
            if (writeFile)
                File.WriteAllText(resultFile, JsonConvert.SerializeObject(new MTFResult { result = [.. points] }));

            masters.Add(new AlgResultMasterModel
            {
                Id = id, BatchId = 41, ImgFileType = ViewResultAlgType.MTF, version = "2.0",
                Params = """{"Algorithm":"StripeMtf","Parameters":{"PercentageDisplay":false}}""",
            });
            details.Add(id, new DetailCommonModel
            {
                PId = id, ResultJson = JsonConvert.SerializeObject(new ResultFile { ResultFileName = resultFile }),
            });
        }

        internal List<AlgResultMasterModel> ReadMasters(int batchId) => masters.Where(master => master.BatchId == batchId).ToList();
        internal List<DetailCommonModel> ReadDetails(int masterId) => [details[masterId]];

        public void Dispose()
        {
            foreach (FileInfo file in directory.EnumerateFiles())
                file.Delete();
            directory.Delete();
        }
    }
}
