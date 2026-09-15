using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.Engine.PropertyEditor;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using ColorVision.UI;
using ColorVision.UI.Menus;
using ColorVision.Util.Draw.Rectangle;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Jsons.FOV2
{
    public sealed class FovImageViewOptions : ViewModelBase
    {
        private double fovDist = 9410;
        private double cameraDegrees = 74.2;
        private double minimumConfidence = 0.25;
        private double luminanceBoundaryRatio = FovLuminousAreaDetector.DefaultBoundaryRatio;

        [Category("FOV")]
        [DisplayName("FovDist")]
        [Description("FOV 计算系数，默认 9410；应与当前相机及镜头标定一致。")]
        public double FovDist
        {
            get => fovDist;
            set { fovDist = value; OnPropertyChanged(); }
        }

        [Category("FOV")]
        [DisplayName("cameraDegrees")]
        [Description("相机镜头有效像素范围对应的标定角度，默认 74.2，范围 0 到 180 度。")]
        [PropertyEditorType(typeof(CameraDegreesPropertiesEditor))]
        public double CameraDegrees
        {
            get => cameraDegrees;
            set { cameraDegrees = value; OnPropertyChanged(); }
        }

        [Category("发光区定位")]
        [DisplayName("最低可信度")]
        [Description("RobustV2 发光区定位结果低于此可信度时拒绝计算；范围 0 到 1。")]
        [Range(0d, 1d)]
        [PropertyEditorType(typeof(SliderPropertiesEditor))]
        public double MinimumConfidence
        {
            get => minimumConfidence;
            set
            {
                minimumConfidence = double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.25;
                OnPropertyChanged();
            }
        }

        // Compatibility read only; excluded from the normal FOV options.
        [Browsable(false)]
        public double LuminanceBoundaryRatio
        {
            get => luminanceBoundaryRatio;
            set { luminanceBoundaryRatio = value; OnPropertyChanged(); }
        }
    }

    internal sealed record FovImageViewRunResult
    {
        public FovCalculationResult? Calculation { get; init; }
        public string? Error { get; init; }
        public int TotalTime { get; init; }
    }

    internal static class FovImageViewRunner
    {
        private static readonly ConditionalWeakTable<ImageProcessingContext, FovImageViewOptions> Options = new();

        public static void ShowOptions(EditorContext editorContext, RoiRect roi)
        {
            ImageProcessingContext imageContext = editorContext.ProcessingContext;
            DrawEditorContext drawContext = editorContext.DrawEditorContext;
            AlgorithmResultOverlay.InvalidateRequest(drawContext, AlgorithmResultOverlay.FovTag);
            AlgorithmResultOverlay.ClearTagged(drawContext, AlgorithmResultOverlay.FovTag);

            FovImageViewOptions options = Options.GetValue(imageContext, static _ => new FovImageViewOptions());
            PropertyEditorWindow window = new(options, PropertyEditorEditMode.Transactional)
            {
                Title = "FOV 计算 (V2)",
                Owner = editorContext.OwnerWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.Submitted += (_, _) => Run(editorContext, roi, options);
            window.ShowDialog();
        }

        internal static void Run(EditorContext editorContext, RoiRect roi, FovImageViewOptions options)
        {
            ImageProcessingContext imageContext = editorContext.ProcessingContext;
            DrawEditorContext drawContext = editorContext.DrawEditorContext;
            long requestId = AlgorithmResultOverlay.BeginRequest(drawContext, AlgorithmResultOverlay.FovTag);
            AlgorithmResultOverlay.ClearTagged(drawContext, AlgorithmResultOverlay.FovTag);

            try
            {
                FovCalculator.ValidateCalibration(options.FovDist, options.CameraDegrees);
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                MessageBox.Show(editorContext.OwnerWindow, ex.Message, "FOV 参数无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ImageFrameLease? lease = imageContext.AcquireImageFrame();
            if (lease == null)
            {
                MessageBox.Show(editorContext.OwnerWindow, "请先打开待计算图像。", "FOV 计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double fovDist = options.FovDist;
            double cameraDegrees = options.CameraDegrees;
            double minimumConfidence = options.MinimumConfidence;
            long revision = lease.Revision;
            _ = Task.Run(() =>
            {
                FovImageViewRunResult runResult;
                Stopwatch stopwatch = Stopwatch.StartNew();
                try
                {
                    FovCalculationResult calculation;
                    using (lease)
                    {
                        calculation = FovCalculator.DetectAndCalculate(
                            lease.Image,
                            roi,
                            fovDist,
                            cameraDegrees,
                            minimumConfidence);
                    }
                    stopwatch.Stop();
                    int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                    runResult = new FovImageViewRunResult
                    {
                        Calculation = calculation,
                        TotalTime = totalTime
                    };
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    runResult = new FovImageViewRunResult
                    {
                        Error = ex.Message,
                        TotalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue))
                    };
                }

                imageContext.Dispatcher.BeginInvoke(() =>
                {
                    if (!imageContext.IsCurrentImageRevision(revision)
                        || !AlgorithmResultOverlay.IsCurrentRequest(drawContext, AlgorithmResultOverlay.FovTag, requestId)) return;
                    if (runResult.Calculation == null)
                    {
                        MessageBox.Show(editorContext.OwnerWindow, runResult.Error ?? "FOV 计算失败。", "FOV 计算", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    Render(imageContext, drawContext, runResult.Calculation.Measurement);
                    MessageBox.Show(
                        editorContext.OwnerWindow,
                        BuildResultMessage(runResult),
                        "FOV 计算结果",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });
            });
        }

        internal static void Render(ImageProcessingContext imageContext, DrawEditorContext drawContext, FovMeasurement measurement)
        {
            FovOverlayRenderer.Render(imageContext, drawContext, measurement);
        }

        internal static string BuildResultMessage(FovImageViewRunResult result)
        {
            FovMeasurement measurement = result.Calculation?.Measurement
                ?? throw new InvalidOperationException("FOV result is missing a measurement.");
            return string.Join(Environment.NewLine,
            [
                $"HorizontalFieldOfViewAngle: {measurement.DirectionalHorizontalFovDegrees:F4}",
                $"VerticalFieldOfViewAngle: {measurement.DirectionalVerticalFovDegrees:F4}",
                $"DiagonalFieldOfViewAngle: {measurement.DiagonalFovDegrees:F4}",
                string.Empty,
                $"H_Fov: {measurement.HorizontalFovDegrees:F4}",
                $"V_FOV: {measurement.VerticalFovDegrees:F4}",
                $"LB-RT: {measurement.LeftDownToRightUpDegrees:F4}",
                $"LT-RB: {measurement.LeftUpToRightDownDegrees:F4}",
                string.Empty,
                $"FovDist: {measurement.FovDist:G12}",
                $"cameraDegrees: {measurement.CameraDegrees:G12}",
                $"定位可信度: {result.Calculation!.Detection?.Confidence?.ToString("F3") ?? "N/A"}",
                $"边界来源: {result.Calculation!.BoundaryMode}（几何四角）",
                $"耗时: {result.TotalTime} ms"
            ]);
        }

    }

    public sealed class CMFovV2(EditorContext editorContext) : IIEditorToolContextMenu
    {
        public List<MenuItemMetadata> GetContextMenuItems() =>
        [
            new MenuItemMetadata
            {
                OwnerGuid = "AlgorithmsCall",
                GuidId = "FOV2.Local",
                Order = 2,
                Header = "FOV 计算 (V2)...",
                Command = new RelayCommand(_ => FovImageViewRunner.ShowOptions(editorContext, new RoiRect()))
            }
        ];
    }

    public sealed class DVCMFovV2(EditorContext editorContext, ImageViewConfig config) : IDVContextMenu
    {
        public Type ContextType => typeof(IRectangle);

        public IEnumerable<MenuItem> GetContextMenuItems(object obj)
        {
            if (obj is not IRectangle rectangle) return Array.Empty<MenuItem>();
            double scaleX = GetDipToPixelScale(config.GetProperties<double>(ImageViewPropertyKeys.DpiX));
            double scaleY = GetDipToPixelScale(config.GetProperties<double>(ImageViewPropertyKeys.DpiY));
            double left = Math.Floor(rectangle.Rect.Left * scaleX);
            double top = Math.Floor(rectangle.Rect.Top * scaleY);
            double right = Math.Ceiling(rectangle.Rect.Right * scaleX);
            double bottom = Math.Ceiling(rectangle.Rect.Bottom * scaleY);
            if (!double.IsFinite(left) || !double.IsFinite(top) || !double.IsFinite(right) || !double.IsFinite(bottom)
                || left < 0 || top < 0 || right > int.MaxValue || bottom > int.MaxValue || right <= left || bottom <= top)
                return Array.Empty<MenuItem>();
            RoiRect roi = new((int)left, (int)top, (int)(right - left), (int)(bottom - top));
            MenuItem item = new() { Header = "FOV 计算 (V2)..." };
            item.Click += (_, _) => FovImageViewRunner.ShowOptions(editorContext, roi);
            return new[] { item };
        }

        private static double GetDipToPixelScale(double dpi) =>
            double.IsFinite(dpi) && dpi > 0 ? dpi / 96 : 1;
    }
}
