using ColorVision.Engine;
using ColorVision.Engine.Templates.Jsons;
using Newtonsoft.Json;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.ScreenDefects;
using SqlSugar;
using System.IO;

namespace ProjectARVRPro.Tests;

public sealed class DetectScreenDefectsProcessTests
{
    [Theory]
    [InlineData("defects")]
    [InlineData("empty details")]
    [InlineData("unavailable table")]
    public async Task ProductionReadsKeepTheOwnedDatabaseWhenSameIdsExistElsewhere(string mode)
    {
        DirectoryInfo files = Directory.CreateTempSubdirectory("cv-screen-defect-source-");
        const int batchId = 37;
        try
        {
            int original = Seed("original", mode, """{"DefectCount":2}""", width: 17);
            int replacement = Seed("replacement", "defects", CleanResult, width: 63);
            Assert.Equal(original, replacement);
            Func<SqlSugarClient> active = () => Open("original");
            var context = new IProcessExecutionContext(active)
            {
                Batch = new MeasureBatchModel { Id = batchId, Code = "duplicate-batch" },
                Result = new ProjectARVRReuslt { BatchId = batchId, Result = true },
                ObjectiveTestResult = new ObjectiveTestResult { TotalResult = true }
            };
            active = () => Open("replacement");
            using (var db = active())
                Assert.Equal(CleanResult, Assert.Single(db.Queryable<DetailCommonModel>().ToList()).ResultJson);

            var process = new DetectScreenDefectsProcess();
            process.Config.Name = "Screen";
            process.Config.TemplateName = "Inspection";
            bool completed = await process.Execute(context);

            Assert.False(context.Result.Result);
            Assert.False(context.ObjectiveTestResult.TotalResult);
            Assert.Equal("original.tif", context.Result.FileName);
            Assert.True(context.TryPopulateResultImageDimensions());
            Assert.Equal(17, context.Result.ImageWidth);
            Assert.Equal(11, context.Result.ImageHeight);
            if (mode == "defects")
            {
                Assert.True(completed);
                Assert.Equal(2, context.ObjectiveTestResult.DynamicScreenDefectResults["Screen"].DefectCount);
            }
            else
            {
                Assert.False(completed);
                Assert.Contains(mode == "empty details" ? "缺少明细" : "无法读取", context.Result.Msg);
                Assert.Empty(context.Result.ViewResultJson!);
                Assert.False(context.ObjectiveTestResult.DynamicScreenDefectResults.ContainsKey("Screen"));
            }
        }
        finally
        {
            foreach (FileInfo file in files.EnumerateFiles()) file.Delete();
            files.Delete();
        }

        SqlSugarClient Open(string name) => new(new ConnectionConfig
        {
            ConnectionString = $"Data Source={Path.Combine(files.FullName, name + ".db")};Pooling=False",
            DbType = DbType.Sqlite, IsAutoCloseConnection = true, InitKeyType = InitKeyType.Attribute
        });

        int Seed(string name, string scenario, string details, int width)
        {
            using var db = Open(name);
            db.CodeFirst.InitTables(typeof(MeasureResultImgModel), typeof(AlgResultMasterModel));
            string image = name + ".tif";
            db.Insertable(new MeasureResultImgModel
            {
                BatchId = batchId,
                FileUrl = image,
                ImgFrameInfo = JsonConvert.SerializeObject(new { width, height = 11 }),
                CreateDate = DateTime.Now
            }).ExecuteCommand();
            var master = Master(0);
            master.BatchId = batchId;
            master.ImgFile = image;
            master.CreateDate = DateTime.Now;
            int masterId = db.Insertable(master).ExecuteReturnIdentity();
            if (scenario != "unavailable table")
            {
                db.CodeFirst.InitTables<DetailCommonModel>();
                if (scenario == "defects")
                    db.Insertable(new DetailCommonModel { PId = masterId, ResultJson = details }).ExecuteCommand();
            }
            return masterId;
        }
    }

    [Fact]
    public async Task ExplicitlyUnavailableBatchDoesNotUseAnAmbientReader()
    {
        var context = new IProcessExecutionContext((Func<SqlSugarClient>?)null)
        {
            Batch = new MeasureBatchModel { Id = 37 },
            Result = new ProjectARVRReuslt { Result = true },
            ObjectiveTestResult = new ObjectiveTestResult { TotalResult = true }
        };

        Assert.False(await new DetectScreenDefectsProcess().Execute(context));

        Assert.Contains("缺少本轮流程的数据库连接", context.Result.Msg);
        Assert.False(context.Result.Result);
        Assert.False(context.ObjectiveTestResult.TotalResult);
    }

