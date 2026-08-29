using ColorVision.Algorithms;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms
{
    public enum LineFitMode
    {
        TotalLeastSquares,
        RobustHuber,
    }

    public enum LineFitOutputExtent
    {
        InlierSpan,
        ImageBounds,
    }

    /// <summary>Stable V1 contract for fitting one line to the point set carried by Invocation.Roi.</summary>
    public sealed class LineFitParameters : StandardAlgorithmParameters
    {
        [DisplayName("拟合方式")]
        public LineFitMode Mode { get; set; } = LineFitMode.RobustHuber;

        [DisplayName("有效点最大残差 (px)")]
        public double ResidualThresholdPixels { get; set; } = 1.5;

        [DisplayName("Huber 调节常数")]
        public double HuberTuningConstant { get; set; } = 1.345;

        [DisplayName("最大迭代次数")]
        public int MaximumIterations { get; set; } = 20;

        [DisplayName("收敛容差")]
        public double ConvergenceTolerance { get; set; } = 1e-9;

        [DisplayName("最少有效点数")]
        public int MinimumInlierCount { get; set; } = 2;

        [DisplayName("最大输入点数")]
        public int MaximumPoints { get; set; } = 100_000;

        [DisplayName("最大叠加点数")]
        public int MaximumOverlayPoints { get; set; } = 2_000;

        [DisplayName("拟合线显示范围")]
        public LineFitOutputExtent OutputExtent { get; set; } = LineFitOutputExtent.ImageBounds;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            if (!Enum.IsDefined(Mode)) result.Add(nameof(Mode), "invalid_enum", "Mode is invalid.");
            if (!Enum.IsDefined(OutputExtent)) result.Add(nameof(OutputExtent), "invalid_enum", "OutputExtent is invalid.");
            Range(result, nameof(ResidualThresholdPixels), ResidualThresholdPixels, 0.000001, 1_000_000);
            Range(result, nameof(HuberTuningConstant), HuberTuningConstant, 0.1, 10);
            if (MaximumIterations is < 1 or > 1_000)
                result.Add(nameof(MaximumIterations), "out_of_range", "MaximumIterations must be between 1 and 1000.");
            Range(result, nameof(ConvergenceTolerance), ConvergenceTolerance, 1e-15, 0.1);
            if (MinimumInlierCount is < 2 or > 100_000)
                result.Add(nameof(MinimumInlierCount), "out_of_range", "MinimumInlierCount must be between 2 and 100000.");
            if (MaximumPoints is < 2 or > 1_000_000)
                result.Add(nameof(MaximumPoints), "out_of_range", "MaximumPoints must be between 2 and 1000000.");
            else if (MaximumPoints < MinimumInlierCount)
                result.Add(nameof(MaximumPoints), "range_order", "MaximumPoints must be at least MinimumInlierCount.");
            if (MaximumOverlayPoints is < 0 or > 10_000)
                result.Add(nameof(MaximumOverlayPoints), "out_of_range", "MaximumOverlayPoints must be between 0 and 10000.");
            return result;
        }
    }
}
