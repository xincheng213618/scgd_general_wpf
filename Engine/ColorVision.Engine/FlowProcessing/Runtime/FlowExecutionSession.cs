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
using log4net;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.FlowProcessing
{
    internal sealed class FlowExecutionSession : IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowExecutionSession));
        private readonly ViewFlow View;
        private readonly FlowEngineManager FlowEngineManager;
        private FlowControl FlowControl => FlowEngineManager.FlowControl;
        private ComboBox ComboBoxFlow => View.FlowTemplateComboBox;
        private ComboBox ComboBoxStartNode => View.RuntimeStartNodeComboBox;
        private readonly Timer timer;
        private readonly Stopwatch stopwatch = new();
        private int _pendingUiUpdate;
        private CancellationTokenSource? _refreshCts;
        private bool _suppressSelectionRefresh;
        private volatile bool _flowCompletionPending;
        private string? _activeFlowSerialNumber;
        private bool _cancelFlowStartRequested;
        private CancellationTokenSource? _flowStartCts;
        private readonly object _flowLifecycleSync = new();
        private const string FlowMqttNotReadyMessage = "流程 MQTT 连接尚未就绪，本次未启动。请检查 MQTT 配置或稍后重试。";

        public FlowExecutionSession(FlowEngineManager flowEngineManager, ViewFlow view)
        {
            FlowEngineManager = flowEngineManager;
            View = view;
            timer = new Timer(UpdateMsg, null, Timeout.Infinite, 100);
            MqttRCService.GetInstance().ServiceTokensUpdated += MqttRCService_ServiceTokensUpdated;
        }

        private static void MqttRCService_ServiceTokensUpdated(object? sender, EventArgs e)
        {
            FlowNodeManager.Instance.UpdateDevice(MqttRCService.GetInstance().ServiceTokens);
        }

        public void InitializeSelection()
        {
            var s = TemplateFlow.Params.FirstOrDefault(a => a.Id == FlowEngineConfig.Instance.LastSelectFlow);
            ComboBoxFlow.SelectedItem = s;
            if (s == null && ComboBoxFlow.Items.Count > 0)
                ComboBoxFlow.SelectedIndex = 0;
        }

        public void OnFlowSelectionChanged()
        {
            if (ComboBoxFlow.SelectedValue is FlowParam flowParam)
            {
                FlowEngineManager.SelectedFlowParam = flowParam;
                FlowEngineConfig.Instance.LastSelectFlow = flowParam.Id;
            }
            if (!_suppressSelectionRefresh)
                _ = DebouncedRefresh();
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
            await Refresh();
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
                ComboBoxFlow.SelectedItem = flowTemplate;
                FlowEngineManager.SelectedFlowParam = flowTemplate.Value;
                FlowEngineManager.TemplateFlowParamsIndex = TemplateFlow.Params.IndexOf(flowTemplate);
                FlowEngineConfig.Instance.LastSelectFlow = flowTemplate.Id;
            }
            finally
            {
                _suppressSelectionRefresh = false;
            }

            while (IsRefresh)
                await Task.Delay(20);

            await Refresh(allowEmptyFlow);
        }

        bool IsRefresh;
        public Task Refresh()
        {
            return Refresh(false);
        }

        private async Task Refresh(bool allowEmptyFlow)
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
                    item.nodeRunEvent -= UpdateMsg;
                    item.nodeEndEvent -= nodeEndEvent;
                }
                ResetNodeTitleProgress();
                View.FlowEngineControl.FlowClear();
                View.FlowEngineControl.LoadFromBase64(flowParam.DataBase64, MqttRCService.GetInstance().ServiceTokens);
                RefreshStartNodeSelection();

                FlowEngineManager.SelectedFlowParam = flowParam;

                foreach (var item in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
                {
                    CVBaseServerNodes.Insert(0,item);
                    item.nodeRunEvent += UpdateMsg;
                    item.nodeEndEvent += nodeEndEvent;
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
                node.nodeRunEvent -= UpdateMsg;
                node.nodeEndEvent -= nodeEndEvent;
            }

            ResetNodeTitleProgress();
            View.FlowEngineControl.LoadFromBase64(string.Empty);
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
            lock (_flowLifecycleSync)
            {
                if (_flowCompletionPending && FlowControl?.IsFlowRun != true)
                    _cancelFlowStartRequested = true;
            }

            while (_flowCompletionPending)
            {
                if (FlowControl?.IsFlowRun == true)
                {
                    log.Info("流程运行中触发刷新，先关闭当前流程。");
                    StopFlow();
                    await Task.Delay(100);
                    break;
                }
                await Task.Delay(20);
            }
        }

        private TemplateModel<FlowParam> GetSelectedFlowTemplate()
        {
            if (ComboBoxFlow.SelectedItem is TemplateModel<FlowParam> selectedTemplate)
                return selectedTemplate;

            if (ComboBoxFlow.SelectedValue is FlowParam flowParam)
                return TemplateFlow.Params.FirstOrDefault(a => a.Value?.Id == flowParam.Id);

            int selectedIndex = ComboBoxFlow.SelectedIndex;
            if (selectedIndex >= 0 && selectedIndex < TemplateFlow.Params.Count)
                return TemplateFlow.Params[selectedIndex];

            return null;
        }

        private void ComboBoxStartNode_DropDownOpened(object sender, EventArgs e)
        {
            RefreshStartNodeSelection();
        }

        public string? RefreshStartNodeSelection()
        {
            string? selectedName = ComboBoxStartNode.SelectedItem as string;
            string[] startNodeNames = View.FlowEngineControl.GetStartNodeNames();
            ComboBoxStartNode.ItemsSource = startNodeNames;
            if (!string.IsNullOrWhiteSpace(selectedName) && startNodeNames.Contains(selectedName))
            {
                ComboBoxStartNode.SelectedItem = selectedName;
            }
            else
            {
                ComboBoxStartNode.SelectedIndex = startNodeNames.Length > 0 ? 0 : -1;
            }
            return ComboBoxStartNode.SelectedItem as string;
        }

        /// <summary>
        /// 流程执行完成事件，外部（如定时任务）可订阅此事件以获取流程执行结果
        /// </summary>
        public event EventHandler<FlowControlData>? FlowExecutionCompleted;

        /// <summary>
        /// 启动流程并等待执行完成，返回流程执行结果。
        /// 如果流程未能启动（验证失败、已在运行等），返回 null。
        /// </summary>
        public async Task<FlowControlData?> RunFlowAndWaitAsync()
        {
            var tcs = new TaskCompletionSource<FlowControlData?>(TaskCreationOptions.RunContinuationsAsynchronously);
            string expectedSerialNumber = CreateFlowSerialNumber();

            EventHandler<FlowControlData>? handler = null;
            handler = (sender, data) =>
            {
                if (!string.Equals(data.SerialNumber, expectedSerialNumber, StringComparison.Ordinal))
                    return;

                FlowExecutionCompleted -= handler;
                tcs.TrySetResult(data);
            };

            FlowExecutionCompleted += handler;

            bool started;
            try
            {
                started = await RunFlowCoreAsync(expectedSerialNumber);
            }
            catch
            {
                FlowExecutionCompleted -= handler;
                throw;
            }

            if (!started)
            {
                FlowExecutionCompleted -= handler;
                return null;
            }
            return await tcs.Task;
        }

        private async void FlowControl_FlowCompleted(object? sender, FlowControlData FlowControlData)
        {
            if (!string.Equals(FlowControlData.SerialNumber, _activeFlowSerialNumber, StringComparison.Ordinal))
                return;

            try
            {
                stopwatch.Stop();
                timer.Change(Timeout.Infinite, 500); // 停止定时器
                MeasureBatchModel completedBatch = FlowEngineManager.Batch;
                string completedFlowName = FlowName;
                long completedGeneration = Volatile.Read(ref _executionGeneration);
                string? completedErrorNodeKey = string.IsNullOrWhiteSpace(FlowControlData.ErrorNodeId)
                    ? FlowControlData.ErrorNodeName
                    : FlowControlData.ErrorNodeId;

                completedBatch.FlowStatus = FlowControlData.FlowStatus;
                completedBatch.TotalTime = (int)stopwatch.ElapsedMilliseconds;
                completedBatch.Result = FlowControlData.Params;
                try
                {
                    using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                    Db.Updateable(completedBatch).ExecuteReturnEntity();
                }
                catch (Exception ex)
                {
                    log.Error("更新流程批次完成状态失败。", ex);
                }
                FlowNodeRecordDataBaseHelper.RecordFlowRun(
                    completedBatch.TId ?? 0,
                    completedFlowName,
                    FlowControlData.SerialNumber,
                    FlowControlData.FlowStatus,
                    stopwatch.ElapsedMilliseconds);

                string lastNodes = string.IsNullOrWhiteSpace(FlowControlData.ErrorNodeName)
                    ? (_runningNodeNames.IsEmpty ? Msg1 : string.Join(", ", _runningNodeNames.Values))
                    : FlowControlData.ErrorNodeName;
                _runningNodeNames.Clear();
                ResetNodeTitleProgress();
                string msg = $"{completedFlowName} {FlowControlData.EventName}{Environment.NewLine}{ColorVision.Engine.Properties.Resources.Flow_NodeLabel}{lastNodes}{Environment.NewLine}{FlowControlData.Params}{Environment.NewLine}{stopwatch.ElapsedMilliseconds}ms";
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
                FlowEngineManager.BatchProgress = 100;
                log.Info(msg);

                await WaitForTerminalNodeEndAsync(completedErrorNodeKey, completedGeneration);
                if (!string.Equals(FlowControlData.SerialNumber, _activeFlowSerialNumber, StringComparison.Ordinal))
                    return;

                PublishFlowExecutionCompleted(FlowControlData);

                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    Processing(completedBatch, completedFlowName);
                });
            }
            catch (Exception ex)
            {
                log.Error("处理流程完成事件失败。", ex);
            }
            finally
            {
                if (string.Equals(FlowControlData.SerialNumber, _activeFlowSerialNumber, StringComparison.Ordinal))
                {
                    FlowControl.FlowCompleted -= FlowControl_FlowCompleted;
                    lock (_flowLifecycleSync)
                    {
                        if (string.Equals(FlowControlData.SerialNumber, _activeFlowSerialNumber, StringComparison.Ordinal))
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
        }

        private void PublishFlowExecutionCompleted(FlowControlData data)
        {
            Delegate[] handlers = FlowExecutionCompleted?.GetInvocationList() ?? Array.Empty<Delegate>();
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
        
        private async Task<bool> PreProcessing(string flowName, string serialNumber)
        {
            return await PreProcessManager.GetInstance().ExecuteAsync(flowName, serialNumber, FlowEngineManager.CVBaseServerNodes);
        }
        
        private void Processing(MeasureBatchModel batch, string flowName)
        {
            try
            {
                // Find all matching post-process entries for this flow template name
                var matchingMetas = PostProcessManager.GetInstance().ProcessMetas
                    .Where(m => string.Equals(m.TemplateName, flowName, StringComparison.OrdinalIgnoreCase) && m.PostProcessor != null)
                    .ToList();


                if (matchingMetas.Count > 0)
                {
                    log.Info($"匹配到 {matchingMetas.Count} 个自定义流程处理 {flowName}");
                    
                    var ctx = new PostProcessContext
                    {
                        Batch = batch,
                        FlowName = flowName,
                    };

                    // Execute all matching processes sequentially
                    foreach (var meta in matchingMetas)
                    {
                        log.Info($"执行自定义流程 {meta.Name} -> {meta.ProcessTypeName}");
                        try
                        {
                            bool executed = meta.PostProcessor.Process(ctx);
                            if (!executed)
                            {
                                log.Warn($"自定义 IProcess {meta.Name} 执行返回失败");
                            }
                        }
                        catch (Exception ex)
                        {
                            log.Error($"自定义 IProcess {meta.Name} 执行异常", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log.Error("匹配/执行自定义 IProcess 出错", ex);
            }
        }

        private long LastFlowTime;

        string Msg1;
        private void UpdateMsg(object? sender)
        {
            if (FlowControl.IsFlowRun)
            {
                // Throttle: skip if a previous UI update is still pending
                if (Interlocked.CompareExchange(ref _pendingUiUpdate, 1, 0) != 0)
                    return;

                long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                TimeSpan elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);
                string elapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}:{elapsed.Milliseconds:D4}";
                string runningNodes = _runningNodeNames.IsEmpty ? Msg1 : string.Join(", ", _runningNodeNames.Values);
                string msg;
                if (LastFlowTime == 0 || LastFlowTime - elapsedMilliseconds < 0)
                {
                    msg = $"{ColorVision.Engine.Properties.Resources.Flow_ExecutingNodeLabel}{runningNodes}{Environment.NewLine}{ColorVision.Engine.Properties.Resources.Flow_ElapsedTimeLabel}{elapsedTime} {Environment.NewLine}";
                }
                else
                {
                    long remainingMilliseconds = LastFlowTime - elapsedMilliseconds;
                    TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                    string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                    msg = $"{FlowName}{ColorVision.Engine.Properties.Resources.Flow_LastExecutionLabel}{LastFlowTime} ms{Environment.NewLine}{ColorVision.Engine.Properties.Resources.Flow_ExecutingNodeLabel}{runningNodes}{Environment.NewLine}{ColorVision.Engine.Properties.Resources.Flow_ElapsedTimeLabel}{elapsedTime} {Environment.NewLine}{ColorVision.Engine.Properties.Resources.Flow_EstimatedRemainingLabel}{remainingTime}";
                }
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    Interlocked.Exchange(ref _pendingUiUpdate, 0);
                    UpdateRunningNodeTitleProgress();
                    if (LastFlowTime != 0)
                    {
                        double perfect = (double) elapsedMilliseconds / (double)LastFlowTime * 100;
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
                FlowNodeRecord record,
                FlowNodeMessage? message,
                long generation)
            {
                WriteKey = writeKey;
                Record = record;
                Message = message;
                Generation = generation;
            }

            public string WriteKey { get; }

            public FlowNodeRecord Record { get; }

            public FlowNodeMessage? Message { get; }

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

        private void nodeEndEvent(object sender, FlowEngineNodeEndEventArgs e)
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

                    if (e.RecvStatusCode == 0)
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
                    if (e != null && !string.IsNullOrEmpty(e.RecvMsgId))
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

                if (e?.RecvStatusCode != 0)
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
            }
        }

        private void UpdateMsg(object sender, FlowEngineNodeRunEventArgs e)
        {
            if (!string.Equals(e?.SerialNumber, _activeFlowSerialNumber, StringComparison.Ordinal))
                return;

            if (sender is CVCommonNode algorithmNode)
            {
                string writeKey = GetNodeExecutionKey(algorithmNode, e.SendMsgId, e.SerialNumber);
                LastNode = algorithmNode;
                long generation = Volatile.Read(ref _executionGeneration);
                _runningNodeCounts.AddOrUpdate(algorithmNode.NodeID, 1, (_, current) => current + 1);
                algorithmNode.IsSelected = true;
                Msg1 = algorithmNode.Title;
                _runningNodeNames[algorithmNode.NodeID] = algorithmNode.Title;
                _runningNodes[algorithmNode.NodeID] = algorithmNode;
                _nodeStartedAt[algorithmNode.NodeID] = Environment.TickCount64;
                algorithmNode.TitleProgressColor = System.Drawing.Color.DeepSkyBlue;
                algorithmNode.TitleProgress = _nodeExpectedDurations.TryGetValue(algorithmNode.NodeID, out long expectedDuration)
                    && expectedDuration > 0 ? 0f : -1f;
                UpdateMsg(sender);

                int batchId = FlowEngineManager.Batch?.Id ?? 0;
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

                var execution = new PendingNodeExecution(writeKey, record, msg, generation);
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
                });
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


        string FlowName;
        public async Task RunFlow()
        {
            await RunFlowCoreAsync();
        }

        private static string CreateFlowSerialNumber()
        {
            return DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
        }

        private async Task<bool> RunFlowCoreAsync(string? requestedSerialNumber = null)
        {
            bool requiresServices = View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>().Any();
            if (requiresServices && !MqttRCService.GetInstance().IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(),ColorVision.Engine.Properties.Resources.RegistryCenterNotConnected);
                return false;
            }

            if (requiresServices && MqttRCService.GetInstance().ServiceTokens.Count == 0)
            {
                MqttRCService.GetInstance().QueryServices();
                View.ShowExecutionSummary(ColorVision.Engine.Properties.Resources.TokenEmpty_RefreshingToken_PleaseRetry);
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.TokenEmpty_RefreshingToken_PleaseRetry);
                return false;
            }

            string? startNodeName = RefreshStartNodeSelection();
            if (string.IsNullOrWhiteSpace(startNodeName))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), ColorVision.Engine.Properties.Resources.WorkflowStartNodeNotFound_RunFailed, "ColorVision");
                return false;
            }

            int selectedIndex = ComboBoxFlow.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= TemplateFlow.Params.Count)
            {
                View.ShowExecutionSummary(ColorVision.Engine.Properties.Resources.Flow_NoValidFlowTemplateSelected);
                log.Warn("未选择有效的流程模板");
                return false;
            }

            string flowName = ComboBoxFlow.Text;
            FlowParam selectedFlowParam = TemplateFlow.Params[selectedIndex].Value;
            string sn = requestedSerialNumber ?? CreateFlowSerialNumber();
            using CancellationTokenSource flowStartCts = new CancellationTokenSource();
            lock (_flowLifecycleSync)
            {
                if (_flowCompletionPending || FlowControl.IsFlowRun)
                {
                    log.Info("流程正在运行或正在启动");
                    return false;
                }
                _activeFlowSerialNumber = sn;
                _flowCompletionPending = true;
                _cancelFlowStartRequested = false;
                _flowStartCts = flowStartCts;
            }

            FlowEditorOperations.ClearSelection(View.STNodeEditorMain);

            bool started = false;
            MeasureBatchModel? preparedBatch = null;
            FlowStatus unstartedBatchStatus = FlowStatus.Failed;
            string unstartedBatchResult = "Flow start failed";
            try
            {
                FlowName = flowName;
                LastFlowTime = await Task.Run(
                    () => FlowNodeRecordDataBaseHelper.GetLastCompletedFlowElapsed(selectedFlowParam.Id, flowName));
                ResetNodeTitleProgress();
                await LoadNodeExpectedDurationsAsync();
                if (!CanContinueFlowStart(sn))
                    return false;

                foreach (var item in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
                {
                    item.TitleColor = System.Drawing.Color.Blue;
                }
                ClearFlowRuntimeData();

                LastNode = null;
                InvalidateExecutionPresentation();
                View.ShowExecutionSummary("Run " + flowName);
                FlowEngineManager.BatchProgress = 0;

                _pendingNodeExecutions.Clear();
                _runningNodeNames.Clear();
                _runningNodeCounts.Clear();
                lock (_nodeWriteSync)
                    _nodeWriteTasks.Clear();
                FlowControl.FlowCompleted -= FlowControl_FlowCompleted;
                FlowControl.FlowCompleted += FlowControl_FlowCompleted;

                stopwatch.Restart();
                timer.Change(0, 100); // 启动定时器

                FlowEngineManager.Batch = new MeasureBatchModel() { TId = selectedFlowParam.Id, Name = sn, Code = sn };
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                FlowEngineManager.Batch.Id = Db.Insertable(FlowEngineManager.Batch).ExecuteReturnIdentity();
                preparedBatch = FlowEngineManager.Batch;

                bool preresult = await PreProcessing(flowName, sn);
                if (!CanContinueFlowStart(sn))
                {
                    unstartedBatchStatus = FlowStatus.Canceled;
                    unstartedBatchResult = ColorVision.Engine.Properties.Resources.ExecutionCancelled;
                    return false;
                }
                if (!preresult)
                {
                    unstartedBatchResult = ColorVision.Engine.Properties.Resources.Flow_PreprocessFailedCancelled;
                    View.ShowExecutionSummary(ColorVision.Engine.Properties.Resources.Flow_PreprocessFailedCancelled);
                    log.Warn("预处理失败，流程取消执行");
                    return false;
                }

                if (!await FlowControl.TryStartAsync(startNodeName, sn, flowStartCts.Token))
                {
                    unstartedBatchResult = FlowMqttNotReadyMessage;
                    View.ShowExecutionSummary(FlowMqttNotReadyMessage);
                    return false;
                }

                lock (_flowLifecycleSync)
                {
                    if (!string.Equals(sn, _activeFlowSerialNumber, StringComparison.Ordinal)
                        || _cancelFlowStartRequested)
                    {
                        FlowControl.Stop();
                        unstartedBatchStatus = FlowStatus.Canceled;
                        unstartedBatchResult = ColorVision.Engine.Properties.Resources.ExecutionCancelled;
                        return false;
                    }
                    started = true;
                }
                return true;
            }
            catch (OperationCanceledException) when (flowStartCts.IsCancellationRequested)
            {
                unstartedBatchStatus = FlowStatus.Canceled;
                unstartedBatchResult = ColorVision.Engine.Properties.Resources.ExecutionCancelled;
                return false;
            }
            catch (Exception ex)
            {
                unstartedBatchResult = ex.Message;
                throw;
            }
            finally
            {
                lock (_flowLifecycleSync)
                {
                    if (ReferenceEquals(_flowStartCts, flowStartCts))
                        _flowStartCts = null;
                }
                if (!started)
                {
                    if (preparedBatch?.Id > 0)
                        FinalizeUnstartedBatch(preparedBatch, unstartedBatchStatus, unstartedBatchResult);
                    FlowControl.FlowCompleted -= FlowControl_FlowCompleted;
                    stopwatch.Stop();
                    timer.Change(Timeout.Infinite, 500);
                    lock (_flowLifecycleSync)
                    {
                        if (string.Equals(sn, _activeFlowSerialNumber, StringComparison.Ordinal))
                        {
                            _activeFlowSerialNumber = null;
                            _flowCompletionPending = false;
                            _cancelFlowStartRequested = false;
                        }
                    }
                }
            }
        }

        private void FinalizeUnstartedBatch(MeasureBatchModel batch, FlowStatus status, string result)
        {
            try
            {
                batch.FlowStatus = status;
                batch.TotalTime = (int)stopwatch.ElapsedMilliseconds;
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
                    && !_cancelFlowStartRequested;
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

        public void StopFlow()
        {
            FlowControl.FlowCompleted -= FlowControl_FlowCompleted;
            bool wasRunning = FlowControl?.IsFlowRun == true;
            CancellationTokenSource? flowStartCts = null;
            lock (_flowLifecycleSync)
            {
                if (_flowCompletionPending && !wasRunning)
                {
                    _cancelFlowStartRequested = true;
                    flowStartCts = _flowStartCts;
                }
                else
                {
                    _activeFlowSerialNumber = null;
                    _flowCompletionPending = false;
                    _cancelFlowStartRequested = false;
                }
            }
            flowStartCts?.Cancel();
            InvalidateExecutionPresentation();
            if (wasRunning)
                FlowControl.Stop();
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器
            ResetNodeTitleProgress();

            if (wasRunning && FlowEngineManager.Batch?.Id > 0)
            {
                FlowEngineManager.Batch.FlowStatus = FlowStatus.Canceled;
                FlowEngineManager.Batch.TotalTime = (int)stopwatch.ElapsedMilliseconds;
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                Db.Updateable(FlowEngineManager.Batch).ExecuteCommand();
            }
            View.ShowExecutionSummary(ColorVision.Engine.Properties.Resources.ExecutionCancelled);

        }

        public void Dispose()
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            MqttRCService.GetInstance().ServiceTokensUpdated -= MqttRCService_ServiceTokensUpdated;
            ResetNodeTitleProgress();
            timer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
