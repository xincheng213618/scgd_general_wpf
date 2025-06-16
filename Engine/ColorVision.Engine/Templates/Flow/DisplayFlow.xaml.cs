#pragma warning disable CS8602,CS8603,CS8601
using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Flow;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates.FOV;
using ColorVision.Scheduler;
using ColorVision.SocketProtocol;
using ColorVision.UI;
using FlowEngineLib;
using FlowEngineLib.Base;
using log4net;
using Panuon.WPF.UI;
using Quartz;
using ST.Library.UI.NodeEditor;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Flow
{
    public class FlowJob : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            var schedulerInfo = QuartzSchedulerManager.GetInstance().TaskInfos.First(x => x.JobName == context.JobDetail.Key.Name && x.GroupName == context.JobDetail.Key.Group);
            schedulerInfo.RunCount++;
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                schedulerInfo.Status = SchedulerStatus.Running;
            });
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                DisplayFlow.GetInstance().RunFlow();
                schedulerInfo.Status = SchedulerStatus.Ready;
            });
            return Task.CompletedTask;
        }
    }

    public class FlowSocketMsgHandle : ISocketEventHandler
    {
        public string EventName => "Flow";
        public SocketResponse Handle(NetworkStream stream, SocketRequest request)
        {
            if (TemplateFlow.Params.FirstOrDefault(a => a.Key == request.Params)?.Value is FlowParam flowParam)
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    DisplayFlow.GetInstance().ComboBoxFlow.SelectedValue = flowParam;
                    DisplayFlow.GetInstance().RunFlow();
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

        private static DisplayFlow _instance;
        private static readonly object _locker = new();
        public static DisplayFlow GetInstance() { lock (_locker) { return _instance ??= new DisplayFlow(); } }

        public ViewFlow View { get; set; }
        public string DisPlayName => "Flow";
        public static FlowConfig Config => FlowConfig.Instance;

        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();
        IPendingHandler handler { get; set; }

        public DisplayFlow()
        {
            InitializeComponent();
            CommandBindings.Add(new CommandBinding(EngineCommands.StartExecutionCommand, (s, e) => RunFlow(), (s, e) =>
            {
                if (flowControl != null)
                    e.CanExecute = !flowControl.IsFlowRun;
            }));
            CommandBindings.Add(new CommandBinding(EngineCommands.StopExecutionCommand, (s, e) => StopFlow(), (s, e) =>
            {
                if (flowControl != null)
                    e.CanExecute = flowControl.IsFlowRun;
            })); 
        }


        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            this.ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = "开始执行(_S)", Command = EngineCommands.StartExecutionCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "停止执行(_S)", Command = EngineCommands.StopExecutionCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "属性", Command = Config.EditCommand });


            View = new ViewFlow();
            View.View.Title = $"流程窗口 ";
            this.SetIconResource("DrawingImageFlow", View.View);

            this.AddViewConfig(View, ComboxView);
            View.DisplayFlow = this;
            ComboBoxFlow.ItemsSource = TemplateFlow.Params;
            ComboBoxFlow.SelectionChanged += (s, e) =>
            {
                if (ComboBoxFlow.SelectedValue is FlowParam flowParam)
                {
                    FlowConfig.Instance.LastSelectFlow = flowParam.Id;
                    if (FlowConfig.Instance.FlowRunTime.TryGetValue(flowParam.Name, out long time))
                        LastFlowTime = time;

                }
                Refresh();
            };
            MqttRCService.GetInstance().ServiceTokensInitialized +=(s,e) => Refresh();

            this.ApplyChangedSelectedColor(DisPlayBorder);

            timer = new Timer(UpdateMsg, null, 0, 500);
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            this.Loaded += FlowDisplayControl_Loaded;
            View.RefreshFlow += (s,e) => Refresh();
            flowControl ??= new FlowControl(MQTTControl.GetInstance(), View.FlowEngineControl);

        }


        private void FlowDisplayControl_Loaded(object sender, RoutedEventArgs e)
        {
            var s = TemplateFlow.Params.FirstOrDefault(a => a.Id == FlowConfig.Instance.LastSelectFlow);
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


        private void Refresh()
        {

            if (MqttRCService.GetInstance().ServiceTokens.Count == 0)
            {
                MqttRCService.GetInstance().QueryServices();
            }
           
            if (ComboBoxFlow.SelectedIndex  <0 && ComboBoxFlow.SelectedIndex >= TemplateFlow.Params.Count) return;
            if (ComboBoxFlow.SelectedIndex < 0) return;
            FlowParam flowParam = TemplateFlow.Params[ComboBoxFlow.SelectedIndex].Value;

            if (View == null) return;
            if (string.IsNullOrEmpty(flowParam.DataBase64))
            {
                MessageBox.Show("再选择之前请先创建对映的模板");
                View.FlowEngineControl.LoadFromBase64(string.Empty);
                return;
            }

            try
            {
                foreach (var item in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
                {
                    item.nodeRunEvent -= UpdateMsg;
                    item.nodeEndEvent -= nodeEndEvent;
                }

                View.FlowEngineControl.LoadFromBase64(string.Empty);
                View.FlowEngineControl.LoadFromBase64(flowParam.DataBase64, MqttRCService.GetInstance().ServiceTokens);
                View.FlowParam = flowParam;
                foreach (var item in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
                {
                    item.nodeRunEvent += UpdateMsg;
                    item.nodeEndEvent += nodeEndEvent;
                }
                View.STNodeEditorHelper.AddNodeContext();

                if (Config.IsAutoSize)
                    View.AutoSize();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                View.FlowEngineControl.LoadFromBase64(string.Empty);
            }
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }

        public FlowControl flowControl { get; set; }

        bool LastCompleted = true;

        private void FlowControl_FlowCompleted(object? sender, FlowControlData FlowControlData)
        {
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            FlowConfig.Instance.FlowRunTime[ComboBoxFlow.Text] = stopwatch.ElapsedMilliseconds;

            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            handler?.Close();

            ButtonRun.Visibility = Visibility.Visible;
            ButtonStop.Visibility = Visibility.Collapsed;

            if (FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
            {
                ErrorSign();
                FlowConfig.Instance.FlowRunComplete[ComboBoxFlow.Text] = false;
            }

            if (FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
            {
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "流程计算" + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params, "ColorVision");
                });
            }
            else
            {
                FlowConfig.Instance.FlowRunComplete[ComboBoxFlow.Text] = true;
            }
        }

        public ImageSource Icon { get => _Icon; set { _Icon = value; } }
        private ImageSource _Icon;

        private static void CheckDiskSpace(string driveLetter = "C")
        {
            if (!Config.ShowWarning) return;
            DriveInfo drive = new DriveInfo(driveLetter);
            if (drive.IsReady)
            {
                long availableSpace = drive.AvailableFreeSpace;
                long threshold = Config.Capacity; //10 GB in bytes

                if (availableSpace < threshold)
                {
                    MessageBox.Show($"警告: {driveLetter}盘空间不足。剩余空间为 {availableSpace / (1024 * 1024 * 1024)} GB", "空间不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private long LastFlowTime;

        string Msg1;
        private void UpdateMsg(object? sender)
        {
            if (!FlowConfig.Instance.FlowPreviewMsg) return;
            if (flowControl.IsFlowRun)
            {
                long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                TimeSpan elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);
                string elapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}:{elapsed.Milliseconds:D4}";
                string msg;
                if (LastFlowTime == 0 || LastFlowTime - elapsedMilliseconds < 0)
                {
                    msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                }
                else
                {
                    long remainingMilliseconds =LastFlowTime - elapsedMilliseconds;
                    TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                    string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                    msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}, 上次执行：{LastFlowTime} ms, 预计还需要：{remainingTime}";
                }
                Application.Current.Dispatcher.BeginInvoke(() =>
                {
                    if (flowControl.IsFlowRun)
                        handler.UpdateMessage(msg);
                });
            }
        }

        PropertyInfo MarkColorProperty { get; set; }
        private void nodeEndEvent(object sender, FlowEngineNodeEndEventArgs e)
        {
            if (sender is CVCommonNode algorithmNode)
            {
                if (e != null)
                {
                    algorithmNode.IsSelected = false;
                    if (MarkColorProperty == null)
                    {
                        Type type = typeof(STNode);
                        MarkColorProperty = type.GetProperty("TitleColor", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    }
                    // 设置值
                    if (MarkColorProperty != null)
                    {
                        MarkColorProperty.SetValue(algorithmNode, System.Drawing.Color.Green);
                    }
                }
            }
        }

        public void ErrorSign()
        {
            foreach (var item in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
            {
                if (item.IsSelected == true)
                {
                    if (MarkColorProperty == null)
                    {
                        Type type = typeof(STNode);
                        MarkColorProperty = type.GetProperty("TitleColor", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    }
                    // 设置值
                    if (MarkColorProperty != null)
                    {
                        MarkColorProperty.SetValue(item, System.Drawing.Color.Red);
                    }
                }

            }
        }



        private void UpdateMsg(object sender, FlowEngineNodeRunEventArgs e)
        {
            if (sender is CVCommonNode algorithmNode)
            {
                algorithmNode.IsSelected = true;
                Msg1 = algorithmNode.Title;

                if (FlowConfig.Instance.FlowPreviewMsg)
                {
                    UpdateMsg(sender);
                }

            }
        }


        private  void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {
            RunFlow();
        }

        public void RunFlow()
        {
            if (flowControl.IsFlowRun)
            {
                log.Info("流程正在运行");
                return;
            }
            CheckDiskSpace("C");
            CheckDiskSpace("D");

            LastFlowTime = FlowConfig.Instance.FlowRunTime.TryGetValue(ComboBoxFlow.Text, out long time) ? time : 0;
            LastCompleted = FlowConfig.Instance.FlowRunComplete.TryGetValue(ComboBoxFlow.Text, out bool completed) ? completed : false;

            string startNode = View.FlowEngineControl.GetStartNodeName();
            if (string.IsNullOrWhiteSpace(startNode))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "找不到流程启动结点，运行失败", "ColorVision");
                return;
            }
            if (MqttRCService.GetInstance().ServiceTokens.Count == 0)
            {
                MqttRCService.GetInstance().QueryServices();
                MessageBox.Show("Token为空，正在刷新token,请重试");
                return;
            }
            if (!LastCompleted)
            {
                Refresh();
            }
            else
            {
                foreach (var item in View.STNodeEditorMain.Nodes.OfType<CVBaseServerNode>())
                {
                    if (MarkColorProperty == null)
                    {
                        Type type = typeof(STNode);
                        MarkColorProperty = type.GetProperty("TitleColor", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    }
                    // 设置值
                    if (MarkColorProperty != null)
                    {
                        MarkColorProperty.SetValue(item, System.Drawing.Color.Blue);
                    }
                }

            }

            if (FlowConfig.Instance.FlowPreviewMsg)
            {
                handler = PendingBox.Show(Application.Current.MainWindow, "TTL:" + "0", "流程运行", true);
                handler.Cancelling -= Handler_Cancelling; ;
                handler.Cancelling += Handler_Cancelling; ;
            }

            flowControl.FlowCompleted += FlowControl_FlowCompleted;
            string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
            ButtonRun.Visibility = Visibility.Collapsed;
            ButtonStop.Visibility = Visibility.Visible;
            stopwatch.Restart();
            stopwatch.Start();
            timer.Change(0, 500); // 启动定时器
            BatchResultMasterDao.Instance.Save(new BatchResultMasterModel() { Name = sn, Code = sn, CreateDate = DateTime.Now });
            flowControl.Start(sn);
        }


        private void Handler_Cancelling(object? sender, CancelEventArgs e)
        {
            foreach (var item in View.STNodeEditorMain.Nodes.OfType<CVCommonNode>())
            {
                item.nodeRunEvent -= UpdateMsg;
                item.nodeEndEvent -= nodeEndEvent;
            }

            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 100); // 停止定时器
            flowControl.Stop();

            Application.Current.Dispatcher.Invoke(() =>
            {
                ButtonRun.Visibility = Visibility.Visible;
                ButtonStop.Visibility = Visibility.Collapsed;
            });
        }

        private void Button_FlowStop_Click(object sender, RoutedEventArgs e)
        {
            StopFlow();
        }

        public void StopFlow()
        {
            ButtonRun.Visibility = Visibility.Visible;
            ButtonStop.Visibility = Visibility.Collapsed;
            Application.Current.Dispatcher.Invoke(() =>
            {
                flowControl?.Stop();
            });
            handler?.Close();
        }

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked =!ToggleButton0.IsChecked;
        }

        private void Button_Click_Refresh(object sender, RoutedEventArgs e)
        {
            Refresh();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            new TemplateEditorWindow(new TemplateFlow(), ComboBoxFlow.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
            Refresh();
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            new FlowEngineToolWindow(TemplateFlow.Params[ComboBoxFlow.SelectedIndex].Value) { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
            Refresh();
        }

        public void Dispose()
        {
            timer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
