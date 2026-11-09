using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.ImageEditor.Draw;
using ColorVision.Themes;
using ScottPlot;
using ScottPlot.WPF;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.Devices.Camera.Views;

public partial class CameraFocusChart : UserControl
{
    private enum ChartMode { None, Motor, AutoFocus, Video }

    public static readonly DependencyProperty MotorVidProperty = DependencyProperty.Register(
        nameof(MotorVid), typeof(double?), typeof(CameraFocusChart),
        new PropertyMetadata(null, (sender, _) => ((CameraFocusChart)sender).RefreshMotorVid(openWhenEntering: true)));

    public double? MotorVid
    {
        get => (double?)GetValue(MotorVidProperty);
        set => SetValue(MotorVidProperty, value);
    }

    private const double VideoWindowSeconds = 30;
    private const double VideoSampleInterval = 0.2;
    private readonly SortedDictionary<int, double> focusSamples = new();
    private readonly Queue<Coordinates> videoSamples = new();
    private readonly Stopwatch videoClock = new();
    private readonly DispatcherTimer redrawTimer;
    private WpfPlot? plot;
    private ChartMode mode;
    private ChartMode axisMode;
    private LocalAutoFocusResult? bestFocus;
    private bool focusRunning;
    private bool videoFocusEnabled;
    private double videoFps;
    private long videoDisplayedFrames;
    private double? videoScore;
    private double lastVideoSample;

