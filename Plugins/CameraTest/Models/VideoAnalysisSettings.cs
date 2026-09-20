using ColorVision.Core;
using System.ComponentModel;

namespace CameraTest.Models;

public enum VideoAnalysisMode
{
    [Description("仅预览")] Preview,
    [Description("基础清晰度")] Sharpness,
    [Description("BMW 四边 SFR")] BmwSfr
}

public sealed class VideoAnalysisSettings
{
    [Category("实时分析"), DisplayName("分析项目")]
    public VideoAnalysisMode Mode { get; set; } = VideoAnalysisMode.BmwSfr;
    [Category("基础清晰度"), DisplayName("清晰度算法"), Description("与本机相机视频模式使用同一套清晰度算法；仅基础清晰度模式生效。")]
    public FocusAlgorithm Algorithm { get; set; } = FocusAlgorithm.VarianceOfLaplacian;
    [Category("基础清晰度区域"), DisplayName("X")]
    public int X { get; set; }
    [Category("基础清晰度区域"), DisplayName("Y")]
    public int Y { get; set; }
    [Category("基础清晰度区域"), DisplayName("宽度"), Description("宽高同时为 0 表示全幅；BMW 模式使用图中各测量点。")]
    public int Width { get; set; }
    [Category("基础清晰度区域"), DisplayName("高度")]
    public int Height { get; set; }
    public void Validate()
    {
        if (!Enum.IsDefined(Mode) || !Enum.IsDefined(Algorithm) || X < 0 || Y < 0 || Width < 0 || Height < 0 || (Width == 0) != (Height == 0))
            throw new ArgumentException("请检查视频分析项目与清晰度区域；宽高需同时为 0 或正数。");
    }
    public RoiRect ResolveRoi(int width, int height)
    {
        Validate();
        if (Width == 0 && Height == 0) return new(0, 0, width, height);
        if ((long)X + Width > width || (long)Y + Height > height) throw new ArgumentException("清晰度区域超出当前图像范围。");
        return new(X, Y, Width, Height);
    }
}
