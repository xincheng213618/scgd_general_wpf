using ColorVision.Algorithms;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms
{
    public enum FrequencyWindowFunction
    {
        Rectangular,
        Hann,
        Hamming,
        Blackman,
    }

    public enum FrequencySpectrumVisualizationScale
    {
        Linear,
        Logarithmic,
    }

    /// <summary>Stable V1 contract for single-channel luminance frequency-spectrum analysis.</summary>
    public sealed class FrequencySpectrumParameters : StandardAlgorithmParameters
    {
        [Category("预处理"), DisplayName("窗函数")]
        public FrequencyWindowFunction WindowFunction { get; set; } = FrequencyWindowFunction.Hann;

        [Category("预处理"), DisplayName("移除直流均值")]
        public bool RemoveMean { get; set; } = true;

        [Category("频谱图"), DisplayName("频谱中心化")]
        [Description("仅改变频谱图坐标布局；频率表始终使用带符号 cycles/pixel 坐标。")]
        public bool CenterSpectrum { get; set; } = true;

        [Category("频谱图"), DisplayName("显示尺度")]
        public FrequencySpectrumVisualizationScale VisualizationScale { get; set; } = FrequencySpectrumVisualizationScale.Logarithmic;

        [Category("统计"), DisplayName("径向频率分箱宽度 (cycles/pixel)")]
        public double RadialBinWidthCyclesPerPixel { get; set; } = 0.005;

        [Category("统计"), DisplayName("方向分箱宽度 (degree)")]
        public double DirectionBinWidthDegrees { get; set; } = 2;

        [Category("峰值"), DisplayName("最小峰值频率 (cycles/pixel)")]
        public double MinimumPeakFrequencyCyclesPerPixel { get; set; } = 0.01;

        [Category("峰值"), DisplayName("最大峰值频率 (cycles/pixel)")]
        public double MaximumPeakFrequencyCyclesPerPixel { get; set; } = 0.7071067811865476;

        [Category("峰值"), DisplayName("相对功率阈值")]
        public double PeakRelativePowerThreshold { get; set; } = 0.1;

        [Category("峰值"), DisplayName("非极大抑制半径 (frequency bins)")]
        public int PeakNeighborhoodRadius { get; set; } = 2;

        [Category("峰值"), DisplayName("最大峰值数量")]
        public int MaximumPeaks { get; set; } = 32;

        [Category("资源"), DisplayName("最大输入像素数")]
        public long MaximumPixels { get; set; } = 100_000_000;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            if (!Enum.IsDefined(WindowFunction)) result.Add(nameof(WindowFunction), "invalid_enum", "WindowFunction is invalid.");
            if (!Enum.IsDefined(VisualizationScale)) result.Add(nameof(VisualizationScale), "invalid_enum", "VisualizationScale is invalid.");
            Range(result, nameof(RadialBinWidthCyclesPerPixel), RadialBinWidthCyclesPerPixel, 0.000001, 1);
            Range(result, nameof(DirectionBinWidthDegrees), DirectionBinWidthDegrees, 0.1, 180);
            Range(result, nameof(MinimumPeakFrequencyCyclesPerPixel), MinimumPeakFrequencyCyclesPerPixel, 0, Math.Sqrt(0.5));
            Range(result, nameof(MaximumPeakFrequencyCyclesPerPixel), MaximumPeakFrequencyCyclesPerPixel, 0.000001, Math.Sqrt(0.5));
            if (MinimumPeakFrequencyCyclesPerPixel >= MaximumPeakFrequencyCyclesPerPixel)
                result.Add(nameof(MaximumPeakFrequencyCyclesPerPixel), "frequency_range_order", "Maximum peak frequency must exceed the minimum peak frequency.");
            Range(result, nameof(PeakRelativePowerThreshold), PeakRelativePowerThreshold, 0, 1);
            if (PeakNeighborhoodRadius is < 1 or > 32)
                result.Add(nameof(PeakNeighborhoodRadius), "out_of_range", "PeakNeighborhoodRadius must be between 1 and 32.");
            if (MaximumPeaks is < 1 or > 10_000)
                result.Add(nameof(MaximumPeaks), "out_of_range", "MaximumPeaks must be between 1 and 10000.");
            if (MaximumPixels is < 1 or > 1_000_000_000)
                result.Add(nameof(MaximumPixels), "out_of_range", "MaximumPixels must be between 1 and 1000000000.");
            return result;
        }
    }
}
