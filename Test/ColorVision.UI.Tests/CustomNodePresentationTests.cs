using ColorVision.Engine.FlowProcessing.Nodes;
using FlowEngineLib.Base;
using System.Reflection;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class CustomNodePresentationTests
{
    public static TheoryData<Type, string> ConciseNodeTitles => new()
    {
        { typeof(LocalBuildPoiNode), "关注点布点(Re)" },
        { typeof(LocalBuildPoiByTemplateNode), "关注点布点(参数)" },
        { typeof(LocalCalibrationNode), "校正" },
        { typeof(LocalCalibrationRealPoiNode), "校正+实时 POI" },
        { typeof(LocalCameraNode), "相机取图" },
        { typeof(LocalFindCrossNode), "十字定位" },
        { typeof(LocalFindLuminousAreaNode), "发光区定位" },
        { typeof(LocalFovNode), "FOV计算" },
        { typeof(LocalGridDistortionNode), "点阵畸变" },
        { typeof(LocalImageNode), "加载图片" },
        { typeof(LocalPoiNode), "POI" }
    };

    public static TheoryData<Type, string, string> LegacyNodeTitles => new()
    {
        { typeof(LocalImageNode), "本地图片", "加载图片" },
        { typeof(LocalCameraNode), "本地相机取图", "相机取图" },
        { typeof(LocalFindLuminousAreaNode), "本地发光区定位(V2)", "发光区定位" },
        { typeof(LocalFovNode), "本地FOV计算(V2)", "FOV计算" }
    };

    [Theory]
    [MemberData(nameof(ConciseNodeTitles))]
    public void CustomNodeUsesConciseDisplayName(Type nodeType, string expectedTitle)
    {
        CVCommonNode node = Assert.IsAssignableFrom<CVCommonNode>(Activator.CreateInstance(nodeType));

        Assert.Equal(expectedTitle, node.Title);
    }

    [Fact]
    public void SingleInputCustomNodesExposeUsefulCompactSummaries()
    {
        Assert.Equal("POI_Black", GetCompactSummaryValue(new LocalBuildPoiNode()));
        Assert.Equal("-", GetCompactSummaryValue(new LocalBuildPoiByTemplateNode()));
        Assert.Equal("-", GetCompactSummaryValue(new LocalCalibrationNode()));
        Assert.Equal("100 ms", GetCompactSummaryValue(new LocalCameraNode()));
        Assert.Equal("全图", GetCompactSummaryValue(new LocalFindCrossNode()));
        Assert.Equal("0.25", GetCompactSummaryValue(new LocalFindLuminousAreaNode()));
        Assert.Equal("9410", GetCompactSummaryValue(new LocalFovNode()));
        Assert.Equal("3×3", GetCompactSummaryValue(new LocalGridDistortionNode()));
        Assert.Equal("-", GetCompactSummaryValue(new LocalImageNode()));
        Assert.Equal("-", GetCompactSummaryValue(new LocalPoiNode()));
    }

    [Fact]
    public void ConfidenceFileAndFovSummariesReflectCurrentConfiguration()
    {
        LocalFindLuminousAreaNode luminousArea = new() { MinimumConfidence = 0.72 };
        LocalImageNode image = new() { ImageFileUrl = @"C:\images\sample.cvraw" };
        LocalFovNode fov = new() { FovDist = 8500, CameraDegrees = 68.5 };

        Assert.Equal("0.72", GetCompactSummaryValue(luminousArea));
        Assert.Equal("sample.cvraw", GetCompactSummaryValue(image));
        Assert.Equal(["8500", "68.5°"], GetCompactSummaryLines(fov));
    }

    [Theory]
    [MemberData(nameof(LegacyNodeTitles))]
    public void LoadingLegacyDefaultTitleUsesCurrentDisplayName(Type nodeType, string legacyTitle, string expectedTitle)
    {
        CVCommonNode node = Assert.IsAssignableFrom<CVCommonNode>(Activator.CreateInstance(nodeType));
        node.Create();

        node.OnLoadNode(new Dictionary<string, byte[]> { ["Title"] = Encoding.UTF8.GetBytes(legacyTitle) });

        Assert.Equal(expectedTitle, node.Title);
    }

    [Fact]
    public void LoadingUserDefinedTitlePreservesIt()
    {
        LocalFovNode node = new();
        node.Create();

        node.OnLoadNode(new Dictionary<string, byte[]> { ["Title"] = Encoding.UTF8.GetBytes("产线视场角") });

        Assert.Equal("产线视场角", node.Title);
    }

    [Fact]
    public void CompactSummaryUsesFreedRowOnlyForSingleInputNodes()
    {
        LocalCalibrationNode singleInput = new();
        LocalCalibrationRealPoiNode multipleInputs = new();
        singleInput.Create();
        multipleInputs.Create();

        Assert.True(ShouldDrawCompactSummary(singleInput));
        Assert.False(ShouldDrawCompactSummary(multipleInputs));
    }

    private static string GetCompactSummaryValue(CVCommonNode node)
    {
        MethodInfo method = node.GetType().GetMethod(
            "GetCompactSummaryValue",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{node.GetType().Name} does not expose a compact summary.");
        return Assert.IsType<string>(method.Invoke(node, null));
    }

    private static IReadOnlyList<string> GetCompactSummaryLines(LocalFlowNodeBase node)
    {
        MethodInfo method = node.GetType().GetMethod(
            "GetCompactSummaryLines",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"{node.GetType().Name} does not expose compact summary lines.");
        return Assert.IsAssignableFrom<IReadOnlyList<string>>(method.Invoke(node, null));
    }

    private static bool ShouldDrawCompactSummary(CVCommonNode node)
    {
        MethodInfo method = typeof(CVCommonNode).GetMethod(
            "ShouldDrawCompactSummary",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("CVCommonNode does not expose the compact-summary visibility rule.");
        return Assert.IsType<bool>(method.Invoke(node, null));
    }
}
