using Cv = OpenCvSharp;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace YoloObjectDetection;

public partial class MainWindow : Window, IDisposable
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new() { WriteIndented = true };

    private readonly object _lastFrameLock = new();
    private readonly string _baseDirectory = Path.GetDirectoryName(typeof(MainWindow).Assembly.Location) ?? AppDomain.CurrentDomain.BaseDirectory;
    private readonly Stopwatch _fpsStopwatch = new();

    private AppSettings _settings = new();
    private YoloDetector? _detector;
    private CancellationTokenSource? _captureCts;
    private Task? _captureTask;
    private Cv.VideoCapture? _activeCapture;
    private Cv.Mat? _lastRenderedFrame;
    private FrameDetectionResult? _lastResult;

    private int _frameCount;
    private float _confidenceThreshold = 0.4f;
    private float _nmsThreshold = 0.5f;
    private bool _useDetectionRoi;
    private bool _useInspectionRegions;
    private string _modelPath = string.Empty;
    private string _classesPath = string.Empty;

    private static readonly Cv.Scalar BoxColor = new(0, 255, 100);
    private static readonly Cv.Scalar TextColor = Cv.Scalar.White;
    private static readonly Cv.Scalar RoiColor = new(0, 180, 255);
    private static readonly Cv.Scalar RegionOkColor = new(70, 210, 80);
    private static readonly Cv.Scalar RegionNgColor = new(55, 55, 230);

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += OnClosed;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            TxtStatus.Text = "正在加载配置...";

            _settings = AppSettings.Load(Path.Combine(_baseDirectory, "appsettings.json"));
            _confidenceThreshold = Math.Clamp(_settings.ConfidenceThreshold, 0.01f, 0.95f);
            _nmsThreshold = Math.Clamp(_settings.NmsThreshold, 0.05f, 0.95f);
            _useDetectionRoi = _settings.DetectionRoi.Enabled;
            _useInspectionRegions = _settings.InspectionRegions.Any(region => region.Enabled);

            ApplySettingsToControls();

            _modelPath = AppSettings.ResolvePath(_baseDirectory, _settings.ModelPath);
            _classesPath = AppSettings.ResolvePath(_baseDirectory, _settings.ClassesPath);

            if (!File.Exists(_modelPath))
            {
                TxtStatus.Text = $"找不到模型文件: {_modelPath}";
                return;
            }

            string[] classNames = ClassNames.Load(_classesPath);
            _detector = new YoloDetector(_modelPath, classNames);

            UpdateModelInfo();
            TxtStatus.Text = "就绪";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"初始化失败: {ex.Message}";
        }
    }

    private void ApplySettingsToControls()
    {
        SldConfidence.Value = _confidenceThreshold;
        SldNms.Value = _nmsThreshold;
        ChkUseRoi.IsChecked = _useDetectionRoi;
        ChkUseInspection.IsChecked = _useInspectionRegions;
        UpdateThresholdLabels();
        UpdateRoiInfo();
    }

    private async void OnOpenImageClick(object sender, RoutedEventArgs e)
    {
        await StopStreamAsync();

        var dialog = new OpenFileDialog
        {
            Title = "打开图片",
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.webp|All Files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        await ProcessImageFileAsync(dialog.FileName);
    }

    private async Task ProcessImageFileAsync(string fileName)
    {
        if (_detector is null)
        {
            TxtStatus.Text = "模型尚未加载";
            return;
        }

        using var image = Cv.Cv2.ImRead(fileName, Cv.ImreadModes.Color);
        if (image.Empty())
        {
            TxtStatus.Text = "图片读取失败";
            return;
        }

        TxtStatus.Text = $"图片检测中: {Path.GetFileName(fileName)}";
        try
        {
            var processed = await Task.Run(() => ProcessFrame(image, fileName));
            using (processed.RenderedFrame)
            {
                ShowProcessedFrame(processed, fps: null);
            }

            TxtFps.Text = "FPS: --";
            TxtStatus.Text = $"图片检测完成: {Path.GetFileName(fileName)}";
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"图片检测失败: {ex.Message}";
        }
    }

    private async void OnOpenVideoClick(object sender, RoutedEventArgs e)
    {
        await StopStreamAsync();

        var dialog = new OpenFileDialog
        {
            Title = "打开视频",
            Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov;*.wmv|All Files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
            return;

        var capture = new Cv.VideoCapture(dialog.FileName);
        if (!capture.IsOpened())
        {
            capture.Dispose();
            TxtStatus.Text = "视频打开失败";
            return;
        }

        StartCapture(capture, dialog.FileName, "视频检测中");
    }

    private async void OnStartCameraClick(object sender, RoutedEventArgs e)
    {
        await StopStreamAsync();

        var capture = new Cv.VideoCapture(_settings.CameraIndex, Cv.VideoCaptureAPIs.DSHOW);
        if (!capture.IsOpened())
        {
            capture.Dispose();
            TxtStatus.Text = $"无法打开摄像头: {_settings.CameraIndex}";
            return;
        }

        capture.Set(Cv.VideoCaptureProperties.FrameWidth, _settings.CameraWidth);
        capture.Set(Cv.VideoCaptureProperties.FrameHeight, _settings.CameraHeight);
        capture.Set(Cv.VideoCaptureProperties.Fps, _settings.CameraFps);
        capture.Set(Cv.VideoCaptureProperties.BufferSize, 1);

        StartCapture(capture, $"Camera {_settings.CameraIndex}", "摄像头检测中");
    }

    private async void OnStopClick(object sender, RoutedEventArgs e)
    {
        await StopStreamAsync();
        TxtStatus.Text = "已停止";
    }

    private void StartCapture(Cv.VideoCapture capture, string source, string status)
    {
        if (_detector is null)
        {
            capture.Dispose();
            TxtStatus.Text = "模型尚未加载";
            return;
        }

        _activeCapture = capture;
        _captureCts = new CancellationTokenSource();
        _frameCount = 0;
        _fpsStopwatch.Restart();
        TxtStatus.Text = status;

        CancellationToken token = _captureCts.Token;
        _captureTask = Task.Run(() => CaptureLoop(capture, source, token), token);
    }

    private void CaptureLoop(Cv.VideoCapture capture, string source, CancellationToken token)
    {
        try
        {
            using (capture)
            using (var frame = new Cv.Mat())
            {
                while (!token.IsCancellationRequested)
                {
                    if (!capture.Read(frame) || frame.Empty())
                    {
                        Dispatcher.InvokeAsync(() => TxtStatus.Text = "视频流结束或帧读取失败");
                        break;
                    }

                    var processed = ProcessFrame(frame, source);
                    double? fps = CountFrameAndGetFps();

                    Dispatcher.Invoke(() =>
                    {
                        using (processed.RenderedFrame)
                        {
                            ShowProcessedFrame(processed, fps);
                        }
                    }, DispatcherPriority.Render, CancellationToken.None);
                }
            }
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            Dispatcher.InvokeAsync(() => TxtStatus.Text = $"视频流错误: {ex.Message}");
        }
        finally
        {
            if (ReferenceEquals(_activeCapture, capture))
                _activeCapture = null;
        }
    }

    private double? CountFrameAndGetFps()
    {
        _frameCount++;
        if (_fpsStopwatch.ElapsedMilliseconds < 1000)
            return null;

        double fps = _frameCount * 1000.0 / _fpsStopwatch.ElapsedMilliseconds;
        _frameCount = 0;
        _fpsStopwatch.Restart();
        return fps;
    }

    private async Task StopStreamAsync()
    {
        var cts = _captureCts;
        var task = _captureTask;
        var capture = _activeCapture;

        _captureCts = null;
        _captureTask = null;
        _activeCapture = null;

        if (cts is null)
            return;

        cts.Cancel();
        capture?.Release();

        if (task is not null)
        {
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
            }
        }

        cts.Dispose();
        _fpsStopwatch.Reset();
        _frameCount = 0;
    }

    private ProcessedFrame ProcessFrame(Cv.Mat frame, string source)
    {
        if (_detector is null)
            throw new InvalidOperationException("模型尚未加载。");

        Cv.Rect? detectionRoi = _useDetectionRoi
            ? GetValidRect(_settings.DetectionRoi, frame.Width, frame.Height)
            : null;

        IReadOnlyList<Detection> detections;
        var inferenceStopwatch = Stopwatch.StartNew();

        if (detectionRoi is { } roi)
        {
            using var roiFrame = new Cv.Mat(frame, roi);
            detections = TranslateDetections(_detector.Detect(roiFrame, _confidenceThreshold, _nmsThreshold), roi);
        }
        else
        {
            detections = _detector.Detect(frame, _confidenceThreshold, _nmsThreshold).ToList();
        }

        inferenceStopwatch.Stop();

        var renderedFrame = frame.Clone();
        if (detectionRoi is { } activeRoi)
            DrawRoi(renderedFrame, activeRoi, _settings.DetectionRoi.Name);

        var inspectionRegions = _useInspectionRegions
            ? EvaluateInspectionRegions(detections, frame.Width, frame.Height)
            : [];

        string inspectionStatus = GetInspectionStatus(inspectionRegions, _useInspectionRegions);
        DrawInspectionRegions(renderedFrame, inspectionRegions);
        DrawDetections(renderedFrame, detections);
        DrawInspectionBadge(renderedFrame, inspectionStatus);

        var result = BuildFrameResult(
            source,
            frame.Width,
            frame.Height,
            inferenceStopwatch.Elapsed.TotalMilliseconds,
            detectionRoi,
            inspectionStatus,
            inspectionRegions,
            detections);

        StoreLastFrame(renderedFrame, result);
        return new ProcessedFrame(renderedFrame, result);
    }

    private static List<Detection> TranslateDetections(IReadOnlyList<Detection> detections, Cv.Rect roi)
    {
        var translated = new List<Detection>(detections.Count);
        foreach (var detection in detections)
        {
            var box = detection.BoundingBox;
            translated.Add(new Detection(
                new Cv.Rect(box.X + roi.X, box.Y + roi.Y, box.Width, box.Height),
                detection.ClassId,
                detection.ClassName,
                detection.Confidence));
        }

        return translated;
    }

    private List<InspectionRegionResult> EvaluateInspectionRegions(IReadOnlyList<Detection> detections, int frameWidth, int frameHeight)
    {
        var results = new List<InspectionRegionResult>();
        foreach (var region in _settings.InspectionRegions.Where(region => region.Enabled))
        {
            Cv.Rect? rect = GetValidRect(region, frameWidth, frameHeight);
            if (rect is null)
                continue;

            int minCount = Math.Max(1, region.MinCount);
            int foundCount = detections.Count(detection => DetectionMatchesRegion(detection, region.RequiredClass, rect.Value));

            results.Add(new InspectionRegionResult
            {
                Name = region.Name,
                RequiredClass = region.RequiredClass,
                MinCount = minCount,
                FoundCount = foundCount,
                Passed = foundCount >= minCount,
                Region = ToExport(rect.Value)
            });
        }

        return results;
    }

    private static bool DetectionMatchesRegion(Detection detection, string requiredClass, Cv.Rect region)
    {
        if (!string.IsNullOrWhiteSpace(requiredClass) &&
            !string.Equals(detection.ClassName, requiredClass, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int centerX = detection.BoundingBox.X + detection.BoundingBox.Width / 2;
        int centerY = detection.BoundingBox.Y + detection.BoundingBox.Height / 2;
        return centerX >= region.X && centerX <= region.X + region.Width &&
               centerY >= region.Y && centerY <= region.Y + region.Height;
    }

    private static string GetInspectionStatus(List<InspectionRegionResult> regions, bool enabled)
    {
        if (!enabled)
            return "NotEnabled";

        if (regions.Count == 0)
            return "NoRegions";

        return regions.All(region => region.Passed) ? "OK" : "NG";
    }

    private FrameDetectionResult BuildFrameResult(
        string source,
        int frameWidth,
        int frameHeight,
        double inferenceMs,
        Cv.Rect? detectionRoi,
        string inspectionStatus,
        List<InspectionRegionResult> inspectionRegions,
        IReadOnlyList<Detection> detections)
    {
        return new FrameDetectionResult
        {
            Timestamp = DateTime.Now,
            Source = source,
            FrameWidth = frameWidth,
            FrameHeight = frameHeight,
            ConfidenceThreshold = _confidenceThreshold,
            NmsThreshold = _nmsThreshold,
            InferenceMs = inferenceMs,
            DetectionRoi = detectionRoi is { } roi ? ToExport(roi) : null,
            InspectionStatus = inspectionStatus,
            InspectionRegions = inspectionRegions,
            Detections = detections.Select(detection => new DetectionExport
            {
                ClassId = detection.ClassId,
                ClassName = detection.ClassName,
                Confidence = detection.Confidence,
                BoundingBox = ToExport(detection.BoundingBox)
            }).ToList()
        };
    }

    private void StoreLastFrame(Cv.Mat renderedFrame, FrameDetectionResult result)
    {
        lock (_lastFrameLock)
        {
            _lastRenderedFrame?.Dispose();
            _lastRenderedFrame = renderedFrame.Clone();
            _lastResult = result;
        }
    }

    private void ShowProcessedFrame(ProcessedFrame processed, double? fps)
    {
        using var rgbFrame = new Cv.Mat();
        Cv.Cv2.CvtColor(processed.RenderedFrame, rgbFrame, Cv.ColorConversionCodes.BGR2RGB);
        UpdateImage(rgbFrame);

        if (fps is not null)
            TxtFps.Text = $"FPS: {fps.Value:F1}";

        TxtInference.Text = $"Inference: {processed.Result.InferenceMs:F1} ms";
        TxtDetections.Text = $"检测: {processed.Result.Detections.Count}";
        TxtOkNg.Text = FormatInspectionStatus(processed.Result.InspectionStatus);
        UpdateModelInfo();
    }

    private static string FormatInspectionStatus(string status)
    {
        return status switch
        {
            "OK" => "OK/NG: OK",
            "NG" => "OK/NG: NG",
            "NoRegions" => "OK/NG: 无区域",
            _ => "OK/NG: --"
        };
    }

    private void OnThresholdChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TxtConfidenceValue is null || TxtNmsValue is null)
            return;

        _confidenceThreshold = (float)SldConfidence.Value;
        _nmsThreshold = (float)SldNms.Value;
        UpdateThresholdLabels();
    }

    private void OnRoiToggleChanged(object sender, RoutedEventArgs e)
    {
        _useDetectionRoi = ChkUseRoi.IsChecked == true;
        UpdateRoiInfo();
    }

    private void OnInspectionToggleChanged(object sender, RoutedEventArgs e)
    {
        _useInspectionRegions = ChkUseInspection.IsChecked == true;
        UpdateRoiInfo();
    }

    private void UpdateThresholdLabels()
    {
        TxtConfidenceValue.Text = _confidenceThreshold.ToString("F2", CultureInfo.InvariantCulture);
        TxtNmsValue.Text = _nmsThreshold.ToString("F2", CultureInfo.InvariantCulture);
    }

    private void UpdateRoiInfo()
    {
        var roi = _settings.DetectionRoi;
        TxtRoiInfo.Text = _useDetectionRoi
            ? $"{roi.Name}: X={roi.X}, Y={roi.Y}, W={roi.Width}, H={roi.Height}"
            : "ROI: 未启用";

        int enabledRegions = _settings.InspectionRegions.Count(region => region.Enabled);
        TxtInspectionInfo.Text = _useInspectionRegions
            ? $"判定区域: {enabledRegions} 个"
            : "判定区域: 未启用";
    }

    private void UpdateModelInfo()
    {
        if (_detector is null)
        {
            TxtModelInfo.Text = "模型: --";
            return;
        }

        TxtModelInfo.Text =
            $"模型: {Path.GetFileName(_modelPath)}{Environment.NewLine}" +
            $"类别: {Path.GetFileName(_classesPath)} ({_detector.ClassCount}){Environment.NewLine}" +
            $"输入: {_detector.InputWidth}x{_detector.InputHeight}{Environment.NewLine}" +
            $"输出: {_detector.OutputShapeText}{Environment.NewLine}" +
            $"摄像头: {_settings.CameraIndex} / {_settings.CameraWidth}x{_settings.CameraHeight}@{_settings.CameraFps}";
    }

    private void OnSaveFrameClick(object sender, RoutedEventArgs e)
    {
        Cv.Mat? frame = null;
        try
        {
            lock (_lastFrameLock)
            {
                if (_lastRenderedFrame is not null)
                    frame = _lastRenderedFrame.Clone();
            }

            if (frame is null)
            {
                TxtStatus.Text = "没有可保存的当前帧";
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "保存当前帧",
                FileName = $"frame_{DateTime.Now:yyyyMMdd_HHmmss}.png",
                Filter = "PNG Image|*.png|JPEG Image|*.jpg"
            };

            if (dialog.ShowDialog(this) == true)
            {
                Cv.Cv2.ImWrite(dialog.FileName, frame);
                TxtStatus.Text = $"已保存当前帧: {Path.GetFileName(dialog.FileName)}";
            }
        }
        finally
        {
            frame?.Dispose();
        }
    }

    private void OnExportJsonClick(object sender, RoutedEventArgs e)
    {
        FrameDetectionResult? result;
        lock (_lastFrameLock)
        {
            result = _lastResult;
        }

        if (result is null)
        {
            TxtStatus.Text = "没有可导出的检测结果";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出 JSON",
            FileName = $"detections_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            Filter = "JSON File|*.json"
        };

        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(result, ExportJsonOptions), new UTF8Encoding(false));
            TxtStatus.Text = $"已导出 JSON: {Path.GetFileName(dialog.FileName)}";
        }
    }

    private void OnExportCsvClick(object sender, RoutedEventArgs e)
    {
        FrameDetectionResult? result;
        lock (_lastFrameLock)
        {
            result = _lastResult;
        }

        if (result is null)
        {
            TxtStatus.Text = "没有可导出的检测结果";
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出 CSV",
            FileName = $"detections_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Filter = "CSV File|*.csv"
        };

        if (dialog.ShowDialog(this) == true)
        {
            File.WriteAllText(dialog.FileName, BuildCsv(result), new UTF8Encoding(false));
            TxtStatus.Text = $"已导出 CSV: {Path.GetFileName(dialog.FileName)}";
        }
    }

    private static string BuildCsv(FrameDetectionResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("timestamp,source,frame_width,frame_height,confidence_threshold,nms_threshold,inference_ms,inspection_status,detection_count,class_id,class_name,confidence,x,y,width,height");

        if (result.Detections.Count == 0)
        {
            builder.AppendLine(string.Join(',', BuildFrameCsvPrefix(result).Concat(["", "", "", "", "", "", ""])));
            return builder.ToString();
        }

        foreach (var detection in result.Detections)
        {
            var fields = BuildFrameCsvPrefix(result).Concat([
                detection.ClassId.ToString(CultureInfo.InvariantCulture),
                CsvEscape(detection.ClassName),
                detection.Confidence.ToString("F4", CultureInfo.InvariantCulture),
                detection.BoundingBox.X.ToString(CultureInfo.InvariantCulture),
                detection.BoundingBox.Y.ToString(CultureInfo.InvariantCulture),
                detection.BoundingBox.Width.ToString(CultureInfo.InvariantCulture),
                detection.BoundingBox.Height.ToString(CultureInfo.InvariantCulture)
            ]);
            builder.AppendLine(string.Join(',', fields));
        }

        return builder.ToString();
    }

    private static IEnumerable<string> BuildFrameCsvPrefix(FrameDetectionResult result)
    {
        return [
            CsvEscape(result.Timestamp.ToString("O", CultureInfo.InvariantCulture)),
            CsvEscape(result.Source),
            result.FrameWidth.ToString(CultureInfo.InvariantCulture),
            result.FrameHeight.ToString(CultureInfo.InvariantCulture),
            result.ConfidenceThreshold.ToString("F2", CultureInfo.InvariantCulture),
            result.NmsThreshold.ToString("F2", CultureInfo.InvariantCulture),
            result.InferenceMs.ToString("F2", CultureInfo.InvariantCulture),
            CsvEscape(result.InspectionStatus),
            result.Detections.Count.ToString(CultureInfo.InvariantCulture)
        ];
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }

    private static Cv.Rect? GetValidRect(RoiSettings roi, int frameWidth, int frameHeight)
    {
        return GetValidRect(roi.X, roi.Y, roi.Width, roi.Height, frameWidth, frameHeight);
    }

    private static Cv.Rect? GetValidRect(InspectionRegionSettings region, int frameWidth, int frameHeight)
    {
        return GetValidRect(region.X, region.Y, region.Width, region.Height, frameWidth, frameHeight);
    }

    private static Cv.Rect? GetValidRect(int x, int y, int width, int height, int frameWidth, int frameHeight)
    {
        if (frameWidth <= 0 || frameHeight <= 0 || width <= 0 || height <= 0)
            return null;

        int left = Math.Clamp(x, 0, Math.Max(0, frameWidth - 1));
        int top = Math.Clamp(y, 0, Math.Max(0, frameHeight - 1));
        int right = Math.Clamp(x + width, left + 1, frameWidth);
        int bottom = Math.Clamp(y + height, top + 1, frameHeight);

        var rect = new Cv.Rect(left, top, right - left, bottom - top);
        return rect.Width > 1 && rect.Height > 1 ? rect : null;
    }

    private static RectExport ToExport(Cv.Rect rect)
    {
        return new RectExport
        {
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height
        };
    }

    private static void DrawRoi(Cv.Mat frame, Cv.Rect roi, string name)
    {
        Cv.Cv2.Rectangle(frame, roi, RoiColor, 2);
        DrawFilledLabel(frame, string.IsNullOrWhiteSpace(name) ? "ROI" : name, roi.X, roi.Y - 4, RoiColor);
    }

    private static void DrawInspectionRegions(Cv.Mat frame, List<InspectionRegionResult> regions)
    {
        foreach (var region in regions)
        {
            var rect = new Cv.Rect(region.Region.X, region.Region.Y, region.Region.Width, region.Region.Height);
            var color = region.Passed ? RegionOkColor : RegionNgColor;
            Cv.Cv2.Rectangle(frame, rect, color, 2);
            string label = $"{region.Name} {(region.Passed ? "OK" : "NG")} {region.FoundCount}/{region.MinCount}";
            DrawFilledLabel(frame, label, rect.X, rect.Y - 4, color);
        }
    }

    private static void DrawDetections(Cv.Mat frame, IReadOnlyList<Detection> detections)
    {
        foreach (var detection in detections)
        {
            Cv.Cv2.Rectangle(frame, detection.BoundingBox, BoxColor, 2);
            string label = $"{detection.ClassName} {detection.Confidence:F2}";
            DrawFilledLabel(frame, label, detection.BoundingBox.X, detection.BoundingBox.Y - 4, BoxColor);
        }
    }

    private static void DrawInspectionBadge(Cv.Mat frame, string status)
    {
        if (status is not ("OK" or "NG"))
            return;

        var color = status == "OK" ? RegionOkColor : RegionNgColor;
        Cv.Cv2.Rectangle(frame, new Cv.Rect(12, 12, 88, 38), color, -1);
        Cv.Cv2.PutText(frame, status, new Cv.Point(28, 39), Cv.HersheyFonts.HersheySimplex, 0.85, TextColor, 2);
    }

    private static void DrawFilledLabel(Cv.Mat frame, string label, int x, int y, Cv.Scalar background)
    {
        if (frame.Width <= 0 || frame.Height <= 0 || string.IsNullOrWhiteSpace(label))
            return;

        var textSize = Cv.Cv2.GetTextSize(label, Cv.HersheyFonts.HersheySimplex, 0.5, 1, out _);
        int labelX = Math.Clamp(x, 0, Math.Max(0, frame.Width - textSize.Width - 6));
        int baselineY = y;
        if (baselineY < textSize.Height + 4)
            baselineY = Math.Min(frame.Height - 2, y + textSize.Height + 8);

        baselineY = Math.Clamp(baselineY, textSize.Height + 4, Math.Max(textSize.Height + 4, frame.Height - 2));
        int backgroundTop = Math.Clamp(baselineY - textSize.Height - 4, 0, Math.Max(0, frame.Height - 1));
        int backgroundBottom = Math.Clamp(baselineY + 2, backgroundTop + 1, frame.Height);
        int backgroundRight = Math.Clamp(labelX + textSize.Width + 6, labelX + 1, frame.Width);

        Cv.Cv2.Rectangle(frame,
            new Cv.Point(labelX, backgroundTop),
            new Cv.Point(backgroundRight, backgroundBottom),
            background,
            -1);

        Cv.Cv2.PutText(frame,
            label,
            new Cv.Point(labelX + 3, baselineY),
            Cv.HersheyFonts.HersheySimplex,
            0.5,
            TextColor,
            1);
    }

    private unsafe void UpdateImage(Cv.Mat frame)
    {
        int width = frame.Width;
        int height = frame.Height;

        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Rgb24, null);
        bitmap.Lock();
        try
        {
            byte* src = (byte*)frame.Data;
            byte* dst = (byte*)bitmap.BackBuffer;
            int srcStride = (int)frame.Step();
            int dstStride = bitmap.BackBufferStride;
            int rowBytes = width * 3;

            int copyBytes = Math.Min(rowBytes, Math.Min(srcStride, dstStride));
            for (int row = 0; row < height; row++)
            {
                Buffer.MemoryCopy(src + row * srcStride, dst + row * dstStride, copyBytes, copyBytes);
            }
            bitmap.AddDirtyRect(new Int32Rect(0, 0, width, height));
        }
        finally
        {
            bitmap.Unlock();
        }

        VideoImage.Source = bitmap;
    }

    private async void OnClosed(object? sender, EventArgs e)
    {
        await StopStreamAsync();
        DisposeResources();
    }

    public void Dispose()
    {
        _captureCts?.Cancel();
        _activeCapture?.Release();
        _captureCts?.Dispose();
        DisposeResources();
        GC.SuppressFinalize(this);
    }

    private void DisposeResources()
    {
        _detector?.Dispose();
        _detector = null;
        lock (_lastFrameLock)
        {
            _lastRenderedFrame?.Dispose();
            _lastRenderedFrame = null;
            _lastResult = null;
        }
    }

    private sealed record ProcessedFrame(Cv.Mat RenderedFrame, FrameDetectionResult Result);
}