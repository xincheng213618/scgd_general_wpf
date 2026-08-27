using ColorVision.Algorithms;
using System;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Stable V1 parameters for ROI statistics. ROI geometry belongs to the invocation.</summary>
    public sealed class RoiStatisticsParameters : StandardAlgorithmParameters
    {
        [DisplayName("直方图分箱数")]
        [Description("2 到 4096。整数图按位深标称范围分箱，32F 图按 ROI 内有限值范围分箱。")]
        public int HistogramBins { get; set; } = 256;

        [DisplayName("百分位 (%)")]
        [Description("需要计算的百分位，范围 0 到 100，最多 32 项。使用线性插值。")]
        public double[] Percentiles { get; set; } = [1, 5, 50, 95, 99];

        [DisplayName("检测坏点候选")]
        public bool DetectBadPixelCandidates { get; set; } = true;

        [DisplayName("坏点邻域半径")]
        [Description("以候选像素为中心的方形邻域半径，范围 1 到 5。")]
        public int BadPixelNeighborhoodRadius { get; set; } = 1;

        [DisplayName("坏点 Sigma 阈值")]
        [Description("候选值相对邻域中位数的偏差必须超过该倍数的局部 MAD Sigma。")]
        public double BadPixelSigmaThreshold { get; set; } = 6;

        [DisplayName("最小坏点偏差比例")]
        [Description("相对位深标称范围的最小绝对偏差；32F 的标称范围为 0 到 1。")]
        public double BadPixelMinimumDeviationFraction { get; set; } = 0.02;

        [DisplayName("最多返回坏点候选")]
        [Description("统计总数不截断；Table、Geometry 和 Overlay 最多返回该数量。")]
        public int MaximumBadPixelCandidates { get; set; } = 1000;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            if (HistogramBins is < 2 or > 4096)
                result.Add(nameof(HistogramBins), "out_of_range", "HistogramBins must be between 2 and 4096.");
            if (Percentiles == null || Percentiles.Length == 0 || Percentiles.Length > 32)
            {
                result.Add(nameof(Percentiles), "invalid_percentiles", "Percentiles must contain between 1 and 32 values.");
            }
            else
            {
                for (int index = 0; index < Percentiles.Length; index++)
                {
                    double percentile = Percentiles[index];
                    if (!double.IsFinite(percentile) || percentile < 0 || percentile > 100)
                        result.Add($"{nameof(Percentiles)}[{index}]", "out_of_range", "Percentiles must be finite values between 0 and 100.");
                }
                if (Percentiles.Distinct().Count() != Percentiles.Length)
                    result.Add(nameof(Percentiles), "duplicate_percentile", "Percentiles cannot contain duplicate values.");
            }
            if (BadPixelNeighborhoodRadius is < 1 or > 5)
                result.Add(nameof(BadPixelNeighborhoodRadius), "out_of_range", "BadPixelNeighborhoodRadius must be between 1 and 5.");
            Range(result, nameof(BadPixelSigmaThreshold), BadPixelSigmaThreshold, 0.1, 100);
            Range(result, nameof(BadPixelMinimumDeviationFraction), BadPixelMinimumDeviationFraction, 0, 1);
            if (MaximumBadPixelCandidates is < 0 or > 100_000)
                result.Add(nameof(MaximumBadPixelCandidates), "out_of_range", "MaximumBadPixelCandidates must be between 0 and 100000.");
            return result;
        }
    }
}