    [Theory]
    [InlineData(null, "结果内容为空")]
    [InlineData("{}", "DefectCount")]
    [InlineData("""{"AvgBrightness":120}""", "DefectCount")]
    [InlineData("""{"DefectCount":null}""", "DefectCount")]
    [InlineData("""{"DefectCount":-1}""", "DefectCount")]
    [InlineData("""{"DefectCount":false}""", "DefectCount")]
    [InlineData("""{"DefectCount":0.5}""", "DefectCount")]
    [InlineData("""{"DefectCount":2147483648}""", "DefectCount")]
    [InlineData("""{"DefectCount":0,"Defects":[{"type":"line"}]}""", "矛盾")]
    public async Task MissingOrInvalidCountDoesNotProduceCleanInspection(string? json, string expectedMessage)
    {
        var context = CreateContext();
        var process = CreateProcess([Master(1)], _ => [Detail(11, json)]);

        Assert.False(await process.Execute(context));

        AssertRejected(context, expectedMessage);
        Assert.Contains("结果 1 明细 11", context.Result.Msg);
    }

    [Fact]
    public async Task NoMatchingInspectionCannotPassUsingOtherAlgorithmOrTemplate()
    {
        var context = CreateContext();
        var otherTemplate = Master(23, "OtherTemplate");
        var otherAlgorithm = Master(15);
        otherAlgorithm.ImgFileType = ViewResultAlgType.POI_XYZ;
        var process = CreateProcess([otherTemplate, otherAlgorithm], _ => throw new InvalidOperationException("No matching result should be queried."));

        Assert.False(await process.Execute(context));

        AssertRejected(context, "缺少匹配");
        Assert.Equal("camera-preview.tif", context.Result.FileName);
    }

    [Fact]
    public async Task CorruptLatestDetailCannotReuseEarlierCleanInspection()
    {
        var context = CreateContext();
        var process = CreateProcess([Master(3), Master(9, "Screen_iNsPeCtIoN_Dark"), Master(12, "DifferentTemplate")], id =>
            id == 9
                ? [Detail(91, CleanResult), Detail(95, """{"DefectCount":""")]
                : throw new InvalidOperationException("An older or unmatched inspection should not be queried."));

        Assert.False(await process.Execute(context));

        AssertRejected(context, "结果 9 明细 95");
        Assert.Equal("inspection-9.tif", context.Result.FileName);
    }

    [Theory]
    [InlineData(false, "缺少明细")]
    [InlineData(true, "无法读取")]
    public async Task LatestMasterWithoutUsableDetailsDoesNotReuseOlderCleanMaster(bool lookupFailed, string expectedMessage)
    {
        var context = CreateContext();
        var process = CreateProcess([Master(7), Master(4)], id => id == 7 ? lookupFailed ? null : [] : [Detail(41, CleanResult)]);

        Assert.False(await process.Execute(context));

        AssertRejected(context, expectedMessage);
        Assert.Equal("inspection-7.tif", context.Result.FileName);
    }

    [Theory]
    [InlineData(CleanResult, 0, true, 0)]
    [InlineData("""{"defectcount":"0","Defects":null}""", 0, true, 0)]
    [InlineData("""{"DefectCount":2}""", 2, false, 0)]
    [InlineData("""{"DefectCount":2,"Defects":[{"type":"line","x":1,"y":2,"width":3,"height":4}]}""", 2, false, 1)]
    public async Task MeasuredCountKeepsSummaryAndOptionalOverlay(string json, int count, bool passed, int boxes)
    {
        var context = CreateContext();
        var process = CreateProcess([Master(1)], _ => [Detail(11, json)]);

        Assert.True(await process.Execute(context));

        Assert.Equal(passed, context.Result.Result);
        Assert.Equal(passed, context.ObjectiveTestResult.TotalResult);
        var output = context.ObjectiveTestResult.DynamicScreenDefectResults["Screen"];
        Assert.Equal(count, output.DefectCount);
        Assert.Equal(boxes, output.Defects.Count);
        if (boxes > 0)
        {
            var box = Assert.Single(output.Defects);
            Assert.Equal(1, box.Id);
            Assert.Equal("line", box.Type);
        }
        var snapshot = JsonConvert.DeserializeObject<ScreenDefectsData>(context.Result.ViewResultJson!);
        Assert.Equal(count, snapshot!.DefectCount);
    }

