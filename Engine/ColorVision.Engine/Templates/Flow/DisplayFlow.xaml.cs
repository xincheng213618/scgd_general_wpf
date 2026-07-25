#pragma warning disable CS4014,CS8601,CS8602,CS8603,CS8625
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.Engine.Services.Flow;
using ColorVision.Engine.Services.RC;
using ColorVision.SocketProtocol;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Extension;
using ColorVision.UI.ServiceHost;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace ColorVision.Engine.Templates.Flow
{

    public class FlowSocketMsgHandle : ISocketJsonHandler
    {
        public string EventName => "Flow";
        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            if (TemplateFlow.Params.FirstOrDefault(a => a.Key == request.Params)?.Value is FlowParam flowParam)
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    FlowEngineManager.GetInstance().DisplayFlow.ComboBoxFlow.SelectedValue = flowParam;
                    FlowEngineManager.GetInstance().DisplayFlow.RunFlow();
                });
                return new SocketResponse { Code = 200, Msg = $"Run {request.Params}", EventName = EventName };
            }
            else
            {
                return new SocketResponse { Code = -1, Msg = $"Cant Find Flow {request.Params}", EventName = EventName };
            }
        }
    }

    public partial class DisplayFlow : UserControl, IDisPlayControl, IIcon, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DisplayFlow));

        public ViewFlow View => FlowEngineManager.View;

        public FlowEngineManager FlowEngineManager { get; set; }
        public FlowControl FlowControl => FlowEngineManager.FlowControl;

        public string DisPlayName => "Flow";
        public static FlowEngineConfig Config => FlowEngineConfig.Instance;

        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();
        private int _pendingUiUpdate;
        private CancellationTokenSource _refreshCts;
        private bool _suppressSelectionRefresh;
        private volatile bool _flowCompletionPending;
        private static readonly string[] RestartServiceNames = ["RegistrationCenterService", "CVMainService_x64", "CVMainService_dev"];

        public DisplayFlow(FlowEngineManager flowEngineManager)
        {
            FlowEngineManager = flowEngineManager;
            InitializeComponent();
        }


        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = FlowEngineManager;


            this.AddViewConfig(View, ColorVision.Engine.Properties.Resources.Workflow);
            View.DisplayFlow = this;

            Unselected += (s, e) =>
            {
                View.STNodeEditorHelper.HidePropertyEditor();
            };
            ComboBoxFlow.SelectionChanged += (s, e) =>
            {
                if (ComboBoxFlow.SelectedValue is FlowParam flowParam)
                {
                    FlowEngineManager.SlectFlowParam = flowParam;
                    FlowEngineConfig.Instance.LastSelectFlow = flowParam.Id;
                    if (FlowEngineConfig.Instance.FlowRunTime.TryGetValue(flowParam.Name, out long time))
                        LastFlowTime = time;

                }
                if (!_suppressSelectionRefresh)
                {
                    _ = DebouncedRefresh();
                }
            };


            this.ApplyChangedSelectedColor(DisPlayBorder);
            EnsureTimedButtonOperations();
            ServiceConfig.Instance.PropertyChanged += ServiceConfig_PropertyChanged;

            this.Loaded += FlowDisplayControl_Loaded;
            View.RefreshFlow += (s, e) =>
            {
                _=Refresh();
            };
            
            MqttRCService.GetInstance().ServiceTokensUpdated += (s, e) => FlowNodeManager.Instance.UpdateDevice(MqttRCService.GetInstance().ServiceTokens);
            timer = new Timer(UpdateMsg, null, 0, 100);
            timer.Change(Timeout.Infinite, 100); // 停止定时器

        }

        private TimedButtonOperationRegistry EnsureTimedButtonOperations()
        {
            TimedButtonOperationRegistry operations = this.GetTimedButtonOperations(actionKey => $"flow:{actionKey}");
            operations.Register(RestartServicesButton, "restart-cv-windows-services", options =>
            {
                options.ContentFactory = stats => TimedButtonOperationTextFormatter.BuildCompactContent(BuildRestartServicesButtonText(), stats);
                options.ToolTipFactory = stats => TimedButtonOperationTextFormatter.BuildTooltip(BuildRestartServicesButtonText(), stats);
                options.RunningText = Properties.Resources.RestartService;
            });
            return operations;
        }

        private static string BuildRestartServicesButtonText()
        {
            string version = ServiceConfig.Instance.RegistrationCenterServiceInfo.FileVersion;
            return string.IsNullOrWhiteSpace(version) ? Properties.Resources.RestartService : string.Format(Properties.Resources.Flow_RestartServiceVersionFormat, version);
        }

        private void ServiceConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!string.Equals(e.PropertyName, nameof(ServiceConfig.RegistrationCenterServiceInfo), StringComparison.Ordinal))
                return;

            if (Dispatcher.CheckAccess())
            {
                RefreshRestartServicesButton();
            }
            else
            {
                _ = Dispatcher.BeginInvoke(new Action(RefreshRestartServicesButton));
            }
        }

        private void RefreshRestartServicesButton()
        {
            this.TryGetTimedButtonOperations()?.RefreshIdleState(RestartServicesButton);
        }

        private double GetExpectedRestartDurationMs()
        {
            TimedButtonOperationStats? stats = EnsureTimedButtonOperations().Get(RestartServicesButton)?.CurrentStats;
            if (stats?.SuccessCount > 0 && stats.AverageElapsedMs > 0) return stats.AverageElapsedMs;
            if (stats?.WarmupCount > 0 && stats.WarmupElapsedMs > 0) return stats.WarmupElapsedMs;
            return 15000;
        }

        private async void Button_RestartServices_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.Flow_ConfirmRestartColorVisionServices, "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            TimedButtonOperationScope? operationScope = EnsureTimedButtonOperations().Begin(RestartServicesButton, GetExpectedRestartDurationMs(), Properties.Resources.RestartService);
            bool success = false;
            try
            {
                await RestartColorVisionServicesAsync();
                success = true;
            }
            catch (Exception ex)
            {
                log.Error("重启 ColorVision 服务失败", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
            }
            finally
            {
                operationScope?.Complete(success);
                this.TryGetTimedButtonOperations()?.RefreshIdleState(RestartServicesButton);
            }
        }

        public static async Task RestartColorVisionServicesAsync()
        {
            foreach (string serviceName in RestartServiceNames)
                await RunServiceHostCommandAsync(serviceName, start: false);

            await Task.Delay(1000);

            foreach (string serviceName in RestartServiceNames)
                await RunServiceHostCommandAsync(serviceName, start: true);

            await Task.Delay(1000);
            await RefreshServiceConnectionAsync();
        }

        private static async Task RunServiceHostCommandAsync(string serviceName, bool start)
        {
            ServiceHostResponse response = start
                ? await ColorVisionServiceHostClient.Default.StartServiceAsync(serviceName, timeoutSeconds: 45, timeout: TimeSpan.FromSeconds(60))
                : await ColorVisionServiceHostClient.Default.StopServiceAsync(serviceName, timeoutSeconds: 45, timeout: TimeSpan.FromSeconds(60));

            if (!response.Success)
                throw new InvalidOperationException(string.Format(start ? Properties.Resources.Flow_StartServiceFailed : Properties.Resources.Flow_StopServiceFailed, serviceName, response.Message));
        }

        private static async Task RefreshServiceConnectionAsync()
        {
            ServiceConfig.Instance.RefreshInstalledServices();
            MqttRCService rcService = MqttRCService.GetInstance();
            rcService.Regist();
            for (int i = 0; i < 20 && !rcService.IsConnect; i++)
                await Task.Delay(250);

            if (rcService.IsConnect)
                rcService.QueryServices();
            else
                log.Warn("服务重启完成，但注册中心重新连接未确认。");
        }

        private void FlowDisplayControl_Loaded(object sender, RoutedEventArgs e)
        {
            var s = TemplateFlow.Params.FirstOrDefault(a => a.Id == FlowEngineConfig.Instance.LastSelectFlow);
            if (s != null)
            {
                ComboBoxFlow.SelectedItem = s;
            }
            else
            {
                ComboBoxFlow.SelectedIndex = 0;
            }
            this.Loaded -= FlowDisplayControl_Loaded;
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

        private async Task SelectFlowTemplateAsync(TemplateModel<FlowParam> flowTemplate, bool allowEmptyFlow = false)
        {
            CancelPendingRefresh();

            _suppressSelectionRefresh = true;
            try
            {
                ComboBoxFlow.SelectedItem = flowTemplate;
                FlowEngineManager.SlectFlowParam = flowTemplate.Value;
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

                FlowEngineManager.SlectFlowParam = flowParam;

                foreach (var item in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
                {
                    CVBaseServerNodes.Insert(0,item);
                    item.nodeRunEvent += UpdateMsg;
                    item.nodeEndEvent += nodeEndEvent;
                }
                View.STNodeEditorHelper.AddNodeContext();
                FlowEngineManager.PublishCopilotContext();
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
            FlowEngineManager.CVBaseServerNodes.Clear();
            FlowEngineManager.SlectFlowParam = flowParam;
            if (flowParam == null)
                FlowEngineManager.TemplateFlowParamsIndex = -1;
            View.STNodeEditorHelper.AddNodeContext();
            FlowEngineManager.PublishCopilotContext();
        }

        private async Task CloseRunningFlowBeforeRefreshAsync()
        {
            if (FlowControl?.IsFlowRun == true)
            {
                log.Info("流程运行中触发刷新，先关闭当前流程。");
                StopFlow();
                await Task.Delay(100);
            }

            while (_flowCompletionPending)
                await Task.Delay(20);
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

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }

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

            EventHandler<FlowControlData>? handler = null;
            handler = (sender, data) =>
            {
                FlowExecutionCompleted -= handler;
                tcs.TrySetResult(data);
            };

            FlowExecutionCompleted += handler;

            await RunFlow();

            // 如果流程未能启动（验证失败、正在运行、无模板等），事件不会触发
            if (!FlowControl.IsFlowRun)
            {
                FlowExecutionCompleted -= handler;
                return null;
            }

            return await tcs.Task;
        }

        public async Task<FlowControlData?> RunFlowAndWaitAsync(TemplateModel<FlowParam> flowTemplate)
        {
            if (flowTemplate == null)
            {
                return null;
            }

            await SelectFlowTemplateAsync(flowTemplate);
            return await RunFlowAndWaitAsync();
        }

        private async void FlowControl_FlowCompleted(object? sender, FlowControlData FlowControlData)
        {
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器
            MeasureBatchModel completedBatch = FlowEngineManager.Batch;
            string completedFlowName = FlowName;
            long completedGeneration = Volatile.Read(ref _executionGeneration);

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

            FlowEngineConfig.Instance.FlowRunTime[completedFlowName] = stopwatch.ElapsedMilliseconds;
            FlowControl.FlowCompleted -= FlowControl_FlowCompleted;

            string lastNodes = string.IsNullOrWhiteSpace(FlowControlData.ErrorNodeName)
                ? (_runningNodeNames.IsEmpty ? Msg1 : string.Join(", ", _runningNodeNames.Values))
                : FlowControlData.ErrorNodeName;
            _runningNodeNames.Clear();
            ResetNodeTitleProgress();
            string msg = $"{completedFlowName} {FlowControlData.EventName}{Environment.NewLine}{ColorVision.Engine.Properties.Resources.Flow_NodeLabel}{lastNodes}{Environment.NewLine}{FlowControlData.Params}{Environment.NewLine}{stopwatch.ElapsedMilliseconds}ms";
            CVCommonNode? failedNode;
            lock (_executionStateSync)
            {
                _completedErrorNodeName = FlowControlData.ErrorNodeName;
                _completedSummaryMessage = msg;
                failedNode = _lastFailedNode;
            }
            View.ShowExecutionSummary(msg);
            if (failedNode != null
                && STNodeEditorHelper.IsExecutionNodeNameMatch(failedNode, FlowControlData.ErrorNodeName))
            {
                _ = ShowExecutionSummaryAfterNodeWritesAsync(
                    failedNode,
                    FlowControlData.ErrorNodeName,
                    msg,
                    completedGeneration);
            }
            FlowEngineManager.BatchProgress = 100;
            log.Info(msg);

            await WaitForTerminalNodeEndAsync(FlowControlData.ErrorNodeName, completedGeneration);
            _flowCompletionPending = false;
            FlowExecutionCompleted?.Invoke(this, FlowControlData);

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                Processing(completedBatch, completedFlowName);
            });
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

        public ImageSource Icon { get => _Icon; set { _Icon = value; } }
        private ImageSource _Icon;

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

        private readonly ConcurrentDictionary<string, FlowNodeRecord> _nodeRecords = new ConcurrentDictionary<string, FlowNodeRecord>();
        private readonly ConcurrentDictionary<string, string> _runningNodeNames = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, FlowNodeMessage> _nodeMessages = new ConcurrentDictionary<string, FlowNodeMessage>();
        private readonly ConcurrentDictionary<string, long> _nodeExpectedDurations = new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, long> _nodeStartedAt = new ConcurrentDictionary<string, long>();
        private readonly ConcurrentDictionary<string, CVCommonNode> _runningNodes = new ConcurrentDictionary<string, CVCommonNode>();
        private readonly ConcurrentDictionary<string, Task> _nodeWriteTasks = new ConcurrentDictionary<string, Task>();
        private readonly ConcurrentDictionary<string, long> _nodeExecutionGenerations = new ConcurrentDictionary<string, long>();
        private readonly object _nodeWriteSync = new object();
        private readonly object _executionStateSync = new object();
        private CVCommonNode? _lastFailedNode;
        private string? _completedErrorNodeName;
        private string? _completedSummaryMessage;
        private TaskCompletionSource<bool>? _terminalNodeEndCompletion;
        private long _executionGeneration;

        private void QueueNodeWrite(string nodeId, Action write)
        {
            lock (_nodeWriteSync)
            {
                Task nextWrite = _nodeWriteTasks.TryGetValue(nodeId, out Task? previous)
                    ? previous.ContinueWith(
                    _ => write(),
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default)
                    : Task.Run(write);
                _nodeWriteTasks[nodeId] = nextWrite;
            }
        }

        private async Task ShowExecutionSummaryAfterNodeWritesAsync(
            CVCommonNode node,
            string? completedErrorNodeName,
            string? completedSummaryMessage,
            long generation)
        {
            if (_nodeWriteTasks.TryGetValue(node.NodeID, out Task? pendingWrite))
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
                        && STNodeEditorHelper.IsExecutionNodeNameMatch(node, _completedErrorNodeName)
                        && STNodeEditorHelper.IsExecutionNodeNameMatch(node, completedErrorNodeName);
                }
                if (isCurrent)
                {
                    View.ShowExecutionSummary(
                        completedSummaryMessage ?? View.logTextBox.Text,
                        completedErrorNodeName,
                        node);
                }
            });
        }

        private async Task WaitForTerminalNodeEndAsync(string? errorNodeName, long generation)
        {
            if (string.IsNullOrWhiteSpace(errorNodeName))
            {
                await Task.Delay(100);
                return;
            }

            TaskCompletionSource<bool>? completion = null;
            lock (_executionStateSync)
            {
                if (generation != Volatile.Read(ref _executionGeneration)
                    || (_lastFailedNode != null
                        && STNodeEditorHelper.IsExecutionNodeNameMatch(_lastFailedNode, errorNodeName)))
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
            _nodeExecutionGenerations.Clear();
            lock (_executionStateSync)
            {
                _lastFailedNode = null;
                _completedErrorNodeName = null;
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

        internal Task SelectCreatedFlowTemplateAsync(TemplateModel<FlowParam> flowTemplate)
        {
            return SelectFlowTemplateAsync(flowTemplate, true);
        }

        private void ResetNodeTitleProgress()
        {
            foreach (CVCommonNode node in View.STNodeEditorMain.Nodes.OfType<CVCommonNode>())
                node.TitleProgress = -1f;

            _runningNodes.Clear();
            _nodeStartedAt.Clear();
        }

        private void nodeEndEvent(object sender, FlowEngineNodeEndEventArgs e)
        {
            if (sender is CVCommonNode algorithmNode)
            {
                if (!_nodeExecutionGenerations.TryRemove(algorithmNode.NodeID, out long generation)
                    || generation != Volatile.Read(ref _executionGeneration))
                {
                    return;
                }

                algorithmNode.TitleProgress = -1f;
                _runningNodes.TryRemove(algorithmNode.NodeID, out _);
                long elapsedFromClock = 0;
                if (_nodeStartedAt.TryRemove(algorithmNode.NodeID, out long startedAt))
                    elapsedFromClock = Math.Max(0, Environment.TickCount64 - startedAt);

                if (e != null)
                {
                    algorithmNode.IsSelected = false;

                    if (e.RecvStatusCode == 0)
                    {
                        algorithmNode.TitleColor = System.Drawing.Color.Green;
                    }
                    else
                    {
                        algorithmNode.TitleColor = System.Drawing.Color.Red;
                    }
                }

                _runningNodeNames.TryRemove(algorithmNode.NodeID, out _);

                string nodeKey = algorithmNode.NodeID;
                if (_nodeRecords.TryRemove(nodeKey, out FlowNodeRecord record))
                {
                    record.EndTime = DateTime.Now;
                    record.ElapsedMs = (long)(record.EndTime.Value - record.StartTime).TotalMilliseconds;
                    if (record.ElapsedMs > 0)
                        _nodeExpectedDurations[algorithmNode.NodeID] = record.ElapsedMs;
                    QueueNodeWrite(algorithmNode.NodeID, () => FlowNodeRecordDataBaseHelper.Update(record));
                }
                else if (elapsedFromClock > 0)
                {
                    _nodeExpectedDurations[algorithmNode.NodeID] = elapsedFromClock;
                }

                // Update the existing message with received MQTT response
                if (_nodeMessages.TryRemove(algorithmNode.NodeID, out FlowNodeMessage nodeMsg))
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
                    QueueNodeWrite(algorithmNode.NodeID, () => FlowNodeRecordDataBaseHelper.UpdateMessage(nodeMsg));
                }

                if (e?.RecvStatusCode != 0)
                {
                    string? completedErrorNodeName;
                    string? completedSummaryMessage;
                    bool matchesCompletedFailure;
                    lock (_executionStateSync)
                    {
                        _lastFailedNode = algorithmNode;
                        completedErrorNodeName = _completedErrorNodeName;
                        completedSummaryMessage = _completedSummaryMessage;
                        matchesCompletedFailure = STNodeEditorHelper.IsExecutionNodeNameMatch(
                            algorithmNode,
                            completedErrorNodeName);
                        if (matchesCompletedFailure)
                            _terminalNodeEndCompletion?.TrySetResult(true);
                    }

                    if (matchesCompletedFailure)
                    {
                        _ = ShowExecutionSummaryAfterNodeWritesAsync(
                            algorithmNode,
                            completedErrorNodeName,
                            completedSummaryMessage,
                            generation);
                    }
                }
            }
        }

        private void UpdateMsg(object sender, FlowEngineNodeRunEventArgs e)
        {
            if (sender is CVCommonNode algorithmNode)
            {
 
                LastNode = algorithmNode;
                _nodeExecutionGenerations[algorithmNode.NodeID] = Volatile.Read(ref _executionGeneration);
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
                    SerialNumber = FlowControl.SerialNumber,
                    NodeId = algorithmNode.NodeID,
                    NodeName = algorithmNode.OnGetDrawTitle(),
                    NodeType = algorithmNode.NodeType,
                    StartTime = DateTime.Now,
                };
                _nodeRecords[algorithmNode.NodeID] = record;
                QueueNodeWrite(algorithmNode.NodeID, () =>
                {
                    int insertId = FlowNodeRecordDataBaseHelper.Insert(record);
                    if (insertId <= 0)
                        _nodeRecords.TryRemove(algorithmNode.NodeID, out _);
                });

                // Record sent MQTT message (combined send/recv record)
                if (e != null && !string.IsNullOrEmpty(e.SendMsgId))
                {
                    var msg = new FlowNodeMessage
                    {
                        BatchId = batchId,
                        SerialNumber = FlowControl.SerialNumber,
                        NodeId = algorithmNode.NodeID,
                        NodeName = algorithmNode.OnGetDrawTitle(),
                        MsgId = e.SendMsgId,
                        EventName = e.SendEventName,
                        SendTopic = e.SendTopic,
                        SendPayload = e.SendPayload,
                        SendTime = DateTime.Now,
                        State = FlowMessageState.Sended
                    };
                    _nodeMessages[algorithmNode.NodeID] = msg;
                    QueueNodeWrite(algorithmNode.NodeID, () =>
                    {
                        int id = FlowNodeRecordDataBaseHelper.InsertMessage(msg);
                        if (id <= 0)
                            _nodeMessages.TryRemove(algorithmNode.NodeID, out _);
                    });
                }
            }
        }


        private void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {
            RunFlow();
        }


        string FlowName;
        public async Task RunFlow()
        {
            while (_flowCompletionPending)
                await Task.Delay(20);

            if (!MqttRCService.GetInstance().IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(),ColorVision.Engine.Properties.Resources.RegistryCenterNotConnected);
                return;
            }

            if (FlowControl.IsFlowRun)
            {
                log.Info("流程正在运行");
                return;
            }
            if (MqttRCService.GetInstance().ServiceTokens.Count == 0)
            {
                MqttRCService.GetInstance().QueryServices();
                View.ShowExecutionSummary(ColorVision.Engine.Properties.Resources.TokenEmpty_RefreshingToken_PleaseRetry);
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.TokenEmpty_RefreshingToken_PleaseRetry);
                return;
            }
            FlowName = ComboBoxFlow.Text;
            LastFlowTime = FlowEngineConfig.Instance.FlowRunTime.TryGetValue(ComboBoxFlow.Text, out long time) ? time : 0;

            string startNode = View.FlowEngineControl.GetStartNodeName();
            if (string.IsNullOrWhiteSpace(startNode))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), ColorVision.Engine.Properties.Resources.WorkflowStartNodeNotFound_RunFailed, "ColorVision");
                return;
            }

            ResetNodeTitleProgress();
            await LoadNodeExpectedDurationsAsync();

            foreach (var item in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
            {
                item.TitleColor = System.Drawing.Color.Blue;
            }
            ClearFlowRuntimeData();

            LastNode = null;
            InvalidateExecutionPresentation();
            View.ShowExecutionSummary("Run " + ComboBoxFlow.Text);
            FlowEngineManager.BatchProgress = 0;

            _nodeRecords.Clear();
            _runningNodeNames.Clear();
            _nodeMessages.Clear();
            lock (_nodeWriteSync)
                _nodeWriteTasks.Clear();
            FlowControl.FlowCompleted -= FlowControl_FlowCompleted;
            FlowControl.FlowCompleted += FlowControl_FlowCompleted;
            string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");

            stopwatch.Restart();
            stopwatch.Start();

            timer.Change(0, 100); // 启动定时器
            int selectedIndex = ComboBoxFlow.SelectedIndex;
            if (selectedIndex < 0 || selectedIndex >= TemplateFlow.Params.Count)
            {
                FlowControl.FlowCompleted -= FlowControl_FlowCompleted;
                stopwatch.Stop();
                timer.Change(Timeout.Infinite, 500);
                View.ShowExecutionSummary(ColorVision.Engine.Properties.Resources.Flow_NoValidFlowTemplateSelected);
                log.Warn("未选择有效的流程模板");
                return;
            }
            FlowEngineManager.Batch = new MeasureBatchModel() { TId = TemplateFlow.Params[selectedIndex].Id, Name = sn, Code = sn };
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            FlowEngineManager.Batch.Id = Db.Insertable(FlowEngineManager.Batch).ExecuteReturnIdentity();

            // Execute pre-processors before flow starts

            bool preresult = await PreProcessing(FlowName, sn);
            if (!preresult)
            {
                FlowControl.FlowCompleted -= FlowControl_FlowCompleted;
                stopwatch.Stop();
                timer.Change(Timeout.Infinite, 500);
                View.ShowExecutionSummary(ColorVision.Engine.Properties.Resources.Flow_PreprocessFailedCancelled);
                log.Warn("预处理失败，流程取消执行");
                return;
            }

            _flowCompletionPending = true;
            try
            {
                FlowControl.Start(sn);
            }
            catch
            {
                _flowCompletionPending = false;
                FlowControl.FlowCompleted -= FlowControl_FlowCompleted;
                stopwatch.Stop();
                timer.Change(Timeout.Infinite, 500);
                throw;
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

        private void Button_FlowStop_Click(object sender, RoutedEventArgs e)
        {
            StopFlow();
        }

        public void StopFlow()
        {
            FlowControl.FlowCompleted -= FlowControl_FlowCompleted;
            _flowCompletionPending = false;
            InvalidateExecutionPresentation();
            FlowControl?.Stop();
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器
            ResetNodeTitleProgress();

            FlowEngineManager.Batch.FlowStatus = FlowStatus.Canceled;
            FlowEngineManager.Batch.TotalTime = (int)stopwatch.ElapsedMilliseconds;
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            Db.Updateable(FlowEngineManager.Batch).ExecuteCommand();
            View.ShowExecutionSummary(ColorVision.Engine.Properties.Resources.ExecutionCancelled);

        }

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked =!ToggleButton0.IsChecked;
        }


        public void Dispose()
        {
            _refreshCts?.Cancel();
            _refreshCts?.Dispose();
            ServiceConfig.Instance.PropertyChanged -= ServiceConfig_PropertyChanged;
            this.DisposeTimedButtonOperations();
            ResetNodeTitleProgress();
            timer.Dispose();
            GC.SuppressFinalize(this);
        }

        private void Button_Click_Refresh(object sender, RoutedEventArgs e)
        {
            _= Refresh();

        }
    }
}
