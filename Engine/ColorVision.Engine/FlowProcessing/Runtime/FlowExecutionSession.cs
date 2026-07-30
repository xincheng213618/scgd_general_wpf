#pragma warning disable CS4014,CS8601,CS8602,CS8603,CS8625
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Runtime;
using log4net;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing
{
    internal sealed class FlowExecutionSession : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowExecutionSession));
        private readonly ViewFlow View;
        private readonly FlowEngineManager FlowEngineManager;
        private FlowControl FlowControl => View.FlowControl;
        private readonly FlowRunExecutor _flowRunExecutor;
        private readonly FlowRunFinalizer _runFinalizer;
        private readonly FlowExecutionJournalCoordinator _journalCoordinator =
            FlowExecutionJournalCoordinator.Shared;
        private readonly Timer _timer;
        private readonly Stopwatch _stopwatch = new();
        private MeasureBatchModel? _batch;
        private int _pendingUiUpdate;
        private CancellationTokenSource? _refreshCts;
        private bool _suppressSelectionRefresh;
        private volatile bool _flowCompletionPending;
        private string? _activeFlowSerialNumber;
        private bool _cancelFlowStartRequested;
        private CancellationTokenSource? _flowRunCts;
        private FlowExecutionJournalScope? _activeJournalScope;
        private readonly object _flowLifecycleSync = new();
        private string? _startNodeName;
        private const string FlowMqttNotReadyMessage = "流程 MQTT 连接尚未就绪，本次未启动。请检查 MQTT 配置或稍后重试。";

        private readonly record struct FlowRunCoreResult(
            FlowControlData? EngineResult,
            FlowRunFinalizedData? FinalizedResult);

        private MeasureBatchModel? CurrentBatch
        {
            get => View.IsStandalone ? _batch : FlowEngineManager.Batch;
            set
            {
                _batch = value;
                if (!View.IsStandalone)
                    FlowEngineManager.Batch = value;
            }
        }

        public FlowExecutionSession(FlowEngineManager flowEngineManager, ViewFlow view)
        {
            FlowEngineManager = flowEngineManager;
            View = view;
            _flowRunExecutor = new FlowRunExecutor(FlowControl);
            _runFinalizer = new FlowRunFinalizer();
            _timer = new Timer(UpdateRuntimeProgress, null, Timeout.Infinite, 100);
            _journalCoordinator.StartRecovery();
            MqttRCService.GetInstance().ServiceTokensUpdated += MqttRCService_ServiceTokensUpdated;
        }

        private static void MqttRCService_ServiceTokensUpdated(object? sender, EventArgs e)
        {
            FlowNodeManager.Instance.UpdateDevice(MqttRCService.GetInstance().ServiceTokens);
        }

        public void InitializeSelection()
        {
            TemplateModel<FlowParam>? selectedTemplate =
                TemplateFlow.Params.FirstOrDefault(a => a.Id == FlowEngineConfig.Instance.LastSelectFlow)
                ?? TemplateFlow.Params.FirstOrDefault();
            if (selectedTemplate == null)
                return;

            _suppressSelectionRefresh = true;
            try
            {
                SetSelectedFlowTemplate(selectedTemplate);
            }
            finally
            {
                _suppressSelectionRefresh = false;
            }
            _ = RefreshAsync();
        }

        public void OnFlowSelectionChanged(TemplateModel<FlowParam>? flowTemplate)
        {
            if (flowTemplate == null)
                return;

            SetSelectedFlowTemplate(flowTemplate);
            if (!_suppressSelectionRefresh)
                _ = DebouncedRefresh();
        }

        private void SetSelectedFlowTemplate(TemplateModel<FlowParam> flowTemplate)
        {
            int selectedIndex = TemplateFlow.Params
                .ToList()
                .FindIndex(item => item.Id == flowTemplate.Id);
            FlowEngineManager.SelectedFlowParam = selectedIndex >= 0
                ? TemplateFlow.Params[selectedIndex].Value
                : flowTemplate.Value;
            FlowEngineManager.TemplateFlowParamsIndex = selectedIndex;
            FlowEngineConfig.Instance.LastSelectFlow = flowTemplate.Id;
        }

        private async Task DebouncedRefresh()
        {
            _refreshCts?.Cancel();
            var cts = new CancellationTokenSource();
            _refreshCts = cts;
            try
            {
                await Task.Delay(200, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            await RefreshAsync();
        }

        private void CancelPendingRefresh()
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            _refreshCts = null;
        }

        public async Task SelectFlowTemplateAsync(
            TemplateModel<FlowParam> flowTemplate,
            bool allowEmptyFlow = false)
        {
            CancelPendingRefresh();

            _suppressSelectionRefresh = true;
            try
            {
                SetSelectedFlowTemplate(flowTemplate);
            }
            finally
            {
                _suppressSelectionRefresh = false;
            }

            while (IsRefresh)
                await Task.Delay(20);

            await RefreshAsync(allowEmptyFlow);
        }

        bool IsRefresh;
        public Task RefreshAsync()
        {
            return RefreshAsync(false);
        }

        private async Task RefreshAsync(bool allowEmptyFlow)
        {
            if (IsRefresh) return;
            IsRefresh = true;
            try
            {
                await CloseRunningFlowBeforeRefreshAsync();
                MqttRCService.GetInstance().QueryServices();

                if (View == null)
                    return;

                InvalidateExecutionPresentation();
                View.ShowExecutionSummary(string.Empty);
                var selectedTemplate = GetSelectedFlowTemplate();
                if (selectedTemplate == null)
                {
                    ClearDisplayedFlow(null);
                    return;
                }

                FlowParam flowParam = selectedTemplate.Value;

                if (string.IsNullOrEmpty(flowParam.DataBase64))
                {
                    if (!allowEmptyFlow)
                        MessageBox.Show(ColorVision.Engine.Properties.Resources.Flow_CreateTemplateBeforeSelection);
                    ClearDisplayedFlow(flowParam);
                    return;
                }

                var CVBaseServerNodes = FlowEngineManager.CVBaseServerNodes;
                CVBaseServerNodes.Clear();
                foreach (var item in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
                {
                    item.nodeRunEvent -= NodeRunEvent;
                    item.nodeEndEvent -= NodeEndEvent;
                }
                ResetNodeTitleProgress();
                View.FlowEngineControl.FlowClear();
                View.ApplyExecutionPolicies(flowParam, out _);
                View.FlowEngineControl.LoadFromBase64(flowParam.DataBase64, MqttRCService.GetInstance().ServiceTokens);
                View.SetDocumentBaseline(flowParam);
                View.FitLoadedFlowToViewport();
                RefreshStartNodeSelection();

                FlowEngineManager.SelectedFlowParam = flowParam;

                foreach (var item in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
                {
                    CVBaseServerNodes.Insert(0,item);
                    item.nodeRunEvent += NodeRunEvent;
                    item.nodeEndEvent += NodeEndEvent;
                }
                View.STNodeEditorMain.Invalidate();
                FlowEngineManager.Copilot.PublishContext();
            }
            catch (Exception ex)
            {
                log.Error(ex);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                });
                View.FlowEngineControl.LoadFromBase64(string.Empty);
            }
            finally
            {
                IsRefresh = false;
            }
        }

        private void ClearDisplayedFlow(FlowParam? flowParam)
        {
            foreach (CVBaseServerNode node in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
            {
                node.nodeRunEvent -= NodeRunEvent;
                node.nodeEndEvent -= NodeEndEvent;
            }

            ResetNodeTitleProgress();
            View.ApplyExecutionPolicies(flowParam, out _);
            View.FlowEngineControl.LoadFromBase64(string.Empty);
            View.SetDocumentBaseline(flowParam);
            RefreshStartNodeSelection();
            FlowEngineManager.CVBaseServerNodes.Clear();
            FlowEngineManager.SelectedFlowParam = flowParam;
            if (flowParam == null)
                FlowEngineManager.TemplateFlowParamsIndex = -1;
            View.STNodeEditorMain.Invalidate();
            FlowEngineManager.Copilot.PublishContext();
        }

        private async Task CloseRunningFlowBeforeRefreshAsync()
        {
            bool shouldStop;
            lock (_flowLifecycleSync)
            {
                shouldStop = _flowCompletionPending;
            }

            if (shouldStop)
            {
                log.Info("流程生命周期中触发刷新，先取消并等待当前流程收尾。");
                StopFlow();
            }
            while (_flowCompletionPending)
                await Task.Delay(20);
        }

        private TemplateModel<FlowParam>? GetSelectedFlowTemplate()
        {
            if (View.IsStandalone)
                return View.GetStandaloneExecutionTemplate();

            FlowParam? flowParam = FlowEngineManager.SelectedFlowParam;
            if (flowParam != null)
                return TemplateFlow.Params.FirstOrDefault(a => a.Value?.Id == flowParam.Id);

            int selectedIndex = FlowEngineManager.TemplateFlowParamsIndex;
            if (selectedIndex >= 0 && selectedIndex < TemplateFlow.Params.Count)
                return TemplateFlow.Params[selectedIndex];

            return null;
        }

        public string[] RefreshStartNodeSelection(string? selectedName = null)
        {
            string[] startNodeNames = View.FlowEngineControl.GetStartNodeNames();
            if (!string.IsNullOrWhiteSpace(selectedName) && startNodeNames.Contains(selectedName))
                _startNodeName = selectedName;
            else if (!string.IsNullOrWhiteSpace(_startNodeName) && startNodeNames.Contains(_startNodeName))
                selectedName = _startNodeName;
            else
                _startNodeName = startNodeNames.FirstOrDefault();
            return startNodeNames;
        }

        public string? SelectedStartNodeName => _startNodeName;

        public void SelectStartNode(string? startNodeName)
        {
            _startNodeName = startNodeName;
        }

        /// <summary>
        /// 流程图引擎结束事件；此时后处理可能仍在运行。
        /// </summary>
        public event EventHandler<FlowControlData>? EngineExecutionCompleted;

        /// <summary>
        /// 流程及其后处理全部结束后发布的最终结果。
        /// </summary>
        public event EventHandler<FlowRunFinalizedData>? RunFinalized;

        /// <summary>
        /// 启动流程并等待执行完成，返回流程执行结果。
        /// 如果流程未能启动（验证失败、已在运行等），返回 null。
        /// </summary>
        public async Task<FlowControlData?> RunFlowAndWaitAsync()
        {
            FlowRunCoreResult result = await RunFlowCoreAsync(CreateFlowSerialNumber());
            return result.EngineResult;
        }

        /// <summary>
        /// 启动流程并等待引擎及全部后处理完成，返回最终结果。
        /// 如果流程未能启动，返回 null。
        /// </summary>
        public async Task<FlowRunFinalizedData?> RunFlowAndWaitForFinalizationAsync()
        {
            FlowRunCoreResult result = await RunFlowCoreAsync(CreateFlowSerialNumber());
            return result.FinalizedResult;
        }

        private async Task<FlowRunFinalizedData?> FinalizeFlowCompletionAsync(
            FlowControlData flowControlData,
            FlowExecutionJournalScope? journalScope)
        {
            if (!string.Equals(flowControlData.SerialNumber, _activeFlowSerialNumber, StringComparison.Ordinal))
                return null;

            _stopwatch.Stop();
            StopRuntimeTimer();
            MeasureBatchModel? completedBatch = CurrentBatch;
            string completedFlowName = _flowName;
            long completedGeneration = Volatile.Read(ref _executionGeneration);
            string? completedErrorNodeKey = string.IsNullOrWhiteSpace(flowControlData.ErrorNodeId)
                ? flowControlData.ErrorNodeName
                : flowControlData.ErrorNodeId;

            try
            {
                if (completedBatch == null)
                {
                    log.Error("流程完成时找不到当前批次。");
                }
                else
                {
                    completedBatch.FlowStatus = flowControlData.FlowStatus;
                    completedBatch.TotalTime = (int)_stopwatch.ElapsedMilliseconds;
                    completedBatch.Result = flowControlData.Params;
                    try
                    {
                        using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                        Db.Updateable(completedBatch).ExecuteReturnEntity();
                    }
                    catch (Exception ex)
                    {
                        log.Error("更新流程批次完成状态失败。", ex);
                    }
                }

                string lastNodes = string.IsNullOrWhiteSpace(flowControlData.ErrorNodeName)
                    ? (_runningNodeNames.IsEmpty ? _currentNodeName : string.Join(", ", _runningNodeNames.Values))
                    : flowControlData.ErrorNodeName;
                _runningNodeNames.Clear();
                ResetNodeTitleProgress();
                string msg = $"{completedFlowName} {flowControlData.EventName}{Environment.NewLine}{ColorVision.Engine.Properties.Resources.Flow_NodeLabel}{lastNodes}{Environment.NewLine}{flowControlData.Params}{Environment.NewLine}{_stopwatch.ElapsedMilliseconds}ms";
                CVCommonNode? failedNode;
                lock (_executionStateSync)
                {
                    _completedErrorNodeKey = completedErrorNodeKey;
                    _completedSummaryMessage = msg;
                    failedNode = _lastFailedNode;
                }
                View.ShowExecutionSummary(msg);
                if (failedNode != null
                    && FlowExecutionNavigator.IsExecutionNodeNameMatch(failedNode, completedErrorNodeKey))
                {
                    _ = ShowExecutionSummaryAfterNodeWritesAsync(
                        failedNode,
                        null,
                        completedErrorNodeKey,
                        msg,
                        completedGeneration);
                }
                if (!View.IsStandalone)
                    FlowEngineManager.BatchProgress = 100;
                log.Info(msg);

                await WaitForTerminalNodeEndAsync(completedErrorNodeKey, completedGeneration);
                await WaitForNodeWritesAsync();
            }
            catch (Exception ex)
            {
                log.Error("处理流程完成事件失败。", ex);
            }

            if (!string.Equals(flowControlData.SerialNumber, _activeFlowSerialNumber, StringComparison.Ordinal))
                return null;

            journalScope?.TryAppendEvent(
                "engine-completed",
                "EngineCompleted",
                code: flowControlData.FlowStatus.ToString(),
                message: flowControlData.Message);
            PublishEngineExecutionCompleted(flowControlData);
            FlowRunFinalizedData finalizedData =
                await _runFinalizer.FinalizeAsync(
                    new FlowRunFinalizationRequest(
                        flowControlData,
                        completedBatch,
                        completedFlowName,
                        _stopwatch.ElapsedMilliseconds),
                    journalScope);
            PublishRunFinalized(finalizedData);
            return finalizedData;
        }

        private void PublishEngineExecutionCompleted(FlowControlData data)
        {
            Delegate[] handlers = EngineExecutionCompleted?.GetInvocationList() ?? Array.Empty<Delegate>();
            foreach (EventHandler<FlowControlData> handler in handlers.Cast<EventHandler<FlowControlData>>())
            {
                try
                {
                    handler(this, data);
                }
                catch (Exception ex)
                {
                    log.Error("流程完成订阅者处理失败。", ex);
                }
            }
        }

        private void PublishRunFinalized(FlowRunFinalizedData data)
        {
            Delegate[] handlers = RunFinalized?.GetInvocationList() ?? Array.Empty<Delegate>();
            foreach (EventHandler<FlowRunFinalizedData> handler in handlers.Cast<EventHandler<FlowRunFinalizedData>>())
            {
                try
                {
                    handler(this, data);
                }
                catch (Exception ex)
                {
                    log.Error("流程最终完成订阅者处理失败。", ex);
                }
            }
        }

        private void AttachExecutionNodeEvents()
        {
            foreach (CVBaseServerNode node in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
            {
                node.nodeRunEvent -= NodeRunEvent;
                node.nodeEndEvent -= NodeEndEvent;
                node.nodeRunEvent += NodeRunEvent;
                node.nodeEndEvent += NodeEndEvent;
            }
        }

        internal void DetachNodeEvents()
        {
            foreach (CVBaseServerNode node in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
            {
                node.nodeRunEvent -= NodeRunEvent;
                node.nodeEndEvent -= NodeEndEvent;
            }
        }

        private async Task<bool> PreProcessing(string flowName, string serialNumber)
        {
            return await PreProcessManager.GetInstance().ExecuteAsync(
                flowName,
                serialNumber,
                new ObservableCollection<CVBaseServerNode>(
                    View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>()));
        }
        
        private long _lastFlowTime;

        private string _currentNodeName = string.Empty;
        private void StopRuntimeTimer()
        {
            try
            {
                _timer.Change(Timeout.Infinite, 500);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void UpdateRuntimeProgress(object? sender)
        {
            if (FlowControl.IsFlowRun)
            {
                // Throttle: skip if a previous UI update is still pending
                if (Interlocked.CompareExchange(ref _pendingUiUpdate, 1, 0) != 0)
                    return;

                long elapsedMilliseconds = _stopwatch.ElapsedMilliseconds;
                TimeSpan elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);
                string elapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}:{elapsed.Milliseconds:D4}";
                string runningNodes = _runningNodeNames.IsEmpty ? _currentNodeName : string.Join(", ", _runningNodeNames.Values);
                string msg;
                if (_lastFlowTime == 0 || _lastFlowTime - elapsedMilliseconds < 0)
                {
                    msg = $"{ColorVision.Engine.Properties.Resources.Flow_ExecutingNodeLabel}{runningNodes}{Environment.NewLine}{ColorVision.Engine.Properties.Resources.Flow_ElapsedTimeLabel}{elapsedTime} {Environment.NewLine}";
                }
                else
                {
                    long remainingMilliseconds = _lastFlowTime - elapsedMilliseconds;
                    TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                    string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                    msg = $"{_flowName}{ColorVision.Engine.Properties.Resources.Flow_LastExecutionLabel}{_lastFlowTime} ms{Environment.NewLine}{ColorVision.Engine.Properties.Resources.Flow_ExecutingNodeLabel}{runningNodes}{Environment.NewLine}{ColorVision.Engine.Properties.Resources.Flow_ElapsedTimeLabel}{elapsedTime} {Environment.NewLine}{ColorVision.Engine.Properties.Resources.Flow_EstimatedRemainingLabel}{remainingTime}";
                }
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    Interlocked.Exchange(ref _pendingUiUpdate, 0);
                    UpdateRunningNodeTitleProgress();
                    if (!View.IsStandalone && _lastFlowTime != 0)
                    {
                        double perfect = (double) elapsedMilliseconds / (double)_lastFlowTime * 100;
                        FlowEngineManager.BatchProgress = perfect >= 100 ?  99:perfect;
                    }
                    View.logTextBox.Text = msg;
                });
            }
        }

        public CVCommonNode LastNode { get; set; }

        private sealed class PendingNodeExecution
        {
            public PendingNodeExecution(
                string writeKey,
                string invocationId,
                FlowNodeRecord record,
                FlowNodeMessage? message,
                FlowExecutionJournalScope? journalScope,
                long generation)
            {
                WriteKey = writeKey;
                InvocationId = invocationId;
                Record = record;
                Message = message;
                JournalScope = journalScope;
                Generation = generation;
            }

            public string WriteKey { get; }

            public string InvocationId { get; }

            public FlowNodeRecord Record { get; }

            public FlowNodeMessage? Message { get; }

            public FlowExecutionJournalScope? JournalScope { get; }

            public FlowNodeAttempt? Attempt { get; set; }

            public long Generation { get; }
        }

        private readonly ConcurrentDictionary<string, ConcurrentQueue<PendingNodeExecution>> _pendingNodeExecutions = new ConcurrentDictionary<string, ConcurrentQueue<PendingNodeExecution>>();
        private readonly ConcurrentDictionary<string, string> _runningNodeNames = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, int> _runningNodeCounts = new ConcurrentDictionary<string, int>();
        private readonly ConcurrentDictionary<string, long> _nodeExpectedDurations = new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, long> _nodeStartedAt = new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, CVCommonNode> _runningNodes = new ConcurrentDictionary<string, CVCommonNode>();
        private readonly ConcurrentDictionary<string, Task> _nodeWriteTasks = new ConcurrentDictionary<string, Task>();
        private readonly object _nodeWriteSync = new object();
        private readonly object _executionStateSync = new object();
        private CVCommonNode? _lastFailedNode;
        private string? _completedErrorNodeKey;
        private string? _completedSummaryMessage;
        private TaskCompletionSource<bool>? _terminalNodeEndCompletion;
        private long _executionGeneration;

        private void QueueNodeWrite(string writeKey, Action write)
        {
            lock (_nodeWriteSync)
            {
                Task nextWrite = _nodeWriteTasks.TryGetValue(writeKey, out Task? previous)
                    ? previous.ContinueWith(
                    _ => write(),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default)
                    : Task.Run(write);
                _nodeWriteTasks[writeKey] = nextWrite;
                FlowNodeRecordDataBaseHelper.TrackPendingWrite(nextWrite);
                _ = nextWrite.ContinueWith(
                    completedTask =>
                    {
                        lock (_nodeWriteSync)
                        {
                            if (_nodeWriteTasks.TryGetValue(writeKey, out Task? current)
                                && ReferenceEquals(current, nextWrite))
                            {
                                _nodeWriteTasks.TryRemove(writeKey, out _);
                            }
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        private async Task ShowExecutionSummaryAfterNodeWritesAsync(
            CVCommonNode node,
            string? writeKey,
            string? completedErrorNodeKey,
            string? completedSummaryMessage,
            long generation)
        {
            if (!string.IsNullOrWhiteSpace(writeKey)
                && _nodeWriteTasks.TryGetValue(writeKey, out Task? pendingWrite))
            {
                try
                {
                    await pendingWrite;
                }
                catch (Exception ex)
                {
                    log.Warn("等待流程节点记录写入失败。", ex);
                }
            }

            bool flushed = await Task.Run(() => FlowNodeRecordDataBaseHelper.FlushPendingWrites());
            if (!flushed)
                log.Warn("等待流程节点记录落库超时，详情窗口可手动刷新。");

            await View.Dispatcher.InvokeAsync(() =>
            {
                bool isCurrent;
                lock (_executionStateSync)
                {
                    isCurrent = generation == Volatile.Read(ref _executionGeneration)
                        && string.Equals(_completedSummaryMessage, completedSummaryMessage, StringComparison.Ordinal)
                        && FlowExecutionNavigator.IsExecutionNodeNameMatch(node, _completedErrorNodeKey)
                        && FlowExecutionNavigator.IsExecutionNodeNameMatch(node, completedErrorNodeKey);
                }
                if (isCurrent)
                {
                    View.ShowExecutionSummary(
                        completedSummaryMessage ?? View.logTextBox.Text,
                        completedErrorNodeKey,
                        node);
                }
            });
        }

        private async Task WaitForTerminalNodeEndAsync(string? errorNodeKey, long generation)
        {
            if (string.IsNullOrWhiteSpace(errorNodeKey))
            {
                await Task.Delay(100);
                return;
            }

            TaskCompletionSource<bool>? completion = null;
            lock (_executionStateSync)
            {
                if (generation != Volatile.Read(ref _executionGeneration)
                    || (_lastFailedNode != null
                        && FlowExecutionNavigator.IsExecutionNodeNameMatch(_lastFailedNode, errorNodeKey)))
                {
                    return;
                }

                completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _terminalNodeEndCompletion = completion;
            }

            await Task.WhenAny(completion.Task, Task.Delay(1000));
            lock (_executionStateSync)
            {
                if (ReferenceEquals(_terminalNodeEndCompletion, completion))
                    _terminalNodeEndCompletion = null;
            }
        }

        private void InvalidateExecutionPresentation()
        {
            Interlocked.Increment(ref _executionGeneration);
            lock (_executionStateSync)
            {
                _lastFailedNode = null;
                _completedErrorNodeKey = null;
                _completedSummaryMessage = null;
                _terminalNodeEndCompletion?.TrySetResult(true);
                _terminalNodeEndCompletion = null;
            }
        }

        private async Task LoadNodeExpectedDurationsAsync()
        {
            string[] nodeIds = View.STNodeEditorMain.Nodes
                .OfType<CVBaseServerNode>()
                .Select(node => node.NodeID)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Dictionary<string, long> durations = await Task.Run(
                () => FlowNodeRecordDataBaseHelper.GetLastElapsedByNodeIds(nodeIds));

            _nodeExpectedDurations.Clear();
            foreach (KeyValuePair<string, long> item in durations)
                _nodeExpectedDurations[item.Key] = item.Value;
        }

        private void UpdateRunningNodeTitleProgress()
        {
            long now = Environment.TickCount64;
            foreach (KeyValuePair<string, CVCommonNode> item in _runningNodes)
            {
                if (!_nodeExpectedDurations.TryGetValue(item.Key, out long expectedDuration) || expectedDuration <= 0)
                    continue;
                if (!_nodeStartedAt.TryGetValue(item.Key, out long startedAt))
                    continue;

                long elapsed = Math.Max(0, now - startedAt);
                item.Value.TitleProgress = (float)Math.Min(0.99d, (double)elapsed / expectedDuration);
            }
        }

        private void ResetNodeTitleProgress()
        {
            foreach (CVCommonNode node in View.STNodeEditorMain.Nodes.OfType<CVCommonNode>())
                node.TitleProgress = -1f;

            _runningNodes.Clear();
            _runningNodeCounts.Clear();
            _nodeStartedAt.Clear();
        }

        private void NodeEndEvent(object sender, FlowEngineNodeEndEventArgs e)
        {
            if (!string.Equals(e?.SerialNumber, _activeFlowSerialNumber, StringComparison.Ordinal))
                return;

            if (sender is CVCommonNode algorithmNode)
            {
                string writeKey = GetNodeExecutionKey(algorithmNode, e.RecvMsgId, e.SerialNumber);
                if (!TryTakePendingNodeExecution(writeKey, algorithmNode, e.SerialNumber, out PendingNodeExecution? execution))
                    return;

                long generation = execution.Generation;
                int runningCount = _runningNodeCounts.AddOrUpdate(
                    algorithmNode.NodeID,
                    0,
                    (_, current) => Math.Max(0, current - 1));
                bool nodeStillRunning = runningCount > 0;
                if (!nodeStillRunning)
                    _runningNodes.TryRemove(algorithmNode.NodeID, out _);
                long elapsedFromClock = 0;
                if (!nodeStillRunning && _nodeStartedAt.TryRemove(algorithmNode.NodeID, out long startedAt))
                    elapsedFromClock = Math.Max(0, Environment.TickCount64 - startedAt);
                if (!nodeStillRunning)
                    algorithmNode.TitleProgress = -1f;

                if (e != null)
                {
                    algorithmNode.IsSelected = nodeStillRunning;

                    if (e.FailureKind == FlowFailureKind.Canceled)
                    {
                        algorithmNode.TitleColor =
                            System.Drawing.Color.Gray;
                    }
                    else if (e.RecvStatusCode == 0)
                    {
                        algorithmNode.TitleColor = System.Drawing.Color.Green;
                    }
                    else
                    {
                        algorithmNode.TitleColor = System.Drawing.Color.Red;
                    }
                }

                if (!nodeStillRunning)
                    _runningNodeNames.TryRemove(algorithmNode.NodeID, out _);

                FlowNodeRecord record = execution.Record;
                record.EndTime = DateTime.Now;
                record.ElapsedMs = (long)(record.EndTime.Value - record.StartTime).TotalMilliseconds;
                if (record.ElapsedMs > 0)
                    _nodeExpectedDurations[algorithmNode.NodeID] = record.ElapsedMs;
                else if (elapsedFromClock > 0)
                    _nodeExpectedDurations[algorithmNode.NodeID] = elapsedFromClock;
                QueueNodeWrite(execution.WriteKey, () => FlowNodeRecordDataBaseHelper.Update(record));

                // Update the existing message with received MQTT response
                if (execution.Message is FlowNodeMessage nodeMsg)
                {
                    nodeMsg.RecvTime = DateTime.Now;
                    if (e?.FailureKind == FlowFailureKind.Canceled)
                    {
                        nodeMsg.StatusCode = e.RecvStatusCode;
                        nodeMsg.StatusMessage = e.RecvStatusMessage;
                        nodeMsg.State = FlowMessageState.Canceled;
                    }
                    else if (e != null && !string.IsNullOrEmpty(e.RecvMsgId))
                    {
                        nodeMsg.RecvTopic = e.RecvTopic;
                        nodeMsg.RecvPayload = e.RecvPayload;
                        nodeMsg.StatusCode = e.RecvStatusCode;
                        nodeMsg.StatusMessage = e.RecvStatusMessage;
                        nodeMsg.State = (e.RecvStatusCode.HasValue && e.RecvStatusCode.Value == 0)
                            ? FlowMessageState.Success : FlowMessageState.Fail;
                    }
                    else
                    {
                        nodeMsg.State = FlowMessageState.Timeout;
                    }
                    QueueNodeWrite(execution.WriteKey, () => FlowNodeRecordDataBaseHelper.UpdateMessage(nodeMsg));
                }

                string attemptOutcome;
                string? attemptErrorCode = null;
                string? attemptErrorMessage = null;
                if (e?.WillRetry == true)
                {
                    attemptOutcome = "Retrying";
                    attemptErrorCode =
                        e.FailureKind?.ToString() ?? "RetryableFailure";
                    attemptErrorMessage = e.RecvStatusMessage;
                }
                else if (e?.FailureHandled == true)
                {
                    attemptOutcome = "HandledFailure";
                    attemptErrorCode =
                        e.FailureKind?.ToString() ?? "HandledFailure";
                    attemptErrorMessage = e.RecvStatusMessage;
                }
                else if (e?.FailureKind == FlowFailureKind.Canceled)
                {
                    attemptOutcome = "Canceled";
                    attemptErrorCode = "Canceled";
                    attemptErrorMessage =
                        e.RecvStatusMessage ?? "节点执行已取消。";
                }
                else if (e?.RecvStatusCode == 0)
                {
                    attemptOutcome = "Succeeded";
                }
                else if (e?.FailureKind == FlowFailureKind.Timeout
                    || e == null
                    || string.IsNullOrWhiteSpace(e.RecvMsgId)
                    || !e.RecvStatusCode.HasValue)
                {
                    attemptOutcome = "TimedOut";
                    attemptErrorCode = "Timeout";
                    attemptErrorMessage = e?.RecvStatusMessage ?? "节点等待响应超时。";
                }
                else
                {
                    attemptOutcome = "Failed";
                    attemptErrorCode = e.RecvStatusCode.Value.ToString();
                    attemptErrorMessage = e.RecvStatusMessage;
                }

                QueueNodeWrite(execution.WriteKey, () =>
                {
                    execution.JournalScope?.TryCompleteAttempt(
                        execution.Attempt,
                        attemptOutcome,
                        attemptErrorCode,
                        attemptErrorMessage);
                    if (string.Equals(
                        attemptOutcome,
                        "Retrying",
                        StringComparison.Ordinal))
                    {
                        execution.JournalScope?.TryAppendEvent(
                            $"node-retry-scheduled:{execution.InvocationId}",
                            "NodeRetryScheduled",
                            algorithmNode.NodeID,
                            execution.Attempt?.Id,
                            attemptErrorCode,
                            attemptErrorMessage,
                            System.Text.Json.JsonSerializer.Serialize(
                                new
                                {
                                    AttemptNumber =
                                        e?.AttemptNumber,
                                    MaxAttempts =
                                        e?.MaxAttempts,
                                    RetryDelayMs =
                                        e?.RetryDelayMs
                                }));
                    }
                    else if (string.Equals(
                        attemptOutcome,
                        "HandledFailure",
                        StringComparison.Ordinal))
                    {
                        execution.JournalScope?.TryAppendEvent(
                            $"node-failure-handled:{execution.InvocationId}",
                            "NodeFailureHandled",
                            algorithmNode.NodeID,
                            execution.Attempt?.Id,
                            attemptErrorCode,
                            attemptErrorMessage,
                            System.Text.Json.JsonSerializer.Serialize(
                                new
                                {
                                    TargetNodeId =
                                        e?.FailureRouteTargetNodeId
                                }));
                    }
                    else if (!string.Equals(
                        attemptOutcome,
                        "Succeeded",
                        StringComparison.Ordinal)
                        && !string.Equals(
                            attemptOutcome,
                            "Canceled",
                            StringComparison.Ordinal))
                    {
                        execution.JournalScope?.TryCreateIncident(
                            $"node-failure:{execution.InvocationId}",
                            attemptOutcome == "TimedOut"
                                ? "NodeTimeout"
                                : "NodeExecutionFailed",
                            "Error",
                            $"{algorithmNode.OnGetDrawTitle()} {attemptOutcome}",
                            algorithmNode.NodeID,
                            execution.Attempt?.Id,
                            attemptErrorMessage);
                    }
                });

                if (e?.RecvStatusCode != 0
                    && e?.FailureHandled != true
                    && e?.WillRetry != true
                    && e?.FailureKind != FlowFailureKind.Canceled)
                {
                    string? completedErrorNodeKey;
                    string? completedSummaryMessage;
                    bool matchesCompletedFailure;
                    lock (_executionStateSync)
                    {
                        _lastFailedNode = algorithmNode;
                        completedErrorNodeKey = _completedErrorNodeKey;
                        completedSummaryMessage = _completedSummaryMessage;
                        matchesCompletedFailure = FlowExecutionNavigator.IsExecutionNodeNameMatch(
                            algorithmNode,
                            completedErrorNodeKey);
                        if (matchesCompletedFailure)
                            _terminalNodeEndCompletion?.TrySetResult(true);
                    }

                    if (matchesCompletedFailure)
                    {
                        _ = ShowExecutionSummaryAfterNodeWritesAsync(
                            algorithmNode,
                            execution.WriteKey,
                            completedErrorNodeKey,
                            completedSummaryMessage,
                        generation);
                    }
                }
                else if (e?.FailureKind == FlowFailureKind.Canceled)
                {
                    lock (_executionStateSync)
                    {
                        if (FlowExecutionNavigator
                            .IsExecutionNodeNameMatch(
                                algorithmNode,
                                _completedErrorNodeKey))
                        {
                            _terminalNodeEndCompletion
                                ?.TrySetResult(true);
                        }
                    }
                }
            }
        }

        private void NodeRunEvent(object sender, FlowEngineNodeRunEventArgs e)
        {
            if (!string.Equals(e?.SerialNumber, _activeFlowSerialNumber, StringComparison.Ordinal))
                return;

            if (sender is CVCommonNode algorithmNode)
            {
                string writeKey = GetNodeExecutionKey(algorithmNode, e.SendMsgId, e.SerialNumber);
                string invocationId = Guid.NewGuid().ToString("N");
                LastNode = algorithmNode;
                long generation = Volatile.Read(ref _executionGeneration);
                _runningNodeCounts.AddOrUpdate(algorithmNode.NodeID, 1, (_, current) => current + 1);
                algorithmNode.IsSelected = true;
                _currentNodeName = algorithmNode.Title;
                _runningNodeNames[algorithmNode.NodeID] = algorithmNode.Title;
                _runningNodes[algorithmNode.NodeID] = algorithmNode;
                _nodeStartedAt[algorithmNode.NodeID] = Environment.TickCount64;
                algorithmNode.TitleProgressColor = System.Drawing.Color.DeepSkyBlue;
                algorithmNode.TitleProgress = _nodeExpectedDurations.TryGetValue(algorithmNode.NodeID, out long expectedDuration)
                    && expectedDuration > 0 ? 0f : -1f;
                UpdateRuntimeProgress(sender);

                int batchId = CurrentBatch?.Id ?? 0;
                var record = new FlowNodeRecord
                {
                    BatchId = batchId,
                    SerialNumber = e.SerialNumber,
                    NodeId = algorithmNode.NodeID,
                    NodeName = algorithmNode.OnGetDrawTitle(),
                    NodeType = algorithmNode.NodeType,
                    StartTime = DateTime.Now,
                };
                // Record sent MQTT message (combined send/recv record)
                FlowNodeMessage? msg = null;
                if (e != null && !string.IsNullOrEmpty(e.SendMsgId))
                {
                    msg = new FlowNodeMessage
                    {
                        BatchId = batchId,
                        SerialNumber = e.SerialNumber,
                        NodeId = algorithmNode.NodeID,
                        NodeName = algorithmNode.OnGetDrawTitle(),
                        MsgId = e.SendMsgId,
                        EventName = e.SendEventName,
                        SendTopic = e.SendTopic,
                        SendPayload = e.SendPayload,
                        SendTime = DateTime.Now,
                        State = FlowMessageState.Sent
                    };
                }

                var execution = new PendingNodeExecution(
                    writeKey,
                    invocationId,
                    record,
                    msg,
                    _activeJournalScope,
                    generation);
                _pendingNodeExecutions
                    .GetOrAdd(writeKey, _ => new ConcurrentQueue<PendingNodeExecution>())
                    .Enqueue(execution);
                QueueNodeWrite(writeKey, () =>
                {
                    int insertId = FlowNodeRecordDataBaseHelper.Insert(record);
                    if (insertId > 0 && msg != null)
                    {
                        msg.NodeRecordId = record.Id;
                        FlowNodeRecordDataBaseHelper.InsertMessage(msg);
                    }

                    execution.Attempt = execution.JournalScope?.TryBeginAttempt(
                        algorithmNode.NodeID,
                        execution.InvocationId,
                        insertId > 0 ? insertId : null);
                });
            }
        }

        private async Task WaitForNodeWritesAsync()
        {
            Task[] pendingWrites;
            lock (_nodeWriteSync)
            {
                pendingWrites = _nodeWriteTasks.Values
                    .Where(task => !task.IsCompleted)
                    .ToArray();
            }
            if (pendingWrites.Length == 0)
                return;

            try
            {
                await Task.WhenAll(pendingWrites);
            }
            catch (Exception ex)
            {
                // Legacy and journal writes are fail-open and already log their
                // individual failures. Do not let a diagnostic task alter the
                // flow result.
                log.Warn("等待流程节点诊断记录完成时发生异常。", ex);
            }
        }

        private bool TryTakePendingNodeExecution(
            string writeKey,
            CVCommonNode node,
            string? serialNumber,
            out PendingNodeExecution? execution)
        {
            if (_pendingNodeExecutions.TryGetValue(writeKey, out ConcurrentQueue<PendingNodeExecution>? exactQueue)
                && exactQueue.TryDequeue(out execution))
            {
                return true;
            }

            string prefix = $"{serialNumber}|{node.NodeID}|";
            foreach (KeyValuePair<string, ConcurrentQueue<PendingNodeExecution>> item in _pendingNodeExecutions
                         .Where(item => item.Key.StartsWith(prefix, StringComparison.Ordinal))
                         .OrderBy(item => item.Value.TryPeek(out PendingNodeExecution? pending)
                             ? pending.Record.StartTime
                             : DateTime.MaxValue))
            {
                if (item.Value.TryDequeue(out execution))
                    return true;
            }

            execution = null;
            return false;
        }

        private static string GetNodeExecutionKey(
            CVCommonNode node,
            string? messageId,
            string? serialNumber)
        {
            return $"{serialNumber}|{node.NodeID}|{messageId}";
        }


        private string _flowName = string.Empty;
        public async Task RunFlowAsync()
        {
            await RunFlowCoreAsync();
        }

        private static string CreateFlowSerialNumber()
        {
            return DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
        }

        private FlowExecutionJournalScope? TryBeginExecutionJournal(
            FlowParam flowParam,
            string flowName,
            string serialNumber,
            MeasureBatchModel batch)
        {
            try
            {
                FlowTemplateSnapshot snapshot =
                    FlowTemplateSnapshotFactory.Create(
                        flowParam.Id,
                        flowParam.DataBase64 ?? string.Empty,
                        templateRevision: flowParam.TemplateRevision,
                        flowKey: flowParam.FlowKey);
                return _journalCoordinator.TryBeginRun(
                    snapshot,
                    new FlowRunRecord
                    {
                        TemplateId = flowParam.Id,
                        FlowKey = flowParam.FlowKey,
                        FlowName = flowName,
                        SerialNumber = serialNumber,
                        BatchId = batch.Id > 0 ? batch.Id : null,
                        TemplateRevision =
                            flowParam.TemplateRevision,
                        ExecutionPolicyRevision =
                            flowParam.ExecutionPolicyRevision,
                        ExecutionPolicyHash =
                            flowParam.ExecutionPolicyHash,
                        ExecutionPolicySnapshotJson =
                            flowParam.ExecutionPolicySnapshotJson,
                        RunKey = Guid.NewGuid().ToString("N"),
                        StartedTimeUtc = DateTime.UtcNow,
                    });
            }
            catch (Exception ex)
            {
                // Invalid legacy template content or an unavailable diagnostic
                // store must not prevent the engine from running.
                log.Error("创建流程运行快照失败，当前流程降级为 legacy 记录。", ex);
                return null;
            }
        }

        private static FlowFinalOutcome GetFinalOutcome(FlowStatus status)
        {
            return status switch
            {
                FlowStatus.Completed => FlowFinalOutcome.Succeeded,
                FlowStatus.Canceled => FlowFinalOutcome.Canceled,
                FlowStatus.OverTime => FlowFinalOutcome.TimedOut,
                _ => FlowFinalOutcome.Failed,
            };
        }

        private async Task<FlowRunCoreResult> RunFlowCoreAsync(string? requestedSerialNumber = null)
        {
            bool requiresServices = View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>().Any();
            if (requiresServices && !MqttRCService.GetInstance().IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(),ColorVision.Engine.Properties.Resources.RegistryCenterNotConnected);
                return default;
            }

            if (requiresServices && MqttRCService.GetInstance().ServiceTokens.Count == 0)
            {
                MqttRCService.GetInstance().QueryServices();
                View.ShowExecutionSummary(ColorVision.Engine.Properties.Resources.TokenEmpty_RefreshingToken_PleaseRetry);
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.TokenEmpty_RefreshingToken_PleaseRetry);
                return default;
            }

            RefreshStartNodeSelection(_startNodeName);
            string? startNodeName = _startNodeName;
            if (string.IsNullOrWhiteSpace(startNodeName))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), ColorVision.Engine.Properties.Resources.WorkflowStartNodeNotFound_RunFailed, "ColorVision");
                return default;
            }

            TemplateModel<FlowParam>? selectedTemplate = GetSelectedFlowTemplate();
            if (selectedTemplate == null)
            {
                View.ShowExecutionSummary(ColorVision.Engine.Properties.Resources.Flow_NoValidFlowTemplateSelected);
                log.Warn("未选择有效的流程模板");
                return default;
            }

            string flowName = selectedTemplate.Key;
            FlowParam selectedFlowParam = selectedTemplate.Value;
            string sn = requestedSerialNumber ?? CreateFlowSerialNumber();
            using CancellationTokenSource flowRunCts = new CancellationTokenSource();
            lock (_flowLifecycleSync)
            {
                if (_flowCompletionPending || FlowControl.IsFlowRun)
                {
                    log.Info("流程正在运行或正在启动");
                    return default;
                }
                _activeFlowSerialNumber = sn;
                _flowCompletionPending = true;
                _cancelFlowStartRequested = false;
                _flowRunCts = flowRunCts;
            }

            FlowEditorOperations.ClearSelection(View.STNodeEditorMain);

            bool engineStarted = false;
            bool finalizationCompleted = false;
            MeasureBatchModel? preparedBatch = null;
            FlowExecutionJournalScope? journalScope = null;
            FlowStatus unstartedBatchStatus = FlowStatus.Failed;
            string unstartedBatchResult = "Flow start failed";
            try
            {
                _flowName = flowName;
                _lastFlowTime = await Task.Run(
                    () => FlowNodeRecordDataBaseHelper.GetLastCompletedFlowElapsed(selectedFlowParam.Id, flowName));
                ResetNodeTitleProgress();
                await LoadNodeExpectedDurationsAsync();
                if (!CanContinueFlowStart(sn))
                {
                    unstartedBatchStatus = FlowStatus.Canceled;
                    unstartedBatchResult = ColorVision.Engine.Properties.Resources.ExecutionCancelled;
                    return new FlowRunCoreResult(
                        CreateCanceledFlowData(startNodeName, sn, unstartedBatchResult),
                        null);
                }

                foreach (var item in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
                {
                    item.TitleColor = System.Drawing.Color.Blue;
                }
                ClearFlowRuntimeData();

                LastNode = null;
                InvalidateExecutionPresentation();
                View.ShowExecutionSummary("Run " + flowName);
                if (!View.IsStandalone)
                    FlowEngineManager.BatchProgress = 0;

                _pendingNodeExecutions.Clear();
                _runningNodeNames.Clear();
                _runningNodeCounts.Clear();
                lock (_nodeWriteSync)
                    _nodeWriteTasks.Clear();
                AttachExecutionNodeEvents();

                _stopwatch.Restart();
                _timer.Change(0, 100); // 启动定时器

                CurrentBatch = new MeasureBatchModel()
                {
                    TId = selectedFlowParam.Id > 0 ? selectedFlowParam.Id : null,
                    Name = sn,
                    Code = sn
                };
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                CurrentBatch.Id = Db.Insertable(CurrentBatch).ExecuteReturnIdentity();
                preparedBatch = CurrentBatch;
                journalScope = TryBeginExecutionJournal(
                    selectedFlowParam,
                    flowName,
                    sn,
                    CurrentBatch);
                _activeJournalScope = journalScope;
                journalScope?.TryAppendEvent(
                    "pre-process-started",
                    "PreProcessStarted",
                    message: "流程前处理已开始。");

                bool preresult = await PreProcessing(flowName, sn);
                journalScope?.TryAppendEvent(
                    "pre-process-completed",
                    "PreProcessCompleted",
                    code: preresult ? "Succeeded" : "Failed",
                    message: preresult ? "流程前处理已完成。" : "流程前处理失败。");
                if (!CanContinueFlowStart(sn))
                {
                    unstartedBatchStatus = FlowStatus.Canceled;
                    unstartedBatchResult = ColorVision.Engine.Properties.Resources.ExecutionCancelled;
                    return new FlowRunCoreResult(
                        CreateCanceledFlowData(startNodeName, sn, unstartedBatchResult),
                        null);
                }
                if (!preresult)
                {
                    unstartedBatchResult = ColorVision.Engine.Properties.Resources.Flow_PreprocessFailedCancelled;
                    View.ShowExecutionSummary(ColorVision.Engine.Properties.Resources.Flow_PreprocessFailedCancelled);
                    log.Warn("预处理失败，流程取消执行");
                    return default;
                }

                journalScope?.TryAppendEvent(
                    "engine-starting",
                    "EngineStarting",
                    message: $"从节点 {startNodeName} 启动流程引擎。");
                FlowRunExecutionResult execution = await _flowRunExecutor.RunAsync(
                    startNodeName,
                    sn,
                    executionTimeout: null,
                    flowRunCts.Token);
                if (execution.Termination == FlowRunTermination.StartRejected)
                {
                    unstartedBatchResult = FlowMqttNotReadyMessage;
                    View.ShowExecutionSummary(FlowMqttNotReadyMessage);
                    return default;
                }

                if (!execution.Started)
                {
                    unstartedBatchStatus = execution.Data.FlowStatus;
                    unstartedBatchResult = execution.Data.Message;
                    return new FlowRunCoreResult(execution.Data, null);
                }

                engineStarted = true;
                FlowRunFinalizedData? finalizedResult =
                    await FinalizeFlowCompletionAsync(execution.Data, journalScope);
                finalizationCompleted = true;
                return new FlowRunCoreResult(execution.Data, finalizedResult);
            }
            catch (OperationCanceledException) when (flowRunCts.IsCancellationRequested)
            {
                unstartedBatchStatus = FlowStatus.Canceled;
                unstartedBatchResult = ColorVision.Engine.Properties.Resources.ExecutionCancelled;
                return new FlowRunCoreResult(
                    CreateCanceledFlowData(startNodeName, sn, unstartedBatchResult),
                    null);
            }
            catch (Exception ex)
            {
                unstartedBatchResult = ex.Message;
                journalScope?.TryCreateIncident(
                    "run-unhandled-exception",
                    "UnhandledRunException",
                    "Error",
                    ex.Message,
                    detailsJson: ex.ToString());
                throw;
            }
            finally
            {
                lock (_flowLifecycleSync)
                {
                    if (ReferenceEquals(_flowRunCts, flowRunCts))
                        _flowRunCts = null;
                }
                if (!engineStarted)
                {
                    if (preparedBatch?.Id > 0)
                        FinalizeUnstartedBatch(preparedBatch, unstartedBatchStatus, unstartedBatchResult);
                    _stopwatch.Stop();
                    StopRuntimeTimer();
                }

                if (journalScope != null
                    && !journalScope.IsCompletionRequested)
                {
                    FlowFinalOutcome incompleteOutcome =
                        GetFinalOutcome(unstartedBatchStatus);
                    journalScope.TryAppendEvent(
                        "run-ended-before-finalization",
                        "RunEndedBeforeFinalization",
                        code: unstartedBatchStatus.ToString(),
                        message: unstartedBatchResult);
                    journalScope.TryCompleteRun(
                        unstartedBatchStatus,
                        _stopwatch.ElapsedMilliseconds,
                        incompleteOutcome);
                }
                else if (journalScope == null
                    && preparedBatch != null
                    && !finalizationCompleted)
                {
                    // Bounded fallback for journal initialization/snapshot
                    // failures. Record exactly once at the terminal boundary.
                    FlowNodeRecordDataBaseHelper.RecordFlowRun(
                        preparedBatch.TId ?? 0,
                        flowName,
                        sn,
                        unstartedBatchStatus,
                        _stopwatch.ElapsedMilliseconds);
                }

                if (ReferenceEquals(_activeJournalScope, journalScope))
                    _activeJournalScope = null;
                journalScope?.Dispose();
                lock (_flowLifecycleSync)
                {
                    if (string.Equals(sn, _activeFlowSerialNumber, StringComparison.Ordinal))
                    {
                        _activeFlowSerialNumber = null;
                        _flowCompletionPending = false;
                        _cancelFlowStartRequested = false;
                    }
                }
                lock (_executionStateSync)
                {
                    _terminalNodeEndCompletion?.TrySetResult(true);
                    _terminalNodeEndCompletion = null;
                }
            }
        }

        private static FlowControlData CreateCanceledFlowData(
            string startNodeName,
            string serialNumber,
            string message)
        {
            return new FlowControlData
            {
                StartNodeName = startNodeName,
                SerialNumber = serialNumber,
                EventName = StatusTypeEnum.Canceled.ToString(),
                Status = StatusTypeEnum.Canceled,
                Message = message,
                Params = message
            };
        }

        private void FinalizeUnstartedBatch(MeasureBatchModel batch, FlowStatus status, string result)
        {
            try
            {
                batch.FlowStatus = status;
                batch.TotalTime = (int)_stopwatch.ElapsedMilliseconds;
                batch.Result = result;
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                db.Updateable(batch).ExecuteCommand();
            }
            catch (Exception ex)
            {
                log.Error("更新未启动流程批次状态失败。", ex);
            }
        }

        private bool CanContinueFlowStart(string serialNumber)
        {
            lock (_flowLifecycleSync)
            {
                return string.Equals(serialNumber, _activeFlowSerialNumber, StringComparison.Ordinal)
                    && !_cancelFlowStartRequested
                    && _flowRunCts?.IsCancellationRequested != true;
            }
        }

        private void ClearFlowRuntimeData()
        {
            foreach (STNode node in View.STNodeEditorMain.Nodes)
            {
                foreach (STNodeOption option in node.GetAllInputOptions())
                    option.Data = null;
                foreach (STNodeOption option in node.GetAllOutputOptions())
                    option.Data = null;
            }
        }

        public void StopFlow(bool updateSummary = true)
        {
            CancellationTokenSource? flowRunCts;
            bool hasActiveLifecycle;
            lock (_flowLifecycleSync)
            {
                hasActiveLifecycle = _flowCompletionPending;
                if (hasActiveLifecycle)
                {
                    _cancelFlowStartRequested = true;
                    flowRunCts = _flowRunCts;
                }
                else
                    flowRunCts = null;
            }
            if (flowRunCts != null)
            {
                try
                {
                    flowRunCts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }
            else if (!hasActiveLifecycle && FlowControl.IsFlowRun)
            {
                FlowControl.Stop();
            }

            InvalidateExecutionPresentation();
            ResetNodeTitleProgress();

            if (updateSummary)
                View.ShowExecutionSummary(ColorVision.Engine.Properties.Resources.ExecutionCancelled);
        }

        public void Dispose()
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            StopFlow(updateSummary: false);
            MqttRCService.GetInstance().ServiceTokensUpdated -= MqttRCService_ServiceTokensUpdated;
            DetachNodeEvents();
            ResetNodeTitleProgress();
            _timer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