    [Fact]
    public async Task ReportPreservesTimestampTextAndMeasurementMetadata()
    {
        const string timestamp = "2026-08-01T13:15:47.123+08:00";
        const string json = """{"DefectCount":0,"AvgBrightness":92.3,"GradeLevel":"A","TimeStamp":"2026-08-01T13:15:47.123+08:00"}""";
        var context = CreateContext();
        var process = CreateProcess([Master(1)], _ => [Detail(11, json)]);

        Assert.True(await process.Execute(context));

        var snapshot = JsonConvert.DeserializeObject<ScreenDefectsData>(context.Result.ViewResultJson!);
        Assert.Equal(timestamp, snapshot!.TimeStamp);
        Assert.Equal(92.3, snapshot.AvgBrightness);
        Assert.Equal("A", snapshot.GradeLevel);
    }

    [Fact]
    public async Task FileBackedZeroPassesAndMissingFileFails()
    {
        var directory = Directory.CreateTempSubdirectory("CV-screen-defects-");
        string path = Path.Combine(directory.FullName, "result.json");
        try
        {
            var context = CreateContext();
            string pointer = JsonConvert.SerializeObject(new ResultFile { ResultFileName = path });
            var process = CreateProcess([Master(1)], _ => [Detail(11, pointer)]);

            Assert.False(await process.Execute(context));
            AssertRejected(context, "结果文件");

            File.WriteAllText(path, CleanResult);
            context = CreateContext();
            Assert.True(await process.Execute(context));
            Assert.True(context.Result.Result);
            Assert.Equal(0, context.ObjectiveTestResult.DynamicScreenDefectResults["Screen"].DefectCount);
        }
        finally
        {
            File.Delete(path);
            directory.Delete();
        }
    }

    [Fact]
    public async Task FlowFailureClearsOnlyOwnResultAndKeepsFailureMessage()
    {
        var context = CreateContext();
        context.Result.Msg = "检测服务不可用";
        var process = CreateProcess([], _ => throw new InvalidOperationException());

        Assert.False(await process.ExecuteFailure(context));

        AssertRejected(context, "检测服务不可用");
    }

    private const string CleanResult = """{"DefectCount":0,"Defects":[]}""";

    private static DetectScreenDefectsProcess CreateProcess(List<AlgResultMasterModel> masters, Func<int, List<DetailCommonModel>?> details)
    {
        var process = new DetectScreenDefectsProcess { ReadMasters = _ => masters, ReadDetails = details };
        process.Config.Name = " Screen ";
        process.Config.TemplateName = "Inspection";
        return process;
    }

    private static AlgResultMasterModel Master(int id, string template = "Screen_Inspection") => new()
    {
        Id = id,
        TName = template,
        ImgFile = $"inspection-{id}.tif",
        ImgFileType = ViewResultAlgType.ARVR_DetectScreenDefects,
    };

    private static DetailCommonModel Detail(int id, string? json) => new() { Id = id, ResultJson = json! };

    private static IProcessExecutionContext CreateContext() => new(_ => [new MeasureResultImgModel { FileUrl = "camera-preview.tif" }])
    {
        Batch = new MeasureBatchModel { Id = 1 },
        Result = new ProjectARVRReuslt { ViewResultJson = "old clean inspection" },
        ObjectiveTestResult = new ObjectiveTestResult
        {
            DynamicScreenDefectResults = new()
            {
                ["Screen"] = new(),
                ["Other"] = new() { DefectCount = 1 },
            },
        },
    };

    private static void AssertRejected(IProcessExecutionContext context, string reason)
    {
        Assert.False(context.Result.Result);
        Assert.False(context.ObjectiveTestResult.TotalResult);
        Assert.Contains(reason, context.Result.Msg);
        Assert.Empty(context.Result.ViewResultJson!);
        Assert.False(context.ObjectiveTestResult.DynamicScreenDefectResults.ContainsKey("Screen"));
        Assert.Equal(1, context.ObjectiveTestResult.DynamicScreenDefectResults["Other"].DefectCount);
    }
}
