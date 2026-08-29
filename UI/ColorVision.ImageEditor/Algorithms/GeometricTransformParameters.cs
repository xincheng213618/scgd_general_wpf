using ColorVision.Algorithms;
using System;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ColorVision.ImageEditor.Algorithms
{
    public enum GeometricTransformKind
    {
        Affine,
        Perspective,
    }

    public enum GeometricTransformCanvas
    {
        SourceSize,
        ExplicitSize,
        FitTransformedBounds,
    }

    public enum GeometricTransformInterpolation
    {
        Nearest,
        Linear,
    }

    public enum GeometricTransformBorder
    {
        Constant,
        Replicate,
    }

    /// <summary>Stable V1 source-to-destination projective transform contract.</summary>
    public sealed class GeometricTransformParameters : StandardAlgorithmParameters
    {
        [Category("变换"), DisplayName("变换类型")]
        public GeometricTransformKind Kind { get; set; } = GeometricTransformKind.Affine;

        [Category("变换"), DisplayName("矩阵 M11")]
        public double M11 { get; set; } = 1;

        [Category("变换"), DisplayName("矩阵 M12")]
        public double M12 { get; set; }

        [Category("变换"), DisplayName("矩阵 M13")]
        public double M13 { get; set; }

        [Category("变换"), DisplayName("矩阵 M21")]
        public double M21 { get; set; }

        [Category("变换"), DisplayName("矩阵 M22")]
        public double M22 { get; set; } = 1;

        [Category("变换"), DisplayName("矩阵 M23")]
        public double M23 { get; set; }

        [Category("变换"), DisplayName("矩阵 M31")]
        public double M31 { get; set; }

        [Category("变换"), DisplayName("矩阵 M32")]
        public double M32 { get; set; }

        [Category("变换"), DisplayName("矩阵 M33")]
        public double M33 { get; set; } = 1;

        [Category("输出"), DisplayName("画布策略")]
        public GeometricTransformCanvas Canvas { get; set; } = GeometricTransformCanvas.SourceSize;

        [Category("输出"), DisplayName("显式输出宽度")]
        public int OutputWidth { get; set; }

        [Category("输出"), DisplayName("显式输出高度")]
        public int OutputHeight { get; set; }

        [Category("输出"), DisplayName("自动包围留白 (px)")]
        public int FitPaddingPixels { get; set; }

        [Category("采样"), DisplayName("插值")]
        public GeometricTransformInterpolation Interpolation { get; set; } = GeometricTransformInterpolation.Linear;

        [Category("采样"), DisplayName("边界模式")]
        public GeometricTransformBorder Border { get; set; } = GeometricTransformBorder.Constant;

        [Category("采样"), DisplayName("边界 B/灰度 (0..1)")]
        public double BorderChannel0 { get; set; }

        [Category("采样"), DisplayName("边界 G (0..1)")]
        public double BorderChannel1 { get; set; }

        [Category("采样"), DisplayName("边界 R (0..1)")]
        public double BorderChannel2 { get; set; }

        [Category("采样"), DisplayName("边界 Alpha (0..1)")]
        public double BorderChannel3 { get; set; }

        [Category("资源"), DisplayName("最大输出像素数")]
        public long MaximumOutputPixels { get; set; } = 100_000_000;

        [Category("资源"), DisplayName("最大条件数")]
        public double MaximumConditionNumber { get; set; } = 1e12;

        [Browsable(false), JsonIgnore]
        public double[] Matrix => [M11, M12, M13, M21, M22, M23, M31, M32, M33];

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            if (!Enum.IsDefined(Kind)) result.Add(nameof(Kind), "invalid_enum", "Kind is invalid.");
            if (!Enum.IsDefined(Canvas)) result.Add(nameof(Canvas), "invalid_enum", "Canvas is invalid.");
            if (!Enum.IsDefined(Interpolation)) result.Add(nameof(Interpolation), "invalid_enum", "Interpolation is invalid.");
            if (!Enum.IsDefined(Border)) result.Add(nameof(Border), "invalid_enum", "Border is invalid.");
            double[] matrix = Matrix;
            for (int index = 0; index < matrix.Length; index++)
                Range(result, $"Matrix[{index}]", matrix[index], -1e12, 1e12);
            if (Kind == GeometricTransformKind.Affine
                && (M31 != 0 || M32 != 0 || M33 != 1))
            {
                result.Add("Matrix", "affine_bottom_row_invalid", "Affine transforms require a bottom row of [0, 0, 1].");
            }
            if (Canvas == GeometricTransformCanvas.ExplicitSize)
            {
                if (OutputWidth <= 0) result.Add(nameof(OutputWidth), "invalid_output_size", "OutputWidth must be positive for ExplicitSize.");
                if (OutputHeight <= 0) result.Add(nameof(OutputHeight), "invalid_output_size", "OutputHeight must be positive for ExplicitSize.");
            }
            else if (OutputWidth < 0 || OutputHeight < 0)
            {
                result.Add("OutputSize", "invalid_output_size", "OutputWidth and OutputHeight cannot be negative.");
            }
            if (FitPaddingPixels is < 0 or > 10_000)
                result.Add(nameof(FitPaddingPixels), "out_of_range", "FitPaddingPixels must be between 0 and 10000.");
            Range(result, nameof(BorderChannel0), BorderChannel0, 0, 1);
            Range(result, nameof(BorderChannel1), BorderChannel1, 0, 1);
            Range(result, nameof(BorderChannel2), BorderChannel2, 0, 1);
            Range(result, nameof(BorderChannel3), BorderChannel3, 0, 1);
            if (MaximumOutputPixels is < 1 or > 1_000_000_000)
                result.Add(nameof(MaximumOutputPixels), "out_of_range", "MaximumOutputPixels must be between 1 and 1000000000.");
            Range(result, nameof(MaximumConditionNumber), MaximumConditionNumber, 1, 1e18);
            return result;
        }
    }
}
