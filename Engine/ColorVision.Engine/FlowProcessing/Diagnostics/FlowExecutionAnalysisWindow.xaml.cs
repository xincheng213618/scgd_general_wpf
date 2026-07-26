#pragma warning disable CS8629
using ColorVision.Themes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    public partial class FlowExecutionAnalysisWindow : Window
    {
        private const long SlowNodeThresholdMs = 30000;
        private readonly MeasureBatchModel? _initialBatch;
        private readonly string? _initialNodeId;
        private readonly string? _initialNodeName;
        private readonly Func<FlowNodeRecord, bool>? _focusFlowNode;
        private readonly Stack<FlowAnalysisNavigationState> _backStack = new Stack<FlowAnalysisNavigationState>();
        private readonly Stack<FlowAnalysisNavigationState> _forwardStack = new Stack<FlowAnalysisNavigationState>();
        private readonly BulkObservableCollection<FlowNodeDurationAnalysis> _durationItems = new BulkObservableCollection<FlowNodeDurationAnalysis>();
        private readonly BulkObservableCollection<FlowNodeRecord> _nodeRecords = new BulkObservableCollection<FlowNodeRecord>();
        private readonly BulkObservableCollection<FlowNodeMessage> _nodeMessages = new BulkObservableCollection<FlowNodeMessage>();
        private readonly ObservableCollection<FlowNodeRecord> _nodeHistoryRecords = new ObservableCollection<FlowNodeRecord>();
        private List<FlowNodeMessage> _loadedMessages = new List<FlowNodeMessage>();
        private List<int> _loadedBatchIds = new List<int>();
        private FlowNodeDurationAnalysis? _selectedDurationItem;
        private FlowNodeRecord? _selectedNodeRecord;
        private int? _messageBatchScope;
        private bool _isComponentInitialized;
        private bool _isApplyingNavigation;
        private bool _isUpdatingMessageFilters;
        private int _loadVersion;

        public ObservableCollection<FlowNodeRecord> NodeRecords => _nodeRecords;

        public ObservableCollection<FlowNodeMessage> NodeMessages => _nodeMessages;

        public FlowExecutionAnalysisWindow()
            : this(null, null, null, null)
        {
        }

        public FlowExecutionAnalysisWindow(MeasureBatchModel batch)
            : this(batch, null, null, null)
        {
        }

        internal FlowExecutionAnalysisWindow(Func<FlowNodeRecord, bool> focusFlowNode)
            : this(null, null, null, focusFlowNode)
        {
        }

        internal FlowExecutionAnalysisWindow(MeasureBatchModel batch, Func<FlowNodeRecord, bool> focusFlowNode)
            : this(batch, null, null, focusFlowNode)
        {
        }

        internal FlowExecutionAnalysisWindow(string nodeId, string? nodeName, Func<FlowNodeRecord, bool>? focusFlowNode)
            : this(null, nodeId, nodeName, focusFlowNode)
        {
        }

        private FlowExecutionAnalysisWindow(
            MeasureBatchModel? batch,
            string? initialNodeId,
            string? initialNodeName,
            Func<FlowNodeRecord, bool>? focusFlowNode)
        {
            _initialBatch = batch;
            _initialNodeId = initialNodeId;
            _initialNodeName = initialNodeName;
            _focusFlowNode = focusFlowNode;
            InitializeComponent();
            _isComponentInitialized = true;
            this.ApplyCaption();
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            NodeRecordListView.ItemsSource = NodeRecords;
            MessageListView.ItemsSource = NodeMessages;
            DurationListBox.ItemsSource = _durationItems;
            NodeHistoryListView.ItemsSource = _nodeHistoryRecords;
            LocateFlowNodeButton.IsEnabled = _focusFlowNode != null;

            var initialData = await Task.Run(() =>
            {
                FlowNodeRecordDataBaseHelper.FlushPendingWrites();
                List<int> batchIds = FlowNodeRecordDataBaseHelper.GetDistinctBatchIds(100);
                FlowNodeRecord? initialNodeRecord = string.IsNullOrWhiteSpace(_initialNodeId)
                    ? null
                    : FlowNodeRecordDataBaseHelper.GetLastByNodeId(_initialNodeId);
                return (BatchIds: batchIds, InitialNodeRecord: initialNodeRecord);
            });

            BatchListView.ItemsSource = initialData.BatchIds;

            int? initialBatchId = _initialBatch?.Id ?? initialData.InitialNodeRecord?.BatchId;
            if (!initialBatchId.HasValue && initialData.BatchIds.Count > 0)
                initialBatchId = initialData.BatchIds[0];

            if (!initialBatchId.HasValue)
            {
                UpdateEmptyState();
                return;
            }

            SelectBatchIds(new[] { initialBatchId.Value });
            await LoadAnalysisAsync(new List<int> { initialBatchId.Value });

            FlowNodeRecord? recordToOpen = FindLoadedNodeRecord(
                initialData.InitialNodeRecord?.Id,
                _initialNodeId,
                initialData.InitialNodeRecord?.BatchId);
            if (recordToOpen != null)
                NavigateToNode(recordToOpen, addHistory: false);
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            List<int> selectedBatchIds = GetSelectedBatchIds();
            if (selectedBatchIds.Count == 0)
            {
                MessageBox.Show(
                    Properties.Resources.Flow_NodeAnalysis_SelectAtLeastOneBatch,
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            await LoadAnalysisAsync(selectedBatchIds);
        }

        private async void CompareButton_Click(object sender, RoutedEventArgs e)
        {
            List<int> selectedBatchIds = GetSelectedBatchIds();
            if (selectedBatchIds.Count < 2)
            {
                MessageBox.Show(
                    Properties.Resources.Flow_NodeAnalysis_SelectAtLeastTwoBatches,
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            await LoadAnalysisAsync(selectedBatchIds);
            AnalysisTabControl.SelectedItem = DistributionTab;
        }

        private async Task LoadAnalysisAsync(List<int> batchIds)
        {
            int loadVersion = ++_loadVersion;
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var result = await Task.Run(() =>
                {
                    FlowNodeRecordDataBaseHelper.FlushPendingWrites();
                    List<FlowNodeRecord> records = FlowNodeRecordDataBaseHelper.GetByBatchIds(batchIds);
                    List<FlowNodeMessage> messages = FlowNodeRecordDataBaseHelper.GetMessagesByBatchIds(batchIds);
                    return (Records: records, Messages: messages);
                });

                if (loadVersion != _loadVersion)
                    return;

                _backStack.Clear();
                _forwardStack.Clear();
                UpdateNavigationButtons();
                _loadedBatchIds = batchIds.Distinct().OrderBy(item => item).ToList();
                _loadedMessages = result.Messages;

                _nodeRecords.ResetWith(result.Records);

                DateTime now = DateTime.Now;
                IReadOnlyList<FlowNodeDurationAnalysis> durationItems =
                    FlowExecutionAnalysisPresentation.BuildDurationItems(result.Records, now, SlowNodeThresholdMs);
                _durationItems.ResetWith(durationItems);

                RefreshMessageFilter();
                UpdateSummary(result.Records, durationItems, now);
                RenderAnalysisChart(result.Records, batchIds, now);
                UpdateWorkspaceSubtitle(result.Records, batchIds);
                UpdateEmptyState();

                if (_selectedNodeRecord != null)
                {
                    FlowNodeRecord? refreshedRecord = FindLoadedNodeRecord(
                        _selectedNodeRecord.Id,
                        _selectedNodeRecord.NodeId,
                        _selectedNodeRecord.BatchId);
                    if (refreshedRecord != null)
                        SelectNode(refreshedRecord);
                    else
                        ClearNodeSelection();
                }
            }
            finally
            {
                if (loadVersion == _loadVersion)
                    Mouse.OverrideCursor = null;
            }
        }

        private void UpdateSummary(
            IReadOnlyList<FlowNodeRecord> records,
            IReadOnlyList<FlowNodeDurationAnalysis> durationItems,
            DateTime now)
        {
            FlowExecutionAnalysisSummary summary =
                FlowExecutionAnalysisPresentation.BuildSummary(records, durationItems, now);

            long wallClockMs = summary.AverageWallClockMs;
            if (_loadedBatchIds.Count == 1
                && _initialBatch?.Id == _loadedBatchIds[0]
                && _initialBatch.TotalTime > 0)
            {
                wallClockMs = _initialBatch.TotalTime;
            }

            SummaryTotalTimeText.Text = FormatDuration(wallClockMs);
            long idleMs = Math.Max(0, wallClockMs - summary.AverageActiveMs);
            string timingBreakdown =
                $"活动 {FormatDuration(summary.AverageActiveMs)} · 空档 {FormatDuration(idleMs)}";
            if (summary.AverageOverlapMs > 0)
                timingBreakdown += $" · 并行 {FormatDuration(summary.AverageOverlapMs)}";
            SummaryTotalTimeHintText.Text = _loadedBatchIds.Count > 1
                ? $"{_loadedBatchIds.Count} 批平均 · {timingBreakdown}"
                : timingBreakdown;
            SummaryNodeCountText.Text = summary.NodeCount.ToString();
            SummaryNodeStateText.Text = BuildNodeStateSummary(summary);
            SummarySlowestNodeText.Text = summary.SlowestNodeName;
            SummarySlowestTimeText.Text = summary.NodeCount == 0
                ? "—"
                : FormatDuration(summary.SlowestNodeElapsedMs);
            SummaryMessageCountText.Text = _loadedMessages.Count.ToString();

            int messageIssueCount = _loadedMessages.Count(item =>
                item.State == FlowMessageState.Fail || item.State == FlowMessageState.Timeout);
            SummaryMessageStateText.Text = messageIssueCount > 0
                ? $"{messageIssueCount} 条失败或超时"
                : _loadedMessages.Count > 0
                    ? "未发现失败或超时"
                    : "当前批次没有 MQTT 记录";

            DistributionHintText.Text = _loadedBatchIds.Count > 1
                ? "按各节点在所选批次中的平均耗时排序；条形长度相对最慢节点"
                : "按耗时从高到低排列；条形长度相对最慢节点，右侧比例为节点工作量占比";
        }

        private static string BuildNodeStateSummary(FlowExecutionAnalysisSummary summary)
        {
            var parts = new List<string>();
            int completeCount = summary.NodeCount - summary.RunningCount;
            if (completeCount > 0)
                parts.Add($"{completeCount} 已完成");
            if (summary.RunningCount > 0)
                parts.Add($"{summary.RunningCount} 运行中");
            if (summary.WarningCount > 0)
                parts.Add($"{summary.WarningCount} 个慢节点");
            return parts.Count == 0 ? "等待加载" : string.Join(" · ", parts);
        }

        private void UpdateWorkspaceSubtitle(IReadOnlyList<FlowNodeRecord> records, IReadOnlyList<int> batchIds)
        {
            string batchText = batchIds.Count == 1
                ? $"Batch {batchIds[0]}"
                : $"{batchIds.Count} 个批次对比";
            string serialNumber = records
                .Select(item => item.SerialNumber)
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? string.Empty;
            string serialText = string.IsNullOrWhiteSpace(serialNumber) ? string.Empty : $" · SN {serialNumber}";
            WorkspaceSubtitleText.Text = $"{batchText} · {records.Count} 次节点执行{serialText}";
        }

        private void UpdateEmptyState()
        {
            bool hasRecords = NodeRecords.Count > 0;
            DistributionEmptyText.Visibility = hasRecords ? Visibility.Collapsed : Visibility.Visible;
        }

        private void AnalyzeNode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: FlowNodeRecord record })
                NavigateToNode(record);
        }

        private void DurationListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DurationListBox.SelectedItem is FlowNodeDurationAnalysis item)
                NavigateToNode(item.Record);
        }

        private void NodeRecordListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (NodeRecordListView.SelectedItem is FlowNodeRecord record)
                NavigateToNode(record);
        }

        private void NavigateToNode(FlowNodeRecord record, bool addHistory = true)
        {
            NavigateTo(
                new FlowAnalysisNavigationState(NodeDetailsTabIndex, record.Id, record.NodeId, null, null),
                addHistory);
        }

        private void SelectNode(FlowNodeRecord record)
        {
            _selectedNodeRecord = record;
            _selectedDurationItem = _durationItems.FirstOrDefault(item =>
                item.Records.Any(candidate => IsSameRecord(candidate, record)));
            NodeRecordListView.SelectedItem = record;
            DurationListBox.SelectedItem = _selectedDurationItem;

            DateTime now = DateTime.Now;
            long elapsedMs = FlowExecutionAnalysisPresentation.GetEffectiveElapsedMs(record, now);
            NodeDetailTitleText.Text = string.IsNullOrWhiteSpace(record.NodeName) ? "Unknown" : record.NodeName;
            NodeDetailSubtitleText.Text = $"Batch {record.BatchId} · {record.NodeType ?? "Unknown"}";
            NodeElapsedText.Text = FormatDuration(elapsedMs);
            NodeShareText.Text = _selectedDurationItem == null
                ? "—"
                : $"{_selectedDurationItem.ShareOfNodeWorkPercent:N1}%";
            NodeBatchText.Text = record.BatchId.ToString();
            NodeTypeText.Text = record.NodeType ?? "—";
            NodeIdText.Text = record.NodeId ?? "—";
            NodeStartTimeText.Text = record.StartTime.ToString("yyyy/MM/dd HH:mm:ss.fff");
            NodeEndTimeText.Text = record.EndTime?.ToString("yyyy/MM/dd HH:mm:ss.fff") ?? "—";
            NodeSerialNumberText.Text = record.SerialNumber ?? "—";
            SetNodeState(record, elapsedMs);

            NodeDetailEmptyText.Visibility = Visibility.Collapsed;
            NodeDetailContent.Visibility = Visibility.Visible;
            _ = LoadNodeHistoryAsync(record);
        }

        private void SetNodeState(FlowNodeRecord record, long elapsedMs)
        {
            if (!record.EndTime.HasValue)
            {
                NodeStateText.Text = elapsedMs > SlowNodeThresholdMs ? "运行中 · 已超过慢节点阈值" : "运行中";
                NodeStateText.Foreground = (Brush)FindResource("AnalysisRunningBrush");
                return;
            }

            if (elapsedMs > SlowNodeThresholdMs)
            {
                NodeStateText.Text = $"已完成 · 慢节点（>{SlowNodeThresholdMs / 1000}s）";
                NodeStateText.Foreground = (Brush)FindResource("AnalysisWarningBrush");
                return;
            }

            NodeStateText.Text = "已完成";
            NodeStateText.Foreground = (Brush)FindResource("AnalysisSuccessBrush");
        }

        private async Task LoadNodeHistoryAsync(FlowNodeRecord selectedRecord)
        {
            List<FlowNodeRecord> history = string.IsNullOrWhiteSpace(selectedRecord.NodeId)
                ? NodeRecords
                    .Where(item => string.Equals(item.NodeName, selectedRecord.NodeName, StringComparison.Ordinal)
                        && string.Equals(item.NodeType, selectedRecord.NodeType, StringComparison.Ordinal))
                    .OrderByDescending(item => item.StartTime)
                    .ToList()
                : await Task.Run(() => FlowNodeRecordDataBaseHelper.GetByNodeId(selectedRecord.NodeId, 50));

            if (_selectedNodeRecord == null || !IsSameRecord(_selectedNodeRecord, selectedRecord))
                return;

            _nodeHistoryRecords.Clear();
            foreach (FlowNodeRecord record in history)
                _nodeHistoryRecords.Add(record);

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
            int p95Index = Math.Clamp((int)Math.Ceiling(completedElapsed.Length * 0.95) - 1, 0, completedElapsed.Length - 1);
            NodeHistoryAverageText.Text = FormatDuration(average);
            NodeHistoryP95Text.Text = FormatDuration(completedElapsed[p95Index]);
            NodeHistoryHintText.Text = $"最近 {completedElapsed.Length} 次已完成执行，用于识别偶发抖动与长期瓶颈";
        }

        private void ClearNodeSelection()
        {
            _selectedNodeRecord = null;
            _selectedDurationItem = null;
            NodeRecordListView.SelectedItem = null;
            DurationListBox.SelectedItem = null;
            _nodeHistoryRecords.Clear();
            NodeDetailContent.Visibility = Visibility.Collapsed;
            NodeDetailEmptyText.Visibility = Visibility.Visible;
        }

        private void PreviousNodeButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToAdjacentNode(-1);
        }

        private void NextNodeButton_Click(object sender, RoutedEventArgs e)
        {
            NavigateToAdjacentNode(1);
        }

        private void NavigateToAdjacentNode(int offset)
        {
            if (_selectedNodeRecord == null || NodeRecords.Count == 0)
                return;

            int currentIndex = NodeRecords
                .Select((record, index) => new { record, index })
                .FirstOrDefault(item => IsSameRecord(item.record, _selectedNodeRecord))?.index ?? -1;
            if (currentIndex < 0)
                return;

            int targetIndex = (currentIndex + offset + NodeRecords.Count) % NodeRecords.Count;
            NavigateToNode(NodeRecords[targetIndex]);
        }

        private void LocateFlowNodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNodeRecord == null || _focusFlowNode == null)
                return;

            if (!_focusFlowNode(_selectedNodeRecord))
            {
                MessageBox.Show(
                    "当前流程图中没有找到这个节点。历史批次可能来自另一个流程模板。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void ViewNodeMessagesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedNodeRecord == null)
                return;

            NavigateTo(
                new FlowAnalysisNavigationState(
                    MessageTraceTabIndex,
                    _selectedNodeRecord.Id,
                    GetMessageNodeKey(_selectedNodeRecord.NodeId, _selectedNodeRecord.NodeName),
                    null,
                    _selectedNodeRecord.BatchId));
            SelectMessageNodeFilter(GetMessageNodeKey(_selectedNodeRecord.NodeId, _selectedNodeRecord.NodeName));
            ApplyMessageFilter();
        }

        private void RefreshMessageFilter()
        {
            _messageBatchScope = null;
            UpdateMessageScopeText();
            _isUpdatingMessageFilters = true;
            try
            {
                MessageNodeFilter.Items.Clear();
                MessageNodeFilter.Items.Add(new ComboBoxItem
                {
                    Content = Properties.Resources.Flow_NodeAnalysis_All,
                    Tag = "All"
                });

                foreach (var node in _loadedMessages
                    .Where(item => !string.IsNullOrWhiteSpace(item.NodeId) || !string.IsNullOrWhiteSpace(item.NodeName))
                    .GroupBy(item => string.IsNullOrWhiteSpace(item.NodeId) ? $"name:{item.NodeName}" : item.NodeId)
                    .Select(group => group.First())
                    .OrderBy(item => item.NodeName, StringComparer.CurrentCulture))
                {
                    MessageNodeFilter.Items.Add(new ComboBoxItem
                    {
                        Content = string.IsNullOrWhiteSpace(node.NodeName) ? node.NodeId : node.NodeName,
                        Tag = string.IsNullOrWhiteSpace(node.NodeId) ? $"name:{node.NodeName}" : node.NodeId
                    });
                }

                MessageNodeFilter.SelectedIndex = 0;
                MessageStateFilter.SelectedIndex = 0;
            }
            finally
            {
                _isUpdatingMessageFilters = false;
            }

            ApplyMessageFilter();
        }

        private void ApplyMessageFilter()
        {
            // ComboBox.SelectionChanged can fire while InitializeComponent is still
            // creating the later controls in this tab. Do not touch sibling named
            // elements until the complete visual tree has been constructed.
            if (!_isComponentInitialized || _isUpdatingMessageFilters)
                return;

            IEnumerable<FlowNodeMessage> filtered = _loadedMessages;
            if (_messageBatchScope.HasValue)
                filtered = filtered.Where(item => item.BatchId == _messageBatchScope.Value);

            if (MessageNodeFilter.SelectedItem is ComboBoxItem nodeItem
                && nodeItem.Tag?.ToString() is string nodeKey
                && nodeKey != "All")
            {
                filtered = nodeKey.StartsWith("name:", StringComparison.Ordinal)
                    ? filtered.Where(item => $"name:{item.NodeName}" == nodeKey)
                    : filtered.Where(item => string.Equals(item.NodeId, nodeKey, StringComparison.Ordinal));
            }

            if (MessageStateFilter.SelectedItem is ComboBoxItem stateItem
                && stateItem.Tag?.ToString() != "All"
                && Enum.TryParse(stateItem.Content?.ToString(), out FlowMessageState state))
            {
                filtered = filtered.Where(item => item.State == state);
            }

            _nodeMessages.ResetWith(filtered);

            MessageDisplayCountText.Text = NodeMessages.Count.ToString();
            MessageTotalCountText.Text = _loadedMessages.Count.ToString();

            if (NodeMessages.Count > 0)
                MessageListView.SelectedIndex = 0;
            else
                ClearMessageDetails();
        }

        private void SelectMessageNodeFilter(string? nodeKey)
        {
            if (string.IsNullOrWhiteSpace(nodeKey))
                return;

            foreach (object item in MessageNodeFilter.Items)
            {
                if (item is ComboBoxItem comboBoxItem
                    && string.Equals(comboBoxItem.Tag?.ToString(), nodeKey, StringComparison.Ordinal))
                {
                    MessageNodeFilter.SelectedItem = comboBoxItem;
                    return;
                }
            }
        }

        private static string? GetMessageNodeKey(string? nodeId, string? nodeName)
        {
            if (!string.IsNullOrWhiteSpace(nodeId))
                return nodeId;
            return string.IsNullOrWhiteSpace(nodeName) ? null : $"name:{nodeName}";
        }

        private void MessageNodeFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyMessageFilter();
        }

        private void MessageStateFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyMessageFilter();
        }

        private void ResetMessageFilterButton_Click(object sender, RoutedEventArgs e)
        {
            _messageBatchScope = null;
            UpdateMessageScopeText();
            _isUpdatingMessageFilters = true;
            MessageNodeFilter.SelectedIndex = 0;
            MessageStateFilter.SelectedIndex = 0;
            _isUpdatingMessageFilters = false;
            ApplyMessageFilter();
        }

        private void MessageListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MessageListView.SelectedItem is not FlowNodeMessage message)
            {
                ClearMessageDetails();
                return;
            }

            SendPayloadTextBox.Text = FormatJsonSafe(message.SendPayload);
            RecvPayloadTextBox.Text = FormatJsonSafe(message.RecvPayload);
            SendTopicTextBlock.Text = string.IsNullOrWhiteSpace(message.SendTopic)
                ? "未记录发送 Topic"
                : message.SendTopic;
            RecvTopicTextBlock.Text = string.IsNullOrWhiteSpace(message.RecvTopic)
                ? "尚未收到响应或未记录接收 Topic"
                : message.RecvTopic;
        }

        private void ClearMessageDetails()
        {
            SendPayloadTextBox.Text = string.Empty;
            RecvPayloadTextBox.Text = string.Empty;
            SendTopicTextBlock.Text = "选择一条消息查看 Topic 与 Payload";
            RecvTopicTextBlock.Text = "选择一条消息查看 Topic 与 Payload";
        }

        private static string FormatJsonSafe(string json)
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

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_backStack.Count == 0)
                return;

            FlowAnalysisNavigationState current = CaptureNavigationState();
            FlowAnalysisNavigationState target = _backStack.Pop();
            _forwardStack.Push(current);
            ApplyNavigationState(target);
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_forwardStack.Count == 0)
                return;

            FlowAnalysisNavigationState current = CaptureNavigationState();
            FlowAnalysisNavigationState target = _forwardStack.Pop();
            _backStack.Push(current);
            ApplyNavigationState(target);
        }

        private void NavigateTo(FlowAnalysisNavigationState target, bool addHistory = true)
        {
            FlowAnalysisNavigationState current = CaptureNavigationState();
            if (addHistory && current != target)
            {
                _backStack.Push(current);
                _forwardStack.Clear();
            }

            ApplyNavigationState(target);
        }

        private FlowAnalysisNavigationState CaptureNavigationState()
        {
            FlowNodeMessage? selectedMessage = MessageListView.SelectedItem as FlowNodeMessage;
            bool isMessageTrace = AnalysisTabControl.SelectedIndex == MessageTraceTabIndex;
            string? nodeId = isMessageTrace
                ? GetMessageNodeKey(
                    selectedMessage?.NodeId ?? _selectedNodeRecord?.NodeId,
                    selectedMessage?.NodeName ?? _selectedNodeRecord?.NodeName)
                : _selectedNodeRecord?.NodeId;
            int? recordId = _selectedNodeRecord?.Id;
            if (isMessageTrace && selectedMessage != null)
            {
                recordId = NodeRecords
                    .Where(item => item.BatchId == selectedMessage.BatchId
                        && (!string.IsNullOrWhiteSpace(selectedMessage.NodeId)
                            ? string.Equals(item.NodeId, selectedMessage.NodeId, StringComparison.Ordinal)
                            : string.Equals(item.NodeName, selectedMessage.NodeName, StringComparison.Ordinal)))
                    .OrderByDescending(item => item.StartTime)
                    .Select(item => (int?)item.Id)
                    .FirstOrDefault();
            }
            return new FlowAnalysisNavigationState(
                AnalysisTabControl.SelectedIndex,
                recordId,
                nodeId,
                selectedMessage?.Id,
                isMessageTrace ? _messageBatchScope : null);
        }

        private void ApplyNavigationState(FlowAnalysisNavigationState state)
        {
            _isApplyingNavigation = true;
            try
            {
                FlowNodeRecord? record = FindLoadedNodeRecord(state.RecordId, state.NodeId, null);
                if (record != null)
                    SelectNode(record);

                AnalysisTabControl.SelectedIndex = Math.Clamp(state.TabIndex, 0, AnalysisTabControl.Items.Count - 1);
                _messageBatchScope = state.TabIndex == MessageTraceTabIndex ? state.MessageBatchId : null;
                UpdateMessageScopeText();

                FlowNodeMessage? message = null;
                if (state.MessageId.HasValue)
                {
                    message = _loadedMessages.FirstOrDefault(item => item.Id == state.MessageId.Value);
                    if (message != null)
                        SelectMessageNodeFilter(GetMessageNodeKey(message.NodeId, message.NodeName));
                }

                if (state.TabIndex == MessageTraceTabIndex)
                {
                    if (message == null)
                        SelectMessageNodeFilter(state.NodeId);
                }
                ApplyMessageFilter();

                if (message != null && NodeMessages.Contains(message))
                {
                    MessageListView.SelectedItem = message;
                    MessageListView.ScrollIntoView(message);
                }
            }
            finally
            {
                _isApplyingNavigation = false;
                UpdateNavigationButtons();
            }
        }

        private void AnalysisTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!ReferenceEquals(e.Source, AnalysisTabControl) || _isApplyingNavigation)
                return;

            if (AnalysisTabControl.SelectedItem == NodeDetailsTab && _selectedNodeRecord == null)
                NodeDetailEmptyText.Visibility = Visibility.Visible;
        }

        private void UpdateNavigationButtons()
        {
            BackButton.IsEnabled = _backStack.Count > 0;
            ForwardButton.IsEnabled = _forwardStack.Count > 0;
        }

        private FlowNodeRecord? FindLoadedNodeRecord(int? recordId, string? nodeId, int? batchId)
        {
            if (recordId.HasValue)
            {
                FlowNodeRecord? byRecordId = NodeRecords.FirstOrDefault(item => item.Id == recordId.Value);
                if (byRecordId != null)
                    return byRecordId;
            }

            if (!string.IsNullOrWhiteSpace(nodeId))
            {
                bool isNodeNameKey = nodeId.StartsWith("name:", StringComparison.Ordinal);
                string nodeKey = isNodeNameKey ? nodeId["name:".Length..] : nodeId;
                return NodeRecords
                    .Where(item => !batchId.HasValue || item.BatchId == batchId.Value)
                    .OrderByDescending(item => item.StartTime)
                    .FirstOrDefault(item => isNodeNameKey
                        ? string.Equals(item.NodeName, nodeKey, StringComparison.Ordinal)
                        : string.Equals(item.NodeId, nodeKey, StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(_initialNodeName))
            {
                return NodeRecords
                    .Where(item => !batchId.HasValue || item.BatchId == batchId.Value)
                    .OrderByDescending(item => item.StartTime)
                    .FirstOrDefault(item => string.Equals(item.NodeName, _initialNodeName, StringComparison.Ordinal));
            }

            return null;
        }

        private static bool IsSameRecord(FlowNodeRecord left, FlowNodeRecord right)
        {
            if (left.Id > 0 && right.Id > 0)
                return left.Id == right.Id;

            return left.BatchId == right.BatchId
                && left.StartTime == right.StartTime
                && string.Equals(left.NodeId, right.NodeId, StringComparison.Ordinal);
        }

        private List<int> GetSelectedBatchIds()
        {
            return BatchListView.SelectedItems.Cast<int>().Distinct().ToList();
        }

        private void SelectBatchIds(IEnumerable<int> batchIds)
        {
            var selection = new HashSet<int>(batchIds);
            BatchListView.SelectedItems.Clear();
            foreach (int batchId in BatchListView.Items.Cast<int>().Where(selection.Contains))
                BatchListView.SelectedItems.Add(batchId);
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (NodeRecords.Count == 0)
            {
                MessageBox.Show(
                    Properties.Resources.Flow_NodeAnalysis_NoDataToExport,
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            using var dialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv",
                FileName = $"{Properties.Resources.Flow_NodeAnalysis_FileNamePrefix}{DateTime.Now:yyyy-MM-dd_HH-mm-ss}",
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine(Properties.Resources.Flow_NodeAnalysis_CsvHeader);
            foreach (FlowNodeRecord record in NodeRecords)
            {
                csvBuilder.Append(record.BatchId).Append(',');
                csvBuilder.Append(CsvEscape(record.NodeName)).Append(',');
                csvBuilder.Append(CsvEscape(record.NodeType)).Append(',');
                csvBuilder.Append(record.StartTime.ToString("yyyy/MM/dd HH:mm:ss.fff")).Append(',');
                csvBuilder.Append(record.EndTime?.ToString("yyyy/MM/dd HH:mm:ss.fff") ?? string.Empty).Append(',');
                csvBuilder.Append(record.ElapsedMs).Append(',');
                csvBuilder.Append(CsvEscape(record.SerialNumber));
                csvBuilder.AppendLine();
            }

            if (_loadedMessages.Count > 0)
            {
                csvBuilder.AppendLine();
                csvBuilder.AppendLine(Properties.Resources.Flow_NodeAnalysis_MqttMessageTrace);
                csvBuilder.AppendLine(Properties.Resources.Flow_NodeAnalysis_MqttCsvHeader);
                foreach (FlowNodeMessage message in _loadedMessages)
                {
                    csvBuilder.Append(message.BatchId).Append(',');
                    csvBuilder.Append(CsvEscape(message.NodeName)).Append(',');
                    csvBuilder.Append(CsvEscape(message.NodeId)).Append(',');
                    csvBuilder.Append(CsvEscape(message.EventName)).Append(',');
                    csvBuilder.Append(CsvEscape(message.MsgId)).Append(',');
                    csvBuilder.Append(CsvEscape(message.SendTopic)).Append(',');
                    csvBuilder.Append(message.SendTime.ToString("yyyy/MM/dd HH:mm:ss.fff")).Append(',');
                    csvBuilder.Append(CsvEscape(message.RecvTopic)).Append(',');
                    csvBuilder.Append(message.RecvTime?.ToString("yyyy/MM/dd HH:mm:ss.fff") ?? string.Empty).Append(',');
                    csvBuilder.Append(message.ElapsedMs).Append(',');
                    csvBuilder.Append(message.StatusCode?.ToString() ?? string.Empty).Append(',');
                    csvBuilder.Append(CsvEscape(message.StatusMessage)).Append(',');
                    csvBuilder.Append(message.State);
                    csvBuilder.AppendLine();
                }
            }

            File.WriteAllText(dialog.FileName, csvBuilder.ToString(), new UTF8Encoding(true));
            MessageBox.Show(Properties.Resources.ExportSucceeded, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        private void RenderAnalysisChart(
            IReadOnlyList<FlowNodeRecord> records,
            IReadOnlyList<int> batchIds,
            DateTime now)
        {
            GanttPlot.Plot.Clear();
            GanttPlot.Plot.Legend.ManualItems.Clear();
            GanttPlot.Plot.Legend.IsVisible = false;

            if (records.Count == 0)
            {
                GanttPlot.Plot.Title(string.Empty);
                GanttPlot.Plot.XLabel(string.Empty);
                GanttPlot.Plot.YLabel(string.Empty);
                GanttPlot.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericAutomatic();
                GanttPlot.Plot.Axes.NumericTicksBottom();
                GanttPlot.Plot.Axes.SetLimits(0, 1, 0, 1);
                GanttPlot.Refresh();
                return;
            }

            SetupChineseFonts();
            if (batchIds.Count == 1)
                RenderExecutionTimeline(records, batchIds[0], now);
            else
                RenderNodeDurationComparison(records, batchIds, now);
            GanttPlot.Refresh();
        }

        private void SetupChineseFonts()
        {
            string chineseFont = ScottPlot.Fonts.Detect("中文");
            GanttPlot.Plot.Axes.Title.Label.FontName = chineseFont;
            GanttPlot.Plot.Axes.Left.Label.FontName = chineseFont;
            GanttPlot.Plot.Axes.Bottom.Label.FontName = chineseFont;
            GanttPlot.Plot.Axes.Left.TickLabelStyle.FontName = chineseFont;
            GanttPlot.Plot.Axes.Bottom.TickLabelStyle.FontName = chineseFont;
            GanttPlot.Plot.Legend.FontName = chineseFont;
        }

        private void RenderExecutionTimeline(
            IReadOnlyList<FlowNodeRecord> records,
            int batchId,
            DateTime now)
        {
            DateTime baseTime = records.Min(item => item.StartTime);
            double totalMs = records.Max(item => ((item.EndTime ?? now) - baseTime).TotalMilliseconds);
            totalMs = Math.Max(1, totalMs);

            ScottPlot.Color completeColor = ScottPlot.Color.FromHex("#4D8DFF");
            ScottPlot.Color runningColor = ScottPlot.Color.FromHex("#D99000");
            ScottPlot.Color slowColor = ScottPlot.Color.FromHex("#D84A4A");
            var bars = new List<ScottPlot.Bar>();
            var ticks = new List<ScottPlot.Tick>();
            var occurrenceCount = new Dictionary<string, int>(StringComparer.CurrentCulture);

            for (int index = 0; index < records.Count; index++)
            {
                FlowNodeRecord record = records[index];
                double startOffset = Math.Max(0, (record.StartTime - baseTime).TotalMilliseconds);
                double endOffset = Math.Max(startOffset, ((record.EndTime ?? now) - baseTime).TotalMilliseconds);
                long elapsedMs = FlowExecutionAnalysisPresentation.GetEffectiveElapsedMs(record, now);
                double yPosition = records.Count - 1 - index;
                ScottPlot.Color color = elapsedMs > SlowNodeThresholdMs
                    ? slowColor
                    : record.EndTime.HasValue
                        ? completeColor
                        : runningColor;

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
                occurrenceCount.TryGetValue(nodeName, out int occurrence);
                occurrence++;
                occurrenceCount[nodeName] = occurrence;
                string label = occurrence == 1 ? nodeName : $"{nodeName} #{occurrence}";
                ticks.Add(new ScottPlot.Tick(yPosition, label));
            }

            ScottPlot.Plottables.BarPlot barPlot = GanttPlot.Plot.Add.Bars(bars.ToArray());
            barPlot.Horizontal = true;
            GanttPlot.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks.ToArray());
            GanttPlot.Plot.Title($"执行时序 · Batch {batchId}");
            GanttPlot.Plot.XLabel(Properties.Resources.Flow_NodeAnalysis_XLabelTime);
            GanttPlot.Plot.YLabel(string.Empty);
            GanttPlot.Plot.Axes.AutoScale();
            GanttPlot.Plot.Axes.SetLimitsX(0, totalMs * 1.04);
            GanttPlot.Plot.Axes.Margins(left: 0, bottom: 0.08);
        }

        private void RenderNodeDurationComparison(
            IReadOnlyList<FlowNodeRecord> records,
            IReadOnlyList<int> batchIds,
            DateTime now)
        {
            List<IGrouping<string, FlowNodeRecord>> grouped = records
                .GroupBy(GetStableNodeKey)
                .OrderByDescending(group => group.Average(item =>
                    FlowExecutionAnalysisPresentation.GetEffectiveElapsedMs(item, now)))
                .ToList();
            if (grouped.Count == 0)
                return;

            ScottPlot.Color[] batchColors =
            {
                ScottPlot.Color.FromHex("#4D8DFF"),
                ScottPlot.Color.FromHex("#2E9D66"),
                ScottPlot.Color.FromHex("#D99000"),
                ScottPlot.Color.FromHex("#9C5CC5"),
                ScottPlot.Color.FromHex("#008FA3"),
                ScottPlot.Color.FromHex("#9A6A4F"),
                ScottPlot.Color.FromHex("#657886"),
                ScottPlot.Color.FromHex("#C64F83")
            };
            var bars = new List<ScottPlot.Bar>();
            var ticks = new List<ScottPlot.Tick>();
            double barHeight = 0.78 / batchIds.Count;
            double maximumElapsed = 1;

            for (int nodeIndex = 0; nodeIndex < grouped.Count; nodeIndex++)
            {
                IGrouping<string, FlowNodeRecord> group = grouped[nodeIndex];
                double yBase = grouped.Count - 1 - nodeIndex;
                string nodeName = group.Select(item => item.NodeName)
                    .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? "Unknown";
                ticks.Add(new ScottPlot.Tick(yBase, nodeName));

                for (int batchIndex = 0; batchIndex < batchIds.Count; batchIndex++)
                {
                    int batchId = batchIds[batchIndex];
                    long[] elapsedValues = group
                        .Where(item => item.BatchId == batchId)
                        .Select(item => FlowExecutionAnalysisPresentation.GetEffectiveElapsedMs(item, now))
                        .ToArray();
                    if (elapsedValues.Length == 0)
                        continue;

                    double elapsed = elapsedValues.Average();
                    maximumElapsed = Math.Max(maximumElapsed, elapsed);
                    double yPosition = yBase + (batchIndex - (batchIds.Count - 1) / 2d) * barHeight;
                    bars.Add(new ScottPlot.Bar
                    {
                        Position = yPosition,
                        ValueBase = 0,
                        Value = elapsed,
                        FillColor = batchColors[batchIndex % batchColors.Length],
                        IsVisible = true,
                        Orientation = ScottPlot.Orientation.Horizontal,
                        Size = barHeight * 0.84
                    });
                }
            }

            ScottPlot.Plottables.BarPlot barPlot = GanttPlot.Plot.Add.Bars(bars.ToArray());
            barPlot.Horizontal = true;
            GanttPlot.Plot.Axes.Left.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks.ToArray());
            GanttPlot.Plot.Title("所选批次节点耗时对比");
            GanttPlot.Plot.XLabel(Properties.Resources.Flow_NodeAnalysis_XLabelElapsed);
            GanttPlot.Plot.YLabel(string.Empty);

            for (int index = 0; index < batchIds.Count; index++)
            {
                GanttPlot.Plot.Legend.ManualItems.Add(new ScottPlot.LegendItem
                {
                    LabelText = $"Batch {batchIds[index]}",
                    FillColor = batchColors[index % batchColors.Length]
                });
            }
            GanttPlot.Plot.Legend.IsVisible = true;
            GanttPlot.Plot.Axes.AutoScale();
            GanttPlot.Plot.Axes.SetLimitsX(0, maximumElapsed * 1.08);
            GanttPlot.Plot.Axes.Margins(left: 0, bottom: 0.08);
        }

        private static string GetStableNodeKey(FlowNodeRecord record)
        {
            return !string.IsNullOrWhiteSpace(record.NodeId)
                ? $"id:{record.NodeId}"
                : $"name:{record.NodeName}|type:{record.NodeType}";
        }

        private static string FormatDuration(long milliseconds)
        {
            if (milliseconds < 1000)
                return $"{milliseconds:N0} ms";
            if (milliseconds < 60000)
                return $"{milliseconds / 1000d:N2} s";

            TimeSpan duration = TimeSpan.FromMilliseconds(milliseconds);
            return duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
                : $"{duration.Minutes}:{duration.Seconds:00}.{duration.Milliseconds / 100:0}";
        }

        private const int NodeDetailsTabIndex = 2;
        private const int MessageTraceTabIndex = 3;

        private void UpdateMessageScopeText()
        {
            MessageScopeText.Text = _messageBatchScope.HasValue
                ? $"仅显示 Batch {_messageBatchScope.Value} 的节点消息"
                : "当前所选批次";
        }

        private readonly record struct FlowAnalysisNavigationState(
            int TabIndex,
            int? RecordId,
            string? NodeId,
            int? MessageId,
            int? MessageBatchId);

        private sealed class BulkObservableCollection<T> : ObservableCollection<T>
        {
            public void ResetWith(IEnumerable<T> items)
            {
                Items.Clear();
                foreach (T item in items)
                    Items.Add(item);

                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
                OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }
    }
}
