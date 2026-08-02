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
    internal readonly record struct FlowRunNavigationItem(
        int BatchId,
        string SerialNumber,
        DateTime ExecutedTime);

    public partial class FlowExecutionAnalysisWindow : Window
    {
        private const long SlowNodeThresholdMs = 30000;
        private readonly MeasureBatchModel? _initialBatch;
        private readonly int? _initialBatchId;
        private readonly string? _initialSerialNumber;
        private readonly string? _initialNodeId;
        private readonly string? _initialNodeName;
        private readonly FlowIdentity? _initialFlowIdentity;
        private readonly Func<FlowNodeRecord, bool>? _focusFlowNode;
        private IReadOnlyList<FlowRunNavigationItem> _allRuns = Array.Empty<FlowRunNavigationItem>();
        private IReadOnlyList<FlowRunNavigationItem> _sameFlowRuns = Array.Empty<FlowRunNavigationItem>();
        private FlowExecutionAnalysisSession? _session;
        private FlowAnalysisNavigationState? _currentState;
        private int _currentAllRunIndex = -1;
        private int _currentSameFlowRunIndex = -1;
        private int _loadVersion;
        private bool _isClearingAnalysisRecords;
        private bool _isLoading;

        public FlowExecutionAnalysisWindow()
            : this(null, null, null, null, null, null, null)
        {
        }

        public FlowExecutionAnalysisWindow(MeasureBatchModel batch)
            : this(batch, null, null, null, null, null, null)
        {
        }

        internal FlowExecutionAnalysisWindow(Func<FlowNodeRecord, bool> focusFlowNode)
            : this(null, null, null, null, null, null, focusFlowNode)
        {
        }

        internal FlowExecutionAnalysisWindow(MeasureBatchModel batch, Func<FlowNodeRecord, bool> focusFlowNode)
            : this(batch, null, null, null, null, null, focusFlowNode)
        {
        }

        internal FlowExecutionAnalysisWindow(
            FlowIdentity flowIdentity,
            Func<FlowNodeRecord, bool> focusFlowNode)
            : this(null, null, null, null, null, flowIdentity, focusFlowNode)
        {
        }

        internal FlowExecutionAnalysisWindow(
            string nodeId,
            string? nodeName,
            Func<FlowNodeRecord, bool>? focusFlowNode)
            : this(null, null, null, nodeId, nodeName, null, focusFlowNode)
        {
        }

        internal FlowExecutionAnalysisWindow(
            int batchId,
            string? serialNumber,
            string? nodeId)
            : this(
                null,
                batchId,
                serialNumber,
                nodeId,
                null,
                null,
                null)
        {
        }

        private FlowExecutionAnalysisWindow(
            MeasureBatchModel? batch,
            int? batchId,
            string? serialNumber,
            string? initialNodeId,
            string? initialNodeName,
            FlowIdentity? initialFlowIdentity,
            Func<FlowNodeRecord, bool>? focusFlowNode)
        {
            _initialBatch = batch;
            _initialBatchId = batchId;
            _initialSerialNumber = serialNumber;
            _initialNodeId = initialNodeId;
            _initialNodeName = initialNodeName;
            _initialFlowIdentity = initialFlowIdentity;
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
                    selection.InitialRecordId,
                    useInitialNodeFallback: true);
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
                string serialNumber = ResolveBatchSerialNumber(_initialBatch, requestedNodeRecord);
                return new InitialRunSelection(
                    _initialBatch.Id,
                    serialNumber,
                    requestedNodeRecord?.Id);
            }

            if (_initialBatchId is > 0)
            {
                return new InitialRunSelection(
                    _initialBatchId,
                    _initialSerialNumber
                        ?? requestedNodeRecord?.SerialNumber
                        ?? string.Empty,
                    requestedNodeRecord?.BatchId == _initialBatchId
                        ? requestedNodeRecord.Id
                        : null);
            }

            if (_initialFlowIdentity is FlowIdentity flowIdentity)
            {
                FlowRunRecord? flowRun =
                    FlowNodeRecordDataBaseHelper.GetLatestFlowRun(
                        flowIdentity);
                return flowRun?.BatchId is > 0
                    ? new InitialRunSelection(
                        flowRun.BatchId,
                        flowRun.SerialNumber ?? string.Empty,
                        null)
                    : new InitialRunSelection(null, string.Empty, null);
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

        internal static string ResolveBatchSerialNumber(
            MeasureBatchModel batch,
            FlowNodeRecord? fallbackRecord = null)
        {
            return batch.Code
                ?? batch.Name
                ?? fallbackRecord?.SerialNumber
                ?? string.Empty;
        }

        private async Task LoadRunAsync(
            int batchId,
            string? serialNumber,
            int? preferredRecordId,
            bool useInitialNodeFallback = false)
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
                    List<int> recentBatchIds =
                        FlowNodeRecordDataBaseHelper.GetDistinctBatchIds(500);
                    List<FlowNodeRecord> recentRecords =
                        FlowNodeRecordDataBaseHelper.GetByBatchIds(recentBatchIds);
                    FlowRunRecord? run =
                        FlowNodeRecordDataBaseHelper.GetFlowRun(batchId, serialNumber);
                    List<FlowExecutionEvent> events = run == null
                        ? new List<FlowExecutionEvent>()
                        : FlowNodeRecordDataBaseHelper.GetExecutionEvents(run.Id);
                    if (string.IsNullOrWhiteSpace(serialNumber))
                    {
                        records = records
                            .Where(item => string.IsNullOrWhiteSpace(item.SerialNumber))
                            .ToList();
                        messages = messages
                            .Where(item => string.IsNullOrWhiteSpace(item.SerialNumber))
                            .ToList();
                    }
                    string effectiveSerialNumber = NormalizeRunSerialNumber(
                        !string.IsNullOrWhiteSpace(serialNumber)
                            ? serialNumber
                            : records.FirstOrDefault()?.SerialNumber);
                    IReadOnlyList<FlowRunNavigationItem> sameFlowRuns =
                        LoadSameFlowRunOrder(effectiveSerialNumber, records);
                    IReadOnlyList<FlowRunNavigationItem> allRuns = BuildFlowRunOrder(
                        recentRecords
                            .Concat(records)
                            .GroupBy(record => record.Id)
                            .Select(group => group.First()));
                    return (
                        Flushed: flushed,
                        Records: records,
                        Messages: messages,
                        AllRuns: allRuns,
                        SameFlowRuns: sameFlowRuns,
                        EffectiveSerialNumber: effectiveSerialNumber,
                        Run: run,
                        Events: events);
                });

                if (loadVersion != _loadVersion)
                    return;

                string effectiveSerial = result.EffectiveSerialNumber;
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
                    result.Run,
                    result.Records,
                    result.Messages,
                    result.Events,
                    DateTime.Now,
                    SlowNodeThresholdMs);

                _allRuns = result.AllRuns;
                _currentAllRunIndex = FindCurrentRunIndex(
                    _allRuns,
                    batchId,
                    effectiveSerial);
                _sameFlowRuns = result.SameFlowRuns;
                _currentSameFlowRunIndex = FindCurrentRunIndex(
                    _sameFlowRuns,
                    batchId,
                    effectiveSerial);
                _currentState = null;
                UpdateNavigationButtons();

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
                NavigateTo(overviewState);

                FlowNodeRecord? preferredRecord = _session.FindRecord(preferredRecordId)
                    ?? (useInitialNodeFallback ? FindInitialNodeRecord() : null);
                if (preferredRecord != null)
                {
                    NavigateTo(CreateNodeState(preferredRecord));
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

        private void NavigateTo(FlowAnalysisNavigationState target)
        {
            if (_session == null
                || target.BatchId != _session.BatchId
                || !string.Equals(target.SerialNumber, _session.SerialNumber, StringComparison.Ordinal))
            {
                return;
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
                                _session.SerialNumber));
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

        private async void PreviousSameFlowRunButton_Click(object sender, RoutedEventArgs e)
        {
            await SwitchSameFlowRunAsync(-1);
        }

        private async void NextSameFlowRunButton_Click(object sender, RoutedEventArgs e)
        {
            await SwitchSameFlowRunAsync(1);
        }

        private async Task SwitchSameFlowRunAsync(int direction)
        {
            if (_session == null || _isLoading)
                return;

            int targetIndex = FindAdjacentRunIndex(
                _sameFlowRuns,
                _session.BatchId,
                _session.SerialNumber,
                direction);
            if (targetIndex < 0)
                return;

            FlowRunNavigationItem targetRun = _sameFlowRuns[targetIndex];
            try
            {
                await LoadRunAsync(targetRun.BatchId, targetRun.SerialNumber, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"切换相同流程执行记录失败：{ex.Message}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private async void PreviousRunButton_Click(object sender, RoutedEventArgs e)
        {
            await SwitchFlowRunAsync(-1);
        }

        private async void NextRunButton_Click(object sender, RoutedEventArgs e)
        {
            await SwitchFlowRunAsync(1);
        }

        private async Task SwitchFlowRunAsync(int direction)
        {
            if (_session == null || _isLoading)
                return;

            int targetIndex = FindAdjacentRunIndex(
                _allRuns,
                _session.BatchId,
                _session.SerialNumber,
                direction);
            if (targetIndex < 0)
                return;

            FlowRunNavigationItem targetRun = _allRuns[targetIndex];
            try
            {
                await LoadRunAsync(targetRun.BatchId, targetRun.SerialNumber, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"切换流程执行记录失败：{ex.Message}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
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
                    CreateMessageState(scope, previousState.Value.MessageId));
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
            _allRuns = Array.Empty<FlowRunNavigationItem>();
            _sameFlowRuns = Array.Empty<FlowRunNavigationItem>();
            _currentAllRunIndex = -1;
            _currentSameFlowRunIndex = -1;
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
            _isLoading = isLoading;
            LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            Mouse.OverrideCursor = isLoading ? Cursors.Wait : null;
            UpdateNavigationButtons();
        }

        private void UpdateNavigationButtons()
        {
            bool canNavigateAllRuns = !_isLoading && _session != null && _currentAllRunIndex >= 0;
            PreviousRunButton.IsEnabled = canNavigateAllRuns && _currentAllRunIndex > 0;
            NextRunButton.IsEnabled = canNavigateAllRuns && _currentAllRunIndex + 1 < _allRuns.Count;
            PreviousRunButton.ToolTip = PreviousRunButton.IsEnabled
                ? $"上一次流程执行：{FormatRunLabel(_allRuns[_currentAllRunIndex - 1])}"
                : "没有上一次流程执行";
            NextRunButton.ToolTip = NextRunButton.IsEnabled
                ? $"下一次流程执行：{FormatRunLabel(_allRuns[_currentAllRunIndex + 1])}"
                : "没有下一次流程执行";

            bool canNavigateSameFlow =
                !_isLoading && _session != null && _currentSameFlowRunIndex >= 0;
            PreviousSameFlowRunButton.IsEnabled =
                canNavigateSameFlow && _currentSameFlowRunIndex > 0;
            NextSameFlowRunButton.IsEnabled =
                canNavigateSameFlow && _currentSameFlowRunIndex + 1 < _sameFlowRuns.Count;
            PreviousSameFlowRunButton.ToolTip = PreviousSameFlowRunButton.IsEnabled
                ? $"上次相同流程执行：{FormatRunLabel(_sameFlowRuns[_currentSameFlowRunIndex - 1])}"
                : "没有上次相同流程执行";
            NextSameFlowRunButton.ToolTip = NextSameFlowRunButton.IsEnabled
                ? $"下次相同流程执行：{FormatRunLabel(_sameFlowRuns[_currentSameFlowRunIndex + 1])}"
                : "没有下次相同流程执行";
        }

        private static IReadOnlyList<FlowRunNavigationItem> LoadSameFlowRunOrder(
            string serialNumber,
            List<FlowNodeRecord> currentRecords)
        {
            List<FlowRunRecord> flowRuns =
                FlowNodeRecordDataBaseHelper.GetSameFlowRuns(serialNumber);
            if (flowRuns.Count > 0)
            {
                List<FlowNodeRecord> nodeRecords =
                    FlowNodeRecordDataBaseHelper.GetBySerialNumbers(
                        flowRuns.Select(run => run.SerialNumber ?? string.Empty));
                IReadOnlyList<FlowRunNavigationItem> exactRuns =
                    BuildSameFlowRunOrder(flowRuns, nodeRecords);
                int currentBatchId = currentRecords.Count > 0
                    ? currentRecords[0].BatchId
                    : 0;
                if (FindCurrentRunIndex(
                        exactRuns,
                        currentBatchId,
                        serialNumber) >= 0)
                {
                    return exactRuns;
                }
            }

            string[] nodeIds = currentRecords
                .Select(record => record.NodeId)
                .Where(nodeId => !string.IsNullOrWhiteSpace(nodeId))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return BuildFlowRunOrder(
                FlowNodeRecordDataBaseHelper.GetRecentByNodeIds(nodeIds));
        }

        internal static IReadOnlyList<FlowRunNavigationItem> BuildSameFlowRunOrder(
            IEnumerable<FlowRunRecord> flowRuns,
            IEnumerable<FlowNodeRecord> nodeRecords)
        {
            if (flowRuns == null || nodeRecords == null)
                return Array.Empty<FlowRunNavigationItem>();

            Dictionary<string, FlowRunNavigationItem> nodeRunsBySerial =
                BuildFlowRunOrder(nodeRecords)
                    .GroupBy(run => run.SerialNumber, StringComparer.Ordinal)
                    .ToDictionary(
                        group => group.Key,
                        group => group.OrderByDescending(run => run.ExecutedTime).First(),
                        StringComparer.Ordinal);

            return flowRuns
                .Where(run => !string.IsNullOrWhiteSpace(run.SerialNumber))
                .GroupBy(run => NormalizeRunSerialNumber(run.SerialNumber), StringComparer.Ordinal)
                .Select(group => group.OrderByDescending(run => run.CompletedTime).First())
                .Where(run => nodeRunsBySerial.ContainsKey(run.SerialNumber!))
                .Select(run =>
                {
                    FlowRunNavigationItem nodeRun = nodeRunsBySerial[run.SerialNumber!];
                    return nodeRun with { ExecutedTime = run.CompletedTime };
                })
                .OrderBy(run => run.ExecutedTime)
                .ThenBy(run => run.BatchId)
                .ThenBy(run => run.SerialNumber, StringComparer.Ordinal)
                .ToArray();
        }

        internal static IReadOnlyList<FlowRunNavigationItem> BuildFlowRunOrder(
            IEnumerable<FlowNodeRecord> records)
        {
            if (records == null)
                return Array.Empty<FlowRunNavigationItem>();

            return records
                .GroupBy(record => new
                {
                    record.BatchId,
                    SerialNumber = NormalizeRunSerialNumber(record.SerialNumber)
                })
                .Select(group => new
                {
                    group.Key.BatchId,
                    group.Key.SerialNumber,
                    FirstStartTime = group.Min(record => record.StartTime),
                    FirstRecordId = group.Min(record => record.Id)
                })
                .OrderBy(run => run.FirstStartTime)
                .ThenBy(run => run.FirstRecordId)
                .ThenBy(run => run.BatchId)
                .ThenBy(run => run.SerialNumber, StringComparer.Ordinal)
                .Select(run => new FlowRunNavigationItem(
                    run.BatchId,
                    run.SerialNumber,
                    run.FirstStartTime))
                .ToArray();
        }

        internal static int FindAdjacentRunIndex(
            IReadOnlyList<FlowRunNavigationItem> orderedRuns,
            int currentBatchId,
            string? currentSerialNumber,
            int direction)
        {
            if (orderedRuns == null || (direction != -1 && direction != 1))
                return -1;

            int currentIndex = FindCurrentRunIndex(
                orderedRuns,
                currentBatchId,
                NormalizeRunSerialNumber(currentSerialNumber));
            int targetIndex = currentIndex + direction;
            return currentIndex >= 0 && targetIndex >= 0 && targetIndex < orderedRuns.Count
                ? targetIndex
                : -1;
        }

        private static int FindCurrentRunIndex(
            IReadOnlyList<FlowRunNavigationItem> orderedRuns,
            int currentBatchId,
            string currentSerialNumber)
        {
            for (int index = 0; index < orderedRuns.Count; index++)
            {
                FlowRunNavigationItem run = orderedRuns[index];
                if (run.BatchId == currentBatchId
                    && string.Equals(run.SerialNumber, currentSerialNumber, StringComparison.Ordinal))
                {
                    return index;
                }
            }
            return -1;
        }

        private static string FormatRunLabel(string serialNumber)
        {
            return string.IsNullOrWhiteSpace(serialNumber) ? "无 SN 流程" : serialNumber;
        }

        private static string FormatRunLabel(FlowRunNavigationItem run)
        {
            return $"Batch {run.BatchId} · {FormatRunLabel(run.SerialNumber)}";
        }

        private static string NormalizeRunSerialNumber(string? serialNumber)
        {
            return string.IsNullOrWhiteSpace(serialNumber) ? string.Empty : serialNumber;
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
