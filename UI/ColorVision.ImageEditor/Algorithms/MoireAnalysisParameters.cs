using ColorVision.Algorithms;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Stable V1 contract for periodic moire evidence and optional symmetric Gaussian notch filtering.</summary>
    public sealed class MoireAnalysisParameters : StandardAlgorithmParameters
    {
        [Category("预处理"), DisplayName("窗函数")]
        public FrequencyWindowFunction WindowFunction { get; set; } = FrequencyWindowFunction.Hann;

        [Category("预处理"), DisplayName("移除直流均值")]
        public bool RemoveMean { get; set; } = true;

        [Category("候选"), DisplayName("最小频率 (cycles/pixel)")]
        public double MinimumFrequencyCyclesPerPixel { get; set; } = 0.02;

        [Category("候选"), DisplayName("最大频率 (cycles/pixel)")]
        public double MaximumFrequencyCyclesPerPixel { get; set; } = 0.7071067811865476;

        [Category("候选"), DisplayName("相对功率阈值")]
        public double RelativePowerThreshold { get; set; } = 0.05;

        [Category("候选"), DisplayName("最小径向背景突出度")]
        public double MinimumProminenceRatio { get; set; } = 4;

        [Category("候选"), DisplayName("非极大抑制半径 (frequency bins)")]
        public int PeakNeighborhoodRadius { get; set; } = 2;

        [Category("候选"), DisplayName("最大 notch 建议数")]
        public int MaximumSuggestions { get; set; } = 8;

        [Category("滤波"), DisplayName("执行 notch 滤波")]
        public bool EnableNotchFilter { get; set; }

        [Category("滤波"), DisplayName("notch Sigma (cycles/pixel)")]
        public double NotchSigmaCyclesPerPixel { get; set; } = 0.005;

        [Category("滤波"), DisplayName("notch 衰减 (0..1)")]
        public double NotchAttenuation { get; set; } = 0.9;

        [Category("资源"), DisplayName("最大输入像素数")]
        public long MaximumPixels { get; set; } = 100_000_000;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            if (!Enum.IsDefined(WindowFunction)) result.Add(nameof(WindowFunction), "invalid_enum", "WindowFunction is invalid.");
            Range(result, nameof(MinimumFrequencyCyclesPerPixel), MinimumFrequencyCyclesPerPixel, 0.000001, Math.Sqrt(0.5));
            Range(result, nameof(MaximumFrequencyCyclesPerPixel), MaximumFrequencyCyclesPerPixel, 0.000001, Math.Sqrt(0.5));
            if (MinimumFrequencyCyclesPerPixel >= MaximumFrequencyCyclesPerPixel)
                result.Add(nameof(MaximumFrequencyCyclesPerPixel), "frequency_range_order", "Maximum frequency must exceed minimum frequency.");
            Range(result, nameof(RelativePowerThreshold), RelativePowerThreshold, 0, 1);
            Range(result, nameof(MinimumProminenceRatio), MinimumProminenceRatio, 1, 1_000_000);
            if (PeakNeighborhoodRadius is < 1 or > 32) result.Add(nameof(PeakNeighborhoodRadius), "out_of_range", "PeakNeighborhoodRadius must be between 1 and 32.");
            if (MaximumSuggestions is < 1 or > 1_000) result.Add(nameof(MaximumSuggestions), "out_of_range", "MaximumSuggestions must be between 1 and 1000.");
            Range(result, nameof(NotchSigmaCyclesPerPixel), NotchSigmaCyclesPerPixel, 0.000001, 0.25);
            Range(result, nameof(NotchAttenuation), NotchAttenuation, 0, 1);
            if (MaximumPixels is < 1 or > 1_000_000_000) result.Add(nameof(MaximumPixels), "out_of_range", "MaximumPixels must be between 1 and 1000000000.");
            return result;
        }
    }
}
