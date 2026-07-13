using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Batch;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using ColorVision.ImageEditor;
using ColorVision.SocketProtocol;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using Newtonsoft.Json;
using ProjectARVRPro.Exports;
using ProjectARVRPro.LegacyARVR;
using ProjectARVRPro.Process;
using ProjectARVRPro.Services;
using ProjectARVRPro.SocketRelay;
using SqlSugar;
using ST.Library.UI.NodeEditor;
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

namespace ProjectARVRPro
{
    public class ARVRWindowConfig : WindowConfig
    {
        public static ARVRWindowConfig Instance => ConfigService.Instance.GetRequiredService<ARVRWindowConfig>();
    }

    public partial class ARVRWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ARVRWindow));

        public static ProjectARVRProConfig ProjectConfig => ProjectARVRProConfig.Instance;

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();

        public static ObservableCollection<ProjectARVRReuslt> ViewResluts { get; set; } = ViewResultManager.ViewResluts;

        public static ProcessManager ProcessManager => ProcessManager.GetInstance();
        public ObservableCollection<ProcessMeta> ProcessMetas => ProcessManager.ProcessMetas;

        private readonly PictureSwitchService _pictureSwitchService;

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
        private (int Code, string Message)? _firstFlowFailure;
        private string _lastFlowFailureMessage = string.Empty;
        private IProcess? _currentFlowProcess;

        Random Random = new Random();
        public void InitTest(string SN)
        {
            ProjectConfig.StepIndex = 0;
            ObjectiveTestResult = new ObjectiveTestResult();
            ObjectiveTestResultRecordId = 0;
            _firstFlowFailure = null;
            _lastFlowFailureMessage = string.Empty;
            _currentFlowProcess = null;
            CurrentTestType = -1;
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProjectARVRProConfig.Instance.SN  = string.IsNullOrWhiteSpace(SN) ? "SN" + Random.NextInt64(10000, 90000).ToString() : SN.Trim(); 
            });
        }

        bool IsSwitchRun;
        private bool _isFlowLifecycleActive;
        public void SwitchPGCompleted()
        {
            if (IsSwitchRun)
            {
                log.Info("重复触发PG");
                return;
            }
            IsSwitchRun = true;

            try
            {
                if (flowControl.IsFlowRun || _isFlowLifecycleActive || _isRunAllRunning)
                {
                    log.Info("PG切换错误，正在执行流程或处理流程结果");
                    return;
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
                    ProjectConfig.StepIndex = nextTestType;
                    RunTemplate(template.Key, processMeta);
                }
            }
            finally
            {
                IsSwitchRun = false;
            }
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
        private void Window_Initialized(object sender, EventArgs e)
        {
            ProcessManager.GenStepBar(stepBar);
            _activeGroupChangedHandler = ProcessManager_ActiveGroupChanged;
            ProcessManager.ActiveGroupChanged += _activeGroupChangedHandler;
            this.DataContext = ProjectARVRProConfig.Instance;
            ProjectConfig.PropertyChanged += ProjectConfig_PropertyChanged;
            ApplyResultOverlayConfig();
            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            flowEngine = new FlowEngineControl(false);
            STNodeEditorMain = new STNodeEditor();
            STNodeEditorMain.LoadAssembly("FlowEngineLib.dll");
            flowEngine.AttachNodeEditor(STNodeEditorMain);
            flowControl = new FlowControl(MQTTControl.GetInstance(), flowEngine);

            timer = new Timer(TimeRun, null, 0, 100);
            timer.Change(Timeout.Infinite, 100); // 停止定时器


            logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
            LogGrid.Children.Add(logOutput);


            this.Closed += (s, e) =>
            {
                this.Dispose();
            };


            ViewResultManager.ListView = listView1;
            listView1.ItemsSource = ViewResluts;

            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Delete, (s, e) => Delete(), (s, e) => e.CanExecute = listView1.SelectedIndex > -1));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.SelectAll, (s, e) => listView1.SelectAll(), (s, e) => e.CanExecute = true));
            listView1.CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, ListViewUtils.Copy, (s, e) => e.CanExecute = true));

            // 构建 ListView 统一的右键菜单（替代原先每个实体各自创建 ContextMenu 的方案）
            BuildListViewContextMenu();

        }

        private void ProcessManager_ActiveGroupChanged(object? sender, EventArgs e)
        {
            if (!_isDisposed)
                ProcessManager.GenStepBar(stepBar);
        }

        private void OpenDatabaseCleanup_Click(object sender, RoutedEventArgs e)
        {
            DatabaseCleanupWindow.OpenWindow();
        }

        private void ViewOptions_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
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

            var viewTestResultCommand = new RelayCommand(
                _ => ContextMenu_ViewTestResult(),
                _ => listView1.SelectedItem is ProjectARVRReuslt item && !string.IsNullOrEmpty(item.ViewResultJson));

            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(new MenuItem() { Command = ApplicationCommands.Delete });
            contextMenu.Items.Add(new MenuItem() { Command = ApplicationCommands.Copy, Header = "复制" });
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(new MenuItem() { Command = openFolderCommand, Header = "OpenFolderAndSelectFile" });
            contextMenu.Items.Add(new MenuItem() { Command = batchHistoryCommand, Header = "流程结果查询" });
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
            if (listView1.SelectedItem is ProjectARVRReuslt item)
                PlatformHelper.OpenFolderAndSelectFile(item.FileName);
        }

        private void ContextMenu_BatchDataHistory()
        {
            if (listView1.SelectedItem is not ProjectARVRReuslt item) return;

            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            var Batch = Db.Queryable<MeasureBatchModel>().Where(a => a.Id == item.BatchId).First();
            if (Batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }
            var frame = new Frame();
            var batchDataHistory = new MeasureBatchPage(frame, Batch);
            var window = new Window() { Owner = Application.Current.GetActiveWindow() };
            window.Content = batchDataHistory;
            window.Show();
        }

        private void ContextMenu_ViewTestResult()
        {
            if (listView1.SelectedItem is not ProjectARVRReuslt item) return;
            if (string.IsNullOrEmpty(item.ViewResultJson))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "ViewResultJson为空", "ColorVision");
                return;
            }
            var window = new TestResultViewWindow(item.ViewResultJson)
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
            string Refreshdata = TemplateFlow.Params[FlowTemplate.SelectedIndex].Value.DataBase64;
            flowEngine.LoadFromBase64(Refreshdata, MqttRCService.GetInstance().ServiceTokens);

            foreach (var item in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
            {
                item.nodeRunEvent -= UpdateMsg;
                item.nodeRunEvent += UpdateMsg;
            }
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
                        msg = $"{FlowName}{Environment.NewLine}正在执行节点:{Msg1}{Environment.NewLine}已经执行：{elapsedTime} {Environment.NewLine}";
                    }
                    else
                    {
                        long remainingMilliseconds = LastFlowTime - elapsedMilliseconds;
                        TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                        string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                        msg = $"{FlowName}{Environment.NewLine}上次执行：{LastFlowTime} ms{Environment.NewLine}正在执行节点:{Msg1}{Environment.NewLine}已经执行：{elapsedTime} {Environment.NewLine}预计还需要：{remainingTime}";
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

        private void TestClick(object sender, RoutedEventArgs e)
        {
            RunTemplate();
        }


        ProjectARVRReuslt CurrentFlowResult { get; set; }
        int TryCount;

        public Task RunTemplate()
        {
            if (FlowTemplate.SelectedItem is not TemplateModel<FlowParam> template)
                return Task.CompletedTask;

            ProcessMeta? processMeta = ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, template.Key, StringComparison.OrdinalIgnoreCase));
            return RunTemplate(template.Key, processMeta);
        }

        private async Task RunTemplate(string flowTemplateKey, ProcessMeta? runProcessMeta)
        {
            if (flowControl.IsFlowRun || _isFlowLifecycleActive)
            {
                log.Info("当前flowControl存在流程执行或正在处理流程结果");
                return;
            }

            TryCount++;
            _currentFlowProcess = runProcessMeta?.Process;
            LastFlowTime = FlowEngineConfig.Instance.FlowRunTime.TryGetValue(flowTemplateKey, out long time) ? time : 0;

            CurrentFlowResult = new ProjectARVRReuslt();
            CurrentFlowResult.SN = ProjectARVRProConfig.Instance.SN;
            CurrentFlowResult.Model = flowTemplateKey;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (runProcessMeta != null)
                {
                    CurrentFlowResult.TestType = ProcessMetas.IndexOf(runProcessMeta);
                    ProjectARVRProConfig.Instance.StepIndex = CurrentFlowResult.TestType;
                }
                else
                {
                    CurrentFlowResult.TestType = CurrentTestType;
                    ProjectARVRProConfig.Instance.StepIndex = CurrentFlowResult.TestType;
                }
            });


            FlowName = flowTemplateKey;

            string sn = ViewResultManager.Config.CodeUseSN ? ProjectARVRProConfig.Instance.SN + "_" : "";
            CurrentFlowResult.Code = sn + DateTime.Now.ToString(ViewResultManager.Config.CodeDateFormat);

            await Refresh();

            if (string.IsNullOrWhiteSpace(flowEngine.GetStartNodeName())) { log.Info( "找不到完整流程，运行失败");return; }

            if (!await _pictureSwitchService.ExecuteAsync(runProcessMeta))
            {
                CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                CurrentFlowResult.Msg = "PictureSwitchFailed";
                await ExecuteProcessFailureAsync(runProcessMeta?.Process);
                RecordFlowFailure(CurrentFlowResult.Msg);
                logTextBox.Text = FlowName + Environment.NewLine + "切图失败";
                SendProjectResultResponse(_firstFlowFailure?.Code ?? -1, _firstFlowFailure?.Message ?? "ARVR Test Fail", ViewResultManager.Config.UseLegacyARVROutput ? LegacyARVRConverter.ToLegacy(ObjectiveTestResult) : ObjectiveTestResult);
                TryCount = 0;
                return;
            }

            if (!await PreProcessing(FlowName, CurrentFlowResult.SN))
            {
                CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                CurrentFlowResult.Msg = "PreProcessFailed";
                await ExecuteProcessFailureAsync(runProcessMeta?.Process);
                RecordFlowFailure(CurrentFlowResult.Msg);
                logTextBox.Text = FlowName + Environment.NewLine + "预处理失败";
                SendProjectResultResponse(_firstFlowFailure?.Code ?? -1, _firstFlowFailure?.Message ?? "ARVR Test Fail", ViewResultManager.Config.UseLegacyARVROutput ? LegacyARVRConverter.ToLegacy(ObjectiveTestResult) : ObjectiveTestResult);
                TryCount = 0;
                return;
            }

            CurrentFlowResult.FlowStatus = FlowStatus.Ready;

            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            flowControl.FlowCompleted += FlowControl_FlowCompleted;
            stopwatch.Reset();
            stopwatch.Start();

            MeasureBatchModel measureBatchModel = new MeasureBatchModel() { Name = CurrentFlowResult.SN, Code = CurrentFlowResult.Code };
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            int id = Db.Insertable(measureBatchModel).ExecuteReturnIdentity();
            CurrentFlowResult.BatchId = id;

            _isFlowLifecycleActive = true;
            flowControl.Start(CurrentFlowResult.Code);
            timer.Change(0, 500); // 启动定时器
        }

        private async Task<bool> PreProcessing(string flowName, string serialNumber)
        {
            var serverNodes = new ObservableCollection<CVBaseServerNode>(STNodeEditorMain.Nodes.OfType<CVBaseServerNode>());
            return await PreProcessManager.GetInstance().ExecuteAsync(flowName, serialNumber, serverNodes);
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
            FlowEngineConfig.Instance.FlowRunTime[FlowName] = stopwatch.ElapsedMilliseconds;

            log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
            CurrentFlowResult.RunTime  = stopwatch.ElapsedMilliseconds;
            logTextBox.Text = FlowName + Environment.NewLine + FlowControlData.EventName;

            if (FlowControlData.EventName == "Completed")
            {
                CurrentFlowResult.Msg = "Completed";
                try
                {
                    await Processing(FlowControlData.SerialNumber);
                    if (!IsTestTypeCompleted())
                    {
                        _isFlowLifecycleActive = false;
                        SwitchPG();
                    }
                    else
                    {
                        _isFlowLifecycleActive = false;
                    }
                }
                catch (Exception ex)
                {
                    _isFlowLifecycleActive = false;
                    MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
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
                Refresh();

                if (TryCount < ProjectARVRProConfig.Instance.TryCountMax)
                {
                    _isFlowLifecycleActive = false;
                    Task.Delay(200).ContinueWith(t =>
                    {
                        log.Info("重新尝试运行流程");
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            RunTemplate();
                        });
                    });
                    return;
                }
                else
                {
                    await ExecuteProcessFailureAsync(_currentFlowProcess);
                    RecordFlowFailure(CurrentFlowResult.Msg, -2);
                    ViewResultManager.Save(CurrentFlowResult);
                    SaveObjectiveTestResultRecord(CurrentFlowResult);
                    _isFlowLifecycleActive = false;
                    var response = new SocketResponse
                    {
                        Version = "1.0",
                        MsgID = "",
                        EventName = "ProjectARVRResult",
                        Code = -2,
                        Msg = _firstFlowFailure?.Message ?? CurrentFlowResult.Msg,
                        Data = ViewResultManager.Config.UseLegacyARVROutput ? LegacyARVRConverter.ToLegacy(ObjectiveTestResult) : ObjectiveTestResult
                    };
                    string respString = JsonConvert.SerializeObject(response);
                    log.Info(respString);
                    SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(respString));
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
                    if (SocketManager.GetInstance().TcpClients.Count > 0 && SocketControl.Current.Stream != null)
                    {
                        var response = new SocketResponse
                        {
                            Version = "1.0",
                            MsgID = "",
                            EventName = "ProjectARVRResult",
                            Code = _firstFlowFailure?.Code ?? -1,
                            Msg = _firstFlowFailure?.Message ?? "ARVR Test Fail",
                            Data = ViewResultManager.Config.UseLegacyARVROutput ? LegacyARVRConverter.ToLegacy(ObjectiveTestResult) : ObjectiveTestResult
                        };
                        string respString = JsonConvert.SerializeObject(response);
                        log.Info(respString);
                        SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(respString));
                    }
                }
            }
        }

        private async Task Processing(string SerialNumber)
        {
            MeasureBatchModel Batch = BatchResultMasterDao.Instance.GetByCode(SerialNumber);


            if (Batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }

            ProjectARVRReuslt result = CurrentFlowResult ?? new ProjectARVRReuslt();

            result.BatchId = Batch.Id;
            result.FlowStatus = FlowStatus.Completed;
            result.CreateTime = DateTime.Now;
            result.Result = true;

            try
            {
                log.Info($"{result.Model}");

                var meta = ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, result.Model, StringComparison.OrdinalIgnoreCase));
                if (meta?.Process != null)
                {
                    log.Info($"匹配到自定义流程 {meta.Name} -> {meta.ProcessTypeName}; 使用 IProcess 处理 {result.Model}");

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
                        executed = await meta.Process.Execute(ctx);
                    }
                    catch (Exception ex)
                    {
                        log.Error("自定义 IProcess 执行异常", ex);
                    }
                    if (executed)
                    {
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

                            string shortcutName = Path.GetFileNameWithoutExtension(result.FileName) + $"_{result.Model}";
                            string shortcutPath = linkPath;

                            if (shortcutName != null)
                                ColorVision.Common.NativeMethods.ShortcutCreator.CreateShortcut(shortcutName, shortcutPath, result.FileName, "");
                        }
                        IsSaveImageReuslt = ViewResultManager.Config.IsSaveImageReuslt;

                        if (IsTestTypeCompleted())
                        {
                            TestCompleted();
                        }
                        return; // 已处理，直接返回
                    }
                    else
                    {
                        log.Warn("自定义 IProcess 执行失败，继续使用内置解析逻辑");
                    }
                }
                else
                {
                    log.Info($"匹配到不到自定义流程");
                }
            }
            catch (Exception ex)
            {
                log.Error("匹配/执行自定义 IProcess 出错", ex);
            }
            ViewResultManager.Save(result);
            SaveObjectiveTestResultRecord(result);
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

        public bool IsSaveImageReuslt { get;set; }

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

            if (outputConfig.IsSaveCsv)
            {
                if (!Directory.Exists(csvOutputDirectory))
                    Directory.CreateDirectory(csvOutputDirectory);
            }

            if (outputConfig.IsSaveCustomXlsx)
            {
                if (!Directory.Exists(customXlsxOutputDirectory))
                    Directory.CreateDirectory(customXlsxOutputDirectory);
            }

            string baseFileName = $"TestResults_{SNtextBox.Text}_{timeStr}";

            if (outputConfig.IsSaveCsv)
            {
                string filePath = Path.Combine(csvOutputDirectory, $"{baseFileName}_.csv");

                if (outputConfig.UseLegacyARVROutput)
                {
                    var legacyResult = LegacyARVRConverter.ToLegacy(ObjectiveTestResult);
                    LegacyARVRCsvExporter.ExportToCsv(new List<LegacyARVRObjectiveTestResult> { legacyResult }, filePath);
                }
                else
                {
                    ObjectiveTestResultCsvExporter.ExportToCsv(ObjectiveTestResult, filePath);
                }
            }

            if (outputConfig.IsSaveCustomXlsx)
            {
                try
                {
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

        private void listView1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isDisposed)
                return;

            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                var result = ViewResluts[listView.SelectedIndex];
                try
                {
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

                Task.Run(() =>
                {
                    try
                    {
                        string filePath = result.FileName;
                        _ = Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                            {
                                ImageView.OpenImage(filePath);
                                RenderResultImage(result);
                                SaveImageResultIfNeeded(result);
                            }
                            else
                            {
                                ImageView.Clear();
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        log.Error("加载结果图片失败", ex);
                    }
                });

            }
        }

        private void RenderResultImage(ProjectARVRReuslt result)
        {
            ImageView.ImageShow.Clear();
            ApplyResultOverlayConfig();

            if (result.FlowStatus != FlowStatus.Completed)
                return;

            var meta = ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, result.Model, StringComparison.OrdinalIgnoreCase));
            if (meta?.Process == null) return;

            try
            {
                var ctx = new IProcessExecutionContext
                {
                    Result = result,
                    ObjectiveTestResult = ObjectiveTestResult,
                    ImageView = ImageView,
                };
                meta.Process.Render(ctx);
            }
            catch (Exception ex)
            {
                log.Error("自定义 IProcess 执行异常", ex);
            }
        }

        private void SaveImageResultIfNeeded(ProjectARVRReuslt result)
        {
            if (!IsSaveImageReuslt || _isDisposed) return;

            log.Info($"IsSaveImageReuslt:{IsSaveImageReuslt}");
            IsSaveImageReuslt = false;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(ViewResultManager.Config.SaveImageReusltDelay);
                    if (_isDisposed)
                        return;

                    string linkPath = ViewResultManager.Config.CsvSavePath;
                    string sn = result.SN;

                    if (ViewResultManager.Config.SaveByDate)
                    {
                        string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                        linkPath = Path.Combine(linkPath, dateFolder);
                    }

                    if (!string.IsNullOrWhiteSpace(sn))
                    {
                        foreach (char c in Path.GetInvalidFileNameChars())
                        {
                            sn = sn.Replace(c.ToString(), "");
                        }

                        if (!string.IsNullOrWhiteSpace(sn))
                        {
                            linkPath = Path.Combine(linkPath, sn);
                        }
                    }

                    if (!Directory.Exists(linkPath))
                        Directory.CreateDirectory(linkPath);

                    string fileName = Path.GetFileNameWithoutExtension(result.FileName);
                    string filePath = Path.Combine(linkPath, $"{fileName}_{result.Model}result.png");
                    log.Info(filePath);
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        if (_isDisposed)
                            return;

                        ImageView.Save(filePath);
                    });
                }
                catch (Exception ex)
                {
                    log.Error("保存结果截图失败", ex);
                }
            });
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

            var meta = ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, result.Model, StringComparison.OrdinalIgnoreCase));
            Brush foreground = result.Result ? Brushes.Black : Brushes.White;
            paragraph = new Paragraph();
            if (meta?.Process != null)
            {
                try
                {
                    var ctx = new IProcessExecutionContext
                    {
                        Result = result,
                        ObjectiveTestResult = ObjectiveTestResult,
                        ImageView = ImageView,
                    };
                    meta.Process.GenText(ctx, paragraph, foreground, outputFontSize);
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
            ProcessManager.GenStepBar(stepBar);
        }

        private void ObjectiveTestResultRecord_Click(object sender, RoutedEventArgs e)
        {
            var window = new ObjectiveTestResultRecordWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
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
            if (flowControl.IsFlowRun)
            {
                log.Info("当前存在流程执行，无法一键执行");
                return;
            }

            _isRunAllRunning = true;
            try
            {
                InitTest(ProjectARVRProConfig.Instance.SN);

                var enabledMetas = ProcessMetas.Where(m => m.IsEnabled).ToList();
                log.Info($"一键执行开始，共 {enabledMetas.Count} 个启用的流程");

                for (int i = 0; i < enabledMetas.Count; i++)
                {
                    ProcessMeta meta = enabledMetas[i];
                    CurrentTestType = ProcessMetas.IndexOf(meta);
                    ProjectConfig.StepIndex = CurrentTestType;

                    log.Info($"一键执行 [{i + 1}/{enabledMetas.Count}]: {meta.Name} ({meta.FlowTemplate})");

                    TemplateModel<FlowParam> templateParam = SelectFlowTemplate(meta);

                    // 执行流程并等待完成
                    var tcs = new TaskCompletionSource<FlowControlData>();
                    void completedHandler(object? s, FlowControlData data)
                    {
                        flowControl.FlowCompleted -= completedHandler;
                        tcs.TrySetResult(data);
                    }

                    // Reset state for this template run
                    TryCount = 0;
                    CurrentFlowResult = new ProjectARVRReuslt();
                    CurrentFlowResult.SN = ProjectARVRProConfig.Instance.SN;
                    CurrentFlowResult.Model = templateParam.Key;
                    CurrentFlowResult.TestType = CurrentTestType;
                    ProjectARVRProConfig.Instance.StepIndex = CurrentTestType;

                    FlowName = CurrentFlowResult.Model;
                    string sn = ViewResultManager.Config.CodeUseSN ? ProjectARVRProConfig.Instance.SN + "_" : "";
                    CurrentFlowResult.Code = sn + DateTime.Now.ToString(ViewResultManager.Config.CodeDateFormat);

                    await Refresh();

                    if (string.IsNullOrWhiteSpace(flowEngine.GetStartNodeName()))
                    {
                        log.Info($"找不到完整流程 {meta.FlowTemplate}，跳过");
                        continue;
                    }

                    if (!await _pictureSwitchService.ExecuteAsync(meta))
                    {
                        CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                        CurrentFlowResult.Msg = "PictureSwitchFailed";
                        await ExecuteProcessFailureAsync(meta.Process);
                        RecordFlowFailure(CurrentFlowResult.Msg);
                        logTextBox.Text = FlowName + Environment.NewLine + "切图失败";

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

                        if (!ProjectARVRProConfig.Instance.AllowTestFailures)
                        {
                            log.Error($"流程 {meta.Name} 预处理失败且不允许失败，终止一键执行");
                            break;
                        }

                        continue;
                    }

                    CurrentFlowResult.FlowStatus = FlowStatus.Ready;

                    LastFlowTime = FlowEngineConfig.Instance.FlowRunTime.TryGetValue(FlowName, out long time) ? time : 0;

                    MeasureBatchModel measureBatchModel = new MeasureBatchModel() { Name = CurrentFlowResult.SN, Code = CurrentFlowResult.Code };
                    using (var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true }))
                    {
                        int id = Db.Insertable(measureBatchModel).ExecuteReturnIdentity();
                        CurrentFlowResult.BatchId = id;
                    }

                    flowControl.FlowCompleted += completedHandler;
                    stopwatch.Reset();
                    stopwatch.Start();
                    flowControl.Start(CurrentFlowResult.Code);
                    timer.Change(0, 500);

                    // 等待流程完成（带超时保护，默认10分钟）
                    var timeoutTask = Task.Delay(TimeSpan.FromMinutes(10));
                    var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);
                    FlowControlData flowResult;
                    if (completedTask == timeoutTask)
                    {
                        flowControl.FlowCompleted -= completedHandler;
                        log.Error($"流程 {meta.Name} 执行超时(10min)");
                        flowResult = new FlowControlData
                        {
                            EventName = "OverTime",
                            ErrorNodeName = meta.Name,
                            Params = $"{meta.Name}({meta.FlowTemplate}) OverTime 10min"
                        };
                    }
                    else
                    {
                        flowResult = await tcs.Task;
                    }

                    stopwatch.Stop();
                    timer.Change(Timeout.Infinite, 500);
                    FlowEngineConfig.Instance.FlowRunTime[FlowName] = stopwatch.ElapsedMilliseconds;
                    log.Info($"流程 {meta.Name} 完成: {flowResult.EventName}, 耗时 {stopwatch.ElapsedMilliseconds}ms");

                    CurrentFlowResult.RunTime = stopwatch.ElapsedMilliseconds;
                    logTextBox.Text = FlowName + Environment.NewLine + flowResult.EventName;

                    if (flowResult.EventName == "Completed")
                    {
                        CurrentFlowResult.Msg = "Completed";
                        await Processing(flowResult.SerialNumber);
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

                        if (!ProjectARVRProConfig.Instance.AllowTestFailures)
                        {
                            log.Error($"流程 {meta.Name} 失败且不允许失败，终止一键执行");
                            break;
                        }
                    }
                }

                log.Info($"一键执行完成, TotalResult={ObjectiveTestResult.TotalResult}");

                // 如果有 Socket 连接，发送结果
                if (SocketManager.GetInstance().TcpClients.Count > 0 && SocketControl.Current.Stream != null)
                {
                    TestCompleted();
                }
            }
            catch (Exception ex)
            {
                log.Error("一键执行异常", ex);
            }
            finally
            {
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
            ProjectConfig.PropertyChanged -= ProjectConfig_PropertyChanged;
            if (_activeGroupChangedHandler != null)
            {
                ProcessManager.ActiveGroupChanged -= _activeGroupChangedHandler;
                _activeGroupChangedHandler = null;
            }
            if (ReferenceEquals(ViewResultManager.ListView, listView1))
                ViewResultManager.ListView = null;

            listView1.SelectionChanged -= listView1_SelectionChanged;
            listView1.ItemsSource = null;
            listView1.ContextMenu = null;
            listView1.CommandBindings.Clear();

            ImageView.Dispose();
            flowControl.Stop();
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
