using ColorVision.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.ImageEditor
{
    public static class LuminousAreaDetector
    {
        public static LuminousAreaDetectionResult Detect(HImage image, RoiRect roi, FindLuminousAreaCorner config)
        {
            ArgumentNullException.ThrowIfNull(config);
            return config.Algorithm switch
            {
                LuminousAreaDetectionMode.RobustV2 => LuminousAreaNative.DetectV2(image, roi, config.MinConfidence),
                LuminousAreaDetectionMode.Legacy => LuminousAreaNative.DetectLegacy(image, roi, config.Threshold, config.UseRotatedRect),
                _ => LuminousAreaDetectionResultForUnsupportedMode(config.Algorithm)
            };
        }

        public static string GetFailureMessage(LuminousAreaDetectionResult result)
        {
            string description = result.FailureReason switch
            {
                "NoSignal" => "未检测到有效发光信号。",
                "NoCandidate" => "未找到满足条件的发光区候选。",
                "Saturated" => "发光区过曝，无法稳定定位。",
                "ClippedByImage" => "发光区被图像边界裁切。",
                "InsufficientSideSupport" => "至少一条边的有效证据不足。",
                "InsufficientIndependentGeometry" => "可见边不足以唯一确定发光区四边形。",
                "AmbiguousCandidates" => "存在多个相近候选，无法唯一定位。",
                "UnstableCorners" => "角点不稳定，已拒绝输出。",
                "InvalidGeometry" => "检测到的四边形几何关系无效。",
                "UnsupportedImage" => "当前图像格式不受发光区算法支持。",
                "LowConfidence" => "定位可信度低于配置要求。",
                "InvalidConfiguration" => "发光区定位配置无效。",
                "NativeLibraryUnavailable" => "找不到本地发光区算法库。",
                "NativeEntryPointUnavailable" => "当前本地算法库不包含鲁棒发光区定位接口。",
                "NativeLibraryIncompatible" => "本地算法库与当前程序架构不兼容。",
                "NativeCallFailed" => "本地发光区算法调用失败。",
                "ResultParseFailed" => "本地发光区算法返回了无效结果。",
                "ManagedInteropFailed" => "发光区定位托管调用失败。",
                "UnsupportedAlgorithm" => "不支持所选的发光区定位算法。",
                "UnknownFailure" or "" => "发光区定位失败。",
                _ => $"发光区定位失败：{result.FailureReason}。"
            };

            if (result.NativeReturnCode < 0)
            {
                description += $" 返回码：{result.NativeReturnCode}。";
            }
            return description;
        }

        public static string GetWarningMessage(LuminousAreaDetectionResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            if (!result.Success || result.Warnings.Count == 0)
            {
                return string.Empty;
            }

            string[] descriptions = result.Warnings
                .Distinct(StringComparer.Ordinal)
                .Select(warning => warning switch
                {
                    "AmbiguousCandidates" or "MultipleComparableCandidates" => "画面中存在多个相近候选",
                    "CandidateTouchesImageBorder" => "定位区域接触图像边界",
                    _ when warning.StartsWith("Inferred", StringComparison.Ordinal) => "至少一条边由其余几何证据推断",
                    _ when warning.StartsWith("Partial", StringComparison.Ordinal) => "至少一条边只有部分有效支持",
                    _ when warning.StartsWith("Weak", StringComparison.Ordinal) => "至少一条边的定位证据偏弱",
                    _ when warning.Contains("Contrast", StringComparison.Ordinal) => "至少一条边对比度偏低",
                    _ => warning
                })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            string confidence = result.Confidence.HasValue ? $"（可信度 {result.Confidence.Value:F3}）" : string.Empty;
            return $"定位已成功{confidence}，但{string.Join("；", descriptions)}。已保留结果，请复核覆盖层后再用于批量流程。";
        }

        public static MRect GetBoundingRect(LuminousAreaDetectionResult result)
        {
            if (!result.HasValidCorners)
            {
                throw new ArgumentException("A successful four-corner result is required.", nameof(result));
            }

            return GetBoundingRect(result.Corners);
        }

        public static MRect GetDipBoundingRect(LuminousAreaDetectionResult result, double dpiX, double dpiY)
        {
            if (!result.HasValidCorners)
            {
                throw new ArgumentException("A successful four-corner result is required.", nameof(result));
            }

            LuminousAreaPoint[] corners = new LuminousAreaPoint[result.Corners.Count];
            for (int index = 0; index < corners.Length; index++)
            {
                corners[index] = ConvertPixelToDip(result.Corners[index], dpiX, dpiY);
            }
            return GetBoundingRect(corners);
        }

        public static LuminousAreaPoint ConvertPixelToDip(LuminousAreaPoint point, double dpiX, double dpiY) =>
            new(point.X * GetPixelToDipScale(dpiX), point.Y * GetPixelToDipScale(dpiY));

        private static MRect GetBoundingRect(IReadOnlyList<LuminousAreaPoint> corners)
        {

            double minX = double.PositiveInfinity;
            double minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double maxY = double.NegativeInfinity;
            foreach (LuminousAreaPoint corner in corners)
            {
                minX = Math.Min(minX, corner.X);
                minY = Math.Min(minY, corner.Y);
                maxX = Math.Max(maxX, corner.X);
                maxY = Math.Max(maxY, corner.Y);
            }

            int left = (int)Math.Floor(minX);
            int top = (int)Math.Floor(minY);
            int right = (int)Math.Ceiling(maxX);
            int bottom = (int)Math.Ceiling(maxY);
            return new MRect { X = left, Y = top, Width = right - left, Height = bottom - top };
        }

        public static double GetDipToPixelScale(double dpi) =>
            double.IsFinite(dpi) && dpi > 0 ? dpi / 96.0 : 1.0;

        public static double GetPixelToDipScale(double dpi) =>
            1.0 / GetDipToPixelScale(dpi);

        private static LuminousAreaDetectionResult LuminousAreaDetectionResultForUnsupportedMode(LuminousAreaDetectionMode mode) =>
            LuminousAreaDetectionResult.CreateFailure("Unknown", "UnsupportedAlgorithm", diagnostic: mode.ToString());
    }
}
