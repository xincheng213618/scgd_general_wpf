#pragma warning disable CA1863,CS8604
#pragma warning disable CA1001
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.POI;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using ColorVision.UI.Menus;
using CVCommCore.CVAlgorithm;
using log4net;
using Newtonsoft.Json;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    [FileExtension(".cvraw|.cvcie")]
    public record class CVRawOpen(EditorContext EditorContext) : IImageOpen, IIEditorToolContextMenu, IImageOpenEditorToolProvider, IImageOpenEditorToolLifecycle
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CVRawOpen));
        private readonly object _bufferSync = new();
        private long _latestOpenRequest;
        private CvcieMouseMagnifierManager? _cvcieMouseMagnifierManager;
        private CvcieDiagramEditorTool? _cvcieDiagramEditorTool;
        private CvcieMouseProbeOptions? _probeOptions;
        private PoiMeasurementBuffer? _measurementBuffer;
        private ImageView? _bufferOwner;
        private EventHandler? _bufferCleanup;
        private Action? _loadBuffer;
        public List<List<Point>> Points { get; set; } = new List<List<Point>>();

        public (int pointIndex, int listIndex) FindNearbyPoints(int mousex, int mousey)
        {
            for (int listIndex = 0; listIndex < Points.Count; listIndex++)
            {
                List<Point> pointList = Points[listIndex];
                for (int pointIndex = 0; pointIndex < pointList.Count; pointIndex++)
                {
                    Point point = pointList[pointIndex];
                    double deltaX = point.X - (double)mousex;
                    double deltaY = point.Y - (double)mousey;
                    if (!(Math.Abs(deltaX) > 5.0) && !(Math.Abs(deltaY) > 5.0))
                    {
                        double distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                        if (distance < 5.0)
                        {
                            return (pointIndex: pointIndex, listIndex: listIndex);
                        }
                    }
                }
            }
            return (pointIndex: -1, listIndex: -1);
        }
        bool ShowDateFilePath;

        private static string? ResolveAssociatedRawFilePath(string filePath, CVCIEFile meta)
        {
            if (!string.IsNullOrWhiteSpace(meta.SrcFileName))
            {
                if (File.Exists(meta.SrcFileName))
                {
                    return meta.SrcFileName;
                }

                try
                {
                    string relativePath = Path.Combine(Path.GetDirectoryName(filePath) ?? string.Empty, meta.SrcFileName);
                    if (File.Exists(relativePath)) return relativePath;
                }
                catch (ArgumentException ex)
                {
                    log.Warn($"CVCIE 关联原图路径无效：{filePath}", ex);
                }
            }

            string siblingRawPath = Path.Combine(
                Path.GetDirectoryName(filePath) ?? string.Empty,
                Path.GetFileNameWithoutExtension(filePath) + ".cvraw");

            return File.Exists(siblingRawPath) ? siblingRawPath : null;
        }

        private string? GetCurrentFilePath()
        {
            return EditorContext.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath)
                ?? EditorContext.Config.GetProperties<string>("FilePath")
                ?? EditorContext.ImageView.Config.FilePath;
        }

        private bool CanCalculateCieForCurrentRaw()
        {
            if (EditorContext.Config.GetProperties<bool>("IsCVCIE"))
            {
                return false;
            }

            string? filePath = GetCurrentFilePath();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return false;
            }

            if (!string.Equals(Path.GetExtension(filePath), ".cvraw", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return EditorContext.Config.GetProperties<int>(ImageViewPropertyKeys.Channel) == 3;
        }

        private static double ResolveDialogExposure(float[]? exposureValues, int index)
        {
            if (exposureValues != null)
            {
                if (index < exposureValues.Length && exposureValues[index] > 0)
                {
                    return exposureValues[index];
                }

                if (exposureValues.Length == 1 && exposureValues[0] > 0)
                {
                    return exposureValues[0];
                }
            }

            return 0d;
        }


        private static CVRawManualCieConfig CreateManualCieConfig(string filePath, CVCIEFile rawFile)
        {
            CVRawManualCieConfig config = CVRawManualCieConfig.CreateFactoryDefaults();
            CVRawManualCieConfig.Instance.CopyTo(config);

            ApplySourceExposureDefaults(config, rawFile);
            ApplySourceGainDefaults(config, filePath, rawFile);

            return config;
        }

        private static void ApplySourceExposureDefaults(CVRawManualCieConfig config, CVCIEFile rawFile)
        {
            if (config.Texp_x <= 0)
            {
                config.Texp_x = ResolveDialogExposure(rawFile.Exp, 0);
            }

            if (config.Texp_y <= 0)
            {
                config.Texp_y = ResolveDialogExposure(rawFile.Exp, 1);
            }

            if (config.Texp_z <= 0)
            {
                config.Texp_z = ResolveDialogExposure(rawFile.Exp, 2);
            }
        }

        private static void ApplySourceGainDefaults(CVRawManualCieConfig config, string filePath, CVCIEFile rawFile)
        {
            double fallbackGain = IsTifFile(filePath) ? 0d : (rawFile.Gain > 0 ? rawFile.Gain : 0d);
            config.Gain_x = fallbackGain;
            config.Gain_y = fallbackGain;
            config.Gain_z = fallbackGain;
        }

        private static bool IsTifFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return string.Equals(extension, ".tif", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".tiff", StringComparison.OrdinalIgnoreCase);
        }

        private void ShowManualCieDialog()
        {
            string? filePath = GetCurrentFilePath();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.Engine_Msg_NoCalculableCvRaw, "ColorVision");
                return;
            }

            if (CVFileUtil.ReadCIEFileHeader(filePath, out CVCIEFile rawHeader) <= 0
                || rawHeader.FileExtType != CVType.Raw
                || rawHeader.Channels != 3)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.Engine_Msg_OnlyThreeChannelCvRaw, "ColorVision");
                return;
            }

            rawHeader.FilePath = filePath;

            CVRawManualCieConfig config = CreateManualCieConfig(filePath, rawHeader);
            CVRawManualCieWindow propertyEditorWindow = new CVRawManualCieWindow(config)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            propertyEditorWindow.Submited += async (_, _) =>
            {
                await CalculateCurrentRawCieAsync(filePath, config);
            };
            propertyEditorWindow.ShowDialog();
        }

        private async Task CalculateCurrentRawCieAsync(string filePath, CVRawManualCieConfig config)
        {
            try
            {
                CVRawManualCieCalculator.CalculationResult result = await Task.Run(() =>
                {
                    using CVCIEFile rawFile = CVFileUtil.OpenLocalCVFile(filePath);
                    return CVRawManualCieCalculator.Calculate(rawFile, config);
                });

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (!string.Equals(GetCurrentFilePath(), filePath, StringComparison.OrdinalIgnoreCase)) return;
                    try
                    {
                        config.CopyTo(CVRawManualCieConfig.Instance);
                        ConfigService.Instance.SaveConfigs();
                    }
                    catch (Exception saveError)
                    {
                        log.Warn("保存已验证的手动 CIE 校正参数失败。", saveError);
                    }
                    EditorContext.Config.SetImageMetadata("srcFileName", Path.GetFileName(filePath), nameof(CVRawOpen), "手动 CIE 计算的源 CVRAW 文件名");
                    AttachLiveCvcie(EditorContext.ImageView, (uint)result.Width, (uint)result.Height, 32, 3, result.XyzData, result.Exposure);
                    log.Info($"Manual CIE calculated for {filePath}");
                });
            }
            catch (Exception ex)
            {
                log.Error("Manual CIE calculation failed.", ex);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (!string.Equals(GetCurrentFilePath(), filePath, StringComparison.OrdinalIgnoreCase)) return;
                        using CVCIEFile raw = CvRawLayerController.LoadSourceFile(filePath, out _);
                        using OpenCvSharp.Mat mat = raw.ToMat(showErrors: false);
                        if (mat != null && !mat.Empty())
                        {
                            ReplaceMeasurementBuffer(null);
                            _loadBuffer = null;
                            EditorContext.Config.SetOpenerRuntime("IsCVCIE", false, nameof(CVRawOpen), "校正失败，继续显示原始 CVRAW");
                            EditorContext.ImageView.OpenImage(mat.ToWriteableBitmap());
                            InitializeCvFileView(EditorContext.ImageView, filePath, "composite", raw.Channels >= 3);
                            EditorContext.ImageView.EditorContext.IEditorToolFactory.ApplyImageOpenTools(this);
                        }
                    }
                    catch (Exception fallbackError)
                    {
                        log.Warn($"校正失败后的 CVRAW 回退不可用，保留当前图像：{filePath}", fallbackError);
                    }
                });
            }
        }

        private (int Channels, PoiMeasurementResult[] Results) CalculatePoi(IReadOnlyList<PoiMeasurementPoint> points, bool preserveNonPositiveValues)
        {
            _loadBuffer?.Invoke();
            lock (_bufferSync)
            {
                PoiMeasurementBuffer buffer = _measurementBuffer
                    ?? throw new InvalidOperationException("当前 CVCIE 视图没有可用的测量缓冲区。");
                return (buffer.Channels, preserveNonPositiveValues
                    ? PoiMeasurementService.CalculateRaw(buffer, points)
                    : PoiMeasurementService.Calculate(buffer, points));
            }
        }

        private (int Channels, PoiMeasurementResult Result) CalculatePoi(PoiMeasurementPoint point)
        {
            (int channels, PoiMeasurementResult[] results) = CalculatePoi(new[] { point }, false);
            return (channels, results[0]);
        }

        private void ReplaceMeasurementBuffer(PoiMeasurementBuffer? buffer)
        {
            PoiMeasurementBuffer? previous;
            lock (_bufferSync)
            {
                previous = _measurementBuffer;
                _measurementBuffer = buffer;
            }
            if (!ReferenceEquals(previous, buffer)) previous?.Dispose();
        }

        private void RegisterBufferLifecycle(ImageView imageView)
        {
            if (_bufferOwner != null && _bufferCleanup != null)
            {
                _bufferOwner.Config.Cleared -= _bufferCleanup;
            }

            _bufferOwner = imageView;
            _bufferCleanup = (_, _) =>
            {
                if (_bufferOwner != null && _bufferCleanup != null)
                {
                    _bufferOwner.Config.Cleared -= _bufferCleanup;
                }
                ReplaceMeasurementBuffer(null);
                _loadBuffer = null;
                _probeOptions = null;
                _bufferOwner = null;
                _bufferCleanup = null;
            };
            imageView.Config.Cleared += _bufferCleanup;
        }

        private CvRawLayerController? InitializeCvFileView(ImageView imageView, string filePath, string displayedLayerId, bool hasRgbLayers)
        {
            if (!File.Exists(filePath) || !CVFileUtil.IsCIEFile(filePath))
            {
                return null;
            }

            imageView.Config.FilePath = filePath;

            int index = CVFileUtil.ReadCIEFileHeader(imageView.Config.FilePath, out CVCIEFile meta);
            if (index <= 0)
            {
                return null;
            }

            if (!string.Equals(Path.GetExtension(filePath), ".cvcie", StringComparison.OrdinalIgnoreCase))
            {
                CvRawLayerController rawController = CvRawLayerController.Create(imageView, filePath, isCie: false, meta.Channels, meta.Bpp, hasRgbLayers: meta.Channels >= 3, displayedLayerId);
                imageView.SetLayerController(rawController);
                return rawController;
            }
            meta.FileExtType = CVType.CIE;

            Action loadBuffer = new(() =>
            {
                lock (_bufferSync)
                {
                    if (_measurementBuffer != null) return;
                    CVFileUtil.ReadCIEFileData(filePath, ref meta, index);
                    byte[] data = meta.Data ?? throw new InvalidDataException($"读取 CVCIE 数据失败：{filePath}");
                    _measurementBuffer = new PoiMeasurementBuffer(data, meta.Cols, meta.Rows, meta.Bpp, meta.Channels);
                    meta.Data = null;
                    imageView.Config.SetOpenerRuntime("meta", meta, nameof(CVRawOpen), "CVCIE 文件头和测量缓冲元信息");
                }
            });

            ReplaceMeasurementBuffer(null);
            _loadBuffer = loadBuffer;

            ShowDateFilePath = false;
            if (File.Exists(ViewAlgorithmConfig.Instance.ShowDateFilePath))
            {
                Points.Clear();
                log.Info("ShowDateFilePath:" + ViewAlgorithmConfig.Instance.ShowDateFilePath);
                string[] lines = File.ReadAllLines(ViewAlgorithmConfig.Instance.ShowDateFilePath);
                string[] dates = lines[0].Split(',');
                int rows = int.Parse(dates[0]);
                int cols = int.Parse(dates[1]);
                for (int lineIndex = 2; lineIndex < lines.Length; lineIndex++)
                {
                    string[] xy = lines[lineIndex].Split(',');
                    List<Point> points = new List<Point>();
                    for (int i = 0; i < xy.Length; i += 4)
                    {
                        if (double.TryParse(xy[i], out var x) && double.TryParse(xy[i + 1], out var y))
                        {
                            points.Add(new Point(x, y));
                        }
                    }
                    Points.Add(points);
                }
                ShowDateFilePath = true;
            }

            RegisterBufferLifecycle(imageView);

            CvcieMouseProbeOptions probeOptions = CvcieMouseProbeOptions.GetOrCreate(imageView);
            _probeOptions = probeOptions;
            log.Debug(JsonConvert.SerializeObject(meta));
            imageView.Config.SetOpenerRuntime("IsCVCIE", true, nameof(CVRawOpen), "当前视图是否由 CVCIE 打开器接管");

            if (ReferenceEquals(imageView.EditorContext.IImageOpen, this)
                && string.Equals(imageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath), filePath, StringComparison.Ordinal))
            {
                imageView.EditorContext.IEditorToolFactory.ApplyImageOpenTools(this);
            }

            imageView.Config.SetOpenerRuntime("meta", meta, nameof(CVRawOpen), "CVCIE 文件头和原始缓冲元信息");
            imageView.Config.SetOpenerRuntime("index", index, nameof(CVRawOpen), "CVCIE 数据块索引");
            imageView.Config.SetOpenerRuntime("Exp", meta.Exp, nameof(CVRawOpen), "当前 CVCIE 曝光数组");

            string? associatedRawFilePath = ResolveAssociatedRawFilePath(filePath, meta);
            if (!string.IsNullOrWhiteSpace(associatedRawFilePath))
            {
                meta.SrcFileName = associatedRawFilePath;
            }

            imageView.Config.SetOpenerRuntime("meta", meta, nameof(CVRawOpen), "CVCIE 文件头和原始缓冲元信息");
            CvRawLayerController controller = CvRawLayerController.Create(imageView, filePath, isCie: true, meta.Channels, meta.Bpp, hasRgbLayers, displayedLayerId);
            imageView.SetLayerController(controller);
            return controller;
        }

        public void AttachLiveCvcie(ImageView imageView, uint width, uint height, uint bpp, uint channels, byte[] xyzData, float[] exposure)
        {
            PoiMeasurementBuffer measurementBuffer = new(
                xyzData,
                checked((int)width),
                checked((int)height),
                checked((int)bpp),
                checked((int)channels));
            ReplaceMeasurementBuffer(measurementBuffer);
            _loadBuffer = null;

            ShowDateFilePath = false;
            Points.Clear();
            RegisterBufferLifecycle(imageView);

            _probeOptions = CvcieMouseProbeOptions.GetOrCreate(imageView);

            imageView.Config.SetImageMetadata("FileExtType", CVType.CIE, nameof(CVRawOpen), "当前视图以本地内存 CVCIE 结果接入");
            imageView.Config.SetOpenerRuntime("IsCVCIE", true, nameof(CVRawOpen), "当前视图是否由 CVCIE 打开器接管");
            imageView.Config.SetOpenerRuntime("Exp", exposure, nameof(CVRawOpen), "当前内存 CVCIE 曝光数组");

            imageView.EditorContext.IImageOpen = this;
            imageView.EditorContext.IEditorToolFactory.ApplyImageOpenTools(this);

            if (imageView.ViewBitmapSource is WriteableBitmap source)
            {
                CVCIEFile xyz = new()
                {
                    Cols = checked((int)width), Rows = checked((int)height), Bpp = checked((int)bpp),
                    Channels = checked((int)channels), FileExtType = CVType.CIE, Data = xyzData,
                };
                imageView.SetLayerController(CvRawLayerController.CreateLive(imageView, xyz, source, "composite"));
                if (CvcieDisplayConfig.Current.EnableTrueColor && CvcieSrgbRenderer.Supports(xyz.Channels, xyz.Bpp))
                {
                    foreach (ColorVision.ImageEditor.Layers.ImageLayerDescriptor layer in imageView.ComboBoxLayers.Items)
                    {
                        if (layer.Id == "cie-srgb")
                        {
                            imageView.ComboBoxLayers.SelectedItem = layer;
                            break;
                        }
                    }
                }
            }
        }


        public IEnumerable<IEditorTool> GetEditorTools()
        {
            if (!EditorContext.Config.GetProperties<bool>("IsCVCIE"))
            {
                yield break;
            }

            _cvcieMouseMagnifierManager ??= new CvcieMouseMagnifierManager(
                EditorContext,
                CalculatePoi,
                () => ShowDateFilePath,
                FindNearbyPoints,
                () => _probeOptions ?? CvcieMouseProbeOptions.GetOrCreate(EditorContext.ImageView));

            _cvcieDiagramEditorTool ??= new CvcieDiagramEditorTool(
                EditorContext,
                CalculatePoi,
                () => _probeOptions ?? CvcieMouseProbeOptions.GetOrCreate(EditorContext.ImageView));

            yield return _cvcieMouseMagnifierManager;
            yield return _cvcieDiagramEditorTool;
        }

        public void OnEditorToolsActivated(EditorContext context)
        {
        }

        public void OnEditorToolsDeactivated(EditorContext context)
        {
            _cvcieDiagramEditorTool?.Deactivate();
            if (_cvcieMouseMagnifierManager != null)
            {
                _cvcieMouseMagnifierManager.IsChecked = false;
            }
        }
        private sealed class ViewPoiRequest
        {
            public required BaseProperties DrawProperties { get; init; }
            public required PoiPoint Point { get; init; }
            public required PoiMeasurementPoint MeasurementPoint { get; init; }
        }

        private List<ViewPoiRequest> CreateViewPoiRequests()
        {
            List<ViewPoiRequest> requests = new();
            foreach (var drawing in EditorContext.DrawingVisualLists)
            {
                BaseProperties properties = drawing.BaseAttribute;
                PoiPoint? point = properties switch
                {
                    CircleTextProperties circle => CreateCirclePoint(
                        circle.Text,
                        (int)circle.Center.X,
                        (int)circle.Center.Y,
                        Math.Max(1, (int)circle.Radius * 2)),
                    CircleProperties circle => CreateCirclePoint(
                        circle.Id.ToString(),
                        (int)circle.Center.X,
                        (int)circle.Center.Y,
                        Math.Max(1, (int)circle.Radius * 2)),
                    RectangleTextProperties rectangle => CreateRectPoint(
                        rectangle.Id.ToString(),
                        (int)(rectangle.Rect.X + rectangle.Rect.Width / 2),
                        (int)(rectangle.Rect.Y + rectangle.Rect.Height / 2),
                        Math.Max(1, (int)rectangle.Rect.Width),
                        Math.Max(1, (int)rectangle.Rect.Height)),
                    RectangleProperties rectangle => CreateRectPoint(
                        rectangle.Id.ToString(),
                        (int)(rectangle.Rect.X + rectangle.Rect.Width / 2),
                        (int)(rectangle.Rect.Y + rectangle.Rect.Height / 2),
                        Math.Max(1, (int)rectangle.Rect.Width),
                        Math.Max(1, (int)rectangle.Rect.Height)),
                    _ => null
                };
                if (point == null) continue;

                PoiMeasurementShape shape = point.PointType switch
                {
                    PoiShape.Point or PoiShape.LegacySolidPoint => PoiMeasurementShape.Point,
                    PoiShape.Circle => PoiMeasurementShape.Circle,
                    PoiShape.Rect or PoiShape.LeftTopRect => PoiMeasurementShape.Rect,
                    _ => throw new NotSupportedException($"Unsupported POI shape: {point.PointType}")
                };
                requests.Add(new ViewPoiRequest
                {
                    DrawProperties = properties,
                    Point = point,
                    MeasurementPoint = new PoiMeasurementPoint(
                        (int)point.PixelX,
                        (int)point.PixelY,
                        (int)point.Width,
                        (int)point.Height,
                        shape)
                });
            }
            return requests;
        }

        private static PoiPoint CreateCirclePoint(string name, int x, int y, int diameter)
            => new()
            {
                Name = name,
                PixelX = x,
                PixelY = y,
                PointType = PoiShape.Circle,
                Width = diameter,
                Height = diameter
            };

        private static PoiPoint CreateRectPoint(string name, int x, int y, int width, int height)
            => new()
            {
                Name = name,
                PixelX = x,
                PixelY = y,
                PointType = PoiShape.Rect,
                Width = width,
                Height = height
            };

        private static void SetLuminanceMessage(BaseProperties properties, double luminance, bool show)
        {
            if (!show) return;
            string message = $"Y:{luminance:F1}";
            switch (properties)
            {
                case CircleTextProperties circle:
                    circle.Msg = message;
                    break;
                case CircleProperties circle:
                    circle.Msg = message;
                    break;
                case RectangleTextProperties rectangle:
                    rectangle.Msg = message;
                    break;
                case RectangleProperties rectangle:
                    rectangle.Msg = message;
                    break;
            }
        }

        private static void SetColorMessage(BaseProperties properties, PoiResultCIExyuvData result, bool show)
        {
            if (!show || !CVCIEShowConfig.Instance.IsShowString) return;
            string message = FormatMessage(CVCIEShowConfig.Instance.Template, result);
            switch (properties)
            {
                case CircleTextProperties circle:
                    circle.Msg = message;
                    break;
                case CircleProperties circle:
                    circle.Msg = message;
                    break;
                case RectangleTextProperties rectangle:
                    rectangle.Msg = message;
                    break;
                case RectangleProperties rectangle:
                    rectangle.Msg = message;
                    break;
            }
        }

        public List<MenuItemMetadata> GetContextMenuItems()
        {
            List<MenuItemMetadata> menuItems = new List<MenuItemMetadata>();
            menuItems.Add(new MenuItemMetadata()
            {
                Header = ColorVision.Engine.Properties.Resources.Export,
                GuidId = "CVCIEExport",
                Order = 301,
                Command = new RelayCommand(a =>
                {
                    if (EditorContext.Config.GetProperties<string>("FilePath") is string FilePath && File.Exists(FilePath))
                    {
                        new ExportCVCIE(FilePath).ShowDialog();
                    }
                })
            });

            if (CanCalculateCieForCurrentRaw())
            {
                menuItems.Add(new MenuItemMetadata()
                {
                    Header = ColorVision.Engine.Properties.Resources.Engine_Msg_CalculateCie,
                    GuidId = "CVRawCalculateCIE",
                    Order = 302,
                    Command = new RelayCommand(_ => ShowManualCieDialog())
                });
            }
            
            if (EditorContext.Config.GetProperties<bool>("IsCVCIE"))
            {
                MenuItemMetadata menuItemMetadata = new MenuItemMetadata()
                {
                    Header = "POI",
                    GuidId = "POI",
                    Order = 303,
                    Command = new RelayCommand(a =>
                    {
                        Func<double, double> normalize = CVCIEShowConfig.Instance.CreateValueNormalizer();
                        List<ViewPoiRequest> viewRequests = CreateViewPoiRequests();
                        PoiMeasurementPoint[] requests = new PoiMeasurementPoint[viewRequests.Count];
                        for (int index = 0; index < requests.Length; index++)
                        {
                            requests[index] = viewRequests[index].MeasurementPoint;
                        }
                        (int channels, PoiMeasurementResult[] measurements) = CalculatePoi(requests, true);
                        bool show = EditorContext.DrawingVisualLists.Count < 1000;

                        if (channels == 1)
                        {
                            ObservableCollection<PoiResultCIEYData> results = new();
                            for (int index = 0; index < measurements.Length; index++)
                            {
                                ViewPoiRequest request = viewRequests[index];
                                PoiResultCIEYData result = new()
                                {
                                    Point = request.Point,
                                    Y = normalize(measurements[index].Y)
                                };
                                SetLuminanceMessage(request.DrawProperties, result.Y, show);
                                results.Add(result);
                            }
                            new WindowCVCIE(results) { Owner = Application.Current.GetActiveWindow() }.Show();
                        }
                        else
                        {
                            ObservableCollection<PoiResultCIExyuvData> results = new();
                            for (int index = 0; index < measurements.Length; index++)
                            {
                                ViewPoiRequest request = viewRequests[index];
                                PoiMeasurementResult measurement = measurements[index];
                                PoiResultCIExyuvData result = new()
                                {
                                    Point = request.Point,
                                    X = measurement.X,
                                    Y = measurement.Y,
                                    Z = measurement.Z,
                                    x = measurement.ChromaX,
                                    y = measurement.ChromaY,
                                    u = measurement.U,
                                    v = measurement.V,
                                    CCT = measurement.Cct,
                                    Wave = measurement.Wave
                                };
                                result.NormalizeXyz(normalize);
                                SetColorMessage(request.DrawProperties, result, show);
                                results.Add(result);
                            }
                            new WindowCVCIE(results) { Owner = Application.Current.GetActiveWindow() }.Show();
                        }
                    })
                };
                menuItems.Add(menuItemMetadata);
            }
            return menuItems;
        }

        public static string FormatMessage(string template, PoiResultCIExyuvData properties)
        {
            template = template.Replace("\\n", Environment.NewLine);
            return Regex.Replace(template, @"@(\w+):([F\d]+)", match =>
            {
                var propertyName = match.Groups[1].Value;
                var format = match.Groups[2].Value;

                var propertyInfo = typeof(PoiResultCIExyuvData).GetProperty(propertyName);
                if (propertyInfo != null)
                {
                    var value = propertyInfo.GetValue(properties);
                    return string.Format($"{{0:{format}}}", value);
                }
                return match.Value;
            });
        }


        public async void OpenImage(EditorContext context, string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return;

            string requestedFilePath = filePath;
            long requestId = Interlocked.Increment(ref _latestOpenRequest);
            CvcieDisplayConfig displayConfig = CvcieDisplayConfig.Current;
            bool preferSrgb = displayConfig.EnableTrueColor
                && string.Equals(Path.GetExtension(filePath), ".cvcie", StringComparison.OrdinalIgnoreCase);
            CvcieBrightnessMode brightnessMode = displayConfig.BrightnessMode;
            double referenceWhite = displayConfig.ReferenceWhiteLuminance;
            try
            {
                await Task.Run(() =>
                {
                    if (requestId != Volatile.Read(ref _latestOpenRequest)) return;
                    WriteableBitmap? srgb = null;
                    if (preferSrgb)
                    {
                        try
                        {
                            srgb = CvRawLayerController.LoadSrgb(requestedFilePath, brightnessMode, referenceWhite);
                        }
                        catch (Exception ex)
                        {
                            log.Warn($"CVCIE 真彩转换失败，尝试 CVRAW 或 Y 灰度回退：{requestedFilePath}", ex);
                        }
                    }
                    bool usesLuminance = false;
                    // A successful XYZ render needs only metadata, not another RAW/Y payload and conversion.
                    using CVCIEFile cVCIEFile = srgb != null ? ReadDisplayHeader(requestedFilePath)
                        : CvRawLayerController.LoadSourceFile(requestedFilePath, out usesLuminance);
                    WriteableBitmap? displayBitmap = srgb;
                    if (displayBitmap == null && cVCIEFile.Channels == 1 && cVCIEFile.Bpp is 32 or 64)
                        displayBitmap = MediaHelper.RenderFloatChannel(cVCIEFile);
                    using OpenCvSharp.Mat? mat = displayBitmap == null ? cVCIEFile.ToMat(showErrors: false) : null;
                    if (displayBitmap == null && (mat == null || mat.Empty())) return;

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        string? activeFilePath = context.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
                        if (requestId != Volatile.Read(ref _latestOpenRequest)
                            || !string.Equals(activeFilePath, requestedFilePath, StringComparison.OrdinalIgnoreCase))
                        {
                            log.Info($"图像目标已切换，丢弃迟到的 CVCIE 加载结果：{requestedFilePath}");
                            return;
                        }

                        int displayWidth = displayBitmap?.PixelWidth ?? mat!.Cols;
                        int displayHeight = displayBitmap?.PixelHeight ?? mat!.Rows;
                        context.Config.SetImageMetadata(ImageViewPropertyKeys.FileSource, requestedFilePath, nameof(CVRawOpen), "打开器接收到的源文件路径");
                        context.Config.SetImageMetadata(ImageViewPropertyKeys.FileName, Path.GetFileName(requestedFilePath), nameof(CVRawOpen), "当前文件名");
                        context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageWidth, displayWidth, nameof(CVRawOpen), "当前显示位图宽度");
                        context.Config.SetImageMetadata(ImageViewPropertyKeys.ImageHeight, displayHeight, nameof(CVRawOpen), "当前显示位图高度");
                        context.Config.SetImageMetadata(ImageViewPropertyKeys.Rows, displayHeight, nameof(CVRawOpen), "当前 CVCIE 图像行数");
                        context.Config.SetImageMetadata(ImageViewPropertyKeys.Cols, displayWidth, nameof(CVRawOpen), "当前 CVCIE 图像列数");
                        context.Config.SetImageMetadata("CVRawSourceChannel", cVCIEFile.Channels, nameof(CVRawOpen), "CVCIE 文件原始通道数");
                        context.Config.SetImageMetadata("CVRawSourceBpp", cVCIEFile.Bpp, nameof(CVRawOpen), "CVCIE 文件原始位深");
                        context.Config.SetImageMetadata("Gain", cVCIEFile.Gain, nameof(CVRawOpen), "CVCIE 采集增益");
                        context.Config.SetImageMetadata("exp", cVCIEFile.Exp, nameof(CVRawOpen), "CVCIE 曝光数组");
                        context.Config.SetImageMetadata("FileExtType", cVCIEFile.FileExtType, nameof(CVRawOpen), "CVCIE 文件扩展类型");
                        context.Config.SetImageMetadata("srcFileName", cVCIEFile.SrcFileName, nameof(CVRawOpen), "CVCIE 关联源文件名");

                        bool hasRgbLayers = cVCIEFile.Channels >= 3 && (srgb != null
                            ? ResolveAssociatedRawFilePath(requestedFilePath, cVCIEFile) != null : !usesLuminance);
                        CvRawLayerController? controller = InitializeCvFileView(context.ImageView, requestedFilePath,
                            srgb != null ? "cie-srgb" : usesLuminance ? "cie-y" : "composite", hasRgbLayers);
                        if (srgb != null) controller?.CacheSrgb(srgb, brightnessMode, referenceWhite);

                        if (displayBitmap != null)
                        {
                            context.ImageView.SetImageSource(displayBitmap.Clone(), context.ImageView.EnableEditorImageServices, configureDefaultLayerController: false);
                            context.ImageView.UpdateZoomAndScale();
                        }
                        else if (context.ImageView.ViewBitmapSource is WriteableBitmap writeableBitmap)
                        {
                            if (!mat!.MatUpdateWriteableBitmap(writeableBitmap))
                            {
                                WriteableBitmap replacement = OpenCvSharp.WpfExtensions.WriteableBitmapConverter.ToWriteableBitmap(mat);
                                context.ImageView.SetImageSource(replacement, context.ImageView.EnableEditorImageServices, configureDefaultLayerController: false);
                                context.ImageView.UpdateZoomAndScale();
                            }
                            else
                            {
                                int displayChannels = mat.Channels();
                                int displayDepth = checked((int)mat.ElemSize1() * 8);
                                int displayStride = checked(mat.Cols * displayChannels * (displayDepth / 8));
                                context.Config.SetImageMetadata(ImageViewPropertyKeys.PixelFormat, writeableBitmap.Format, nameof(CVRawOpen), "当前显示位图像素格式");
                                context.Config.SetImageMetadata(ImageViewPropertyKeys.Channel, displayChannels, nameof(CVRawOpen), "当前显示位图通道数");
                                context.Config.SetImageMetadata(ImageViewPropertyKeys.Depth, displayDepth, nameof(CVRawOpen), "当前显示位图位深");
                                context.Config.SetImageMetadata(ImageViewPropertyKeys.Stride, displayStride, nameof(CVRawOpen), "当前显示位图行跨度");
                                context.Config.SetImageMetadata(ImageViewPropertyKeys.DpiX, writeableBitmap.DpiX, nameof(CVRawOpen), "当前 CVCIE 图像水平 DPI");
                                context.Config.SetImageMetadata(ImageViewPropertyKeys.DpiY, writeableBitmap.DpiY, nameof(CVRawOpen), "当前 CVCIE 图像垂直 DPI");
                                //这里需要强制切换过来
                                context.ImageView.ImageShow.Source = writeableBitmap;
                                context.ImageView.NotifySourcePixelsChanged();
                                context.ImageView.NotifyImageSourceLoaded();
                            }
                        }
                        else
                        {
                            WriteableBitmap replacement = mat!.ToWriteableBitmap();
                            context.ImageView.SetImageSource(replacement, context.ImageView.EnableEditorImageServices, configureDefaultLayerController: false);
                            context.ImageView.UpdateZoomAndScale();
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                log.Error($"打开 CVCIE 图像失败：{requestedFilePath}", ex);
            }
        }

        private static CVCIEFile ReadDisplayHeader(string filePath)
        {
            if (CVFileUtil.ReadCIEFileHeader(filePath, out CVCIEFile header) > 0) return header;
            header.Dispose();
            throw new InvalidDataException($"CVCIE 文件头无效：{filePath}");
        }
    }
}
