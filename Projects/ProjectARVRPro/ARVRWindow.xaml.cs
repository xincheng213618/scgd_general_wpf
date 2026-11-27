using LocalizedText = global::ProjectARVRPro.DisplayText;
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PreProcess;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.FlowProcessing;
using ColorVision.ImageEditor;
using ColorVision.SocketProtocol;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Controls;
using ColorVision.UI.LogImp;
using FlowEngineLib;
using FlowEngineLib.Base;
using HandyControl.Data;
using log4net;
using Newtonsoft.Json;
using ProjectARVRPro.Exports;
using ProjectARVRPro.ImageExport;
using ProjectARVRPro.LegacyARVR;
using ProjectARVRPro.Process;
using ProjectARVRPro.Services;
using SqlSugar;
using ST.Library.UI.NodeEditor;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProjectARVRPro
{
    public class ARVRWindowConfig : WindowConfig
    {
        public static ARVRWindowConfig Instance => ConfigService.Instance.GetRequiredService<ARVRWindowConfig>();

        [Browsable(false)]
        public string[] ResultColumnOrder { get; set; } = [];
    }

    public partial class ARVRWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ARVRWindow));
        private const string FlowStartRejectedMessage = "FlowStartRejected";
        private const int ExecutionStatusRefreshIntervalMs = 100;

        public static ProjectARVRProConfig ProjectConfig => ProjectARVRProConfig.Instance;

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();

        public static ObservableCollection<ProjectARVRReuslt> ViewResluts { get; set; } = ViewResultManager.ViewResluts;

        public static ProcessManager ProcessManager => ProcessManager.GetInstance();
        public ObservableCollection<ProcessMeta> ProcessMetas => ProcessManager.ProcessMetas;

        private readonly PictureSwitchService _pictureSwitchService;
        private readonly string[] _initialResultColumnOrder;

        private readonly ResultImagePlaceholderCache _resultImagePlaceholderCache = new();
        private long _resultImagePresentationVersion;
        private CancellationTokenSource? _resultImagePresentationCancellation;

        private static readonly HashSet<string> ResultOverlayConfigNames =
        [
            nameof(ProjectARVRProConfig.ResultOverlayShowName),
            nameof(ProjectARVRProConfig.ResultOverlayShowDetail),
            nameof(ProjectARVRProConfig.ResultOverlayFontSize),
            nameof(ProjectARVRProConfig.ResultOverlayAutoRefresh)
        ];

        public ARVRWindow()
        {
            _pictureSwitchService = new PictureSwitchService(ThunderbirdSerialController.GetInstance());
            InitializeComponent();
            this.ApplyCaption(false);
            var windowConfig = ARVRWindowConfig.Instance;
            windowConfig.SetWindow(this);
            var resultColumns = ((GridView)listView1.View).Columns;
            ResultColumnLayout.Restore(resultColumns, windowConfig.ResultColumnOrder);
            _initialResultColumnOrder = ResultColumnLayout.Capture(resultColumns);
            this.Title += Assembly.GetAssembly(typeof(ARVRWindow))?.GetName().Version?.ToString() ?? "";
            Loaded += (_, _) => UpdateResultViewRefreshButtonState();
            DisPlayManager.GetInstance().IDisPlayControls.CollectionChanged += DisplayControls_CollectionChanged;
            foreach (ViewConfigBase config in ConfigHandler.GetInstance().Configs.Values.OfType<ViewConfigBase>())
                config.PropertyChanged += ViewRefreshConfig_PropertyChanged;
        }

        private int CurrentTestType = -1;

        ObjectiveTestResult ObjectiveTestResult { get; set; } = new ObjectiveTestResult();
        private int ObjectiveTestResultRecordId;
        private string _objectiveSessionSerialNumber = string.Empty;
        private bool _objectiveSessionCompleted;
        private (int Code, string Message)? _firstFlowFailure;
        private string _lastFlowFailureMessage = string.Empty;
        private IProcess? _currentFlowProcess;
        private int _currentFlowTemplateId;
        private ArvrFlowBatchReceipt? _currentFlowBatch;
        internal FlowBatchFinalization BatchFinalization { get; } = new();
        private readonly FlowNodeExecutionRecorder _flowNodeExecutionRecorder = new FlowNodeExecutionRecorder();
        private readonly FlowDiagnosticsWriteQueue _flowDiagnosticsWrites = new();
        // Older supported hosts expose only the synchronous write API.
        private static readonly Func<int, string?, string?, FlowStatus, long, Task<int>>? QueueDiagnosticRun =
            typeof(FlowNodeRecordDataBaseHelper).GetMethod(nameof(FlowNodeRecordDataBaseHelper.QueueFlowRunAsync),
                [typeof(int), typeof(string), typeof(string), typeof(FlowStatus), typeof(long)])
                ?.CreateDelegate<Func<int, string?, string?, FlowStatus, long, Task<int>>>();
        private readonly FlowRunningNodeTracker _runningFlowNodes = new();
        private readonly SwitchPgAcknowledgement _switchPgAcknowledgement = new();
        private NetworkStream? _socketSwitchSessionStream;
        private DateTime? _pendingSwitchRequestedAt;
        private DateTime? _pendingSwitchAcknowledgedAt;
        private long? _currentSwitchPreparationMilliseconds;
        private long _currentPictureSwitchMilliseconds;
        private long _currentPreProcessingMilliseconds;
        private double _currentBatchCreateMs;
        private double _currentNodeRecorderStartMs;
        private long _currentFlowFinalizeMilliseconds;
        private double _currentFlowRunRecordMs;
        private double _currentBatchFinalizeMs;
        private double _currentNodeRecorderFlushMs;
        private bool? _currentNodeRecorderFlushed;
        private bool _currentFlowDiagnosticsDeferred;
        private double _currentFlowDiagnosticsBackpressureMs;
        private readonly FlowRuntimeEstimateCache _flowRuntimeEstimates = new();
        private FlowRuntimeEstimateKey _currentRuntimeEstimateKey;
        private double _currentRuntimeEstimateLookupMs;
        private double _currentStartupWorkMs;
        private double _currentRefreshServicesMs;
        private double _currentRefreshDetachNodesMs;
        private double _currentRefreshLoadGraphMs;
        private double _currentRefreshAttachNodesMs;
        private double _currentRefreshTotalMs;
        private bool _isFlowStartPending;
        private bool _isFlowLifecycleActive;
        private bool _isFlowCompletionPending;
        private bool _runAllSessionPrepared;
        internal bool IsResultExportPending { get; private set; }
        internal bool IsCompletingFlowBatch => BatchFinalization.IsCompleting || BatchFinalization.CloseRequested;
        internal int? CurrentFlowResultBatchId => CurrentFlowResult is { BatchId: > 0 } result ? result.BatchId : null;

        internal bool IsTestExecutionBusy => _isDisposed
            || IsSwitchRun
            || _luxSession != null
            || IsJsonVidRunning
            || _isFlowCompletionPending
            || IsCompletingFlowBatch
            || _isFlowStartPending
            || flowControl?.IsFlowRun == true
            || _isFlowLifecycleActive
            || _isRunAllRunning
            || IsResultExportPending
            || _runAllSessionPrepared;

        private bool CanInitializeTestSession => !_isDisposed && _luxSession == null && !IsJsonVidRunning
            && !IsResultExportPending && !IsCompletingFlowBatch;

        public string InitTest(string? serialNumber)
        {
            if (!CanInitializeTestSession)
            {
                if (IsResultExportPending || IsCompletingFlowBatch)
                    log.Warn("正在保存测试结果，暂不初始化下一次测试");
                return _objectiveSessionSerialNumber;
            }
            return InitializeTestSession(serialNumber);
        }

        public bool TryPrepareRunAllSession(string? serialNumber, out string resolvedSerialNumber)
        {
            if (IsTestExecutionBusy)
            {
                resolvedSerialNumber = string.IsNullOrWhiteSpace(_objectiveSessionSerialNumber)
                    ? ProjectARVRProConfig.Instance.SN
                    : _objectiveSessionSerialNumber;
                log.Warn($"当前测试正在执行，拒绝启动 RunAll：{serialNumber}");
                return false;
            }

            resolvedSerialNumber = InitializeTestSession(serialNumber);
            _runAllSessionPrepared = true;
            return true;
        }

        private string InitializeTestSession(string? serialNumber)
        {
            ResetStepProgress();
            ObjectiveTestResult = new ObjectiveTestResult { SessionStartTime = DateTime.Now };
            ObjectiveTestResultRecordId = 0;
            _objectiveSessionCompleted = false;
            _firstFlowFailure = null;
            _lastFlowFailureMessage = string.Empty;
            _currentFlowProcess = null;
            _currentFlowTemplateId = 0;
            _currentFlowBatch = null;
            ClearSocketSwitchSession();
            _pendingSwitchRequestedAt = null;
            _pendingSwitchAcknowledgedAt = null;
            ResetCurrentPhaseDurations();
            CurrentFlowResult = null!;
            CurrentTestType = -1;
            bool isAutoGenerated = string.IsNullOrWhiteSpace(serialNumber);
            string resolvedSerialNumber = isAutoGenerated ? AutoSerialNumberGenerator.Create() : serialNumber!.Trim();
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProjectARVRProConfig.Instance.SN = resolvedSerialNumber;
                resolvedSerialNumber = ProjectARVRProConfig.Instance.SN;
            });
            _objectiveSessionSerialNumber = resolvedSerialNumber;
            if (isAutoGenerated)
            {
                log.Info($"未收到SN，已自动生成规则化SN: {resolvedSerialNumber}");
            }

            return resolvedSerialNumber;
        }

        internal (string SerialNumber, int StepIndex, Action? AfterSend) PrepareSocketTestSession(NetworkStream stream, string? serialNumber)
        {
            Dispatcher.VerifyAccess();
            if (!CanInitializeTestSession)
                return (serialNumber?.Trim() ?? string.Empty, -2, null);

            int firstStepIndex = FindNextEnabledProcessIndex(-1);
            if (firstStepIndex < 0)
                return (serialNumber?.Trim() ?? string.Empty, -1, null);

            ProcessMeta process = ProcessMetas[firstStepIndex];
            string resolvedSerialNumber = InitializeTestSession(serialNumber);
            _socketSwitchSessionStream = stream;
            SocketControl.Current.Stream = stream;
            int externalIndex = firstStepIndex + GetProcessEnableSocket.GetIndexOffset();
            return (resolvedSerialNumber, externalIndex, PrepareSocketSwitch(stream, firstStepIndex, process));
        }

        private Action PrepareSocketSwitch(NetworkStream stream, int stepIndex, ProcessMeta process)
        {
            var request = _switchPgAcknowledgement.Prepare(stream, stepIndex, process);
            return () => _switchPgAcknowledgement.MarkSent(request, DateTime.Now);
        }

        private void ClearSocketSwitchSession()
        {
            _switchPgAcknowledgement.Clear();
            _socketSwitchSessionStream = null;
        }

        private void ApplyPendingSwitchTiming(ProjectARVRReuslt result)
        {
            result.SwitchRequestedAt = _pendingSwitchRequestedAt;
            result.SwitchAcknowledgedAt = _pendingSwitchAcknowledgedAt;
            _pendingSwitchRequestedAt = null;
            _pendingSwitchAcknowledgedAt = null;
            ResetCurrentPhaseDurations();
        }

        private void ResetCurrentPhaseDurations()
        {
            _currentSwitchPreparationMilliseconds = null;
            _currentPictureSwitchMilliseconds = 0;
            _currentPreProcessingMilliseconds = 0;
            _currentBatchCreateMs = 0;
            _currentNodeRecorderStartMs = 0;
            _currentFlowFinalizeMilliseconds = 0;
            _currentFlowRunRecordMs = 0;
            _currentBatchFinalizeMs = 0;
            _currentNodeRecorderFlushMs = 0;
            _currentNodeRecorderFlushed = null;
            _currentFlowDiagnosticsDeferred = false;
            _currentFlowDiagnosticsBackpressureMs = 0;
            _currentRuntimeEstimateLookupMs = 0;
            _currentStartupWorkMs = 0;
            _currentRefreshServicesMs = 0;
            _currentRefreshDetachNodesMs = 0;
            _currentRefreshLoadGraphMs = 0;
            _currentRefreshAttachNodesMs = 0;
            _currentRefreshTotalMs = 0;
        }

        private static long? GetElapsedMilliseconds(DateTime? startedAt, DateTime? completedAt)
        {
            if (!startedAt.HasValue || !completedAt.HasValue)
                return null;

            return Math.Max(0, (long)(completedAt.Value - startedAt.Value).TotalMilliseconds);
        }

        private void LogFlowPhaseTiming(
            ProjectARVRReuslt result,
            long batchLookupMilliseconds,
            long processExecutionMilliseconds,
            long viewResultSaveMilliseconds,
            long objectiveResultSaveMilliseconds,
            long linkSaveMilliseconds,
            long resultProcessingTimestampPersistMilliseconds,
            bool resultProcessingTimestampPersisted,
            bool resultImageDimensionsFromProcessCache,
            (ResultSqliteWriteTiming? View, ResultSqliteWriteTiming? Objective, ResultSqliteWriteTiming? CompletedTimestamp) sqliteWrites)
        {
            if (!log.IsInfoEnabled) return;
            DateTime persistedAt = DateTime.Now;
            long persistMilliseconds = Math.Max(0, viewResultSaveMilliseconds)
                + Math.Max(0, objectiveResultSaveMilliseconds)
                + Math.Max(0, resultProcessingTimestampPersistMilliseconds);
            var timing = new
            {
                Event = "ARVRFlowPhaseTiming",
                result.SN,
                result.Model,
                result.BatchId,
                ProcessId = Environment.ProcessId,
                ObjectiveTestResultRecordId,
                FlowStatus = result.FlowStatus.ToString(),
                result.Result,
                result.Code,
                result.Msg,
                result.SwitchRequestedAt,
                result.SwitchAcknowledgedAt,
                result.PictureSwitchStartedAt,
                result.PictureSwitchCompletedAt,
                result.PreProcessingCompletedAt,
                result.FlowStartedAt,
                result.FlowCompletedAt,
                result.ResultProcessingCompletedAt,
                PersistedAt = persistedAt,
                SwitchWaitMs = GetElapsedMilliseconds(result.SwitchRequestedAt, result.SwitchAcknowledgedAt),
                SwitchPreparationMs = _currentSwitchPreparationMilliseconds,
                StartupWorkMs = Math.Round(_currentStartupWorkMs, 3),
                RuntimeEstimateCacheHit = LastFlowTime > 0,
                RuntimeEstimateLookupMs = Math.Round(_currentRuntimeEstimateLookupMs, 3),
                RefreshServicesMs = Math.Round(_currentRefreshServicesMs, 3),
                RefreshDetachNodesMs = Math.Round(_currentRefreshDetachNodesMs, 3),
                RefreshLoadGraphMs = Math.Round(_currentRefreshLoadGraphMs, 3),
                RefreshAttachNodesMs = Math.Round(_currentRefreshAttachNodesMs, 3),
                RefreshTotalMs = Math.Round(_currentRefreshTotalMs, 3),
                StartupOtherMs = Math.Round(Math.Max(0, _currentStartupWorkMs - _currentRefreshTotalMs - _currentRuntimeEstimateLookupMs), 3),
                PictureSwitchMs = _currentPictureSwitchMilliseconds,
                PreProcessingMs = _currentPreProcessingMilliseconds,
                BatchCreateMs = Math.Round(_currentBatchCreateMs, 3),
                NodeRecorderStartMs = Math.Round(_currentNodeRecorderStartMs, 3),
                FlowMs = Math.Max(0, result.RunTime),
                FlowFinalizeMs = _currentFlowFinalizeMilliseconds,
                FlowRunRecordMs = Math.Round(_currentFlowRunRecordMs, 3),
                BatchFinalizeMs = Math.Round(_currentBatchFinalizeMs, 3),
                NodeRecorderFlushMs = Math.Round(_currentNodeRecorderFlushMs, 3),
                NodeRecorderFlushed = _currentNodeRecorderFlushed,
                FlowDiagnosticsDeferred = _currentFlowDiagnosticsDeferred,
                FlowDiagnosticsBackpressureMs = Math.Round(_currentFlowDiagnosticsBackpressureMs, 3),
                BatchLookupMs = Math.Max(0, batchLookupMilliseconds),
                ProcessExecuteMs = Math.Max(0, processExecutionMilliseconds),
                ViewResultSaveMs = Math.Max(0, viewResultSaveMilliseconds),
                ObjectiveResultSaveMs = Math.Max(0, objectiveResultSaveMilliseconds),
                LinkSaveMs = Math.Max(0, linkSaveMilliseconds),
                ResultProcessingTimestampPersistMs = Math.Max(0, resultProcessingTimestampPersistMilliseconds),
                ResultProcessingTimestampPersisted = resultProcessingTimestampPersisted,
                ResultImageDimensionsFromProcessCache = resultImageDimensionsFromProcessCache,
                ResultSqliteWrites = new { sqliteWrites.View, sqliteWrites.Objective, sqliteWrites.CompletedTimestamp },
                PersistMs = persistMilliseconds,
                FlowCompletionToResultProcessingCompleteMs = GetElapsedMilliseconds(result.FlowCompletedAt, result.ResultProcessingCompletedAt),
                FlowCompletionToPersistedMs = GetElapsedMilliseconds(result.FlowCompletedAt, persistedAt),
                ImageExportIncludedInCt = false,
                DisplayedResultCount = ViewResluts.Count,
                AwaitingAutomaticImageSnapshotCount = _automaticImageExportResults.Count,
                OutstandingImageExportCount = Volatile.Read(ref _outstandingImageExports),
            };
            log.Info($"ARVR阶段耗时: {JsonConvert.SerializeObject(timing)}");
        }

        bool IsSwitchRun;
        internal bool TryAcknowledgeSocketSwitch(NetworkStream stream)
        {
            Dispatcher.VerifyAccess();
            if (IsTestExecutionBusy || _objectiveSessionCompleted)
                return false;

            int nextTestType = FindNextEnabledProcessIndex(CurrentTestType);
            if (nextTestType < 0)
                return false;

            var sentAt = _switchPgAcknowledgement.TryConsume(stream, SocketControl.Current.Stream, nextTestType, ProcessMetas[nextTestType]);
            if (!sentAt.HasValue)
                return false;

            _pendingSwitchRequestedAt = sentAt;
            _pendingSwitchAcknowledgedAt = DateTime.Now;
            _ = StartAcknowledgedSocketSwitchAsync();
            return true;
        }

        private async Task StartAcknowledgedSocketSwitchAsync()
        {
            try
            {
                await TryStartNextTemplateAsync();
            }
            catch (Exception ex)
            {
                log.Error("启动下一项 ARVR 流程失败", ex);
            }
        }

        public async Task<bool> TryStartNextTemplateAsync(CancellationToken cancellationToken = default)
        {
            if (_luxSession != null || IsJsonVidRunning || IsCompletingFlowBatch) return false;
            if (IsSwitchRun)
            {
                log.Warn("重复触发PG，忽略本次请求");
                return false;
            }
            IsSwitchRun = true;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_isFlowStartPending || flowControl.IsFlowRun || _isFlowLifecycleActive || _isRunAllRunning || IsResultExportPending)
                {
                    log.Warn("PG切换错误，正在执行流程或处理流程结果");
                    return false;
                }

                int nextTestType = FindNextEnabledProcessIndex(CurrentTestType);
                if (nextTestType >= 0)
                {
                    ProcessMeta processMeta = ProcessMetas[nextTestType];
                    TemplateModel<FlowParam> template = SelectFlowTemplate(processMeta);
                    CurrentTestType = nextTestType;
                    return await TryRunTemplate(template, processMeta, cancellationToken: cancellationToken);
                }

                log.Info("没有可执行的 ARVR 流程");
                await AbortCurrentTestSessionAsync("没有可执行的 ARVR 流程");
                return false;
            }
            catch (OperationCanceledException)
            {
                await AbortCurrentTestSessionAsync("测试已取消");
                throw;
            }
            catch (Exception ex)
            {
                await AbortCurrentTestSessionAsync($"启动下一项 ARVR 流程失败: {ex.Message}");
                throw;
            }
            finally
            {
                IsSwitchRun = false;
            }
        }

        private async Task AbortCurrentTestSessionAsync(string message)
        {
            ClearSocketSwitchSession();
            if (_objectiveSessionCompleted || !ObjectiveTestResult.SessionStartTime.HasValue)
                return;

            RecordFlowFailure(message);
            if (CurrentFlowResult != null && ObjectiveTestResultRecordId > 0)
            {
                // The last flow row is already complete. Only update the product summary so a
                // missing next template cannot rewrite that historical flow as failed.
                SaveObjectiveTestResultRecord(CurrentFlowResult);
            }
            await TestCompletedAsync();
        }

        private TemplateModel<FlowParam> SelectFlowTemplate(ProcessMeta processMeta)
        {
            var template = processMeta.RequireFlowTemplate();
            FlowTemplate.SelectedItem = template;
            return template;
        }
 
        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        private int _executionStatusUpdatePending;

        Stopwatch stopwatch = new Stopwatch();

        private LogOutput? logOutput;
        private bool _isDisposed;
        private EventHandler? _activeGroupChangedHandler;
        private EventHandler? _activeProcessMetasChangedHandler;
        private void Window_Initialized(object sender, EventArgs e)
        {
            stepBar.CanEditConfiguration = () => !IsTestExecutionBusy;
            stepBar.TemplateRequested += meta => ProcessManager.OpenFlowTemplateEditorCommand.Execute(meta);
            stepBar.ConfigurationRequested += meta =>
            {
                if (!IsTestExecutionBusy) ProcessManager.EditConfiguration(meta);
            };
            RefreshStepBar();
            _activeGroupChangedHandler = ProcessManager_ActiveGroupChanged;
            ProcessManager.ActiveGroupChanged += _activeGroupChangedHandler;
            _activeProcessMetasChangedHandler = ProcessManager_ActiveProcessMetasChanged;
            ProcessManager.ActiveProcessMetasChanged += _activeProcessMetasChangedHandler;
            this.DataContext = ProjectARVRProConfig.Instance;
            ProjectConfig.PropertyChanged += ProjectConfig_PropertyChanged;
            ApplyResultOverlayConfig();
            flowEngine = new FlowEngineControl(false);
            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);

            flowControl = new FlowControl(MQTTControl.GetInstance(), flowEngine);
            flowControl.PropertyChanged += FlowControl_PropertyChanged;

            timer = new Timer(TimeRun, null, Timeout.Infinite, Timeout.Infinite);


            logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline", ProjectARVRProLogConfig.Instance);
            LogGrid.Children.Add(logOutput);


            this.Closed += (s, e) =>
            {
                SaveResultColumnOrder();
                this.Dispose();
            };


            ImageView.ExternalRenderCompleted += ImageView_ExternalRenderCompleted;
            listView1.ItemsSource = ViewResluts;

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedItem is ProjectARVRReuslt { Id: > 0 }));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));

            // 构建 ListView 统一的右键菜单（替代原先每个实体各自创建 ContextMenu 的方案）
            BuildListViewContextMenu();
            ViewResluts.CollectionChanged += ViewResults_CollectionChanged;

        }

        private void ViewResults_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add
                && e.NewStartingIndex == 0
                && e.NewItems?[0] is ProjectARVRReuslt result)
            {
                listView1.SelectedItem = result;
                listView1.ScrollIntoView(result);
            }
        }

        private void ProcessManager_ActiveGroupChanged(object? sender, EventArgs e)
        {
            if (!_isDisposed)
            {
                RefreshStepBar();
                ResetStepProgress();
            }
        }

        private void ProcessManager_ActiveProcessMetasChanged(object? sender, EventArgs e)
        {
            if (!_isDisposed)
            {
                RefreshStepBar();
                ResetStepProgress();
            }
        }

        private void OpenDatabaseCleanup_Click(object sender, RoutedEventArgs e)
        {
            DatabaseCleanupWindow.OpenWindow();
        }

        private void OpenCycleTimeStatistics_Click(object sender, RoutedEventArgs e)
        {
            new CycleTimeStatisticsWindow
            {
                WaitForFlowDiagnosticsAsync = _flowDiagnosticsWrites.WhenWritten,
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }

        public void Delete()
        {
            if (listView1.SelectedItem is not ProjectARVRReuslt { Id: > 0 } item) return;
            ArvrResultIdentity selection = ArvrResultIdentity.From(item);
            if (MessageBox.Show(this, LocalizedText.Format($"是否删除选中的项目步骤结果 {selection.SN} / {selection.Model}？"),
                "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                if (ViewResultManager.DeleteResult(selection))
                    log.Info($"删除项目步骤结果：resultId={selection.Id}, model={selection.Model}");
                else
                    MessageBox.Show(this, LocalizedText.Get("所选测试结果已变更，请刷新后重试。"), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception error)
            {
                log.Error($"删除项目步骤结果失败：resultId={selection.Id}", error);
                MessageBox.Show(this, LocalizedText.Get("删除项目步骤结果失败。"), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region ListView ContextMenu

        private void BuildListViewContextMenu()
        {
            var openFolderCommand = new RelayCommand(
                _ => ContextMenu_OpenFolderAndSelectFile(),
                _ => listView1.SelectedItem is ProjectARVRReuslt item && File.Exists(item.FileName));

            var batchHistoryCommand = new RelayCommand(
                _ => ContextMenu_BatchDataHistory(),
                _ => listView1.SelectedItem is ProjectARVRReuslt item && item.BatchId > 0);

            var flowExecutionAnalysisCommand = new RelayCommand(
                _ => ContextMenu_FlowExecutionAnalysis(),
                _ => listView1.SelectedItem is ProjectARVRReuslt item && item.BatchId > 0);

            var viewTestResultCommand = new RelayCommand(
                _ => ContextMenu_ViewTestResult(),
                _ => listView1.SelectedItem is ProjectARVRReuslt item && (item.Id > 0 || !string.IsNullOrEmpty(item.ViewResultJson)));

            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem() { Command = ApplicationCommands.Delete });
            contextMenu.Items.Add(new MenuItem() { Command = ApplicationCommands.Copy, Header = LocalizedText.Get("复制") });
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem() { Command = openFolderCommand, Header = "OpenFolderAndSelectFile" });
            contextMenu.Items.Add(new MenuItem() { Command = batchHistoryCommand, Header = LocalizedText.Get("流程结果查询") });
            contextMenu.Items.Add(new MenuItem() { Command = flowExecutionAnalysisCommand, Header = LocalizedText.Get("流程分析") });
            contextMenu.Items.Add(new MenuItem() { Command = viewTestResultCommand, Header = LocalizedText.Get("查看测试结果") });
            contextMenu.Items.Add(new MenuItem
            {
                Header = LocalizedText.Get("算法实证"),
                Command = new RelayCommand(_ => OpenAlgorithmEvidence_Click(this, new RoutedEventArgs()), _ => listView1.SelectedItem is ProjectARVRReuslt)
            });

            // 右键菜单打开时刷新 CanExecute 状态
            contextMenu.Opened += (s, e) => CommandManager.InvalidateRequerySuggested();

            // 右键菜单打开前确保点击位置的行被选中
            listView1.PreviewMouseRightButtonDown += (s, e) =>
            {
                var element = listView1.InputHitTest(e.GetPosition(listView1)) as DependencyObject;
                while (element != null && element is not ListViewItem)
                    element = VisualTreeHelper.GetParent(element);

                if (element is ListViewItem targetItem)
                {
                    targetItem.IsSelected = true;
                }
            };

            listView1.ContextMenu = contextMenu;
        }

        private void OpenAlgorithmEvidence_Click(object sender, RoutedEventArgs e) =>
            new AlgorithmEvidence.AlgorithmEvidenceWindow(ViewResluts.ToArray(), (listView1.SelectedItem as ProjectARVRReuslt)?.Id)
            { Owner = this }.Show();

        private void ContextMenu_OpenFolderAndSelectFile()
        {
            if (listView1.SelectedItem is ProjectARVRReuslt item && !string.IsNullOrWhiteSpace(item.FileName))
                PlatformHelper.OpenFolderAndSelectFile(item.FileName);
        }

        private void ContextMenu_BatchDataHistory()
        {
            MeasureBatchModel? batch = GetSelectedMeasureBatch();
            if (batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), LocalizedText.Get("找不到批次号，请检查流程配置"), "ColorVision");
                return;
            }
            var frame = new Frame();
            var batchDataHistory = new MeasureBatchPage(frame, batch);
            var window = new Window() { Owner = Application.Current.GetActiveWindow() };
            window.ApplyCaption();
            window.Content = batchDataHistory;
            window.Show();
        }

        private async void ContextMenu_FlowExecutionAnalysis()
        {
            if (listView1.SelectedItem is not ProjectARVRReuslt result) return;

            try
            {
                bool written = string.IsNullOrWhiteSpace(result.Code) || await FlowDiagnosticsWriteQueue.WaitAsync(
                    _flowDiagnosticsWrites.WhenWritten(result.Code), FlowDiagnosticsWriteQueue.WriteWaitTimeout);
                if (_isDisposed) return;
                if (!written)
                {
                    MessageBox.Show(this, LocalizedText.Get("流程诊断仍在写入，请稍后打开。"), "ColorVision");
                    return;
                }

                MeasureBatchModel batch = await Task.Run(() => ArvrFlowAnalysisHistory.ResolveLocalBatch(result));
                if (_isDisposed) return;
                new FlowExecutionAnalysisWindow(batch)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                }.Show();
            }
            catch (Exception ex)
            {
                log.Warn($"打开流程分析失败：BatchId={result.BatchId}", ex);
                if (!_isDisposed) MessageBox.Show(this, ex.Message, LocalizedText.Get("流程分析"), MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private MeasureBatchModel? GetSelectedMeasureBatch() => ArvrBatchHistory.Resolve(listView1.SelectedItem as ProjectARVRReuslt);

        private void ContextMenu_ViewTestResult()
        {
            if (listView1.SelectedItem is not ProjectARVRReuslt item) return;
            string? viewResultJson = ViewResultManager.LoadViewResultJson(item);
            if (string.IsNullOrEmpty(viewResultJson))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), LocalizedText.Get("ViewResultJson为空"), "ColorVision");
                return;
            }
            var window = new TestResultViewWindow(viewResultJson)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
        }

        #endregion

        public Task Refresh()
        {
            if (FlowTemplate.SelectedItem is not TemplateModel<FlowParam> template) return Task.CompletedTask;

            Stopwatch refreshTiming = Stopwatch.StartNew();
            MqttRCService.GetInstance().QueryServices();
            _currentRefreshServicesMs = refreshTiming.Elapsed.TotalMilliseconds;
            foreach (CVCommonNode node in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
            {
                node.nodeRunEvent -= UpdateMsg;
                node.nodeEndEvent -= NodeExecutionEnded;
            }
            _runningFlowNodes.Reset(null);
            _flowNodeExecutionRecorder.DetachNodes();
            double detachedAt = refreshTiming.Elapsed.TotalMilliseconds;
            _currentRefreshDetachNodesMs = detachedAt - _currentRefreshServicesMs;

            string Refreshdata = template.Value.DataBase64;
            flowEngine.LoadFromBase64(Refreshdata, MqttRCService.GetInstance().ServiceTokens);
            double loadedAt = refreshTiming.Elapsed.TotalMilliseconds;
            _currentRefreshLoadGraphMs = loadedAt - detachedAt;

            CVCommonNode[] flowNodes = STNodeEditorMain.Nodes.OfType<CVCommonNode>().ToArray();
            foreach (CVCommonNode item in flowNodes)
            {
                item.nodeRunEvent -= UpdateMsg;
                item.nodeRunEvent += UpdateMsg;
                item.nodeEndEvent -= NodeExecutionEnded;
                item.nodeEndEvent += NodeExecutionEnded;
            }
            _flowNodeExecutionRecorder.AttachNodes(flowNodes);
            _currentRefreshTotalMs = refreshTiming.Elapsed.TotalMilliseconds;
            _currentRefreshAttachNodesMs = _currentRefreshTotalMs - loadedAt;
            return Task.CompletedTask;
        }

        private void CaptureRuntimeEstimate(TemplateModel<FlowParam> template, ProcessMeta? processMeta)
        {
            long startedAt = Stopwatch.GetTimestamp();
            _currentRuntimeEstimateKey = new FlowRuntimeEstimateKey(
                template.Key,
                template.Value.DataBase64,
                processMeta?.FlowCameraParameterOverrideConfig.GetRuntimeSignature());
            LastFlowTime = _flowRuntimeEstimates.GetElapsed(_currentRuntimeEstimateKey);
            _currentRuntimeEstimateLookupMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        }

        private void ApplyFlowCameraParameterOverride(ProcessMeta? processMeta)
        {
            if (processMeta == null)
                return;

            long startedAt = Stopwatch.GetTimestamp();
            FlowCameraParameterOverrideResult result = FlowCameraParameterOverrideService.ApplyToLoadedFlow(
                processMeta.FlowCameraParameterOverrideConfig,
                STNodeEditorMain.Nodes.Cast<STNode>(),
                flowEngine.GetStartNodeName());
            double elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

            if (result.Applied)
                log.Info($"{result.Message} 参数覆盖耗时 {elapsedMs:F3} ms。");
            else if (!string.IsNullOrWhiteSpace(result.Message))
                log.Info($"{result.Message} 检查耗时 {elapsedMs:F3} ms。");
        }


        private void TimeRun(object? state)
        {
            UpdateMsg(state);
        }

        private long LastFlowTime;
        string FlowName;
        private void UpdateMsg(object? sender)
        {
            // Keep one pending update; it reads the latest node and elapsed time on the UI thread.
            if (_isDisposed || Interlocked.Exchange(ref _executionStatusUpdatePending, 1) != 0)
                return;

            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        // A timer callback already queued on the dispatcher must not replace a final error.
                        if (_isDisposed || !stopwatch.IsRunning || ExecutionStatus.Status.Kind != FlowExecutionStatusKind.Running)
                            return;
                        ExecutionStatus.Status = FlowExecutionStatusInfo.Running(FlowName, _runningFlowNodes.GetRunningNodeNames(), stopwatch.ElapsedMilliseconds, LastFlowTime);
                        int stepIndex = ProcessManager.GetEnabledStepIndex(ProcessMetas, CurrentFlowResult?.TestType ?? -1);
                        stepBar.UpdateElapsed(stepIndex, stopwatch.ElapsedMilliseconds, LastFlowTime);
                    }
                    catch
                    {

                    }
                    finally
                    {
                        Volatile.Write(ref _executionStatusUpdatePending, 0);
                    }
                });
            }
            catch
            {
                Volatile.Write(ref _executionStatusUpdatePending, 0);
                throw;
            }
        }

        private void UpdateMsg(object sender, FlowEngineNodeRunEventArgs e)
        {
            if (!_isDisposed && e != null && sender is CVCommonNode node
                && _runningFlowNodes.NodeStarted(e.SerialNumber, node.NodeID, node.Title, e.SendMsgId))
                UpdateMsg(sender);
        }

        private void NodeExecutionEnded(object sender, FlowEngineNodeEndEventArgs e)
        {
            if (!_isDisposed && e != null && sender is CVCommonNode node
                && _runningFlowNodes.NodeEnded(e.SerialNumber, node.NodeID, e.RecvMsgId))
                UpdateMsg(sender);
        }

        private async void TestClick(object sender, RoutedEventArgs e)
        {
            if (!_isRunAllRunning && flowControl.IsFlowRun && CurrentFlowResult != null)
            {
                flowControl.Stop();
                await CompleteSingleFlowAsync(new FlowControlData
                {
                    EventName = "Failed",
                    Status = StatusTypeEnum.Failed,
                    SerialNumber = CurrentFlowResult.Code,
                    Message = "用户停止测试",
                    Params = "用户停止测试",
                    TotalTime = stopwatch.ElapsedMilliseconds,
                });
                return;
            }

            await RunTemplate();
        }


        ProjectARVRReuslt CurrentFlowResult { get; set; }
        int TryCount;

        public async Task RunTemplate()
        {
            if (_luxSession != null || IsJsonVidRunning || IsCompletingFlowBatch) return;
            if (FlowTemplate.SelectedItem is not TemplateModel<FlowParam> template)
                return;

            ProcessMeta? processMeta = ProcessManager.FindProcessMetaForTemplate(template.Key);
            ProcessMeta? cameraOverrideMeta = ProcessManager.FindUniqueProcessMetaForTemplate(template.Key);
            await TryRunTemplate(
                template,
                processMeta,
                applyCameraParameterOverride: ReferenceEquals(processMeta, cameraOverrideMeta));
        }

        private async Task<bool> TryRunTemplate(
            TemplateModel<FlowParam> flowTemplate,
            ProcessMeta? runProcessMeta,
            bool applyCameraParameterOverride = true,
            CancellationToken cancellationToken = default)
        {
            if (_isFlowStartPending || flowControl.IsFlowRun || _isFlowLifecycleActive || _isRunAllRunning || IsResultExportPending)
            {
                log.Info("当前flowControl存在流程执行或正在处理流程结果");
                return false;
            }

            _isFlowStartPending = true;
            _switchPgAcknowledgement.Clear();
            UpdateTestButtonState();
            CurrentFlowResult = null!;
            bool flowStarted = false;
            Stopwatch startupTiming = Stopwatch.StartNew();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string currentSerialNumber = ProjectARVRProConfig.Instance.SN;
                if (_objectiveSessionCompleted
                    || !ObjectiveTestResult.SessionStartTime.HasValue
                    || !string.Equals(_objectiveSessionSerialNumber, currentSerialNumber, StringComparison.Ordinal))
                {
                    InitializeTestSession(currentSerialNumber);
                }
                TryCount++;
                _currentFlowTemplateId = flowTemplate.Id;
                CurrentFlowResult = new ProjectARVRReuslt();
                CurrentFlowResult.SN = ProjectARVRProConfig.Instance.SN;
                CurrentFlowResult.Model = flowTemplate.Key;
                ApplyPendingSwitchTiming(CurrentFlowResult);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    int groupIndex = runProcessMeta == null ? -1 : ProcessMetas.IndexOf(runProcessMeta);
                    if (groupIndex >= 0)
                    {
                        CurrentFlowResult.TestType = groupIndex;
                    }
                    else
                    {
                        CurrentFlowResult.TestType = CurrentTestType;
                    }
                });


                FlowName = flowTemplate.Key;
                PrepareExecutionStatus();

                CurrentFlowResult.Code = ViewResultManager.Config.CreateFlowCode(ProjectARVRProConfig.Instance.SN, DateTime.Now);
                _currentFlowProcess = runProcessMeta?.Process ?? ProcessManager.CreateBlankProcess();
                ResultProcessResolver.Capture(CurrentFlowResult, _currentFlowProcess);

                ProcessMeta? cameraOverrideMeta = applyCameraParameterOverride ? runProcessMeta : null;
                CaptureRuntimeEstimate(flowTemplate, cameraOverrideMeta);

                await Refresh();
                cancellationToken.ThrowIfCancellationRequested();

                ApplyFlowCameraParameterOverride(cameraOverrideMeta);

                CurrentFlowResult.PictureSwitchStartedAt = DateTime.Now;
                _currentStartupWorkMs = startupTiming.Elapsed.TotalMilliseconds;
                _currentSwitchPreparationMilliseconds = GetElapsedMilliseconds(
                    CurrentFlowResult.SwitchAcknowledgedAt,
                    CurrentFlowResult.PictureSwitchStartedAt);
                Stopwatch pictureSwitchStopwatch = Stopwatch.StartNew();
                bool pictureSwitchSucceeded = await _pictureSwitchService.ExecuteAsync(runProcessMeta);
                pictureSwitchStopwatch.Stop();
                _currentPictureSwitchMilliseconds = Math.Max(0, pictureSwitchStopwatch.ElapsedMilliseconds);
                if (!pictureSwitchSucceeded)
                {
                    CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                    CurrentFlowResult.Msg = "PictureSwitchFailed";
                    await ExecuteProcessFailureAsync(runProcessMeta?.Process);
                    RecordFlowFailure(CurrentFlowResult.Msg);
                    SaveFlowResult(CurrentFlowResult);
                    SaveObjectiveTestResultRecord(CurrentFlowResult);
                    await TestCompletedAsync();
                    TryCount = 0;
                    return false;
                }
                CurrentFlowResult.PictureSwitchCompletedAt = DateTime.Now;
                cancellationToken.ThrowIfCancellationRequested();

                Stopwatch preProcessingStopwatch = Stopwatch.StartNew();
                bool preProcessingSucceeded = await PreProcessing(FlowName, CurrentFlowResult.SN);
                preProcessingStopwatch.Stop();
                _currentPreProcessingMilliseconds = Math.Max(0, preProcessingStopwatch.ElapsedMilliseconds);
                if (!preProcessingSucceeded)
                {
                    CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                    CurrentFlowResult.Msg = "PreProcessFailed";
                    await ExecuteProcessFailureAsync(runProcessMeta?.Process);
                    RecordFlowFailure(CurrentFlowResult.Msg);
                    SaveFlowResult(CurrentFlowResult);
                    SaveObjectiveTestResultRecord(CurrentFlowResult);
                    await TestCompletedAsync();
                    TryCount = 0;
                    return false;
                }
                CurrentFlowResult.PreProcessingCompletedAt = DateTime.Now;
                cancellationToken.ThrowIfCancellationRequested();

                CurrentFlowResult.FlowStatus = FlowStatus.Ready;

                flowControl.FlowCompleted -= FlowControl_FlowCompleted;
                flowControl.FlowCompleted += FlowControl_FlowCompleted;
                stopwatch.Reset();
                CurrentFlowResult.FlowStartedAt = DateTime.Now;
                stopwatch.Start();

                ArvrFlowBatchReceipt receipt = await CreateCurrentFlowBatchAsync();

                _isFlowLifecycleActive = true;
                if (!await receipt.StartAsync(flowControl, cancellationToken))
                {
                    await HandleFlowStartFailureAsync(FlowStartRejectedMessage, runProcessMeta?.Process, receipt, persistResult: true);
                    return false;
                }

                flowStarted = true;
                if (ReferenceEquals(_currentFlowBatch, receipt))
                {
                    SetStepProgress(CurrentFlowResult.TestType);
                    timer.Change(0, ExecutionStatusRefreshIntervalMs); // 启动定时器
                }
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (CurrentFlowResult != null)
                {
                    var receipt = _currentFlowBatch;
                    if (receipt != null)
                    {
                        await FinalizeCurrentFlowRunAsync(new FlowControlData
                        {
                            EventName = "Canceled",
                            Status = StatusTypeEnum.Canceled,
                            SerialNumber = CurrentFlowResult.Code,
                            Message = "测试已取消",
                            Params = "测试已取消",
                            TotalTime = stopwatch.ElapsedMilliseconds,
                        }, receipt);
                    }
                    CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                    RecordFlowFailure("测试已取消");
                    SaveFlowResult(CurrentFlowResult, receipt);
                    SaveObjectiveTestResultRecord(CurrentFlowResult);
                    await TestCompletedAsync();
                }
                else
                {
                    _objectiveSessionCompleted = true;
                }
                throw;
            }
            catch (Exception ex)
            {
                if (CurrentFlowResult != null)
                {
                    CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                    CurrentFlowResult.Msg = $"流程启动异常: {ex.Message}";
                    var receipt = _currentFlowBatch;
                    if (receipt != null)
                    {
                        await FinalizeCurrentFlowRunAsync(new FlowControlData
                        {
                            EventName = "Failed",
                            Status = StatusTypeEnum.Failed,
                            SerialNumber = CurrentFlowResult.Code,
                            Message = CurrentFlowResult.Msg,
                            Params = CurrentFlowResult.Msg,
                            TotalTime = stopwatch.ElapsedMilliseconds,
                        }, receipt);
                    }
                    await ExecuteProcessFailureAsync(runProcessMeta?.Process, receipt);
                    RecordFlowFailure(CurrentFlowResult.Msg);
                    TryAttachCapturedImage(CurrentFlowResult, receipt);
                    SaveFlowResult(CurrentFlowResult, receipt);
                    SaveObjectiveTestResultRecord(CurrentFlowResult);
                    await TestCompletedAsync();
                }
                else
                {
                    _objectiveSessionCompleted = true;
                }
                log.Error($"流程启动异常 => flow={flowTemplate.Key}", ex);
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision");
                TryCount = 0;
                return false;
            }
            finally
            {
                if (!flowStarted && !flowControl.IsFlowRun)
                {
                    await CompleteFlowNodeRecordingAsync();
                    _isFlowLifecycleActive = false;
                }
                _isFlowStartPending = false;
                UpdateTestButtonState();
            }
        }

        private async Task<bool> PreProcessing(string flowName, string serialNumber)
        {
            var serverNodes = new ObservableCollection<CVBaseServerNode>(STNodeEditorMain.Nodes.OfType<CVBaseServerNode>());
            return await PreProcessManager.GetInstance().ExecuteAsync(flowName, serialNumber, serverNodes);
        }

        private async Task<ArvrFlowBatchReceipt> CreateCurrentFlowBatchAsync()
        {
            if (_flowNodeExecutionRecorder.IsRecording())
            {
                log.Warn("流程已停止，收尾上一轮残留的节点诊断记录后继续执行");
                await CompleteFlowNodeRecordingAsync();
            }

            long batchInsertStartedAt = Stopwatch.GetTimestamp();
            var batch = new MeasureBatchModel
            {
                TId = _currentFlowTemplateId > 0 ? _currentFlowTemplateId : null,
                Name = CurrentFlowResult.SN,
                Code = CurrentFlowResult.Code,
            };
            ArvrFlowBatchReceipt receipt = ArvrFlowBatchReceipt.Insert(batch, batchInsertStartedAt);
            _currentFlowBatch = receipt;
            CurrentFlowResult.BatchId = receipt.BatchId;
            CurrentFlowResult.BatchDatabaseKey = ArvrBatchHistory.GetDatabaseKey(receipt.OpenDatabase);
            _currentBatchCreateMs = Stopwatch.GetElapsedTime(batchInsertStartedAt).TotalMilliseconds;
            long recorderStartedAt = Stopwatch.GetTimestamp();
            _flowNodeExecutionRecorder.StartRun(receipt.BatchId, receipt.Code);
            _runningFlowNodes.Reset(receipt.Code);
            _currentNodeRecorderStartMs = Stopwatch.GetElapsedTime(recorderStartedAt).TotalMilliseconds;
            return receipt;
        }

        private async Task FinalizeCurrentFlowRunAsync(FlowControlData flowResult, ArvrFlowBatchReceipt? receipt)
        {
            string serialNumber = string.IsNullOrWhiteSpace(flowResult.SerialNumber)
                ? CurrentFlowResult.Code
                : flowResult.SerialNumber;
            if (receipt != null && (!ReferenceEquals(_currentFlowBatch, receipt)
                || !string.Equals(receipt.Code, serialNumber, StringComparison.Ordinal)))
                return;
            flowResult.SerialNumber = serialNumber;

            long elapsedMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds);
            CurrentFlowResult.RunTime = elapsedMilliseconds;
            CurrentFlowResult.FlowStatus = flowResult.FlowStatus;

            long runRecordStarted = Stopwatch.GetTimestamp();
            bool deferDiagnostics = QueueDiagnosticRun != null && !_isDisposed && flowResult.EventName == "Completed";
            _currentFlowDiagnosticsDeferred = deferDiagnostics;
            Task diagnosticsBackpressure = Task.CompletedTask;
            if (deferDiagnostics)
            {
                Task<int> runWrite = QueueDiagnosticRun!(
                    _currentFlowTemplateId, FlowName, serialNumber, flowResult.FlowStatus, elapsedMilliseconds);
                _currentFlowRunRecordMs = Stopwatch.GetElapsedTime(runRecordStarted).TotalMilliseconds;
                long recorderStarted = Stopwatch.GetTimestamp();
                // CompleteRunAsync seals this recorder before its first await so the next run can start independently.
                Task<bool>? recorderWrite = _flowNodeExecutionRecorder.IsRecording()
                    ? _flowNodeExecutionRecorder.CompleteRunAsync(flushTimeout: TimeSpan.FromSeconds(5))
                    : null;
                _currentNodeRecorderFlushMs = Stopwatch.GetElapsedTime(recorderStarted).TotalMilliseconds;
                Task completion = ObserveFlowDiagnosticsAsync(serialNumber, CurrentFlowResult.BatchId,
                    runRecordStarted, runWrite, recorderWrite);
                diagnosticsBackpressure = _flowDiagnosticsWrites.Track(serialNumber, completion);
            }
            else
            {
                FlowNodeRecordDataBaseHelper.RecordFlowRun(
                    _currentFlowTemplateId, FlowName, serialNumber, flowResult.FlowStatus, elapsedMilliseconds);
                _currentFlowRunRecordMs = Stopwatch.GetElapsedTime(runRecordStarted).TotalMilliseconds;
            }

            // Once a terminal write owns the batch, closing the window must drain it
            // rather than write a second, canceled state for that same run.
            int batchId = receipt?.BatchId ?? CurrentFlowResult.BatchId;
            _currentFlowBatch = null;

            long batchFinalizeStarted = Stopwatch.GetTimestamp();
            try
            {
                if (receipt != null)
                {
                    MeasureBatchModel batch = receipt.Batch;
                    batch.TotalTime = elapsedMilliseconds > int.MaxValue
                        ? int.MaxValue
                        : (int)elapsedMilliseconds;
                    batch.FlowStatus = flowResult.FlowStatus;
                    batch.Result = flowResult.Params ?? flowResult.Message ?? flowResult.EventName;
                    if (!_isDisposed && flowResult.EventName == "Completed")
                        await BatchFinalization.RunAsync(receipt.WriteOutcome);
                    else
                        receipt.WriteOutcome();
                }
                else if (batchId > 0)
                    log.Warn($"回写流程批次缺少原数据库连接 => batchId={batchId}, serialNumber={serialNumber}");
            }
            catch (Exception ex)
            {
                log.Error($"回写流程批次失败 => batchId={batchId}, serialNumber={serialNumber}", ex);
            }
            finally
            {
                _currentBatchFinalizeMs = Stopwatch.GetElapsedTime(batchFinalizeStarted).TotalMilliseconds;
            }

            if (!deferDiagnostics)
            {
                await CompleteFlowNodeRecordingAsync().ConfigureAwait(false);
            }
            else if (!diagnosticsBackpressure.IsCompleted)
            {
                long backpressureStarted = Stopwatch.GetTimestamp();
                if (!await FlowDiagnosticsWriteQueue.WaitAsync(diagnosticsBackpressure, FlowDiagnosticsWriteQueue.WriteWaitTimeout))
                    log.Warn($"等待流程诊断写入队列超时 => serialNumber={serialNumber}");
                _currentFlowDiagnosticsBackpressureMs = Stopwatch.GetElapsedTime(backpressureStarted).TotalMilliseconds;
            }
        }

        private static async Task ObserveFlowDiagnosticsAsync(string serialNumber, int batchId, long queuedAt,
            Task<int> runWrite, Task<bool>? recorderWrite)
        {
            try
            {
                int runRecordId = await runWrite.ConfigureAwait(false);
                bool? recorderFlushed = recorderWrite == null ? null : await recorderWrite.ConfigureAwait(false);
                log.Info($"ARVRFlowDiagnosticsCompletion SerialNumber={serialNumber} BatchId={batchId} "
                    + $"RunRecordId={runRecordId} NodeRecorderFlushed={recorderFlushed} "
                    + FormattableString.Invariant($"ElapsedMs={Stopwatch.GetElapsedTime(queuedAt).TotalMilliseconds:F3}"));
            }
            catch (Exception ex)
            {
                log.Error($"结束流程诊断写入失败 => batchId={batchId}, serialNumber={serialNumber}", ex);
            }
        }

        private async Task CompleteFlowNodeRecordingAsync()
        {
            if (!_flowNodeExecutionRecorder.IsRecording())
                return;

            long recorderFlushStarted = Stopwatch.GetTimestamp();
            try
            {
                _currentNodeRecorderFlushed = await _flowNodeExecutionRecorder.CompleteRunAsync(
                    flushTimeout: TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Error("结束流程节点统计失败", ex);
            }
            finally
            {
                _currentNodeRecorderFlushMs = Stopwatch.GetElapsedTime(recorderFlushStarted).TotalMilliseconds;
            }
        }

        private void ResetStepProgress()
        {
            ProjectConfig.StepIndex = 0;
            stepBar?.Reset();
        }

        private void RefreshStepBar()
        {
            stepBar.Rebuild(ProcessMetas);
        }

        private void SetStepProgress(int stepIndex)
        {
            int normalizedStepIndex = ProcessManager.GetEnabledStepIndex(ProcessMetas, stepIndex);
            if (normalizedStepIndex < 0) return;
            ProjectConfig.StepIndex = normalizedStepIndex;
            stepBar.Start(normalizedStepIndex);
        }

        private async Task HandleFlowStartFailureAsync(string message, IProcess? process, ArvrFlowBatchReceipt receipt, bool persistResult)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500);
            _isFlowLifecycleActive = false;

            CurrentFlowResult.FlowStatus = FlowStatus.Failed;
            CurrentFlowResult.Msg = message;
            await FinalizeCurrentFlowRunAsync(new FlowControlData
            {
                EventName = "Failed",
                Status = StatusTypeEnum.Failed,
                SerialNumber = CurrentFlowResult.Code,
                Message = message,
                Params = message,
                TotalTime = stopwatch.ElapsedMilliseconds,
            }, receipt);
            await ExecuteProcessFailureAsync(process ?? _currentFlowProcess, receipt);
            RecordFlowFailure(message);

            if (persistResult)
            {
                SaveFlowResult(CurrentFlowResult, receipt);
                SaveObjectiveTestResultRecord(CurrentFlowResult);
            }

            log.ErrorFormat("流程启动失败 => flow={0}, code={1}, reason={2}", FlowName, CurrentFlowResult.Code, message);
            if (persistResult)
            {
                await TestCompletedAsync();
            }
            else
            {
                SendProjectResultResponse(
                    _firstFlowFailure?.Code ?? -1,
                    _firstFlowFailure?.Message ?? message,
                    ViewResultManager.Config.UseLegacyARVROutput
                        ? LegacyARVRConverter.ToLegacy(ObjectiveTestResult)
                        : ObjectiveTestResult);
            }
            TryCount = 0;
        }



        private FlowControl flowControl;

        private async Task ExecuteProcessFailureAsync(IProcess? process, ArvrFlowBatchReceipt? receipt = null)
        {
            if (process == null || CurrentFlowResult == null)
                return;

            bool ownsBatch = false;
            try
            {
                ownsBatch = receipt != null && ArvrBatchHistory.ContainsBatch(receipt.Batch, receipt.OpenDatabase);
            }
            catch (Exception ex)
            {
                log.Warn("失败结果无法读取原流程批次。", ex);
            }

            try
            {
                var ctx = new IProcessExecutionContext(ownsBatch ? receipt!.OpenDatabase : null)
                {
                    Batch = ownsBatch ? receipt!.Batch : new MeasureBatchModel
                    {
                        Name = CurrentFlowResult.SN,
                        Code = CurrentFlowResult.Code
                    },
                    Result = CurrentFlowResult,
                    ObjectiveTestResult = ObjectiveTestResult,
                    ImageView = ImageView
                };

                await process.ExecuteFailure(ctx);
                ctx.TryPopulateResultImageDimensions();
            }
            catch (Exception ex)
            {
                log.Error("自定义 IProcess 失败处理异常", ex);
            }
        }

        private void RecordFlowFailure(string? message, int code = -1)
        {
            string normalizedMessage = string.IsNullOrWhiteSpace(message) ? "ARVR Test Fail" : message.Trim();
            string failureMessage = normalizedMessage;

            _lastFlowFailureMessage = failureMessage;
            _firstFlowFailure ??= (code, failureMessage);
            if (CurrentFlowResult != null)
            {
                CurrentFlowResult.Result = false;
                CurrentFlowResult.Msg = failureMessage;
            }
            ObjectiveTestResult.TotalResult = false;
            ObjectiveTestResult.Msg = _firstFlowFailure?.Message ?? failureMessage;
            ShowExecutionResult(code == -2 ? "OverTime" : failureMessage == "测试已取消" ? "Canceled" : "Failed", failureMessage);
        }

        private static IReadOnlyList<MeasureResultImgModel> ReadFlowImages(ArvrFlowBatchReceipt? receipt, int batchId) =>
            receipt != null && receipt.BatchId == batchId
                ? ArvrBatchHistory.ReadCapturedImages(receipt.Batch, receipt.OpenDatabase)
                : [];

        private static void SaveFlowResult(ProjectARVRReuslt result, ArvrFlowBatchReceipt? receipt = null, ResultSqliteWriteTiming? timing = null) =>
            ViewResultManager.Save(result, timing, readMeasureResults: id => ReadFlowImages(receipt, id));

        private void TryAttachCapturedImage(ProjectARVRReuslt result, ArvrFlowBatchReceipt? receipt)
        {
            if (result == null) return;

            if (string.IsNullOrWhiteSpace(result.Model))
                result.Model = FlowName;

            try
            {
                int batchId = result.BatchId;
                if (batchId <= 0) return;

                IReadOnlyList<MeasureResultImgModel> images = ReadFlowImages(receipt, batchId);
                MeasureResultImgModel? image = images
                    .Where(x => !string.IsNullOrWhiteSpace(x.FileUrl))
                    .OrderBy(x => x.ZIndex ?? int.MaxValue)
                    .ThenBy(x => x.Id)
                    .FirstOrDefault(x => ColorVision.FileIO.CVFileReadCache.GetCachedLength(x.FileUrl).HasValue || File.Exists(x.FileUrl));

                if (!string.IsNullOrWhiteSpace(image?.FileUrl))
                    result.FileName = image.FileUrl;
                ResultImageDimensions.TryPopulate(result, _ => images);
            }
            catch (Exception ex)
            {
                log.Warn("失败结果回填拍照图像失败", ex);
            }
        }

        private void SendProjectResultResponse(int code, string message, object responseData)
        {
            if (code != 0)
            {
                ObjectiveTestResult.TotalResult = false;
                ObjectiveTestResult.Msg = message;
                if (CurrentFlowResult != null)
                {
                    CurrentFlowResult.Result = false;
                    CurrentFlowResult.Msg = message;
                }
            }

            if (SocketManager.GetInstance().TcpClients.Count <= 0 || SocketControl.Current.Stream == null)
            {
                log.Info("找不到连接的Socket");
                return;
            }

            var response = new SocketResponse
            {
                Version = "1.0",
                MsgID = string.Empty,
                EventName = "ProjectARVRResult",
                Code = code,
                SerialNumber = SNtextBox.Text,
                Msg = message,
                Data = responseData
            };
            string respString = JsonConvert.SerializeObject(response);
            log.Info(respString);
            SocketMessageManager.GetInstance().AddMessage(new SocketMessage
            {
                Direction = SocketMessageDirection.Sent,
                Content = respString,
                MessageTime = DateTime.Now,
                EventName = response.EventName,
                MsgID = response.MsgID,
                ResponseCode = response.Code
            });
            SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(respString));
        }

        private async void FlowControl_FlowCompleted(object? sender, FlowControlData FlowControlData)
        {
            if (ReferenceEquals(sender, flowControl))
                await CompleteSingleFlowAsync(FlowControlData);
        }

        private async Task CompleteSingleFlowAsync(FlowControlData FlowControlData)
        {
            if (_isDisposed || _isFlowCompletionPending || !_isFlowLifecycleActive
                || CurrentFlowResult == null
                || !string.Equals(CurrentFlowResult.Code, FlowControlData.SerialNumber, StringComparison.Ordinal))
                return;

            _isFlowCompletionPending = true;
            bool completed = FlowControlData.EventName == "Completed";
            if (completed)
                BatchFinalization.BeginCompletion();
            (bool switchNext, SocketResponse? response) handoff;
            try
            {
                UpdateTestButtonState();
                handoff = await ProcessSingleFlowCompletionAsync(FlowControlData);
            }
            finally
            {
                _isFlowCompletionPending = false;
                if (completed)
                    EndFlowBatchCompletion();
                UpdateTestButtonState();
            }

            // The station can reply as soon as it sees the notification. Release
            // the completion guard before advertising that it may advance.
            if (!_isDisposed && handoff.response != null)
                SendCompletedTestResponse(handoff.response);
            else if (!_isDisposed && handoff.switchNext && !BatchFinalization.CloseRequested)
                SwitchPG();
        }

        private async Task<(bool SwitchNext, SocketResponse? Response)> ProcessSingleFlowCompletionAsync(FlowControlData FlowControlData)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            stopwatch.Stop();
            CurrentFlowResult.FlowCompletedAt = DateTime.Now;
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            log.Debug($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
            Stopwatch flowFinalizeStopwatch = Stopwatch.StartNew();
            var receipt = _currentFlowBatch;
            await FinalizeCurrentFlowRunAsync(FlowControlData, receipt);
            if (_isDisposed)
                return default;
            flowFinalizeStopwatch.Stop();
            _currentFlowFinalizeMilliseconds = Math.Max(0, flowFinalizeStopwatch.ElapsedMilliseconds);
            ShowExecutionResult(FlowControlData.EventName, FlowControlData.Params);

            if (FlowControlData.EventName == "Completed")
            {
                _flowRuntimeEstimates.RecordCompleted(_currentRuntimeEstimateKey, stopwatch.ElapsedMilliseconds);
                CurrentFlowResult.Msg = "Completed";
                bool processingSucceeded;
                try
                {
                    processingSucceeded = await Processing(FlowControlData.SerialNumber, receipt);
                }
                catch (Exception ex)
                {
                    if (_isDisposed)
                        return default;
                    CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                    RecordFlowFailure($"结果处理异常: {ex.Message}");
                    SaveFlowResult(CurrentFlowResult, receipt);
                    SaveObjectiveTestResultRecord(CurrentFlowResult);
                    MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                    processingSucceeded = false;
                }

                if (_isDisposed)
                    return default;
                _isFlowLifecycleActive = false;
                if (_luxSession != null)
                {
                    await CompleteLuxFlowAsync(processingSucceeded ? "Completed" : "Failed", CurrentFlowResult.Msg);
                    TryCount = 0;
                    return default;
                }

                bool switchNext = (processingSucceeded || ProjectARVRProConfig.Instance.AllowTestFailures) && !IsTestTypeCompleted();
                SocketResponse? response = switchNext ? null : await TestCompletedAsync(deferSocketResponse: true);
                TryCount = 0;
                return (switchNext, response);
            }
            else if (FlowControlData.EventName == "OverTime")
            {
                log.Warn("流程运行超时，正在重新尝试");
                CurrentFlowResult.FlowStatus = FlowStatus.OverTime;
                CurrentFlowResult.Msg = FlowControlData.Params;
                TryAttachCapturedImage(CurrentFlowResult, receipt);
                SaveFlowResult(CurrentFlowResult, receipt);
                SaveObjectiveTestResultRecord(CurrentFlowResult);

                flowEngine.LoadFromBase64(string.Empty);
                await Refresh();

                if (TryCount < ProjectARVRProConfig.Instance.TryCountMax)
                {
                    _isFlowLifecycleActive = false;
                    await Task.Delay(200);
                    log.Info("重新尝试运行流程");
                    if (_luxSession != null) _ = RunLuxTemplateAsync();
                    else _ = RunTemplate();
                    return default;
                }
                else
                {
                    await ExecuteProcessFailureAsync(_currentFlowProcess, receipt);
                    RecordFlowFailure(CurrentFlowResult.Msg, -2);
                    SaveFlowResult(CurrentFlowResult, receipt);
                    SaveObjectiveTestResultRecord(CurrentFlowResult);
                    _isFlowLifecycleActive = false;
                    if (_luxSession != null)
                    {
                        await CompleteLuxFlowAsync(FlowControlData.EventName, FlowControlData.Params);
                        TryCount = 0;
                        return default;
                    }
                    await TestCompletedAsync();
                }
                TryCount = 0;
            }
            else
            {
                log.Error("流程运行失败" + FlowControlData.EventName + FlowControlData.Params);
                CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                CurrentFlowResult.Msg = FlowControlData.Params;
                await ExecuteProcessFailureAsync(_currentFlowProcess, receipt);
                RecordFlowFailure(CurrentFlowResult.Msg, _firstFlowFailure?.Code ?? -1);
                TryAttachCapturedImage(CurrentFlowResult, receipt);

                SaveFlowResult(CurrentFlowResult, receipt);
                SaveObjectiveTestResultRecord(CurrentFlowResult);
                ShowExecutionResult(FlowControlData.EventName, CurrentFlowResult.Msg);

                TryCount = 0;

                if (_luxSession != null)
                {
                    _isFlowLifecycleActive = false;
                    await CompleteLuxFlowAsync(FlowControlData.EventName, FlowControlData.Params);
                    return default;
                }
                _isFlowLifecycleActive = false;
                if (ProjectARVRProConfig.Instance.AllowTestFailures && !IsTestTypeCompleted())
                    return (true, null);

                await TestCompletedAsync();
            }

            return default;
        }

        private async Task<bool> Processing(string serialNumber, ArvrFlowBatchReceipt? receipt)
        {
            Stopwatch batchLookupStopwatch = Stopwatch.StartNew();
            bool ownsBatch = receipt != null && receipt.BatchId == CurrentFlowResult.BatchId
                && string.Equals(receipt.Code, serialNumber, StringComparison.Ordinal)
                && ArvrBatchHistory.ContainsBatch(receipt.Batch, receipt.OpenDatabase);
            batchLookupStopwatch.Stop();

            if (!ownsBatch)
            {
                CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                RecordFlowFailure("找不到批次号，请检查流程配置");
                SaveFlowResult(CurrentFlowResult);
                SaveObjectiveTestResultRecord(CurrentFlowResult);
                MessageBox.Show(Application.Current.GetActiveWindow(), LocalizedText.Get("找不到批次号，请检查流程配置"), "ColorVision");
                return false;
            }

            ProjectARVRReuslt result = CurrentFlowResult ?? new ProjectARVRReuslt();

            result.BatchId = receipt!.BatchId;
            result.FlowStatus = FlowStatus.Completed;
            result.CreateTime = DateTime.Now;
            result.Result = true;

            try
            {
                log.Debug($"{result.Model}");

                IProcess? process = _currentFlowProcess ?? ResultProcessResolver.Resolve(result, ProcessManager.Processes, ProcessManager.GetResultProcessMappings());
                if (process != null)
                {
                    if (string.IsNullOrWhiteSpace(result.ProcessTypeFullName))
                        ResultProcessResolver.Capture(result, process);

                    string processTypeName = process.GetType().Name;
                    log.Info($"使用本次流程解析器 {processTypeName} 处理 {result.Model}");

                    bool executed = false;
                    bool resultImageDimensionsFromProcessCache = false;
                    Stopwatch processExecutionStopwatch = Stopwatch.StartNew();
                    try
                    {
                        var ctx = new IProcessExecutionContext(receipt.OpenDatabase)
                        {
                            Batch = receipt.Batch,
                            Result = result,
                            ObjectiveTestResult = ObjectiveTestResult,
                            ImageView =ImageView,
                        };
                        executed = await process.Execute(ctx);
                        if (_isDisposed)
                            return false;
                        resultImageDimensionsFromProcessCache = ctx.TryPopulateResultImageDimensions();
                    }
                    catch (Exception ex)
                    {
                        log.Error("自定义 IProcess 执行异常", ex);
                    }
                    finally
                    {
                        processExecutionStopwatch.Stop();
                    }
                    if (_isDisposed)
                        return false;
                    if (executed)
                    {
                        ViewResultManagerConfig exportConfig = ViewResultManager.Config;
                        if (exportConfig.IsSaveImageReuslt || exportConfig.IsSaveSourceImage)
                        {
                            _automaticImageExportResults.Add(result);
                        }

                        bool measureSqliteWrites = log.IsInfoEnabled;
                        ResultSqliteWriteTiming? viewWrite = measureSqliteWrites ? new() : null;
                        Stopwatch viewResultSaveStopwatch = Stopwatch.StartNew();
                        SaveFlowResult(result, receipt, viewWrite);
                        viewResultSaveStopwatch.Stop();
                        ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && result.Result;

                        ResultSqliteWriteTiming? objectiveWrite = measureSqliteWrites ? new() : null;
                        Stopwatch objectiveResultSaveStopwatch = Stopwatch.StartNew();
                        SaveObjectiveTestResultRecord(result, objectiveWrite);
                        objectiveResultSaveStopwatch.Stop();

                        long linkSaveMilliseconds = 0;
                        if (exportConfig.IsSaveLink)
                        {
                            Stopwatch linkSaveStopwatch = Stopwatch.StartNew();
                            bool wasExportPending = IsResultExportPending;
                            IsResultExportPending = true;
                            try
                            {
                                string linkPath = ProjectImageExportService.BuildOutputDirectory(
                                    exportConfig.CsvSavePath, exportConfig.SaveByDate, DateTime.Now, result.SN);
                                using var linkWrite = ResultStorageSpaceManager.Instance.BeginWrite(
                                    exportConfig.CsvSavePath, exportConfig.AutoCleanupEnabled, exportConfig.MinimumFreeSpaceGB, linkPath);
                                await linkWrite.EnsureSpaceAsync();

                                if (!Directory.Exists(linkPath))
                                    Directory.CreateDirectory(linkPath);

                                if (!string.IsNullOrWhiteSpace(result.FileName))
                                {
                                    string shortcutName = Path.GetFileNameWithoutExtension(result.FileName) + $"_{result.Model}";
                                    ColorVision.Common.NativeMethods.ShortcutCreator.CreateShortcut(shortcutName, linkPath, result.FileName, "");
                                }
                            }
                            catch (Exception ex)
                            {
                                log.Error($"保存原图快捷方式失败，流程={result.Model}，SN={result.SN}，结果输出目录={exportConfig.CsvSavePath}", ex);
                            }
                            finally
                            {
                                IsResultExportPending = wasExportPending;
                            }
                            linkSaveStopwatch.Stop();
                            linkSaveMilliseconds = Math.Max(0, linkSaveStopwatch.ElapsedMilliseconds);
                        }

                        DateTime resultProcessingCompletedAt = DateTime.Now;
                        ResultSqliteWriteTiming? timestampWrite = measureSqliteWrites ? new() : null;
                        Stopwatch timestampPersistStopwatch = Stopwatch.StartNew();
                        bool timestampPersisted = false;
                        try
                        {
                            timestampPersisted = ViewResultManager.MarkResultProcessingCompleted(result, resultProcessingCompletedAt, timestampWrite);
                        }
                        catch (Exception ex)
                        {
                            log.Error($"持久化 ARVR 结果处理完成时间失败: {result.Model}", ex);
                        }
                        timestampPersistStopwatch.Stop();
                        LogFlowPhaseTiming(
                            result,
                            batchLookupStopwatch.ElapsedMilliseconds,
                            processExecutionStopwatch.ElapsedMilliseconds,
                            viewResultSaveStopwatch.ElapsedMilliseconds,
                            objectiveResultSaveStopwatch.ElapsedMilliseconds,
                            linkSaveMilliseconds,
                            timestampPersistStopwatch.ElapsedMilliseconds,
                            timestampPersisted,
                            resultImageDimensionsFromProcessCache,
                            (viewWrite, objectiveWrite, timestampWrite));
                        return true;
                    }

                    else
                    {
                        string failureMessage = $"自定义 IProcess 执行失败: {result.Model} -> {processTypeName}";
                        log.Error($"{failureMessage}，当前结果按失败处理");
                        result.FlowStatus = FlowStatus.Failed;
                        RecordFlowFailure(failureMessage);
                        TryAttachCapturedImage(result, receipt);
                        SaveFlowResult(result, receipt);
                        SaveObjectiveTestResultRecord(result);
                        return false;
                    }
                }
                else
                {
                    string failureMessage = $"未匹配到自定义流程: {result.Model}";
                    log.Error(failureMessage);
                    result.FlowStatus = FlowStatus.Failed;
                    RecordFlowFailure(failureMessage);
                }
            }
            catch (Exception ex)
            {
                log.Error("匹配/执行自定义 IProcess 出错", ex);
                result.FlowStatus = FlowStatus.Failed;
                RecordFlowFailure($"匹配/执行自定义 IProcess 出错: {ex.Message}");
            }
            SaveFlowResult(result, receipt);
            SaveObjectiveTestResultRecord(result);
            return false;
        }

        private void SaveObjectiveTestResultRecord(ProjectARVRReuslt result, ResultSqliteWriteTiming? timing = null)
        {
            try
            {
                ObjectiveTestResultRecordId = ViewResultManager.SaveObjectiveTestResult(ObjectiveTestResultRecordId, result, ObjectiveTestResult, timing);
                log.Debug($"保存 ObjectiveTestResult 记录：{ObjectiveTestResultRecordId}");
            }
            catch (Exception ex)
            {
                log.Error("保存 ObjectiveTestResult 记录失败", ex);
            }
        }

        private void FinalizeObjectiveTestResultRecord()
        {
            if (ObjectiveTestResultRecordId <= 0)
                return;

            int recordId = ObjectiveTestResultRecordId;
            DateTime completedAt = DateTime.Now;
            try
            {
                if (ViewResultManager.FinalizeObjectiveTestResult(recordId, completedAt) > 0)
                {
                    log.Info($"最终化 ObjectiveTestResult 记录：{recordId}");
                    return;
                }

                log.Warn($"最终化 ObjectiveTestResult 记录未更新行，将后台重试：{recordId}");
            }
            catch (Exception ex)
            {
                log.Error("最终化 ObjectiveTestResult 记录失败", ex);
            }

            _ = RetryFinalizeObjectiveTestResultRecordAsync(recordId, completedAt);
        }

        private static async Task RetryFinalizeObjectiveTestResultRecordAsync(int recordId, DateTime completedAt)
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250 * attempt)).ConfigureAwait(false);
                try
                {
                    if (ViewResultManager.FinalizeObjectiveTestResult(recordId, completedAt) > 0)
                    {
                        log.Info($"后台重试最终化 ObjectiveTestResult 记录成功：{recordId}，第 {attempt} 次");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($"后台重试最终化 ObjectiveTestResult 记录失败：{recordId}，第 {attempt} 次", ex);
                }
            }

            log.Error($"ObjectiveTestResult 记录最终化重试耗尽：{recordId}");
        }

        private int FindNextEnabledProcessIndex(int afterIndex)
        {
            for (int i = afterIndex + 1; i < ProcessMetas.Count; i++)
            {
                if (ProcessMetas[i].IsEnabled)
                    return i;
            }
            return -1;
        }

        private bool IsTestTypeCompleted() => FindNextEnabledProcessIndex(CurrentTestType) < 0;

        private void SwitchPG()
        {
            NetworkStream? stream = _socketSwitchSessionStream ?? SocketControl.Current.Stream;
            if (stream == null || !ReferenceEquals(stream, SocketControl.Current.Stream))
            {
                _switchPgAcknowledgement.Clear();
                log.Warn("普通切图请求未发送，原控制连接已不可用或被替换");
                return;
            }

            int nextTestType = FindNextEnabledProcessIndex(CurrentTestType);
            if (nextTestType < 0)
                return;

            string switchPGMessage = string.IsNullOrWhiteSpace(_lastFlowFailureMessage) ? "Switch PG" : $"上一流程失败: {_lastFlowFailureMessage}";
            var response = new SocketResponse
            {
                Version = "1.0",
                MsgID = string.Empty,
                EventName = "SwitchPG",
                Code = 0,
                Msg = switchPGMessage,
                SerialNumber = SNtextBox.Text,
                Data = new SwitchPG
                {
                    ARVRTestType = nextTestType + GetProcessEnableSocket.GetIndexOffset()
                },
            };
            _lastFlowFailureMessage = string.Empty;

            Action afterSend = PrepareSocketSwitch(stream, nextTestType, ProcessMetas[nextTestType]);
            try
            {
                SocketControl.WriteSwitchResponse(stream, response, afterSend, recordEndpoint: false);
                _socketSwitchSessionStream = stream;
            }
            catch (Exception ex)
            {
                _switchPgAcknowledgement.Clear();
                log.Error("普通切图请求发送失败", ex);
            }
        }

        private async Task<SocketResponse?> TestCompletedAsync(bool deferSocketResponse = false)
        {
            ClearSocketSwitchSession();
            if (_luxSession != null)
            {
                await CompleteLuxFlowAsync("Failed", CurrentFlowResult?.Msg);
                return null;
            }
            if (_objectiveSessionCompleted)
                return null;

            _objectiveSessionCompleted = true;
            IsResultExportPending = true;
            try
            {
                SocketResponse? response = await ExportCompletedTestAsync();
                if (deferSocketResponse)
                    return response;
                if (response != null)
                    SendCompletedTestResponse(response);
                return null;
            }
            finally
            {
                IsResultExportPending = false;
            }
        }

        private async Task<SocketResponse?> ExportCompletedTestAsync()
        {
            FinalizeObjectiveTestResultRecord();

            log.Info($"ARVR测试完成,TotalResult {ObjectiveTestResult.TotalResult}");

            // Settings and the editable SN can change while background cleanup is awaited.
            var config = ViewResultManager.Config;
            CustomTestResultExportOptions customerOutput = CustomTestResultExportOptions.Capture(config);
            var outputConfig = new
            {
                config.CsvSavePath, config.SaveByDate,
                config.IsSaveCsv, config.UseLegacyARVROutput,
                config.AutoCleanupEnabled, config.MinimumFreeSpaceGB
            };
            string serialNumber = SNtextBox.Text;
            DateTime exportTime = DateTime.Now;
            int objectiveRecordId = ObjectiveTestResultRecordId;
            bool usesLuxReport = customerOutput.Enabled && CustomTestResultExportService.IsLuxProfile(customerOutput.Profile);
            IReadOnlyList<ProjectARVRReuslt>? exportFlowResults = null;

            string csvOutputDirectory = outputConfig.CsvSavePath;
            if (outputConfig.SaveByDate)
            {
                string dateFolder = exportTime.ToString("yyyy-MM-dd");
                csvOutputDirectory = Path.Combine(csvOutputDirectory, dateFolder);
            }

            if (outputConfig.IsSaveCsv)
            {
                try
                {
                    string filePath = Path.Combine(csvOutputDirectory, ProjectARVRResultCsvExporter.BuildFileName(serialNumber, exportTime));
                    string currentSnDirectory = ProjectImageExportService.BuildOutputDirectory(
                        outputConfig.CsvSavePath, outputConfig.SaveByDate, exportTime, serialNumber);
                    using var csvWrite = ResultStorageSpaceManager.Instance.BeginWrite(
                        outputConfig.CsvSavePath, outputConfig.AutoCleanupEnabled, outputConfig.MinimumFreeSpaceGB, currentSnDirectory, filePath);
                    await csvWrite.EnsureSpaceAsync();
                    Directory.CreateDirectory(csvOutputDirectory);

                    if (outputConfig.UseLegacyARVROutput)
                    {
                        var legacyResult = LegacyARVRConverter.ToLegacy(ObjectiveTestResult);
                        LegacyARVRCsvExporter.ExportToCsv(new List<LegacyARVRObjectiveTestResult> { legacyResult }, filePath);
                    }
                    else
                    {
                        IReadOnlyList<ProjectARVRReuslt> flowResults = exportFlowResults = ViewResultManager.GetObjectiveTestFlowResults(objectiveRecordId);
                        // An extension's CSV extractor can modify its input. Preserve the saved rows for the LUX report.
                        IReadOnlyList<ObjectiveTestCsvRow> rows = ProjectARVRResultCsvExporter.CollectRows(
                            usesLuxReport ? flowResults.Select(static result => result.CopyForCsvExport()) : flowResults);
                        if (rows.Count > 0)
                            ProjectARVRResultCsvExporter.ExportRows(rows, filePath);
                        else
                            ObjectiveTestResultCsvExporter.ExportToCsv(ObjectiveTestResult, filePath);
                    }
                }
                catch (Exception ex)
                {
                    log.Error("ObjectiveTestResult CSV导出失败", ex);
                }
            }

            if (customerOutput.Enabled)
            {
                try
                {
                    await ExportCustomerReportAsync(customerOutput, serialNumber, exportTime, objectiveRecordId, flowResults: exportFlowResults);
                }
                catch (Exception ex)
                {
                    log.Error("客户报表导出失败", ex);
                }
            }

            try
            {
                // 根据配置决定输出格式：旧版扁平格式或新版嵌套格式
                object responseData = ObjectiveTestResult;
                if (outputConfig.UseLegacyARVROutput)
                {
                    responseData = LegacyARVRConverter.ToLegacy(ObjectiveTestResult);
                }

                return new SocketResponse
                {
                    Version = "1.0",
                    MsgID = string.Empty,
                    EventName = "ProjectARVRResult",
                    Code = _firstFlowFailure?.Code ?? 0,
                    SerialNumber = serialNumber,
                    Msg = _firstFlowFailure?.Message ?? (ObjectiveTestResult.TotalResult ? "ARVR Test Completed" : "ARVR Test Fail"),
                    Data = responseData
                };
            }
            catch (Exception ex)
            {
                log.Error("生成 ProjectARVRResult 响应失败", ex);
                return null;
            }
        }

        private static void SendCompletedTestResponse(SocketResponse response)
        {
            try
            {
                string respString = JsonConvert.SerializeObject(response);
                log.Info(respString);
                var sentMsg = new SocketMessage
                {
                    Direction = SocketMessageDirection.Sent,
                    Content = respString,
                    MessageTime = DateTime.Now,
                    EventName = response.EventName,
                    MsgID = response.MsgID,
                    ResponseCode = response.Code
                };

                var responseStream = SocketControl.Current.Stream;
                if (SocketManager.GetInstance().TcpClients.Count <= 0 || responseStream == null)
                {
                    log.Info("找不到连接的Socket");
                    return;
                }
                SocketMessageManager.GetInstance().AddMessage(sentMsg);
                responseStream.Write(Encoding.UTF8.GetBytes(respString));
            }
            catch (Exception ex)
            {
                log.Error("ProjectARVRResult响应发送失败", ex);
            }
        }

        private void GridSplitter_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            ViewResultManager.Config.Height = row2.ActualHeight;
            row2.Height = GridLength.Auto;
        }

        private void SaveResultColumnOrder()
        {
            var order = ResultColumnLayout.Capture(((GridView)listView1.View).Columns);
            if (order.SequenceEqual(_initialResultColumnOrder)) return;

            var config = ARVRWindowConfig.Instance;
            config.ResultColumnOrder = order;
            if (!ConfigHandler.GetInstance().TrySave(config, out string error))
                log.Warn($"保存结果表列顺序失败：{error}");
        }

        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            ViewResluts.Clear();
            ImageView.Clear();
            ClearResultOutputText();
        }

        private void Button_Click_EditResultConfig(object sender, RoutedEventArgs e)
        {
            OpenSettings(ProjectSettingsPage.Results);
        }

        public void OpenSettings(ProjectSettingsPage page)
        {
            new ProjectSettingsWindow(ProjectConfig, ViewResultManager.Config, CanCurrentSourceExportBmp(), page)
            {
                Owner = this,
            }.ShowDialog();
        }

        private void OpenResultViewRefreshManager_Click(object sender, RoutedEventArgs e)
        {
            var window = new ResultViewRefreshManagerWindow
            {
                Owner = this,
            };
            bool? saved = window.ShowDialog();
            UpdateResultViewRefreshButtonState();
            if (saved == true)
                log.Info("视图刷新配置已更新并保存。");
        }

        private void UpdateResultViewRefreshButtonState()
        {
            int enabledCount = ResultViewRefreshDiscovery.Discover().Count(item => item.IsWarning);
            ViewRefreshManagerButton.Content = enabledCount > 0
                ? LocalizedText.Format($"视图刷新（{enabledCount}项开启）")
                : LocalizedText.Get("视图刷新（已关闭）");
            ViewRefreshManagerButton.ToolTip = enabledCount > 0
                ? LocalizedText.Get("仍有已加载的读图或结果视图在自动刷新，点击查看")
                : LocalizedText.Get("所有已加载的读图与结果视图均已关闭自动刷新");

            if (enabledCount > 0)
            {
                ViewRefreshManagerButton.Background = new SolidColorBrush(Color.FromArgb(64, 255, 152, 0));
                ViewRefreshManagerButton.BorderBrush = new SolidColorBrush(Color.FromRgb(230, 144, 0));
            }
            else
            {
                ViewRefreshManagerButton.ClearValue(Control.BackgroundProperty);
                ViewRefreshManagerButton.ClearValue(Control.BorderBrushProperty);
            }
        }

        private void DisplayControls_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
            RefreshResultViewButtonOnDispatcher();

        private void ViewRefreshConfig_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewConfigBase.AutoRefreshView))
                RefreshResultViewButtonOnDispatcher();
        }

        private void RefreshResultViewButtonOnDispatcher()
        {
            if (_isDisposed)
                return;

            Dispatcher.BeginInvoke(() =>
            {
                if (!_isDisposed)
                    UpdateResultViewRefreshButtonState();
            });
        }

        public Task OpenBatchResultAsync(MeasureBatchModel batch, string flowName)
        {
            ArgumentNullException.ThrowIfNull(batch);
            return OpenBatchResultAsync(batch, flowName, ArvrBatchHistory.CaptureDatabase(batch));
        }

        internal async Task OpenBatchResultAsync(MeasureBatchModel batch, string flowName, Func<SqlSugarClient>? openBatchDatabase)
        {
            if (!Dispatcher.CheckAccess())
            {
                Task dispatchedTask = await Dispatcher.InvokeAsync(() => OpenBatchResultAsync(batch, flowName, openBatchDatabase));
                await dispatchedTask;
                return;
            }

            string? databaseKey = openBatchDatabase == null ? null : ArvrBatchHistory.GetDatabaseKey(openBatchDatabase);
            ProjectARVRReuslt? existingResult = ViewResultManager.FindByOwnedBatch(batch, flowName, databaseKey);
            if (existingResult != null)
            {
                SelectViewResult(existingResult);
                return;
            }

            if (IsTestExecutionBusy)
            {
                log.Warn($"当前测试尚未结束，暂不重放历史批次：{batch.Id}");
                MessageBox.Show(this, LocalizedText.Get("当前测试尚未结束，请在测试完成后再打开历史批次。"), "ColorVision");
                return;
            }

            bool foundBatch = false;
            if (openBatchDatabase != null)
            {
                try
                {
                    foundBatch = ArvrBatchHistory.ContainsBatch(batch, openBatchDatabase);
                }
                catch (Exception ex)
                {
                    log.Warn($"读取原始批次失败，BatchId={batch.Id}, Flow={flowName}, Error={ex.GetType().Name}");
                }
            }
            if (!foundBatch || openBatchDatabase == null)
            {
                log.Warn($"未找到原始批次，无法重放：BatchId={batch.Id}, Flow={flowName}");
                MessageBox.Show(this, LocalizedText.Get("无法读取该批次的原始数据库，未重放结果。"), "ColorVision");
                return;
            }

            string serialNumber = batch.Name ?? batch.Code ?? string.Empty;
            var replayObjective = new ObjectiveTestResult { SessionStartTime = batch.CreateDate ?? DateTime.Now };

            var result = new ProjectARVRReuslt
            {
                BatchId = batch.Id,
                BatchDatabaseKey = databaseKey,
                Model = flowName,
                SN = serialNumber,
                Code = batch.Code ?? string.Empty,
                FlowStatus = batch.FlowStatus,
                Result = batch.FlowStatus == FlowStatus.Completed,
                RunTime = batch.TotalTime,
                Msg = batch.Result ?? string.Empty,
                CreateTime = batch.CreateDate ?? DateTime.Now
            };

            IProcess? process = ResultProcessResolver.Resolve(
                result,
                ProcessManager.Processes,
                ProcessManager.GetResultProcessMappings());
            if (process == null)
            {
                result.Result = false;
                result.FlowStatus = FlowStatus.Failed;
                result.Msg = $"未配置 ARVR 结果解析器: {flowName}";
                log.Error(result.Msg);
            }
            else if (result.FlowStatus == FlowStatus.Completed)
            {
                ResultProcessResolver.Capture(result, process);
                try
                {
                    var ctx = new IProcessExecutionContext(openBatchDatabase)
                    {
                        Batch = batch,
                        Result = result,
                        ObjectiveTestResult = replayObjective,
                        ImageView = ImageView
                    };
                    bool executed = await process.Execute(ctx);
                    ctx.TryPopulateResultImageDimensions();
                    if (!executed)
                    {
                        result.Result = false;
                        result.FlowStatus = FlowStatus.Failed;
                        result.Msg = $"ARVR 结果解析失败: {flowName} -> {process.GetType().Name}";
                        log.Error(result.Msg);
                    }
                    else
                    {
                        replayObjective.TotalResult &= result.Result;
                    }
                }
                catch (Exception ex)
                {
                    result.Result = false;
                    result.FlowStatus = FlowStatus.Failed;
                    result.Msg = $"ARVR 结果解析异常: {flowName} -> {process.GetType().Name}";
                    log.Error(result.Msg, ex);
                }
            }

            ViewResultManager.Save(result, readMeasureResults: id => id == batch.Id
                ? ArvrBatchHistory.ReadCapturedImages(batch, openBatchDatabase) : []);
            try
            {
                replayObjective.TotalResult &= result.Result;
                ViewResultManager.SaveObjectiveTestResult(0, result, replayObjective);
            }
            catch (Exception ex)
            {
                log.Error($"保存 ARVR 重放汇总失败：BatchId={batch.Id}", ex);
            }
            SelectViewResult(result);
        }

        private void SelectViewResult(ProjectARVRReuslt result)
        {
            ProjectARVRReuslt? displayResult = ViewResluts.FirstOrDefault(item =>
                ReferenceEquals(item, result) || (result.Id > 0 && item.Id == result.Id));
            if (displayResult == null)
            {
                ViewResultManager.AddLiveResult(ViewResluts, result);
                displayResult = result;
            }

            listView1.SelectedItem = null;
            listView1.SelectedItem = displayResult;
            listView1.ScrollIntoView(displayResult);
        }

        private void listView1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isDisposed)
                return;

            long requestVersion = Interlocked.Increment(ref _resultImagePresentationVersion);
            Interlocked.Exchange(ref _resultImagePresentationCancellation, null)?.Cancel();

            if (sender is ListView listView && listView.SelectedItem is ProjectARVRReuslt result)
            {
                bool needsHistoryPayload = result.Id > 0 && result.ViewResultJson == null;
                try
                {
                    if (result.FlowStatus != FlowStatus.Completed)
                    {
                        RenderResultOutputText(result);
                    }
                    else if (needsHistoryPayload)
                    {
                        ClearResultOutputText();
                        SNtextBox.Focus();
                    }
                    else
                    {
                        GenoutputText(result);
                    }
                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
                if (!IsCurrentResultImageRequest(requestVersion, result))
                    return;

                ResultImageFilePaths imagePaths = ResultImageFilePaths.Capture(result);
                CancellationTokenSource requestCancellation = new();
                Interlocked.Exchange(ref _resultImagePresentationCancellation, requestCancellation)?.Cancel();
                _ = Application.Current.Dispatcher.BeginInvoke(async () =>
                {
                    bool gateEntered = false;
                    try
                    {
                        if (!IsCurrentResultImageRequest(requestVersion, result))
                            return;

                        // Historical reads have their own gate. A slow read must not hold up a live result image.
                        if (needsHistoryPayload)
                        {
                            try
                            {
                                if (result.ViewResultJson == null)
                                {
                                    string? json = await ViewResultManager.ReadHistoricalViewResultJsonAsync(result.Id, requestCancellation.Token);
                                    requestCancellation.Token.ThrowIfCancellationRequested();
                                    if (!IsCurrentResultImageRequest(requestVersion, result))
                                        return;
                                    result.ViewResultJson ??= json ?? string.Empty;
                                }
                                if (result.FlowStatus == FlowStatus.Completed)
                                    RenderResultOutputText(result);
                            }
                            catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception ex)
                            {
                                log.Error("读取或显示历史结果详情失败", ex);
                            }
                        }

                        await _resultImagePresentationGate.WaitAsync(requestCancellation.Token);
                        gateEntered = true;
                        if (!IsCurrentResultImageRequest(requestVersion, result))
                            return;

                        bool hasDisplaySurface = false;
                        bool renderOverlays = true;
                        ResultImageFileCandidate? openedCandidate = await ResultImageFileCandidates.OpenFirstAsync(
                            imagePaths,
                            async (candidate, cancellationToken) =>
                            {
                                BitmapSource? loadedSource = await ResultImageLoader.OpenAsync(ImageView, candidate.FilePath, cancellationToken);
                                if (!IsCurrentResultImageRequest(requestVersion, result))
                                    throw new OperationCanceledException(cancellationToken);
                                return loadedSource != null;
                            },
                            (candidate, exception) =>
                            {
                                if (exception is TimeoutException)
                                    log.Warn($"加载结果图片超时，将尝试下一候选图：{candidate.FilePath}", exception);
                                else if (exception != null)
                                    log.Warn($"加载结果图片失败，将尝试下一候选图：{candidate.FilePath}", exception);
                                else
                                    log.Warn($"加载结果图片后没有有效图像，将尝试下一候选图：{candidate.FilePath}");
                            },
                            requestCancellation.Token);
                        requestCancellation.Token.ThrowIfCancellationRequested();
                        if (!IsCurrentResultImageRequest(requestVersion, result))
                            return;

                        if (openedCandidate is ResultImageFileCandidate candidate
                            && GetLoadedImageSource() is BitmapSource)
                        {
                            hasDisplaySurface = true;
                            renderOverlays = candidate.RequiresOverlayRendering;
                            if (candidate.Kind != ResultImageFileKind.Original)
                                log.Info($"原始结果图不可用，已改用{DescribeResultImageCandidate(candidate.Kind)}：{candidate.FilePath}");
                        }

                        if (!hasDisplaySurface)
                        {
                            if (TryGetResultImageDimensions(result, out int width, out int height))
                            {
                                ShowResultImagePlaceholder(width, height);
                                hasDisplaySurface = true;
                            }
                            else
                            {
                                ClearResultImageSurface();
                                log.Warn($"结果图片不存在且没有可用尺寸，已清除旧底图：resultId={result.Id}, file={result.FileName}");
                            }
                        }

                        if (hasDisplaySurface && HasResultDisplaySurface())
                        {
                            if (renderOverlays)
                                RenderResultImage(result);
                            else
                                ShowSavedResultImage(result);
                        }
                        else
                        {
                            ImageView.NotifyExternalRenderCompleted(result, succeeded: false);
                        }
                    }
                    catch (OperationCanceledException) when (requestCancellation.IsCancellationRequested)
                    {
                        if (!ReferenceEquals(listView1.SelectedItem, result))
                            _automaticImageExportResults.Remove(result);
                    }
                    catch (Exception ex)
                    {
                        if (IsCurrentResultImageRequest(requestVersion, result))
                        {
                            ImageView.NotifyExternalRenderCompleted(result, succeeded: false);
                            ClearResultImageSurface();
                        }
                        log.Error("加载结果图片失败", ex);
                    }
                    finally
                    {
                        if (gateEntered)
                            _resultImagePresentationGate.Release();
                        Interlocked.CompareExchange(ref _resultImagePresentationCancellation, null, requestCancellation);
                        requestCancellation.Dispose();
                    }
                });
            }
            else
            {
                ClearResultOutputText();
            }
        }

        private void RenderResultImage(ProjectARVRReuslt result)
        {
            bool succeeded = false;
            try
            {
                ImageView.ImageShow.Clear();
                ApplyResultOverlayConfig();

                if (result.FlowStatus != FlowStatus.Completed)
                    return;

                IProcess? process = ResultProcessResolver.ResolveForRender(result, ProcessManager.Processes, ProcessManager.GetResultProcessMappings());
                if (process == null)
                    return;

                var ctx = new IProcessExecutionContext
                {
                    Result = result,
                    ObjectiveTestResult = ObjectiveTestResult,
                    ImageView = ImageView,
                };
                process.Render(ctx);
                succeeded = HasResultDisplaySurface();
            }
            catch (Exception ex)
            {
                log.Error("自定义 IProcess 执行异常", ex);
            }
            finally
            {
                ImageView.NotifyExternalRenderCompleted(result, succeeded);
            }
        }

        private void ShowSavedResultImage(ProjectARVRReuslt result)
        {
            ImageView.ImageShow.Clear();
            ImageView.NotifyExternalRenderCompleted(result, succeeded: HasResultDisplaySurface());
        }

        private static string DescribeResultImageCandidate(ResultImageFileKind kind) => kind switch
        {
            ResultImageFileKind.SavedSource => "已保存原图并重新渲染标记",
            ResultImageFileKind.SavedResult => "已保存标记图",
            _ => "算法原图",
        };

        private readonly SemaphoreSlim _resultImagePresentationGate = new(1, 1);
        private readonly HashSet<ProjectARVRReuslt> _automaticImageExportResults = new(ReferenceEqualityComparer.Instance);

        private bool IsCurrentResultImageRequest(long requestVersion, ProjectARVRReuslt result)
        {
            return !_isDisposed
                && requestVersion == Volatile.Read(ref _resultImagePresentationVersion)
                && ReferenceEquals(listView1.SelectedItem, result);
        }

        private static bool TryGetResultImageDimensions(ProjectARVRReuslt result, out int width, out int height)
        {
            width = result.ImageWidth.GetValueOrDefault();
            height = result.ImageHeight.GetValueOrDefault();
            return width > 0 && height > 0;
        }

        private void ShowResultImagePlaceholder(int width, int height)
        {
            DrawingImage placeholder = _resultImagePlaceholderCache.GetOrCreate(width, height);
            if (!_resultImagePlaceholderCache.IsCurrent(ImageView.ImageShow.Source, width, height))
            {
                ImageView.Clear();
                ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.Cols, width, nameof(ARVRWindow), LocalizedText.Get("历史结果坐标空间宽度"));
                ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.Rows, height, nameof(ARVRWindow), LocalizedText.Get("历史结果坐标空间高度"));
                ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.ImageWidth, width, nameof(ARVRWindow), LocalizedText.Get("历史结果图像像素宽度"));
                ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.ImageHeight, height, nameof(ARVRWindow), LocalizedText.Get("历史结果图像像素高度"));
                ImageView.SetImageSource(placeholder, enableEditorImageServices: false, configureDefaultLayerController: false);
                ImageView.UpdateZoomAndScale();
            }
        }

        private void ClearResultImageSurface()
        {
            string? activeFilePath = ImageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
            if (ImageView.ImageShow.Source != null
                || !string.IsNullOrWhiteSpace(activeFilePath))
            {
                ImageView.Clear();
            }
        }

        private bool HasResultDisplaySurface()
        {
            return ImageView.ImageShow.Source != null;
        }

        private BitmapSource? GetLoadedImageSource() => ResultImageLoader.GetCurrentBitmap(ImageView);

        private void ImageView_ExternalRenderCompleted(
            object? sender,
            ImageViewExternalRenderCompletedEventArgs e)
        {
            if (e.Context is not ProjectARVRReuslt result
                || !_automaticImageExportResults.Remove(result))
                return;

            if (_isDisposed
                || !e.Succeeded
                || e.Source is not BitmapSource
                || !ImageView.IsCurrentImageRevision(e.ImageRevision))
            {
                log.Warn("图像导出已取消：本次结果的图像加载或外部渲染未成功完成。");
                return;
            }

            log.Debug("ImageEditor图像加载及外部点位渲染已完成，开始捕获本次结果快照。");
            StartImageExportFromLoadedImage(result);
        }

        private bool CanCurrentSourceExportBmp()
        {
            if (!ImageView.Dispatcher.CheckAccess())
                return ImageView.Dispatcher.Invoke(CanCurrentSourceExportBmp);

            BitmapSource? source = GetLoadedImageSource();
            return source != null && ColorVision.ImageEditor.ImageView.CanBmpPreserveSourceBitDepth(source.Format);
        }

        private void StartImageExportFromLoadedImage(ProjectARVRReuslt result)
        {
            ViewResultManagerConfig config = ViewResultManager.Config;
            bool saveResultImage = config.IsSaveImageReuslt;
            bool saveSourceImage = config.IsSaveSourceImage;
            ResultImageFormat resultFormat = config.ResultSnapshotFormat;
            ImageExportSize resultSize = config.ResultSnapshotSize;
            bool includeOverlays = saveResultImage && config.ResultSnapshotIncludeOverlays;
            bool useHeatmap = saveResultImage && config.ResultSnapshotSaveHeatmap;
            SourceImageFormat sourceFormat = config.SourceExportFormat;
            SourceTiffCompression sourceTiffCompression = config.SourceTiffCompressionMode;
            string? resultFileNameTemplate = config.UseFlowNameForResultImage ? config.ResultImageFileNameTemplate : null;
            string? sourceFileNameTemplate = config.UseFlowNameForSourceImage ? config.SourceImageFileNameTemplate : null;
            string outputRoot = config.CsvSavePath;
            bool saveByDate = config.SaveByDate;
            bool autoCleanupEnabled = config.AutoCleanupEnabled;
            int minimumFreeSpaceGB = config.MinimumFreeSpaceGB;
            DateTime requestedAt = result.CreateTime == default ? DateTime.Now : result.CreateTime;

            ImageViewSnapshot? snapshot = null;
            try
            {
                if (_isDisposed)
                    return;
                if (!saveResultImage && !saveSourceImage)
                    return;
                ImageView.Dispatcher.VerifyAccess();

                log.Debug($"准备图像导出：8位标记图={saveResultImage}，保留位深原图={saveSourceImage}");

                BitmapSource? loadedSource = GetLoadedImageSource();
                if (loadedSource == null)
                {
                    log.Warn("图像导出失败：渲染完成后ImageEditor仍没有有效像素源；不会回读CVRAW或其他磁盘文件。");
                    return;
                }

                if (saveSourceImage
                    && sourceFormat == SourceImageFormat.BMP
                    && !ColorVision.ImageEditor.ImageView.CanBmpPreserveSourceBitDepth(loadedSource.Format))
                {
                    saveSourceImage = false;
                    log.Warn(
                        $"当前原图格式为 {loadedSource.Format}（{loadedSource.Format.BitsPerPixel}bpp），"
                        + "BMP无法逐像素保留该位深；已跳过原图BMP，请改选PNG或TIFF。");
                }

                if (!saveResultImage && !saveSourceImage)
                    return;

                Stopwatch snapshotStopwatch = Stopwatch.StartNew();
                snapshot = ImageView.CaptureSnapshotForBackgroundSave(includeOverlays);
                snapshotStopwatch.Stop();
                if (snapshot == null)
                {
                    log.Warn("图像导出失败：ImageEditor无法生成后台快照。");
                    return;
                }
                if (log.IsInfoEnabled)
                    log.Info(JsonConvert.SerializeObject(new
                    {
                        Event = "ARVRImageSnapshotTiming", result.SN, result.BatchId, result.Model,
                        ResultId = result.Id, SourcePixelFormat = loadedSource.Format.ToString(),
                        SourceWidth = loadedSource.PixelWidth, SourceHeight = loadedSource.PixelHeight,
                        SaveRendered = saveResultImage, SaveSource = saveSourceImage, IncludeOverlays = includeOverlays, UseHeatmap = useHeatmap,
                        ResultFormat = resultFormat.ToString(), ResultSize = resultSize.ToString(),
                        SourceFormat = sourceFormat.ToString(), SourceTiffCompression = sourceTiffCompression.ToString(),
                        CaptureMs = snapshotStopwatch.ElapsedMilliseconds,
                    }));

                if (_isDisposed)
                    return;

                _ = ExportImagesAsync(
                    snapshot,
                    ImageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath),
                    saveResultImage,
                    saveSourceImage,
                    resultFormat,
                    resultSize,
                    includeOverlays,
                    useHeatmap,
                    sourceFormat,
                    sourceTiffCompression,
                    resultFileNameTemplate,
                    sourceFileNameTemplate,
                    result,
                    outputRoot,
                    saveByDate,
                    requestedAt,
                    autoCleanupEnabled,
                    minimumFreeSpaceGB);
                snapshot = null;
            }
            catch (Exception ex)
            {
                log.Error("准备ImageEditor图像导出任务失败", ex);
            }
            finally
            {
                snapshot?.Dispose();
            }
        }

        private void OpenLocalCacheManager_Click(object sender, RoutedEventArgs e)
        {
            ColorVision.Engine.Services.Devices.Camera.Local.LocalCalibrationCacheManagerWindow.OpenWindow(ImageView.SnapshotCacheId);
        }

        private int _outstandingImageExports;

        private async Task ExportImagesAsync(
            ImageViewSnapshot? snapshot,
            string? inputFileName,
            bool saveResultImage,
            bool saveSourceImage,
            ResultImageFormat resultFormat,
            ImageExportSize resultSize,
            bool includeOverlays,
            bool useHeatmap,
            SourceImageFormat sourceFormat,
            SourceTiffCompression sourceTiffCompression,
            string? resultFileNameTemplate,
            string? sourceFileNameTemplate,
            ProjectARVRReuslt result,
            string outputRoot,
            bool saveByDate,
            DateTime requestedAt,
            bool autoCleanupEnabled,
            int minimumFreeSpaceGB)
        {
            string? renderedFilePath = null;
            string? sourceFilePath = null;
            long submittedAt = Stopwatch.GetTimestamp();
            Stopwatch? exportStopwatch = null;
            double? storageWaitMs = null, filePublishMs = null, resultPathPersistMs = null;
            bool exportCompleted = false;
            string pathUpdateStatus = "NotRequired";
            ProjectImageExportAttempt? exportAttempt = null;
            ResultStorageSpaceManager.WriteLease? storageWrite = null;
            ProjectImageExportAttemptResult exportResult = new();
            int outstandingAtSubmit = Interlocked.Increment(ref _outstandingImageExports);
            try
            {
                if (_isDisposed)
                    return;

                string outputDirectory = ProjectImageExportService.BuildOutputDirectory(
                    outputRoot,
                    saveByDate,
                    requestedAt,
                    result.SN);

                if (snapshot == null)
                    return;

                string sourceName = string.IsNullOrWhiteSpace(result.FileName)
                    ? $"Image_{result.Id}_{requestedAt:yyyyMMddTHHmmssfffffff}"
                    : result.FileName;
                var (renderedStem, sourceStem) = ProjectImageExportService.BuildFileStems(
                    sourceName, result.Model, result.SN, resultFileNameTemplate, sourceFileNameTemplate);
                if (saveResultImage)
                {
                    renderedFilePath = ProjectImageExportService.BuildFilePath(
                        outputDirectory,
                        renderedStem,
                        ProjectImageExportService.GetResultExtension(resultFormat));
                    string overlayDescription = includeOverlays ? "混合标记" : "仅底图";
                    log.Debug(
                        $"后台导出8位标记图：{resultFormat}，{DescribeImageSize(resultSize)}，{overlayDescription}，"
                        + (resultFormat == ResultImageFormat.JPEG ? "JPEG质量100" : "PNG自动压缩"));
                }
                if (saveSourceImage)
                {
                    sourceFilePath = ProjectImageExportService.BuildSourceFilePath(
                        outputDirectory,
                        sourceStem,
                        ProjectImageExportService.GetSourceExtension(sourceFormat),
                        renderedFilePath);
                    string sourceDescription = sourceFormat switch
                    {
                        SourceImageFormat.TIFF => $"TIFF {sourceTiffCompression}无损压缩",
                        SourceImageFormat.PNG => "PNG自动无损压缩",
                        _ => "BMP（仅8位源图）",
                    };
                    log.Debug($"后台导出原尺寸、原位深、无标记原图：{sourceDescription}");
                }

                long storageStartedAt = Stopwatch.GetTimestamp();
                try
                {
                    storageWrite = ResultStorageSpaceManager.Instance.BeginWrite(
                        outputRoot, autoCleanupEnabled, minimumFreeSpaceGB, outputDirectory);
                    await storageWrite.EnsureSpaceAsync().ConfigureAwait(false);
                }
                finally { storageWaitMs = Stopwatch.GetElapsedTime(storageStartedAt).TotalMilliseconds; }
                exportAttempt = new ProjectImageExportAttempt(renderedFilePath, sourceFilePath, inputFileName);
                ImageViewSnapshotExportOptions exportOptions = exportAttempt.CreateOptions(
                    ProjectImageExportService.CreateRenderedOptions(resultFormat, resultSize, useHeatmap),
                    ProjectImageExportService.CreateSourceOptions(sourceFormat, sourceTiffCompression),
                    ProjectImageExportService.BuildDiagnosticContext(
                        result.SN,
                        result.Id,
                        result.BatchId,
                        result.Model));

                exportStopwatch = Stopwatch.StartNew();
                ImageViewSnapshot ownedSnapshot = snapshot;
                snapshot = null;
                await ColorVision.ImageEditor.ImageView.SaveSnapshotExportsAsync(
                    ownedSnapshot,
                    exportOptions).ConfigureAwait(false);
                exportCompleted = true;
            }
            catch (Exception ex)
            {
                log.Error("图像导出任务失败；已停止本任务，之前已经写盘的文件不会回滚。", ex);
            }
            finally
            {
                try
                {
                    exportStopwatch?.Stop();
                    if (exportAttempt != null)
                    {
                        long publishStartedAt = Stopwatch.GetTimestamp();
                        try
                        {
                            exportResult = exportAttempt.CommitSuccessfulChannels((channel, fileName, ex) =>
                                log.Error($"{channel}已编码，但替换正式导出文件失败：{fileName}", ex));
                            exportAttempt.Dispose();
                        }
                        finally { filePublishMs = Stopwatch.GetElapsedTime(publishStartedAt).TotalMilliseconds; }
                    }
                    ResultImageExportPathUpdate pathUpdate = ResultImageExportPathUpdate.From(
                        exportResult,
                        includeOverlays,
                        result.SavedResultImageFileName);
                    if (pathUpdate.UpdateSavedResultImageFileName || pathUpdate.UpdateSavedSourceImageFileName)
                    {
                        long persistStartedAt = Stopwatch.GetTimestamp();
                        try
                        {
                            pathUpdateStatus = ViewResultManager.UpdateSavedImagePaths(result, pathUpdate) ? "Updated" : "Unchanged";
                        }
                        catch (Exception ex)
                        {
                            pathUpdateStatus = "Failed";
                            log.Error("图像已写盘，但保存本次成功导出路径到结果数据库失败；内存结果未更新。", ex);
                        }
                        finally { resultPathPersistMs = Stopwatch.GetElapsedTime(persistStartedAt).TotalMilliseconds; }
                    }
                    LogExportedImage("8位标记图", exportResult.RenderedFileName);
                    LogExportedImage("原位深原图", exportResult.SourceFileName);
                    if (exportStopwatch != null)
                    {
                        string outcome = exportCompleted ? "完成" : "结束（含失败）";
                        log.Info($"ImageEditor图像导出任务{outcome}，总耗时 {exportStopwatch.ElapsedMilliseconds}ms。");
                    }
                    storageWrite?.Dispose();
                    snapshot?.Dispose();
                }
                finally
                {
                    int outstandingAfterCompletion = Interlocked.Decrement(ref _outstandingImageExports);
                    if (log.IsInfoEnabled)
                        log.Info(JsonConvert.SerializeObject(new
                        {
                            Event = "ARVRImagePublicationTiming", result.SN, result.BatchId, result.Model,
                            ResultId = result.Id, SaveRendered = saveResultImage, SaveSource = saveSourceImage,
                            EncodingCompleted = exportCompleted,
                            RenderedPublished = exportResult.RenderedFileName != null,
                            SourcePublished = exportResult.SourceFileName != null,
                            PathUpdateStatus = pathUpdateStatus,
                            OutstandingAtSubmit = outstandingAtSubmit,
                            OutstandingAfterCompletion = outstandingAfterCompletion,
                            StorageWaitMs = storageWaitMs,
                            SnapshotPipelineMs = exportStopwatch?.Elapsed.TotalMilliseconds,
                            FilePublishMs = filePublishMs,
                            ResultPathPersistMs = resultPathPersistMs,
                            TotalMs = Stopwatch.GetElapsedTime(submittedAt).TotalMilliseconds,
                        }));
                }
            }
        }

        private static string DescribeImageSize(ImageExportSize size) => size switch
        {
            ImageExportSize.二分之一尺寸 => "1/2尺寸",
            ImageExportSize.四分之一尺寸 => "1/4尺寸",
            _ => "完整尺寸",
        };

        private static void LogExportedImage(string label, string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return;

            FileInfo file = new(filePath);
            log.Info($"{label}写盘完成：{filePath}，{file.Length / 1024d / 1024d:F2}MiB。");
        }

        private void ApplyResultOverlayConfig()
        {
            var config = ProjectARVRProConfig.Instance;
            ImageView.Config.IsShowText = config.ResultOverlayShowName;
            ImageView.Config.IsShowMsg = config.ResultOverlayShowDetail;
            ImageView.Config.DrawingTextFontSize = config.ResultOverlayFontSize;
            ImageView.Config.IsLayoutUpdated = config.ResultOverlayAutoRefresh;
            ImageView.ImageShow.TextFontSizeOverride = config.ResultOverlayFontSize;
            ImageView.ImageShow.ApplyLayoutScaleToVisuals();
        }

        private void ProjectConfig_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.PropertyName) && !ResultOverlayConfigNames.Contains(e.PropertyName))
                return;

            ApplyResultOverlayConfig();
        }

        public void GenoutputText(ProjectARVRReuslt result)
        {
            RenderResultOutputText(result);
            SNtextBox.Focus();
        }

        private void RenderResultOutputText(ProjectARVRReuslt result)
        {
            if (result.FlowStatus != FlowStatus.Completed)
            {
                ClearResultOutputText();
                if (CreateFailedResultParagraph(result) is { } failure)
                {
                    outputText.Document.Blocks.Add(failure);
                    outputText.ScrollToHome();
                }
                return;
            }

            outputText.Background = result.Result ? Brushes.Lime : Brushes.Red;
            outputText.Document.Blocks.Clear(); // 清除之前的内容

            string outtext = $"Model:{result.Model}  SN:{result.SN}  {DateTime.Now:yyyy/MM//dd HH:mm:ss}";
            double outputFontSize = outputText.FontSize > 0 ? outputText.FontSize + 1 : 13;
            Run run = new Run(outtext);
            run.Foreground = result.Result ? Brushes.Black : Brushes.White;
            run.FontSize = outputFontSize;

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(run);
            outputText.Document.Blocks.Add(paragraph);

            IProcess? process = ResultProcessResolver.Resolve(result, ProcessManager.Processes, ProcessManager.GetResultProcessMappings());
            Brush foreground = result.Result ? Brushes.Black : Brushes.White;
            paragraph = new Paragraph();
            if (process != null)
            {
                try
                {
                    var ctx = new IProcessExecutionContext
                    {
                        Result = result,
                        ObjectiveTestResult = ObjectiveTestResult,
                        ImageView = ImageView,
                    };
                    process.GenText(ctx, paragraph, foreground, outputFontSize);
                }
                catch (Exception ex)
                {
                    log.Error("自定义 IProcess 执行异常", ex);
                }
            }

            AppendOutputLine(paragraph, string.Empty, foreground, outputFontSize);
            AppendOutputLine(paragraph, "Pass/Fail Criteria:", foreground, outputFontSize);
            AppendOutputLine(paragraph, result.Result ? "Pass" : "Fail", foreground, outputFontSize);
            outputText.Document.Blocks.Add(paragraph);
        }

        private void ClearResultOutputText()
        {
            outputText.Document.Blocks.Clear();
            outputText.Background = Brushes.Transparent;
        }

        private static void AppendOutputLine(Paragraph paragraph, string text, Brush foreground, double fontSize)
        {
            if (paragraph.Inlines.Count > 0)
                paragraph.Inlines.Add(new LineBreak());

            paragraph.Inlines.Add(CreateOutputRun(text, foreground, fontSize));
        }

        private static Run CreateOutputRun(string text, Brush foreground, double fontSize)
        {
            return new Run(text)
            {
                Foreground = foreground,
                FontSize = fontSize
            };
        }

        private void listView1_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {

        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }
        private void SNtextBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
            }
        }

        private void GroupSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            RefreshStepBar();
        }

        private bool _isRunAllRunning;

        private async void RunAllClick(object sender, RoutedEventArgs e)
        {
            await RunAllAsync();
        }

        /// <summary>
        /// 一键执行当前组的所有启用的 ProcessMeta
        /// </summary>
        public async Task RunAllAsync()
        {
            if (_luxSession != null || IsJsonVidRunning || IsCompletingFlowBatch) return;
            if (_isRunAllRunning)
            {
                log.Info("一键执行已在运行中，忽略重复调用");
                return;
            }
            if (_isFlowStartPending || flowControl.IsFlowRun || _isFlowLifecycleActive || IsResultExportPending)
            {
                log.Info("当前存在流程执行或正在处理流程结果，无法一键执行");
                return;
            }
            _isRunAllRunning = true;
            CurrentFlowResult = null!;
            ProjectARVRReuslt? lastPersistedRunAllResult = null;
            ArvrFlowBatchReceipt? resultReceipt = null;
            SocketResponse? completionResponse = null;
            try
            {
                bool usePreparedSession = _runAllSessionPrepared;
                _runAllSessionPrepared = false;
                if (!usePreparedSession)
                    InitializeTestSession(ProjectARVRProConfig.Instance.SN);

                var enabledMetas = ProcessMetas.Where(m => m.IsEnabled).ToList();
                log.Info($"一键执行开始，共 {enabledMetas.Count} 个启用的流程");
                if (enabledMetas.Count == 0)
                {
                    RecordFlowFailure("当前组没有启用的测试流程");
                    await TestCompletedAsync();
                    return;
                }

                for (int i = 0; i < enabledMetas.Count; i++)
                {
                    resultReceipt = null;
                    Stopwatch startupTiming = Stopwatch.StartNew();
                    ProcessMeta meta = enabledMetas[i];
                    _currentFlowProcess = meta.Process;
                    CurrentTestType = ProcessMetas.IndexOf(meta);

                    // Do not leave the previous iteration in CurrentFlowResult while preparing
                    // the next flow. Cancellation or template errors must never rewrite the
                    // previous, already completed ARVR result as failed/canceled.
                    CurrentFlowResult = new ProjectARVRReuslt
                    {
                        SN = ProjectARVRProConfig.Instance.SN,
                        Model = string.IsNullOrWhiteSpace(meta.FlowTemplate) ? meta.Name : meta.FlowTemplate,
                        TestType = CurrentTestType,
                    };
                    ApplyPendingSwitchTiming(CurrentFlowResult);
                    FlowName = CurrentFlowResult.Model;
                    PrepareExecutionStatus();
                    CurrentFlowResult.Code = ViewResultManager.Config.CreateFlowCode(ProjectARVRProConfig.Instance.SN, DateTime.Now);

                    log.Info($"一键执行 [{i + 1}/{enabledMetas.Count}]: {meta.Name} ({meta.FlowTemplate})");

                    TemplateModel<FlowParam> templateParam = SelectFlowTemplate(meta);
                    _currentFlowTemplateId = templateParam.Id;
                    CurrentFlowResult.Model = templateParam.Key;
                    FlowName = CurrentFlowResult.Model;
                    ResultProcessResolver.Capture(CurrentFlowResult, _currentFlowProcess);
                    CaptureRuntimeEstimate(templateParam, meta);

                    // 执行流程并等待完成
                    var tcs = new TaskCompletionSource<FlowControlData>(TaskCreationOptions.RunContinuationsAsynchronously);
                    string flowCode = CurrentFlowResult.Code;
                    void completedHandler(object? s, FlowControlData data)
                    {
                        if (!ReferenceEquals(s, flowControl) || !string.Equals(flowCode, data.SerialNumber, StringComparison.Ordinal))
                            return;
                        flowControl.FlowCompleted -= completedHandler;
                        tcs.TrySetResult(data);
                    }

                    // Reset state for this template run
                    TryCount = 0;

                    await Refresh();

                    ApplyFlowCameraParameterOverride(meta);

                    CurrentFlowResult.PictureSwitchStartedAt = DateTime.Now;
                    _currentStartupWorkMs = startupTiming.Elapsed.TotalMilliseconds;
                    Stopwatch pictureSwitchStopwatch = Stopwatch.StartNew();
                    bool pictureSwitchSucceeded = await _pictureSwitchService.ExecuteAsync(meta);
                    pictureSwitchStopwatch.Stop();
                    _currentPictureSwitchMilliseconds = Math.Max(0, pictureSwitchStopwatch.ElapsedMilliseconds);
                    if (!pictureSwitchSucceeded)
                    {
                        CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                        CurrentFlowResult.Msg = "PictureSwitchFailed";
                        await ExecuteProcessFailureAsync(meta.Process);
                        RecordFlowFailure(CurrentFlowResult.Msg);
                        SaveFlowResult(CurrentFlowResult);
                        SaveObjectiveTestResultRecord(CurrentFlowResult);
                        lastPersistedRunAllResult = CurrentFlowResult;

                        if (!ProjectARVRProConfig.Instance.AllowTestFailures)
                        {
                            log.Error($"流程 {meta.Name} 切图失败且不允许失败，终止一键执行");
                            break;
                        }

                        continue;
                    }
                    CurrentFlowResult.PictureSwitchCompletedAt = DateTime.Now;

                    Stopwatch preProcessingStopwatch = Stopwatch.StartNew();
                    bool preProcessingSucceeded = await PreProcessing(FlowName, CurrentFlowResult.SN);
                    preProcessingStopwatch.Stop();
                    _currentPreProcessingMilliseconds = Math.Max(0, preProcessingStopwatch.ElapsedMilliseconds);
                    if (!preProcessingSucceeded)
                    {
                        CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                        CurrentFlowResult.Msg = "PreProcessFailed";
                        await ExecuteProcessFailureAsync(meta.Process);
                        RecordFlowFailure(CurrentFlowResult.Msg);
                        SaveFlowResult(CurrentFlowResult);
                        SaveObjectiveTestResultRecord(CurrentFlowResult);
                        lastPersistedRunAllResult = CurrentFlowResult;

                        if (!ProjectARVRProConfig.Instance.AllowTestFailures)
                        {
                            log.Error($"流程 {meta.Name} 预处理失败且不允许失败，终止一键执行");
                            break;
                        }

                        continue;
                    }
                    CurrentFlowResult.PreProcessingCompletedAt = DateTime.Now;

                    CurrentFlowResult.FlowStatus = FlowStatus.Ready;

                    ArvrFlowBatchReceipt receipt = await CreateCurrentFlowBatchAsync();
                    resultReceipt = receipt;

                    flowControl.FlowCompleted += completedHandler;
                    stopwatch.Reset();
                    CurrentFlowResult.FlowStartedAt = DateTime.Now;
                    stopwatch.Start();
                    FlowControlData flowResult;
                    try
                    {
                        if (!await receipt.StartAsync(flowControl))
                        {
                            log.Error($"流程 {meta.Name} 启动被拒绝");
                            flowResult = new FlowControlData
                            {
                                EventName = "Failed",
                                Status = StatusTypeEnum.Failed,
                                SerialNumber = CurrentFlowResult.Code,
                                ErrorNodeName = meta.Name,
                                Message = $"{meta.Name}({meta.FlowTemplate}) {FlowStartRejectedMessage}",
                                Params = $"{meta.Name}({meta.FlowTemplate}) {FlowStartRejectedMessage}",
                            };
                        }
                        else
                        {
                            SetStepProgress(CurrentTestType);
                            timer.Change(0, ExecutionStatusRefreshIntervalMs);

                            // 等待流程完成，默认超时 10 分钟。
                            try
                            {
                                flowResult = await tcs.Task.WaitAsync(TimeSpan.FromMinutes(10));
                            }
                            catch (TimeoutException)
                            {
                                flowControl.Stop();
                                log.Error($"流程 {meta.Name} 执行超时(10min)");
                                flowResult = new FlowControlData
                                {
                                    EventName = "OverTime",
                                    Status = StatusTypeEnum.OverTime,
                                    SerialNumber = CurrentFlowResult.Code,
                                    ErrorNodeName = meta.Name,
                                    Message = $"{meta.Name}({meta.FlowTemplate}) OverTime 10min",
                                    Params = $"{meta.Name}({meta.FlowTemplate}) OverTime 10min",
                                };
                            }
                        }
                    }
                    finally
                    {
                        flowControl.FlowCompleted -= completedHandler;
                    }

                    stopwatch.Stop();
                    CurrentFlowResult.FlowCompletedAt = DateTime.Now;
                    timer.Change(Timeout.Infinite, 500);
                    log.Info($"流程 {meta.Name} 完成: {flowResult.EventName}, 耗时 {stopwatch.ElapsedMilliseconds}ms");

                    Stopwatch flowFinalizeStopwatch = Stopwatch.StartNew();
                    bool completed = flowResult.EventName == "Completed";
                    if (completed)
                        BatchFinalization.BeginCompletion();
                    await FinalizeCurrentFlowRunAsync(flowResult, receipt);
                    if (_isDisposed)
                        return;
                    flowFinalizeStopwatch.Stop();
                    _currentFlowFinalizeMilliseconds = Math.Max(0, flowFinalizeStopwatch.ElapsedMilliseconds);
                    ShowExecutionResult(flowResult.EventName, flowResult.Params);

                    if (completed)
                    {
                        _flowRuntimeEstimates.RecordCompleted(_currentRuntimeEstimateKey, stopwatch.ElapsedMilliseconds);
                        CurrentFlowResult.Msg = "Completed";
                        bool processingSucceeded = await Processing(flowResult.SerialNumber, receipt);
                        if (_isDisposed)
                            return;
                        lastPersistedRunAllResult = CurrentFlowResult;
                        if (!processingSucceeded && !ProjectARVRProConfig.Instance.AllowTestFailures)
                        {
                            log.Error($"流程 {meta.Name} 结果处理失败且不允许失败，终止一键执行");
                            break;
                        }
                        if (i + 1 < enabledMetas.Count)
                        {
                            if (BatchFinalization.CloseRequested)
                                return;
                            EndFlowBatchCompletion();
                        }
                    }
                    else
                    {
                        CurrentFlowResult.FlowStatus = flowResult.EventName == "OverTime" ? FlowStatus.OverTime : FlowStatus.Failed;
                        CurrentFlowResult.Msg = flowResult.Params;
                        await ExecuteProcessFailureAsync(meta.Process, receipt);
                        RecordFlowFailure(CurrentFlowResult.Msg, flowResult.EventName == "OverTime" ? -2 : -1);
                        TryAttachCapturedImage(CurrentFlowResult, receipt);
                        ShowExecutionResult(flowResult.EventName, CurrentFlowResult.Msg);
                        SaveFlowResult(CurrentFlowResult, receipt);
                        SaveObjectiveTestResultRecord(CurrentFlowResult);
                        lastPersistedRunAllResult = CurrentFlowResult;

                        if (!ProjectARVRProConfig.Instance.AllowTestFailures)
                        {
                            log.Error($"流程 {meta.Name} 失败且不允许失败，终止一键执行");
                            break;
                        }
                    }
                }

                log.Info($"一键执行完成, TotalResult={ObjectiveTestResult.TotalResult}");
                completionResponse = await TestCompletedAsync(deferSocketResponse: BatchFinalization.IsCompleting);
            }
            catch (Exception ex)
            {
                if (_isDisposed)
                    return;
                string message = $"一键执行异常: {ex.Message}";
                RecordFlowFailure(message);
                flowControl.Stop();
                stopwatch.Stop();
                timer.Change(Timeout.Infinite, 500);
                if (CurrentFlowResult != null)
                {
                    CurrentFlowResult.Msg = message;
                    CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                    var receipt = _currentFlowBatch;
                    if (receipt != null)
                    {
                        await FinalizeCurrentFlowRunAsync(new FlowControlData
                        {
                            EventName = "Failed",
                            Status = StatusTypeEnum.Failed,
                            SerialNumber = CurrentFlowResult.Code,
                            Message = message,
                            Params = message,
                            TotalTime = stopwatch.ElapsedMilliseconds,
                        }, receipt);
                    }
                    SaveFlowResult(CurrentFlowResult, receipt ?? resultReceipt);
                    SaveObjectiveTestResultRecord(CurrentFlowResult);
                }
                else if (lastPersistedRunAllResult != null)
                {
                    // The exception occurred between two iterations. Update only the product
                    // summary; the previous flow row has already completed successfully.
                    SaveObjectiveTestResultRecord(lastPersistedRunAllResult);
                }
                completionResponse = await TestCompletedAsync(deferSocketResponse: BatchFinalization.IsCompleting);
                log.Error("一键执行异常", ex);
            }
            finally
            {
                // Initialization or a reporting exception may have cleared the
                // current result. The recording belongs to this window regardless.
                await CompleteFlowNodeRecordingAsync();
                _runAllSessionPrepared = false;
                _isRunAllRunning = false;
                if (BatchFinalization.IsCompleting)
                    EndFlowBatchCompletion();
                if (!_isDisposed && completionResponse != null)
                    SendCompletedTestResponse(completionResponse);
            }
        }

        private static string BuildDailyCustomXlsxBaseFileName(DateTime exportTime, string? projectName)
        {
            string safeProjectName = SanitizeFileName(string.IsNullOrWhiteSpace(projectName)
                ? "ProjectARVRPro"
                : projectName.Trim());

            return $"{exportTime:yyyy-M-d}TestResults+{safeProjectName}";
        }

        private static string SanitizeFileName(string fileName)
        {
            char[] invalidChars = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(fileName.Length);

            foreach (char ch in fileName)
            {
                builder.Append(invalidChars.Contains(ch) ? '_' : ch);
            }

            return builder.Length == 0 ? "ProjectARVRPro" : builder.ToString();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            BatchFinalization.OnClosing(e);
        }

        internal void EndFlowBatchCompletion()
        {
            if (BatchFinalization.EndCompletion() && !_isDisposed)
                _ = Dispatcher.BeginInvoke(() => { if (!_isDisposed) Close(); });
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            ClearSocketSwitchSession();
            _luxLifetime.Cancel();
            _runningFlowNodes.Reset(null);
            foreach (CVCommonNode node in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
            {
                node.nodeRunEvent -= UpdateMsg;
                node.nodeEndEvent -= NodeExecutionEnded;
            }
            Interlocked.Increment(ref _resultImagePresentationVersion);
            Interlocked.Exchange(ref _resultImagePresentationCancellation, null)?.Cancel();
            _automaticImageExportResults.Clear();
            ImageView.ExternalRenderCompleted -= ImageView_ExternalRenderCompleted;
            ViewResluts.CollectionChanged -= ViewResults_CollectionChanged;
            ProjectConfig.PropertyChanged -= ProjectConfig_PropertyChanged;
            DisPlayManager.GetInstance().IDisPlayControls.CollectionChanged -= DisplayControls_CollectionChanged;
            foreach (ViewConfigBase config in ConfigHandler.GetInstance().Configs.Values.OfType<ViewConfigBase>())
                config.PropertyChanged -= ViewRefreshConfig_PropertyChanged;
            if (_activeGroupChangedHandler != null)
            {
                ProcessManager.ActiveGroupChanged -= _activeGroupChangedHandler;
                _activeGroupChangedHandler = null;
            }
            if (_activeProcessMetasChangedHandler != null)
            {
                ProcessManager.ActiveProcessMetasChanged -= _activeProcessMetasChangedHandler;
                _activeProcessMetasChangedHandler = null;
            }
            listView1.SelectionChanged -= listView1_SelectionChanged;
            listView1.ItemsSource = null;
            listView1.ContextMenu = null;
            listView1.CommandBindings.Clear();

            ImageView.Dispose();
            flowControl.PropertyChanged -= FlowControl_PropertyChanged;
            flowControl.Stop();
            stopwatch.Stop();
            if (BatchFinalization.PendingWrite is Task pendingBatchWrite)
            {
                try { pendingBatchWrite.GetAwaiter().GetResult(); }
                catch (Exception ex) { log.Warn("关闭窗口时等待流程批次回写失败。", ex); }
            }
            else if (_currentFlowBatch is { } receipt && CurrentFlowResult != null)
            {
                try
                {
                    FinalizeCurrentFlowRunAsync(new FlowControlData
                    {
                        EventName = "Canceled",
                        Status = StatusTypeEnum.Canceled,
                        SerialNumber = CurrentFlowResult.Code,
                        Message = "ARVRWindow closed",
                        Params = "ARVRWindow closed",
                        TotalTime = stopwatch.ElapsedMilliseconds,
                    }, receipt).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    log.Warn("关闭窗口时更新流程批次状态失败。", ex);
                }
            }
            _flowNodeExecutionRecorder.Dispose();
            if (!FlowDiagnosticsWriteQueue.WaitAsync(_flowDiagnosticsWrites.DrainAsync(),
                FlowDiagnosticsWriteQueue.WriteWaitTimeout).GetAwaiter().GetResult())
                log.Warn("关闭窗口时等待流程诊断写入超时。");
            _isFlowStartPending = false;
            _isFlowLifecycleActive = false;
            flowEngine.Dispose();
            STNodeEditorMain.Dispose();
            timer?.Change(Timeout.Infinite, 500); // 停止定时器
            timer?.Dispose();
            logOutput?.Dispose();
            logOutput = null;
            _pictureSwitchService.Dispose();
            DataContext = null;
            GC.SuppressFinalize(this);
        }

    }
}
