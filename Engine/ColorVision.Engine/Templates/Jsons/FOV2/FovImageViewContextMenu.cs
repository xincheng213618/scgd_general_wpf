using LocalizedText = global::ColorVision.Engine.EngineLocalization;
using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.FOV2
{
    public sealed class FovImageViewOptions : ViewModelBase, IConfig
    {
        private double fovDist = 9410;
        private double cameraDegrees = 74.2;
        private double minimumConfidence = 0.25;
        private FovMeasurementMode measurementMode;
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

        [Category("FOV")]
        [DisplayName("测量模式")]
        [Description("默认四角模式保持原有计算；轮廓模式支持曲边、圆形、椭圆等完整填充轮廓，独立读图测量中心水平、垂直及斜向跨度，使用相同的相机角度参数。")]
        public FovMeasurementMode MeasurementMode
        {
            get => measurementMode;
            set { measurementMode = value; OnPropertyChanged(); }
        }

        [Category("发光区定位")]
        [DisplayName("最低可信度")]
        [Description("四角自动定位所需的最低可信度，范围 0 到 1。")]
        [PropertyVisibility(nameof(MeasurementMode), FovMeasurementMode.FourCorners)]
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

    internal sealed record FovImageViewCamera(string Key, string Name)
    {
        internal static FovImageViewCamera? FromIdentity(string? cameraId, string? deviceCode, string? deviceName)
        {
            string? id = cameraId?.Trim();
            string? code = deviceCode?.Trim();
            string? identifier = !string.IsNullOrEmpty(id) ? id : code;
            if (string.IsNullOrEmpty(identifier)) return null;

            string prefix = !string.IsNullOrEmpty(id) ? "camera:" : "device:";
            string name = string.IsNullOrWhiteSpace(deviceName) || string.Equals(deviceName.Trim(), identifier, StringComparison.OrdinalIgnoreCase)
                ? identifier : $"{deviceName.Trim()} ({identifier})";
            return new FovImageViewCamera(prefix + identifier, name);
        }
    }

    internal sealed class FovImageViewCameraOptions : IConfig
    {
        public FovImageViewCameraOptions() { }

        public Dictionary<string, FovImageViewOptions> Overrides { get; internal set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    internal static class FovImageViewRunner
    {
        private static readonly FovImageViewOptions SessionOptions = new();
        private static readonly FovImageViewCameraOptions SessionCameraOptions = new();
        private static readonly ConditionalWeakTable<ImageView, DeviceCamera> CameraHosts = new();

        internal static void BindCamera(ImageView view, DeviceCamera device) => CameraHosts.AddOrUpdate(view, device);

        internal static FovImageViewCamera? ResolveCamera(ImageView view)
        {
            var source = view.SourceCamera;
            FovImageViewCamera? camera = FovImageViewCamera.FromIdentity(source?.CameraId, source?.DeviceCode, source?.DeviceName);
            if (camera != null) return camera;
            if (!CameraHosts.TryGetValue(view, out DeviceCamera? host)) return null;

            string? cameraId = host.LocalCameraSession.OpenedCameraId;
            if (string.IsNullOrWhiteSpace(cameraId)) cameraId = host.Config.CameraID;
            return FovImageViewCamera.FromIdentity(cameraId, host.Code, host.Config.Name);
        }

        public static void ShowOptions(EditorContext editorContext, RoiRect roi)
        {
            DrawEditorContext drawContext = editorContext.DrawEditorContext;
            AlgorithmResultOverlay.InvalidateRequest(drawContext, AlgorithmResultOverlay.FovTag);
            AlgorithmResultOverlay.ClearTagged(drawContext, AlgorithmResultOverlay.FovTag);

            FovImageViewCamera? camera = ResolveCamera(editorContext.ImageView);
            PropertyEditorWindow window = CreateOptionsEditor(ConfigService.Instance as ConfigHandler, camera,
                () => string.Equals(camera?.Key, ResolveCamera(editorContext.ImageView)?.Key, StringComparison.OrdinalIgnoreCase));
            window.Owner = editorContext.OwnerWindow;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Submitted += (_, _) => Run(editorContext, roi, (FovImageViewOptions)window.Config);
            window.ShowDialog();
        }

        internal static PropertyEditorWindow CreateOptionsEditor(ConfigHandler? config, FovImageViewCamera? camera = null, Func<bool>? isCurrentCamera = null)
        {
            FovImageViewOptions global = config?.GetRequiredService<FovImageViewOptions>() ?? SessionOptions;
            FovImageViewCameraOptions? cameraOptions = camera == null ? null : config?.GetRequiredService<FovImageViewCameraOptions>() ?? SessionCameraOptions;
            FovImageViewOptions options = camera == null ? global : CopyOptions(cameraOptions!.Overrides.GetValueOrDefault(camera.Key) ?? global);
            string scope = camera?.Name ?? LocalizedText.Get("全局");
            string destination = LocalizedText.Format($"保存到：{scope}");
            PropertyEditorWindow window = new(options, PropertyEditorEditMode.Transactional)
            {
                Title = $"{ColorVision.ImageEditor.Properties.Resources.Algorithm_FieldOfView} · {destination}"
            };
            if (camera != null)
                window.ConfigureResetToDefaults(LocalizedText.Get("恢复到全局"), () => global);

            window.Submitting += (_, args) =>
            {
                if (isCurrentCamera?.Invoke() == false
                    || (config != null && (!ReferenceEquals(global, config.GetRequiredService<FovImageViewOptions>())
                        || (cameraOptions != null && !ReferenceEquals(cameraOptions, config.GetRequiredService<FovImageViewCameraOptions>())))))
                {
                    args.Cancel = true;
                    args.ErrorMessage = LocalizedText.Get("配置已更新，请重新打开参数窗口。");
                    return;
                }

                var draft = (FovImageViewOptions)window.EditConfig;
                try
                {
                    FovCalculator.ValidateCalibration(draft.FovDist, draft.CameraDegrees);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    args.Cancel = true;
                    args.ErrorMessage = ex.Message;
                    return;
                }

                // Save the working copy before the editor commits it to the live options.
                string error = string.Empty;
                bool saved = camera == null
                    ? config == null || config.TrySave(draft, out error)
                    : SaveCameraOptions(config, cameraOptions!, camera.Key, draft, global, out error);
                if (!saved)
                {
                    args.Cancel = true;
                    args.ErrorMessage = error;
                }
            };
            return window;
        }

        private static bool SaveCameraOptions(ConfigHandler? config, FovImageViewCameraOptions current, string key,
            FovImageViewOptions draft, FovImageViewOptions global, out string error)
        {
            error = string.Empty;
            bool inheritsGlobal = OptionValues(draft) == OptionValues(global);
            FovImageViewOptions? existing = current.Overrides.GetValueOrDefault(key);
            if (inheritsGlobal ? existing == null : existing != null && OptionValues(draft) == OptionValues(existing))
                return true;

            var candidate = new FovImageViewCameraOptions { Overrides = new(current.Overrides, StringComparer.OrdinalIgnoreCase) };
            if (inheritsGlobal)
                candidate.Overrides.Remove(key);
            else
                candidate.Overrides[key] = CopyOptions(draft);

            if (config == null)
            {
                current.Overrides = candidate.Overrides;
                return true;
            }
            return config.TrySaveAndPublish(candidate, () => current.Overrides = candidate.Overrides, out error)
                == ConfigSavePublicationStatus.PersistedAndPublished;
        }

        private static FovImageViewOptions CopyOptions(FovImageViewOptions options) => new()
        {
            FovDist = options.FovDist,
            CameraDegrees = options.CameraDegrees,
            MeasurementMode = options.MeasurementMode,
            MinimumConfidence = options.MinimumConfidence
        };

        private static (double, double, FovMeasurementMode, double) OptionValues(FovImageViewOptions options) =>
            (options.FovDist, options.CameraDegrees, options.MeasurementMode, options.MinimumConfidence);

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
                MessageBox.Show(editorContext.OwnerWindow, ex.Message, LocalizedText.Get("FOV 参数无效"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ImageFrameLease? lease = imageContext.AcquireImageFrame();
            if (lease == null)
            {
                MessageBox.Show(editorContext.OwnerWindow, LocalizedText.Get("请先打开待计算图像。"), LocalizedText.Get("FOV 计算"), MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double fovDist = options.FovDist;
            double cameraDegrees = options.CameraDegrees;
            double minimumConfidence = options.MinimumConfidence;
            FovMeasurementMode mode = options.MeasurementMode;
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
                            mode,
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
                        MessageBox.Show(editorContext.OwnerWindow, runResult.Error ?? LocalizedText.Get("FOV 计算失败。"), LocalizedText.Get("FOV 计算"), MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    Render(imageContext, drawContext, runResult.Calculation.Measurement);
                    MessageBox.Show(
                        editorContext.OwnerWindow,
                        BuildResultMessage(runResult),
                        LocalizedText.Get("FOV 计算结果"),
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
                $"H_Fov{(measurement.Boundary == null ? "" : "（轮廓横向包络）")}: {measurement.HorizontalFovDegrees:F4}",
                $"V_FOV{(measurement.Boundary == null ? "" : "（轮廓纵向包络）")}: {measurement.VerticalFovDegrees:F4}",
                $"{(measurement.Boundary == null ? "LB-RT" : "中心斜向 /")}: {measurement.LeftDownToRightUpDegrees:F4}",
                $"{(measurement.Boundary == null ? "LT-RB" : "中心斜向 \\")}: {measurement.LeftUpToRightDownDegrees:F4}",
                string.Empty,
                $"FovDist: {measurement.FovDist:G12}",
                $"cameraDegrees: {measurement.CameraDegrees:G12}",
                $"{(measurement.Mode == FovMeasurementMode.CurvedBoundary ? "最弱弧边缘对比度（参考）" : "定位可信度")}: {result.Calculation!.Detection?.Confidence?.ToString("F3") ?? "N/A"}",
                $"测量模式: {(measurement.Mode == FovMeasurementMode.CurvedBoundary ? "轮廓（实际中心边界，D 为两条中心斜向跨度均值）" : "四角")}",
                $"边界来源: {result.Calculation!.BoundaryMode}",
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
                OwnerGuid = AlgorithmMenuGroups.FieldGeometry.Id,
                GuidId = "FOV2.Local",
                Order = 1,
                Header = ColorVision.ImageEditor.Properties.Resources.Algorithm_FieldOfView + "...",
                Command = new RelayCommand(_ => FovImageViewRunner.ShowOptions(editorContext, new RoiRect()))
            }
        ];
    }

    public sealed class DVCMFovV2(EditorContext editorContext, ImageViewConfig config) : IDVContextMenu
    {
        public AlgorithmInteractiveGroupPresentation AnalysisGroup => AlgorithmMenuGroups.FieldGeometry;

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
            MenuItem item = new() { Header = ColorVision.ImageEditor.Properties.Resources.Algorithm_FieldOfView + "..." };
            item.Click += (_, _) => FovImageViewRunner.ShowOptions(editorContext, roi);
            return new[] { item };
        }

        private static double GetDipToPixelScale(double dpi) =>
            double.IsFinite(dpi) && dpi > 0 ? dpi / 96 : 1;
    }
}
