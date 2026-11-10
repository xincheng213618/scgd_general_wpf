using LocalizedText = global::ColorVision.Engine.EngineLocalization;
#pragma warning disable CA1863,CS8604
#pragma warning disable CA1001
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Devices.Algorithm.Views;
using ColorVision.Engine.Services.POI;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Templates.POI;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Presentation;
using ColorVision.ImageEditor.Tif;
using ColorVision.UI;
using ColorVision.UI.Menus;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    public record class CalibratedImageCapability(EditorContext EditorContext) : IAsyncImageOpen, IImageOpenContentLifetime, IIEditorToolContextMenu, IImageContentCapability
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CVRawOpen));
        public long? GetCachedFileLength(string filePath) => CVFileReadCache.GetCachedLength(filePath);
        private readonly object _bufferSync = new();
        private CvRawPixelBuffer _rawPixels = new();
        private readonly SemaphoreSlim _fileOpenGate = new(1, 1);
        private long _latestOpenRequest;
        private CvcieMouseMagnifierManager? _cvcieMouseMagnifierManager;
        private CvcieDiagramEditorTool? _cvcieDiagramEditorTool;
        private CvcieMouseProbeOptions? _probeOptions;
        private PoiMeasurementBuffer? _measurementBuffer;
        private EmbeddedProfileLifetime? _embeddedProfileLifetime;
        private long _activationVersion = -1;
        private Task _retiredReaders = Task.CompletedTask;
        private ImageView? _bufferOwner;
        private EventHandler? _bufferCleanup;
        private CvcieFileMeasurements? _fileMeasurements;
        private CvcieFileMeasurements? _failedFileProbe;
        private Task<PoiMeasurementBuffer>? _fileMeasurementTask;
        private CancellationTokenSource? _fileMeasurementCancellation;
        private CancellationTokenSource? _viewPoiRequest;
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
            if (string.IsNullOrWhiteSpace(filePath) || !(CVFileReadCache.GetCachedLength(filePath).HasValue || File.Exists(filePath)))
            {
                return false;
            }

            if (!string.Equals(Path.GetExtension(filePath), ".cvraw", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return EditorContext.Config.GetProperties<int>("CVRawSourceChannel") == 3;
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
            if (string.IsNullOrWhiteSpace(filePath) || !(CVFileReadCache.GetCachedLength(filePath).HasValue || File.Exists(filePath)))
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
            long contentVersion = EditorContext.ImageView.ContentVersion;
            try
            {
                var calculated = await Task.Run(() =>
                {
                    CVCIEFile rawFile = CVFileUtil.OpenLocalCVFile(filePath);
                    try
                    {
                        var result = CVRawManualCieCalculator.Calculate(rawFile, config);
                        result.Snapshot.Save(filePath, canReplay: true);
                        return (Result: result, Raw: rawFile, Metadata: ReadCvFileViewMetadata(filePath, null));
                    }
                    catch { rawFile.Dispose(); throw; }
                });
                using CVCIEFile calculatedRaw = calculated.Raw;
                CVRawManualCieCalculator.CalculationResult result = calculated.Result;

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (EditorContext.ImageView.ContentVersion != contentVersion || !string.Equals(GetCurrentFilePath(), filePath, StringComparison.OrdinalIgnoreCase)) return;
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
                    string layerId = (EditorContext.ImageView.ComboBoxLayers.SelectedItem as ColorVision.ImageEditor.Layers.ImageLayerDescriptor)?.Id ?? "composite";
                    CvRawLayerController? controller = InitializeCvFileView(EditorContext.ImageView, filePath, layerId, true,
                        calculated.Metadata, calculatedRaw);
                    _measurementBuffer?.RawSource?.UseCalculatedXyz(result.XyzData);
                    EditorContext.ImageView.EditorContext.IEditorToolFactory.ApplyImageOpenTools(this);
                    if (controller?.DefaultLayer != null) controller.SelectLayer(controller.DefaultLayer);
                    log.Info($"Manual CIE calculated for {filePath}");
                });
            }
            catch (Exception ex)
            {
                log.Error("Manual CIE calculation failed.", ex);
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
                    try
                    {
                        if (EditorContext.ImageView.ContentVersion != contentVersion || !string.Equals(GetCurrentFilePath(), filePath, StringComparison.OrdinalIgnoreCase)) return;
                        using CVCIEFile raw = CVFileUtil.OpenLocalCVFile(filePath, CVType.Raw);
                        using OpenCvSharp.Mat mat = raw.ToMat(showErrors: false);
                        if (mat != null && !mat.Empty())
                        {
                            ReplaceMeasurementBuffer(null);
                            EditorContext.Config.SetOpenerRuntime("IsCVCIE", false, nameof(CVRawOpen), "校正失败，继续显示原始 CVRAW");
                            EditorContext.ImageView.OpenImage(mat.CreateDisplayBitmap());
                            InitializeCvFileView(EditorContext.ImageView, filePath, "composite", raw.Channels >= 3,
                                ReadCvFileViewMetadata(filePath, null));
                            EditorContext.ImageView.AttachImageContentCapability(this);
                        }
                    }
                    catch (Exception fallbackError)
                    {
                        log.Warn($"校正失败后的 CVRAW 回退不可用，保留当前图像：{filePath}", fallbackError);
                    }
                });
            }
        }

        internal (int Channels, PoiMeasurementResult[] Results) CalculatePoi(IReadOnlyList<PoiMeasurementPoint> points, bool preserveNonPositiveValues)
        {
            PoiMeasurementBuffer buffer = GetMeasurementBuffer();
            return (buffer.Channels, preserveNonPositiveValues
                ? PoiMeasurementService.CalculateRaw(buffer, points)
                : PoiMeasurementService.Calculate(buffer, points));
        }

        private PoiMeasurementBuffer GetMeasurementBuffer()
        {
            lock (_bufferSync)
            {
                return _measurementBuffer
                    ?? throw new InvalidOperationException("当前 CVCIE 视图没有可用的测量缓冲区。");
            }
        }

        internal ValueTask<PoiMeasurementBuffer> PrepareMeasurementAsync()
        {
            lock (_bufferSync)
            {
                if (_measurementBuffer != null) return new(_measurementBuffer);
                var file = _fileMeasurements ?? throw new InvalidOperationException("当前图像没有可用的 CIE 测量数据。");
                if (_fileMeasurementTask == null)
                {
                    var cancellation = new CancellationTokenSource();
                    CancellationToken token = cancellation.Token;
                    _fileMeasurementCancellation = cancellation;
                    _fileMeasurementTask = Task.Run(() =>
                    {
                        try
                        {
                            PoiMeasurementBuffer buffer = file.Load(token);
                            lock (_bufferSync)
                            {
                                if (ReferenceEquals(_fileMeasurementCancellation, cancellation))
                                {
                                    _measurementBuffer = buffer;
                                    return buffer;
                                }
                            }
                            buffer.Dispose();
                            throw new OperationCanceledException(token);
                        }
                        catch (Exception error) when (error is not OperationCanceledException)
                        {
                            token.ThrowIfCancellationRequested();
                            log.Warn("CVCIE 测量数据加载失败。", error);
                            throw;
                        }
                    }, token);
                }
                return new(_fileMeasurementTask);
            }
        }

        internal ValueTask<CvcieProbeResult?> CalculateProbeAsync(PoiMeasurementPoint point, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            CvcieFileMeasurements? file;
            PoiMeasurementBuffer? buffer;
            lock (_bufferSync)
            {
                buffer = _measurementBuffer;
                file = _fileMeasurements;
                if (ReferenceEquals(file, _failedFileProbe)) file = null;
            }
            if (buffer != null) return new(CalculateBufferedProbe(buffer, point));
            return file == null ? new((CvcieProbeResult?)null) : new(CalculateFileProbeAsync(file, point, token));
        }

        private async Task<CvcieProbeResult?> CalculateFileProbeAsync(CvcieFileMeasurements file, PoiMeasurementPoint point, CancellationToken token)
        {
            try
            {
                CvcieProbeResult? result = await Task.Run(() => file.CalculateProbe(point, token), token);
                token.ThrowIfCancellationRequested();
                ValueTask<PoiMeasurementBuffer> pending;
                lock (_bufferSync)
                {
                    if (!ReferenceEquals(file, _fileMeasurements)) return null;
                    if (result != null) return result;
                    // Exceptionally large regions use the existing shared full-image measurement snapshot.
                    pending = PrepareMeasurementAsync();
                }
                PoiMeasurementBuffer buffer = await pending;
                token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(buffer, _measurementBuffer)) return null;
                return CalculateBufferedProbe(buffer, point);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException)
            {
                token.ThrowIfCancellationRequested();
                lock (_bufferSync)
                {
                    if (!ReferenceEquals(file, _fileMeasurements) || ReferenceEquals(file, _failedFileProbe)) return null;
                    _failedFileProbe = file;
                }
                log.Warn("CVCIE 探针读取失败。", error);
                return null;
            }
        }

        private static CvcieProbeResult CalculateBufferedProbe(PoiMeasurementBuffer buffer, PoiMeasurementPoint point)
            => new(buffer.Channels, PoiMeasurementService.Calculate(buffer, point));

        private void RetireFileMeasurementLocked()
        {
            _viewPoiRequest?.Cancel();
            _fileMeasurements = null;
            _failedFileProbe = null;
            _fileMeasurementTask = null;
            _fileMeasurementCancellation?.Cancel();
            _fileMeasurementCancellation?.Dispose();
            _fileMeasurementCancellation = null;
        }

        private void ReplaceMeasurementBuffer(PoiMeasurementBuffer? buffer)
        {
            PoiMeasurementBuffer? previous;
            lock (_bufferSync)
            {
                RetireFileMeasurementLocked();
                previous = _measurementBuffer;
                if (!ReferenceEquals(previous, buffer))
                {
                    _embeddedProfileLifetime?.Retire();
                    _embeddedProfileLifetime = null;
                }
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
            EventHandler? cleanup = null;
            cleanup = (_, _) =>
            {
                if (!ReferenceEquals(_bufferCleanup, cleanup)) return;
                if (_bufferOwner != null && _bufferCleanup != null)
                {
                    _bufferOwner.Config.Cleared -= _bufferCleanup;
                }
                ReplaceMeasurementBuffer(null);
                _probeOptions = null;
                _bufferOwner = null;
                _bufferCleanup = null;
            };
            _bufferCleanup = cleanup;
            imageView.Config.Cleared += _bufferCleanup;
        }

        private sealed record CvFileViewMetadata(CVCIEFile Header, int HeaderEnd, ColorVisionTiffParameters Parameters, ColorCalibrationSnapshot? Calibration,
            string? AssociatedRawPath, List<List<Point>>? PointRows, IReadOnlyList<ImageProfileSourceOption>? ProfileSources);

        private static CvFileViewMetadata? ReadCvFileViewMetadata(string filePath, string? pointsPath, Stream? verifiedFile = null)
        {
            int index = verifiedFile == null
                ? CVFileUtil.ReadCIEFileHeader(filePath, out CVCIEFile meta)
                : CVFileUtil.ReadCIEFileHeader(verifiedFile, out meta);
            if (index <= 0)
            {
                meta.Dispose();
                return null;
            }

            bool isCie = string.Equals(Path.GetExtension(filePath), ".cvcie", StringComparison.OrdinalIgnoreCase);
            meta.FileExtType = isCie ? CVType.CIE : CVType.Raw;
            meta.FilePath = filePath;
            ColorVisionTiffParameters parameters = VExportCIE.CreateTiffParameters(meta, "Src");
            if (!isCie)
            {
                ColorCalibrationSnapshot? calibration = null;
                try { calibration = ColorCalibrationSnapshot.Read(filePath, meta); }
                catch (Exception ex) { log.Warn($"RAW 色度参数不可用，保留原图：{filePath}", ex); }
                var profiles = calibration?.CanReplay == true ? CvRawProfileSource.CreateOptions(filePath) : null;
                return new(meta, index, parameters, calibration, null, null, profiles);
            }

            List<List<Point>>? pointRows = null;
            if (File.Exists(pointsPath))
            {
                log.Info("ShowDateFilePath:" + pointsPath);
                string[] lines = File.ReadAllLines(pointsPath);
                string[] dimensions = lines[0].Split(',');
                _ = int.Parse(dimensions[0]);
                _ = int.Parse(dimensions[1]);
                pointRows = new();
                for (int lineIndex = 2; lineIndex < lines.Length; lineIndex++)
                {
                    string[] xy = lines[lineIndex].Split(',');
                    List<Point> points = new();
                    for (int i = 0; i < xy.Length; i += 4)
                    {
                        if (double.TryParse(xy[i], out var x) && double.TryParse(xy[i + 1], out var y))
                            points.Add(new Point(x, y));
                    }
                    pointRows.Add(points);
                }
            }

            return new(meta, index, parameters, null, ResolveAssociatedRawFilePath(filePath, meta), pointRows,
                CvcieProfileSource.CreateOptions(filePath, meta.Channels));
        }

        private CvRawLayerController? InitializeCvFileView(ImageView imageView, string filePath, string displayedLayerId, bool hasRgbLayers,
            CvFileViewMetadata? metadata, CVCIEFile? loadedRaw = null, CvcieFileRevision? fileRevision = null)
        {
            imageView.Config.SetImageMetadata(RawCalibrationInformationProvider.MetadataKey, null, nameof(CVRawOpen), "CVRAW 尾部保存的实际色度校正参数（只读）");
            if (metadata == null) return null;

            imageView.Config.FilePath = filePath;
            CVCIEFile meta = metadata.Header;
            int index = metadata.HeaderEnd;
            imageView.EditorContext.ProcessingContext.ProfileMeasurementSources = metadata.ProfileSources;
            ColorVisionTiffParameters parameterRecord = metadata.Parameters;
            imageView.SetImageParameterRecord(parameterRecord);

            if (meta.FileExtType != CVType.CIE)
            {
                ReplaceMeasurementBuffer(null);
                imageView.Config.SetOpenerRuntime("IsCVCIE", false, nameof(CVRawOpen), "文件仍为 RAW");
                imageView.Config.SetOpenerRuntime("HasCieMeasurements", false, nameof(CVRawOpen), "是否具有 CIE 测量数据源");
                ColorCalibrationSnapshot? snapshot = metadata.Calibration;
                imageView.Config.SetImageMetadata(RawCalibrationInformationProvider.MetadataKey, snapshot, nameof(CVRawOpen), "CVRAW 尾部保存的实际色度校正参数（只读）");
                if (snapshot != null)
                {
                    parameterRecord.CalibrationSummary = snapshot.ToSummary();
                    imageView.SetImageParameterRecord(parameterRecord);
                }
                if (snapshot?.CanReplay == true)
                {
                    CVCIEFile raw = loadedRaw ?? CVFileUtil.OpenLocalCVFile(filePath);
                    try { ReplaceMeasurementBuffer(new PoiMeasurementBuffer(raw, snapshot)); }
                    finally { if (loadedRaw == null) raw.Dispose(); }
                    _probeOptions = CvcieMouseProbeOptions.GetOrCreate(imageView);
                    ShowDateFilePath = false;
                    Points.Clear();
                    RegisterBufferLifecycle(imageView);
                    imageView.Config.SetOpenerRuntime("HasCieMeasurements", true, nameof(CVRawOpen), "RAW 与当前色度参数可按需测量");
                    imageView.Config.SetOpenerRuntime("meta", meta, nameof(CVRawOpen), "RAW 原始文件头");
                    imageView.Config.SetOpenerRuntime("Exp", snapshot.Exposure, nameof(CVRawOpen), "色度校正实际曝光");
                    CvRawLayerController calibrated = CvRawLayerController.CreateCalibratedRaw(imageView, filePath, _measurementBuffer!.RawSource!, displayedLayerId, null);
                    imageView.SetLayerController(calibrated);
                    imageView.EditorContext.IEditorToolFactory.ApplyImageOpenTools(this);
                    return calibrated;
                }
                CvRawLayerController rawController = CvRawLayerController.Create(imageView, filePath, isCie: false, meta.Channels, meta.Bpp, hasRgbLayers: meta.Channels >= 3, displayedLayerId);
                imageView.SetLayerController(rawController);
                return rawController;
            }
            CvcieFileRevision revision = fileRevision ?? throw new InvalidOperationException("缺少 CVCIE 文件版本。");

            ReplaceMeasurementBuffer(null);
            lock (_bufferSync)
            {
                _fileMeasurements = new CvcieFileMeasurements(filePath, revision, index, meta.Version, meta.Cols,
                    meta.Rows, meta.Bpp, meta.Channels);
            }

            ShowDateFilePath = false;
            if (metadata.PointRows != null)
            {
                Points.Clear();
                Points.AddRange(metadata.PointRows);
                ShowDateFilePath = true;
            }

            RegisterBufferLifecycle(imageView);

            CvcieMouseProbeOptions probeOptions = CvcieMouseProbeOptions.GetOrCreate(imageView);
            _probeOptions = probeOptions;
            log.Debug(JsonConvert.SerializeObject(meta));
            imageView.Config.SetOpenerRuntime("IsCVCIE", true, nameof(CVRawOpen), "当前视图是否由 CVCIE 打开器接管");
            imageView.Config.SetOpenerRuntime("HasCieMeasurements", true, nameof(CVRawOpen), "CVCIE 内嵌测量数据");

            if (ReferenceEquals(imageView.EditorContext.IImageOpen, this)
                && string.Equals(imageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath), filePath, StringComparison.Ordinal))
            {
                imageView.EditorContext.IEditorToolFactory.ApplyImageOpenTools(this);
            }

            imageView.Config.SetOpenerRuntime("meta", meta, nameof(CVRawOpen), "CVCIE 文件头和原始缓冲元信息");
            imageView.Config.SetOpenerRuntime("index", index, nameof(CVRawOpen), "CVCIE 数据块索引");
            imageView.Config.SetOpenerRuntime("Exp", meta.Exp, nameof(CVRawOpen), "当前 CVCIE 曝光数组");

            if (!string.IsNullOrWhiteSpace(metadata.AssociatedRawPath))
            {
                meta.SrcFileName = metadata.AssociatedRawPath;
            }

            imageView.Config.SetOpenerRuntime("meta", meta, nameof(CVRawOpen), "CVCIE 文件头和原始缓冲元信息");
            CvRawLayerController controller = CvRawLayerController.Create(imageView, filePath, isCie: true, meta.Channels, meta.Bpp,
                hasRgbLayers, displayedLayerId, revision);
            imageView.SetLayerController(controller);
            return controller;
        }

        public void AttachLiveCvcie(ImageView imageView, uint width, uint height, uint bpp, uint channels, byte[] xyzData, float[] exposure)
        {
            imageView.ReleaseImageContent();
            PoiMeasurementBuffer measurementBuffer = new(
                xyzData,
                checked((int)width),
                checked((int)height),
                checked((int)bpp),
                checked((int)channels));
            ReplaceMeasurementBuffer(measurementBuffer);

            ShowDateFilePath = false;
            Points.Clear();
            RegisterBufferLifecycle(imageView);

            _probeOptions = CvcieMouseProbeOptions.GetOrCreate(imageView);

            imageView.Config.SetImageMetadata("FileExtType", CVType.CIE, nameof(CVRawOpen), "当前视图以本地内存 CVCIE 结果接入");
            imageView.Config.SetOpenerRuntime("IsCVCIE", true, nameof(CVRawOpen), "当前视图是否由 CVCIE 打开器接管");
            imageView.Config.SetOpenerRuntime("Exp", exposure, nameof(CVRawOpen), "当前内存 CVCIE 曝光数组");

            imageView.EditorContext.IImageOpen = this;
            imageView.AttachImageContentCapability(this);

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
            if (!EditorContext.Config.GetProperties<bool>("IsCVCIE") && !EditorContext.Config.GetProperties<bool>("HasCieMeasurements"))
            {
                yield break;
            }

            _cvcieMouseMagnifierManager ??= new CvcieMouseMagnifierManager(
                EditorContext,
                CalculateProbeAsync,
                () => ShowDateFilePath,
                FindNearbyPoints,
                () => _probeOptions ?? CvcieMouseProbeOptions.GetOrCreate(EditorContext.ImageView));

            _cvcieDiagramEditorTool ??= new CvcieDiagramEditorTool(
                EditorContext,
                CalculateProbeAsync,
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
            public required Func<bool> MatchesGeometry { get; init; }
            public PoiMeasurementPoint MeasurementPoint { get; init; }
            public ClosedPixelRegion? Region { get; init; }
        }

        private List<ViewPoiRequest> CreateViewPoiRequests()
        {
            List<ViewPoiRequest> requests = new();
            foreach (var drawing in EditorContext.DrawingVisualLists)
            {
                BaseProperties properties = drawing.BaseAttribute;
                if (properties is PolygonProperties { IsClosed: false })
                    throw new ArgumentException($"多边形 {properties.Id} 尚未闭合，请在图形属性或右键菜单中启用“闭合区域”。");
                ClosedPixelRegion? region = properties switch
                {
                    CircleProperties ellipse when ellipse.Radius != ellipse.RadiusY => ClosedPixelRegion.Ellipse(ellipse.Center, ellipse.Radius, ellipse.RadiusY, ellipse.Rotation),
                    RectangleProperties rectangle when rectangle.Rotation != 0 => ClosedPixelRegion.Polygon(new[] { rectangle.Rect.TopLeft, rectangle.Rect.TopRight, rectangle.Rect.BottomRight, rectangle.Rect.BottomLeft }, rectangle.Rotation),
                    PolygonProperties polygon when polygon.IsClosed => ClosedPixelRegion.Polygon(polygon.Points, polygon.Rotation),
                    _ => null
                };
                if (region != null)
                {
                    Rect bounds = RegionGeometry.LocalBounds((RegionProperties)properties);
                    requests.Add(new ViewPoiRequest
                    {
                        DrawProperties = properties,
                        Region = region,
                        MatchesGeometry = CaptureGeometry((RegionProperties)properties),
                        Point = new PoiPoint
                        {
                            Name = properties is ITextProperties text ? text.Text : properties.Id.ToString(),
                            PixelX = bounds.X + bounds.Width / 2, PixelY = bounds.Y + bounds.Height / 2,
                            Width = bounds.Width, Height = bounds.Height,
                            PointType = region.IsEllipse ? PoiShape.Circle : properties is RectangleProperties ? PoiShape.Rect : PoiShape.Polygon,
                            RegionShapeName = region.IsEllipse ? "椭圆" : properties is RectangleProperties ? "矩形" : "多边形"
                        }
                    });
                    continue;
                }
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
                    MatchesGeometry = CaptureGeometry((RegionProperties)properties),
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

        private static Func<bool> CaptureGeometry(RegionProperties properties)
        {
            double rotation = properties.Rotation;
            if (properties is PolygonProperties polygon)
            {
                Point[] vertices = polygon.Points.ToArray();
                return () => polygon.IsClosed && polygon.Rotation == rotation && polygon.Points.SequenceEqual(vertices);
            }
            Rect bounds = RegionGeometry.LocalBounds(properties);
            return () => properties.Rotation == rotation && RegionGeometry.LocalBounds(properties) == bounds;
        }

        private static (int Channels, PoiMeasurementResult[] Results) CalculateViewPoi(
            PoiMeasurementBuffer buffer, List<ViewPoiRequest> requests, CancellationToken token)
        {
            var results = new PoiMeasurementResult[requests.Count];
            var standard = new List<PoiMeasurementPoint>();
            var indices = new List<int>();
            for (int i = 0; i < requests.Count; i++)
            {
                token.ThrowIfCancellationRequested();
                if (requests[i].Region is ClosedPixelRegion region)
                {
                    try { results[i] = PoiMeasurementService.CalculateRegion(buffer, region, true); }
                    catch (ArgumentException error) { throw new ArgumentException($"区域 {requests[i].Point.Name}：{error.Message}", error); }
                }
                else { standard.Add(requests[i].MeasurementPoint); indices.Add(i); }
            }
            token.ThrowIfCancellationRequested();
            PoiMeasurementResult[] legacy = PoiMeasurementService.CalculateRaw(buffer, standard);
            for (int i = 0; i < legacy.Length; i++) results[indices[i]] = legacy[i];
            return (buffer.Channels, results);
        }

        private async Task<(int Channels, PoiMeasurementResult[] Results)> CalculateViewPoiAsync(
            List<ViewPoiRequest> regions, CancellationToken token)
        {
            CvcieFileMeasurements? file;
            lock (_bufferSync)
            {
                file = _measurementBuffer == null && _fileMeasurementTask is not { IsCompleted: false }
                    ? _fileMeasurements : null;
            }

            if (file != null)
            {
                var requests = regions.Select(region => new CvcieFilePoiRequest(region.MeasurementPoint, region.Region, region.Point.Name)).ToArray();
                var sparse = await Task.Run(() => file.TryCalculateSparse(requests, token), token);
                token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(file, _fileMeasurements)) throw new OperationCanceledException(token);
                if (sparse is { } measured) return measured;
            }

            PoiMeasurementBuffer buffer = await PrepareMeasurementAsync();
            token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(buffer, _measurementBuffer)) throw new OperationCanceledException(token);
            using PoiMeasurementBuffer? snapshot = buffer.SharePlanarData();
            return snapshot == null
                ? CalculateViewPoi(buffer, regions, token)
                : await Task.Run(() => CalculateViewPoi(snapshot, regions, token), token);
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
            properties.IsMeasurementMessage = true;
            properties.Msg = message;
        }

        private static void SetColorMessage(BaseProperties properties, PoiResultCIExyuvData result, bool show)
        {
            if (!show || !CVCIEShowConfig.Instance.IsShowString) return;
            properties.IsMeasurementMessage = true;
            properties.Msg = FormatMessage(CVCIEShowConfig.Instance.Template, result);
        }

        internal async Task ShowViewPoiAsync()
        {
            ImageView view = EditorContext.ImageView;
            view.Dispatcher.VerifyAccess();
            using var request = new CancellationTokenSource();
            lock (_bufferSync)
            {
                if (_viewPoiRequest != null) return;
                _viewPoiRequest = request;
            }

            long contentVersion = view.ContentVersion;
            CancellationToken token = request.Token;
            bool IsCurrent() => !token.IsCancellationRequested && contentVersion == view.ContentVersion;
            try
            {
                CommandManager.InvalidateRequerySuggested();
                Func<double, double> normalize = CVCIEShowConfig.Instance.CreateValueNormalizer();
                List<ViewPoiRequest> regions = CreateViewPoiRequests();
                if (regions.Count == 0) throw new ArgumentException("请先绘制圆、椭圆、矩形或闭合多边形；开放折线需要先闭合。");

                (int channels, PoiMeasurementResult[] measurements) = await CalculateViewPoiAsync(regions, token);
                if (!IsCurrent()) return;

                // Results describe the regions captured at command time. Only unchanged drawings receive labels.
                HashSet<BaseProperties>? annotationTargets = EditorContext.DrawingVisualLists.Count < 1000
                    ? new(EditorContext.DrawingVisualLists.Select(drawing => drawing.BaseAttribute), ReferenceEqualityComparer.Instance)
                    : null;
                bool CanAnnotate(ViewPoiRequest region) => IsCurrent()
                    && annotationTargets?.Contains(region.DrawProperties) == true && region.MatchesGeometry();
                Window? owner = Window.GetWindow(view) ?? Application.Current.GetActiveWindow();
                if (channels == 1)
                {
                    ObservableCollection<PoiResultCIEYData> results = new();
                    for (int index = 0; index < measurements.Length; index++)
                    {
                        ViewPoiRequest region = regions[index];
                        PoiResultCIEYData result = new() { Point = region.Point, Y = normalize(measurements[index].Y) };
                        SetLuminanceMessage(region.DrawProperties, result.Y, CanAnnotate(region));
                        results.Add(result);
                    }
                    if (IsCurrent()) new WindowCVCIE(results) { Owner = owner }.Show();
                }
                else
                {
                    ObservableCollection<PoiResultCIExyuvData> results = new();
                    for (int index = 0; index < measurements.Length; index++)
                    {
                        ViewPoiRequest region = regions[index];
                        PoiMeasurementResult measurement = measurements[index];
                        PoiResultCIExyuvData result = new()
                        {
                            Point = region.Point,
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
                        SetColorMessage(region.DrawProperties, result, CanAnnotate(region));
                        results.Add(result);
                    }
                    if (IsCurrent()) new WindowCVCIE(results) { Owner = owner }.Show();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                if (!IsCurrent()) return;
                log.Error("CVCIE POI calculation failed.", error);
                MessageBox.Show(error.Message, LocalizedText.Get("POI 区域测量"), MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                lock (_bufferSync) _viewPoiRequest = null;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public List<MenuItemMetadata> GetContextMenuItems()
        {
            List<MenuItemMetadata> menuItems = new List<MenuItemMetadata>();
            if (ReferenceEquals(EditorContext.ImageView.ImageContentCapability, this) && EditorContext.Config.GetProperties<bool>("HasCieMeasurements") && EditorContext.ImageView.ImageContent is { } content)
            {
                menuItems.Add(new MenuItemMetadata
                {
                    Header = "导出测量原图…", GuidId = "CalibratedImageExport", Order = 300,
                    Command = new RelayCommand(_ =>
                    {
                        if (!ReferenceEquals(EditorContext.ImageView.ImageContent, content) || !ReferenceEquals(EditorContext.ImageView.ImageContentCapability, this)) return;
                        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "TIFF|*.tif|PNG|*.png", FileName = System.IO.Path.GetFileNameWithoutExtension(content.FilePath) };
                        if (dialog.ShowDialog() != true) return;
                        if (!ReferenceEquals(EditorContext.ImageView.ImageContent, content) || !ReferenceEquals(EditorContext.ImageView.ImageContentCapability, this)) return;
                        try { CalibratedImageExport.Save(content.Pixels, content.Parameters, dialog.FileName); }
                        catch (Exception error) { MessageBox.Show(error.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning); }
                    })
                });
            }
            if (CVFileUtil.IsCIEFile(GetCurrentFilePath() ?? string.Empty)) menuItems.Add(new MenuItemMetadata()
            {
                Header = ColorVision.Engine.Properties.Resources.Export,
                GuidId = "CVCIEExport",
                Order = 301,
                Command = new RelayCommand(a =>
                {
                    if (EditorContext.Config.GetProperties<string>("FilePath") is string FilePath && (CVFileReadCache.GetCachedLength(FilePath).HasValue || File.Exists(FilePath)))
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
            
            if (EditorContext.Config.GetProperties<bool>("IsCVCIE") || EditorContext.Config.GetProperties<bool>("HasCieMeasurements"))
            {
                MenuItemMetadata menuItemMetadata = new MenuItemMetadata()
                {
                    Header = "POI",
                    GuidId = "POI",
                    Order = 303,
                    Command = new RelayCommand(async _ => await ShowViewPoiAsync(), _ => _viewPoiRequest == null)
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


        public async Task ReleaseContentAsync(bool reuseBuffers)
        {
            Interlocked.Increment(ref _latestOpenRequest);
            ReplaceMeasurementBuffer(null);
            _probeOptions = null;
            if (_bufferOwner != null && _bufferCleanup != null)
                _bufferOwner.Config.Cleared -= _bufferCleanup;
            _bufferOwner = null;
            _bufferCleanup = null;
            // A subsequent RAW read uses the same gate and may reuse its allocation after old readers exit.
            Task readers;
            lock (_bufferSync) readers = _retiredReaders;
            if (reuseBuffers)
            {
                await readers;
                return;
            }
            // Detach now: a later open must never be cleared by this asynchronous retirement.
            CvRawPixelBuffer retired = _rawPixels;
            _rawPixels = new();
            // Never block the UI waiting for a load that may itself be waiting to publish on the UI.
            await _fileOpenGate.WaitAsync();
            try
            {
                await readers;
                retired.Clear();
            }
            finally { _fileOpenGate.Release(); }
        }

        public async void OpenImage(EditorContext context, string? filePath)
        {
            try { await OpenImageAsync(context, filePath); }
            catch (OperationCanceledException) { }
            catch (Exception error) { log.Error("图像打开失败。", error); }
        }

        public async Task OpenImageAsync(EditorContext context, string? filePath, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

            context.ImageView.AttachImageContentCapability(null);
            string requestedFilePath = filePath;
            long requestId = Interlocked.Increment(ref _latestOpenRequest);
            bool isRaw = string.Equals(Path.GetExtension(filePath), ".cvraw", StringComparison.OrdinalIgnoreCase);
            bool isCie = string.Equals(Path.GetExtension(filePath), ".cvcie", StringComparison.OrdinalIgnoreCase);
            string? pointsPath = isCie ? ViewAlgorithmConfig.Instance.ShowDateFilePath : null;
            CvRawPixelBuffer rawPixels = _rawPixels;
            // Config.ClearProperties normally does this first. Also protect callers that invoke the opener directly.
            ReplaceMeasurementBuffer(null);
            CvcieDisplayConfig displayConfig = CvcieDisplayConfig.Current;
            bool preferSrgb = displayConfig.EnableTrueColor && isCie;
            CvcieBrightnessMode brightnessMode = displayConfig.BrightnessMode;
            double referenceWhite = displayConfig.ReferenceWhiteLuminance;
            ImageView view = context.ImageView;

            void VerifyOpenTarget()
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? activeFilePath = context.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
                if (requestId != Volatile.Read(ref _latestOpenRequest)
                    || !string.Equals(activeFilePath, requestedFilePath, StringComparison.OrdinalIgnoreCase))
                    throw new OperationCanceledException("Image open request was superseded.", cancellationToken);
            }

            bool gateEntered = false;
            try
            {
                // Finish the previous file load before another request allocates image-sized buffers.
                await _fileOpenGate.WaitAsync(cancellationToken);
                gateEntered = true;
                if (isRaw)
                {
                    Task readers;
                    lock (_bufferSync) readers = _retiredReaders;
                    await readers.WaitAsync(cancellationToken);
                }
                await Task.Run(async () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (requestId != Volatile.Read(ref _latestOpenRequest))
                        throw new OperationCanceledException("Image open request was superseded.", cancellationToken);
                    CvcieFileRevision? fileRevision = isCie ? CvcieFileRevision.Capture(requestedFilePath) : null;
                    WriteableBitmap? srgb = null;
                    if (preferSrgb)
                    {
                        try
                        {
                            srgb = CvRawLayerController.LoadSrgb(requestedFilePath, brightnessMode, referenceWhite, cancellationToken);
                        }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex)
                        {
                            log.Warn($"CVCIE 真彩转换失败，尝试 CVRAW 或 Y 灰度回退：{requestedFilePath}", ex);
                        }
                    }
                    bool usesLuminance = false;
                    WriteableBitmap? displayBitmap = srgb;
                    // Keep the verified selected file stable through publication, even if the display uses an associated source.
                    using FileStream? publicationRead = fileRevision?.OpenRead(requestedFilePath);
                    // A successful XYZ render needs only metadata, not another RAW/Y payload and conversion.
                    using CVCIEFile? displayFile = srgb != null ? null
                        : isRaw ? rawPixels.Read(requestedFilePath)
                        : CvRawLayerController.LoadSourceForDisplay(requestedFilePath, out usesLuminance, out displayBitmap, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    CvFileViewMetadata? viewMetadata = ReadCvFileViewMetadata(requestedFilePath, pointsPath, publicationRead);
                    CVCIEFile cVCIEFile = displayFile ?? viewMetadata?.Header
                        ?? throw new InvalidDataException($"CVCIE 文件头无效：{requestedFilePath}");
                    if (displayBitmap == null && cVCIEFile.Channels == 1 && cVCIEFile.Bpp is 32 or 64)
                        displayBitmap = MediaHelper.RenderFloatChannel(cVCIEFile, cancellationToken);
                    using OpenCvSharp.Mat? mat = displayBitmap == null ? cVCIEFile.ToMat(showErrors: false) : null;
                    if (displayBitmap == null && (mat == null || mat.Empty()))
                        throw new InvalidDataException("The file contains no displayable image pixels.");
                    cancellationToken.ThrowIfCancellationRequested();

                    await view.Dispatcher.InvokeAsync(async () =>
                    {
                        VerifyOpenTarget();
                        WriteableBitmap? editableDisplay = null;
                        (ImageSource? Source, long Revision, long ContentVersion)? stagingTarget = null;
                        if (displayBitmap is { IsFrozen: true } frozen && EditableBitmapCopy.ShouldCopyInBackground(frozen))
                        {
                            editableDisplay = await EditableBitmapCopy.CreateAsync(frozen, cancellationToken);
                        }
                        // Reusing a compatible bitmap avoids allocating a full display buffer.
                        else if (mat != null && MediaHelper.TryGetDisplayPixelFormat(mat.Type(), out var format)
                            && EditableBitmapCopy.ShouldCopyInBackground(mat.Cols, mat.Rows, format.BitsPerPixel)
                            && !MediaHelper.IsCompatibleDisplayBitmap(view.ViewBitmapSource as WriteableBitmap, mat, format))
                        {
                            stagingTarget = (view.ViewBitmapSource, view.ImageRevision, view.ContentVersion);
                            editableDisplay = await mat.PrepareDisplayBitmapAsync(format, cancellationToken);
                        }

                        VerifyStagingTarget();
                        bool published = false;
                        using var publication = view.BeginImageContentPublication(() => published);
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
                            ? viewMetadata?.AssociatedRawPath != null : !usesLuminance);
                        CvRawLayerController? controller = InitializeCvFileView(view, requestedFilePath,
                            srgb != null ? "cie-srgb" : usesLuminance ? "cie-y" : "composite", hasRgbLayers, viewMetadata, cVCIEFile, fileRevision);
                        VerifyStagingTarget();
                        if (srgb != null) controller?.CacheSrgb(srgb, brightnessMode, referenceWhite);

                        WriteableBitmap source;
                        bool preservesView = false;
                        if (editableDisplay != null || displayBitmap != null)
                        {
                            source = editableDisplay ?? displayBitmap!.Clone();
                        }
                        else if (view.ViewBitmapSource is WriteableBitmap current && mat!.MatUpdateWriteableBitmap(current))
                        {
                            source = current;
                            preservesView = true;
                        }
                        else
                        {
                            source = mat!.CreateDisplayBitmap();
                        }

                        long revision = view.ImageRevision;
                        long contentVersion = view.ContentVersion;
                        if (preservesView)
                        {
                            int displayChannels = mat!.Channels();
                            int displayDepth = checked((int)mat.ElemSize1() * 8);
                            int displayStride = checked(mat.Cols * displayChannels * (displayDepth / 8));
                            context.Config.SetImageMetadata(ImageViewPropertyKeys.PixelFormat, source.Format, nameof(CVRawOpen), "当前显示位图像素格式");
                            context.Config.SetImageMetadata(ImageViewPropertyKeys.Channel, displayChannels, nameof(CVRawOpen), "当前显示位图通道数");
                            context.Config.SetImageMetadata(ImageViewPropertyKeys.Depth, displayDepth, nameof(CVRawOpen), "当前显示位图位深");
                            context.Config.SetImageMetadata(ImageViewPropertyKeys.Stride, displayStride, nameof(CVRawOpen), "当前显示位图行跨度");
                            context.Config.SetImageMetadata(ImageViewPropertyKeys.DpiX, source.DpiX, nameof(CVRawOpen), "当前 CVCIE 图像水平 DPI");
                            context.Config.SetImageMetadata(ImageViewPropertyKeys.DpiY, source.DpiY, nameof(CVRawOpen), "当前 CVCIE 图像垂直 DPI");
                            // Publish the reused source before revision callbacks observe the update.
                            context.ProcessingContext.Presentation.Publish(source, context.FunctionImage);
                            VerifyOpenTarget();
                            var profileSources = context.ProcessingContext.ProfileMeasurementSources;
                            context.CommitSourcePixels(source);
                            VerifyPublishedSource();
                            // The pixel revision invalidates derived sources, including the newly loaded options.
                            context.ProcessingContext.ProfileMeasurementSources = profileSources;
                        }
                        else
                        {
                            view.SetImageSource(source, view.EnableEditorImageServices, configureDefaultLayerController: false);
                            VerifyPublishedSource();
                            view.UpdateZoomAndScale();
                        }
                        VerifyPublishedSource();
                        view.AttachImageContentCapability(this);
                        VerifyOpenTarget();
                        published = true;

                        void VerifyStagingTarget()
                        {
                            VerifyOpenTarget();
                            if (stagingTarget is { } target && (!view.IsCurrentImageRevision(target.Revision)
                                || view.ContentVersion != target.ContentVersion || !ReferenceEquals(view.ViewBitmapSource, target.Source)))
                                throw new OperationCanceledException("The target image changed during display preparation.", cancellationToken);
                        }

                        void VerifyPublishedSource()
                        {
                            VerifyOpenTarget();
                            if (!view.IsCurrentImageRevision(revision + 1) || view.ContentVersion != contentVersion + 1
                                || !ReferenceEquals(view.ViewBitmapSource, source))
                                throw new OperationCanceledException("The target image changed during source publication.", cancellationToken);
                        }
                    }).Task.Unwrap().ConfigureAwait(false);
                }, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                log.Error($"打开 CVCIE 图像失败：{requestedFilePath}", ex);
                throw;
            }
            finally { if (gateEntered) _fileOpenGate.Release(); }
        }

        internal void InitializeEmbedded(PoiMeasurementBuffer buffer, ColorCalibrationSnapshot? snapshot, string path, BitmapSource? sourcePixels)
        {
            ReplaceMeasurementBuffer(buffer);
            _probeOptions = CvcieMouseProbeOptions.GetOrCreate(EditorContext.ImageView);
            ShowDateFilePath = false;
            Points.Clear();
            EditorContext.Config.SetOpenerRuntime("IsCVCIE", false, nameof(CalibratedImageCapability), "保留实际图像文件格式");
            EditorContext.Config.SetOpenerRuntime("HasCieMeasurements", true, nameof(CalibratedImageCapability), "当前内容提供校正测量数据");
            EditorContext.Config.SetImageMetadata(RawCalibrationInformationProvider.MetadataKey, snapshot, nameof(CalibratedImageCapability), "图像内嵌的实际校正参数");
            EmbeddedProfileLifetime profileLifetime = new();
            lock (_bufferSync) _embeddedProfileLifetime = profileLifetime;
            EditorContext.ProcessingContext.ProfileMeasurementSources = EmbeddedImageProfileSource.CreateOptions(buffer, snapshot, profileLifetime);
            if (buffer.RawSource != null)
                EditorContext.ImageView.SetLayerController(CvRawLayerController.CreateCalibratedRaw(EditorContext.ImageView, path, buffer.RawSource, "composite", sourcePixels));
        }

        public virtual void Activate() => Volatile.Write(ref _activationVersion, EditorContext.ImageView.ContentVersion);

        public virtual void Dispose() => DetachForRetirement(EditorContext.ImageView.ContentVersion)();

        public virtual Action DetachForRetirement(long retirementVersion)
        {
            long activationVersion = Volatile.Read(ref _activationVersion);
            PoiMeasurementBuffer? buffer;
            ImageView? owner;
            EventHandler? cleanup;
            TaskCompletionSource? readersExited = null;
            lock (_bufferSync)
            {
                buffer = _measurementBuffer;
                _measurementBuffer = null;
                _embeddedProfileLifetime?.Retire();
                _embeddedProfileLifetime = null;
                RetireFileMeasurementLocked();
                _probeOptions = null;
                owner = _bufferOwner;
                cleanup = _bufferCleanup;
                _bufferOwner = null;
                _bufferCleanup = null;
                if (buffer != null)
                {
                    readersExited = new(TaskCreationOptions.RunContinuationsAsynchronously);
                    _retiredReaders = _retiredReaders.IsCompleted ? readersExited.Task : Task.WhenAll(_retiredReaders, readersExited.Task);
                }
            }
            if (owner != null && cleanup != null) owner.Config.Cleared -= cleanup;
            void RetireUi()
            {
                // CVRawOpen is reusable. A queued cleanup belongs only to its old activation.
                if (Volatile.Read(ref _activationVersion) != activationVersion) return;
                OnEditorToolsDeactivated(EditorContext);
                if (activationVersion >= 0 && EditorContext.ImageView.ContentVersion == retirementVersion
                    && (EditorContext.ImageView.ImageContentCapability == null || ReferenceEquals(EditorContext.ImageView.ImageContentCapability, this)))
                {
                    EditorContext.Config.SetOpenerRuntime("HasCieMeasurements", false, nameof(CalibratedImageCapability), "内容测量能力已失效");
                    EditorContext.Config.SetOpenerRuntime("IsCVCIE", false, nameof(CalibratedImageCapability), "内容测量能力已失效");
                    EditorContext.ProcessingContext.ProfileMeasurementSources = null;
                }
            }
            return () =>
            {
                try { buffer?.Dispose(); }
                finally
                {
                    readersExited?.TrySetResult();
                    if (EditorContext.ImageView.Dispatcher.CheckAccess()) RetireUi();
                    else _ = EditorContext.ImageView.Dispatcher.BeginInvoke((Action)RetireUi);
                }
            };
        }

    }

    [FileExtension(".cvraw|.cvcie")]
    public record class CVRawOpen(EditorContext EditorContext) : CalibratedImageCapability(EditorContext), IPreparedImageOpen;
}
