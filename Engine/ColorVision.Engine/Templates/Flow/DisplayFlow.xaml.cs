﻿using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services;
using ColorVision.Engine.Services.DAO;
using ColorVision.Engine.Services.Flow;
using ColorVision.UI;
using FlowEngineLib;
using FlowEngineLib.Base;
using Panuon.WPF.UI;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Templates.Flow
{

    public class DisplayFlowConfig : ViewModelBase, IConfig
    {
        public static DisplayFlowConfig Instance => ConfigService.Instance.GetRequiredService<DisplayFlowConfig>();

        public int LastSelectFlow { get => _LastSelectFlow; set { _LastSelectFlow = value; NotifyPropertyChanged(); } }
        private int _LastSelectFlow;
        public long LastFlowTime { get => _LastFlowTime; set { _LastFlowTime = value; NotifyPropertyChanged(); } }
        private long _LastFlowTime;

    }

    public partial class DisplayFlow : UserControl, IDisPlayControl, IIcon, IDisposable
    {
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
        }


        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            FlowEngineLib.MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);

            View = new ViewFlow();

            View.View.Title = $"流程窗口 ";
            this.SetIconResource("DrawingImageFlow", View.View);

            this.AddViewConfig(View, ComboxView);

            ComboBoxFlow.ItemsSource = FlowParam.Params;
            ComboBoxFlow.SelectionChanged += (s, e) =>
            {
                if (ComboBoxFlow.SelectedValue is FlowParam flowParam)
                    DisplayFlowConfig.Instance.LastSelectFlow = flowParam.Id;
                FlowUpdate();
            };


            this.ApplyChangedSelectedColor(DisPlayBorder);


            timer = new Timer(UpdateMsg, null, 0, 100);
            timer.Change(Timeout.Infinite, 100); // 停止定时器

            this.Loaded += FlowDisplayControl_Loaded;
        }

        private void FlowDisplayControl_Loaded(object sender, RoutedEventArgs e)
        {
            var s = FlowParam.Params.FirstOrDefault(a => a.Id == DisplayFlowConfig.Instance.LastSelectFlow);
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

        private void FlowUpdate()
        {
            if (ComboBoxFlow.SelectedValue is FlowParam flowParam)
            {
                if (View != null)
                {
                    try
                    {
                        if (string.IsNullOrEmpty(flowParam.DataBase64))
                        {
                            MessageBox.Show("再选择之前请先创建对映的模板");
                        }
                        else
                        {
                            var tokens = ServiceManager.GetInstance().ServiceTokens;

                            foreach (var item in View.STNodeEditorMain.Nodes)
                            {
                                if (item is CVCommonNode algorithmNode)
                                {
                                    algorithmNode.nodeRunEvent -= UpdateMsg;
                                }
                            }
                            View.FlowEngineControl.LoadFromBase64(FlowParam.Params[ComboBoxFlow.SelectedIndex].Value.DataBase64, tokens);
                            foreach (var item in View.STNodeEditorMain.Nodes)
                            {
                                if (item is CVCommonNode algorithmNode)
                                {
                                    algorithmNode.nodeRunEvent += UpdateMsg;
                                }
                            }
                            if(Config.IsAutoSize)
                                View.AutoSize();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
            }
            else
            {
                View.FlowEngineControl.LoadFromBase64(string.Empty);
            }
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }

        private FlowControl flowControl;

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            handler?.Close();
            if (sender is FlowControlData FlowControlData)
            {
                ButtonRun.Visibility = Visibility.Visible;
                ButtonStop.Visibility = Visibility.Collapsed;

                if (FlowControlData.EventName == "Completed" || FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
                {
                    stopwatch.Stop();
                    timer.Change(Timeout.Infinite, 100); // 停止定时器
                    DisplayFlowConfig.Instance.LastFlowTime = stopwatch.ElapsedMilliseconds;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "流程计算" + FlowControlData.EventName, "ColorVision");
                    });
                }
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

        string Msg1;
        private void UpdateMsg(object? sender)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (handler != null)
                    {
                        long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
                        TimeSpan elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);
                        string elapsedTime = $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}:{elapsed.Milliseconds:D4}";
                        string msg;
                        if (DisplayFlowConfig.Instance.LastFlowTime == 0 ||  DisplayFlowConfig.Instance.LastFlowTime - elapsedMilliseconds <0)
                        {
                             msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}";
                        }
                        else
                        {
                            long remainingMilliseconds = DisplayFlowConfig.Instance.LastFlowTime - elapsedMilliseconds;
                            TimeSpan remaining = TimeSpan.FromMilliseconds(remainingMilliseconds);
                            string remainingTime = $"{remaining.Minutes:D2}:{remaining.Seconds:D2}:{elapsed.Milliseconds:D4}";

                            msg = Msg1 + Environment.NewLine + $"已经执行：{elapsedTime}, 上次执行：{DisplayFlowConfig.Instance.LastFlowTime} ms, 预计还需要：{remainingTime}";
                        }
                        handler.UpdateMessage(msg);
                    }
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


        private  void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {

            CheckDiskSpace("C");
            CheckDiskSpace("D");
            string startNode = View.FlowEngineControl.GetStartNodeName();
            if (!string.IsNullOrWhiteSpace(startNode))
            {
                flowControl ??= new FlowControl(MQTTControl.GetInstance(), View.FlowEngineControl);

                handler = PendingBox.Show(Application.Current.MainWindow, "TTL:" + "0", "流程运行", true);

                handler.Cancelling += Handler_Cancelling; ;
               
                flowControl.FlowData += (s, e) =>
                {
                    if (s is FlowControlData msg)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            handler?.UpdateMessage("TTL: " + msg.Params.TTL.ToString());
                        });
                    }
                };



                flowControl.FlowCompleted += FlowControl_FlowCompleted;
                string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                ButtonRun.Visibility = Visibility.Collapsed;
                ButtonStop.Visibility = Visibility.Visible;
                stopwatch.Restart();
                stopwatch.Start();
                timer.Change(0, 100); // 启动定时器
                flowControl.Start(sn);
                string name = string.Empty;
                if (IsName.IsChecked.HasValue && IsName.IsChecked.Value) { name = TextBoxName.Text; }
                BeginNewBatch(sn, name);
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(), "找不到完整流程，运行失败", "ColorVision");
            }
        }



        public static void BeginNewBatch(string sn, string name)
        {
            BatchResultMasterModel batch = new();
            batch.Name = string.IsNullOrEmpty(name) ? sn : name;
            batch.Code = sn;
            batch.CreateDate = DateTime.Now;
            batch.TenantId = 0;
            BatchResultMasterDao.Instance.Save(batch);
        }



        private void Handler_Cancelling(object? sender, CancelEventArgs e)
        {
            if (sender is IPendingHandler pendingHandler)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ButtonRun.Visibility = Visibility.Visible;
                    ButtonStop.Visibility = Visibility.Collapsed;
                });

                flowControl?.Stop();

                pendingHandler.Cancelling -= Handler_Cancelling;
                pendingHandler?.Close();
            }
        }

        private void Button_FlowStop_Click(object sender, RoutedEventArgs e)
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
            FlowUpdate();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            new TemplateEditorWindow(new TemplateFlow(), ComboBoxFlow.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
            FlowUpdate();
        }

        private void ButtonEdit_Click(object sender, RoutedEventArgs e)
        {
            new FlowEngineToolWindow(FlowParam.Params[ComboBoxFlow.SelectedIndex].Value) { Owner = Application.Current.GetActiveWindow() }.ShowDialog();
            FlowUpdate();
        }

        public void Dispose()
        {
            timer.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
