using ColorVision.Core;
using ColorVision.ImageEditor;
using ManualFindLuminousArea = ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.FindLuminousArea.FindLuminousArea;

namespace ColorVision.UI.Tests;

public sealed class FindLuminousAreaManualResultTests
{
    [Fact]
    public void ResultMessageIncludesDetailedManualDiagnostics()
    {
        LuminousAreaDetectionResult result = new(
            true,
            "RobustV2",
            [
                new LuminousAreaPoint(3745, 2695),
                new LuminousAreaPoint(6017, 2723),
                new LuminousAreaPoint(6027, 3917),
                new LuminousAreaPoint(3707, 3881)
            ],
            0.983,
            [new LuminousAreaSideQuality("Top", 0.95, new Dictionary<string, double> { ["Coverage"] = 0.97 })],
            string.Empty,
            ["DarkCorner"]);

        string message = ManualFindLuminousArea.BuildResultMessage(result, new RoiRect(), 9568, 6380);

        Assert.Contains("算法：RobustV2", message);
        Assert.Contains("图像尺寸：9568 × 6380 px", message);
        Assert.Contains("搜索区域：全图", message);
        Assert.Contains("中心：(4874.000, 3304.000) px", message);
        Assert.Contains("可信度：0.983", message);
        Assert.Contains("左上 LT：(3745.000, 2695.000)", message);
        Assert.Contains("右下 RB：(6027.000, 3917.000)", message);
        Assert.Contains("Top：score=0.950, Coverage=0.970", message);
        Assert.Contains("警告：", message);
    }
}
