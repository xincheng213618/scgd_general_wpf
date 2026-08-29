using ColorVision.Algorithms;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms
{
    public enum SubpixelEdgePolarity
    {
        Either,
        Rising,
        Falling,
    }

    public enum SubpixelEdgeBoundaryMode
    {
        RejectCaliper,
        Clamp,
    }

    /// <summary>Stable V1 caliper contract; consecutive Invocation.Roi polyline points define independent search segments.</summary>
    public sealed class SubpixelEdgeParameters : StandardAlgorithmParameters
    {
        [DisplayName("边缘极性")]
        public SubpixelEdgePolarity Polarity { get; set; } = SubpixelEdgePolarity.Either;

        [DisplayName("采样间距 (px)")]
        public double SampleSpacingPixels { get; set; } = 0.25;

        [DisplayName("法向平均半径 (px)")]
        [Description("在卡尺线法向两侧按整数像素间隔平均；0 表示仅采样中心线。")]
        public int NormalAveragingRadiusPixels { get; set; }

        [DisplayName("一维高斯 Sigma (px)")]
        [Description("沿搜索方向平滑后计算梯度；0 表示不平滑。")]
        public double SmoothingSigmaPixels { get; set; } = 0.75;

        [DisplayName("最小梯度 (标称 8-bit DN/px)")]
        public double MinimumGradient { get; set; } = 10;

        [DisplayName("越界规则")]
        public SubpixelEdgeBoundaryMode BoundaryMode { get; set; } = SubpixelEdgeBoundaryMode.RejectCaliper;

        [DisplayName("最大卡尺数")]
        public int MaximumCalipers { get; set; } = 10_000;

        [DisplayName("单卡尺最大采样数")]
        public int MaximumSamplesPerCaliper { get; set; } = 100_000;

        [DisplayName("总最大采样数")]
        public int MaximumTotalSamples { get; set; } = 1_000_000;

        [DisplayName("最大叠加卡尺数")]
        public int MaximumOverlayCalipers { get; set; } = 2_000;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            if (!Enum.IsDefined(Polarity)) result.Add(nameof(Polarity), "invalid_enum", "Polarity is invalid.");
            if (!Enum.IsDefined(BoundaryMode)) result.Add(nameof(BoundaryMode), "invalid_enum", "BoundaryMode is invalid.");
            Range(result, nameof(SampleSpacingPixels), SampleSpacingPixels, 0.05, 2);
            if (NormalAveragingRadiusPixels is < 0 or > 32)
                result.Add(nameof(NormalAveragingRadiusPixels), "out_of_range", "NormalAveragingRadiusPixels must be between 0 and 32.");
            Range(result, nameof(SmoothingSigmaPixels), SmoothingSigmaPixels, 0, 10);
            Range(result, nameof(MinimumGradient), MinimumGradient, 0, 255);
            if (MaximumCalipers is < 1 or > 10_000)
                result.Add(nameof(MaximumCalipers), "out_of_range", "MaximumCalipers must be between 1 and 10000.");
            if (MaximumSamplesPerCaliper is < 6 or > 1_000_000)
                result.Add(nameof(MaximumSamplesPerCaliper), "out_of_range", "MaximumSamplesPerCaliper must be between 6 and 1000000.");
            if (MaximumTotalSamples is < 6 or > 10_000_000)
                result.Add(nameof(MaximumTotalSamples), "out_of_range", "MaximumTotalSamples must be between 6 and 10000000.");
            if (MaximumOverlayCalipers is < 0 or > 5_000)
                result.Add(nameof(MaximumOverlayCalipers), "out_of_range", "MaximumOverlayCalipers must be between 0 and 5000.");
            return result;
        }
    }
}
