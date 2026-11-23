using ColorVision.Engine;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Black;
using ProjectARVRPro.Process.W255;

namespace ProjectARVRPro.Tests;

public sealed class BlackProcessTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FofoUsesConfiguredCenterRegardlessOfPointOrder(bool centerFirst)
    {
        var corner = CreatePoint("corner", 1d / 2048);
        var center = CreatePoint("black-center", 1d / 512);
        var process = centerFirst ? CreateProcess(center, corner) : CreateProcess(corner, center);
        process.Config.Key_Center = "Black-Center";
        var ctx = CreateContext();

        Assert.True(await process.Execute(ctx));
        Assert.False(ctx.Result.Result);
        var contrast = ctx.ObjectiveTestResult.BlackTestResult.FOFOContrast;
        Assert.Equal(51200, contrast.Value);
        Assert.Equal(100000, contrast.LowLimit);
        Assert.False(contrast.TestResult);
        var row = Assert.Single(process.GetObjectiveCsvRows(ctx.Result), item => item.TestItem == "FOFOContrast");
        Assert.Equal("51200", row.TestValue);
        Assert.Equal("fail", row.TestResult);
    }

    [Theory]
    [InlineData(false, null, "缺少已配置黑场中心点")]
    [InlineData(true, null, "缺少有限的实测亮度")]
    [InlineData(true, """{"X":1,"y":0.3,"Z":1}""", "缺少有限的实测亮度")]
    public async Task MissingConfiguredCenterLuminanceFailsAndClearsOnlyBlackResult(bool hasCenter, string? payload, string reason)
    {
        var corner = CreatePoint("corner", 1d / 2048);
        var process = hasCenter ? CreateProcess(corner, new PoiPointResultModel { PoiName = "P_5", Value = payload }) : CreateProcess(corner);
        var ctx = CreateContext();
        var white = ctx.ObjectiveTestResult.W255TestResult;
        ctx.ObjectiveTestResult.BlackTestResult = new();
        ctx.Result.ViewResultJson = "old contrast";

        Assert.False(await process.Execute(ctx));
        Assert.False(ctx.Result.Result);
        Assert.False(ctx.ObjectiveTestResult.TotalResult);
        Assert.Contains(reason, ctx.Result.Msg);
        Assert.Contains("P_5", ctx.Result.Msg);
        Assert.Equal(string.Empty, ctx.Result.ViewResultJson);
        Assert.Empty(process.GetObjectiveCsvRows(ctx.Result));
        Assert.Null(ctx.ObjectiveTestResult.BlackTestResult);
        Assert.Same(white, ctx.ObjectiveTestResult.W255TestResult);
    }

    [Fact]
    public async Task ExplicitZeroPreservesExistingCieTreatmentAndRecipe()
    {
        var center = CreatePoint("P_5", 0);
        var process = CreateProcess(CreatePoint("corner", 1d / 512), center);
        double displayedLuminance = new PoiResultCIExyuvData(center).Y;
        process.Config.RecipeConfig.FOFOContrast.Fix = 2;
        var ctx = CreateContext();

        Assert.True(await process.Execute(ctx));
        Assert.Equal(2 * (100 / displayedLuminance), ctx.ObjectiveTestResult.BlackTestResult.FOFOContrast.Value);
    }

    [Fact]
    public async Task SummedModeUsesAllPointsAndStandaloneBlackStillPreservesTheMeasurement()
    {
        var points = new[] { CreatePoint("corner-a", 1d / 2048), CreatePoint("corner-b", 1d / 512) };
        var summed = CreateProcess(points);
        summed.Config.IsUsingNing = true;
        summed.Config.RecipeConfig.FOFOContrast.Min = 75000;
        var ctx = CreateContext();

        Assert.True(await summed.Execute(ctx));
        Assert.True(ctx.Result.Result);
        Assert.Equal(81920, ctx.ObjectiveTestResult.BlackTestResult.FOFOContrast.Value);

        var standalone = CreateProcess(points);
        ctx = CreateContext();
        ctx.ObjectiveTestResult.W255TestResult = null!;
        Assert.True(await standalone.Execute(ctx));
        Assert.Equal(2, ctx.ObjectiveTestResult.BlackTestResult.PoixyuvDatas.Count);
    }

    private static BlackProcess CreateProcess(params PoiPointResultModel[] points) => new()
    {
        ReadMasters = _ => [new AlgResultMasterModel { Id = 2, ImgFileType = ViewResultAlgType.POI_XYZ }],
        ReadPoints = _ => [.. points],
    };

    private static IProcessExecutionContext CreateContext() => new(_ => [])
    {
        Batch = new MeasureBatchModel { Id = 1 },
        Result = new ProjectARVRReuslt { Result = true },
        ObjectiveTestResult = new ObjectiveTestResult
        {
            W255TestResult = new W255TestResult
            {
                CenterLunimance = new ObjectiveTestItem { Value = 100 },
                PoixyuvDatas = [new PoixyuvData { Y = 75 }, new PoixyuvData { Y = 125 }],
            },
        },
    };

    private static PoiPointResultModel CreatePoint(string name, double luminance) => new()
    {
        PoiName = name,
        Value = FormattableString.Invariant($$"""{"X":1,"Y":"{{luminance}}","Z":1,"x":0.32,"y":0.34,"u":0.21,"v":0.47,"CCT":6500}"""),
    };
}
