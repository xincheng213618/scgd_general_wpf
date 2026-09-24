using ColorVision.Algorithms;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms;

public sealed class DustDetectionParameters : DisplayMetrologyParameters
{
    [Category("检测"), DisplayName("暗斑对比度 (%)"), Description("相对局部背景的变暗比例；越小越敏感，也更易检出噪声。结果为候选，不是灰尘成因或合格判定。")]
    public double ContrastPercent { get; set; } = 2.5;

    [Category("检测"), DisplayName("最小面积 (分析像素²)"), Description("在缩小后的分析图上过滤面积；原图位置和面积换算记录在结果中。")]
    public int MinimumArea { get; set; } = 8;

    [Category("检测"), DisplayName("最大面积 (分析像素²)")]
    public int MaximumArea { get; set; } = 200_000;

    [Category("成像区域"), DisplayName("成像阈值 (%)"), Description("相对于分析图亮度第 95 百分位；取最大连通区域的外轮廓，保留缺口，不强制拟合圆。")]
    public double ForegroundPercent { get; set; } = 30;

    [Category("成像区域"), DisplayName("边界内缩 (分析像素)"), Description("排除成像边缘和图像四边；内缩区域与触及边界的候选不参与统计。")]
    public int BorderMargin { get; set; } = 65;

    [Category("分析"), DisplayName("分析图最长边 (px)"), Description("默认 3200。大图等比例缩小且不放大，小于分析分辨率的灰尘可能漏检。所有叠图坐标映射回原图。")]
    public int MaximumAnalysisDimension { get; set; } = 3200;

    [Category("分析"), DisplayName("最大背景窗口 (分析像素，奇数)"), Description("同时使用 31、121 及此窗口提取不同尺寸暗斑；应大于待检测污斑的尺寸。")]
    public int BackgroundKernel { get; set; } = 401;

    public override AlgorithmValidationResult Validate()
    {
        var result = base.Validate();
        Range(result, nameof(ContrastPercent), ContrastPercent, 0.1, 50);
        Range(result, nameof(MinimumArea), MinimumArea, 1, 1_000_000);
        Range(result, nameof(MaximumArea), MaximumArea, MinimumArea, 1_000_000);
        Range(result, nameof(ForegroundPercent), ForegroundPercent, 1, 90);
        Range(result, nameof(BorderMargin), BorderMargin, 2, 512);
        Range(result, nameof(MaximumAnalysisDimension), MaximumAnalysisDimension, 256, 4096);
        Range(result, nameof(BackgroundKernel), BackgroundKernel, 121, 801);
        if (BackgroundKernel % 2 == 0) result.Add(nameof(BackgroundKernel), "odd_kernel_required", "背景窗口必须为奇数。");
        return result;
    }
}
