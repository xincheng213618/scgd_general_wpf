using ColorVision.Algorithms;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms
{
    public enum CircleFitMode
    {
        LeastSquares,
        RobustHuber,
    }

    /// <summary>Stable V1 contract for fitting one circle to the point set carried by Invocation.Roi.</summary>
    public sealed class CircleFitParameters : StandardAlgorithmParameters
    {
        [DisplayName("拟合方式")]
        public CircleFitMode Mode { get; set; } = CircleFitMode.RobustHuber;

        [DisplayName("有效点最大径向残差 (px)")]
        public double ResidualThresholdPixels { get; set; } = 1.5;

        [DisplayName("Huber 调节常数")]
        public double HuberTuningConstant { get; set; } = 1.345;

        [DisplayName("最大迭代次数")]
        public int MaximumIterations { get; set; } = 25;

        [DisplayName("收敛容差")]
        public double ConvergenceTolerance { get; set; } = 1e-9;

        [DisplayName("最少有效点数")]
        public int MinimumInlierCount { get; set; } = 3;

        [DisplayName("最小半径 (px)")]
        public double MinimumRadiusPixels { get; set; }

        [DisplayName("最大半径 (px，0 不限制)")]
        public double MaximumRadiusPixels { get; set; }

        [DisplayName("最小角覆盖 (degree)")]
        public double MinimumAngularCoverageDegrees { get; set; }

        [DisplayName("最大输入点数")]
        public int MaximumPoints { get; set; } = 100_000;

        [DisplayName("最大共识候选数")]
        public int MaximumConsensusCandidates { get; set; } = 512;

        [DisplayName("最大共识点-候选评估数")]
        public long MaximumConsensusEvaluations { get; set; } = 10_000_000;

        [DisplayName("最大叠加点数")]
        public int MaximumOverlayPoints { get; set; } = 2_000;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            if (!Enum.IsDefined(Mode)) result.Add(nameof(Mode), "invalid_enum", "Mode is invalid.");
            Range(result, nameof(ResidualThresholdPixels), ResidualThresholdPixels, 0.000001, 1_000_000);
            Range(result, nameof(HuberTuningConstant), HuberTuningConstant, 0.1, 10);
            if (MaximumIterations is < 1 or > 1_000)
                result.Add(nameof(MaximumIterations), "out_of_range", "MaximumIterations must be between 1 and 1000.");
            Range(result, nameof(ConvergenceTolerance), ConvergenceTolerance, 1e-15, 0.1);
            if (MinimumInlierCount is < 3 or > 100_000)
                result.Add(nameof(MinimumInlierCount), "out_of_range", "MinimumInlierCount must be between 3 and 100000.");
            Range(result, nameof(MinimumRadiusPixels), MinimumRadiusPixels, 0, 1_000_000_000);
            Range(result, nameof(MaximumRadiusPixels), MaximumRadiusPixels, 0, 1_000_000_000);
            if (MaximumRadiusPixels != 0 && MaximumRadiusPixels < MinimumRadiusPixels)
                result.Add(nameof(MaximumRadiusPixels), "range_order", "MaximumRadiusPixels must be zero or at least MinimumRadiusPixels.");
            Range(result, nameof(MinimumAngularCoverageDegrees), MinimumAngularCoverageDegrees, 0, 360);
            if (MaximumPoints is < 3 or > 1_000_000)
                result.Add(nameof(MaximumPoints), "out_of_range", "MaximumPoints must be between 3 and 1000000.");
            else if (MaximumPoints < MinimumInlierCount)
                result.Add(nameof(MaximumPoints), "range_order", "MaximumPoints must be at least MinimumInlierCount.");
            if (MaximumConsensusCandidates is < 1 or > 10_000)
                result.Add(nameof(MaximumConsensusCandidates), "out_of_range", "MaximumConsensusCandidates must be between 1 and 10000.");
            if (MaximumConsensusEvaluations is < 3 or > 100_000_000)
                result.Add(nameof(MaximumConsensusEvaluations), "out_of_range", "MaximumConsensusEvaluations must be between 3 and 100000000.");
            if (MaximumOverlayPoints is < 0 or > 10_000)
                result.Add(nameof(MaximumOverlayPoints), "out_of_range", "MaximumOverlayPoints must be between 0 and 10000.");
            return result;
        }
    }
}
