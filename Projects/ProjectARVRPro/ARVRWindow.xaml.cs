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
using ProjectARVRPro.SocketRelay;
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
        private bool _isFlowStartPending;
        private bool _isFlowLifecycleActive;
        private bool _runAllSessionPrepared;

        private bool IsTestExecutionBusy => IsSwitchRun
            || _isFlowStartPending
            || flowControl.IsFlowRun
            || _isFlowLifecycleActive
            || _isRunAllRunning
            || _runAllSessionPrepared;

        public string InitTest(string? serialNumber)
        {
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

        bool IsSwitchRun;
        public async void SwitchPGCompleted()
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
            if (IsSwitchRun)
            {
                log.Info("重复触发PG");
                return false;
            }
            IsSwitchRun = true;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_isFlowStartPending || flowControl.IsFlowRun || _isFlowLifecycleActive || _isRunAllRunning)
                {
                    log.Info("PG切换错误，正在执行流程或处理流程结果");
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
                    return await TryRunTemplate(template, processMeta, cancellationToken);
                }

                log.Info("没有可执行的 ARVR 流程");
                AbortCurrentTestSession("没有可执行的 ARVR 流程");
                return false;
            }
            catch (OperationCanceledException)
            {
                AbortCurrentTestSession("测试已取消");
                throw;
            }
            catch (Exception ex)
            {
                AbortCurrentTestSession($"启动下一项 ARVR 流程失败: {ex.Message}");
                throw;
            }
            finally
            {
                IsSwitchRun = false;
            }
        }

        private void AbortCurrentTestSession(string message)
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
            TestCompleted();
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

            timer = new Timer(TimeRun, null, 0, 100);
            timer.Change(Timeout.Infinite, 100); // 停止定时器


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
            if (MessageBox.Show(Application.Current.GetActiveWindow(), $"是否删除 {item.SN} 测试结果？", "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
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
            contextMenu.Items.Add(new MenuItem() { Command = ApplicationCommands.Copy, Header = "复制" });
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem() { Command = openFolderCommand, Header = "OpenFolderAndSelectFile" });
            contextMenu.Items.Add(new MenuItem() { Command = batchHistoryCommand, Header = "流程结果查询" });
            contextMenu.Items.Add(new MenuItem() { Command = flowExecutionAnalysisCommand, Header = "流程执行分析" });
            contextMenu.Items.Add(new MenuItem() { Command = viewTestResultCommand, Header = "查看测试结果" });

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
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }
            var frame = new Frame();
            var batchDataHistory = new MeasureBatchPage(frame, batch);
            var window = new Window() { Owner = Application.Current.GetActiveWindow() };
            window.Content = batchDataHistory;
            window.Show();
        }

        private void ContextMenu_FlowExecutionAnalysis()
        {
            MeasureBatchModel? batch = GetSelectedMeasureBatch();
            if (batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
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
                MessageBox.Show(Application.Current.GetActiveWindow(), "ViewResultJson为空", "ColorVision");
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
            if (FlowTemplate.SelectedIndex < 0) return Task.CompletedTask;

            MqttRCService.GetInstance().QueryServices();
            foreach (CVCommonNode node in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
                node.nodeRunEvent -= UpdateMsg;
            _flowNodeExecutionRecorder.DetachNodes();

            string Refreshdata = TemplateFlow.Params[FlowTemplate.SelectedIndex].Value.DataBase64;
            flowEngine.LoadFromBase64(Refreshdata, MqttRCService.GetInstance().ServiceTokens);

            CVCommonNode[] flowNodes = STNodeEditorMain.Nodes.OfType<CVCommonNode>().ToArray();
            foreach (CVCommonNode item in flowNodes)
            {
                item.nodeRunEvent -= UpdateMsg;
                item.nodeRunEvent += UpdateMsg;
            }
            _flowNodeExecutionRecorder.AttachNodes(flowNodes);
            return Task.CompletedTask;
        }


        private void TimeRun(object? state)
        {
            UpdateMsg(state);
        }

        string Msg1;
        private long LastFlowTime;
        string FlowName;
        private void UpdateMsg(object? sender)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                    TimeSpan elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);
                    string elapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}:{elapsed.Milliseconds:D4}";
                    string msg;
                    if (LastFlowTime == 0 || LastFlowTime - elapsedMilliseconds < 0)
                    {
                        msg = $"正在执行节点:{Msg1}{Environment.NewLine}已经执行：{elapsedTime} {Environment.NewLine}";
                    }
                    else
                    {
                        long remainingMilliseconds = LastFlowTime - elapsedMilliseconds;
                        TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                        string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";
                        msg = $"上次执行：{LastFlowTime} ms{Environment.NewLine}正在执行节点:{Msg1}{Environment.NewLine}已经执行：{elapsedTime} {Environment.NewLine}预计还需要：{remainingTime}";
                    }
                    logTextBox.Text = msg;
                }
                catch
                {

                }
            });
        }

        private void UpdateMsg(object sender, FlowEngineNodeRunEventArgs e)
        {
            if (sender is CVCommonNode algorithmNode)
            {
                if (e != null)
                {
                    Msg1 = algorithmNode.Title;
                    UpdateMsg(sender);
                }
            }
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
            await TryRunTemplate(template, processMeta);
        }

        private async Task<bool> TryRunTemplate(
            TemplateModel<FlowParam> flowTemplate,
            ProcessMeta? runProcessMeta,
            CancellationToken cancellationToken = default)
        {
            if (_isFlowStartPending || flowControl.IsFlowRun || _isFlowLifecycleActive || _isRunAllRunning)
            {
                log.Info("当前flowControl存在流程执行或正在处理流程结果");
                return false;
            }

            _isFlowStartPending = true;
            CurrentFlowResult = null!;
            bool flowStarted = false;
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

                string sn = ViewResultManager.Config.CodeUseSN ? ProjectARVRProConfig.Instance.SN + "_" : "";
                CurrentFlowResult.Code = sn + DateTime.Now.ToString(ViewResultManager.Config.CodeDateFormat);
                _currentFlowProcess = runProcessMeta?.Process ?? ProcessManager.CreateBlankProcess();
                ResultProcessResolver.Capture(CurrentFlowResult, _currentFlowProcess);

                LastFlowTime = await Task.Run(
                    () => FlowNodeRecordDataBaseHelper.GetLastCompletedFlowElapsed(
                        new FlowIdentity(
                            flowTemplate.Id,
                            flowTemplate.Key,
                            flowTemplate.Key)),
                    cancellationToken);

                await Refresh();
                cancellationToken.ThrowIfCancellationRequested();

                if (!await _pictureSwitchService.ExecuteAsync(runProcessMeta))
                {
                    CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                    CurrentFlowResult.Msg = "PictureSwitchFailed";
                    await ExecuteProcessFailureAsync(runProcessMeta?.Process);
                    RecordFlowFailure(CurrentFlowResult.Msg);
                    ViewResultManager.Save(CurrentFlowResult);
                    SaveObjectiveTestResultRecord(CurrentFlowResult);
                    logTextBox.Text = FlowName + Environment.NewLine + "切图失败";
                    TestCompleted();
                    TryCount = 0;
                    return false;
                }
                cancellationToken.ThrowIfCancellationRequested();

                if (!await PreProcessing(FlowName, CurrentFlowResult.SN))
                {
                    CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                    CurrentFlowResult.Msg = "PreProcessFailed";
                    await ExecuteProcessFailureAsync(runProcessMeta?.Process);
                    RecordFlowFailure(CurrentFlowResult.Msg);
                    ViewResultManager.Save(CurrentFlowResult);
                    SaveObjectiveTestResultRecord(CurrentFlowResult);
                    logTextBox.Text = FlowName + Environment.NewLine + "预处理失败";
                    TestCompleted();
                    TryCount = 0;
                    return false;
                }
                cancellationToken.ThrowIfCancellationRequested();

                CurrentFlowResult.FlowStatus = FlowStatus.Ready;

                flowControl.FlowCompleted -= FlowControl_FlowCompleted;
                flowControl.FlowCompleted += FlowControl_FlowCompleted;
                stopwatch.Reset();
                stopwatch.Start();

                CreateCurrentFlowBatch();

                _isFlowLifecycleActive = true;
                if (!await flowControl.TryStartAsync(CurrentFlowResult.Code, cancellationToken))
                {
                    await HandleFlowStartFailureAsync(FlowStartRejectedMessage, runProcessMeta?.Process, persistResult: true);
                    return false;
                }

                flowStarted = true;
                SetStepProgress(CurrentFlowResult.TestType, completed: false);
                timer.Change(0, 500); // 启动定时器
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
                    TestCompleted();
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
                    TestCompleted();
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
                _isFlowStartPending = false;
                if (!flowStarted && !flowControl.IsFlowRun)
                    _isFlowLifecycleActive = false;
            }
        }

        private async Task<bool> PreProcessing(string flowName, string serialNumber)
        {
            var serverNodes = new ObservableCollection<CVBaseServerNode>(STNodeEditorMain.Nodes.OfType<CVBaseServerNode>());
            return await PreProcessManager.GetInstance().ExecuteAsync(flowName, serialNumber, serverNodes);
        }

        private void CreateCurrentFlowBatch()
        {
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
            _currentFlowBatch.Id = db.Insertable(_currentFlowBatch).ExecuteReturnIdentity();
            CurrentFlowResult.BatchId = _currentFlowBatch.Id;
            _flowNodeExecutionRecorder.StartRun(_currentFlowBatch.Id, CurrentFlowResult.Code);
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

            FlowNodeRecordDataBaseHelper.RecordFlowRun(
                _currentFlowTemplateId,
                FlowName,
                serialNumber,
                flowResult.FlowStatus,
                elapsedMilliseconds);

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
                    db.Updateable(batch).ExecuteCommand();
                }
            }
            catch (Exception ex)
            {
                log.Error($"回写流程批次失败 => batchId={CurrentFlowResult.BatchId}, serialNumber={serialNumber}", ex);
            }

            try
            {
                await _flowNodeExecutionRecorder.CompleteRunAsync(
                    serialNumber,
                    flushTimeout: TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                log.Error($"结束流程节点统计失败 => batchId={CurrentFlowResult.BatchId}, serialNumber={serialNumber}", ex);
            }
            finally
            {
                _currentFlowBatch = null;
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

            logTextBox.Text = FlowName + Environment.NewLine + message;
            log.ErrorFormat("流程启动失败 => flow={0}, code={1}, reason={2}", FlowName, CurrentFlowResult.Code, message);
            if (persistResult)
            {
                TestCompleted();
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

                var image = MeasureImgResultDao.Instance.GetAllByBatchId(batchId)
                    .Where(x => !string.IsNullOrWhiteSpace(x.FileUrl))
                    .OrderBy(x => x.ZIndex ?? int.MaxValue)
                    .ThenBy(x => x.Id)
                    .FirstOrDefault(x => File.Exists(x.FileUrl));

                if (!string.IsNullOrWhiteSpace(image?.FileUrl))
                    result.FileName = image.FileUrl;
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
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
            await FinalizeCurrentFlowRunAsync(FlowControlData);
            logTextBox.Text = FlowName + Environment.NewLine + FlowControlData.EventName;

            if (FlowControlData.EventName == "Completed")
            {
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
                    TestCompleted();
                }
                else if (!IsTestTypeCompleted())
                {
                    SwitchPG();
                }
                else
                {
                    TestCompleted();
                }
                TryCount = 0;
            }
            else if (FlowControlData.EventName == "OverTime")
            {
                log.Info("流程运行超时，正在重新尝试");
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
                    TestCompleted();
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
                logTextBox.Text = FlowName + Environment.NewLine + FlowControlData.EventName + Environment.NewLine + CurrentFlowResult.Msg;

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
                        TestCompleted();
                    }
                }
                else
                {
                    _isFlowLifecycleActive = false;
                    TestCompleted();
                }
            }
        }

        private async Task<bool> Processing(string SerialNumber)
        {
            MeasureBatchModel Batch = BatchResultMasterDao.Instance.GetByCode(SerialNumber);


            if (Batch == null)
            {
                CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                RecordFlowFailure("找不到批次号，请检查流程配置");
                ViewResultManager.Save(CurrentFlowResult);
                SaveObjectiveTestResultRecord(CurrentFlowResult);
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return false;
            }

            ProjectARVRReuslt result = CurrentFlowResult ?? new ProjectARVRReuslt();

            result.BatchId = Batch.Id;
            result.FlowStatus = FlowStatus.Completed;
            result.CreateTime = DateTime.Now;
            result.Result = true;

            try
            {
                log.Info($"{result.Model}");

                IProcess? process = _currentFlowProcess ?? ResultProcessResolver.Resolve(result, ProcessManager.Processes, ProcessManager.GetResultProcessMappings());
                if (process != null)
                {
                    if (string.IsNullOrWhiteSpace(result.ProcessTypeFullName))
                        ResultProcessResolver.Capture(result, process);

                    string processTypeName = process.GetType().Name;
                    log.Info($"使用本次流程解析器 {processTypeName} 处理 {result.Model}");

                    bool executed = false;
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
                    }
                    catch (Exception ex)
                    {
                        log.Error("自定义 IProcess 执行异常", ex);
                    }
                    if (executed)
                    {
                        ViewResultManagerConfig exportConfig = ViewResultManager.Config;
                        if (exportConfig.IsSaveImageReuslt || exportConfig.IsSaveSourceImage)
                        {
                            _automaticImageExportResults.Add(result);
                        }
                        ViewResultManager.Save(result);
                        ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && result.Result;
                        SaveObjectiveTestResultRecord(result);

                        if (ViewResultManager.Config.IsSaveLink)
                        {
                            string linkPath = ViewResultManager.Config.CsvSavePath;
                            string sn = result.SN;

                            if (ViewResultManager.Config.SaveByDate)
                            {
                                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                                linkPath = Path.Combine(linkPath, dateFolder);
                            }

                            // 处理 SN 不为空的情况
                            if (!string.IsNullOrWhiteSpace(sn))
                            {
                                // 移除 SN 中的非法文件名字符
                                foreach (char c in Path.GetInvalidFileNameChars())
                                {
                                    sn = sn.Replace(c.ToString(), "");
                                }

                                // 再次检查移除特殊字符后是否为空，如果不为空则组合路径
                                if (!string.IsNullOrWhiteSpace(sn))
                                {
                                    linkPath = Path.Combine(linkPath, sn);
                                }
                            }
                            // 如果 sn 原本为空或清理后为空，linkPath 保持为 ViewResultManager.Config.CsvSavePath

                            // 注意：原始代码中是 if (Directory.Exists) Create... 
                            // 这里修正为如果目录不存在(!Exists)则创建，确保路径有效
                            if (!Directory.Exists(linkPath))
                                Directory.CreateDirectory(linkPath);

                            if (!string.IsNullOrWhiteSpace(result.FileName))
                            {
                                string shortcutName = Path.GetFileNameWithoutExtension(result.FileName) + $"_{result.Model}";
                                string shortcutPath = linkPath;
                                ColorVision.Common.NativeMethods.ShortcutCreator.CreateShortcut(shortcutName, shortcutPath, result.FileName, "");
                            }
                        }
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
                log.Info($"保存 ObjectiveTestResult 记录：{ObjectiveTestResultRecordId}");
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
            log.Info("Socket已经链接 ");

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
                log.Info("UseLegacyARVROutput + nextTestType 1");
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
            SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(respString));

        }

        private void TestCompleted()
        {
            if (_objectiveSessionCompleted)
                return;

            _objectiveSessionCompleted = true;
            FinalizeObjectiveTestResultRecord();
            SetStepProgress(CurrentTestType, completed: true);

            log.Info($"ARVR测试完成,TotalResult {ObjectiveTestResult.TotalResult}");

            var outputConfig = ViewResultManager.Config;
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

            string baseFileName = $"TestResults_{SNtextBox.Text}_{timeStr}";

            if (outputConfig.IsSaveCsv)
            {
                try
                {
                    Directory.CreateDirectory(csvOutputDirectory);
                    string filePath = Path.Combine(csvOutputDirectory, $"{baseFileName}_.csv");

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
                    Directory.CreateDirectory(customXlsxOutputDirectory);
                    string customXlsxBaseFileName = BuildDailyCustomXlsxBaseFileName(exportTime, outputConfig.CustomXlsxProjectName);
                    string xlsxPath = CustomTestResultExportService.Export(
                        new ObjectiveTestResultExportContext
                        {
                            Result = ObjectiveTestResult,
                            SerialNumber = SNtextBox.Text,
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
                    SerialNumber = SNtextBox.Text,
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

                if (SocketManager.GetInstance().TcpClients.Count <= 0 || SocketControl.Current.Stream == null)
                {
                    log.Info("找不到连接的Socket");
                    return;
                }
                SocketMessageManager.GetInstance().AddMessage(sentMsg);
                SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(respString));
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
            ViewResultManager.Config.SourceImageSupportsBmp = CanCurrentSourceExportBmp();
            new PropertyEditorWindow(ViewResultManager.Config)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            }.ShowDialog();
            ConfigService.Instance.SaveConfigs();
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
                MessageBox.Show(this, "当前测试尚未结束，请在测试完成后再打开历史批次。", "ColorVision");
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
                ViewResluts.Insert(0, result);
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

                IProcess? process = ResultProcessResolver.Resolve(result, ProcessManager.Processes, ProcessManager.GetResultProcessMappings());
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
                ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.Cols, width, nameof(ARVRWindow), "历史结果坐标空间宽度");
                ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.Rows, height, nameof(ARVRWindow), "历史结果坐标空间高度");
                ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.ImageWidth, width, nameof(ARVRWindow), "历史结果图像像素宽度");
                ImageView.Config.SetImageMetadata(ImageViewPropertyKeys.ImageHeight, height, nameof(ARVRWindow), "历史结果图像像素高度");
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

            log.Info("ImageEditor图像加载及外部点位渲染已完成，开始捕获本次结果快照。");
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
            DateTime requestedAt = result.CreateTime == default ? DateTime.Now : result.CreateTime;

            ImageViewSnapshot? snapshot = null;
            try
            {
                if (_isDisposed)
                    return;
                if (!saveResultImage && !saveSourceImage)
                    return;
                ImageView.Dispatcher.VerifyAccess();

                log.Info($"准备图像导出：8位标记图={saveResultImage}，保留位深原图={saveSourceImage}");

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
                log.Info(
                    $"ImageEditor像素与场景快照准备完成，源格式 {loadedSource.Format}，"
                    + $"耗时 {snapshotStopwatch.ElapsedMilliseconds}ms。");

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
                    requestedAt);
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
            DateTime requestedAt)
        {
            string? renderedFilePath = null;
            string? sourceFilePath = null;
            Stopwatch? exportStopwatch = null;
            bool exportCompleted = false;
            ProjectImageExportAttempt? exportAttempt = null;
            ProjectImageExportAttemptResult exportResult = new();
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
                    log.Info(
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
                    log.Info($"后台导出原尺寸、原位深、无标记原图：{sourceDescription}");
                }

                exportAttempt = new ProjectImageExportAttempt(renderedFilePath, sourceFilePath);
                ImageViewSnapshotExportOptions exportOptions = exportAttempt.CreateOptions(
                    ProjectImageExportService.CreateRenderedOptions(resultFormat, resultSize),
                    ProjectImageExportService.CreateSourceOptions(sourceFormat, sourceTiffCompression));

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
                snapshot?.Dispose();
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
            if (_isFlowStartPending || flowControl.IsFlowRun || _isFlowLifecycleActive)
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
                    TestCompleted();
                    return;
                }

                for (int i = 0; i < enabledMetas.Count; i++)
                {
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
                    FlowName = CurrentFlowResult.Model;
                    string sn = ViewResultManager.Config.CodeUseSN ? ProjectARVRProConfig.Instance.SN + "_" : "";
                    CurrentFlowResult.Code = sn + DateTime.Now.ToString(ViewResultManager.Config.CodeDateFormat);

                    log.Info($"一键执行 [{i + 1}/{enabledMetas.Count}]: {meta.Name} ({meta.FlowTemplate})");

                    TemplateModel<FlowParam> templateParam = SelectFlowTemplate(meta);
                    _currentFlowTemplateId = templateParam.Id;
                    CurrentFlowResult.Model = templateParam.Key;
                    FlowName = CurrentFlowResult.Model;
                    ResultProcessResolver.Capture(CurrentFlowResult, _currentFlowProcess);

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

                    if (!await _pictureSwitchService.ExecuteAsync(meta))
                    {
                        CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                        CurrentFlowResult.Msg = "PictureSwitchFailed";
                        await ExecuteProcessFailureAsync(meta.Process);
                        RecordFlowFailure(CurrentFlowResult.Msg);
                        logTextBox.Text = FlowName + Environment.NewLine + "切图失败";
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

                    if (!await PreProcessing(FlowName, CurrentFlowResult.SN))
                    {
                        CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                        CurrentFlowResult.Msg = "PreProcessFailed";
                        await ExecuteProcessFailureAsync(meta.Process);
                        RecordFlowFailure(CurrentFlowResult.Msg);
                        logTextBox.Text = FlowName + Environment.NewLine + "预处理失败";
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

                    CurrentFlowResult.FlowStatus = FlowStatus.Ready;

                    LastFlowTime = await Task.Run(
                        () => FlowNodeRecordDataBaseHelper.GetLastCompletedFlowElapsed(
                            new FlowIdentity(
                                templateParam.Id,
                                templateParam.Key,
                                FlowName)));

                    CreateCurrentFlowBatch();

                    flowControl.FlowCompleted += completedHandler;
                    stopwatch.Reset();
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
                            timer.Change(0, 500);

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
                    timer.Change(Timeout.Infinite, 500);
                    log.Info($"流程 {meta.Name} 完成: {flowResult.EventName}, 耗时 {stopwatch.ElapsedMilliseconds}ms");

                    await FinalizeCurrentFlowRunAsync(flowResult);
                    logTextBox.Text = FlowName + Environment.NewLine + flowResult.EventName;

                    if (flowResult.EventName == "Completed")
                    {
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
                        logTextBox.Text = FlowName + Environment.NewLine + flowResult.EventName + Environment.NewLine + CurrentFlowResult.Msg;
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
                TestCompleted();
            }
            catch (Exception ex)
            {
                string message = $"一键执行异常: {ex.Message}";
                RecordFlowFailure(message);
                if (CurrentFlowResult != null)
                {
                    flowControl.Stop();
                    stopwatch.Stop();
                    timer.Change(Timeout.Infinite, 500);
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
                TestCompleted();
                log.Error("一键执行异常", ex);
            }
            finally
            {
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
            Interlocked.Increment(ref _resultImagePresentationVersion);
            Interlocked.Exchange(ref _resultImagePresentationCancellation, null)?.Cancel();
            _automaticImageExportResults.Clear();
            ImageView.ExternalRenderCompleted -= ImageView_ExternalRenderCompleted;
            ViewResluts.CollectionChanged -= ViewResults_CollectionChanged;
            ProjectConfig.PropertyChanged -= ProjectConfig_PropertyChanged;
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
