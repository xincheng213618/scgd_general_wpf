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
            ARVRWindowConfig.Instance.SetWindow(this);
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
        private MeasureBatchModel? _currentFlowBatch;
        private readonly FlowNodeExecutionRecorder _flowNodeExecutionRecorder = new FlowNodeExecutionRecorder();
        private readonly FlowRunningNodeTracker _runningFlowNodes = new();
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
        private bool _runAllSessionPrepared;
        internal bool IsResultExportPending { get; private set; }

        private bool IsTestExecutionBusy => IsSwitchRun
            || _isFlowStartPending
            || flowControl.IsFlowRun
            || _isFlowLifecycleActive
            || _isRunAllRunning
            || IsResultExportPending
            || _runAllSessionPrepared;

        public string InitTest(string? serialNumber)
        {
            if (IsResultExportPending)
            {
                log.Warn("正在保存测试结果，暂不初始化下一次测试");
                return _objectiveSessionSerialNumber;
            }
            string resolvedSerialNumber = InitializeTestSession(serialNumber);
            MarkSwitchRequested();
            return resolvedSerialNumber;
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
            _pendingSwitchRequestedAt = null;
            _pendingSwitchAcknowledgedAt = null;
            ResetCurrentPhaseDurations();
            CurrentFlowResult = null!;
            CurrentTestType = -1;
            bool isAutoGenerated = string.IsNullOrWhiteSpace(serialNumber);
            string resolvedSerialNumber = isAutoGenerated ? AutoSerialNumberGenerator.Create() : serialNumber!.Trim();
            _objectiveSessionSerialNumber = resolvedSerialNumber;
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProjectARVRProConfig.Instance.SN = resolvedSerialNumber;
                resolvedSerialNumber = ProjectARVRProConfig.Instance.SN;
            });
            if (isAutoGenerated)
            {
                log.Info($"未收到SN，已自动生成规则化SN: {resolvedSerialNumber}");
            }

            return resolvedSerialNumber;
        }

        private void MarkSwitchRequested()
        {
            _pendingSwitchRequestedAt = DateTime.Now;
            _pendingSwitchAcknowledgedAt = null;
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
            bool resultImageDimensionsFromProcessCache)
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
                BatchLookupMs = Math.Max(0, batchLookupMilliseconds),
                ProcessExecuteMs = Math.Max(0, processExecutionMilliseconds),
                ViewResultSaveMs = Math.Max(0, viewResultSaveMilliseconds),
                ObjectiveResultSaveMs = Math.Max(0, objectiveResultSaveMilliseconds),
                LinkSaveMs = Math.Max(0, linkSaveMilliseconds),
                ResultProcessingTimestampPersistMs = Math.Max(0, resultProcessingTimestampPersistMilliseconds),
                ResultProcessingTimestampPersisted = resultProcessingTimestampPersisted,
                ResultImageDimensionsFromProcessCache = resultImageDimensionsFromProcessCache,
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
        public async void SwitchPGCompleted()
        {
            _pendingSwitchAcknowledgedAt ??= DateTime.Now;
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

                // Find next enabled ProcessMeta
                int nextTestType = -1;
                for (int i = CurrentTestType + 1; i < ProcessMetas.Count; i++)
                {
                    if (ProcessMetas[i].IsEnabled)
                    {
                        nextTestType = i;
                        break;
                    }
                }

                if (nextTestType >= 0 && nextTestType < ProcessMetas.Count)
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
            var template = TemplateFlow.Params.First(a => string.Equals(a.Key, processMeta.FlowTemplate, StringComparison.OrdinalIgnoreCase));
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

            timer = new Timer(TimeRun, null, Timeout.Infinite, Timeout.Infinite);


            logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline", ProjectARVRProLogConfig.Instance);
            LogGrid.Children.Add(logOutput);


            this.Closed += (s, e) =>
            {
                this.Dispose();
            };


            ImageView.ExternalRenderCompleted += ImageView_ExternalRenderCompleted;
            listView1.ItemsSource = ViewResluts;

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
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
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }

        public void Delete()
        {
            if (listView1.SelectedIndex < 0) return;
            var item = listView1.SelectedItem as ProjectARVRReuslt;
            if (item == null) return;
            if (MessageBox.Show(Application.Current.GetActiveWindow(), LocalizedText.Format($"是否删除 {item.SN} 测试结果？"), "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                ViewResluts.Remove(item);
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

                Db.Deleteable<MeasureBatchModel>().Where(it => it.Id == item.Id).ExecuteCommand();
                log.Info($"删除测试结果 {item.SN}");
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
            contextMenu.Items.Add(new MenuItem() { Command = flowExecutionAnalysisCommand, Header = LocalizedText.Get("流程执行分析") });
            contextMenu.Items.Add(new MenuItem() { Command = viewTestResultCommand, Header = LocalizedText.Get("查看测试结果") });

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

        private void ContextMenu_FlowExecutionAnalysis()
        {
            MeasureBatchModel? batch = GetSelectedMeasureBatch();
            if (batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), LocalizedText.Get("找不到批次号，请检查流程配置"), "ColorVision");
                return;
            }

            var window = new FlowExecutionAnalysisWindow(batch)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            window.Show();
        }

        private MeasureBatchModel? GetSelectedMeasureBatch()
        {
            if (listView1.SelectedItem is not ProjectARVRReuslt item || item.BatchId <= 0)
                return null;

            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true,
            });
            return db.Queryable<MeasureBatchModel>().Where(batch => batch.Id == item.BatchId).First();
        }

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
            await RunTemplate();
        }


        ProjectARVRReuslt CurrentFlowResult { get; set; }
        int TryCount;

        public async Task RunTemplate()
        {
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

                string sn = ViewResultManager.Config.CodeUseSN ? ProjectARVRProConfig.Instance.SN + "_" : "";
                CurrentFlowResult.Code = sn + DateTime.Now.ToString(ViewResultManager.Config.CodeDateFormat);
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
                    ViewResultManager.Save(CurrentFlowResult);
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
                    ViewResultManager.Save(CurrentFlowResult);
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

                await CreateCurrentFlowBatchAsync();

                _isFlowLifecycleActive = true;
                if (!await flowControl.TryStartAsync(CurrentFlowResult.Code, cancellationToken))
                {
                    await HandleFlowStartFailureAsync(FlowStartRejectedMessage, runProcessMeta?.Process, persistResult: true);
                    return false;
                }

                flowStarted = true;
                SetStepProgress(CurrentFlowResult.TestType, completed: false);
                timer.Change(0, ExecutionStatusRefreshIntervalMs); // 启动定时器
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                if (CurrentFlowResult != null)
                {
                    if (_currentFlowBatch?.Id > 0)
                    {
                        await FinalizeCurrentFlowRunAsync(new FlowControlData
                        {
                            EventName = "Canceled",
                            Status = StatusTypeEnum.Canceled,
                            SerialNumber = CurrentFlowResult.Code,
                            Message = "测试已取消",
                            Params = "测试已取消",
                            TotalTime = stopwatch.ElapsedMilliseconds,
                        });
                    }
                    CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                    RecordFlowFailure("测试已取消");
                    ViewResultManager.Save(CurrentFlowResult);
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
                    if (_currentFlowBatch?.Id > 0)
                    {
                        await FinalizeCurrentFlowRunAsync(new FlowControlData
                        {
                            EventName = "Failed",
                            Status = StatusTypeEnum.Failed,
                            SerialNumber = CurrentFlowResult.Code,
                            Message = CurrentFlowResult.Msg,
                            Params = CurrentFlowResult.Msg,
                            TotalTime = stopwatch.ElapsedMilliseconds,
                        });
                    }
                    await ExecuteProcessFailureAsync(runProcessMeta?.Process);
                    RecordFlowFailure(CurrentFlowResult.Msg);
                    TryAttachCapturedImage(CurrentFlowResult);
                    ViewResultManager.Save(CurrentFlowResult);
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
            }
        }

        private async Task<bool> PreProcessing(string flowName, string serialNumber)
        {
            var serverNodes = new ObservableCollection<CVBaseServerNode>(STNodeEditorMain.Nodes.OfType<CVBaseServerNode>());
            return await PreProcessManager.GetInstance().ExecuteAsync(flowName, serialNumber, serverNodes);
        }

        private async Task CreateCurrentFlowBatchAsync()
        {
            if (_flowNodeExecutionRecorder.IsRecording())
            {
                log.Warn("流程已停止，收尾上一轮残留的节点诊断记录后继续执行");
                await CompleteFlowNodeRecordingAsync();
            }

            long batchStartedAt = Stopwatch.GetTimestamp();
            _currentFlowBatch = new MeasureBatchModel
            {
                TId = _currentFlowTemplateId > 0 ? _currentFlowTemplateId : null,
                Name = CurrentFlowResult.SN,
                Code = CurrentFlowResult.Code,
            };
            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true,
            });
            _currentFlowBatch.Id = DatabaseCommandTiming.Execute(db, "ARVR.CreateBatch", () => db.Insertable(_currentFlowBatch).ExecuteReturnIdentity(), CurrentFlowResult.Code);
            CurrentFlowResult.BatchId = _currentFlowBatch.Id;
            _currentBatchCreateMs = Stopwatch.GetElapsedTime(batchStartedAt).TotalMilliseconds;
            long recorderStartedAt = Stopwatch.GetTimestamp();
            _flowNodeExecutionRecorder.StartRun(_currentFlowBatch.Id, CurrentFlowResult.Code);
            _runningFlowNodes.Reset(CurrentFlowResult.Code);
            _currentNodeRecorderStartMs = Stopwatch.GetElapsedTime(recorderStartedAt).TotalMilliseconds;
        }

        private async Task FinalizeCurrentFlowRunAsync(FlowControlData flowResult)
        {
            string serialNumber = string.IsNullOrWhiteSpace(flowResult.SerialNumber)
                ? CurrentFlowResult.Code
                : flowResult.SerialNumber;
            flowResult.SerialNumber = serialNumber;

            long elapsedMilliseconds = Math.Max(0, stopwatch.ElapsedMilliseconds);
            CurrentFlowResult.RunTime = elapsedMilliseconds;
            CurrentFlowResult.FlowStatus = flowResult.FlowStatus;

            long runRecordStarted = Stopwatch.GetTimestamp();
            FlowNodeRecordDataBaseHelper.RecordFlowRun(
                _currentFlowTemplateId,
                FlowName,
                serialNumber,
                flowResult.FlowStatus,
                elapsedMilliseconds);
            _currentFlowRunRecordMs = Stopwatch.GetElapsedTime(runRecordStarted).TotalMilliseconds;

            long batchFinalizeStarted = Stopwatch.GetTimestamp();
            try
            {
                MeasureBatchModel? batch = _currentFlowBatch;
                if (batch == null && CurrentFlowResult.BatchId > 0)
                    batch = BatchResultMasterDao.Instance.GetById(CurrentFlowResult.BatchId);
                if (batch != null)
                {
                    batch.TId = _currentFlowTemplateId > 0 ? _currentFlowTemplateId : null;
                    batch.TotalTime = elapsedMilliseconds > int.MaxValue
                        ? int.MaxValue
                        : (int)elapsedMilliseconds;
                    batch.FlowStatus = flowResult.FlowStatus;
                    batch.Result = flowResult.Params ?? flowResult.Message ?? flowResult.EventName;
                    using var db = new SqlSugarClient(new ConnectionConfig
                    {
                        ConnectionString = MySqlControl.GetConnectionString(),
                        DbType = SqlSugar.DbType.MySql,
                        IsAutoCloseConnection = true,
                    });
                    DatabaseCommandTiming.Execute(db, "ARVR.FinalizeBatch", () => db.Updateable(batch).ExecuteCommand(), serialNumber);
                }
            }
            catch (Exception ex)
            {
                log.Error($"回写流程批次失败 => batchId={CurrentFlowResult.BatchId}, serialNumber={serialNumber}", ex);
            }
            finally
            {
                _currentBatchFinalizeMs = Stopwatch.GetElapsedTime(batchFinalizeStarted).TotalMilliseconds;
            }

            await CompleteFlowNodeRecordingAsync();
            _currentFlowBatch = null;
        }

        private async Task CompleteFlowNodeRecordingAsync()
        {
            if (!_flowNodeExecutionRecorder.IsRecording())
                return;

            long recorderFlushStarted = Stopwatch.GetTimestamp();
            try
            {
                _currentNodeRecorderFlushed = await _flowNodeExecutionRecorder.CompleteRunAsync(
                    flushTimeout: TimeSpan.FromSeconds(5));
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
            if (stepBar == null)
                return;

            foreach (object item in stepBar.Items)
            {
                if (item is HandyControl.Controls.StepBarItem stepBarItem)
                    stepBarItem.SetValue(HandyControl.Controls.StepBarItem.StatusProperty, StepStatus.Waiting);
            }

        }

        private void RefreshStepBar()
        {
            ProcessManager.GenStepBar(stepBar);

            foreach (object item in stepBar.Items)
            {
                if (item is HandyControl.Controls.StepBarItem stepBarItem)
                    stepBarItem.ToolTip = stepBarItem.Content;
            }
        }

        private void SetStepProgress(int stepIndex, bool completed)
        {
            if (stepBar == null || stepBar.Items.Count == 0 || stepIndex < 0)
                return;

            int normalizedStepIndex = ProcessManager.GetEnabledStepIndex(ProcessMetas, stepIndex);
            if (normalizedStepIndex < 0 || normalizedStepIndex >= stepBar.Items.Count)
                return;

            ProjectConfig.StepIndex = normalizedStepIndex;

            for (int i = 0; i < stepBar.Items.Count; i++)
            {
                if (stepBar.Items[i] is not HandyControl.Controls.StepBarItem stepBarItem)
                    continue;

                StepStatus status = i < normalizedStepIndex || completed && i == normalizedStepIndex
                    ? StepStatus.Complete
                    : i == normalizedStepIndex
                        ? StepStatus.UnderWay
                        : StepStatus.Waiting;
                stepBarItem.SetValue(HandyControl.Controls.StepBarItem.StatusProperty, status);
            }

            if (stepBar.Items[normalizedStepIndex] is HandyControl.Controls.StepBarItem currentStep)
                currentStep.BringIntoView();
        }

        private async Task HandleFlowStartFailureAsync(string message, IProcess? process, bool persistResult)
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
            });
            await ExecuteProcessFailureAsync(process ?? _currentFlowProcess);
            RecordFlowFailure(message);

            if (persistResult)
            {
                ViewResultManager.Save(CurrentFlowResult);
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

        private async Task ExecuteProcessFailureAsync(IProcess? process)
        {
            if (process == null || CurrentFlowResult == null)
                return;

            try
            {
                MeasureBatchModel? batch = null;
                if (CurrentFlowResult.BatchId > 0)
                    batch = BatchResultMasterDao.Instance.GetById(CurrentFlowResult.BatchId);

                batch ??= new MeasureBatchModel
                {
                    Id = CurrentFlowResult.BatchId,
                    Name = CurrentFlowResult.SN,
                    Code = CurrentFlowResult.Code
                };

                var ctx = new IProcessExecutionContext
                {
                    Batch = batch,
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

        private void TryAttachCapturedImage(ProjectARVRReuslt result)
        {
            if (result == null) return;

            if (string.IsNullOrWhiteSpace(result.Model))
                result.Model = FlowName;

            try
            {
                int batchId = result.BatchId;
                if (batchId <= 0) return;

                List<MeasureResultImgModel> images = MeasureImgResultDao.Instance.GetAllByBatchId(batchId);
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
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            stopwatch.Stop();
            CurrentFlowResult.FlowCompletedAt = DateTime.Now;
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            log.Debug($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
            Stopwatch flowFinalizeStopwatch = Stopwatch.StartNew();
            await FinalizeCurrentFlowRunAsync(FlowControlData);
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
                    processingSucceeded = await Processing(FlowControlData.SerialNumber);
                }
                catch (Exception ex)
                {
                    CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                    RecordFlowFailure($"结果处理异常: {ex.Message}");
                    ViewResultManager.Save(CurrentFlowResult);
                    SaveObjectiveTestResultRecord(CurrentFlowResult);
                    MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                    processingSucceeded = false;
                }

                _isFlowLifecycleActive = false;
                if (!processingSucceeded && !ProjectARVRProConfig.Instance.AllowTestFailures)
                {
                    await TestCompletedAsync();
                }
                else if (!IsTestTypeCompleted())
                {
                    SwitchPG();
                }
                else
                {
                    await TestCompletedAsync();
                }
                TryCount = 0;
            }
            else if (FlowControlData.EventName == "OverTime")
            {
                log.Warn("流程运行超时，正在重新尝试");
                CurrentFlowResult.FlowStatus = FlowStatus.OverTime;
                CurrentFlowResult.Msg = FlowControlData.Params;
                TryAttachCapturedImage(CurrentFlowResult);
                ViewResultManager.Save(CurrentFlowResult);
                SaveObjectiveTestResultRecord(CurrentFlowResult);

                flowEngine.LoadFromBase64(string.Empty);
                await Refresh();

                if (TryCount < ProjectARVRProConfig.Instance.TryCountMax)
                {
                    _isFlowLifecycleActive = false;
                    await Task.Delay(200);
                    log.Info("重新尝试运行流程");
                    _ = RunTemplate();
                    return;
                }
                else
                {
                    await ExecuteProcessFailureAsync(_currentFlowProcess);
                    RecordFlowFailure(CurrentFlowResult.Msg, -2);
                    ViewResultManager.Save(CurrentFlowResult);
                    SaveObjectiveTestResultRecord(CurrentFlowResult);
                    _isFlowLifecycleActive = false;
                    await TestCompletedAsync();
                }
                TryCount = 0;
            }
            else
            {
                log.Error("流程运行失败" + FlowControlData.EventName + FlowControlData.Params);
                CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                CurrentFlowResult.Msg = FlowControlData.Params;
                await ExecuteProcessFailureAsync(_currentFlowProcess);
                RecordFlowFailure(CurrentFlowResult.Msg, _firstFlowFailure?.Code ?? -1);
                TryAttachCapturedImage(CurrentFlowResult);

                ViewResultManager.Save(CurrentFlowResult);
                SaveObjectiveTestResultRecord(CurrentFlowResult);
                ShowExecutionResult(FlowControlData.EventName, CurrentFlowResult.Msg);

                TryCount = 0;

                if (ProjectARVRProConfig.Instance.AllowTestFailures)
                {
                    //如果允许失败，则切换PG，并且提前设置流程,执行结束时直接发送结束
                    if (!IsTestTypeCompleted())
                    {
                        _isFlowLifecycleActive = false;
                        SwitchPG();
                    }
                    else
                    {
                        _isFlowLifecycleActive = false;
                        await TestCompletedAsync();
                    }
                }
                else
                {
                    _isFlowLifecycleActive = false;
                    await TestCompletedAsync();
                }
            }
        }

        private async Task<bool> Processing(string SerialNumber)
        {
            Stopwatch batchLookupStopwatch = Stopwatch.StartNew();
            MeasureBatchModel Batch = BatchResultMasterDao.Instance.GetByCode(SerialNumber);
            batchLookupStopwatch.Stop();


            if (Batch == null)
            {
                CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                RecordFlowFailure("找不到批次号，请检查流程配置");
                ViewResultManager.Save(CurrentFlowResult);
                SaveObjectiveTestResultRecord(CurrentFlowResult);
                MessageBox.Show(Application.Current.GetActiveWindow(), LocalizedText.Get("找不到批次号，请检查流程配置"), "ColorVision");
                return false;
            }

            ProjectARVRReuslt result = CurrentFlowResult ?? new ProjectARVRReuslt();

            result.BatchId = Batch.Id;
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
                        var ctx = new IProcessExecutionContext
                        {
                            Batch = Batch,
                            Result = result,
                            ObjectiveTestResult = ObjectiveTestResult,
                            ImageView =ImageView,
                        };
                        executed = await process.Execute(ctx);
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
                    if (executed)
                    {
                        ViewResultManagerConfig exportConfig = ViewResultManager.Config;
                        if (exportConfig.IsSaveImageReuslt || exportConfig.IsSaveSourceImage)
                        {
                            _automaticImageExportResults.Add(result);
                        }

                        Stopwatch viewResultSaveStopwatch = Stopwatch.StartNew();
                        ViewResultManager.Save(result);
                        viewResultSaveStopwatch.Stop();
                        ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && result.Result;

                        Stopwatch objectiveResultSaveStopwatch = Stopwatch.StartNew();
                        SaveObjectiveTestResultRecord(result);
                        objectiveResultSaveStopwatch.Stop();

                        long linkSaveMilliseconds = 0;
                        if (ViewResultManager.Config.IsSaveLink)
                        {
                            Stopwatch linkSaveStopwatch = Stopwatch.StartNew();
                            string linkPath = ProjectImageExportService.BuildOutputDirectory(
                                exportConfig.CsvSavePath, exportConfig.SaveByDate, DateTime.Now, result.SN);
                            using var linkWrite = ResultStorageSpaceManager.Instance.BeginWrite(
                                exportConfig.CsvSavePath, exportConfig.AutoCleanupEnabled, exportConfig.MinimumFreeSpaceGB, linkPath);
                            IsResultExportPending = true;
                            try
                            {
                                await linkWrite.EnsureSpaceAsync();

                                if (!Directory.Exists(linkPath))
                                    Directory.CreateDirectory(linkPath);

                                if (!string.IsNullOrWhiteSpace(result.FileName))
                                {
                                    string shortcutName = Path.GetFileNameWithoutExtension(result.FileName) + $"_{result.Model}";
                                    ColorVision.Common.NativeMethods.ShortcutCreator.CreateShortcut(shortcutName, linkPath, result.FileName, "");
                                }
                            }
                            finally
                            {
                                IsResultExportPending = false;
                            }
                            linkSaveStopwatch.Stop();
                            linkSaveMilliseconds = Math.Max(0, linkSaveStopwatch.ElapsedMilliseconds);
                        }

                        DateTime resultProcessingCompletedAt = DateTime.Now;
                        Stopwatch timestampPersistStopwatch = Stopwatch.StartNew();
                        bool timestampPersisted = false;
                        try
                        {
                            timestampPersisted = ViewResultManager.MarkResultProcessingCompleted(result, resultProcessingCompletedAt);
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
                            resultImageDimensionsFromProcessCache);
                        return true;
                    }
                    else
                    {
                        string failureMessage = $"自定义 IProcess 执行失败: {result.Model} -> {processTypeName}";
                        log.Error($"{failureMessage}，当前结果按失败处理");
                        result.FlowStatus = FlowStatus.Failed;
                        RecordFlowFailure(failureMessage);
                        TryAttachCapturedImage(result);
                        ViewResultManager.Save(result);
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
            ViewResultManager.Save(result);
            SaveObjectiveTestResultRecord(result);
            return false;
        }

        private void SaveObjectiveTestResultRecord(ProjectARVRReuslt result)
        {
            try
            {
                ObjectiveTestResultRecordId = ViewResultManager.SaveObjectiveTestResult(ObjectiveTestResultRecordId, result, ObjectiveTestResult);
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

        private bool IsTestTypeCompleted()
        {
            // Find if there are any enabled ProcessMetas after CurrentTestType
            for (int i = CurrentTestType + 1; i < ProcessMetas.Count; i++)
            {
                if (ProcessMetas[i].IsEnabled)
                {
                    return false; // There is at least one more enabled ProcessMeta
                }
            }
            return true; // No more enabled ProcessMetas
        }


        private void SwitchPG()
        {
            if (SocketManager.GetInstance().TcpClients.Count <= 0 || SocketControl.Current.Stream == null)
            {
                log.Info("找不到连接的Socket");
                return;
            }
            log.Debug("Socket已经链接 ");

            // Find next enabled ProcessMeta index
            int nextTestType = -1;
            for (int i = CurrentTestType + 1; i < ProcessMetas.Count; i++)
            {
                if (ProcessMetas[i].IsEnabled)
                {
                    nextTestType = i;
                    break;
                }
            }

            //如果开启了UseLegacyARVROutput，则说明第一个ProcessMeta是LegacyARVROutput，不参与测试流程，所以需要+1
            if (ViewResultManager.GetInstance().Config.UseLegacyARVROutput)
            {
                log.Debug("UseLegacyARVROutput + nextTestType 1");
                nextTestType = nextTestType + 1;
            }

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
                    ARVRTestType = nextTestType
                },
            };
            _lastFlowFailureMessage = string.Empty;

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
            SocketMessageManager.GetInstance().AddMessage(sentMsg);
            MarkSwitchRequested();
            SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(respString));

        }

        private async Task TestCompletedAsync()
        {
            if (_objectiveSessionCompleted)
                return;

            _objectiveSessionCompleted = true;
            IsResultExportPending = true;
            try
            {
                await ExportCompletedTestAsync();
            }
            finally
            {
                IsResultExportPending = false;
            }
        }

        private async Task ExportCompletedTestAsync()
        {
            FinalizeObjectiveTestResultRecord();
            SetStepProgress(CurrentTestType, completed: true);

            log.Info($"ARVR测试完成,TotalResult {ObjectiveTestResult.TotalResult}");

            // Settings and the editable SN can change while background cleanup is awaited.
            var config = ViewResultManager.Config;
            var outputConfig = new
            {
                config.CsvSavePath, config.CustomXlsxSavePath, config.SaveByDate,
                config.IsSaveCsv, config.UseLegacyARVROutput, config.IsSaveCustomXlsx,
                config.AutoCleanupEnabled, config.MinimumFreeSpaceGB,
                config.CustomXlsxProjectName, config.CustomOutputProfile
            };
            string serialNumber = SNtextBox.Text;
            var responseStream = SocketControl.Current.Stream;
            DateTime exportTime = DateTime.Now;
            string timeStr = exportTime.ToString("yyyyMMdd_HHmmss");
            string csvOutputDirectory = outputConfig.CsvSavePath;
            string customXlsxOutputDirectory = string.IsNullOrWhiteSpace(outputConfig.CustomXlsxSavePath)
                ? outputConfig.CsvSavePath
                : outputConfig.CustomXlsxSavePath;
            if (outputConfig.SaveByDate)
            {
                string dateFolder = exportTime.ToString("yyyy-MM-dd");
                csvOutputDirectory = Path.Combine(csvOutputDirectory, dateFolder);
            }

            string baseFileName = $"TestResults_{serialNumber}_{timeStr}";

            if (outputConfig.IsSaveCsv)
            {
                try
                {
                    string filePath = Path.Combine(csvOutputDirectory, $"{baseFileName}_.csv");
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
                        IReadOnlyList<ProjectARVRReuslt> flowResults = ViewResultManager.GetObjectiveTestFlowResults(ObjectiveTestResultRecordId);
                        IReadOnlyList<ObjectiveTestCsvRow> rows = ProjectARVRResultCsvExporter.CollectRows(flowResults);
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

            if (outputConfig.IsSaveCustomXlsx)
            {
                try
                {
                    string customXlsxBaseFileName = BuildDailyCustomXlsxBaseFileName(exportTime, outputConfig.CustomXlsxProjectName);
                    string currentSnDirectory = ProjectImageExportService.BuildOutputDirectory(
                        outputConfig.CsvSavePath, outputConfig.SaveByDate, exportTime, serialNumber);
                    using var xlsxWrite = ResultStorageSpaceManager.Instance.BeginWrite(
                        customXlsxOutputDirectory, outputConfig.AutoCleanupEnabled, outputConfig.MinimumFreeSpaceGB,
                        currentSnDirectory, Path.Combine(customXlsxOutputDirectory, $"{customXlsxBaseFileName}.xlsx"));
                    await xlsxWrite.EnsureSpaceAsync();
                    Directory.CreateDirectory(customXlsxOutputDirectory);
                    string xlsxPath = CustomTestResultExportService.Export(
                        new ObjectiveTestResultExportContext
                        {
                            Result = ObjectiveTestResult,
                            SerialNumber = serialNumber,
                            OutputDirectory = customXlsxOutputDirectory,
                            BaseFileName = customXlsxBaseFileName,
                            ExportTime = exportTime,
                        },
                        outputConfig.CustomOutputProfile);

                    log.Info($"客制化XLSX导出完成:{xlsxPath}");
                }
                catch (Exception ex)
                {
                    log.Error("客制化XLSX导出失败", ex);
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

                var response = new SocketResponse
                {
                    Version = "1.0",
                    MsgID = string.Empty,
                    EventName = "ProjectARVRResult",
                    Code = _firstFlowFailure?.Code ?? 0,
                    SerialNumber = serialNumber,
                    Msg = _firstFlowFailure?.Message ?? (ObjectiveTestResult.TotalResult ? "ARVR Test Completed" : "ARVR Test Fail"),
                    Data = responseData
                };
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

        private void Button_Click_Clear(object sender, RoutedEventArgs e)
        {
            ViewResluts.Clear();
            ImageView.Clear();
            outputText.Document.Blocks.Clear();
            outputText.Background = Brushes.Transparent;
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

        public async Task OpenBatchResultAsync(MeasureBatchModel batch, string flowName)
        {
            ArgumentNullException.ThrowIfNull(batch);
            if (!Dispatcher.CheckAccess())
            {
                Task dispatchedTask = await Dispatcher.InvokeAsync(() => OpenBatchResultAsync(batch, flowName));
                await dispatchedTask;
                return;
            }

            ProjectARVRReuslt? existingResult = ViewResultManager.FindByBatchId(batch.Id);
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

            string serialNumber = batch.Name ?? batch.Code ?? string.Empty;
            EnsureObjectiveResultSession(serialNumber, batch.CreateDate);

            var result = new ProjectARVRReuslt
            {
                BatchId = batch.Id,
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
                    var ctx = new IProcessExecutionContext
                    {
                        Batch = batch,
                        Result = result,
                        ObjectiveTestResult = ObjectiveTestResult,
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
                        ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && result.Result;
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

            ViewResultManager.Save(result);
            SaveObjectiveTestResultRecord(result);
            SelectViewResult(result);
            _objectiveSessionCompleted = true;
        }

        private void EnsureObjectiveResultSession(string serialNumber, DateTime? sessionStartTime = null)
        {
            if (!_objectiveSessionCompleted
                && string.Equals(_objectiveSessionSerialNumber, serialNumber, StringComparison.Ordinal)
                && ObjectiveTestResult.SessionStartTime.HasValue)
                return;

            ObjectiveTestResult = new ObjectiveTestResult { SessionStartTime = sessionStartTime ?? DateTime.Now };
            ObjectiveTestResultRecordId = 0;
            _objectiveSessionCompleted = false;
            _objectiveSessionSerialNumber = serialNumber;
        }

        private void SelectViewResult(ProjectARVRReuslt result)
        {
            ProjectARVRReuslt? displayResult = ViewResluts.FirstOrDefault(item =>
                (result.Id > 0 && item.Id == result.Id)
                || (result.BatchId > 0 && item.BatchId == result.BatchId));
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
                try
                {
                    ViewResultManager.LoadViewResultJson(result);
                    if (result.FlowStatus == FlowStatus.Completed)
                    {
                        GenoutputText(result);
                    }
                    else
                    {
                        outputText.Background = Brushes.Transparent;
                        outputText.Document.Blocks.Clear(); // 清除之前的内容
                    }

                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }

                IReadOnlyList<ResultImageFileCandidate> imageCandidates = ResultImageFileCandidates.GetExisting(result);
                CancellationTokenSource requestCancellation = new();
                Interlocked.Exchange(ref _resultImagePresentationCancellation, requestCancellation)?.Cancel();
                _ = Application.Current.Dispatcher.BeginInvoke(async () =>
                {
                    bool gateEntered = false;
                    try
                    {
                        await _resultImagePresentationGate.WaitAsync(requestCancellation.Token);
                        gateEntered = true;
                        if (!IsCurrentResultImageRequest(requestVersion, result))
                            return;

                        bool hasDisplaySurface = false;
                        bool renderOverlays = true;
                        ResultImageFileCandidate? openedCandidate = await ResultImageFileCandidates.OpenFirstAsync(
                            imageCandidates,
                            async (candidate, cancellationToken) =>
                            {
                                BitmapSource? loadedSource = await OpenResultImageAsync(candidate.FilePath, cancellationToken);
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

        private async Task<BitmapSource?> OpenResultImageAsync(string filePath, CancellationToken cancellationToken)
        {
            string? activeFilePath = ImageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
            if (string.Equals(activeFilePath, filePath, StringComparison.OrdinalIgnoreCase)
                && GetLoadedImageSource() is BitmapSource currentSource)
                return currentSource;

            TaskCompletionSource<ImageViewImageSourceLoadedEventArgs> imageLoaded = new(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<ImageViewImageSourceLoadedEventArgs> imageSourceLoaded = (_, e) => imageLoaded.TrySetResult(e);
            ImageView.ImageSourceLoaded += imageSourceLoaded;
            try
            {
                ImageView.OpenImage(filePath);
                ImageViewImageSourceLoadedEventArgs loaded = await imageLoaded.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                activeFilePath = ImageView.Config.GetProperties<string>(ImageViewPropertyKeys.FilePath);
                if (!string.Equals(activeFilePath, filePath, StringComparison.OrdinalIgnoreCase)
                    || !ImageView.IsCurrentImageRevision(loaded.ImageRevision))
                {
                    return null;
                }

                return loaded.Source as BitmapSource;
            }
            finally
            {
                ImageView.ImageSourceLoaded -= imageSourceLoaded;
            }
        }

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

        private BitmapSource? GetLoadedImageSource()
        {
            return ImageView.ViewBitmapSource as BitmapSource
                ?? ImageView.ImageShow.Source as BitmapSource;
        }

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
            SourceImageFormat sourceFormat = config.SourceExportFormat;
            SourceTiffCompression sourceTiffCompression = config.SourceTiffCompressionMode;
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
                        SaveRendered = saveResultImage, SaveSource = saveSourceImage, IncludeOverlays = includeOverlays,
                        ResultFormat = resultFormat.ToString(), ResultSize = resultSize.ToString(),
                        SourceFormat = sourceFormat.ToString(), SourceTiffCompression = sourceTiffCompression.ToString(),
                        CaptureMs = snapshotStopwatch.ElapsedMilliseconds,
                    }));

                if (_isDisposed)
                    return;

                _ = ExportImagesAsync(
                    snapshot,
                    saveResultImage,
                    saveSourceImage,
                    resultFormat,
                    resultSize,
                    includeOverlays,
                    sourceFormat,
                    sourceTiffCompression,
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

        private void ReleaseSnapshotBuffer_Click(object sender, RoutedEventArgs e)
        {
            ImageView.ReleaseSnapshotBuffer();
            log.Info("结果截图缓存已释放；若正在后台使用，将在归还时释放。");
        }

        private void OpenLocalCacheManager_Click(object sender, RoutedEventArgs e)
        {
            ColorVision.Engine.Services.Devices.Camera.Local.LocalCalibrationCacheManagerWindow.OpenWindow(ImageView.SnapshotCacheId);
        }

        private void OpenCacheActions_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu is ContextMenu menu)
            {
                menu.PlacementTarget = button;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private int _outstandingImageExports;

        private async Task ExportImagesAsync(
            ImageViewSnapshot? snapshot,
            bool saveResultImage,
            bool saveSourceImage,
            ResultImageFormat resultFormat,
            ImageExportSize resultSize,
            bool includeOverlays,
            SourceImageFormat sourceFormat,
            SourceTiffCompression sourceTiffCompression,
            ProjectARVRReuslt result,
            string outputRoot,
            bool saveByDate,
            DateTime requestedAt,
            bool autoCleanupEnabled,
            int minimumFreeSpaceGB)
        {
            string? renderedFilePath = null;
            string? sourceFilePath = null;
            Stopwatch? exportStopwatch = null;
            bool exportCompleted = false;
            ProjectImageExportAttempt? exportAttempt = null;
            ResultStorageSpaceManager.WriteLease? storageWrite = null;
            ProjectImageExportAttemptResult exportResult = new();
            Interlocked.Increment(ref _outstandingImageExports);
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
                if (saveResultImage)
                {
                    string fileStem = ProjectImageExportService.BuildResultFileStem(sourceName, result.Model);
                    renderedFilePath = ProjectImageExportService.BuildFilePath(
                        outputDirectory,
                        fileStem,
                        ProjectImageExportService.GetResultExtension(resultFormat));
                    string overlayDescription = includeOverlays ? "混合标记" : "仅底图";
                    log.Debug(
                        $"后台导出8位标记图：{resultFormat}，{DescribeImageSize(resultSize)}，{overlayDescription}，"
                        + (resultFormat == ResultImageFormat.JPEG ? "JPEG质量100" : "PNG自动压缩"));
                }
                if (saveSourceImage)
                {
                    string fileStem = ProjectImageExportService.BuildSourceFileStem(sourceName, result.Model);
                    sourceFilePath = ProjectImageExportService.BuildFilePath(
                        outputDirectory,
                        fileStem,
                        ProjectImageExportService.GetSourceExtension(sourceFormat));
                    string sourceDescription = sourceFormat switch
                    {
                        SourceImageFormat.TIFF => $"TIFF {sourceTiffCompression}无损压缩",
                        SourceImageFormat.PNG => "PNG自动无损压缩",
                        _ => "BMP（仅8位源图）",
                    };
                    log.Debug($"后台导出原尺寸、原位深、无标记原图：{sourceDescription}");
                }

                storageWrite = ResultStorageSpaceManager.Instance.BeginWrite(
                    outputRoot, autoCleanupEnabled, minimumFreeSpaceGB, outputDirectory);
                await storageWrite.EnsureSpaceAsync().ConfigureAwait(false);
                exportAttempt = new ProjectImageExportAttempt(renderedFilePath, sourceFilePath);
                ImageViewSnapshotExportOptions exportOptions = exportAttempt.CreateOptions(
                    ProjectImageExportService.CreateRenderedOptions(resultFormat, resultSize),
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
                        exportResult = exportAttempt.CommitSuccessfulChannels((channel, fileName, ex) =>
                            log.Error($"{channel}已编码，但替换正式导出文件失败：{fileName}", ex));
                        exportAttempt.Dispose();
                    }
                    ResultImageExportPathUpdate pathUpdate = ResultImageExportPathUpdate.From(
                        exportResult,
                        includeOverlays,
                        result.SavedResultImageFileName);
                    if (pathUpdate.UpdateSavedResultImageFileName || pathUpdate.UpdateSavedSourceImageFileName)
                    {
                        try
                        {
                            ViewResultManager.UpdateSavedImagePaths(result, pathUpdate);
                        }
                        catch (Exception ex)
                        {
                            log.Error("图像已写盘，但保存本次成功导出路径到结果数据库失败；内存结果未更新。", ex);
                        }
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
                finally { Interlocked.Decrement(ref _outstandingImageExports); }
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
            SNtextBox.Focus();
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
                    string sn = ViewResultManager.Config.CodeUseSN ? ProjectARVRProConfig.Instance.SN + "_" : "";
                    CurrentFlowResult.Code = sn + DateTime.Now.ToString(ViewResultManager.Config.CodeDateFormat);

                    log.Info($"一键执行 [{i + 1}/{enabledMetas.Count}]: {meta.Name} ({meta.FlowTemplate})");

                    TemplateModel<FlowParam> templateParam = SelectFlowTemplate(meta);
                    _currentFlowTemplateId = templateParam.Id;
                    CurrentFlowResult.Model = templateParam.Key;
                    FlowName = CurrentFlowResult.Model;
                    ResultProcessResolver.Capture(CurrentFlowResult, _currentFlowProcess);
                    CaptureRuntimeEstimate(templateParam, meta);

                    // 执行流程并等待完成
                    var tcs = new TaskCompletionSource<FlowControlData>(TaskCreationOptions.RunContinuationsAsynchronously);
                    void completedHandler(object? s, FlowControlData data)
                    {
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
                        ViewResultManager.Save(CurrentFlowResult);
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
                        ViewResultManager.Save(CurrentFlowResult);
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

                    await CreateCurrentFlowBatchAsync();

                    flowControl.FlowCompleted += completedHandler;
                    stopwatch.Reset();
                    CurrentFlowResult.FlowStartedAt = DateTime.Now;
                    stopwatch.Start();
                    FlowControlData flowResult;
                    try
                    {
                        if (!await flowControl.TryStartAsync(CurrentFlowResult.Code))
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
                            SetStepProgress(CurrentTestType, completed: false);
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
                    await FinalizeCurrentFlowRunAsync(flowResult);
                    flowFinalizeStopwatch.Stop();
                    _currentFlowFinalizeMilliseconds = Math.Max(0, flowFinalizeStopwatch.ElapsedMilliseconds);
                    ShowExecutionResult(flowResult.EventName, flowResult.Params);

                    if (flowResult.EventName == "Completed")
                    {
                        _flowRuntimeEstimates.RecordCompleted(_currentRuntimeEstimateKey, stopwatch.ElapsedMilliseconds);
                        CurrentFlowResult.Msg = "Completed";
                        bool processingSucceeded = await Processing(flowResult.SerialNumber);
                        lastPersistedRunAllResult = CurrentFlowResult;
                        if (!processingSucceeded && !ProjectARVRProConfig.Instance.AllowTestFailures)
                        {
                            log.Error($"流程 {meta.Name} 结果处理失败且不允许失败，终止一键执行");
                            break;
                        }
                    }
                    else
                    {
                        CurrentFlowResult.FlowStatus = flowResult.EventName == "OverTime" ? FlowStatus.OverTime : FlowStatus.Failed;
                        CurrentFlowResult.Msg = flowResult.Params;
                        await ExecuteProcessFailureAsync(meta.Process);
                        RecordFlowFailure(CurrentFlowResult.Msg, flowResult.EventName == "OverTime" ? -2 : -1);
                        TryAttachCapturedImage(CurrentFlowResult);
                        ShowExecutionResult(flowResult.EventName, CurrentFlowResult.Msg);
                        ViewResultManager.Save(CurrentFlowResult);
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
                await TestCompletedAsync();
            }
            catch (Exception ex)
            {
                string message = $"一键执行异常: {ex.Message}";
                RecordFlowFailure(message);
                flowControl.Stop();
                stopwatch.Stop();
                timer.Change(Timeout.Infinite, 500);
                if (CurrentFlowResult != null)
                {
                    CurrentFlowResult.Msg = message;
                    CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                    if (_currentFlowBatch?.Id > 0)
                    {
                        await FinalizeCurrentFlowRunAsync(new FlowControlData
                        {
                            EventName = "Failed",
                            Status = StatusTypeEnum.Failed,
                            SerialNumber = CurrentFlowResult.Code,
                            Message = message,
                            Params = message,
                            TotalTime = stopwatch.ElapsedMilliseconds,
                        });
                    }
                    ViewResultManager.Save(CurrentFlowResult);
                    SaveObjectiveTestResultRecord(CurrentFlowResult);
                }
                else if (lastPersistedRunAllResult != null)
                {
                    // The exception occurred between two iterations. Update only the product
                    // summary; the previous flow row has already completed successfully.
                    SaveObjectiveTestResultRecord(lastPersistedRunAllResult);
                }
                await TestCompletedAsync();
                log.Error("一键执行异常", ex);
            }
            finally
            {
                // Initialization or a reporting exception may have cleared the
                // current result. The recording belongs to this window regardless.
                await CompleteFlowNodeRecordingAsync();
                _runAllSessionPrepared = false;
                _isRunAllRunning = false;
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

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
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
            flowControl.Stop();
            stopwatch.Stop();
            if (_currentFlowBatch?.Id > 0 && CurrentFlowResult != null)
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
                    }).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    log.Warn("关闭窗口时更新流程批次状态失败。", ex);
                }
            }
            _flowNodeExecutionRecorder.Dispose();
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
