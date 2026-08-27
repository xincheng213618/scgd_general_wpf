using ColorVision.Algorithms;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms
{
    public enum ImageProfileInterpolation
    {
        Nearest,
        Bilinear,
    }

    public enum ImageProfileBoundaryMode
    {
        Reject,
        Clamp,
        Skip,
    }

    /// <summary>Stable V1 sampling contract; path geometry belongs to Invocation.Roi.</summary>
    public sealed class ImageProfileParameters : StandardAlgorithmParameters
    {
        [DisplayName("采样间距 (px)")]
        [Description("沿折线累计像素距离采样；开放路径始终包含首尾点。")]
        public double SampleSpacingPixels { get; set; } = 1;

        [DisplayName("插值")]
        public ImageProfileInterpolation Interpolation { get; set; } = ImageProfileInterpolation.Bilinear;

        [DisplayName("越界规则")]
        public ImageProfileBoundaryMode BoundaryMode { get; set; } = ImageProfileBoundaryMode.Reject;

        [DisplayName("闭合路径")]
        [Description("连接最后一点与第一点；不重复输出首点。")]
        public bool ClosePath { get; set; }

        [DisplayName("输出亮度曲线")]
        [Description("彩色图使用 Rec.601 系数 0.299R + 0.587G + 0.114B。")]
        public bool IncludeLuminance { get; set; } = true;

        [DisplayName("输出 Alpha 曲线")]
        public bool IncludeAlpha { get; set; } = true;

        [DisplayName("最大采样点数")]
        public int MaximumSamples { get; set; } = 100_000;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            Range(result, nameof(SampleSpacingPixels), SampleSpacingPixels, 0.01, 1_000_000);
            if (!System.Enum.IsDefined(Interpolation))
                result.Add(nameof(Interpolation), "invalid_enum", "Interpolation is invalid.");
            if (!System.Enum.IsDefined(BoundaryMode))
                result.Add(nameof(BoundaryMode), "invalid_enum", "BoundaryMode is invalid.");
            if (MaximumSamples is < 2 or > 1_000_000)
                result.Add(nameof(MaximumSamples), "out_of_range", "MaximumSamples must be between 2 and 1000000.");
            return result;
        }
    }
}