    public CameraFocusChart()
    {
        InitializeComponent();
        redrawTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher) { Interval = TimeSpan.FromMilliseconds(150) };
        redrawTimer.Tick += (_, _) => { redrawTimer.Stop(); RenderChart(); };
        Loaded += (_, _) =>
        {
            ThemeManager.Current.CurrentUIThemeChanged += ApplyTheme;
            ApplyTheme(ThemeManager.Current.CurrentUITheme);
            RenderChart();
        };
        Unloaded += (_, _) =>
        {
            redrawTimer.Stop();
            ThemeManager.Current.CurrentUIThemeChanged -= ApplyTheme;
        };
    }

    internal void BeginAutoFocus(string? evaluation = null)
    {
        Reset();
        mode = ChartMode.AutoFocus;
        focusRunning = true;
        string title = EngineLocalization.Get("CameraFocusChart_AutoFocus");
        ChartTitle.Text = string.IsNullOrEmpty(evaluation) ? title : $"{title} · {evaluation}";
        ChartReadout.Text = EngineLocalization.Get("CameraFocusChart_Waiting");
        RefreshMotorVid(openWhenEntering: false);
        SetOpen(true);
    }

    internal void AddAutoFocusSample(int position, double score)
    {
        if (mode != ChartMode.AutoFocus || !focusRunning || !double.IsFinite(score)) return;
        focusSamples[position] = score;
        ChartReadout.Text = FormatReadout("CameraFocusChart_Sample", position, score);
        ScheduleRedraw();
    }

    internal void CompleteAutoFocus(LocalAutoFocusResult result)
    {
        if (mode != ChartMode.AutoFocus || !focusRunning) return;
        focusRunning = false;
        bestFocus = result;
        ChartReadout.Text = result.VidPosition is double vid
            ? FormatReadout("CameraFocusChart_BestWithVid", result.Position, result.Score, vid)
            : FormatReadout("CameraFocusChart_Best", result.Position, result.Score);
        RefreshMotorVid(openWhenEntering: false);
        ScheduleRedraw();
    }

    internal void FailAutoFocus()
    {
        if (mode != ChartMode.AutoFocus || !focusRunning) return;
        focusRunning = false;
        ChartReadout.Text = EngineLocalization.Get("CameraFocusChart_Failed");
    }

    internal void ClearCompletedAutoFocus()
    {
        if (mode != ChartMode.AutoFocus || focusRunning) return;
        Reset();
        RefreshMotorVid(openWhenEntering: false);
    }

    internal void BeginVideo(bool calculateFocus)
    {
        Reset();
        mode = ChartMode.Video;
        videoClock.Start();
        ChartTitle.Text = EngineLocalization.Get("CameraFocusChart_Video");
        ChartReadout.TextWrapping = TextWrapping.NoWrap;
        ChartReadout.TextTrimming = TextTrimming.CharacterEllipsis;
        ResetVideoSamples(calculateFocus);
        SetOpen(true);
    }

    internal void UpdateVideoDisplayMetrics(double fps, long displayedFrames)
    {
        if (mode != ChartMode.Video || (videoFps == fps && videoDisplayedFrames == displayedFrames)) return;
        videoFps = fps;
        videoDisplayedFrames = displayedFrames;
        UpdateVideoReadout();
    }

    internal void UpdateVideoCrossGuideStatus(string? status)
    {
        if (mode == ChartMode.Video) SetDetailReadout(status);
    }

    private void RefreshMotorVid(bool openWhenEntering)
    {
        if (mode == ChartMode.Video) return;
        string? currentVid = MotorVid is double vid && double.IsFinite(vid)
            ? FormatReadout("CameraFocusChart_CurrentVid", vid) : null;
        if (mode == ChartMode.AutoFocus)
        {
            SetDetailReadout(bestFocus?.VidPosition is double bestVid && bestVid == MotorVid ? null : currentVid);
            return;
        }

        if (currentVid == null)
        {
            if (mode == ChartMode.Motor) Reset();
            return;
        }

        bool entering = mode != ChartMode.Motor;
        mode = ChartMode.Motor;
        PlotHost.Visibility = Visibility.Collapsed;
        ChartTitle.Text = EngineLocalization.Get("CameraFocusChart_Position");
        ChartReadout.Text = currentVid;
        if (entering) SetOpen(openWhenEntering);
    }

    private void SetDetailReadout(string? text)
    {
        ChartDetailReadout.Text = text ?? string.Empty;
        ChartDetailReadout.Visibility = string.IsNullOrWhiteSpace(text) ? Visibility.Collapsed : Visibility.Visible;
        UpdatePanelHeight();
    }

    internal void ResetVideoSamples(bool calculateFocus)
    {
        if (mode != ChartMode.Video) return;
        videoFocusEnabled = calculateFocus;
        PlotHost.Visibility = calculateFocus ? Visibility.Visible : Visibility.Collapsed;
        UpdatePanelHeight();
        videoSamples.Clear();
        lastVideoSample = double.NegativeInfinity;
        videoScore = null;
        UpdateVideoReadout();
        ScheduleRedraw();
    }

    internal void AddVideoSample(double score)
    {
        if (mode != ChartMode.Video || !videoFocusEnabled || !double.IsFinite(score)) return;
        double elapsed = videoClock.Elapsed.TotalSeconds;
        if (elapsed - lastVideoSample < VideoSampleInterval) return;
        lastVideoSample = elapsed;
        videoSamples.Enqueue(new Coordinates(elapsed, score));
        while (videoSamples.Peek().X < elapsed - VideoWindowSeconds) videoSamples.Dequeue();
        videoScore = score;
        UpdateVideoReadout();
        ScheduleRedraw();
    }

    private void UpdateVideoReadout()
    {
        string score = videoScore?.ToString("G6", CultureInfo.CurrentCulture) ?? "—";
        ChartReadout.Text = FormatReadout("CameraFocusChart_VideoReadout", videoFps, videoDisplayedFrames, score);
        ChartReadout.ToolTip = videoFocusEnabled ? ChartReadout.Text
            : ChartReadout.Text + Environment.NewLine + EngineLocalization.Get("CameraFocusChart_EnableCalculation");
    }

    internal void EndVideo()
    {
        if (mode != ChartMode.Video) return;
        Reset();
        RefreshMotorVid(openWhenEntering: false);
    }

    internal void Reset()
    {
        mode = ChartMode.None;
        focusRunning = false;
        bestFocus = null;
        focusSamples.Clear();
        videoSamples.Clear();
        videoClock.Reset();
        SetOpen(false);
        PlotHost.Visibility = Visibility.Visible;
        videoFps = 0;
        videoDisplayedFrames = 0;
        videoScore = null;
        ChartReadout.TextWrapping = TextWrapping.Wrap;
        ChartReadout.TextTrimming = TextTrimming.None;
        ChartReadout.ToolTip = null;
        SetDetailReadout(null);
        plot?.Plot.Clear();
    }

    private void ShowChart_Click(object sender, RoutedEventArgs e) => SetOpen(true);
    private void CloseChart_Click(object sender, RoutedEventArgs e) => SetOpen(false);

    private static string FormatReadout(string key, params object[] values) =>
        string.Format(CultureInfo.CurrentCulture, EngineLocalization.Get(key), values);

    private void SetOpen(bool open)
    {
        redrawTimer.Stop();
        Width = open ? mode switch { ChartMode.Video => 320, ChartMode.Motor => 168, _ => 376 } : double.NaN;
        ChartPanel.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        UpdatePanelHeight();
        ShowChartButton.Content = mode == ChartMode.Motor ? "VID" : EngineLocalization.Get("CameraFocusChart_Show");
        ShowChartButton.Visibility = !open && mode != ChartMode.None ? Visibility.Visible : Visibility.Collapsed;
        if (open) RenderChart();
    }

    private void UpdatePanelHeight()
    {
        Height = ChartPanel.Visibility == Visibility.Visible ? mode switch
        {
            ChartMode.AutoFocus => ChartDetailReadout.Visibility == Visibility.Visible ? 246 : 226,
            ChartMode.Video when videoFocusEnabled => ChartDetailReadout.Visibility == Visibility.Visible ? 240 : 204,
            _ => double.NaN
        } : double.NaN;
    }

    private void ScheduleRedraw()
    {
        if (ChartPanel.Visibility == Visibility.Visible && PlotHost.Visibility == Visibility.Visible && !redrawTimer.IsEnabled)
            redrawTimer.Start();
    }

    private void RenderChart()
    {
        if (ChartPanel.Visibility != Visibility.Visible || PlotHost.Visibility != Visibility.Visible) return;
        if (plot == null)
        {
            plot = new WpfPlot();
            PlotHost.Content = plot;
            SpectrumPlotStyle.ApplyTheme(plot.Plot, ThemeManager.Current.CurrentUITheme == Theme.Dark);
        }

        Plot chart = plot.Plot;
        if (axisMode != mode)
        {
            axisMode = mode;
            string xLabel = EngineLocalization.Get(mode == ChartMode.Video ? "CameraFocusChart_Seconds" : "CameraFocusChart_Position");
            string yLabel = EngineLocalization.Get("CameraFocusChart_Score");
            chart.XLabel(xLabel);
            chart.YLabel(yLabel);
            chart.Axes.Bottom.Label.FontName = Fonts.Detect(xLabel);
            chart.Axes.Left.Label.FontName = Fonts.Detect(yLabel);
            chart.Axes.Bottom.Label.FontSize = chart.Axes.Left.Label.FontSize = 12;
        }

        chart.Clear();
        Coordinates[] samples = mode == ChartMode.Video
            ? videoSamples.ToArray()
            : focusSamples.Select(sample => new Coordinates(sample.Key, sample.Value)).ToArray();
        if (samples.Length != 0)
        {
            var curve = chart.Add.Scatter(samples);
            curve.Color = ScottPlot.Color.FromHex("#3196CE");
            curve.MarkerSize = mode == ChartMode.AutoFocus || samples.Length == 1 ? 4 : 0;
        }
        if (bestFocus != null)
            chart.Add.Marker(bestFocus.Position, bestFocus.Score, MarkerShape.FilledCircle, 9, Colors.Orange);

        chart.Axes.AutoScale();
        if (mode == ChartMode.Video && samples.Length != 0)
        {
            double latest = samples[^1].X;
            chart.Axes.SetLimitsX(Math.Max(0, latest - VideoWindowSeconds), Math.Max(5, latest));
        }
        plot.Refresh();
    }

    private void ApplyTheme(Theme theme)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => ApplyTheme(theme)); return; }
        if (plot == null) return;
        SpectrumPlotStyle.ApplyTheme(plot.Plot, theme == Theme.Dark);
        plot.Refresh();
    }
}
