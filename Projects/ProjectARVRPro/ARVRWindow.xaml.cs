#pragma warning disable CA1822,CS0168,CS0219,CS4014,CS8601
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Batch;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Scheduler;
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
using ProjectARVRPro.PluginConfig;
using ProjectARVRPro.Process;
using ProjectARVRPro.Services;
using ProjectARVRPro.SocketRelay;
using Quartz;
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
    public class ProjectARVRLitetestJob : IJob
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ProjectARVRLitetestJob));

        public Task Execute(IJobExecutionContext context)
        {
            var schedulerInfo = QuartzSchedulerManager.GetInstance().TaskInfos.First(x => x.JobName == context.JobDetail.Key.Name && x.GroupName == context.JobDetail.Key.Group);
            schedulerInfo.RunCount++;
            Application.Current.Dispatcher.Invoke(() =>
            {
                schedulerInfo.Status = SchedulerStatus.Running;
            });
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProjectWindowInstance.WindowInstance.SwitchPGCompleted();

                ProjectWindowInstance.WindowInstance.RunTemplate();

                schedulerInfo.Status = SchedulerStatus.Ready;
            });
            return Task.CompletedTask;
        }
    }


    public class SwitchPG
    {
        public int ARVRTestType { get; set; }
    }


    public class ARVRWindowConfig : WindowConfig
    {
        public static ARVRWindowConfig Instance => ConfigService.Instance.GetRequiredService<ARVRWindowConfig>();
    }

    public partial class ARVRWindow : Window, IDisposable
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ARVRWindow));
        public static ARVRWindowConfig Config => ARVRWindowConfig.Instance;

        public static ProjectARVRProConfig ProjectConfig => ProjectARVRProConfig.Instance;

        public static ViewResultManager ViewResultManager => ViewResultManager.GetInstance();

        public static ObservableCollection<ProjectARVRReuslt> ViewResluts { get; set; } = ViewResultManager.ViewResluts;

        public static ProcessManager ProcessManager => ProcessManager.GetInstance();
        public ObservableCollection<ProcessMeta> ProcessMetas => ProcessManager.ProcessMetas;

        // 雷鸟切图控制器
        private ThunderbirdSerialController _thunderbirdController = ThunderbirdSerialController.GetInstance();

        private static readonly HashSet<string> ResultOverlayConfigNames =
        [
            nameof(ProjectARVRProConfig.ResultOverlayShowName),
            nameof(ProjectARVRProConfig.ResultOverlayShowDetail),
            nameof(ProjectARVRProConfig.ResultOverlayFontSize),
            nameof(ProjectARVRProConfig.ResultOverlayAutoRefresh)
        ];

        public ARVRWindow()
        {
            InitializeComponent();
            this.ApplyCaption(false);
            Config.SetWindow(this);
            this.Title += Assembly.GetAssembly(typeof(ARVRWindow))?.GetName().Version?.ToString() ?? "";
        }

        private int CurrentTestType = -1;

        ObjectiveTestResult ObjectiveTestResult { get; set; } = new ObjectiveTestResult();
        private string _flowFailureMessage = string.Empty;
        private int _flowFailureCode;
        Random Random = new Random();
        public void InitTest(string SN)
        {
            ProjectConfig.StepIndex = 0;
            ObjectiveTestResult = new ObjectiveTestResult();
            _flowFailureMessage = string.Empty;
            _flowFailureCode = 0;
            CurrentTestType = -1;
            if (string.IsNullOrWhiteSpace(SN))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectARVRProConfig.Instance.SN = "SN" + Random.NextInt64(10000, 90000).ToString();
                });
            }
            else
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProjectARVRProConfig.Instance.SN = SN;
                });
            }
        }

        bool IsSwitchRun;
        public void SwitchPGCompleted()
        {
            if (IsSwitchRun)
            {
                log.Info("重复触发PG");
                return;
            }
            IsSwitchRun = true;

            if (flowControl.IsFlowRun)
            {
                log.Info("PG切换错误，正在执行流程");
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
                ProjectConfig.StepIndex = nextTestType;
                FlowTemplate.SelectedValue = TemplateFlow.Params.First(a => a.Key.Contains(processMeta.FlowTemplate)).Value;
                CurrentTestType = nextTestType;
                RunTemplate();
            }

            IsSwitchRun = false;
        }
 
        public STNodeEditor STNodeEditorMain { get; set; }
        private FlowEngineControl flowEngine;
        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();




        private LogOutput? logOutput;
        private void Window_Initialized(object sender, EventArgs e)
        {
            ProcessManager.GenStepBar(stepBar);
            ProcessManager.ActiveGroupChanged += (s, ev) => ProcessManager.GenStepBar(stepBar);
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


            if (ProjectARVRProConfig.Instance.LogControlVisibility)
            {
                logOutput = new LogOutput("%date{HH:mm:ss} [%thread] %-5level %message%newline");
                LogGrid.Children.Add(logOutput);
            }
            else
            {
                LogGrid.Visibility = Visibility.Collapsed;
            }


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

            _thunderbirdController.ConnectionStateChanged += ThunderbirdController_ConnectionStateChanged;
            UpdateThunderbirdStatusIndicator();
            _ = TryAutoConnectThunderbirdAsync();

        }

        private void OpenDatabaseCleanup_Click(object sender, RoutedEventArgs e)
        {
            DatabaseCleanupWindow.OpenWindow();
        }

        private void ThunderbirdController_ConnectionStateChanged(object? sender, EventArgs e)
        {
            UpdateThunderbirdStatusIndicator();
        }

        private async Task TryAutoConnectThunderbirdAsync()
        {
            if (_thunderbirdController.IsConnected)
            {
                UpdateThunderbirdStatusIndicator();
                return;
            }

            if (!ProjectConfig.ThunderbirdAutoConnect)
                return;

            if (string.IsNullOrWhiteSpace(ProjectConfig.ThunderbirdPortName))
            {
                log.Warn("雷鸟自动连接已启用，但未配置串口号");
                return;
            }

            try
            {
                int timeoutMs = ProjectConfig.ThunderbirdTimeoutMs > 0 ? ProjectConfig.ThunderbirdTimeoutMs : 1000;
                _thunderbirdController.Open(ProjectConfig.ThunderbirdPortName, ProjectConfig.ThunderbirdBaudRate, timeoutMs);
                log.Info($"雷鸟自动连接成功: {ProjectConfig.ThunderbirdPortName}");
                UpdateThunderbirdStatusIndicator();
                await _thunderbirdController.QueryBrightnessAsync(timeoutMs);
            }
            catch (Exception ex)
            {
                log.Warn("雷鸟自动连接失败", ex);
                UpdateThunderbirdStatusIndicator();
            }
        }

        private void UpdateThunderbirdStatusIndicator()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => UpdateThunderbirdStatusIndicator());
                return;
            }

            if (_thunderbirdController.IsConnected)
            {
                string port = string.IsNullOrWhiteSpace(_thunderbirdController.CurrentPortName) ? "未知串口" : _thunderbirdController.CurrentPortName;
                ThunderbirdConnectionStatusText.Content = $"切图: 已连接 {port}";
                ThunderbirdConnectionStatusText.Foreground = Brushes.LimeGreen;
            }
            else
            {
                ThunderbirdConnectionStatusText.Content = "切图: 未连接";
                ThunderbirdConnectionStatusText.Foreground = Brushes.Gray;
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

        private void ServicesChanged(object? sender, EventArgs e)
        {
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                log.Info("Service触发拍照，执行流程");
                RunTemplate();
            });
        }


        public async Task Refresh()
        {
            if (FlowTemplate.SelectedIndex < 0) return;
            MqttRCService.GetInstance().QueryServices();
            string Refreshdata = TemplateFlow.Params[FlowTemplate.SelectedIndex].Value.DataBase64;

            try
            {
                foreach (var item in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
                    item.nodeRunEvent -= UpdateMsg;

                flowEngine.LoadFromBase64(Refreshdata, MqttRCService.GetInstance().ServiceTokens);

                for (int i = 0; i < 200; i++)
                {
                    if (flowEngine.IsReady)
                        break;
                    await Task.Delay(10);
                }
                foreach (var item in STNodeEditorMain.Nodes.OfType<CVCommonNode>())
                    item.nodeRunEvent += UpdateMsg;

            }
            catch (Exception ex)
            {
                flowEngine.LoadFromBase64(string.Empty);
            }
            log.Info($"IsReady{flowEngine.IsReady}");
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

        public async Task RunTemplate()
        {
            if (flowControl.IsFlowRun)
            {
                log.Info("当前flowControl存在流程执行");
                return;
            }

            TryCount++;
            LastFlowTime = FlowEngineConfig.Instance.FlowRunTime.TryGetValue(FlowTemplate.Text, out long time) ? time : 0;

            CurrentFlowResult = new ProjectARVRReuslt();
            CurrentFlowResult.SN = ProjectARVRProConfig.Instance.SN;
            CurrentFlowResult.Model = FlowTemplate.Text;

            ProcessMeta? runProcessMeta = null;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, FlowTemplate.Text, StringComparison.OrdinalIgnoreCase)) is ProcessMeta processMeta)
                {
                    runProcessMeta = processMeta;
                    CurrentFlowResult.TestType = ProcessMetas.IndexOf(processMeta);
                    ProjectARVRProConfig.Instance.StepIndex = CurrentFlowResult.TestType;

                }
                else
                {
                    CurrentFlowResult.TestType = CurrentTestType;
                    ProjectARVRProConfig.Instance.StepIndex = CurrentFlowResult.TestType;
                }
            });


            FlowName = FlowTemplate.Text;

            string sn = ViewResultManager.Config.CodeUseSN ? ProjectARVRProConfig.Instance.SN + "_" : "";
            CurrentFlowResult.Code = sn + DateTime.Now.ToString(ViewResultManager.Config.CodeDateFormat);

            await Refresh();

            if (string.IsNullOrWhiteSpace(flowEngine.GetStartNodeName())) { log.Info( "找不到完整流程，运行失败");return; }

            if (!flowEngine.IsReady)
            {
                string base64 = string.Empty;
                flowEngine.LoadFromBase64(base64);
                await Refresh();
                log.Info($"IsReady{flowEngine.IsReady}");
            }

            if (!await ExecutePictureSwitchAsync(runProcessMeta))
            {
                CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                CurrentFlowResult.Msg = "PictureSwitchFailed";
                RecordFlowFailure(CurrentFlowResult.Msg);
                logTextBox.Text = FlowName + Environment.NewLine + "切图失败";
                SendProjectResultResponse(GetProjectResultCode(-1), GetProjectResultMessage("ARVR Test Fail"), CreateProjectResponseData());
                TryCount = 0;
                return;
            }

            if (!await PreProcessing(FlowName, CurrentFlowResult.SN))
            {
                CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                CurrentFlowResult.Msg = "PreProcessFailed";
                RecordFlowFailure(CurrentFlowResult.Msg);
                logTextBox.Text = FlowName + Environment.NewLine + "预处理失败";
                SendProjectResultResponse(GetProjectResultCode(-1), GetProjectResultMessage("ARVR Test Fail"), CreateProjectResponseData());
                TryCount = 0;
                return;
            }

            CurrentFlowResult.FlowStatus = FlowStatus.Ready;

            flowControl.FlowCompleted += FlowControl_FlowCompleted;
            stopwatch.Reset();
            stopwatch.Start();

            MeasureBatchModel measureBatchModel = new MeasureBatchModel() { Name = CurrentFlowResult.SN, Code = CurrentFlowResult.Code };
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            int id = Db.Insertable(measureBatchModel).ExecuteReturnIdentity();
            CurrentFlowResult.BatchId = id;

            flowControl.Start(CurrentFlowResult.Code);
            timer.Change(0, 500); // 启动定时器
        }

        private async Task<bool> PreProcessing(string flowName, string serialNumber)
        {
            var serverNodes = new ObservableCollection<CVBaseServerNode>(STNodeEditorMain.Nodes.OfType<CVBaseServerNode>());
            return await PreProcessManager.GetInstance().ExecuteAsync(flowName, serialNumber, serverNodes);
        }



        private FlowControl flowControl;

        private bool HasFlowFailure => !string.IsNullOrWhiteSpace(_flowFailureMessage);

        private void RecordFlowFailure(string? message, int code = -1)
        {
            string normalizedMessage = string.IsNullOrWhiteSpace(message) ? "ARVR Test Fail" : message.Trim();
            if (!HasFlowFailure)
            {
                _flowFailureMessage = normalizedMessage;
                _flowFailureCode = code;
            }
            ObjectiveTestResult.TotalResult = false;
        }

        private string GetProjectResultMessage(string defaultMessage)
        {
            return HasFlowFailure ? _flowFailureMessage : defaultMessage;
        }

        private int GetProjectResultCode(int defaultCode = 0)
        {
            return HasFlowFailure ? _flowFailureCode : defaultCode;
        }

        private string GetFlowControlMessage(FlowControlData flowControlData)
        {
            if (!string.IsNullOrWhiteSpace(flowControlData.Params))
                return flowControlData.Params.Trim();

            if (!string.IsNullOrWhiteSpace(flowControlData.Message))
                return flowControlData.Message.Trim();

            string eventName = string.IsNullOrWhiteSpace(flowControlData.EventName) ? "Failed" : flowControlData.EventName.Trim();
            if (!string.IsNullOrWhiteSpace(flowControlData.ErrorNodeName))
                return $"{flowControlData.ErrorNodeName}:{eventName}";

            return string.IsNullOrWhiteSpace(FlowName) ? eventName : $"{FlowName}:{eventName}";
        }

        private object CreateProjectResponseData()
        {
            return ViewResultManager.Config.UseLegacyARVROutput
                ? LegacyARVRConverter.ToLegacy(ObjectiveTestResult)
                : ObjectiveTestResult;
        }

        private void SendProjectResultResponse(int code, string message, object responseData)
        {
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

        private void FlowControl_FlowCompleted(object? sender, FlowControlData FlowControlData)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器
            FlowEngineConfig.Instance.FlowRunTime[FlowTemplate.Text] = stopwatch.ElapsedMilliseconds;

            log.Info($"流程执行Elapsed Time: {stopwatch.ElapsedMilliseconds} ms");
            CurrentFlowResult.RunTime  = stopwatch.ElapsedMilliseconds;
            logTextBox.Text = FlowName + Environment.NewLine + FlowControlData.EventName;

            if (FlowControlData.EventName == "Completed")
            {
                CurrentFlowResult.Msg = "Completed";
                try
                {
                    //如果没有执行完，先切换PG，并且提前设置流程
                    if (!IsTestTypeCompleted())
                    {
                        SwitchPG();
                    }

                    Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        Processing(FlowControlData.SerialNumber);
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                }
                TryCount = 0;
            }
            else if (FlowControlData.EventName == "OverTime")
            {
                log.Info("流程运行超时，正在重新尝试");
                CurrentFlowResult.FlowStatus = FlowStatus.OverTime;
                CurrentFlowResult.Msg = GetFlowControlMessage(FlowControlData);
                ViewResultManager.Save(CurrentFlowResult);

                flowEngine.LoadFromBase64(string.Empty);
                Refresh();

                if (TryCount < ProjectARVRProConfig.Instance.TryCountMax)
                {
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
                    RecordFlowFailure(CurrentFlowResult.Msg, -2);
                    var response = new SocketResponse
                    {
                        Version = "1.0",
                        MsgID = "",
                        EventName = "ProjectARVRResult",
                        Code = -2,
                        Msg = GetProjectResultMessage(CurrentFlowResult.Msg),
                        Data = CreateProjectResponseData()
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
                CurrentFlowResult.Msg = GetFlowControlMessage(FlowControlData);
                RecordFlowFailure(CurrentFlowResult.Msg);

                //算法失败但是图像是有的，可以帮助用户即使发现原因
                if (CurrentFlowResult.Msg.Contains("SDK return failed") || CurrentFlowResult.Msg.Contains("BinocularFusion calculation failed") || CurrentFlowResult.Msg.Contains("Not get cie file"))
                {
                    MeasureBatchModel Batch = BatchResultMasterDao.Instance.GetByCode(FlowControlData.SerialNumber);
                    if (Batch != null)
                    {
                        var values = MeasureImgResultDao.Instance.GetAllByBatchId(Batch.Id);
                        if (values.Count > 0)
                        {
                            CurrentFlowResult.FileName = values[0].FileUrl;
                        }
                    }
                }


                ViewResultManager.Save(CurrentFlowResult);
                logTextBox.Text = FlowName + Environment.NewLine + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params;

                TryCount = 0;

                if (ProjectARVRProConfig.Instance.AllowTestFailures)
                {
                    //如果允许失败，则切换PG，并且提前设置流程,执行结束时直接发送结束
                    if (!IsTestTypeCompleted())
                    {
                        SwitchPG();
                    }
                    else
                    {
                        TestCompleted();
                    }
                }
                else
                {
                    if (SocketManager.GetInstance().TcpClients.Count > 0 && SocketControl.Current.Stream != null)
                    {
                        var response = new SocketResponse
                        {
                            Version = "1.0",
                            MsgID = "",
                            EventName = "ProjectARVRResult",
                            Code = GetProjectResultCode(-1),
                            Msg = GetProjectResultMessage("ARVR Test Fail"),
                            Data = CreateProjectResponseData()
                        };
                        string respString = JsonConvert.SerializeObject(response);
                        log.Info(respString);
                        SocketControl.Current.Stream.Write(Encoding.UTF8.GetBytes(respString));
                    }
                }
            }
        }

        private void Processing(string SerialNumber)
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
                        executed = meta.Process.Execute(ctx);
                    }
                    catch (Exception ex)
                    {
                        log.Error("自定义 IProcess 执行异常", ex);
                    }
                    if (executed)
                    {
                        ViewResultManager.Save(result);
                        ObjectiveTestResult.TotalResult = ObjectiveTestResult.TotalResult && result.Result;

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
                log.Error("匹配/执行自定义 IProcess 出错，回退内置逻辑", ex);
            }
            ViewResultManager.Save(result);
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

            var response = new SocketResponse
            {
                Version = "1.0",
                MsgID = string.Empty,
                EventName = "SwitchPG",
                Code = 0,
                Msg = "Switch PG",
                SerialNumber = SNtextBox.Text,
                Data = new SwitchPG
                {
                    ARVRTestType = nextTestType
                },
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
                Code = GetProjectResultCode(),
                SerialNumber = SNtextBox.Text,
                Msg = GetProjectResultMessage("ARVR Test Completed"),
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
            outputText.Background = Brushes.White;
        }

        private void listView1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
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
                        outputText.Background = Brushes.White;
                        outputText.Document.Blocks.Clear(); // 清除之前的内容
                    }

                }
                catch (Exception ex)
                {
                    log.Error(ex);
                }
                Task.Run((Func<Task?>)(async () =>
                {
                    if (File.Exists(result.FileName))
                    {
                        _ = Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            ImageView.OpenImage(result.FileName);
                            ImageView.ImageShow.Clear();
                            ApplyResultOverlayConfig();

                            if (result.FlowStatus != FlowStatus.Completed)
                                return;

                            var meta = ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, result.Model, StringComparison.OrdinalIgnoreCase));
                            if (meta?.Process != null)
                            {
                                bool executed = false;
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

                            if (IsSaveImageReuslt)
                            {
                                log.Info($"IsSaveImageReuslt:{IsSaveImageReuslt}");
                                IsSaveImageReuslt = false;
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        await Task.Delay(ViewResultManager.Config.SaveImageReusltDelay);
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

                                        string FileName = Path.GetFileNameWithoutExtension(result.FileName);

                                        string FilePath = Path.Combine(linkPath, $"{FileName}_{result.Model}result.png");
                                        log.Info(FilePath);
                                        Application.Current?.Dispatcher.Invoke(() =>
                                        {
                                            ImageView.Save(FilePath);
                                        });
                                    }
                                    catch (Exception ex)
                                    {
                                        log.Error("保存结果截图失败", ex);
                                    }
                                });
                            }


                        });
                    }
                }));

            }
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

            string outtext = string.Empty;
            outtext += $"Model:{result.Model}  SN:{result.SN}  {DateTime.Now:yyyy/MM//dd HH:mm:ss}";
            Run run = new Run(outtext);
            run.Foreground = result.Result ? Brushes.Black : Brushes.White;
            run.FontSize += 1;

            var paragraph = new Paragraph();
            paragraph.Inlines.Add(run);
            outputText.Document.Blocks.Add(paragraph);
            outtext = string.Empty;


            var meta = ProcessMetas.FirstOrDefault(m => string.Equals(m.FlowTemplate, result.Model, StringComparison.OrdinalIgnoreCase));
            if (meta?.Process != null)
            {
                bool executed = false;
                try
                {
                    var ctx = new IProcessExecutionContext
                    {
                        Result = result,
                        ObjectiveTestResult = ObjectiveTestResult,
                        ImageView = ImageView,
                    };
                    outtext += meta.Process.GenText(ctx);
                }
                catch (Exception ex)
                {
                    log.Error("自定义 IProcess 执行异常", ex);
                }
            }

            outtext += Environment.NewLine + $"Pass/Fail Criteria:" + Environment.NewLine;
            outtext += result.Result ? "Pass" : "Fail" + Environment.NewLine;



            run = new Run(outtext);
            run.Foreground = result.Result ? Brushes.Black : Brushes.White;
            run.FontSize += 1;
            paragraph = new Paragraph(run);
            outtext = string.Empty;
            outputText.Document.Blocks.Add(paragraph);
            SNtextBox.Focus();
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

        private void SNtextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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

                    var templateParam = TemplateFlow.Params.FirstOrDefault(a => a.Key.Contains(meta.FlowTemplate));
                    if (templateParam == null)
                    {
                        log.Error($"找不到流程模板: {meta.FlowTemplate}");
                        continue;
                    }
                    FlowTemplate.SelectedValue = templateParam.Value;

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
                    CurrentFlowResult.Model = FlowTemplate.Text;
                    CurrentFlowResult.TestType = CurrentTestType;
                    ProjectARVRProConfig.Instance.StepIndex = CurrentTestType;

                    FlowName = FlowTemplate.Text;
                    string sn = ViewResultManager.Config.CodeUseSN ? ProjectARVRProConfig.Instance.SN + "_" : "";
                    CurrentFlowResult.Code = sn + DateTime.Now.ToString(ViewResultManager.Config.CodeDateFormat);

                    await Refresh();

                    if (string.IsNullOrWhiteSpace(flowEngine.GetStartNodeName()))
                    {
                        log.Info($"找不到完整流程 {meta.FlowTemplate}，跳过");
                        continue;
                    }

                    if (!flowEngine.IsReady)
                    {
                        flowEngine.LoadFromBase64(string.Empty);
                        await Refresh();
                    }

                    if (!await ExecutePictureSwitchAsync(meta))
                    {
                        CurrentFlowResult.FlowStatus = FlowStatus.Failed;
                        CurrentFlowResult.Msg = "PictureSwitchFailed";
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
                        RecordFlowFailure(CurrentFlowResult.Msg);
                        logTextBox.Text = FlowName + Environment.NewLine + "预处理失败";
                        ViewResultManager.Save(CurrentFlowResult);

                        if (!ProjectARVRProConfig.Instance.AllowTestFailures)
                        {
                            log.Error($"流程 {meta.Name} 预处理失败且不允许失败，终止一键执行");
                            break;
                        }

                        continue;
                    }

                    CurrentFlowResult.FlowStatus = FlowStatus.Ready;

                    LastFlowTime = FlowEngineConfig.Instance.FlowRunTime.TryGetValue(FlowTemplate.Text, out long time) ? time : 0;

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
                    FlowEngineConfig.Instance.FlowRunTime[FlowTemplate.Text] = stopwatch.ElapsedMilliseconds;
                    log.Info($"流程 {meta.Name} 完成: {flowResult.EventName}, 耗时 {stopwatch.ElapsedMilliseconds}ms");

                    CurrentFlowResult.RunTime = stopwatch.ElapsedMilliseconds;
                    logTextBox.Text = FlowName + Environment.NewLine + flowResult.EventName;

                    if (flowResult.EventName == "Completed")
                    {
                        CurrentFlowResult.Msg = "Completed";
                        Processing(flowResult.SerialNumber);
                    }
                    else
                    {
                        CurrentFlowResult.FlowStatus = flowResult.EventName == "OverTime" ? FlowStatus.OverTime : FlowStatus.Failed;
                        CurrentFlowResult.Msg = GetFlowControlMessage(flowResult);
                        RecordFlowFailure(CurrentFlowResult.Msg, flowResult.EventName == "OverTime" ? -2 : -1);
                        ViewResultManager.Save(CurrentFlowResult);

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

        private async Task<bool> ExecutePictureSwitchAsync(ProcessMeta? meta)
        {
            PictureSwitchConfig? config = meta?.PictureSwitchConfig;
            if (config == null || !config.IsEnabled)
                return true;

            if (config.Mode != PictureSwitchMode.Thunderbird)
            {
                log.Error($"不支持的切图模式: {config.Mode}");
                return false;
            }

            if (!_thunderbirdController.IsConnected)
            {
                log.Error($"流程 {meta?.Name} 已启用雷鸟切图，但雷鸟串口未连接");
                return false;
            }

            try
            {
                int timeoutMs = config.TimeoutMs > 0 ? config.TimeoutMs : 1000;
                ThunderbirdSerialController.CommandResult result = await _thunderbirdController.SendConfiguredCommandAsync(
                    config.SendCommand,
                    config.ExpectedResponse,
                    timeoutMs);

                if (!result.Success)
                {
                    log.Error($"流程 {meta?.Name} 雷鸟切图失败: Command={result.Command}, Expected={config.ExpectedResponse}, Response={result.Response ?? "<null>"}");
                    return false;
                }

                if (config.SuccessDelayMs > 0)
                {
                    log.Info($"流程 {meta?.Name} 雷鸟切图成功，等待图像稳定 {config.SuccessDelayMs}ms");
                    await Task.Delay(config.SuccessDelayMs);
                }

                log.Info($"流程 {meta?.Name} 雷鸟切图完成，执行流程");
                return true;
            }
            catch (Exception ex)
            {
                log.Error($"流程 {meta?.Name} 雷鸟切图异常", ex);
                return false;
            }
        }


        public void Dispose()
        {
            ProjectConfig.PropertyChanged -= ProjectConfig_PropertyChanged;
            flowControl.Stop();
            STNodeEditorMain.Dispose();
            timer.Change(Timeout.Infinite, 500); // 停止定时器
            timer?.Dispose();
            logOutput?.Dispose();
            _thunderbirdController.ConnectionStateChanged -= ThunderbirdController_ConnectionStateChanged;
            _thunderbirdController?.Close();
            GC.SuppressFinalize(this);
        }


        private void OpenSocketRelay_Click(object sender, RoutedEventArgs e)
        {
            SocketRelayWindow.OpenWindow();
        }

        private ThunderbirdSerialDebugWindow? _thunderbirdDebugWindow;

        private void OpenThunderbirdSerialDebug_Click(object sender, RoutedEventArgs e)
        {
            if (_thunderbirdDebugWindow == null)
            {
                _thunderbirdDebugWindow = new ThunderbirdSerialDebugWindow();
                _thunderbirdDebugWindow.Closed += (s, args) => _thunderbirdDebugWindow = null;
                _thunderbirdDebugWindow.Show();
            }
            else
            {
                _thunderbirdDebugWindow.Activate();
            }
        }
    }
}
