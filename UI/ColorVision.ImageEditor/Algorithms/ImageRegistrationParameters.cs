using ColorVision.Algorithms;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms
{
    public enum ImageRegistrationMethod
    {
        PhaseCorrelation,
        OrbHomography,
    }

    /// <summary>Stable V1 contract for registering a moving image into reference pixel-center coordinates.</summary>
    public sealed class ImageRegistrationParameters : StandardAlgorithmParameters
    {
        [Category("方法"), DisplayName("配准方法")]
        public ImageRegistrationMethod Method { get; set; } = ImageRegistrationMethod.PhaseCorrelation;

        [Category("相位相关"), DisplayName("使用 Hann 窗")]
        public bool UseHannWindow { get; set; } = true;

        [Category("相位相关"), DisplayName("最小响应")]
        public double MinimumPhaseResponse { get; set; } = 0.05;

        [Category("相位相关"), DisplayName("最大平移 (px)")]
        public double MaximumTranslationPixels { get; set; } = 10_000;

        [Category("ORB"), DisplayName("最大特征数")]
        public int MaximumFeatures { get; set; } = 2_000;

        [Category("ORB"), DisplayName("金字塔比例")]
        public double PyramidScaleFactor { get; set; } = 1.2;

        [Category("ORB"), DisplayName("金字塔层数")]
        public int PyramidLevels { get; set; } = 8;

        [Category("ORB"), DisplayName("FAST 阈值")]
        public int FastThreshold { get; set; } = 20;

        [Category("匹配"), DisplayName("Lowe 比率")]
        public double LoweRatio { get; set; } = 0.75;

        [Category("匹配"), DisplayName("最少双向匹配")]
        public int MinimumMatchCount { get; set; } = 10;

        [Category("共识"), DisplayName("重投影阈值 (px)")]
        public double ConsensusReprojectionThresholdPixels { get; set; } = 3;

        [Category("共识"), DisplayName("最少内点")]
        public int MinimumInlierCount { get; set; } = 8;

        [Category("共识"), DisplayName("最小内点比例")]
        public double MinimumInlierRatio { get; set; } = 0.35;

        [Category("共识"), DisplayName("最大候选匹配")]
        public int MaximumConsensusMatches { get; set; } = 40;

        [Category("共识"), DisplayName("最大共识评估数")]
        public int MaximumConsensusEvaluations { get; set; } = 5_000;

        [Category("结果"), DisplayName("最大报告匹配数")]
        public int MaximumReportedMatches { get; set; } = 500;

        [Category("变换"), DisplayName("插值")]
        public GeometricTransformInterpolation Interpolation { get; set; } = GeometricTransformInterpolation.Linear;

        [Category("变换"), DisplayName("边界模式")]
        public GeometricTransformBorder Border { get; set; } = GeometricTransformBorder.Constant;

        [Category("变换"), DisplayName("边界 B/灰度 (0..1)")]
        public double BorderChannel0 { get; set; }

        [Category("变换"), DisplayName("边界 G (0..1)")]
        public double BorderChannel1 { get; set; }

        [Category("变换"), DisplayName("边界 R (0..1)")]
        public double BorderChannel2 { get; set; }

        [Category("变换"), DisplayName("边界 Alpha (0..1)")]
        public double BorderChannel3 { get; set; }

        [Category("数值"), DisplayName("最大条件数")]
        public double MaximumConditionNumber { get; set; } = 1e12;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            if (!Enum.IsDefined(Method)) result.Add(nameof(Method), "invalid_enum", "Method is invalid.");
            if (!Enum.IsDefined(Interpolation)) result.Add(nameof(Interpolation), "invalid_enum", "Interpolation is invalid.");
            if (!Enum.IsDefined(Border)) result.Add(nameof(Border), "invalid_enum", "Border is invalid.");
            Range(result, nameof(MinimumPhaseResponse), MinimumPhaseResponse, 0, 1);
            Range(result, nameof(MaximumTranslationPixels), MaximumTranslationPixels, 0, 1_000_000);
            if (MaximumFeatures is < 100 or > 100_000) result.Add(nameof(MaximumFeatures), "out_of_range", "MaximumFeatures must be between 100 and 100000.");
            Range(result, nameof(PyramidScaleFactor), PyramidScaleFactor, 1.01, 2);
            if (PyramidLevels is < 1 or > 32) result.Add(nameof(PyramidLevels), "out_of_range", "PyramidLevels must be between 1 and 32.");
            if (FastThreshold is < 1 or > 255) result.Add(nameof(FastThreshold), "out_of_range", "FastThreshold must be between 1 and 255.");
            Range(result, nameof(LoweRatio), LoweRatio, 0.1, 0.99);
            if (MinimumMatchCount is < 4 or > 100_000) result.Add(nameof(MinimumMatchCount), "out_of_range", "MinimumMatchCount must be between 4 and 100000.");
            Range(result, nameof(ConsensusReprojectionThresholdPixels), ConsensusReprojectionThresholdPixels, 0.01, 1_000);
            if (MinimumInlierCount is < 4 or > 100_000) result.Add(nameof(MinimumInlierCount), "out_of_range", "MinimumInlierCount must be between 4 and 100000.");
            Range(result, nameof(MinimumInlierRatio), MinimumInlierRatio, 0.01, 1);
            if (MaximumConsensusMatches is < 4 or > 200) result.Add(nameof(MaximumConsensusMatches), "out_of_range", "MaximumConsensusMatches must be between 4 and 200.");
            if (MaximumConsensusEvaluations is < 1 or > 1_000_000) result.Add(nameof(MaximumConsensusEvaluations), "out_of_range", "MaximumConsensusEvaluations must be between 1 and 1000000.");
            if (MaximumReportedMatches is < 0 or > 100_000) result.Add(nameof(MaximumReportedMatches), "out_of_range", "MaximumReportedMatches must be between 0 and 100000.");
            Range(result, nameof(BorderChannel0), BorderChannel0, 0, 1);
            Range(result, nameof(BorderChannel1), BorderChannel1, 0, 1);
            Range(result, nameof(BorderChannel2), BorderChannel2, 0, 1);
            Range(result, nameof(BorderChannel3), BorderChannel3, 0, 1);
            Range(result, nameof(MaximumConditionNumber), MaximumConditionNumber, 1, 1e18);
            if (MinimumInlierCount > MinimumMatchCount)
                result.Add(nameof(MinimumInlierCount), "inlier_count_exceeds_match_count", "MinimumInlierCount cannot exceed MinimumMatchCount.");
            if (MaximumConsensusMatches < MinimumMatchCount)
                result.Add(nameof(MaximumConsensusMatches), "consensus_limit_below_match_count", "MaximumConsensusMatches cannot be below MinimumMatchCount.");
            return result;
        }
    }
}
