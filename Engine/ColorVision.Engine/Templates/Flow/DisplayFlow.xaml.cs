#pragma warning disable CS8602,CS8603,CS8601
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Flow;
using ColorVision.Engine.Services.RC;
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

        public static ViewFlow View => FlowEngineManager.GetInstance().View;

        public string DisPlayName => "Flow";
        public static FlowEngineConfig Config => FlowEngineConfig.Instance;

        private Timer timer;
        Stopwatch stopwatch = new Stopwatch();
        IPendingHandler handler { get; set; }

        static FlowEngineControl FlowEngineControl => FlowEngineManager.GetInstance().FlowEngineControl;

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
            this.DataContext = FlowEngineManager.GetInstance();
            this.ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = "开始执行(_S)", Command = EngineCommands.StartExecutionCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "停止执行(_S)", Command = EngineCommands.StopExecutionCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "属性", Command = Config.EditCommand });

            this.SetIconResource("DrawingImageFlow", View.View);

            this.AddViewConfig(View, ComboxView);
            View.DisplayFlow = this;

            ComboBoxFlow.SelectionChanged += (s, e) =>
            {
                if (ComboBoxFlow.SelectedValue is FlowParam flowParam)
                {
                    FlowEngineConfig.Instance.LastSelectFlow = flowParam.Id;
                    if (FlowEngineConfig.Instance.FlowRunTime.TryGetValue(flowParam.Name, out long time))
                        LastFlowTime = time;

                }
                _ = Refresh();
            };
            MqttRCService.GetInstance().ServiceTokensInitialized +=(s,e) => 
            {
                View.FlowEngineControl.LoadFromBase64(string.Empty);
                _ = Refresh();
            };

            this.ApplyChangedSelectedColor(DisPlayBorder);

            this.Loaded += FlowDisplayControl_Loaded;
            View.RefreshFlow += (s, e) =>
            {
                View.FlowEngineControl.LoadFromBase64(string.Empty);
                _=Refresh();
            };
            flowControl ??= new FlowControl(MQTTControl.GetInstance(), View.FlowEngineControl);

            timer = new Timer(UpdateMsg, null, 0, 100);
            timer.Change(Timeout.Infinite, 100); // 停止定时器

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


        public async Task Refresh()
        {
            if (MqttRCService.GetInstance().ServiceTokens.Count == 0)
                MqttRCService.GetInstance().QueryServices();
            if (ComboBoxFlow.SelectedIndex  <0 || ComboBoxFlow.SelectedIndex >= TemplateFlow.Params.Count) return;

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
                View.FlowEngineControl.LoadFromBase64(flowParam.DataBase64, MqttRCService.GetInstance().ServiceTokens);

                for (int i = 0; i < 20; i++)
                {
                    Config.IsReady = View.FlowEngineControl.IsReady;
                    if (View.FlowEngineControl.IsReady)
                        break;
                     await Task.Delay(10);
                }

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
                log.Error(ex);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
                });
                View.FlowEngineControl.LoadFromBase64(string.Empty);
            }
            return;
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }

        public FlowControl flowControl { get; set; } 


        private void FlowControl_FlowCompleted(object? sender, FlowControlData FlowControlData)
        {
            stopwatch.Stop();
            timer.Change(Timeout.Infinite, 500); // 停止定时器

            FlowEngineConfig.Instance.FlowRunTime[ComboBoxFlow.Text] = stopwatch.ElapsedMilliseconds;
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;

            FlowEngineManager.GetInstance().CurrentFlowMsg = FlowControlData;


            if (Config.IsNewMsgUI)
            {
                View.logTextBox.Text = FlowName +  Environment.NewLine+ FlowControlData.EventName;
            }
            else
            {
                handler?.Close();
            }

            ButtonRun.Visibility = Visibility.Visible;
            ButtonStop.Visibility = Visibility.Collapsed;

            if (FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
            {
                ErrorSign();
            }
            if (FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
            {
                if (Config.IsNewMsgUI)
                {
                    View.logTextBox.Text = $"{FlowName} {FlowControlData.EventName}{Environment.NewLine}节点:{Msg1}{Environment.NewLine}{FlowControlData.Params}";
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "流程计算" + FlowControlData.EventName + Environment.NewLine + FlowControlData.Params, "ColorVision");
                }
            }
        }

        public ImageSource Icon { get => _Icon; set { _Icon = value; } }
        private ImageSource _Icon;


        private long LastFlowTime;

        string Msg1;
        private void UpdateMsg(object? sender)
        {
            if (flowControl.IsFlowRun)
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
                    long remainingMilliseconds =LastFlowTime - elapsedMilliseconds;
                    TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                    string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                    msg = $"{FlowName} 上次执行：{LastFlowTime} ms{Environment.NewLine}正在执行节点:{Msg1}{Environment.NewLine}已经执行：{elapsedTime} {Environment.NewLine}预计还需要：{remainingTime}";
                }
                Application.Current?.Dispatcher.BeginInvoke(() =>
                {
                    if (flowControl.IsFlowRun)
                    {
                        if (Config.IsNewMsgUI)
                        {
                            View.logTextBox.Text = msg;
                        }
                        else
                        {
                            handler.UpdateMessage(msg);
                        }
                    }
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
                UpdateMsg(sender);
            }
        }


        private  void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {
            RunFlow();
        }
        string FlowName;
        public async void RunFlow()
        {
            if (!MqttRCService.GetInstance().IsConnect)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(),"注册中心没有连接");
                return;
            }

            if (flowControl.IsFlowRun)
            {
                log.Info("流程正在运行");
                return;
            }
            if (MqttRCService.GetInstance().ServiceTokens.Count == 0)
            {
                MqttRCService.GetInstance().QueryServices();
                View.logTextBox.Text = $"Token为空，正在刷新token,请重试";
                MessageBox.Show(Application.Current.GetActiveWindow(), "Token为空，正在刷新token,请重试");
                return;
            }
            View.logTextBox.Text = $"IsReady{View.FlowEngineControl.IsReady}";
            log.Info($"IsReady{View.FlowEngineControl.IsReady}");
            Config.IsReady = View.FlowEngineControl.IsReady;
            if (!View.FlowEngineControl.IsReady)
            {
                View.FlowEngineControl.LoadFromBase64(string.Empty);
                await Refresh();
                log.Info($"IsReady{View.FlowEngineControl.IsReady}");
                Config.IsReady = View.FlowEngineControl.IsReady;
            }
            FlowName = ComboBoxFlow.Text;
            LastFlowTime = FlowEngineConfig.Instance.FlowRunTime.TryGetValue(ComboBoxFlow.Text, out long time) ? time : 0;

            string startNode = View.FlowEngineControl.GetStartNodeName();
            if (string.IsNullOrWhiteSpace(startNode))
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "找不到流程启动结点，运行失败", "ColorVision");
                return;
            }

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

            if (Config.IsNewMsgUI)
            {
                View.logTextBox.Text = "Run " + ComboBoxFlow.Text;
            }
            else
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

            timer.Change(0, 100); // 启动定时器
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

            if (Config.IsNewMsgUI)
            {
                View.logTextBox.Text = "已经取消执行";
            }
            else
            {

                handler?.Close();
            }



        }

        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            ToggleButton0.IsChecked =!ToggleButton0.IsChecked;
        }


        public void Dispose()
        {
            timer.Dispose();
            GC.SuppressFinalize(this);
        }

        private void Button_Click_Refresh(object sender, RoutedEventArgs e)
        {
            Refresh();
        }
    }
}
