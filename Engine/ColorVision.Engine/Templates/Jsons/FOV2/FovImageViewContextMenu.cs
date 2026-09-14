using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Nodes;
using ColorVision.Engine.PropertyEditor;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Results;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using ColorVision.UI;
using ColorVision.UI.Menus;
using ColorVision.Util.Draw.Rectangle;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Jsons.FOV2
{
    public sealed class FovImageViewOptions : ViewModelBase
    {
        private string cameraDeviceCode = string.Empty;
        private double fovDist = 9410;
        private double cameraDegrees = 74.2;
        private double luminanceBoundaryRatio = FovLuminousAreaDetector.DefaultBoundaryRatio;

        [Category("FOV")]
        [DisplayName("相机")]
        [Description("可选。选择当前图像所属相机，用于结果关联和追溯；不影响 FOV 参数计算。")]
        [PropertyEditorType(typeof(DeviceNameEditor)), DeviceSourceType(typeof(DeviceCamera))]
        public string CameraDeviceCode
        {
            get => cameraDeviceCode;
            set { cameraDeviceCode = value ?? string.Empty; OnPropertyChanged(); }
        }

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
        public int? MasterId { get; init; }
        public string PersistenceMessage { get; init; } = string.Empty;
        public required FovCameraCalibration Calibration { get; init; }
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
            if (string.IsNullOrWhiteSpace(options.CameraDeviceCode))
                options.CameraDeviceCode = ResolveDefaultCameraDeviceCode(editorContext.Config.FilePath);
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

            FovCameraCalibration calibration;
            try
            {
                calibration = FovCameraCalibrationResolver.Resolve(options.CameraDeviceCode);
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
            string imageFilePath = editorContext.Config.FilePath ?? string.Empty;
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
                            LocalFindLuminousAreaNode.DefaultMinimumConfidence);
                    }
                    stopwatch.Stop();
                    int totalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue));
                    int? masterId = null;
                    string persistenceMessage;
                    try
                    {
                        (masterId, persistenceMessage) = TryPersist(
                            calculation,
                            calibration,
                            cameraDegrees,
                            imageFilePath,
                            roi,
                            totalTime);
                    }
                    catch (Exception persistenceException)
                    {
                        persistenceMessage = $"结果已计算并将绘制，但数据库保存或结果通知失败：{persistenceException.Message}";
                    }
                    runResult = new FovImageViewRunResult
                    {
                        Calculation = calculation,
                        TotalTime = totalTime,
                        MasterId = masterId,
                        PersistenceMessage = persistenceMessage,
                        Calibration = calibration
                    };
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    runResult = new FovImageViewRunResult
                    {
                        Error = ex.Message,
                        TotalTime = checked((int)Math.Min(stopwatch.ElapsedMilliseconds, int.MaxValue)),
                        Calibration = calibration
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
                $"相机: {(string.IsNullOrWhiteSpace(result.Calibration.CameraDeviceCode) ? "未关联" : result.Calibration.CameraDeviceCode)}",
                $"FovDist: {measurement.FovDist:G12}",
                $"cameraDegrees: {measurement.CameraDegrees:G12}",
                $"边界来源: {result.Calculation!.BoundaryMode}（几何四角）",
                $"耗时: {result.TotalTime} ms",
                result.PersistenceMessage
            ]);
        }

        private static (int? MasterId, string Message) TryPersist(
            FovCalculationResult calculation,
            FovCameraCalibration calibration,
            double cameraDegrees,
            string imageFilePath,
            RoiRect roi,
            int totalTime)
        {
            MeasureResultImgModel? imageResult = TryFindImageResult(imageFilePath, calibration.CameraDeviceCode);
            if (imageResult == null || imageResult.BatchId <= 0)
                return (null, "当前图像未关联测量批次，结果已绘制但未写入数据库。");

            string algorithmDeviceCode = ServiceManager.Current?.DeviceServices
                .OfType<DeviceAlgorithm>()
                .FirstOrDefault()?.Code ?? string.Empty;
            LocalFovPersistenceResult persisted = LocalFovResultPersistence.Save(new LocalFovPersistenceRequest
            {
                BatchId = imageResult.BatchId,
                ImageFilePath = string.IsNullOrWhiteSpace(imageFilePath) ? null : imageFilePath,
                AlgorithmDeviceCode = algorithmDeviceCode,
                ZIndex = imageResult.ZIndex ?? 0,
                TotalTime = totalTime,
                Measurement = calculation.Measurement,
                Parameters = new
                {
                    Algorithm = "LocalFOV2",
                    Invocation = "ImageView",
                    Formula = "2*atan((pixelDistance/FovDist)*tan(cameraDegrees/2))*180/PI",
                    FovDist = calculation.Measurement.FovDist,
                    cameraDegrees,
                    BoundaryMode = calculation.BoundaryMode,
                    CameraCalibration = new
                    {
                        calibration.CameraDeviceCode,
                        calibration.PhysicalCameraCode
                    },
                    SourceMasterId = imageResult.Id,
                    SearchRegion = new { roi.X, roi.Y, roi.Width, roi.Height },
                    Localization = new
                    {
                        UsedUpstreamCorners = false,
                        calculation.Detection?.Algorithm,
                        calculation.Detection?.Confidence,
                        calculation.Detection?.SideQuality,
                        calculation.Detection?.Warnings,
                        CoarseAlgorithm = calculation.CoarseDetection?.Algorithm,
                        CoarseConfidence = calculation.CoarseDetection?.Confidence
                    },
                    Corners = calculation.Measurement.Corners.Select((point, index) => new
                    {
                        Name = new[] { "LT", "RT", "RB", "LB" }[index],
                        point.X,
                        point.Y
                    })
                }
            });
            MeasureBatchModel? batch = BatchResultMasterDao.Instance.GetById(imageResult.BatchId);
            ResultMessageBus.Default.PublishPersisted(
                ResultRoutes.Algorithm,
                ResultKinds.Algorithm,
                algorithmDeviceCode,
                "FOV",
                batch?.Code ?? batch?.Name ?? string.Empty,
                "ImageView.FOV2",
                imageResult.ZIndex ?? 0,
                persisted.MasterId,
                (int)ViewResultAlgType.FOV);
            return (persisted.MasterId, $"数据库结果 ID: {persisted.MasterId}");
        }

        private static string ResolveDefaultCameraDeviceCode(string? imageFilePath)
        {
            MeasureResultImgModel? imageResult = TryFindImageResult(imageFilePath, null);
            if (!string.IsNullOrWhiteSpace(imageResult?.DeviceCode)) return imageResult.DeviceCode;
            DeviceCamera[] cameras = ServiceManager.Current?.DeviceServices.OfType<DeviceCamera>().ToArray()
                ?? Array.Empty<DeviceCamera>();
            return cameras.Length == 1 ? cameras[0].Code : string.Empty;
        }

        private static MeasureResultImgModel? TryFindImageResult(string? imageFilePath, string? cameraDeviceCode)
        {
            if (string.IsNullOrWhiteSpace(imageFilePath) || !MySqlControl.GetInstance().IsConnect) return null;
            try
            {
                string fullPath = Path.GetFullPath(imageFilePath.Trim());
                string fileName = Path.GetFileName(fullPath);
                using SqlSugarClient db = MySqlControl.CreateDbClient();
                ISugarQueryable<MeasureResultImgModel> query = db.Queryable<MeasureResultImgModel>()
                    .Where(item => item.FileUrl == fullPath || item.RawFile == fullPath || item.RawFile == fileName);
                if (!string.IsNullOrWhiteSpace(cameraDeviceCode))
                    query = query.Where(item => item.DeviceCode == cameraDeviceCode);
                return query.OrderBy(item => item.Id, OrderByType.Desc).First();
            }
            catch
            {
                return null;
            }
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
