using ColorVision.Algorithms;
using ColorVision.UI.PropertyEditor;
using System.ComponentModel;

namespace ProjectARVRPro.AlgorithmEvidence;

internal sealed class EvidenceSearchRegion
{
    [Category("搜索区域"), DisplayName("限定搜索区域"), Description("关闭时使用整张原图；开启时填写原图像素坐标。")]
    public bool Enabled { get; set; }
    [Category("搜索区域"), DisplayName("左侧 X (px)"), PropertyVisibility(nameof(Enabled))]
    public int X { get; set; }
    [Category("搜索区域"), DisplayName("顶部 Y (px)"), PropertyVisibility(nameof(Enabled))]
    public int Y { get; set; }
    [Category("搜索区域"), DisplayName("宽度 (px)"), PropertyVisibility(nameof(Enabled))]
    public int Width { get; set; } = 1000;
    [Category("搜索区域"), DisplayName("高度 (px)"), PropertyVisibility(nameof(Enabled))]
    public int Height { get; set; } = 1000;

    internal RectangleAlgorithmRoi? ToRoi() => Enabled ? new RectangleAlgorithmRoi(X, Y, Width, Height) : null;
}
