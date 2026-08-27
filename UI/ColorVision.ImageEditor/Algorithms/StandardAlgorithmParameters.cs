using ColorVision.Algorithms;
using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ColorVision.ImageEditor.Algorithms
{
    public static class StandardAlgorithmIds
    {
        public static readonly AlgorithmId Invert = new("colorvision.image.invert");
        public static readonly AlgorithmId Canny = new("colorvision.image.canny");
        public static readonly AlgorithmId BasicAdjustment = new("colorvision.image.basic-adjustment");
        public static readonly AlgorithmId Threshold = new("colorvision.image.threshold");
        public static readonly AlgorithmId Sharpen = new("colorvision.image.sharpen");
        public static readonly AlgorithmId GaussianBlur = new("colorvision.image.gaussian-blur");
        public static readonly AlgorithmId MedianBlur = new("colorvision.image.median-blur");
        public static readonly AlgorithmId Morphology = new("colorvision.image.morphology");
        public static readonly AlgorithmId Denoise = new("colorvision.image.denoise");
        public static readonly AlgorithmId AutoLevels = new("colorvision.image.auto-levels");
        public static readonly AlgorithmId WhiteBalance = new("colorvision.image.white-balance");
        public static readonly AlgorithmId HistogramEqualization = new("colorvision.image.histogram-equalization");
        public static readonly AlgorithmId RemoveMoire = new("colorvision.image.remove-moire");
        public static readonly AlgorithmId PseudoColor = new("colorvision.image.pseudo-color");
        public static readonly AlgorithmId RoiStatistics = new("colorvision.analysis.roi-statistics");
        public static readonly AlgorithmId ImageProfile = new("colorvision.analysis.image-profile");
        public static readonly AlgorithmId ImageComparison = new("colorvision.analysis.image-comparison");
    }

    public abstract class StandardAlgorithmParameters : IAlgorithmParameters
    {
        [JsonIgnore]
        public virtual int SchemaVersion => 1;

        public abstract AlgorithmValidationResult Validate();

        protected static void Range(AlgorithmValidationResult result, string path, double value, double minimum, double maximum)
        {
            if (!double.IsFinite(value) || value < minimum || value > maximum)
                result.Add(path, "out_of_range", $"{path} must be between {minimum} and {maximum}.");
        }

        protected static void Odd(AlgorithmValidationResult result, string path, int value, int minimum, int maximum)
        {
            if (value < minimum || value > maximum || value % 2 == 0)
                result.Add(path, "invalid_odd_value", $"{path} must be an odd number between {minimum} and {maximum}.");
        }
    }

    public sealed class NoAlgorithmParameters : StandardAlgorithmParameters
    {
        public override AlgorithmValidationResult Validate() => AlgorithmValidationResult.Valid();
    }

    public sealed class ImageComparisonParameters : StandardAlgorithmParameters
    {
        [Browsable(false), JsonIgnore]
        public override int SchemaVersion => 2;

        [DisplayName("统计包含 Alpha 通道")]
        public bool IncludeAlphaInMetrics { get; set; } = true;

        [DisplayName("浮点图像峰值")]
        public double FloatPeakValue { get; set; } = 1;

        [DisplayName("热力图最大差值（0 使用标称峰值）")]
        public double HeatmapMaximum { get; set; }

        [DisplayName("计算 SSIM")]
        public bool EnableSsim { get; set; } = true;

        [DisplayName("SSIM 窗口大小（奇数）")]
        public int SsimWindowSize { get; set; } = 11;

        [DisplayName("SSIM K1")]
        public double SsimK1 { get; set; } = 0.01;

        [DisplayName("SSIM K2")]
        public double SsimK2 { get; set; } = 0.03;

        [DisplayName("SSIM 最小有效窗口比例")]
        public double SsimMinimumValidFraction { get; set; } = 0.5;

        [DisplayName("执行对齐预检")]
        public bool EnableAlignmentPrecheck { get; set; } = true;

        [DisplayName("对齐预检搜索半径 (px)")]
        public int AlignmentSearchRadius { get; set; } = 8;

        [DisplayName("平移警告阈值 (px)")]
        public double AlignmentWarningThresholdPixels { get; set; } = 0.5;

        [DisplayName("最小重叠比例")]
        public double AlignmentMinimumOverlapFraction { get; set; } = 0.75;

        [DisplayName("对齐预检最大采样数")]
        public int AlignmentMaximumSamples { get; set; } = 4096;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            if (!double.IsFinite(FloatPeakValue) || FloatPeakValue <= 0)
                result.Add(nameof(FloatPeakValue), "invalid_float_peak", "FloatPeakValue must be positive and finite.");
            if (!double.IsFinite(HeatmapMaximum) || HeatmapMaximum < 0)
                result.Add(nameof(HeatmapMaximum), "invalid_heatmap_maximum", "HeatmapMaximum must be zero or positive and finite.");
            Odd(result, nameof(SsimWindowSize), SsimWindowSize, 3, 255);
            Range(result, nameof(SsimK1), SsimK1, 0.000001, 1);
            Range(result, nameof(SsimK2), SsimK2, 0.000001, 1);
            Range(result, nameof(SsimMinimumValidFraction), SsimMinimumValidFraction, 0.01, 1);
            if (AlignmentSearchRadius is < 0 or > 32)
                result.Add(nameof(AlignmentSearchRadius), "out_of_range", "AlignmentSearchRadius must be between 0 and 32.");
            Range(result, nameof(AlignmentWarningThresholdPixels), AlignmentWarningThresholdPixels, 0, 64);
            Range(result, nameof(AlignmentMinimumOverlapFraction), AlignmentMinimumOverlapFraction, 0.1, 1);
            if (AlignmentMaximumSamples is < 256 or > 100_000)
                result.Add(nameof(AlignmentMaximumSamples), "out_of_range", "AlignmentMaximumSamples must be between 256 and 100000.");
            return result;
        }
    }

    public sealed class CannyParameters : StandardAlgorithmParameters
    {
        [DisplayName("低阈值")]
        public double LowThreshold { get; set; } = 50;

        [DisplayName("高阈值")]
        public double HighThreshold { get; set; } = 150;

        [DisplayName("Sobel 核大小")]
        public int ApertureSize { get; set; } = 3;

        [DisplayName("使用 L2 梯度")]
        public bool L2Gradient { get; set; }

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            Range(result, nameof(LowThreshold), LowThreshold, 0, 255);
            Range(result, nameof(HighThreshold), HighThreshold, 0, 255);
            if (LowThreshold > HighThreshold) result.Add(nameof(LowThreshold), "threshold_order", "LowThreshold cannot exceed HighThreshold.");
            if (ApertureSize is not 3 and not 5 and not 7) result.Add(nameof(ApertureSize), "invalid_aperture", "ApertureSize must be 3, 5 or 7.");
            return result;
        }
    }

    public sealed class BasicAdjustmentParameters : StandardAlgorithmParameters
    {
        [DisplayName("曝光 (EV)")]
        public double Exposure { get; set; }

        [DisplayName("亮度偏移 %")]
        public double Brightness { get; set; }

        [DisplayName("对比度 %")]
        public double Contrast { get; set; }

        [DisplayName("Gamma")]
        public double Gamma { get; set; } = 1;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            Range(result, nameof(Exposure), Exposure, -5, 5);
            Range(result, nameof(Brightness), Brightness, -100, 100);
            Range(result, nameof(Contrast), Contrast, -100, 100);
            Range(result, nameof(Gamma), Gamma, 0.1, 5);
            return result;
        }
    }

    public sealed class ThresholdParameters : StandardAlgorithmParameters
    {
        [DisplayName("阈值")]
        public double Threshold { get; set; } = 128;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            Range(result, nameof(Threshold), Threshold, 0, ushort.MaxValue);
            return result;
        }
    }

    public sealed class GaussianBlurParameters : StandardAlgorithmParameters
    {
        [DisplayName("核大小（奇数）")]
        public int KernelSize { get; set; } = 5;

        [DisplayName("Sigma")]
        public double Sigma { get; set; } = 1.5;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            Odd(result, nameof(KernelSize), KernelSize, 1, 255);
            Range(result, nameof(Sigma), Sigma, 0, 1000);
            return result;
        }
    }

    public sealed class MedianBlurParameters : StandardAlgorithmParameters
    {
        [DisplayName("核大小（奇数）")]
        public int KernelSize { get; set; } = 5;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            Odd(result, nameof(KernelSize), KernelSize, 3, 255);
            return result;
        }
    }

    public enum StandardMorphologyOperation
    {
        Erode,
        Dilate,
        Open,
        Close,
        Gradient,
        TopHat,
        BlackHat,
    }

    public sealed class MorphologyParameters : StandardAlgorithmParameters
    {
        [DisplayName("操作")]
        public StandardMorphologyOperation Operation { get; set; }

        [DisplayName("核大小（奇数）")]
        public int KernelSize { get; set; } = 3;

        [DisplayName("迭代次数")]
        public int Iterations { get; set; } = 1;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            if (!Enum.IsDefined(Operation)) result.Add(nameof(Operation), "invalid_enum", "Unknown morphology operation.");
            Odd(result, nameof(KernelSize), KernelSize, 1, 255);
            if (Iterations is < 1 or > 100) result.Add(nameof(Iterations), "out_of_range", "Iterations must be between 1 and 100.");
            return result;
        }
    }

    public enum StandardDenoiseOperation
    {
        Bilateral,
        MeanBlur,
    }

    public sealed class DenoiseParameters : StandardAlgorithmParameters
    {
        [DisplayName("滤波类型")]
        public StandardDenoiseOperation Operation { get; set; }

        [DisplayName("核大小（奇数）")]
        public int KernelSize { get; set; } = 5;

        [DisplayName("颜色 Sigma")]
        public double SigmaColor { get; set; } = 75;

        [DisplayName("空间 Sigma")]
        public double SigmaSpace { get; set; } = 75;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            if (!Enum.IsDefined(Operation)) result.Add(nameof(Operation), "invalid_enum", "Unknown denoise operation.");
            Odd(result, nameof(KernelSize), KernelSize, 1, 255);
            Range(result, nameof(SigmaColor), SigmaColor, 0, 10000);
            Range(result, nameof(SigmaSpace), SigmaSpace, 0, 10000);
            return result;
        }
    }

    public sealed class WhiteBalanceParameters : StandardAlgorithmParameters
    {
        [DisplayName("红色通道系数")]
        public double RedScale { get; set; } = 1;

        [DisplayName("绿色通道系数")]
        public double GreenScale { get; set; } = 1;

        [DisplayName("蓝色通道系数")]
        public double BlueScale { get; set; } = 1;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            Range(result, nameof(RedScale), RedScale, 0, 16);
            Range(result, nameof(GreenScale), GreenScale, 0, 16);
            Range(result, nameof(BlueScale), BlueScale, 0, 16);
            return result;
        }
    }

    public enum StandardPseudoColorMap
    {
        Autumn = 0,
        Bone = 1,
        Jet = 2,
        Winter = 3,
        Rainbow = 4,
        Ocean = 5,
        Summer = 6,
        Spring = 7,
        Cool = 8,
        Hsv = 9,
        Pink = 10,
        Hot = 11,
        Parula = 12,
        Magma = 13,
        Inferno = 14,
        Plasma = 15,
        Viridis = 16,
        Cividis = 17,
        Twilight = 18,
        TwilightShifted = 19,
        Turbo = 20,
        DeepGreen = 21,
    }

    public sealed class PseudoColorParameters : StandardAlgorithmParameters
    {
        [DisplayName("使用位深标称范围")]
        public bool UseNominalRange { get; set; } = true;

        [DisplayName("色图")]
        public StandardPseudoColorMap Colormap { get; set; } = StandardPseudoColorMap.Jet;

        [DisplayName("最小值")]
        public uint Minimum { get; set; }

        [DisplayName("最大值")]
        public uint Maximum { get; set; } = 255;

        [DisplayName("源通道（-1 为灰度）")]
        public int Channel { get; set; } = -1;

        [DisplayName("按数据范围拉伸")]
        public bool AutoRange { get; set; }

        [Browsable(false)]
        public uint DataMinimum { get; set; }

        [Browsable(false)]
        public uint DataMaximum { get; set; }

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            if (!Enum.IsDefined(Colormap)) result.Add(nameof(Colormap), "invalid_enum", "Unknown color map.");
            if (Minimum > Maximum) result.Add(nameof(Minimum), "range_order", "Minimum cannot exceed Maximum.");
            if (Channel is < -1 or > 3) result.Add(nameof(Channel), "channel_out_of_range", "Channel must be -1 or an index from 0 to 3.");
            if (AutoRange && DataMinimum >= DataMaximum) result.Add(nameof(DataMinimum), "invalid_data_range", "AutoRange requires DataMinimum to be less than DataMaximum.");
            return result;
        }
    }
}
