using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal partial class FlowMessageAnalysisPage : Page
    {
        private const int MaximumDisplayedMessages = 2000;

        private readonly FlowExecutionAnalysisSession _session;
        private readonly FlowNodeRecord? _scopeRecord;
        private readonly Action<FlowNodeRecord> _openNode;
        private readonly Action _showOverview;
        private readonly IReadOnlyList<FlowNodeMessage> _sourceMessages;
        private int? _requestedMessageId;
        private bool _isInitializingFilters;
        private FlowNodeRecord? _selectedRecord;

        internal FlowMessageAnalysisPage(
            FlowExecutionAnalysisSession session,
            FlowNodeRecord? scopeRecord,
            int? selectedMessageId,
            Action<FlowNodeRecord> openNode,
            Action showOverview)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _scopeRecord = scopeRecord;
            _requestedMessageId = selectedMessageId;
            _openNode = openNode ?? throw new ArgumentNullException(nameof(openNode));
            _showOverview = showOverview ?? throw new ArgumentNullException(nameof(showOverview));
            _sourceMessages = scopeRecord == null
                ? session.Messages
                : session.GetMessages(scopeRecord);

            InitializeComponent();
            ConfigureScope();
            InitializeFilters();
            ApplyFilter();
        }

        private bool IsNodeScoped => _scopeRecord != null;

        private void ConfigureScope()
        {
            string serialNumber = string.IsNullOrWhiteSpace(_session.SerialNumber)
                ? "—"
                : _session.SerialNumber;
            if (_scopeRecord == null)
            {
                PageScopeText.Text = $"Batch {_session.BatchId} · SN {serialNumber} · 本次运行的全部消息";
                NodeScopePanel.Visibility = Visibility.Collapsed;
                OverviewFilterPanel.Visibility = Visibility.Visible;
                TotalMetricLabelText.Text = "本运行消息";
                FilteredMetricLabelText.Text = "当前匹配";
                return;
            }

            string nodeName = GetNodeDisplayName(_scopeRecord);
            PageScopeText.Text =
                $"Batch {_scopeRecord.BatchId} · {nodeName} · {_scopeRecord.StartTime:yyyy/MM/dd HH:mm:ss.fff}";
            NodeScopeDescriptionText.Text =
                $"仅显示“{nodeName}”在 {_scopeRecord.StartTime:HH:mm:ss.fff} 开始的这一次执行所关联的消息。" +
                "作用域由节点执行记录确定，不会混入同名节点的其他批次或其他执行。";
            NodeScopePanel.Visibility = Visibility.Visible;
            OverviewFilterPanel.Visibility = Visibility.Collapsed;
            TotalMetricLabelText.Text = "本次节点执行";
            FilteredMetricLabelText.Text = "固定范围消息";
        }

        private void InitializeFilters()
        {
            _isInitializingFilters = true;
            try
            {
                var nodeOptions = new List<NodeFilterOption>
                {
                    new NodeFilterOption(null, "全部节点")
                };
                nodeOptions.AddRange(_sourceMessages
                    .GroupBy(GetNodeKey, StringComparer.Ordinal)
                    .Select(group =>
                    {
                        FlowNodeMessage first = group.First();
                        string nodeName = GetNodeDisplayName(first);
                        string nodeId = string.IsNullOrWhiteSpace(first.NodeId)
                            ? string.Empty
                            : $" · {first.NodeId}";
                        return new NodeFilterOption(
                            group.Key,
                            $"{nodeName}{nodeId}（{group.Count():N0}）");
                    })
                    .OrderBy(option => option.Label, StringComparer.CurrentCulture));

                NodeFilterComboBox.ItemsSource = nodeOptions;
                NodeFilterComboBox.SelectedIndex = 0;

                StateFilterComboBox.ItemsSource = new[]
                {
                    new StateFilterOption(null, "全部状态"),
                    new StateFilterOption(FlowMessageState.Initial, "初始"),
                    new StateFilterOption(FlowMessageState.Sent, "已发送"),
                    new StateFilterOption(FlowMessageState.Success, "成功"),
                    new StateFilterOption(FlowMessageState.Fail, "失败"),
                    new StateFilterOption(FlowMessageState.Timeout, "超时"),
                    new StateFilterOption(FlowMessageState.Canceled, "已取消")
                };
                StateFilterComboBox.SelectedIndex = 0;
            }
            finally
            {
                _isInitializingFilters = false;
            }
        }

        private void ApplyFilter()
        {
            FlowNodeMessage? currentSelection = MessageListView.SelectedItem as FlowNodeMessage;
            IEnumerable<FlowNodeMessage> filtered = _sourceMessages;

            if (!IsNodeScoped)
            {
                if (NodeFilterComboBox.SelectedItem is NodeFilterOption nodeOption &&
                    nodeOption.Key != null)
                {
                    filtered = filtered.Where(message =>
                        string.Equals(GetNodeKey(message), nodeOption.Key, StringComparison.Ordinal));
                }

                if (StateFilterComboBox.SelectedItem is StateFilterOption stateOption &&
                    stateOption.State.HasValue)
                {
                    filtered = filtered.Where(message => message.State == stateOption.State.Value);
                }
            }

            List<FlowNodeMessage> matchingMessages = filtered.ToList();
            FlowNodeMessage? preferredSelection = FindPreferredSelection(
                matchingMessages,
                currentSelection,
                _requestedMessageId);
            _requestedMessageId = null;

            List<FlowNodeMessage> displayedMessages = matchingMessages
                .Take(MaximumDisplayedMessages)
                .ToList();
            if (preferredSelection != null &&
                displayedMessages.Count == MaximumDisplayedMessages &&
                !displayedMessages.Contains(preferredSelection))
            {
                displayedMessages[displayedMessages.Count - 1] = preferredSelection;
            }

            MessageListView.ItemsSource = displayedMessages;
            UpdateStatistics(matchingMessages.Count, displayedMessages.Count);
            UpdateEmptyState(matchingMessages.Count);

            if (displayedMessages.Count == 0)
            {
                MessageListView.SelectedItem = null;
                UpdateMessageDetails(null);
                return;
            }

            MessageListView.SelectedItem = preferredSelection != null &&
                                           displayedMessages.Contains(preferredSelection)
                ? preferredSelection
                : displayedMessages[0];
            MessageListView.ScrollIntoView(MessageListView.SelectedItem);
        }

        private void UpdateStatistics(int matchingCount, int displayedCount)
        {
            int issueCount = _sourceMessages.Count(message =>
                message.State == FlowMessageState.Fail ||
                message.State == FlowMessageState.Timeout);

            TotalMessageCountText.Text = _sourceMessages.Count.ToString("N0");
            IssueMessageCountText.Text = issueCount.ToString("N0");
            FilteredMessageCountText.Text = matchingCount.ToString("N0");
            DisplayedMessageCountText.Text = displayedCount.ToString("N0");
            ListScopeSummaryText.Text = matchingCount == _sourceMessages.Count
                ? $"{matchingCount:N0} 条"
                : $"{matchingCount:N0} / {_sourceMessages.Count:N0} 条";

            bool isLimited = matchingCount > MaximumDisplayedMessages;
            LimitNoticePanel.Visibility = isLimited ? Visibility.Visible : Visibility.Collapsed;
            LimitNoticeText.Text = isLimited
                ? $"为保证界面流畅，列表最多显示 {MaximumDisplayedMessages:N0} 条。" +
                  $"当前筛选完整匹配 {matchingCount:N0} 条，上方统计仍基于完整数据。"
                : string.Empty;
        }

        private void UpdateEmptyState(int matchingCount)
        {
            bool isEmpty = matchingCount == 0;
            EmptyStatePanel.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
            MessageListView.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
            if (!isEmpty)
                return;

            if (_sourceMessages.Count == 0 && _scopeRecord != null)
            {
                EmptyStateTitleText.Text = "本次节点执行没有消息记录";
                EmptyStateDescriptionText.Text =
                    "该节点在这个 Batch 的这一次执行中没有匹配到 MQTT 消息。" +
                    "这通常表示节点未发送消息，或执行时消息追踪尚未启用。";
            }
            else if (_sourceMessages.Count == 0)
            {
                EmptyStateTitleText.Text = "本次运行没有消息记录";
                EmptyStateDescriptionText.Text =
                    "当前 Batch 没有记录 MQTT 消息。流程仍可能正常完成；也可能是本次执行未启用消息追踪。";
            }
            else
            {
                EmptyStateTitleText.Text = "当前筛选没有匹配消息";
                EmptyStateDescriptionText.Text = "完整消息仍然存在，请调整节点或状态筛选，或点击“重置筛选”。";
            }
        }

        private void UpdateMessageDetails(FlowNodeMessage? message)
        {
            bool hasMessage = message != null;
            MessageDetailsPanel.Visibility = hasMessage ? Visibility.Visible : Visibility.Collapsed;
            NoSelectionPanel.Visibility = hasMessage ? Visibility.Collapsed : Visibility.Visible;

            if (message == null)
            {
                _selectedRecord = _scopeRecord;
                OpenNodeButton.IsEnabled = _selectedRecord != null;
                OpenNodeButton.ToolTip = _selectedRecord != null
                    ? "打开当前固定范围对应的节点执行分析"
                    : "请先选择一条能够关联到节点执行的消息";
                return;
            }

            _selectedRecord = ResolveRecord(message);
            OpenNodeButton.IsEnabled = _selectedRecord != null;
            OpenNodeButton.ToolTip = _selectedRecord != null
                ? "打开当前消息所属的节点执行分析"
                : "这条消息没有可定位的节点执行记录";

            string eventName = string.IsNullOrWhiteSpace(message.EventName)
                ? "MQTT 消息"
                : message.EventName;
            string nodeName = GetNodeDisplayName(message);
            string elapsed = message.ElapsedMs >= 0
                ? FlowExecutionAnalysisPresentation.FormatDuration(message.ElapsedMs)
                : "等待接收";
            SelectedMessageTitleText.Text = eventName;
            SelectedMessageMetaText.Text =
                $"{nodeName} · {message.SendTime:yyyy/MM/dd HH:mm:ss.fff} · {GetStateDisplayText(message.State)} · {elapsed}";

            var statusParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(message.MsgId))
                statusParts.Add($"MsgId {message.MsgId}");
            if (message.StatusCode.HasValue)
                statusParts.Add($"状态码 {message.StatusCode.Value}");
            if (!string.IsNullOrWhiteSpace(message.StatusMessage))
                statusParts.Add(message.StatusMessage);
            SelectedMessageStatusText.Text = statusParts.Count == 0
                ? "未记录额外状态信息"
                : string.Join(" · ", statusParts);

            SendTopicTextBox.Text = string.IsNullOrWhiteSpace(message.SendTopic)
                ? "（未记录发送 Topic）"
                : message.SendTopic;
            SendPayloadTextBox.Text = FormatPayload(message.SendPayload, "（无发送 Payload）");
            ReceiveTopicTextBox.Text = string.IsNullOrWhiteSpace(message.RecvTopic)
                ? "（尚未记录接收 Topic）"
                : message.RecvTopic;
            ReceivePayloadTextBox.Text = FormatPayload(message.RecvPayload, "（尚未收到 Payload）");
        }

        private FlowNodeRecord? ResolveRecord(FlowNodeMessage message)
        {
            if (_scopeRecord != null)
                return _scopeRecord;

            if (message.NodeRecordId.HasValue)
            {
                FlowNodeRecord? exactRecord = _session.FindRecord(message.NodeRecordId);
                if (exactRecord != null)
                    return exactRecord;
            }

            foreach (FlowNodeRecord record in _session.Records.Where(record =>
                         record.BatchId == message.BatchId &&
                         IsSameNode(record, message)))
            {
                if (_session.GetMessages(record).Any(candidate => IsSameMessage(candidate, message)))
                    return record;
            }

            return null;
        }

        private static FlowNodeMessage? FindPreferredSelection(
            IReadOnlyList<FlowNodeMessage> messages,
            FlowNodeMessage? currentSelection,
            int? requestedMessageId)
        {
            if (requestedMessageId.HasValue)
            {
                FlowNodeMessage? requested = messages.FirstOrDefault(message =>
                    message.Id == requestedMessageId.Value);
                if (requested != null)
                    return requested;
            }

            return currentSelection != null && messages.Contains(currentSelection)
                ? currentSelection
                : null;
        }

        private static bool IsSameNode(FlowNodeRecord record, FlowNodeMessage message)
        {
            if (!string.IsNullOrWhiteSpace(record.NodeId) &&
                !string.IsNullOrWhiteSpace(message.NodeId))
            {
                return string.Equals(record.NodeId, message.NodeId, StringComparison.Ordinal);
            }

            return string.Equals(record.NodeName, message.NodeName, StringComparison.Ordinal);
        }

        private static bool IsSameMessage(FlowNodeMessage left, FlowNodeMessage right)
        {
            if (left.Id > 0 && right.Id > 0)
                return left.Id == right.Id;

            return ReferenceEquals(left, right) ||
                   left.SendTime == right.SendTime &&
                   string.Equals(left.MsgId, right.MsgId, StringComparison.Ordinal) &&
                   string.Equals(left.NodeId, right.NodeId, StringComparison.Ordinal);
        }

        private static string GetNodeKey(FlowNodeMessage message)
        {
            if (!string.IsNullOrWhiteSpace(message.NodeId))
                return $"id:{message.NodeId}";
            if (!string.IsNullOrWhiteSpace(message.NodeName))
                return $"name:{message.NodeName}";
            return "unknown";
        }

        private static string GetNodeDisplayName(FlowNodeMessage message)
        {
            if (!string.IsNullOrWhiteSpace(message.NodeName))
                return message.NodeName;
            if (!string.IsNullOrWhiteSpace(message.NodeId))
                return message.NodeId;
            return "未知节点";
        }

        private static string GetNodeDisplayName(FlowNodeRecord record)
        {
            if (!string.IsNullOrWhiteSpace(record.NodeName))
                return record.NodeName;
            if (!string.IsNullOrWhiteSpace(record.NodeId))
                return record.NodeId;
            return "未知节点";
        }

        private static string GetStateDisplayText(FlowMessageState state)
        {
            return state switch
            {
                FlowMessageState.Initial => "初始",
                FlowMessageState.Sent => "已发送",
                FlowMessageState.Success => "成功",
                FlowMessageState.Fail => "失败",
                FlowMessageState.Timeout => "超时",
                FlowMessageState.Canceled => "已取消",
                _ => state.ToString()
            };
        }

        private static string FormatPayload(string? payload, string emptyText)
        {
            if (string.IsNullOrWhiteSpace(payload))
                return emptyText;

            try
            {
                object? value = JsonConvert.DeserializeObject(payload);
                return value == null
                    ? payload
                    : JsonConvert.SerializeObject(value, Formatting.Indented);
            }
            catch (JsonException)
            {
                return payload;
            }
        }

        private void FilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitializingFilters)
                ApplyFilter();
        }

        private void ResetFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            _isInitializingFilters = true;
            try
            {
                NodeFilterComboBox.SelectedIndex = 0;
                StateFilterComboBox.SelectedIndex = 0;
            }
            finally
            {
                _isInitializingFilters = false;
            }
            ApplyFilter();
        }

        private void MessageListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateMessageDetails(MessageListView.SelectedItem as FlowNodeMessage);
        }

        private void MessageListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_selectedRecord != null)
                _openNode(_selectedRecord);
        }

        private void OpenNodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRecord != null)
                _openNode(_selectedRecord);
        }

        private void ShowOverviewButton_Click(object sender, RoutedEventArgs e)
        {
            _showOverview();
        }

        private sealed record NodeFilterOption(string? Key, string Label);

        private sealed record StateFilterOption(FlowMessageState? State, string Label);
    }
}
