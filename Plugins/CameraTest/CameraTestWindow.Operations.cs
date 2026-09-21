using CameraTest.Application;
using CameraTest.Models;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CameraTest;

public partial class CameraTestWindow
{
    private readonly CameraOperationSettingsStore _settingsStore;
    private CameraOperationSettings _operationSettings = new();
    private readonly DispatcherTimer _settingsTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private bool _settingsDirty;
    private readonly VideoRunMetrics _videoMetrics = new();
    private readonly Stopwatch _videoClock = new();
    private Task? _operationTask;
    private Task? _stopTask;
    private bool _stopping;

    private void LoadOperationSettings()
    {
        try
        {
            _operationSettings = _settingsStore.Load();
            _profile.Camera = _operationSettings.Camera;
            _profile.Video = _operationSettings.Video;
        }
        catch (Exception exception) { StatusText.Text = $"本地设置读取失败，使用默认参数：{exception.Message}"; }
        _settingsTimer.Tick += SaveSettings_Tick;
        if (!string.IsNullOrWhiteSpace(_profile.Camera.CameraId))
        {
            CameraIds.ItemsSource = new[] { _profile.Camera.CameraId };
            CameraIds.SelectedIndex = 0;
        }
    }

    private void ScheduleSettingsSave()
    {
        _settingsDirty = true;
        _settingsTimer.Stop();
        if (!_closing) _settingsTimer.Start();
    }

    private void SaveSettings_Tick(object? sender, EventArgs e) => SaveOperationSettings();

    private void SaveOperationSettings()
    {
        _settingsTimer.Stop();
        if (!_settingsDirty) return;
        try
        {
            _operationSettings.Camera = _profile.Camera.Copy();
            _operationSettings.Video = _profile.Video;
            _settingsStore.Save(_operationSettings);
            _settingsDirty = false;
            ExposureInput.ToolTip = GainInput.ToolTip = $"参数已保存：{_settingsStore.PathName}";
        }
        catch (Exception exception)
        {
            ExposureInput.ToolTip = GainInput.ToolTip = $"参数未保存：{exception.Message}";
            StatusText.Text = $"本地参数保存失败：{exception.Message}";
        }
    }

    private void RefreshOperationControls()
    {
        SaveCaptureCheck.IsChecked = _operationSettings.SaveCapturedImages;
        SaveCaptureCheck.IsEnabled = CaptureDirectoryButton.IsEnabled = !_busy && !_live && !_closing && !_stopping;
        CaptureDirectoryButton.ToolTip = _operationSettings.CaptureDirectory;
        VideoModeSelector.SelectedIndex = (int)_profile.Video.Mode;
        VideoModeSelector.IsEnabled = !_busy && !_live && !_closing && !_stopping;
        VideoReadoutBar.Visibility = _live || _frame?.SourceKind == FrameSourceKind.Live ? Visibility.Visible : Visibility.Collapsed;
        CaptureTimingText.Visibility = VideoReadoutBar.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        RefreshChartTypeSelector();
    }

    private void SaveCapture_Click(object sender, RoutedEventArgs e)
    {
        _operationSettings.SaveCapturedImages = SaveCaptureCheck.IsChecked == true;
        ScheduleSettingsSave();
    }

