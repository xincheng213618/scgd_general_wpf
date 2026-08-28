using ColorVision.Algorithms;
using System;
using System.ComponentModel;

namespace ColorVision.ImageEditor.Algorithms
{
    public enum LensDistortionPrincipalPointMode
    {
        ImageCenter,
        Explicit,
    }

    public enum LensDistortionOutputCameraMode
    {
        PreserveCalibratedIntrinsics,
        OptimalNewCameraMatrix,
    }

    /// <summary>Stable V1 Brown-Conrady pinhole-camera undistortion contract.</summary>
    public sealed class LensDistortionCorrectionParameters : StandardAlgorithmParameters
    {
        [Category("相机内参"), DisplayName("焦距 Fx (px)")]
        public double FxPixels { get; set; } = 1_000;

        [Category("相机内参"), DisplayName("焦距 Fy (px)")]
        public double FyPixels { get; set; } = 1_000;

        [Category("相机内参"), DisplayName("主点模式")]
        public LensDistortionPrincipalPointMode PrincipalPointMode { get; set; } = LensDistortionPrincipalPointMode.ImageCenter;

        [Category("相机内参"), DisplayName("主点 Cx (px)")]
        public double PrincipalPointX { get; set; }

        [Category("相机内参"), DisplayName("主点 Cy (px)")]
        public double PrincipalPointY { get; set; }

        [Category("Brown-Conrady 畸变"), DisplayName("径向 K1")]
        public double K1 { get; set; }

        [Category("Brown-Conrady 畸变"), DisplayName("径向 K2")]
        public double K2 { get; set; }

        [Category("Brown-Conrady 畸变"), DisplayName("切向 P1")]
        public double P1 { get; set; }

        [Category("Brown-Conrady 畸变"), DisplayName("切向 P2")]
        public double P2 { get; set; }

        [Category("Brown-Conrady 畸变"), DisplayName("径向 K3")]
        public double K3 { get; set; }

        [Category("Brown-Conrady 畸变"), DisplayName("有理模型 K4")]
        public double K4 { get; set; }

        [Category("Brown-Conrady 畸变"), DisplayName("有理模型 K5")]
        public double K5 { get; set; }

        [Category("Brown-Conrady 畸变"), DisplayName("有理模型 K6")]
        public double K6 { get; set; }

        [Category("输出相机"), DisplayName("输出内参策略")]
        public LensDistortionOutputCameraMode OutputCameraMode { get; set; } = LensDistortionOutputCameraMode.PreserveCalibratedIntrinsics;

        [Category("输出相机"), DisplayName("保留视场 Alpha")]
        [Description("仅用于 OptimalNewCameraMatrix；0 尽量只保留全有效像素，1 尽量保留全部源视场。")]
        public double OptimalAlpha { get; set; }

        [Category("输出相机"), DisplayName("输出主点居中")]
        public bool CenterOptimalPrincipalPoint { get; set; }

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

        [Category("有效区域"), DisplayName("最小有效像素比例")]
        public double MinimumValidFraction { get; set; } = 0.01;

        [Category("标定追溯"), DisplayName("标定来源")]
        public string CalibrationSource { get; set; } = "manual-entry";

        [Category("标定追溯"), DisplayName("标定版本")]
        public string CalibrationVersion { get; set; } = "unspecified";

        [Category("标定追溯"), DisplayName("标定校验和")]
        public string CalibrationChecksum { get; set; } = string.Empty;

        [Category("标定质量"), DisplayName("包含标定质量")]
        public bool HasCalibrationQuality { get; set; }

        [Category("标定质量"), DisplayName("标定 RMS (px)")]
        public double CalibrationRmsErrorPixels { get; set; }

        [Category("标定质量"), DisplayName("标定置信度")]
        public double CalibrationConfidence { get; set; }

        public override AlgorithmValidationResult Validate()
        {
            AlgorithmValidationResult result = new();
            Range(result, nameof(FxPixels), FxPixels, 0.000001, 1_000_000_000);
            Range(result, nameof(FyPixels), FyPixels, 0.000001, 1_000_000_000);
            if (!Enum.IsDefined(PrincipalPointMode)) result.Add(nameof(PrincipalPointMode), "invalid_enum", "PrincipalPointMode is invalid.");
            if (!Enum.IsDefined(OutputCameraMode)) result.Add(nameof(OutputCameraMode), "invalid_enum", "OutputCameraMode is invalid.");
            if (!Enum.IsDefined(Interpolation)) result.Add(nameof(Interpolation), "invalid_enum", "Interpolation is invalid.");
            if (!Enum.IsDefined(Border)) result.Add(nameof(Border), "invalid_enum", "Border is invalid.");
            if (PrincipalPointMode == LensDistortionPrincipalPointMode.Explicit)
            {
                Range(result, nameof(PrincipalPointX), PrincipalPointX, -1_000_000_000, 1_000_000_000);
                Range(result, nameof(PrincipalPointY), PrincipalPointY, -1_000_000_000, 1_000_000_000);
            }
            foreach ((string name, double value) in new[]
            {
                (nameof(K1), K1), (nameof(K2), K2), (nameof(P1), P1), (nameof(P2), P2),
                (nameof(K3), K3), (nameof(K4), K4), (nameof(K5), K5), (nameof(K6), K6),
            })
            {
                Range(result, name, value, -1_000_000, 1_000_000);
            }
            Range(result, nameof(OptimalAlpha), OptimalAlpha, 0, 1);
            Range(result, nameof(BorderChannel0), BorderChannel0, 0, 1);
            Range(result, nameof(BorderChannel1), BorderChannel1, 0, 1);
            Range(result, nameof(BorderChannel2), BorderChannel2, 0, 1);
            Range(result, nameof(BorderChannel3), BorderChannel3, 0, 1);
            Range(result, nameof(MinimumValidFraction), MinimumValidFraction, 0, 1);
            if (string.IsNullOrWhiteSpace(CalibrationSource) || CalibrationSource.Length > 1_024)
                result.Add(nameof(CalibrationSource), "invalid_calibration_source", "CalibrationSource is required and cannot exceed 1024 characters.");
            if (string.IsNullOrWhiteSpace(CalibrationVersion) || CalibrationVersion.Length > 128)
                result.Add(nameof(CalibrationVersion), "invalid_calibration_version", "CalibrationVersion is required and cannot exceed 128 characters.");
            if (CalibrationChecksum is null || CalibrationChecksum.Length > 256)
                result.Add(nameof(CalibrationChecksum), "invalid_calibration_checksum", "CalibrationChecksum cannot exceed 256 characters.");
            if (HasCalibrationQuality)
            {
                Range(result, nameof(CalibrationRmsErrorPixels), CalibrationRmsErrorPixels, 0, 1_000_000);
                Range(result, nameof(CalibrationConfidence), CalibrationConfidence, 0, 1);
            }
            else if (CalibrationRmsErrorPixels != 0 || CalibrationConfidence != 0)
            {
                result.Add(nameof(HasCalibrationQuality), "calibration_quality_flag_required", "Set HasCalibrationQuality before supplying RMS error or confidence.");
            }
            return result;
        }
    }
}
