using System;
using System.ComponentModel;

namespace ColorVision.Core;

public enum SfrChartType
{
    [Description("BMW / 宝马靶标")] Bmw,
    [Description("棋盘格交叉点")] Checkerboard,
    [Description("自动识别")] Auto
}

/// <summary>Original-pixel dimensions. Zero retains the locator's automatic geometry.</summary>
public sealed record BmwSfrRoiSettings
{
    [Category("图卡"), DisplayName("图卡类型"), Description("BMW：外框包含一个完整靶标。棋盘格：将所需交叉点置于外框中心，四侧保留足够格面。自动：优先验证 BMW 形状，再检测棋盘格。")]
    public SfrChartType ChartType { get; set; } = SfrChartType.Bmw;

    [Category("四边测量框"), DisplayName("沿刃边长度 (px，0=自动)"), Description("左/右边框的宽、上/下边框的高；非零时至少 40 px。")]
    public int AlongEdgePixels { get; set; }
    [Category("四边测量框"), DisplayName("跨刃边宽度 (px，0=自动)"), Description("左/右边框的高、上/下边框的宽；非零时至少 40 px。")]
    public int AcrossEdgePixels { get; set; }
    [Category("四边测量框"), DisplayName("距靶标中心 (px，0=自动)"), Description("从识别到的中心沿各刃边方向移动测量框中心。四边共用距离；0 保留按靶标大小自动确定的位置。")]
    public double CenterDistancePixels { get; set; }

    public void Validate()
    {
        if (!Enum.IsDefined(ChartType)) throw new ArgumentException("图卡类型无效。");
        if (AlongEdgePixels != 0 && (AlongEdgePixels < 40 || AlongEdgePixels > 8192)
            || AcrossEdgePixels != 0 && (AcrossEdgePixels < 40 || AcrossEdgePixels > 8192)
            || !double.IsFinite(CenterDistancePixels) || CenterDistancePixels < 0 || CenterDistancePixels > 8192)
            throw new ArgumentException("测量框长宽应为 0（自动）或 40～8192 px，距中心应为 0～8192 px。");
    }

    public RoiRect Resolve(BmwEdgeId id, RoiRect automatic, double centerX, double centerY)
    {
        Validate();
        if (automatic.Width <= 0 || automatic.Height <= 0) return automatic;
        bool horizontal = id is BmwEdgeId.Left or BmwEdgeId.Right;
        int width = (horizontal ? AlongEdgePixels : AcrossEdgePixels);
        int height = (horizontal ? AcrossEdgePixels : AlongEdgePixels);
        if (width == 0) width = automatic.Width;
        if (height == 0) height = automatic.Height;
        double x = automatic.X + automatic.Width / 2.0, y = automatic.Y + automatic.Height / 2.0;
        if (CenterDistancePixels > 0)
        {
            // The automatic ROI center lies on the detected target axis, including chart rotation.
            double dx = x - centerX, dy = y - centerY, length = Math.Sqrt(dx * dx + dy * dy);
            if (!double.IsFinite(length) || length <= 0) return default;
            x = centerX + dx / length * CenterDistancePixels;
            y = centerY + dy / length * CenterDistancePixels;
        }
        return new((int)Math.Round(x - width / 2.0), (int)Math.Round(y - height / 2.0), width, height);
    }

    public static bool IsInside(RoiRect roi, RoiRect parent) => roi.Width > 0 && roi.Height > 0 && roi.X >= parent.X && roi.Y >= parent.Y
        && (long)roi.X + roi.Width <= (long)parent.X + parent.Width && (long)roi.Y + roi.Height <= (long)parent.Y + parent.Height;
}
