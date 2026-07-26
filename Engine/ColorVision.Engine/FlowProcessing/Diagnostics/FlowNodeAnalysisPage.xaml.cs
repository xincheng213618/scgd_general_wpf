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
        private readonly Action<FlowNodeRecord> _locateNode;
        private readonly Action<FlowNodeRecord?, int?> _openMessages;
        private readonly Action _showOverview;
        private readonly IReadOnlyList<FlowNodeMessage> _messages;
        private CancellationTokenSource? _historyLoadCts;
        private int _historyLoadVersion;

        internal FlowNodeAnalysisPage(
            FlowExecutionAnalysisSession session,
            FlowNodeRecord record,
            bool canLocate,
            Action<FlowNodeRecord> navigateNode,
            Action<FlowNodeRecord> locateNode,
            Action<FlowNodeRecord?, int?> openMessages,
            Action showOverview)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _record = record ?? throw new ArgumentNullException(nameof(record));
            _navigateNode = navigateNode ?? throw new ArgumentNullException(nameof(navigateNode));
            _locateNode = locateNode ?? throw new ArgumentNullException(nameof(locateNode));
            _openMessages = openMessages ?? throw new ArgumentNullException(nameof(openMessages));
            _showOverview = showOverview ?? throw new ArgumentNullException(nameof(showOverview));
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
            long elapsedMs = duration?.ElapsedMs
                ?? FlowExecutionAnalysisPresentation.GetEffectiveElapsedMs(_record, _session.CapturedAt);

            NodeTitleText.Text = string.IsNullOrWhiteSpace(_record.NodeName) ? "未知节点" : _record.NodeName;
            NodeSubtitleText.Text = $"Batch {_record.BatchId} · {_record.NodeType ?? "未知类型"} · {_record.StartTime:yyyy/MM/dd HH:mm:ss.fff}";
            NodeElapsedText.Text = FlowExecutionAnalysisPresentation.FormatDuration(elapsedMs);
            NodeShareText.Text = duration == null ? "—" : $"{duration.ShareOfNodeWorkPercent:N1}%";
            NodeBatchText.Text = _record.BatchId.ToString();
            NodeTypeText.Text = string.IsNullOrWhiteSpace(_record.NodeType) ? "—" : _record.NodeType;
            NodeStartTimeText.Text = _record.StartTime.ToString("HH:mm:ss.fff");
            NodeEndTimeText.Text = _record.EndTime?.ToString("HH:mm:ss.fff") ?? "—";
            NodeSerialNumberText.Text = string.IsNullOrWhiteSpace(_record.SerialNumber) ? "—" : _record.SerialNumber;
            NodeIdText.Text = string.IsNullOrWhiteSpace(_record.NodeId) ? "—" : _record.NodeId;

            if (!_record.EndTime.HasValue)
            {
                NodeStateText.Text = duration?.IsWarning == true ? "运行中 · 已超慢节点阈值" : "运行中";
                NodeStateText.Foreground = (Brush)FindResource("AnalysisRunningBrush");
            }
            else if (duration?.IsWarning == true)
            {
                NodeStateText.Text = "已完成 · 慢节点";
                NodeStateText.Foreground = (Brush)FindResource("AnalysisWarningBrush");
            }
            else
            {
                NodeStateText.Text = "已完成";
                NodeStateText.Foreground = (Brush)FindResource("AnalysisSuccessBrush");
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

            NodeHistoryAverageText.Text = "加载中…";
            NodeHistoryP95Text.Text = "加载中…";
            NodeHistoryHintText.Text = "正在加载该节点最近的执行记录…";

            try
            {
                List<FlowNodeRecord> history;
                if (string.IsNullOrWhiteSpace(_record.NodeId))
                {
                    history = _session.Records
                        .Where(item => string.Equals(item.NodeName, _record.NodeName, StringComparison.Ordinal)
                            && string.Equals(item.NodeType, _record.NodeType, StringComparison.Ordinal))
                        .OrderByDescending(item => item.StartTime)
                        .ToList();
                }
                else
                {
                    string nodeId = _record.NodeId;
                    history = await Task.Run(
                        () => FlowNodeRecordDataBaseHelper.GetByNodeId(nodeId, 50),
                        cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (loadVersion != Volatile.Read(ref _historyLoadVersion) || !IsLoaded)
                    return;

                NodeHistoryListView.ItemsSource = history;
                long[] completedElapsed = history
                    .Where(item => item.EndTime.HasValue)
                    .Select(item => Math.Max(0, item.ElapsedMs))
                    .OrderBy(item => item)
                    .ToArray();
                if (completedElapsed.Length == 0)
                {
                    NodeHistoryAverageText.Text = "—";
                    NodeHistoryP95Text.Text = "—";
                    NodeHistoryHintText.Text = "暂时没有已完成的历史记录";
                    return;
                }

                long average = Convert.ToInt64(completedElapsed.Average());
                int p95Index = Math.Clamp(
                    (int)Math.Ceiling(completedElapsed.Length * 0.95) - 1,
                    0,
                    completedElapsed.Length - 1);
                NodeHistoryAverageText.Text = FlowExecutionAnalysisPresentation.FormatDuration(average);
                NodeHistoryP95Text.Text = FlowExecutionAnalysisPresentation.FormatDuration(completedElapsed[p95Index]);
                NodeHistoryHintText.Text = $"最近 {completedElapsed.Length} 次已完成执行，用于识别偶发抖动与长期瓶颈";
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (loadVersion != Volatile.Read(ref _historyLoadVersion) || !IsLoaded)
                    return;

                NodeHistoryAverageText.Text = "—";
                NodeHistoryP95Text.Text = "—";
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
