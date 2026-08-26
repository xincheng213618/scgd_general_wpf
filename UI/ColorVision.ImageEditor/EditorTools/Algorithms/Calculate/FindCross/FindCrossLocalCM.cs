using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using ColorVision.Util.Draw.Rectangle;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.FindCross
{
    /// <summary>
    /// Production-facing FindCross configuration. Detection thresholds, polarity,
    /// processing resolution and the rotation estimator are a versioned algorithm
    /// profile rather than operator-tunable recipe parameters.
    /// </summary>
    public sealed class FindCrossImageViewOptions : ViewModelBase
    {
        [Category("Pattern"), DisplayName("预期角度 (°)")]
        [Description("用于消除十字的 90° 方向歧义；通常填产品期望的零位角。")]
        public double ExpectedAngleDegrees { get; set; }

        [Category("Pattern"), DisplayName("最大允许旋转偏差 (±°)")]
        [Description("限定相对产品名义角度的搜索范围，也用于排除方向错误的 Pattern。")]
        public double AngleToleranceDegrees { get; set; } = 10;

        [Category("光学"), DisplayName("标准中心使用图像中心")]
        [Description("启用时自动使用当前图像中心计算倾角。关闭后使用下方现场标定中心。")]
        public bool UseImageCenterAsStandard
        {
            get => _useImageCenterAsStandard;
            set { _useImageCenterAsStandard = value; OnPropertyChanged(); }
        }
        private bool _useImageCenterAsStandard = true;

        [Category("光学"), DisplayName("标准中心 X")]
        [PropertyVisibility(nameof(UseImageCenterAsStandard), true)]
        public double StandardCenterX { get; set; } = 4784;

        [Category("光学"), DisplayName("标准中心 Y")]
        [PropertyVisibility(nameof(UseImageCenterAsStandard), true)]
        public double StandardCenterY { get; set; } = 3190;

        [Category("光学"), DisplayName("焦距 (mm)")]
        public double FocusLengthMillimeters { get; set; } = 25.4;

        [Category("光学"), DisplayName("像元尺寸 (μm)")]
        public double SensorPixelSizeMicrometers { get; set; } = 3.76;

        [Category("光学"), DisplayName("启用镜头畸变校正")]
        [Description("仅在已有完整相机标定时启用；需要 Brown k1/k2/p1/p2/k3 以及 Fx/Fy/Cx/Cy。")]
        public bool EnableDistortionCorrection { get => _enableDistortionCorrection; set { _enableDistortionCorrection = value; OnPropertyChanged(); } }
        private bool _enableDistortionCorrection;

        [Category("光学"), DisplayName("畸变 K1"), PropertyVisibility(nameof(EnableDistortionCorrection))]
        public double DistortionK1 { get; set; }

        [Category("光学"), DisplayName("畸变 K2"), PropertyVisibility(nameof(EnableDistortionCorrection))]
        public double DistortionK2 { get; set; }

        [Category("光学"), DisplayName("畸变 P1"), PropertyVisibility(nameof(EnableDistortionCorrection))]
        public double DistortionP1 { get; set; }

        [Category("光学"), DisplayName("畸变 P2"), PropertyVisibility(nameof(EnableDistortionCorrection))]
        public double DistortionP2 { get; set; }

        [Category("光学"), DisplayName("畸变 K3"), PropertyVisibility(nameof(EnableDistortionCorrection))]
        public double DistortionK3 { get; set; }

        [Category("光学"), DisplayName("畸变内参 Fx (px)"), PropertyVisibility(nameof(EnableDistortionCorrection))]
        [Description("必须填写现场相机标定得到的 Fx/Fy/Cx/Cy；不再用名义焦距或倾角标准中心代替镜头内参。")]
        public double DistortionFxPixels { get; set; }

        [Category("光学"), DisplayName("畸变内参 Fy (px)"), PropertyVisibility(nameof(EnableDistortionCorrection))]
        public double DistortionFyPixels { get; set; }

        [Category("光学"), DisplayName("镜头主点 Cx (px)"), PropertyVisibility(nameof(EnableDistortionCorrection))]
        [Description("这是镜头标定主点，不是产线倾角标准中心。")]
        public double DistortionPrincipalPointX { get; set; }

        [Category("光学"), DisplayName("镜头主点 Cy (px)"), PropertyVisibility(nameof(EnableDistortionCorrection))]
        public double DistortionPrincipalPointY { get; set; }

        [Category("显示"), DisplayName("弹窗显示结果")]
        [Description("默认只在图像上回显；调试时可额外弹出数值摘要。")]
        public bool ShowResultDialog { get; set; }

        internal FindCrossLocalOptions ToOptions()
        {
            return new FindCrossLocalOptions
            {
                ExpectedAngleDegrees = ExpectedAngleDegrees,
                AngleToleranceDegrees = AngleToleranceDegrees,
                Name = "Point_1",
                Optics = new FindCrossLocalOpticsOptions
                {
                    StandardCenter = UseImageCenterAsStandard
                        ? null
                        : new FindCrossLocalPoint(StandardCenterX, StandardCenterY),
                    FocusLengthMillimeters = FocusLengthMillimeters,
                    SensorPixelSizeMicrometers = SensorPixelSizeMicrometers,
                    Distortion = EnableDistortionCorrection
                        ? new FindCrossLocalDistortionOptions
                        {
                            Enabled = true,
                            K1 = DistortionK1,
                            K2 = DistortionK2,
                            P1 = DistortionP1,
                            P2 = DistortionP2,
                            K3 = DistortionK3,
                            FxPixels = DistortionFxPixels,
                            FyPixels = DistortionFyPixels,
                            PrincipalPointX = DistortionPrincipalPointX,
                            PrincipalPointY = DistortionPrincipalPointY
                        }
                        : null
                }
            };
        }
    }

    internal static class FindCrossImageViewOptionsStore
    {
        private static readonly ConditionalWeakTable<ImageProcessingContext, FindCrossImageViewOptions> Options = new();

        public static FindCrossImageViewOptions Get(ImageProcessingContext imageContext) =>
            Options.GetValue(imageContext, static _ => new FindCrossImageViewOptions());
    }

    internal static class FindCrossImageViewRunner
    {
        public static void Run(
            ImageProcessingContext imageContext,
            DrawEditorContext drawContext,
            RoiRect requestedRoi,
            FindCrossImageViewOptions uiOptions)
        {
            FindCrossLocalOptions options = uiOptions.ToOptions();
            if (!options.TryValidate(out string validationError))
            {
                MessageBox.Show(validationError, "本地 FindCross 参数无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            AlgorithmResultOverlay.ClearTagged(drawContext, AlgorithmResultOverlay.FindCrossTag);
            long requestId = AlgorithmResultOverlay.BeginRequest(drawContext, AlgorithmResultOverlay.FindCrossTag);

            ImageFrameLease? lease = imageContext.AcquireImageFrame();
            if (lease == null) return;
            if (!TryNormalizeRoi(requestedRoi, lease.Image, out RoiRect roi))
            {
                lease.Dispose();
                MessageBox.Show("所选 ROI 与当前图像没有有效交集。", "本地 FindCross", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            long revision = lease.Revision;
            _ = Task.Run(() =>
            {
                try
                {
                    FindCrossLocalResult result;
                    using (lease)
                    {
                        result = ColorVision.Core.FindCrossLocal.Run(lease.Image, roi, options);
                    }

                    imageContext.Dispatcher.BeginInvoke(() =>
                    {
                        if (!imageContext.IsCurrentImageRevision(revision) ||
                            !AlgorithmResultOverlay.IsCurrentRequest(drawContext, AlgorithmResultOverlay.FindCrossTag, requestId)) return;

                        if (!result.Success || result.Items.Count == 0)
                        {
                            MessageBox.Show(GetFailureMessage(result), "本地 FindCross", MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }

                        RenderResult(imageContext, drawContext, result);
                        if (uiOptions.ShowResultDialog)
                        {
                            MessageBox.Show(BuildSummary(result), "本地 FindCross 结果", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    });
                }
                catch (Exception ex)
                {
                    imageContext.Dispatcher.BeginInvoke(() =>
                    {
                        if (!imageContext.IsCurrentImageRevision(revision) ||
                            !AlgorithmResultOverlay.IsCurrentRequest(drawContext, AlgorithmResultOverlay.FindCrossTag, requestId)) return;

                        MessageBox.Show($"本地 FindCross 计算异常：{ex.Message}", "本地 FindCross", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }

        private static void RenderResult(
            ImageProcessingContext imageContext,
            DrawEditorContext drawContext,
            FindCrossLocalResult result)
        {
            AlgorithmResultOverlay.ClearTagged(drawContext, AlgorithmResultOverlay.FindCrossTag);

            double pixelToDipX = LuminousAreaDetector.GetPixelToDipScale(
                imageContext.Config.GetProperties<double>(ImageViewPropertyKeys.DpiX));
            double pixelToDipY = LuminousAreaDetector.GetPixelToDipScale(
                imageContext.Config.GetProperties<double>(ImageViewPropertyKeys.DpiY));
            double zoom = AlgorithmResultOverlay.GetZoom(drawContext);
            Pen outlinePen = new(Brushes.LimeGreen, 1.5 / zoom);

            IReadOnlyList<FindCrossLocalPoint> corners = result.Diagnostics.Corners;
            if (corners.Count >= 4)
            {
                Point[] cornerPoints = corners
                    .Take(4)
                    .Select(point => ToDip(point, pixelToDipX, pixelToDipY))
                    .ToArray();
                AlgorithmResultOverlay.AddPolygon(
                    drawContext,
                    cornerPoints,
                    outlinePen,
                    AlgorithmResultOverlay.FindCrossTag);
            }

            FindCrossLocalItem item = result.Items[0];
            FindCrossLocalPoint outputCenter = result.Diagnostics.CenterSubpixel ?? item.Center;
            FindCrossLocalPoint rawCenter = result.Diagnostics.RawGeometricCenter ?? outputCenter;
            bool hasCoordinateCorrection = result.Diagnostics.DistortionApplied == true ||
                (result.Diagnostics.AppliedOffset is FindCrossLocalPoint offset &&
                    (Math.Abs(offset.X) > 1e-9 || Math.Abs(offset.Y) > 1e-9));
            IReadOnlyList<FindCrossLocalPoint> rawEndpoints = result.Diagnostics.RawArmEndpoints.Count >= 4
                ? result.Diagnostics.RawArmEndpoints
                : result.Diagnostics.ArmEndpoints;
            if (rawEndpoints.Count >= 4)
            {
                AlgorithmResultOverlay.AddLine(
                    drawContext,
                    ToDip(rawEndpoints[0], pixelToDipX, pixelToDipY),
                    ToDip(rawEndpoints[1], pixelToDipX, pixelToDipY),
                    outlinePen.CloneCurrentValue(),
                    AlgorithmResultOverlay.FindCrossTag);
                AlgorithmResultOverlay.AddLine(
                    drawContext,
                    ToDip(rawEndpoints[2], pixelToDipX, pixelToDipY),
                    ToDip(rawEndpoints[3], pixelToDipX, pixelToDipY),
                    outlinePen.CloneCurrentValue(),
                    AlgorithmResultOverlay.FindCrossTag);
            }
            else
            {
                double armLengthPixels = Math.Clamp(Math.Min(item.W, item.H) * 0.10, 24, 320);
                double angleRadians = item.RotationAngle * Math.PI / 180.0;
                AddAxis(drawContext, rawCenter, angleRadians, armLengthPixels, pixelToDipX, pixelToDipY, outlinePen);
                AddAxis(drawContext, rawCenter, angleRadians + Math.PI / 2, armLengthPixels, pixelToDipX, pixelToDipY, outlinePen);
            }

            if (hasCoordinateCorrection)
            {
                Pen correctedPen = new(Brushes.Gold, 1.5 / zoom);
                IReadOnlyList<FindCrossLocalPoint> correctedEndpoints = result.Diagnostics.ArmEndpoints;
                if (result.Diagnostics.DistortionApplied == true && correctedEndpoints.Count >= 4)
                {
                    AlgorithmResultOverlay.AddLine(
                        drawContext,
                        ToDip(correctedEndpoints[0], pixelToDipX, pixelToDipY),
                        ToDip(correctedEndpoints[1], pixelToDipX, pixelToDipY),
                        correctedPen.CloneCurrentValue(),
                        AlgorithmResultOverlay.FindCrossTag);
                    AlgorithmResultOverlay.AddLine(
                        drawContext,
                        ToDip(correctedEndpoints[2], pixelToDipX, pixelToDipY),
                        ToDip(correctedEndpoints[3], pixelToDipX, pixelToDipY),
                        correctedPen.CloneCurrentValue(),
                        AlgorithmResultOverlay.FindCrossTag);
                }
                double outputMarkerLength = Math.Clamp(Math.Min(item.W, item.H) * 0.015, 10, 40);
                double outputAngleRadians = item.RotationAngle * Math.PI / 180.0;
                AddAxis(drawContext, outputCenter, outputAngleRadians, outputMarkerLength, pixelToDipX, pixelToDipY, correctedPen);
                AddAxis(drawContext, outputCenter, outputAngleRadians + Math.PI / 2, outputMarkerLength, pixelToDipX, pixelToDipY, correctedPen);
                AlgorithmResultOverlay.AddLabel(
                    drawContext,
                    ToDip(rawCenter, pixelToDipX, pixelToDipY),
                    $"raw evidence center=({rawCenter.X:F3}, {rawCenter.Y:F3})",
                    Brushes.LimeGreen,
                    AlgorithmResultOverlay.FindCrossTag);
            }

            Brush messageBrush = result.Diagnostics.Warnings.Count == 0 ? Brushes.LimeGreen : Brushes.Orange;
            AlgorithmResultOverlay.AddLabel(
                drawContext,
                ToDip(outputCenter, pixelToDipX, pixelToDipY),
                BuildSummary(result),
                hasCoordinateCorrection ? Brushes.Gold : messageBrush,
                AlgorithmResultOverlay.FindCrossTag);
        }

        private static void AddAxis(
            DrawEditorContext drawContext,
            FindCrossLocalPoint center,
            double angleRadians,
            double armLengthPixels,
            double pixelToDipX,
            double pixelToDipY,
            Pen pen)
        {
            double dx = Math.Cos(angleRadians) * armLengthPixels;
            double dy = Math.Sin(angleRadians) * armLengthPixels;
            Point start = new((center.X - dx) * pixelToDipX, (center.Y - dy) * pixelToDipY);
            Point end = new((center.X + dx) * pixelToDipX, (center.Y + dy) * pixelToDipY);
            AlgorithmResultOverlay.AddLine(drawContext, start, end, pen.CloneCurrentValue(), AlgorithmResultOverlay.FindCrossTag);
        }

        private static Point ToDip(FindCrossLocalPoint point, double pixelToDipX, double pixelToDipY) =>
            new(point.X * pixelToDipX, point.Y * pixelToDipY);

        private static string BuildSummary(FindCrossLocalResult result)
        {
            FindCrossLocalItem item = result.Items[0];
            FindCrossLocalPoint center = result.Diagnostics.CenterSubpixel ?? item.Center;
            string confidence = result.Diagnostics.Confidence.HasValue
                ? result.Diagnostics.Confidence.Value.ToString("F3")
                : "N/A";
            string summary =
                $"{item.Name}  output center=({center.X:F3}, {center.Y:F3})  angle={item.RotationAngle:F4}°\n" +
                $"tilt=({item.TiltX:F4}°, {item.TiltY:F4}°)  confidence={confidence}";
            if (!string.IsNullOrWhiteSpace(result.Diagnostics.PatternPolarity))
            {
                summary += $"  polarity={result.Diagnostics.PatternPolarity}";
            }
            if (result.Diagnostics.PatternContrast.HasValue)
            {
                summary += $"  contrast={result.Diagnostics.PatternContrast.Value:F4}";
            }
            if (result.Diagnostics.OrthogonalityError.HasValue)
            {
                summary += $"\northogonality error={result.Diagnostics.OrthogonalityError.Value:F4}°";
            }
            if (result.Diagnostics.DistortionApplied == true)
            {
                summary += "  distortion=corrected";
            }
            if (result.Diagnostics.Warnings.Count > 0)
            {
                summary += $"\nwarning: {string.Join(", ", result.Diagnostics.Warnings)}";
            }
            return summary;
        }

        private static string GetFailureMessage(FindCrossLocalResult result)
        {
            string message = result.FailureReason switch
            {
                "NoSignal" => "未检测到有效发光信号。",
                "NoCandidate" or "NoPatternCandidate" => "未找到具有四条有效长臂的十字 Pattern；Pattern 可能未显示。",
                "AmbiguousPattern" => "检测到多个相近十字候选，无法唯一定位。",
                "PatternClipped" => "十字 Pattern 被 ROI 或图像边界裁切。",
                "InsufficientArmSupport" => "十字至少一条臂的长度或连续性不足。",
                "InsufficientFullResolutionInliers" => "十字原分辨率精修的有效采样点不足。",
                "LowPatternContrast" => "十字 Pattern 与背景的对比度不足。",
                "PoorLineFit" => "候选不像两条稳定直线，可能是暗斑或异常光。",
                "NonOrthogonalAxes" => "候选两轴不接近垂直，已拒绝输出。",
                "UnstableRefinement" => "粗定位与原图精修差异过大，结果不稳定。",
                "InvalidDistortionGeometry" => "镜头畸变标定在当前 ROI 产生了退化或越界映射，请检查 Fx/Fy/Cx/Cy 和畸变系数。",
                "LowConfidence" => "结果可信度低于配置要求。",
                "InvalidConfiguration" or "InvalidConfigurationJson" or "NativeConfigurationInvalid" => "FindCross 参数无效。",
                "NativeLibraryUnavailable" => "找不到本地 FindCross 算法库。",
                "NativeEntryPointUnavailable" => "当前本地算法库不包含 FindCross 接口。",
                "NativeLibraryIncompatible" => "本地算法库与当前程序架构不兼容。",
                "ResultParseFailed" => "本地 FindCross 返回了无效结果。",
                "" => "本地 FindCross 定位失败。",
                _ => $"本地 FindCross 定位失败：{result.FailureReason}。"
            };
            if (result.NativeReturnCode < 0)
            {
                message += $" 返回码：{result.NativeReturnCode}。";
            }
            if (!string.IsNullOrWhiteSpace(result.InteropDiagnostic))
            {
                message += $"\n{result.InteropDiagnostic}";
            }
            return message;
        }

        internal static bool TryNormalizeRoi(RoiRect requestedRoi, HImage image, out RoiRect roi)
        {
            roi = new RoiRect();
            if (image.cols <= 0 || image.rows <= 0) return false;
            if (requestedRoi.Width <= 0 || requestedRoi.Height <= 0)
            {
                roi = new RoiRect(0, 0, image.cols, image.rows);
                return true;
            }

            long left = Math.Max(0L, requestedRoi.X);
            long top = Math.Max(0L, requestedRoi.Y);
            long right = Math.Min((long)image.cols, (long)requestedRoi.X + requestedRoi.Width);
            long bottom = Math.Min((long)image.rows, (long)requestedRoi.Y + requestedRoi.Height);
            if (right <= left || bottom <= top) return false;

            roi = new RoiRect((int)left, (int)top, (int)(right - left), (int)(bottom - top));
            return true;
        }
    }

    public sealed class DVCMFindCrossLocal : IDVContextMenu
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;
        private readonly ImageViewConfig _config;
        private readonly FindCrossImageViewOptions _options;

        public DVCMFindCrossLocal(
            ImageProcessingContext imageContext,
            DrawEditorContext drawContext,
            ImageViewConfig config)
        {
            _imageContext = imageContext;
            _drawContext = drawContext;
            _config = config;
            _options = FindCrossImageViewOptionsStore.Get(imageContext);
        }

        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            if (obj is not IRectangle rectangle || !TryGetRoi(rectangle, out RoiRect roi))
            {
                return Array.Empty<MenuItem>();
            }

            MenuItem menuItem = new() { Header = "本地 FindCross..." };
            menuItem.Click += (_, _) => ShowOptionsAndRun(roi);
            return new[] { menuItem };
        }

        private bool TryGetRoi(IRectangle rectangle, out RoiRect roi)
        {
            roi = new RoiRect();
            using ImageFrameLease? lease = _imageContext.AcquireImageFrame();
            if (lease == null) return false;

            double dipToPixelX = LuminousAreaDetector.GetDipToPixelScale(
                _config.GetProperties<double>(ImageViewPropertyKeys.DpiX));
            double dipToPixelY = LuminousAreaDetector.GetDipToPixelScale(
                _config.GetProperties<double>(ImageViewPropertyKeys.DpiY));
            double left = rectangle.Rect.Left * dipToPixelX;
            double top = rectangle.Rect.Top * dipToPixelY;
            double right = rectangle.Rect.Right * dipToPixelX;
            double bottom = rectangle.Rect.Bottom * dipToPixelY;
            RoiRect requested = new(
                (int)Math.Floor(left),
                (int)Math.Floor(top),
                (int)Math.Ceiling(right) - (int)Math.Floor(left),
                (int)Math.Ceiling(bottom) - (int)Math.Floor(top));
            return FindCrossImageViewRunner.TryNormalizeRoi(requested, lease.Image, out roi);
        }

        private void ShowOptionsAndRun(RoiRect roi)
        {
            PropertyEditorWindow window = new(_options)
            {
                Title = "本地 FindCross 参数",
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Submitted += (_, _) => FindCrossImageViewRunner.Run(_imageContext, _drawContext, roi, _options);
            window.ShowDialog();
        }
    }

    public sealed class CMFindCrossLocal : IIEditorToolContextMenu
    {
        private readonly ImageProcessingContext _imageContext;
        private readonly DrawEditorContext _drawContext;
        private readonly FindCrossImageViewOptions _options;

        public CMFindCrossLocal(ImageProcessingContext imageContext, DrawEditorContext drawContext)
        {
            _imageContext = imageContext;
            _drawContext = drawContext;
            _options = FindCrossImageViewOptionsStore.Get(imageContext);
        }

        public List<MenuItemMetadata> GetContextMenuItems()
        {
            RelayCommand command = new(_ =>
            {
                PropertyEditorWindow window = new(_options)
                {
                    Title = "本地 FindCross 参数",
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                window.Submitted += (_, _) => FindCrossImageViewRunner.Run(_imageContext, _drawContext, new RoiRect(), _options);
                window.ShowDialog();
            });

            return new List<MenuItemMetadata>
            {
                new()
                {
                    OwnerGuid = "AlgorithmsCall",
                    GuidId = "FindCrossLocal",
                    Order = 2,
                    Header = "本地 FindCross...",
                    Command = command
                }
            };
        }
    }
}
