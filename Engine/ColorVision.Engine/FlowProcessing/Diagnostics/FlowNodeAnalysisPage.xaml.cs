using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal partial class FlowNodeAnalysisPage : Page
    {
        private readonly FlowExecutionAnalysisSession _session;
        private readonly FlowNodeRecord _record;
        private readonly Action<FlowNodeRecord> _navigateNode;
        private readonly Action<FlowNodeRecord> _navigateHistoryRecord;
        private readonly Action<FlowNodeRecord> _locateNode;
        private readonly Action<FlowNodeRecord?, int?> _openMessages;
        private readonly Action _showOverview;
        private readonly Action<FlowNodeRecord> _clearCurrentNode;
        private readonly IReadOnlyList<FlowNodeMessage> _messages;
        private CancellationTokenSource? _historyLoadCts;
        private int _historyLoadVersion;
        private bool _suppressHistorySelection;

        internal FlowNodeAnalysisPage(
            FlowExecutionAnalysisSession session,
            FlowNodeRecord record,
            bool canLocate,
            Action<FlowNodeRecord> navigateNode,
            Action<FlowNodeRecord> navigateHistoryRecord,
            Action<FlowNodeRecord> locateNode,
            Action<FlowNodeRecord?, int?> openMessages,
            Action showOverview,
            Action<FlowNodeRecord> clearCurrentNode)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _record = record ?? throw new ArgumentNullException(nameof(record));
            _navigateNode = navigateNode ?? throw new ArgumentNullException(nameof(navigateNode));
            _navigateHistoryRecord = navigateHistoryRecord
                ?? throw new ArgumentNullException(nameof(navigateHistoryRecord));
            _locateNode = locateNode ?? throw new ArgumentNullException(nameof(locateNode));
            _openMessages = openMessages ?? throw new ArgumentNullException(nameof(openMessages));
            _showOverview = showOverview ?? throw new ArgumentNullException(nameof(showOverview));
            _clearCurrentNode =
                clearCurrentNode ?? throw new ArgumentNullException(nameof(clearCurrentNode));
            _messages = _session.GetMessages(_record);

            InitializeComponent();
            LocateFlowNodeButton.IsEnabled = canLocate;
            bool hasAdjacentNodes = _session.Records.Count > 1;
            PreviousNodeButton.IsEnabled = hasAdjacentNodes;
            NextNodeButton.IsEnabled = hasAdjacentNodes;
            PopulateExecution();
            PopulateMessages();
        }

        private void PopulateExecution()
        {
            FlowNodeDurationAnalysis? duration = _session.FindDuration(_record);
            bool timedOut = _messages.Any(message =>
                    message.State == FlowMessageState.Timeout
                    || message.StatusCode == -2)
                || !_record.EndTime.HasValue;
            MessageListView.Tag = timedOut;
            long? elapsedMs = timedOut
                ? null
                : duration?.ElapsedMs
                    ?? FlowExecutionAnalysisPresentation.GetEffectiveElapsedMs(
                        _record,
                        _session.CapturedAt);

            NodeTitleText.Text = string.IsNullOrWhiteSpace(_record.NodeName) ? "未知节点" : _record.NodeName;
            NodeSubtitleText.Text = $"Batch {_record.BatchId} · {_record.NodeType ?? "未知类型"} · {_record.StartTime:yyyy/MM/dd HH:mm:ss.fff}";
            NodeElapsedText.Text = elapsedMs.HasValue
                ? FlowExecutionAnalysisPresentation.FormatDuration(elapsedMs.Value)
                : "—";
            NodeShareText.Text = timedOut || duration == null
                ? "—"
                : $"{duration.ShareOfNodeWorkPercent:N1}%";
            NodeBatchText.Text = _record.BatchId.ToString();
            NodeTypeText.Text = string.IsNullOrWhiteSpace(_record.NodeType) ? "—" : _record.NodeType;
            NodeStartTimeText.Text = _record.StartTime.ToString("HH:mm:ss.fff");
            NodeEndTimeText.Text = _record.EndTime?.ToString("HH:mm:ss.fff") ?? "—";
            NodeSerialNumberText.Text = string.IsNullOrWhiteSpace(_record.SerialNumber) ? "—" : _record.SerialNumber;
            NodeIdText.Text = string.IsNullOrWhiteSpace(_record.NodeId) ? "—" : _record.NodeId;

            FlowNodeExecutionOutcome outcome =
                FlowExecutionAnalysisPresentation.GetNodeExecutionOutcome(_record, _messages);
            if (outcome == FlowNodeExecutionOutcome.Failed)
            {
                NodeStateText.Text = timedOut ? "失败 · 超时" : "失败";
                NodeStateText.Foreground = (Brush)FindResource("AnalysisFailureBrush");
            }
            else if (outcome == FlowNodeExecutionOutcome.Succeeded)
            {
                NodeStateText.Text = duration?.IsWarning == true ? "成功 · 慢节点" : "成功";
                NodeStateText.Foreground = (Brush)FindResource("AnalysisSuccessBrush");
            }
            else
            {
                NodeStateText.Text = duration?.IsWarning == true
                    ? "已完成 · 未记录结果 · 慢节点"
                    : "已完成 · 未记录结果";
                NodeStateText.Foreground = (Brush)FindResource("AnalysisCompletedBrush");
            }
        }

        private void PopulateMessages()
        {
            MessageListView.ItemsSource = _messages;
            MessageSummaryText.Text = _messages.Count == 0
                ? "本次执行没有可追踪消息"
                : $"本次执行 {_messages.Count} 条消息 · 双击可进入完整消息页";
            bool hasMessages = _messages.Count > 0;
            MessageEmptyText.Visibility = hasMessages ? Visibility.Collapsed : Visibility.Visible;
            MessageListView.Visibility = hasMessages ? Visibility.Visible : Visibility.Collapsed;

            if (hasMessages)
                MessageListView.SelectedIndex = 0;
            else
                ClearMessageDetails();
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadHistoryAsync();
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            Interlocked.Increment(ref _historyLoadVersion);
            CancellationTokenSource? cancellationTokenSource = Interlocked.Exchange(ref _historyLoadCts, null);
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }

        private async Task LoadHistoryAsync()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            CancellationTokenSource? previous = Interlocked.Exchange(ref _historyLoadCts, cancellationTokenSource);
            previous?.Cancel();
            previous?.Dispose();
            int loadVersion = Interlocked.Increment(ref _historyLoadVersion);
            CancellationToken cancellationToken = cancellationTokenSource.Token;

            SetHistoryStatisticsLoading();
            NodeHistoryHintText.Text = "正在加载该节点最近的执行记录…";

            try
            {
                List<FlowNodeRecord> history;
                List<FlowNodeMessage> historyMessages;
                if (string.IsNullOrWhiteSpace(_record.NodeId))
                {
                    history = _session.Records
                        .Where(item => string.Equals(item.NodeName, _record.NodeName, StringComparison.Ordinal)
                            && string.Equals(item.NodeType, _record.NodeType, StringComparison.Ordinal))
                        .OrderByDescending(item => item.StartTime)
                        .ToList();
                    historyMessages = _session.Messages.ToList();
                }
                else
                {
                    string nodeId = _record.NodeId;
                    var result = await Task.Run(
                        () =>
                        {
                            List<FlowNodeRecord> records =
                                FlowNodeRecordDataBaseHelper.GetByNodeId(nodeId, 50);
                            int[] batchIds = records
                                .Select(item => item.BatchId)
                                .Append(_record.BatchId)
                                .Distinct()
                                .ToArray();
                            List<FlowNodeMessage> messages =
                                FlowNodeRecordDataBaseHelper.GetHistoryMessagesByNodeId(
                                    nodeId,
                                    batchIds);
                            return (Records: records, Messages: messages);
                        },
                        cancellationToken);
                    history = result.Records;
                    historyMessages = result.Messages;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (loadVersion != Volatile.Read(ref _historyLoadVersion) || !IsLoaded)
                    return;

                if (!history.Any(item => IsSameRecord(item, _record)))
                    history.Insert(0, _record);

                IReadOnlyList<FlowNodeHistoryAnalysis> historyItems =
                    FlowExecutionAnalysisPresentation.BuildNodeHistoryItems(
                        history,
                        historyMessages,
                        DateTime.Now);
                FlowNodeHistorySummary summary =
                    FlowExecutionAnalysisPresentation.BuildNodeHistorySummary(historyItems);

                _suppressHistorySelection = true;
                try
                {
                    NodeHistoryListView.ItemsSource = historyItems;
                    NodeHistoryListView.SelectedItem = historyItems
                        .FirstOrDefault(item => IsSameRecord(item.Record, _record));
                    if (NodeHistoryListView.SelectedItem != null)
                        NodeHistoryListView.ScrollIntoView(NodeHistoryListView.SelectedItem);
                }
                finally
                {
                    _suppressHistorySelection = false;
                }

                UpdateHistoryStatistics(summary);
                NodeHistoryHintText.Text = summary.TotalCount == 0
                    ? "暂时没有该节点的历史记录"
                    : $"最近 {summary.TotalCount} 次执行 · 单击一行切换；失败包含超时，超时不统计耗时";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (loadVersion != Volatile.Read(ref _historyLoadVersion) || !IsLoaded)
                    return;

                ClearHistoryStatistics();
                NodeHistoryHintText.Text = $"历史记录加载失败：{ex.Message}";
            }
            finally
            {
                if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _historyLoadCts, null, cancellationTokenSource),
                    cancellationTokenSource))
                {
                    cancellationTokenSource.Dispose();
                }
            }
        }

        private void SetHistoryStatisticsLoading()
        {
            NodeHistorySuccessAverageText.Text = "加载中…";
            NodeHistorySuccessP95Text.Text = "P95 —";
            NodeHistoryFailureAverageText.Text = "加载中…";
            NodeHistoryFailureP95Text.Text = "P95 —";
            NodeHistorySuccessCountText.Text = "—";
            NodeHistoryFailureCountText.Text = "—";
            NodeHistoryOtherCountText.Text = "其中超时 — · 未判定 —";
        }

        private void ClearHistoryStatistics()
        {
            NodeHistorySuccessAverageText.Text = "—";
            NodeHistorySuccessP95Text.Text = "P95 —";
            NodeHistoryFailureAverageText.Text = "—";
            NodeHistoryFailureP95Text.Text = "P95 —";
            NodeHistorySuccessCountText.Text = "0";
            NodeHistoryFailureCountText.Text = "0";
            NodeHistoryOtherCountText.Text = "其中超时 0 · 未判定 0";
        }

        private void UpdateHistoryStatistics(FlowNodeHistorySummary summary)
        {
            NodeHistorySuccessAverageText.Text = FormatHistoryDuration(summary.SuccessAverageMs);
            NodeHistorySuccessP95Text.Text = $"P95 {FormatHistoryDuration(summary.SuccessP95Ms)}";
            NodeHistoryFailureAverageText.Text = FormatHistoryDuration(summary.FailureAverageMs);
            NodeHistoryFailureP95Text.Text = $"P95 {FormatHistoryDuration(summary.FailureP95Ms)}";
            NodeHistorySuccessCountText.Text = summary.SuccessCount.ToString();
            NodeHistoryFailureCountText.Text = summary.FailureCount.ToString();
            string successRate = summary.SuccessRatePercent.HasValue
                ? $"{summary.SuccessRatePercent.Value:N1}%"
                : "—";
            NodeHistoryOtherCountText.Text =
                $"其中超时 {summary.TimeoutCount} · 未判定 {summary.CompletedCount} · 成功率 {successRate}";
        }

        private static string FormatHistoryDuration(long? elapsedMs)
        {
            return elapsedMs.HasValue
                ? FlowExecutionAnalysisPresentation.FormatDuration(elapsedMs.Value)
                : "—";
        }

        private static bool IsSameRecord(FlowNodeRecord left, FlowNodeRecord right)
        {
            if (left.Id > 0 && right.Id > 0)
                return left.Id == right.Id;

            return left.BatchId == right.BatchId
                && left.StartTime == right.StartTime
                && string.Equals(left.NodeId, right.NodeId, StringComparison.Ordinal)
                && string.Equals(left.SerialNumber, right.SerialNumber, StringComparison.Ordinal);
        }

        private void NodeHistoryListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressHistorySelection
                || NodeHistoryListView.SelectedItem is not FlowNodeHistoryAnalysis historyItem
                || IsSameRecord(historyItem.Record, _record))
            {
                return;
            }

            _navigateHistoryRecord(historyItem.Record);
        }

        private void MessageListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MessageListView.SelectedItem is not FlowNodeMessage message)
            {
                ClearMessageDetails();
                return;
            }

            SendTopicText.Text = string.IsNullOrWhiteSpace(message.SendTopic)
                ? "未记录发送 Topic"
                : message.SendTopic;
            RecvTopicText.Text = string.IsNullOrWhiteSpace(message.RecvTopic)
                ? "尚未收到响应或未记录接收 Topic"
                : message.RecvTopic;
            SendPayloadTextBox.Text = FormatJsonSafe(message.SendPayload);
            RecvPayloadTextBox.Text = FormatJsonSafe(message.RecvPayload);
        }

        private void ClearMessageDetails()
        {
            SendTopicText.Text = "选择一条消息查看 Topic 与 Payload";
            RecvTopicText.Text = "选择一条消息查看 Topic 与 Payload";
            SendPayloadTextBox.Text = string.Empty;
            RecvPayloadTextBox.Text = string.Empty;
        }

        private static string FormatJsonSafe(string? json)
        {
            if (string.IsNullOrEmpty(json))
                return string.Empty;

            try
            {
                object? value = JsonConvert.DeserializeObject(json);
                return JsonConvert.SerializeObject(value, Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }

        private void ShowOverviewButton_Click(object sender, RoutedEventArgs e)
        {
            _showOverview();
        }

        private void PreviousNodeButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateAdjacent(-1);
        }

        private void NextNodeButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateAdjacent(1);
        }

        private void NavigateAdjacent(int offset)
        {
            FlowNodeRecord? adjacent = _session.GetAdjacentRecord(_record, offset);
            if (adjacent != null && !ReferenceEquals(adjacent, _record))
                _navigateNode(adjacent);
        }

        private void LocateFlowNodeButton_Click(object sender, RoutedEventArgs e)
        {
            _locateNode(_record);
        }

        private void ClearCurrentNodeButton_Click(object sender, RoutedEventArgs e)
        {
            _clearCurrentNode(_record);
        }

        private void OpenMessagesButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSelectedMessage();
        }

        private void MessageListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (MessageListView.SelectedItem is FlowNodeMessage)
                OpenSelectedMessage();
        }

        private void OpenSelectedMessage()
        {
            int? messageId = MessageListView.SelectedItem is FlowNodeMessage message && message.Id > 0
                ? message.Id
                : null;
            _openMessages(_record, messageId);
        }

    }
}
