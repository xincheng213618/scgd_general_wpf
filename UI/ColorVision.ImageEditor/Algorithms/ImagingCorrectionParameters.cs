using ColorVision.Algorithms;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms
{
    public enum InvalidReferencePixelPolicy
    {
        RejectInvocation,
        PreserveSource,
        FillConstant,
    }

    public enum ImagingCorrectionOutputRangePolicy
    {
        ClampToNominalRange,
        PreserveFloatingPoint,
    }

    /// <summary>
    /// Stable V1 contract for dark-frame, flat-field, residual shading and bad-pixel correction.
    /// File paths are host locator hints; providers consume only named image inputs.
    /// </summary>
    public sealed class ImagingCorrectionParameters : StandardAlgorithmParameters
    {
        [Category("校正阶段"), DisplayName("启用 Dark-frame")]
        public bool EnableDarkFrame { get; set; }

        [Category("校正阶段"), DisplayName("启用 Flat-field")]
        public bool EnableFlatField { get; set; }

        [Category("校正阶段"), DisplayName("启用 Shading / Non-uniformity")]
        public bool EnableShading { get; set; }

        [Category("校正阶段"), DisplayName("启用 Bad-pixel map")]
        public bool EnableBadPixelCorrection { get; set; }

        [Category("参考图像"), DisplayName("Dark-frame 路径")]
        public string DarkFramePath { get; set; } = string.Empty;

        [Category("参考图像"), DisplayName("Flat-field 路径")]
        public string FlatFieldPath { get; set; } = string.Empty;

        [Category("参考图像"), DisplayName("Shading 参考路径")]
        public string ShadingReferencePath { get; set; } = string.Empty;

        [Category("参考图像"), DisplayName("Bad-pixel map 路径")]
        public string BadPixelMapPath { get; set; } = string.Empty;

        [Category("参考保护"), DisplayName("零值保护阈值 (0..1)")]
        public double ReferenceZeroThresholdNormalized { get; set; } = 1e-6;

        [Category("参考保护"), DisplayName("拒绝过曝参考像素")]
        public bool RejectSaturatedReferencePixels { get; set; } = true;

        [Category("参考保护"), DisplayName("过曝阈值 (0..1)")]
        public double ReferenceSaturationThresholdNormalized { get; set; } = 0.999;

        [Category("参考保护"), DisplayName("最小有效参考比例")]
        public double MinimumValidReferenceFraction { get; set; } = 0.5;

        [Category("参考保护"), DisplayName("无效参考策略")]
        public InvalidReferencePixelPolicy InvalidReferencePolicy { get; set; } = InvalidReferencePixelPolicy.PreserveSource;

        [Category("参考保护"), DisplayName("无效参考填充值 (0..1)")]
        public double InvalidReferenceFillNormalized { get; set; }

        [Category("增益"), DisplayName("最小校正增益")]
        public double MinimumGain { get; set; } = 0;

        [Category("增益"), DisplayName("最大校正增益")]
        public double MaximumGain { get; set; } = 16;

        [Category("坏点"), DisplayName("坏点 map 阈值 (0..1)")]
        public double BadPixelThresholdNormalized { get; set; } = 0.5;

        [Category("坏点"), DisplayName("中值邻域半径")]
        public int BadPixelRadius { get; set; } = 1;

        [Category("输出"), DisplayName("校正 Alpha 通道")]
        public bool CorrectAlpha { get; set; }

        [Category("输出"), DisplayName("输出范围策略")]
        public ImagingCorrectionOutputRangePolicy OutputRangePolicy { get; set; } = ImagingCorrectionOutputRangePolicy.ClampToNominalRange;

        [Category("校正追溯"), DisplayName("校正来源")]
        public string CalibrationSource { get; set; } = "manual-reference-set";

        [Category("校正追溯"), DisplayName("校正版本")]
        public string CalibrationVersion { get; set; } = "unspecified";

        [Category("校正追溯"), DisplayName("校正集合校验和")]
        public string CalibrationChecksum { get; set; } = string.Empty;

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            if (!Enum.IsDefined(InvalidReferencePolicy)) result.Add(nameof(InvalidReferencePolicy), "invalid_enum", "InvalidReferencePolicy is invalid.");
            if (!Enum.IsDefined(OutputRangePolicy)) result.Add(nameof(OutputRangePolicy), "invalid_enum", "OutputRangePolicy is invalid.");
            Range(result, nameof(ReferenceZeroThresholdNormalized), ReferenceZeroThresholdNormalized, 0, 1);
            Range(result, nameof(ReferenceSaturationThresholdNormalized), ReferenceSaturationThresholdNormalized, 0, 1);
            if (RejectSaturatedReferencePixels && ReferenceSaturationThresholdNormalized <= ReferenceZeroThresholdNormalized)
                result.Add(nameof(ReferenceSaturationThresholdNormalized), "reference_threshold_order", "The saturation threshold must exceed the zero threshold.");
            Range(result, nameof(MinimumValidReferenceFraction), MinimumValidReferenceFraction, 0, 1);
            Range(result, nameof(InvalidReferenceFillNormalized), InvalidReferenceFillNormalized, 0, 1);
            Range(result, nameof(MinimumGain), MinimumGain, 0, 1_000_000);
            Range(result, nameof(MaximumGain), MaximumGain, 0.000001, 1_000_000);
            if (MinimumGain > MaximumGain) result.Add(nameof(MinimumGain), "gain_order", "MinimumGain cannot exceed MaximumGain.");
            Range(result, nameof(BadPixelThresholdNormalized), BadPixelThresholdNormalized, 0, 1);
            if (BadPixelRadius is < 1 or > 7) result.Add(nameof(BadPixelRadius), "out_of_range", "BadPixelRadius must be between 1 and 7.");
            ValidateText(result, nameof(DarkFramePath), DarkFramePath, 4_096, false);
            ValidateText(result, nameof(FlatFieldPath), FlatFieldPath, 4_096, false);
            ValidateText(result, nameof(ShadingReferencePath), ShadingReferencePath, 4_096, false);
            ValidateText(result, nameof(BadPixelMapPath), BadPixelMapPath, 4_096, false);
            ValidateText(result, nameof(CalibrationSource), CalibrationSource, 1_024, true);
            ValidateText(result, nameof(CalibrationVersion), CalibrationVersion, 128, true);
            ValidateText(result, nameof(CalibrationChecksum), CalibrationChecksum, 256, false);
            return result;
        }

        private static void ValidateText(AlgorithmValidationResult result, string path, string? value, int maximumLength, bool required)
        {
            if (value is null || value.Length > maximumLength || (required && string.IsNullOrWhiteSpace(value)))
                result.Add(path, "invalid_text", required
                    ? $"{path} is required and cannot exceed {maximumLength} characters."
                    : $"{path} cannot be null or exceed {maximumLength} characters.");
        }
    }
}
