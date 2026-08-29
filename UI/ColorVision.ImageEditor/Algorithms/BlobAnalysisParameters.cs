using ColorVision.Algorithms;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms
{
    public enum BlobForegroundPolarity
    {
        Bright,
        Dark,
    }

    public enum BlobConnectivity
    {
        Four = 4,
        Eight = 8,
    }

    /// <summary>Stable V1 thresholding and filtering contract for connected-component analysis.</summary>
    public sealed class BlobAnalysisParameters : StandardAlgorithmParameters
    {
        [DisplayName("阈值（0..255 标称刻度）")]
        [Description("按输入位深映射：Gray8/8-bit 为 0..255，16-bit 为 0..65535，float 为 0..1。彩色图按 BGR 亮度计算。")]
        public double Threshold { get; set; } = 128;

        [DisplayName("前景极性")]
        public BlobForegroundPolarity ForegroundPolarity { get; set; } = BlobForegroundPolarity.Bright;

        [DisplayName("连通性")]
        public BlobConnectivity Connectivity { get; set; } = BlobConnectivity.Eight;

        [DisplayName("最小面积 (px)")]
        public int MinimumArea { get; set; } = 1;

        [DisplayName("最大面积 (px，0 为不限)")]
        public int MaximumArea { get; set; }

        [DisplayName("最小宽度 (px)")]
        public int MinimumWidth { get; set; } = 1;

        [DisplayName("最大宽度 (px，0 为不限)")]
        public int MaximumWidth { get; set; }

        [DisplayName("最小高度 (px)")]
        public int MinimumHeight { get; set; } = 1;

        [DisplayName("最大高度 (px，0 为不限)")]
        public int MaximumHeight { get; set; }

        [DisplayName("排除接触图像边界的区域")]
        public bool ExcludeImageBorder { get; set; }

        [DisplayName("最大候选数量")]
        [Description("超过该数量时返回结构化失败，避免构造无界结果。")]
        public int MaximumCandidates { get; set; } = 10_000;

        [DisplayName("最多显示区域数量")]
        [Description("Table 和 Geometry 保留所有候选；仅限制 ImageView overlay 数量。")]
        public int MaximumOverlayComponents { get; set; } = 500;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            Range(result, nameof(Threshold), Threshold, 0, byte.MaxValue);
            if (!Enum.IsDefined(ForegroundPolarity))
                result.Add(nameof(ForegroundPolarity), "invalid_enum", "Unknown foreground polarity.");
            if (!Enum.IsDefined(Connectivity))
                result.Add(nameof(Connectivity), "invalid_enum", "Connectivity must be Four or Eight.");
            PositiveRange(result, nameof(MinimumArea), MinimumArea, nameof(MaximumArea), MaximumArea);
            PositiveRange(result, nameof(MinimumWidth), MinimumWidth, nameof(MaximumWidth), MaximumWidth);
            PositiveRange(result, nameof(MinimumHeight), MinimumHeight, nameof(MaximumHeight), MaximumHeight);
            if (MaximumCandidates is < 1 or > 100_000)
                result.Add(nameof(MaximumCandidates), "out_of_range", "MaximumCandidates must be between 1 and 100000.");
            if (MaximumOverlayComponents is < 0 or > 5_000)
                result.Add(nameof(MaximumOverlayComponents), "out_of_range", "MaximumOverlayComponents must be between 0 and 5000.");
            return result;
        }

        private static void PositiveRange(AlgorithmValidationResult result, string minimumName, int minimum, string maximumName, int maximum)
        {
            if (minimum < 1) result.Add(minimumName, "out_of_range", $"{minimumName} must be at least 1.");
            if (maximum < 0) result.Add(maximumName, "out_of_range", $"{maximumName} must be zero or positive.");
            else if (maximum != 0 && maximum < minimum)
                result.Add(maximumName, "range_order", $"{maximumName} must be zero or at least {minimumName}.");
        }
    }
}
