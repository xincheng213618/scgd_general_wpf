using ColorVision.Algorithms;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms
{
    public enum ContourForegroundPolarity
    {
        Bright,
        Dark,
    }

    public enum ContourRetrievalMode
    {
        External,
        List,
        Tree,
    }

    public enum ContourApproximationMode
    {
        None,
        Simple,
    }

    /// <summary>Stable V1 threshold, extraction and filtering contract for pixel-accurate contours.</summary>
    public sealed class ContourAnalysisParameters : StandardAlgorithmParameters
    {
        [DisplayName("阈值（0..255 标称刻度）")]
        [Description("按输入位深映射：8-bit 为 0..255，16-bit 为 0..65535，float 为 0..1；彩色按 BGR 亮度计算。")]
        public double Threshold { get; set; } = 128;

        [DisplayName("前景极性")]
        public ContourForegroundPolarity ForegroundPolarity { get; set; } = ContourForegroundPolarity.Bright;

        [DisplayName("层级检索")]
        public ContourRetrievalMode RetrievalMode { get; set; } = ContourRetrievalMode.External;

        [DisplayName("边界点压缩")]
        public ContourApproximationMode ApproximationMode { get; set; } = ContourApproximationMode.Simple;

        [DisplayName("额外简化容差 (px)")]
        [Description("0 保留 OpenCV 提取结果；大于 0 时用 Douglas–Peucker 简化闭合轮廓。")]
        public double SimplificationEpsilon { get; set; }

        [DisplayName("最小面积 (px²)")]
        public double MinimumArea { get; set; } = 1;

        [DisplayName("最大面积 (px²，0 为不限)")]
        public double MaximumArea { get; set; }

        [DisplayName("最小周长 (px)")]
        public double MinimumPerimeter { get; set; }

        [DisplayName("最大周长 (px，0 为不限)")]
        public double MaximumPerimeter { get; set; }

        [DisplayName("最少边界点数")]
        public int MinimumPointCount { get; set; } = 1;

        [DisplayName("最多边界点数（0 为不限）")]
        public int MaximumPointCount { get; set; }

        [DisplayName("最小圆度")]
        [Description("4πA/P²，范围 0..1；0 表示不按圆度过滤。")]
        public double MinimumCircularity { get; set; }

        [DisplayName("最小实心度")]
        [Description("轮廓面积/凸包面积，范围 0..1；同时作为几何 confidence，非分类概率。")]
        public double MinimumSolidity { get; set; }

        [DisplayName("排除接触图像边界的轮廓")]
        public bool ExcludeImageBorder { get; set; }

        [DisplayName("最大候选数量")]
        public int MaximumCandidates { get; set; } = 10_000;

        [DisplayName("最大结构化点数")]
        [Description("所有候选简化后的点数总和；超限返回结构化失败，避免无界结果。")]
        public int MaximumTotalPoints { get; set; } = 1_000_000;

        [DisplayName("最多显示轮廓数量")]
        [Description("Table 和 Geometry 保留所有候选；仅限制 ImageView overlay 数量。")]
        public int MaximumOverlayContours { get; set; } = 500;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            Range(result, nameof(Threshold), Threshold, 0, byte.MaxValue);
            if (!Enum.IsDefined(ForegroundPolarity))
                result.Add(nameof(ForegroundPolarity), "invalid_enum", "Unknown foreground polarity.");
            if (!Enum.IsDefined(RetrievalMode))
                result.Add(nameof(RetrievalMode), "invalid_enum", "Unknown contour retrieval mode.");
            if (!Enum.IsDefined(ApproximationMode))
                result.Add(nameof(ApproximationMode), "invalid_enum", "Unknown contour approximation mode.");
            Range(result, nameof(SimplificationEpsilon), SimplificationEpsilon, 0, 1_000_000);
            NonNegativeRange(result, nameof(MinimumArea), MinimumArea, nameof(MaximumArea), MaximumArea);
            NonNegativeRange(result, nameof(MinimumPerimeter), MinimumPerimeter, nameof(MaximumPerimeter), MaximumPerimeter);
            if (MinimumPointCount < 1)
                result.Add(nameof(MinimumPointCount), "out_of_range", "MinimumPointCount must be at least 1.");
            if (MaximumPointCount < 0)
                result.Add(nameof(MaximumPointCount), "out_of_range", "MaximumPointCount must be zero or positive.");
            else if (MaximumPointCount != 0 && MaximumPointCount < MinimumPointCount)
                result.Add(nameof(MaximumPointCount), "range_order", "MaximumPointCount must be zero or at least MinimumPointCount.");
            Range(result, nameof(MinimumCircularity), MinimumCircularity, 0, 1);
            Range(result, nameof(MinimumSolidity), MinimumSolidity, 0, 1);
            if (MaximumCandidates is < 1 or > 100_000)
                result.Add(nameof(MaximumCandidates), "out_of_range", "MaximumCandidates must be between 1 and 100000.");
            if (MaximumTotalPoints is < 1 or > 10_000_000)
                result.Add(nameof(MaximumTotalPoints), "out_of_range", "MaximumTotalPoints must be between 1 and 10000000.");
            if (MaximumOverlayContours is < 0 or > 5_000)
                result.Add(nameof(MaximumOverlayContours), "out_of_range", "MaximumOverlayContours must be between 0 and 5000.");
            return result;
        }

        private static void NonNegativeRange(AlgorithmValidationResult result, string minimumName, double minimum, string maximumName, double maximum)
        {
            if (!double.IsFinite(minimum) || minimum < 0)
                result.Add(minimumName, "out_of_range", $"{minimumName} must be finite and non-negative.");
            if (!double.IsFinite(maximum) || maximum < 0)
                result.Add(maximumName, "out_of_range", $"{maximumName} must be finite and non-negative.");
            else if (maximum != 0 && maximum < minimum)
                result.Add(maximumName, "range_order", $"{maximumName} must be zero or at least {minimumName}.");
        }
    }
}
