using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal partial class FlowExecutionOverviewPage : Page
    {
        private readonly FlowExecutionAnalysisSession _session;
        private readonly Action<FlowNodeRecord> _openNode;
        private readonly Action<FlowNodeRecord> _locateNode;
        private readonly Action _openMessages;
        private readonly Action _clearCurrentFlow;
        private bool _isTimelineRendered;

        internal FlowExecutionOverviewPage(
            FlowExecutionAnalysisSession session,
            Action<FlowNodeRecord> openNode,
            Action<FlowNodeRecord> locateNode,
            Action openMessages,
            Action clearCurrentFlow,
            bool canLocate)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _openNode = openNode ?? throw new ArgumentNullException(nameof(openNode));
            _locateNode = locateNode ?? throw new ArgumentNullException(nameof(locateNode));
            _openMessages = openMessages ?? throw new ArgumentNullException(nameof(openMessages));
            _clearCurrentFlow =
                clearCurrentFlow ?? throw new ArgumentNullException(nameof(clearCurrentFlow));
            CanLocate = canLocate;

            InitializeComponent();
            DurationListBox.ItemsSource = _session.DurationItems;
            PopulateHeader();
            PopulateSummary();
            UpdateEmptyState();
        }

        public bool CanLocate { get; }

        private void PopulateHeader()
        {
            string serialText = string.IsNullOrWhiteSpace(_session.SerialNumber)
                ? string.Empty
                : $" · SN {_session.SerialNumber}";
            SessionText.Text = EngineLocalization.Format($"Batch {_session.BatchId} · {_session.Records.Count} 个执行节点{serialText}");
            OpenMessagesButton.Content = EngineLocalization.Format($"查看本批消息（{_session.Messages.Count}）");
        }

        private void PopulateSummary()
        {
            FlowExecutionAnalysisSummary summary = _session.Summary;
            long wallClockMs = _session.WallClockMs;

            SummaryTotalTimeText.Text = FlowExecutionAnalysisPresentation.FormatDuration(wallClockMs);
            var timingParts = new List<string>();
            if (_session.PhaseSummary.PreProcessMs is long preProcessMs)
            {
                timingParts.Add(
                    EngineLocalization.Format($"前处理 {FlowExecutionAnalysisPresentation.FormatDuration(preProcessMs)}"));
            }
            timingParts.Add(
                EngineLocalization.Format($"节点活动 {FlowExecutionAnalysisPresentation.FormatDuration(summary.ActiveMs)}"));
            timingParts.Add(
                _session.PhaseSummary.PreProcessMs.HasValue
                    ? EngineLocalization.Format($"其他流程开销 {FlowExecutionAnalysisPresentation.FormatDuration(_session.OtherExecutionMs)}")
                    : EngineLocalization.Format($"节点外耗时 {FlowExecutionAnalysisPresentation.FormatDuration(_session.NodeInactiveMs)}"));
            if (_session.PhaseSummary.PostProcessMs is long postProcessMs)
            {
                timingParts.Add(
                    EngineLocalization.Format($"后处理 {FlowExecutionAnalysisPresentation.FormatDuration(postProcessMs)}（另计）"));
            }
            if (summary.OverlapMs > 0)
            {
                timingParts.Add(
                    EngineLocalization.Format($"节点并行重叠 {FlowExecutionAnalysisPresentation.FormatDuration(summary.OverlapMs)}"));
            }
            SummaryTotalTimeHintText.Text = string.Join(" · ", timingParts);

            SummaryNodeCountText.Text = summary.NodeCount.ToString();
            SummaryNodeStateText.Text = BuildNodeStateSummary(summary);
            SummarySlowestNodeText.Text = summary.SlowestNodeName;
            SummarySlowestTimeText.Text = summary.SlowestNodeName == "—"
                ? "—"
                : FlowExecutionAnalysisPresentation.FormatDuration(summary.SlowestNodeElapsedMs);

            SummaryMessageCountText.Text = _session.Messages.Count.ToString();
            int messageIssueCount = _session.Messages.Count(item =>
                item.State == FlowMessageState.Fail || item.State == FlowMessageState.Timeout);
            SummaryMessageStateText.Text = messageIssueCount > 0
                ? EngineLocalization.Format($"{messageIssueCount} 条失败或超时")
                : _session.Messages.Count > 0
                    ? EngineLocalization.Get("未发现失败或超时")
                    : EngineLocalization.Get("本批次没有 MQTT 记录");
        }

        private static string BuildNodeStateSummary(FlowExecutionAnalysisSummary summary)
        {
            var parts = new List<string>();
            int completedCount = summary.NodeCount - summary.TimeoutCount;
            if (completedCount > 0)
                parts.Add(EngineLocalization.Format($"{completedCount} 已完成"));
            if (summary.TimeoutCount > 0)
                parts.Add(EngineLocalization.Format($"{summary.TimeoutCount} 超时"));
            if (summary.WarningCount > 0)
                parts.Add(EngineLocalization.Format($"{summary.WarningCount} 个慢节点"));
            return parts.Count == 0 ? EngineLocalization.Get("等待执行记录") : string.Join(" · ", parts);
        }

        private void UpdateEmptyState()
        {
            bool hasRecords = _session.Records.Count > 0;
            DurationEmptyText.Visibility = hasRecords ? Visibility.Collapsed : Visibility.Visible;
            TimelineEmptyText.Visibility = hasRecords ? Visibility.Collapsed : Visibility.Visible;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isTimelineRendered)
                return;

            _isTimelineRendered = true;
            RenderTimeline();
        }

        private void OpenMessagesButton_Click(object sender, RoutedEventArgs e)
        {
            _openMessages();
        }

        private void ClearCurrentFlowButton_Click(object sender, RoutedEventArgs e)
        {
            _clearCurrentFlow();
        }

        private void OpenNodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: FlowNodeRecord record })
                _openNode(record);
        }

        private void LocateNodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (CanLocate && sender is Button { Tag: FlowNodeRecord record })
                _locateNode(record);
        }

        private void DurationListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            DependencyObject? source = e.OriginalSource as DependencyObject;
            if (FindVisualAncestor<Button>(source) != null)
                return;

            if (FindVisualAncestor<ListBoxItem>(source)?.DataContext is not FlowNodeDurationAnalysis item)
                return;

            _openNode(item.Record);
            e.Handled = true;
        }

        private void DurationListBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter
                || FindVisualAncestor<Button>(e.OriginalSource as DependencyObject) != null)
            {
                return;
            }

            if (OpenSelectedNode())
                e.Handled = true;
        }

        private bool OpenSelectedNode()
        {
            if (DurationListBox.SelectedItem is not FlowNodeDurationAnalysis item)
                return false;

            _openNode(item.Record);
            return true;
        }

        private static T? FindVisualAncestor<T>(DependencyObject? source)
            where T : DependencyObject
        {
            DependencyObject? current = source;
            while (current != null)
            {
                if (current is T ancestor)
                    return ancestor;
                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void RenderTimeline()
        {
            TimelinePlot.Plot.Clear();
            TimelinePlot.Plot.Legend.ManualItems.Clear();
            TimelinePlot.Plot.Legend.IsVisible = false;

            if (_session.Records.Count == 0)
            {
                TimelinePlot.Plot.Title(string.Empty);
                TimelinePlot.Plot.XLabel(string.Empty);
                TimelinePlot.Plot.YLabel(string.Empty);
                TimelinePlot.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();
                TimelinePlot.Plot.Axes.NumericTicksBottom();
                TimelinePlot.Plot.Axes.SetLimits(0, 1, 0, 1);
                TimelinePlot.Refresh();
                return;
            }

            SetupChineseFonts();
            DateTime baseTime = _session.Records.Min(item => item.StartTime);
            double totalMs = _session.Records.Max(item =>
                ((item.EndTime ?? item.StartTime) - baseTime).TotalMilliseconds);
            totalMs = Math.Max(1, totalMs);

            ScottPlot.Color completedColor = ScottPlot.Color.FromHex("#4D8DFF");
            ScottPlot.Color timeoutColor = ScottPlot.Color.FromHex("#D84A4A");
            ScottPlot.Color warningColor = ScottPlot.Color.FromHex("#D99000");
            var bars = new List<ScottPlot.Bar>(_session.Records.Count);
            var ticks = new List<ScottPlot.Tick>(_session.Records.Count);
            var durations = _session.DurationItems.ToDictionary(item => item.Record);

            for (int index = 0; index < _session.Records.Count; index++)
            {
                FlowNodeRecord record = _session.Records[index];
                FlowNodeDurationAnalysis duration = durations[record];
                double startOffset = Math.Max(0, (record.StartTime - baseTime).TotalMilliseconds);
                double endOffset = Math.Max(
                    startOffset,
                    ((record.EndTime ?? record.StartTime) - baseTime).TotalMilliseconds);
                double yPosition = _session.Records.Count - 1 - index;
                ScottPlot.Color color = duration.IsTimedOut
                    ? timeoutColor
                    : duration.IsWarning
                        ? warningColor
                        : completedColor;

                bars.Add(new ScottPlot.Bar
                {
                    Position = yPosition,
                    ValueBase = startOffset,
                    Value = endOffset,
                    FillColor = color,
                    IsVisible = true,
                    Orientation = ScottPlot.Orientation.Horizontal,
                    Size = 0.62
                });

                string nodeName = string.IsNullOrWhiteSpace(record.NodeName) ? "Unknown" : record.NodeName;
                ticks.Add(new ScottPlot.Tick(yPosition, $"{index + 1}. {nodeName}"));
            }

            ScottPlot.Plottables.BarPlot barPlot = TimelinePlot.Plot.Add.Bars(bars.ToArray());
            barPlot.Horizontal = true;
            TimelinePlot.Plot.Axes.Left.TickGenerator =
                new ScottPlot.TickGenerators.NumericManual(ticks.ToArray());
            TimelinePlot.Plot.Title(EngineLocalization.Format($"执行时序 · Batch {_session.BatchId}"));
            TimelinePlot.Plot.XLabel(EngineLocalization.Get("时间 (ms)"));
            TimelinePlot.Plot.YLabel(string.Empty);
            TimelinePlot.Plot.Axes.AutoScale();
            TimelinePlot.Plot.Axes.SetLimitsX(0, totalMs * 1.04);
            TimelinePlot.Plot.Axes.Margins(left: 0, bottom: 0.08);
            TimelinePlot.Refresh();
        }

        private void SetupChineseFonts()
        {
            string chineseFont = ScottPlot.Fonts.Detect("中文");
            TimelinePlot.Plot.Axes.Title.Label.FontName = chineseFont;
            TimelinePlot.Plot.Axes.Left.Label.FontName = chineseFont;
            TimelinePlot.Plot.Axes.Bottom.Label.FontName = chineseFont;
            TimelinePlot.Plot.Axes.Left.TickLabelStyle.FontName = chineseFont;
            TimelinePlot.Plot.Axes.Bottom.TickLabelStyle.FontName = chineseFont;
            TimelinePlot.Plot.Legend.FontName = chineseFont;
        }
    }
}
