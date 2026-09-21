using CameraTest.Application;
using CameraTest.Models;
using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;
using ColorVision.Themes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CameraTest;

public partial class CameraTestWindow
{
    private const string AllTargets = "全部点位";
    private bool _syncingOverview;
    private double _cameraPaneWidth = 200;
    private readonly ThemeManager _themeManager = ThemeManager.Current;
    private bool _syncingDisplayMetric;

    private void OnWorkbenchThemeChanged(Theme theme)
    {
        if (_closing) return;
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => OnWorkbenchThemeChanged(theme)); return; }
        UpdatePlot();
    }

    private void ToggleCameraPane_Click(object sender, RoutedEventArgs e)
    {
        bool hide = CameraPane.Visibility == Visibility.Visible;
        if (hide) _cameraPaneWidth = CameraPaneColumn.ActualWidth;
        CameraPaneColumn.MinWidth = hide ? 0 : 180;
        CameraPaneColumn.Width = new GridLength(hide ? 0 : _cameraPaneWidth);
        CameraSplitterColumn.Width = new GridLength(hide ? 0 : 10);
        CameraPane.Visibility = CameraSplitter.Visibility = hide ? Visibility.Collapsed : Visibility.Visible;
        CameraPaneButton.Content = hide ? "相机面板" : "收起相机";
    }

    private void RefreshTargetFilter()
    {
        string[] targets = [AllTargets, .. (_result?.Targets.Select(t => t.Id) ?? [])];
        if (TargetFilter.Items.Cast<string>().SequenceEqual(targets)) return;
        string? selected = TargetFilter.SelectedItem as string;
        _syncingOverview = true;
        try
        {
            TargetFilter.ItemsSource = targets;
            TargetFilter.SelectedItem = targets.Contains(selected) ? selected : AllTargets;
        }
        finally { _syncingOverview = false; }
    }

    private void TargetFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _syncingOverview) return;
        RefreshMetricRows(Metrics.SelectedItem as MetricRow);
    }

    private void EnsureTargetVisible(string target)
    {
        if (TargetFilter.SelectedItem is string filter && filter != AllTargets && filter != target)
            TargetFilter.SelectedItem = AllTargets;
    }

    private void RefreshOverview()
    {
        _syncingOverview = true;
        try
        {
            Overview.ItemsSource = MeasurementOverview.Create(Metrics.Items.OfType<MetricRow>(), OverviewMetric.SelectedIndex, _profile.Display.Frequency);
            OverviewY.Visibility = ShowY.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            OverviewR.Visibility = ShowR.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            OverviewG.Visibility = ShowG.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            OverviewB.Visibility = ShowB.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }
        finally { _syncingOverview = false; }
        SynchronizeOverviewSelection();
    }

    private void SynchronizeOverviewSelection()
    {
        if (!_ready || _syncingOverview) return;
        var selected = Metrics.SelectedItem as MetricRow;
        _syncingOverview = true;
        try
        {
            Overview.SelectedItem = Overview.Items.OfType<MeasurementOverview>().FirstOrDefault(r => r.Target == selected?.Target && r.Edge == selected.Edge);
            if (Overview.SelectedItem != null && Overview.IsVisible) Overview.ScrollIntoView(Overview.SelectedItem);
        }
        finally { _syncingOverview = false; }
        RefreshWorkbenchState();
    }

    private void Overview_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _syncingOverview || Overview.SelectedItem is not MeasurementOverview row) return;
        SelectOverviewRow(row, (Metrics.SelectedItem as MetricRow)?.Channel);
    }

    private void Overview_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_ready || _closing) return;
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, new Action(() =>
        {
            if (!_closing && Overview.IsVisible && Overview.SelectedItem != null) Overview.ScrollIntoView(Overview.SelectedItem);
        }));
    }

    private void SelectOverviewRow(MeasurementOverview row, string? channel)
    {
        Metrics.SelectedItem = row.Rows.FirstOrDefault(r => r.Channel == channel) ?? row.Rows.FirstOrDefault();
    }

    private void Overview_MouseUp(object sender, MouseButtonEventArgs e)
    {
        // A channel cell is also a deliberate selection when its edge row was already selected.
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source != null && source is not DataGridCell)
            source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
        if (source is DataGridCell { DataContext: MeasurementOverview row } cell)
            SelectOverviewRow(row, cell.Column == OverviewY ? "Y (L)" : cell.Column == OverviewR ? "R" : cell.Column == OverviewG ? "G" : cell.Column == OverviewB ? "B" : (Metrics.SelectedItem as MetricRow)?.Channel);
    }

    private void OverviewMetric_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _syncingDisplayMetric) return;
        _profile.Display.Metric = (BmwSfrDisplayMetric)Math.Max(0, OverviewMetric.SelectedIndex);
        RefreshOverview();
        RenderOverlays();
    }

    private void SynchronizeDisplayMetric()
    {
        _syncingDisplayMetric = true;
        try { OverviewMetric.SelectedIndex = (int)_profile.Display.Metric; }
        finally { _syncingDisplayMetric = false; }
        RefreshOverview();
    }

    private void Direction_Click(object sender, RoutedEventArgs e)
    {
        if (Metrics.SelectedItem is not MetricRow current || sender is not Button { Tag: string direction }) return;
        var row = Metrics.Items.OfType<MetricRow>().FirstOrDefault(r => r.Target == current.Target && r.Edge == direction && r.Channel == current.Channel)
            ?? Metrics.Items.OfType<MetricRow>().FirstOrDefault(r => r.Target == current.Target && r.Edge == direction);
        if (row != null) Metrics.SelectedItem = row;
    }

    private void RefreshDirectionReadouts(MetricRow? selected)
    {
        var rows = Metrics.Items.OfType<MetricRow>().Where(r => r.Target == selected?.Target).ToArray();
        foreach (var (button, readout) in new[] { (LeftEdgeButton, LeftEdgeValue), (TopEdgeButton, TopEdgeValue), (RightEdgeButton, RightEdgeValue), (BottomEdgeButton, BottomEdgeValue) })
        {
            string edge = (string)button.Tag;
            var row = rows.FirstOrDefault(r => r.Edge == edge && r.Channel == selected?.Channel);
            readout.Text = MeasurementOverview.Format(row, OverviewMetric.SelectedIndex, _profile.Display.Frequency);
            button.IsEnabled = !_closing && rows.Any(r => r.Edge == edge);
            bool active = selected?.Edge == edge;
            button.SetResourceReference(Control.BackgroundProperty, active ? "PrimaryBrush" : "CV.Surface.Alternate");
            if (active) button.Foreground = Brushes.White;
            else button.SetResourceReference(Control.ForegroundProperty, "PrimaryTextBrush");
            button.ToolTip = MeasurementOverview.Describe(row, OverviewMetric.SelectedIndex, _profile.Display.Frequency);
        }
    }

    private void ShowDetails_Click(object sender, RoutedEventArgs e)
    {
        Metrics.Visibility = ShowDetails.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        Overview.Visibility = ShowDetails.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
        if (Metrics.IsVisible && Metrics.SelectedItem != null) Metrics.ScrollIntoView(Metrics.SelectedItem);
    }

    private void Lowest_Click(object sender, RoutedEventArgs e)
    {
        if (MeasurementOverview.Lowest(Metrics.Items.OfType<MetricRow>(), OverviewMetric.SelectedIndex, _profile.Display.Frequency) is not { } row) return;
        Metrics.SelectedItem = row;
        if (Metrics.IsVisible) Metrics.ScrollIntoView(row);
        StatusText.Text = $"本帧当前范围最低值：{row.Target} · {row.Edge} · {row.Channel}；仅用于调焦比较。";
    }

    private void FocusLabels_Click(object sender, RoutedEventArgs e) { if (_ready) RenderOverlays(); }

    private void MissingResult_Click(object sender, RoutedEventArgs e)
    {
        var missing = Metrics.Items.OfType<MetricRow>().Where(row => MeasurementOverview.Missing(row, OverviewMetric.SelectedIndex, _profile.Display.Frequency)).ToArray();
        if (missing.Length == 0) return;
        var current = Metrics.SelectedItem as MetricRow;
        int index = Array.FindIndex(missing, row => row.Target == current?.Target && row.Edge == current.Edge && row.Channel == current.Channel);
        Metrics.SelectedItem = missing[(index + 1) % missing.Length];
        if (Metrics.IsVisible) Metrics.ScrollIntoView(Metrics.SelectedItem);
    }

    private void FailedTarget_Click(object sender, RoutedEventArgs e)
    {
        var failed = _result?.Targets.Where(t => !t.Located).ToArray();
        if (failed == null || failed.Length == 0) return;
        string? selected = (Metrics.SelectedItem as MetricRow)?.Target;
        var next = failed[(Array.FindIndex(failed, t => t.Id == selected) + 1) % failed.Length];
        EnsureTargetVisible(next.Id);
        Metrics.SelectedItem = Metrics.Items.OfType<MetricRow>().FirstOrDefault(row => row.Target == next.Id);
    }

    private void RefreshWorkbenchState()
    {
        if (!_ready) return;
        StartGuide.Visibility = _frame == null ? Visibility.Visible : Visibility.Collapsed;
        var selected = Metrics.SelectedItem as MetricRow;
        int metric = OverviewMetric.SelectedIndex;
        string name = metric == 3 ? "MTF@0.5" : metric == 2 ? $"MTF@{_profile.Display.Frequency:G}" : metric == 1 ? "MTF10" : "MTF50";
        TargetFrequencyOption.Content = $"MTF@{_profile.Display.Frequency:G}";
        SelectedMetricValue.Text = MeasurementOverview.Format(selected, metric, _profile.Display.Frequency);
        string channel = selected?.Channel == "Y (L)" ? "Y" : selected?.Channel ?? "Y";
        string chart = GetChartTypeText(_result?.Targets.FirstOrDefault(t => t.Id == selected?.Target));
        SelectedMetricCaption.Text = $"{(chart.Length > 0 ? chart + " · " : "")}{channel} · {name}{(metric >= 2 ? " · %" : " · cy/px")}";
        RefreshDirectionReadouts(selected);
        ResultSummary.Text = _result == null ? "每条边一行，通道横向对比"
            : $"{_result.Targets.Count(t => t.Located)}/{_result.Targets.Count} 点已定位 · {Overview.Items.Count} 条边";
        LowestButton.IsEnabled = !_closing && MeasurementOverview.Lowest(Metrics.Items.OfType<MetricRow>(), metric, _profile.Display.Frequency) != null;
        bool noChannels = SelectedPlotChannels().Count == 0;
        int failedCount = _result?.Targets.Count(t => !t.Located) ?? 0;
        FailedTargetsButton.Visibility = failedCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        FailedTargetsButton.Content = $"处理未定位 ({failedCount})";
        FailedTargetsButton.IsEnabled = !noChannels && !_closing;
        int missingCount = Metrics.Items.OfType<MetricRow>().Count(row => MeasurementOverview.Missing(row, metric, _profile.Display.Frequency));
        MissingResultsButton.Visibility = missingCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        MissingResultsButton.Content = $"查看缺值 ({missingCount})";
        MissingResultsButton.IsEnabled = !noChannels && !_closing;
        ResultHint.Text = noChannels ? "勾选至少一个通道，查看曲线与结果。"
            : _result == null ? GetRegionError() ?? (_frame == null ? "打开图像或连接相机取图。" : _profile.Regions.Count == 0 ? ChartSelectionHint : "测量点已就绪，点击“开始分析”。")
            : selected == null ? "当前范围没有结果。"
            : MeasurementOverview.Missing(selected, metric, _profile.Display.Frequency) ? MeasurementOverview.Describe(selected, metric, _profile.Display.Frequency)
            : missingCount > 0 ? $"当前筛选范围有 {missingCount} 项缺值，点击“查看缺值”逐项检查，或点击对应通道单元格。"
            : MeasurementOverview.Explain(selected.Status) is { Length: > 0 } reason ? reason : "点击数值切换通道，点击图上刃边可定位结果。";
        bool unknownEncoding = selected?.Status.Contains("unknown_input_encoding", StringComparison.Ordinal) == true;
        bool clipping = selected?.Status.Contains("clipped_pixels", StringComparison.Ordinal) == true;
        CheckSignalButton.Visibility = unknownEncoding || clipping ? Visibility.Visible : Visibility.Collapsed;
        bool selectedMissing = selected != null && MeasurementOverview.Missing(selected, metric, _profile.Display.Frequency);
        if (unknownEncoding && !selectedMissing && missingCount == 0)
            ResultHint.Text = clipping ? "输入编码待确认 · 像素存在削顶风险" : "输入编码待确认 · 当前数值仅供调试";
        else if (clipping && !selectedMissing && missingCount == 0) ResultHint.Text = "像素存在削顶风险，请检查曝光。";
        ResultHint.ToolTip = selected == null ? ResultHint.Text : MeasurementOverview.Describe(selected, metric, _profile.Display.Frequency);
        var target = _result?.Targets.FirstOrDefault(t => t.Id == selected?.Target);
        bool failed = target is { Located: false } || selected is { Analysis: null } || selected?.ChannelAnalysis is { Valid: false };
        RecoveryActions.Visibility = failed ? Visibility.Visible : Visibility.Collapsed;
        ExpandRegionButton.Visibility = ChartTypeSupport.Selection(_profile.MeasurementRoi) == 0 && target is { Located: false, Reason: "target_not_found" } ? Visibility.Visible : Visibility.Collapsed;
        ExpandRegionButton.IsEnabled = failed && !_busy && !_live && !_closing && CanExpandSelectedRegion();
        EmptyPlotText.Text = noChannels ? "尚未选择通道" : failed ? "本条刃边没有可用曲线\n请按下方提示调整选框或测量参数" : _result == null ? "分析后，点击结果或图上的刃边查看曲线" : "所选结果没有可用曲线";
    }

    private ISelectVisual? SelectedSearchDrawing()
    {
        string? id = (Metrics.SelectedItem as MetricRow)?.Target;
        return ImageView.EditorContext.DrawEditorContext.DrawingVisualLists
            .FirstOrDefault(v => _regionIdentities.TryGetValue(v, out var identity) && identity.Id == id) as ISelectVisual;
    }

    private Rect ExpandedSearchBounds(ISelectVisual drawing)
    {
        if (_frame == null || drawing is not IDrawingVisual { BaseAttribute: RectangleProperties properties } || properties.Rotation != 0) return Rect.Empty;
        var before = drawing.GetRect();
        if (before.IsEmpty || before.Width <= 0 || before.Height <= 0) return Rect.Empty;
        // ShowFrame normalizes the analysis image to 96 DPI, so these drawing bounds are source pixels.
        var expanded = before;
        expanded.Inflate(before.Width * .25, before.Height * .25);
        expanded.Intersect(new Rect(0, 0, _frame.Data.Width, _frame.Data.Height));
        return expanded;
    }

    private bool CanExpandSelectedRegion() => SelectedSearchDrawing() is { } drawing && ExpandedSearchBounds(drawing) is { IsEmpty: false } after && after != drawing.GetRect();

    private async void ExpandRegion_Click(object sender, RoutedEventArgs e) => await ExpandSelectedRegionAsync();

    private async Task ExpandSelectedRegionAsync()
    {
        if (_busy || _live || _closing || SelectedSearchDrawing() is not { } drawing) return;
        Rect before = drawing.GetRect(), after = ExpandedSearchBounds(drawing);
        if (after.IsEmpty || after == before) return;
        string? target = (Metrics.SelectedItem as MetricRow)?.Target;
        drawing.SetRect(after);
        var draw = ImageView.EditorContext.DrawEditorContext;
        draw.DrawCanvas.AddActionCommand(new ActionCommand(() => drawing.SetRect(before), () => drawing.SetRect(after)) { Header = "扩大靶标选框" });
        draw.SelectionVisual.SetRender(drawing);
        await AnalyzeCurrentFrameAsync();
        var row = Metrics.Items.OfType<MetricRow>().FirstOrDefault(r => r.Target == target);
        if (row != null) Metrics.SelectedItem = row;
    }
}
