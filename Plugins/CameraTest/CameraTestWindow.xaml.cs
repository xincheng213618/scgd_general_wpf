using CameraTest.Application;
using CameraTest.Models;
using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using ColorVision.UI;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace CameraTest;

public partial class CameraTestWindow : Window
{
    private TestProfile _profile = new();
    private readonly ArchiveSettings _archiveSettings = new();
    private readonly FocusHistory _focusHistory = new();
    private readonly StandaloneCameraSession _camera = new();
    private readonly DispatcherTimer _timer;
    private TestFrame? _frame;
    private FrameAnalysis? _result;
    private IDisposable? _overlays;
    private bool _busy, _live, _closing, _closed, _ready, _presenting;
    private long _generation;
    private int _nextRegion = 1;
    private Task? _analysisTask;
    private Task? _archiveTask;

    public CameraTestWindow()
    {
        InitializeComponent();
        ImageView.AllowDrop = false;
        ImageView.Config.IsToolBarLeftVisible = false;
        ImageView.Config.IsToolBarRightVisible = false;
        ImageView.ImageSourceLoaded += ImageSourceChanged;
        ImageView.EditorContext.DrawEditorContext.DrawingVisualLists.CollectionChanged += DrawingsChanged;
        ImageView.EditorContext.DrawEditorContext.Zoombox.ContentMatrixChanged += OverlayZoomChanged;
        ImageView.EditorContext.DrawEditorContext.Zoombox.PreviewMouseDown += MeasurementEdge_MouseDown;
        ImageView.EditorContext.DrawEditorContext.SelectionVisual.SelectionChanged += DrawingSelectionChanged;
        ImageView.EditorContext.DrawEditorContext.Zoombox.AddHandler(ContextMenuService.ContextMenuOpeningEvent, new ContextMenuEventHandler(MeasurementEdge_ContextMenuOpening), true);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_profile.Analysis.LiveIntervalMilliseconds) };
        _timer.Tick += Live_Tick;
        _ready = true;
        InitializeCameraControls();
        Refresh();
    }

    private void ImageSourceChanged(object? sender, ImageViewImageSourceLoadedEventArgs e)
    {
        if (_presenting || !_ready || _closing) return;
        _generation++;
        _frame = null;
        FrameText.Text = "图像已更改";
        FrameText.ToolTip = null;
        ResetFocus();
        InvalidateResult();
        StatusText.Text = "图像已在编辑器中改变。请通过打开图像或取图分析重新加载测量源。";
        Refresh();
    }

    private void Refresh()
    {
        if (!_ready) return;
        RefreshCameraControls();
        _regionError = GetRegionError();
        bool idle = !_busy && !_closing;
        CameraSummary.Text = $"{_profile.Camera.Model} · {_profile.Camera.Mode}\n{(_camera.IsConnected ? "已连接" : "未连接")} · {_profile.Camera.BitDepth} bit\n曝光 {_profile.Camera.ExposureMilliseconds:G} ms · 增益 {_profile.Camera.Gain:G}";
        SignalSummary.Text = $"输入编码：{_profile.Sfr.InputEncoding}\n目标频率：{_profile.Analysis.TargetFrequency:G} cycles/pixel";
        ResponseColumn.Header = $"MTF@{_profile.Analysis.TargetFrequency:G}";
        ModeText.Text = _live ? "实时调试" : "单帧调试";
        ArchiveSummary.Text = string.IsNullOrWhiteSpace(_archiveSettings.DeviceSerial) ? "设备编号 / SN 未填写" : $"设备编号：{_archiveSettings.DeviceSerial}\n批次：{_archiveSettings.Batch}";
        RegionCountText.Text = _profile.Regions.Count.ToString();
        DisplaySettingsMenuItem.IsEnabled = idle && !_live;
        VideoSettingsMenuItem.IsEnabled = idle && !_live;
        ArchiveSettingsMenuItem.IsEnabled = idle && !_live;
        ArchiveButton.IsEnabled = idle && !_live && _frame != null;
        JudgmentSettingsMenuItem.IsEnabled = idle && !_live;
        ClearFocusButton.IsEnabled = idle;
        ExportFocusButton.IsEnabled = idle && !_live && _focusHistory.Count > 0;
        LiveButton.IsEnabled = idle && !_live;
        StopButton.IsEnabled = _live && !_closing;
        CaptureButton.IsEnabled = idle && !_live;
        OpenButton.IsEnabled = idle && !_live;
        AnalyzeButton.IsEnabled = idle && !_live && _frame != null && _profile.Regions.Count > 0 && _regionError == null;
        AnalyzeButton.Content = _result == null ? "开始分析" : "重新分析";
        AnalyzeButton.ToolTip = _regionError;
        ImageView.EditorContext.DrawEditorContext.DrawCanvas.IsEnabled = !_closing && !_live && (!_busy || _selectingRegion);
        ExportMenuItem.IsEnabled = idle && !_live && _result != null && _result.FrameId == _frame?.Id;
        SaveImageMenuItem.IsEnabled = idle && !_live && _frame != null;
        ConnectButton.IsEnabled = idle && !_camera.IsConnected;
        DisconnectButton.IsEnabled = idle && _camera.IsConnected;
        CameraSettingsButton.IsEnabled = DiscoverButton.IsEnabled = CameraIds.IsEnabled = idle && !_camera.IsConnected;
        SignalSettingsMenuItem.IsEnabled = MetricSettingsMenuItem.IsEnabled = LoadProfileMenuItem.IsEnabled = idle && !_live;
        SaveProfileMenuItem.IsEnabled = idle && !_live;
        AddRegionButton.IsEnabled = idle && !_live && _frame != null;
        string? selectedRegion = (RegionList.SelectedItem as SearchRegion)?.Id;
        RegionList.ItemsSource = _profile.Regions.ToArray();
        RegionList.SelectedItem = _profile.Regions.FirstOrDefault(r => r.Id == selectedRegion);
        RemoveRegionMenuItem.IsEnabled = idle && !_live && RegionList.SelectedItem != null;
    }

    private async Task PerformAsync(Func<Task> operation)
    {
        if (_busy || _closing) return;
        _busy = true;
        Refresh();
        try { await operation(); }
        catch (Exception exception) { StatusText.Text = exception.Message; }
        finally { _busy = false; Refresh(); }
    }

    private async Task EnsureCameraAsync(bool live)
    {
        if (_camera.IsConnected && _camera.IsLive != live) await _camera.DisconnectAsync();
        if (!_camera.IsConnected) await _camera.ConnectAsync(_profile.Camera, live);
    }

    private async void Connect_Click(object sender, RoutedEventArgs e) => await PerformAsync(async () =>
    {
        await EnsureCameraAsync(false);
        StatusText.Text = "相机已连接，可以取图分析。";
    });

    private async void Disconnect_Click(object sender, RoutedEventArgs e) => await PerformAsync(async () =>
    {
        StopLive();
        await _camera.DisconnectAsync();
        StatusText.Text = "相机已断开，当前图像保留。";
    });

    private async void Discover_Click(object sender, RoutedEventArgs e) => await PerformAsync(async () =>
    {
        var result = await StandaloneCameraSession.DiscoverAsync(_profile.Camera.Model);
        CameraIds.ItemsSource = result.Cameras.Select(c => c.CameraId).ToArray();
        if (result.Cameras.Count > 0) CameraIds.SelectedIndex = 0;
        StatusText.Text = result.Cameras.Count > 0 ? $"发现 {result.Cameras.Count} 台相机。" : string.Join("；", result.Models.Select(m => m.ErrorMessage).Where(m => !string.IsNullOrWhiteSpace(m))) is { Length: > 0 } error ? error : "未发现相机，请核对驱动和连接。";
    });

    private void CameraIds_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_ready && CameraIds.SelectedItem is string id) _profile.Camera.CameraId = id;
    }

    private async void Capture_Click(object sender, RoutedEventArgs e) => await PerformAsync(async () =>
    {
        await EnsureCameraAsync(false);
        var data = await _camera.CaptureAsync();
        await AcceptAndAnalyzeAsync(new TestFrame(data, $"Camera:{_profile.Camera.Model}/{_profile.Camera.CameraId}", FrameSourceKind.Capture, _profile.Camera));
    });

    private async void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "图像|*.bmp;*.png;*.jpg;*.jpeg;*.tif;*.tiff|所有文件|*.*" };
        if (dialog.ShowDialog(this) != true) return;
        await OpenImageAsync(dialog.FileName);
    }

    public Task OpenImageAsync(string path) => PerformAsync(async () => await AcceptAndAnalyzeAsync(await Task.Run(() => TestFrame.Open(path))));

    private async Task AcceptAndAnalyzeAsync(TestFrame frame)
    {
        if (_closing) return;
        ResetFocus();
        ShowFrame(frame);
        SynchronizeRegionDrawings();
        InvalidateResult();
        _regionError = GetRegionError();
        if (_profile.Regions.Count == 0)
        {
            _profile.ImageWidth = frame.Data.Width;
            _profile.ImageHeight = frame.Data.Height;
            StatusText.Text = "图像已加载。请框选一个完整 BMW 靶标；可重复添加多个搜索区域。";
        }
        else if (_regionError != null) StatusText.Text = _regionError;
        else await AnalyzeFrameAsync(frame, _generation);
        RenderOverlays();
    }

    private void ShowFrame(TestFrame frame)
    {
        CloseEdgeMenu();
        bool sameSize = _frame?.Data.Width == frame.Data.Width && _frame.Data.Height == frame.Data.Height;
        var matrix = ImageView.EditorContext.DrawEditorContext.Zoombox.ContentMatrix;
        _overlays?.Dispose();
        _overlays = null;
        _presenting = true;
        try { ImageView.OpenImage(new WriteableBitmap(frame.CreateBitmap())); }
        finally { _presenting = false; }
        _frame = frame;
        if (sameSize) ImageView.EditorContext.DrawEditorContext.Zoombox.ContentMatrix = matrix;
        else Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (_closing || _frame?.Id != frame.Id) return;
            ImageView.UpdateLayout();
            ImageView.UpdateZoomAndScale();
        }));
        FrameText.Text = $"{frame.Data.Width} × {frame.Data.Height} · {frame.Data.BitDepth} bit · {frame.Data.Channels} 通道 · {(frame.SourceKind == FrameSourceKind.ImageFile ? Path.GetFileName(frame.Source) : "相机图像")}";
        FrameText.ToolTip = $"{frame.Source}\n{frame.Data.CapturedAt:yyyy-MM-dd HH:mm:ss.fff}";
    }

    private async void Analyze_Click(object sender, RoutedEventArgs e) => await AnalyzeCurrentFrameAsync();

    public Task AnalyzeCurrentFrameAsync() => PerformAsync(async () =>
    {
        SynchronizeRegionDrawings();
        if (_regionError != null) throw new InvalidOperationException(_regionError);
        if (_frame != null) await AnalyzeFrameAsync(_frame, _generation);
    });

    private async Task AnalyzeFrameAsync(TestFrame frame, long generation)
    {
        var selected = Metrics.SelectedItem as MetricRow;
        var selectedColor = ColorMetrics.SelectedItem as ColorShiftRow;
        if (!_live) InvalidateResult();
        StatusText.Text = "正在定位 BMW 并计算四边 SFR…";
        var snapshot = JsonSerializer.Deserialize<TestProfile>(JsonSerializer.Serialize(_profile, ProfileStore.JsonOptions), ProfileStore.JsonOptions)!;
        var job = Task.Run(() => FrameAnalysis.Run(frame, snapshot));
        _analysisTask = job;
        FrameAnalysis result;
        try { result = await job; }
        catch (Exception exception)
        {
            if (_live && generation == _generation && !_closing)
            {
                _focusHistory.AddMissing(frame.Id, frame.Data.CapturedAt, snapshot.Analysis.TargetFrequency, exception.Message);
                UpdateFocusPlot();
            }
            throw;
        }
        if (_closing || generation != _generation || (!_live && _frame?.Id != frame.Id)) return;
        if (_live) ShowFrame(frame);
        _result = result;
        RefreshMetricRows(selected);
        var colors = ColorShiftPresentation.Rows(result);
        ColorSummary.Text = ColorShiftPresentation.Summary(colors);
        ColorMetrics.ItemsSource = colors;
        ColorMetrics.SelectedItem = colors.FirstOrDefault(row => row.Target == selectedColor?.Target && row.Edge == selectedColor.Edge && row.Pair == selectedColor.Pair) ?? colors.FirstOrDefault();
        JudgmentMetrics.ItemsSource = result.Judgment.Items;
        VerdictText.Text = result.Judgment.Status;
        if (_live)
        {
            _focusHistory.Add(result);
            var key = FocusSelection.SelectedItem as FocusKey;
            string[] names = frame.Data.Channels == 1 ? ["L"] : ["L", "R", "G", "B"];
            var keys = result.Targets.SelectMany(t => t.Edges.SelectMany(e => names.Select(c => new FocusKey(t.Id, e.Id.ToString(), c)))).ToArray();
            FocusSelection.ItemsSource = keys;
            FocusSelection.SelectedItem = keys.FirstOrDefault(k => k == key) ?? keys.FirstOrDefault();
            UpdateFocusPlot();
        }
        int located = result.Targets.Count(t => t.Located);
        var channels = result.Targets.SelectMany(t => t.Edges).Where(edge => edge.Analysis != null).SelectMany(edge => edge.Analysis!.Channels).ToArray();
        StatusText.Text = $"定位 {located}/{result.Targets.Count} 个靶标 · 有效通道结果 {channels.Count(channel => channel.Valid)}/{channels.Length} · {result.ElapsedMilliseconds:F0} ms · {result.Judgment.Status}";
        RenderOverlays();
    }

    private async void Live_Click(object sender, RoutedEventArgs e) => await PerformAsync(async () =>
    {
        await EnsureCameraAsync(true);
        ResetFocus();
        _generation++;
        _live = true;
        _timer.Interval = TimeSpan.FromMilliseconds(_profile.Analysis.LiveIntervalMilliseconds);
        _timer.Start();
        StatusText.Text = _profile.Regions.Count == 0 ? "实时预览中。停止并冻结后框选靶标，再开始实时分析。" : "实时分析已启动，等待相机帧。";
    });

    private async void Live_Tick(object? sender, EventArgs e)
    {
        if (!_live || _busy || _closing) return;
        await PerformAsync(async () =>
        {
            var pixels = _camera.TakeLatestFrame();
            if (pixels == null) return;
            var frame = new TestFrame(pixels, $"Live:{_profile.Camera.Model}/{_profile.Camera.CameraId}", FrameSourceKind.Live, _profile.Camera);
            if (_profile.Video.Mode != VideoAnalysisMode.BmwSfr || _profile.Regions.Count == 0)
            {
                long generation = _generation;
                double? sharpness = null;
                if (_profile.Video.Mode == VideoAnalysisMode.Sharpness)
                {
                    var roi = _profile.Video.ResolveRoi(frame.Data.Width, frame.Data.Height);
                    var algorithm = _profile.Video.Algorithm;
                    sharpness = await Task.Run(() => frame.Read(image => OpenCVMediaHelper.M_CalArtculation(image, algorithm, roi)));
                }
                if (!_live || _closing || generation != _generation) return;
                ShowFrame(frame);
                InvalidateResult();
                if (_profile.Regions.Count == 0)
                {
                    _profile.ImageWidth = frame.Data.Width;
                    _profile.ImageHeight = frame.Data.Height;
                }
                StatusText.Text = sharpness.HasValue ? $"实时清晰度 · {_profile.Video.Algorithm}：{sharpness.Value:G7}" : "实时预览 · 停止后可框选测量点。";
            }
            else await AnalyzeFrameAsync(frame, _generation);
        });
    }

    private void StopLive()
    {
        _timer.Stop();
        _live = false;
        _generation++;
    }

    private async void Stop_Click(object sender, RoutedEventArgs e)
    {
        if (!_live || _closing) return;
        StopLive();
        Refresh();
        try { await _camera.DisconnectAsync(); StatusText.Text = "实时分析已停止，画面已冻结。"; }
        catch (Exception exception) { StatusText.Text = exception.Message; }
        Refresh();
    }

    private async void AddRegion_Click(object sender, RoutedEventArgs e) => await PerformAsync(async () =>
    {
        if (_frame == null) return;
        if (_profile.Regions.Count >= 64) throw new InvalidOperationException("最多支持 64 个搜索区域。");
        if (_profile.Regions.Count > 0 && (_profile.ImageWidth != _frame.Data.Width || _profile.ImageHeight != _frame.Data.Height))
            throw new InvalidOperationException("请先移除尺寸不匹配的旧搜索区域。");
        Guid sourceId = _frame.Id;
        StatusText.Text = "在图中拖出矩形包住一个完整 BMW 靶标；Esc 取消。";
        SelectResult? selected;
        _selectingRegion = true;
        Refresh();
        try { selected = await ImageView.BeginSelectAsync(SelectShapeType.Rectangle); }
        finally { _selectingRegion = false; Refresh(); }
        if (selected?.SourceScope is not { } scope || _frame?.Id != sourceId) return;
        int x = Math.Clamp((int)Math.Floor(selected.Rect.X * scope.DpiX / 96), 0, _frame.Data.Width);
        int y = Math.Clamp((int)Math.Floor(selected.Rect.Y * scope.DpiY / 96), 0, _frame.Data.Height);
        int right = Math.Clamp((int)Math.Ceiling(selected.Rect.Right * scope.DpiX / 96), 0, _frame.Data.Width);
        int bottom = Math.Clamp((int)Math.Ceiling(selected.Rect.Bottom * scope.DpiY / 96), 0, _frame.Data.Height);
        if (right - x < 40 || bottom - y < 40) throw new InvalidOperationException("搜索区域至少需要 40×40 像素。");
        string id = NextRegionId();
        var visual = new DVRectangleText(new() { Id = NextDrawingId(), Text = id, Rect = new(x * 96.0 / scope.DpiX, y * 96.0 / scope.DpiY, (right - x) * 96.0 / scope.DpiX, (bottom - y) * 96.0 / scope.DpiY) });
        ImageView.EditorContext.DrawEditorContext.DrawCanvas.AddVisualCommand(visual);
        visual.Render();
    });

    private void RemoveRegion_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _live || _closing) return;
        var region = (sender as FrameworkElement)?.Tag as SearchRegion ?? RegionList.SelectedItem as SearchRegion;
        if (region == null) { StatusText.Text = "请先选择测量点。"; return; }
        var draw = ImageView.EditorContext.DrawEditorContext;
        var visual = draw.DrawingVisualLists.FirstOrDefault(v => _regionIdentities.TryGetValue(v, out var identity) && identity.Id == region.Id);
        if (visual is System.Windows.Media.Visual shape) draw.DrawCanvas.RemoveVisualCommand(shape);
    }

    private void InvalidateResult()
    {
        CloseEdgeMenu();
        _result = null;
        Metrics.ItemsSource = null;
        ColorMetrics.ItemsSource = null;
        ColorSummary.Text = ColorShiftPresentation.Summary([]);
        JudgmentMetrics.ItemsSource = null;
        VerdictText.Text = "未分析";
        CurvePlot.Clear();
        _overlays?.Dispose();
        _overlays = null;
    }

    private HashSet<string> SelectedPlotChannels()
    {
        var channels = new HashSet<string>();
        if (ShowY.IsChecked == true) channels.Add("L");
        if (ShowR.IsChecked == true) channels.Add("R");
        if (ShowG.IsChecked == true) channels.Add("G");
        if (ShowB.IsChecked == true) channels.Add("B");
        return channels;
    }

    private void RefreshMetricRows(MetricRow? selected)
    {
        var channels = SelectedPlotChannels();
        // Channel-less rows report failed edges and remain visible while any channel is enabled.
        var rows = (_result?.Rows() ?? []).Where(row => channels.Count > 0 &&
            (row.Channel == "—" || channels.Contains(row.Channel == "Y (L)" ? "L" : row.Channel))).ToArray();
        Metrics.ItemsSource = rows;
        Metrics.SelectedItem = rows.FirstOrDefault(row => row.Target == selected?.Target && row.Edge == selected.Edge && row.Channel == selected.Channel)
            ?? rows.FirstOrDefault(row => row.Target == selected?.Target && row.Edge == selected.Edge)
            ?? rows.FirstOrDefault(row => row.Target == selected?.Target)
            ?? rows.FirstOrDefault();
    }

    private void UpdatePlot()
    {
        if (!_ready) return;
        var selected = Metrics.SelectedItem as MetricRow;
        CurveSelectionText.Text = selected == null ? "请选择结果行"
            : $"{selected.Target} · {(Enum.TryParse<BmwEdgeId>(selected.Edge, out var edge) ? EdgeName(edge) : selected.Edge)}边";
        if (selected is not { Analysis: { } analysis })
        {
            CurvePlot.Clear();
            EmptyPlot.Visibility = Visibility.Visible;
            return;
        }
        EmptyPlot.Visibility = Visibility.Collapsed;
        CurvePlot.ShowResult(analysis, PlotMode.SelectedIndex, SelectedPlotChannels(), false);
    }
    private void Metrics_SelectionChanged(object sender, SelectionChangedEventArgs e) { UpdatePlot(); if (_ready) RenderOverlays(); }
    private void PlotMode_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdatePlot();
    private void PlotSettings_Click(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        RefreshMetricRows(Metrics.SelectedItem as MetricRow);
        UpdatePlot();
    }

    private void ColorMetrics_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        var plot = ColorPlot.Plot;
        plot.Clear();
        if (ColorMetrics.SelectedItem is ColorShiftRow row)
            foreach (var channel in row.Analysis.Channels.Where(c => c.Valid))
            {
                var curve = plot.Add.Scatter(channel.CommonNormalPositions, channel.Esf);
                curve.LegendText = channel.Channel;
                curve.Color = ScottPlot.Color.FromHex(channel.Channel switch { "R" => "#BD3535", "G" => "#20833C", _ => "#3267CD" });
                curve.MarkerSize = 0;
                curve.LineWidth = 2;
            }
        plot.Add.HorizontalLine(0.5, 1, ScottPlot.Colors.Gray, ScottPlot.LinePattern.Dashed);
        plot.Axes.SetLimits(-12, 12, -0.1, 1.1);
        plot.XLabel("公共法线位置 (pixel)");
        plot.YLabel("归一化边缘信号");
        plot.Axes.Bottom.Label.FontName = plot.Axes.Left.Label.FontName = ScottPlot.Fonts.Detect("公共法线位置");
        plot.ShowLegend();
        ColorPlot.Refresh();
    }

    private void ResetFocus()
    {
        _focusHistory.Clear();
        FocusSelection.ItemsSource = null;
        UpdateFocusPlot();
    }

    private void UpdateFocusPlot()
    {
        if (!_ready) return;
        var plot = FocusPlot.Plot;
        plot.Clear();
        if (FocusSelection.SelectedItem is not FocusKey key)
        {
            FocusReadout.Text = "开始实时分析后记录调焦趋势。";
            plot.Axes.SetLimits(0, 1, 0, 1);
            FocusPlot.Refresh();
            return;
        }
        var metric = FocusMetricPicker.SelectedIndex == 1 ? FocusMetric.Mtf50 : FocusMetric.FrequencyResponse;
        var samples = _focusHistory.Series(key, metric);
        var segment = new List<FocusSample>();
        void Flush()
        {
            if (segment.Count == 0) return;
            var curve = plot.Add.Scatter(segment.Select(s => s.Seconds).ToArray(), segment.Select(s => s.Value!.Value).ToArray());
            curve.Color = ScottPlot.Color.FromHex("#3478D4"); curve.MarkerSize = 4; curve.LineWidth = 2;
            segment.Clear();
        }
        foreach (var sample in samples) { if (sample.Value.HasValue) segment.Add(sample); else Flush(); }
        Flush();
        double[] valid = samples.Where(s => s.Value.HasValue).Select(s => s.Value!.Value).ToArray();
        double end = samples.Count == 0 ? 1 : Math.Max(1, samples[^1].Seconds);
        double high = metric == FocusMetric.Mtf50 ? 0.5 : Math.Max(1, valid.DefaultIfEmpty(1).Max() * 1.05);
        plot.Axes.SetLimits(0, end, 0, high);
        plot.XLabel("本轮时间 (s)");
        plot.YLabel(metric == FocusMetric.Mtf50 ? "MTF50 (cycles/pixel)" : $"MTF@{_profile.Analysis.TargetFrequency:G}");
        plot.Axes.Bottom.Label.FontName = plot.Axes.Left.Label.FontName = ScottPlot.Fonts.Detect("本轮时间");
        string Format(double? value) => value.HasValue ? value.Value.ToString(metric == FocusMetric.Mtf50 ? "F4" : "P1") : "缺测";
        FocusReadout.Text = $"当前：{Format(samples.LastOrDefault()?.Value)}    窗口峰值：{Format(valid.Length == 0 ? null : valid.Max())}\n有效 {valid.Length}/{samples.Count} 帧 · 调焦参考";
        FocusPlot.Refresh();
    }
    private void FocusSelection_Changed(object sender, SelectionChangedEventArgs e) => UpdateFocusPlot();
    private void ClearFocus_Click(object sender, RoutedEventArgs e) { ResetFocus(); Refresh(); }
    private void ExportFocus_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "调焦记录 CSV|*.csv", FileName = $"Focus-{DateTime.Now:yyyyMMdd-HHmmss}.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try { _focusHistory.Export(dialog.FileName); StatusText.Text = "调焦记录已导出。"; }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private void EditSettings(object settings, string title)
    {
        var window = new PropertyEditorWindow(settings, PropertyEditorEditMode.Transactional) { Owner = this, Title = title, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        window.Submitted += (_, _) => { InvalidateResult(); ResetFocus(); };
        window.ShowDialog();
        Refresh();
        RenderOverlays();
    }
    private void CameraSettings_Click(object sender, RoutedEventArgs e) => CameraModels.Focus();
    private void VideoSettings_Click(object sender, RoutedEventArgs e) => EditSettings(_profile.Video, "视频分析设置");
    private void SignalSettings_Click(object sender, RoutedEventArgs e) => EditSettings(_profile.Sfr, "输入信号与质量检查");
    private void MetricSettings_Click(object sender, RoutedEventArgs e) => EditSettings(_profile.Analysis, "指标与实时刷新");
    private void JudgmentSettings_Click(object sender, RoutedEventArgs e) => EditSettings(_profile.Judgment, "可选判定标准（留空不检查）");

    private bool EditArchiveSettings()
    {
        bool submitted = false;
        var window = new PropertyEditorWindow(_archiveSettings, PropertyEditorEditMode.Transactional)
        {
            Owner = this, Title = "生产调试存档信息", WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        window.Submitted += (_, _) => submitted = true;
        window.ShowDialog();
        Refresh();
        return submitted;
    }
    private void ArchiveSettings_Click(object sender, RoutedEventArgs e) => EditArchiveSettings();

    private async void Archive_Click(object sender, RoutedEventArgs e)
    {
        if (_frame == null || _busy || _live) return;
        if (string.IsNullOrWhiteSpace(_archiveSettings.DeviceSerial) && !EditArchiveSettings()) return;
        await PerformAsync(async () =>
        {
            var frame = _frame;
            var result = _result;
            var profile = JsonSerializer.Deserialize<TestProfile>(JsonSerializer.Serialize(_profile, ProfileStore.JsonOptions), ProfileStore.JsonOptions)!;
            var settings = _archiveSettings with { };
            var job = Task.Run(() => ProductionArchive.Save(frame, result, profile, settings, _focusHistory));
            _archiveTask = job;
            string folder = await job;
            StatusText.Text = $"设备 {settings.DeviceSerial} 已存档：{folder}";
            StatusText.ToolTip = folder;
        });
    }

    private void ArchiveFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string path = Path.GetFullPath(_archiveSettings.RootDirectory);
            Directory.CreateDirectory(path);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private void LoadProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "检测配置|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { LoadProfile(dialog.FileName); }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    public void LoadProfile(string path)
    {
        if (_busy || _closing || _camera.IsConnected) throw new InvalidOperationException("请先完成当前操作并断开相机，再加载配置。");
        _profile = ProfileStore.Load(path);
        RestoreRegionDrawings();
        ResetFocus();
        InvalidateResult();
        RenderOverlays();
        StatusText.Text = "检测配置已加载；分析前将检查图像尺寸。";
        Refresh();
    }

    private void SaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "检测配置|*.json", FileName = "CameraTest.profile.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { ProfileStore.Save(dialog.FileName, _profile); StatusText.Text = "配置已保存。"; }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_result == null || _result.FrameId != _frame?.Id) return;
        var dialog = new SaveFileDialog { Filter = "完整结果 JSON|*.json|指标与 MTF 曲线 CSV|*.csv", FileName = $"CameraTest-{DateTime.Now:yyyyMMdd-HHmmss}" };
        if (dialog.ShowDialog(this) != true) return;
        try { _result.Export(dialog.FileName); StatusText.Text = "分析结果已导出。"; }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private void SaveImage_Click(object sender, RoutedEventArgs e)
    {
        if (_frame == null) return;
        var dialog = new SaveFileDialog { Filter = "无损 PNG|*.png", FileName = $"CameraTest-{DateTime.Now:yyyyMMdd-HHmmss}.png" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            using var stream = File.Create(dialog.FileName);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(_frame.CreateBitmap()));
            encoder.Save(stream);
            StatusText.Text = "当前原始像素已保存为 PNG（不包含叠图或显示滤镜）。";
        }
        catch (Exception exception) { StatusText.Text = exception.Message; }
    }

    private async void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_closed) return;
        e.Cancel = true;
        if (_closing) return;
        _closing = true;
        _acquisitionTimer.Stop();
        _acquisitionTimer.Tick -= ApplyAcquisition_Tick;
        StopLive();
        Refresh();
        try
        {
            if (_archiveTask != null) try { await _archiveTask; } catch (Exception exception) { System.Diagnostics.Trace.TraceError(exception.ToString()); }
            await _camera.DisposeAsync();
            if (_analysisTask != null) try { await _analysisTask; } catch (Exception) { }
            _timer.Tick -= Live_Tick;
            ImageView.ImageSourceLoaded -= ImageSourceChanged;
            ImageView.EditorContext.DrawEditorContext.DrawingVisualLists.CollectionChanged -= DrawingsChanged;
            ImageView.EditorContext.DrawEditorContext.Zoombox.ContentMatrixChanged -= OverlayZoomChanged;
            ImageView.EditorContext.DrawEditorContext.Zoombox.PreviewMouseDown -= MeasurementEdge_MouseDown;
            ImageView.EditorContext.DrawEditorContext.SelectionVisual.SelectionChanged -= DrawingSelectionChanged;
            ImageView.EditorContext.DrawEditorContext.Zoombox.RemoveHandler(ContextMenuService.ContextMenuOpeningEvent, new ContextMenuEventHandler(MeasurementEdge_ContextMenuOpening));
            CloseEdgeMenu();
            DetachRegionProperties();
            _overlays?.Dispose();
            ImageView.Dispose();
        }
        catch (Exception exception) { System.Diagnostics.Trace.TraceError(exception.ToString()); }
        // Disposal may complete synchronously; finish the current Closing event before closing again.
        finally { _closed = true; _ = Dispatcher.BeginInvoke(new Action(Close)); }
    }
}