    private void CaptureDirectory_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "取图保存目录", InitialDirectory = _operationSettings.CaptureDirectory };
        if (dialog.ShowDialog(this) != true) return;
        _operationSettings.CaptureDirectory = dialog.FolderName;
        ScheduleSettingsSave();
        RefreshOperationControls();
    }

    private void OpenCaptureDirectory_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(_operationSettings.CaptureDirectory);
            Process.Start(new ProcessStartInfo(_operationSettings.CaptureDirectory) { UseShellExecute = true });
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private void VideoMode_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || VideoModeSelector.SelectedIndex < 0 || _live || _busy || _closing) return;
        var mode = (VideoAnalysisMode)VideoModeSelector.SelectedIndex;
        if (_profile.Video.Mode == mode) return;
        _profile.Video.Mode = mode;
        ScheduleSettingsSave();
        RefreshOperationControls();
    }

    private void UpdateFrameSaveState(TestFrame frame)
    {
        CaptureStampText.Text = frame.SourceKind == FrameSourceKind.ImageFile ? "" : $"取图 {frame.Data.CapturedAt.LocalDateTime:HH:mm:ss.fff}";
        CaptureStampText.ToolTip = $"{frame.Source}\n{frame.Data.CapturedAt.LocalDateTime:yyyy-MM-dd HH:mm:ss.fff}";
        FrameSaveText.Text = frame.SourceKind == FrameSourceKind.ImageFile ? "原文件已在磁盘" : "当前帧未保存";
        FrameSaveText.ToolTip = frame.SourceKind == FrameSourceKind.ImageFile ? frame.Source : "取图可勾选自动保存；视频停止后通过“文件 → 保存原图”保存当前帧。";
        CaptureTimingText.Text = "";
        CaptureTimingText.ToolTip = null;
    }

    private void MarkFrameSaved(string path)
    {
        FrameSaveText.Text = "已保存";
        FrameSaveText.ToolTip = path;
    }

    private async Task AcceptCaptureAsync(TestFrame frame, double captureMilliseconds, Stopwatch total)
    {
        SavedCapture? saved = null;
        string? saveError = null;
        double? analysisMilliseconds = null;
        // Save before analysis so a measurement failure cannot lose a requested original image.
        if (_operationSettings.SaveCapturedImages)
        {
            FrameSaveText.Text = "正在保存原图…";
            try { saved = await Task.Run(() => CaptureFileStore.Save(frame, _operationSettings.CaptureDirectory, captureMilliseconds)); }
            catch (Exception exception) { saveError = exception.Message; }
        }
        try
        {
            await AcceptAndAnalyzeAsync(frame);
            if (_result?.FrameId == frame.Id) analysisMilliseconds = _result.ElapsedMilliseconds;
        }
        finally
        {
            if (saved != null) MarkFrameSaved(saved.ImagePath);
            else if (saveError != null) { FrameSaveText.Text = "保存失败"; FrameSaveText.ToolTip = saveError; }
            string analysis = analysisMilliseconds.HasValue ? $" · 分析 {analysisMilliseconds:F0} ms" : "";
            string saving = saved != null ? $" · 保存 {saved.SaveMilliseconds:F0} ms" : "";
            CaptureTimingText.Text = $"取图 {captureMilliseconds:F0} ms · 总计 {total.Elapsed.TotalMilliseconds:F0} ms";
            CaptureTimingText.ToolTip = $"取图 {captureMilliseconds:F0} ms{analysis}{saving} · 总计 {total.Elapsed.TotalMilliseconds:F0} ms\n取图为 SDK 等待和数据读取耗时；总计包含连接、显示、分析和保存。";
            if (saveError != null) StatusText.Text = $"取图成功，但自动保存失败：{saveError}";
        }
    }

    private void RefreshVideoReadouts()
    {
        TimeSpan timeout = TimeSpan.FromMilliseconds(Math.Max(3000, _profile.Camera.ExposureMilliseconds * 2));
        bool stalled = _live && !_busy && !_processingFrame && _videoMetrics.IsStalled(_videoClock.Elapsed, timeout);
        VideoFpsText.Text = stalled ? "显示 0.0 FPS · 等待帧" : _videoMetrics.DisplayFramesPerSecond is { } fps ? $"显示 {fps:F1} FPS" : "显示 — FPS";
        VideoSharpnessText.Text = stalled ? "清晰度 —" : _videoMetrics.Sharpness is { } value ? $"清晰度 {value:G6}" : "清晰度 —";
        VideoAnalysisText.Text = _videoMetrics.AnalysisMilliseconds is { } elapsed ? $"计算 {elapsed:F0} ms" : "";
        VideoAnalysisText.ToolTip = stalled ? "等待相机帧，请检查连接与曝光时间。" : "当前显示帧的分析耗时";
        VideoSharpnessText.Visibility = _profile.Video.Mode == VideoAnalysisMode.Sharpness || _profile.Video.Mode == VideoAnalysisMode.BmwSfr && _profile.Video.IncludeSharpnessWithSfr ? Visibility.Visible : Visibility.Collapsed;
    }
}
