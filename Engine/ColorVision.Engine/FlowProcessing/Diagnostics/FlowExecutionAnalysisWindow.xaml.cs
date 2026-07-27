using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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
        private FlowExecutionAnalysisSession? _session;
        private FlowAnalysisNavigationState? _currentState;
        private int _loadVersion;
        private bool _isClearingAnalysisRecords;

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

        internal FlowExecutionAnalysisWindow(
            string nodeId,
            string? nodeName,
            Func<FlowNodeRecord, bool>? focusFlowNode)
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
            this.ApplyCaption();
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            int loadVersion = ++_loadVersion;
            SetLoading(true);
            try
            {
                InitialRunSelection selection = await Task.Run(ResolveInitialRun);
                if (loadVersion != _loadVersion)
                    return;

                if (!selection.BatchId.HasValue)
                {
                    ShowEmptyPage(
                        "还没有流程执行记录",
                        "执行一次流程后，这里会显示节点耗时、执行时序和消息追踪。");
                    return;
                }

                await LoadRunAsync(
                    selection.BatchId.Value,
                    selection.SerialNumber,
                    selection.InitialRecordId);
            }
            finally
            {
                if (loadVersion == _loadVersion)
                    SetLoading(false);
            }
        }

        private InitialRunSelection ResolveInitialRun()
        {
            FlowNodeRecordDataBaseHelper.FlushPendingWrites(TimeSpan.FromSeconds(5));

            FlowNodeRecord? requestedNodeRecord = null;
            if (!string.IsNullOrWhiteSpace(_initialNodeId))
                requestedNodeRecord = FlowNodeRecordDataBaseHelper.GetLastByNodeId(_initialNodeId);

            if (_initialBatch?.Id > 0)
            {
                string serialNumber = _initialBatch.Name
                    ?? _initialBatch.Code
                    ?? requestedNodeRecord?.SerialNumber
                    ?? string.Empty;
                return new InitialRunSelection(
                    _initialBatch.Id,
                    serialNumber,
                    requestedNodeRecord?.Id);
            }

            FlowNodeRecord? runRecord =
                requestedNodeRecord ?? FlowNodeRecordDataBaseHelper.GetLatestRecord();
            return runRecord == null
                ? new InitialRunSelection(null, string.Empty, null)
                : new InitialRunSelection(
                    runRecord.BatchId,
                    runRecord.SerialNumber ?? string.Empty,
                    requestedNodeRecord?.Id);
        }

        private async Task LoadRunAsync(int batchId, string? serialNumber, int? preferredRecordId)
        {
            int loadVersion = ++_loadVersion;
            SetLoading(true);
            try
            {
                var result = await Task.Run(() =>
                {
                    bool flushed = FlowNodeRecordDataBaseHelper.FlushPendingWrites(TimeSpan.FromSeconds(5));
                    List<FlowNodeRecord> records =
                        FlowNodeRecordDataBaseHelper.GetByRun(batchId, serialNumber);
                    List<FlowNodeMessage> messages =
                        FlowNodeRecordDataBaseHelper.GetMessagesByRun(batchId, serialNumber);
                    if (string.IsNullOrWhiteSpace(serialNumber))
                    {
                        records = records
                            .Where(item => string.IsNullOrWhiteSpace(item.SerialNumber))
                            .ToList();
                        messages = messages
                            .Where(item => string.IsNullOrWhiteSpace(item.SerialNumber))
                            .ToList();
                    }
                    return (Flushed: flushed, Records: records, Messages: messages);
                });

                if (loadVersion != _loadVersion)
                    return;

                string effectiveSerial = !string.IsNullOrWhiteSpace(serialNumber)
                    ? serialNumber
                    : result.Records.FirstOrDefault()?.SerialNumber ?? string.Empty;
                MeasureBatchModel? batch = _initialBatch?.Id == batchId
                    && (string.IsNullOrWhiteSpace(effectiveSerial)
                        || string.Equals(_initialBatch.Name, effectiveSerial, StringComparison.Ordinal)
                        || string.Equals(_initialBatch.Code, effectiveSerial, StringComparison.Ordinal))
                    ? _initialBatch
                    : null;

                _session = new FlowExecutionAnalysisSession(
                    batchId,
                    effectiveSerial,
                    batch,
                    result.Records,
                    result.Messages,
                    DateTime.Now,
                    SlowNodeThresholdMs);

                _backStack.Clear();
                _forwardStack.Clear();
                _currentState = null;

                if (_session.Records.Count == 0)
                {
                    ShowEmptyPage(
                        $"Batch {batchId} 没有节点记录",
                        result.Flushed
                            ? "该批次可能来自旧版本，或没有执行到可记录节点。"
                            : "节点记录仍在写入，请稍后点击刷新。");
                    return;
                }

                FlowAnalysisNavigationState overviewState = new FlowAnalysisNavigationState(
                    FlowAnalysisPageKind.Overview,
                    _session.BatchId,
                    _session.SerialNumber);
                NavigateTo(overviewState, addHistory: false);

                FlowNodeRecord? preferredRecord = _session.FindRecord(preferredRecordId)
                    ?? FindInitialNodeRecord();
                if (preferredRecord != null)
                {
                    NavigateTo(
                        CreateNodeState(preferredRecord),
                        addHistory: true);
                }
            }
            finally
            {
                if (loadVersion == _loadVersion)
                    SetLoading(false);
            }
        }

        private FlowNodeRecord? FindInitialNodeRecord()
        {
            if (_session == null)
                return null;

            if (!string.IsNullOrWhiteSpace(_initialNodeId))
            {
                FlowNodeRecord? byId = _session.Records
                    .OrderByDescending(item => item.StartTime)
                    .FirstOrDefault(item =>
                        string.Equals(item.NodeId, _initialNodeId, StringComparison.Ordinal));
                if (byId != null)
                    return byId;
            }

            if (string.IsNullOrWhiteSpace(_initialNodeName))
                return null;

            return _session.Records
                .OrderByDescending(item => item.StartTime)
                .FirstOrDefault(item =>
                    string.Equals(item.NodeName, _initialNodeName, StringComparison.Ordinal));
        }

        private void NavigateTo(FlowAnalysisNavigationState target, bool addHistory = true)
        {
            if (_session == null
                || target.BatchId != _session.BatchId
                || !string.Equals(target.SerialNumber, _session.SerialNumber, StringComparison.Ordinal))
            {
                return;
            }

            if (addHistory
                && _currentState.HasValue
                && _currentState.Value != target)
            {
                _backStack.Push(_currentState.Value);
                _forwardStack.Clear();
            }

            _currentState = target;
            RenderCurrentPage();
            UpdateNavigationButtons();
        }

        private void RenderCurrentPage()
        {
            if (_session == null || !_currentState.HasValue)
                return;

            FlowAnalysisNavigationState state = _currentState.Value;
            switch (state.PageKind)
            {
                case FlowAnalysisPageKind.Overview:
                    AnalysisFrame.Content = new FlowExecutionOverviewPage(
                        _session,
                        record => NavigateTo(CreateNodeState(record)),
                        LocateFlowNode,
                        () => NavigateTo(CreateMessageState(null, null)),
                        ClearCurrentFlowRecords,
                        _focusFlowNode != null);
                    UpdateHeader("流程执行分析", "流程概览", BuildRunSubtitle(_session));
                    break;

                case FlowAnalysisPageKind.Node:
                    FlowNodeRecord? record = _session.FindRecord(state.RecordId);
                    if (record == null)
                    {
                        NavigateTo(
                            new FlowAnalysisNavigationState(
                                FlowAnalysisPageKind.Overview,
                                _session.BatchId,
                                _session.SerialNumber),
                            addHistory: false);
                        return;
                    }

                    AnalysisFrame.Content = new FlowNodeAnalysisPage(
                        _session,
                        record,
                        _focusFlowNode != null,
                        adjacent => NavigateTo(CreateNodeState(adjacent)),
                        NavigateToHistoryRecord,
                        LocateFlowNode,
                        (scope, messageId) => NavigateTo(CreateMessageState(scope, messageId)),
                        () => NavigateTo(
                            new FlowAnalysisNavigationState(
                                FlowAnalysisPageKind.Overview,
                                _session.BatchId,
                                _session.SerialNumber)),
                        ClearCurrentNodeRecords);
                    UpdateHeader(
                        string.IsNullOrWhiteSpace(record.NodeName) ? "节点分析" : record.NodeName,
                        "流程概览 / 节点分析",
                        BuildRunSubtitle(_session));
                    break;

                case FlowAnalysisPageKind.Messages:
                    FlowNodeRecord? scopeRecord = _session.FindRecord(state.RecordId);
                    AnalysisFrame.Content = new FlowMessageAnalysisPage(
                        _session,
                        scopeRecord,
                        state.MessageId,
                        linkedRecord => NavigateTo(CreateNodeState(linkedRecord)),
                        () => NavigateTo(
                            new FlowAnalysisNavigationState(
                                FlowAnalysisPageKind.Overview,
                                _session.BatchId,
                                _session.SerialNumber)));
                    string breadcrumb = scopeRecord == null
                        ? "流程概览 / 消息追踪"
                        : $"流程概览 / 节点分析 / 消息追踪";
                    string subtitle = scopeRecord == null
                        ? BuildRunSubtitle(_session)
                        : $"{scopeRecord.NodeName} · {BuildRunSubtitle(_session)}";
                    UpdateHeader("消息追踪", breadcrumb, subtitle);
                    break;
            }
        }

        private async void NavigateToHistoryRecord(FlowNodeRecord historyRecord)
        {
            if (_session != null
                && historyRecord.BatchId == _session.BatchId
                && string.Equals(
                    historyRecord.SerialNumber ?? string.Empty,
                    _session.SerialNumber,
                    StringComparison.Ordinal))
            {
                FlowNodeRecord? sessionRecord = _session.FindRecord(historyRecord.Id);
                if (sessionRecord != null)
                {
                    NavigateTo(CreateNodeState(sessionRecord));
                    return;
                }
            }

            try
            {
                await LoadRunAsync(
                    historyRecord.BatchId,
                    historyRecord.SerialNumber,
                    historyRecord.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"切换到该次执行失败：{ex.Message}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private FlowAnalysisNavigationState CreateNodeState(FlowNodeRecord record)
        {
            if (_session == null)
                return default;

            return new FlowAnalysisNavigationState(
                FlowAnalysisPageKind.Node,
                _session.BatchId,
                _session.SerialNumber,
                record.Id);
        }

        private FlowAnalysisNavigationState CreateMessageState(
            FlowNodeRecord? scopeRecord,
            int? messageId)
        {
            if (_session == null)
                return default;

            return new FlowAnalysisNavigationState(
                FlowAnalysisPageKind.Messages,
                _session.BatchId,
                _session.SerialNumber,
                scopeRecord?.Id,
                messageId);
        }

        private void LocateFlowNode(FlowNodeRecord record)
        {
            if (_focusFlowNode == null)
                return;

            if (!_focusFlowNode(record))
            {
                MessageBox.Show(
                    this,
                    "当前流程图中没有找到这个节点。历史记录可能来自另一个流程模板。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_backStack.Count == 0 || !_currentState.HasValue)
                return;

            _forwardStack.Push(_currentState.Value);
            _currentState = _backStack.Pop();
            RenderCurrentPage();
            UpdateNavigationButtons();
        }

        private void ForwardButton_Click(object sender, RoutedEventArgs e)
        {
            if (_forwardStack.Count == 0 || !_currentState.HasValue)
                return;

            _backStack.Push(_currentState.Value);
            _currentState = _forwardStack.Pop();
            RenderCurrentPage();
            UpdateNavigationButtons();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null)
            {
                Window_Initialized(sender, EventArgs.Empty);
                return;
            }

            FlowAnalysisNavigationState? previousState = _currentState;
            int? preferredRecordId = previousState?.PageKind == FlowAnalysisPageKind.Node
                ? previousState.Value.RecordId
                : null;
            await LoadRunAsync(_session.BatchId, _session.SerialNumber, preferredRecordId);

            if (_session == null || !previousState.HasValue)
                return;

            if (previousState.Value.PageKind == FlowAnalysisPageKind.Messages)
            {
                FlowNodeRecord? scope = _session.FindRecord(previousState.Value.RecordId);
                NavigateTo(
                    CreateMessageState(scope, previousState.Value.MessageId),
                    addHistory: false);
            }
        }

        private async void ClearCurrentFlowRecords()
        {
            if (_session == null || _isClearingAnalysisRecords)
                return;

            string[] nodeIds = _session.Records
                .Select(record => record.NodeId)
                .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (nodeIds.Length == 0)
            {
                MessageBox.Show(
                    this,
                    "当前流程没有可识别的节点标识，未执行清理。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirmation = MessageBox.Show(
                this,
                $"将清空当前流程中 {nodeIds.Length} 个节点的全部本地分析历史和消息记录。\n\n"
                    + "该操作不会删除测量结果或流程配置，但分析记录无法恢复。是否继续？",
                "确认清理当前流程分析记录",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes)
                return;

            int batchId = _session.BatchId;
            string serialNumber = _session.SerialNumber;
            await ExecuteClearAsync(
                () => FlowNodeRecordDataBaseHelper.DeleteAnalysisForNodeIds(nodeIds),
                "当前流程",
                async () => await LoadRunAsync(batchId, serialNumber, null));
        }

        private async void ClearCurrentNodeRecords(FlowNodeRecord record)
        {
            if (_isClearingAnalysisRecords)
                return;
            if (string.IsNullOrWhiteSpace(record.NodeId))
            {
                MessageBox.Show(
                    this,
                    "当前记录没有节点标识，为避免误删其他同名节点，未执行清理。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            string nodeName = string.IsNullOrWhiteSpace(record.NodeName)
                ? record.NodeId
                : record.NodeName;
            MessageBoxResult confirmation = MessageBox.Show(
                this,
                $"将清空节点“{nodeName}”的全部本地分析历史和消息记录。\n\n"
                    + "该操作不会删除测量结果或流程配置，但分析记录无法恢复。是否继续？",
                "确认清理当前节点分析记录",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes)
                return;

            int batchId = _session?.BatchId ?? record.BatchId;
            string serialNumber = _session?.SerialNumber ?? record.SerialNumber ?? string.Empty;
            string nodeId = record.NodeId;
            await ExecuteClearAsync(
                () => FlowNodeRecordDataBaseHelper.DeleteAnalysisForNodeId(nodeId),
                $"节点“{nodeName}”",
                async () => await LoadRunAsync(batchId, serialNumber, null));
        }

        private async void ClearAllRecordsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isClearingAnalysisRecords)
                return;

            MessageBoxResult confirmation = MessageBox.Show(
                this,
                "将清空本机流程分析数据库中的全部节点记录和消息记录，包含所有流程。\n\n"
                    + "该操作不会删除测量结果或流程配置，但分析记录无法恢复。是否继续？",
                "确认清空全部流程分析记录",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (confirmation != MessageBoxResult.Yes)
                return;

            await ExecuteClearAsync(
                FlowNodeRecordDataBaseHelper.DeleteAllAnalysis,
                "全部流程",
                () =>
                {
                    ShowEmptyPage(
                        "流程分析记录已清空",
                        "执行新的流程后，这里会重新开始记录节点耗时与消息。");
                    return Task.CompletedTask;
                });
        }

        private async Task ExecuteClearAsync(
            Func<FlowAnalysisDeleteResult> clearAction,
            string scopeName,
            Func<Task> refreshAction)
        {
            _isClearingAnalysisRecords = true;
            ClearAllRecordsButton.IsEnabled = false;
            SetLoading(true);
            try
            {
                FlowAnalysisDeleteResult result = await Task.Run(clearAction);
                await refreshAction();
                MessageBox.Show(
                    this,
                    $"{scopeName}分析记录已清理：节点记录 {result.RecordCount} 条，消息记录 {result.MessageCount} 条。",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"清理分析记录失败：{ex.Message}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                _isClearingAnalysisRecords = false;
                ClearAllRecordsButton.IsEnabled = true;
                SetLoading(false);
            }
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (_session == null || _session.Records.Count == 0)
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
                FileName = $"流程执行分析_Batch{_session.BatchId}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}",
                RestoreDirectory = true
            };
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            var csvBuilder = new StringBuilder();
            csvBuilder.AppendLine(Properties.Resources.Flow_NodeAnalysis_CsvHeader);
            foreach (FlowNodeRecord record in _session.Records)
            {
                FlowNodeDurationAnalysis? duration = _session.FindDuration(record);
                csvBuilder.Append(record.BatchId).Append(',');
                csvBuilder.Append(CsvEscape(record.NodeName)).Append(',');
                csvBuilder.Append(CsvEscape(record.NodeType)).Append(',');
                csvBuilder.Append(record.StartTime.ToString("yyyy/MM/dd HH:mm:ss.fff")).Append(',');
                csvBuilder.Append(record.EndTime?.ToString("yyyy/MM/dd HH:mm:ss.fff") ?? string.Empty).Append(',');
                csvBuilder.Append(duration?.IsTimedOut == true
                    ? string.Empty
                    : record.ElapsedMs.ToString()).Append(',');
                csvBuilder.Append(CsvEscape(record.SerialNumber));
                csvBuilder.AppendLine();
            }

            if (_session.Messages.Count > 0)
            {
                csvBuilder.AppendLine();
                csvBuilder.AppendLine(Properties.Resources.Flow_NodeAnalysis_MqttMessageTrace);
                csvBuilder.AppendLine(Properties.Resources.Flow_NodeAnalysis_MqttCsvHeader);
                foreach (FlowNodeMessage message in _session.Messages)
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
            MessageBox.Show(
                Properties.Resources.ExportSucceeded,
                "ColorVision",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        protected override void OnClosed(EventArgs e)
        {
            ++_loadVersion;
            AnalysisFrame.Content = null;
            base.OnClosed(e);
        }

        private void ShowEmptyPage(string title, string description)
        {
            _session = null;
            _currentState = null;
            _backStack.Clear();
            _forwardStack.Clear();
            UpdateNavigationButtons();
            UpdateHeader("流程执行分析", "空状态", description);

            AnalysisFrame.Content = new Page
            {
                Background = System.Windows.Media.Brushes.Transparent,
                Content = new Border
                {
                    Padding = new Thickness(24),
                    BorderThickness = new Thickness(1),
                    BorderBrush = TryFindResource("ButtonBorderBrush") as System.Windows.Media.Brush,
                    CornerRadius = new CornerRadius(8),
                    Child = new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = title,
                                FontSize = 22,
                                FontWeight = FontWeights.SemiBold,
                                HorizontalAlignment = HorizontalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = description,
                                Margin = new Thickness(0, 10, 0, 0),
                                TextWrapping = TextWrapping.Wrap,
                                HorizontalAlignment = HorizontalAlignment.Center
                            }
                        }
                    }
                }
            };
        }

        private void SetLoading(bool isLoading)
        {
            LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            Mouse.OverrideCursor = isLoading ? Cursors.Wait : null;
        }

        private void UpdateNavigationButtons()
        {
            BackButton.IsEnabled = _backStack.Count > 0;
            ForwardButton.IsEnabled = _forwardStack.Count > 0;
        }

        private void UpdateHeader(string title, string breadcrumb, string subtitle)
        {
            HeaderTitleText.Text = title;
            BreadcrumbText.Text = breadcrumb;
            HeaderSubtitleText.Text = subtitle;
        }

        private static string BuildRunSubtitle(FlowExecutionAnalysisSession session)
        {
            string serialText = string.IsNullOrWhiteSpace(session.SerialNumber)
                ? string.Empty
                : $" · SN {session.SerialNumber}";
            return $"Batch {session.BatchId} · {session.Records.Count} 次节点执行{serialText}";
        }

        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }

        private readonly record struct InitialRunSelection(
            int? BatchId,
            string SerialNumber,
            int? InitialRecordId);
    }
}
