using ColorVision.MQTT;
using ColorVision.Templates;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using Panuon.WPF.UI;
using ColorVision.Services;
using ColorVision.Themes;
using System.Windows.Media;
using ColorVision.Solution;
using ColorVision.Flow.Templates;

namespace ColorVision.Flow
{
    /// <summary>
    /// FlowDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class FlowDisplayControl : UserControl
    {
        public IFlowView View { get; set; }

        public FlowDisplayControl()
        {
            InitializeComponent();
        }

        public GlobalSetting GlobalSetting { get; set; }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            GlobalSetting = GlobalSetting.GetInstance();
            MQTTConfig mQTTConfig = GlobalSetting.SoftwareConfig.MQTTConfig;
            FlowEngineLib.MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);

            using System.Drawing.Graphics graphics = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            bool result = graphics.DpiX > 96;
            if (result)
            {
                View = new CVFlowView1();
            }
            else
            {
                View = new CVFlowView();

            }

            if (Application.Current.TryFindResource("DrawingImageFlow") is DrawingImage DrawingImageAlgorithm)
                View.View.Icon = DrawingImageAlgorithm;

            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                if (Application.Current.TryFindResource("DrawingImageFlow") is DrawingImage DrawingImageAlgorithm)
                    View.View.Icon = DrawingImageAlgorithm;
            };
            View.View.Title = "流程窗口";
            if (View is UserControl control)
            {
                ViewGridManager.GetInstance().AddView(0, control);

                ViewMaxChangedEvent(ViewGridManager.GetInstance().ViewMax);
                ViewGridManager.GetInstance().ViewMaxChangedEvent += ViewMaxChangedEvent;

                void ViewMaxChangedEvent(int max)
                {
                    List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                    KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowSingle, -2));
                    KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowHidden, -1));
                    for (int i = 0; i < max; i++)
                    {
                        KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                    }
                    ComboxView.ItemsSource = KeyValues;
                    ComboxView.SelectedValue = View.View.ViewIndex;
                }
                View.View.ViewIndexChangedEvent += (e1, e2) =>
                {
                    ComboxView.SelectedIndex = e2 + 2;
                };
                ComboxView.SelectionChanged += (s, e) =>
                {
                    if (ComboxView.SelectedItem is KeyValuePair<string, int> KeyValue)
                    {
                        View.View.ViewIndex = KeyValue.Value;
                        ViewGridManager.GetInstance().SetViewIndex(control, KeyValue.Value);
                    }
                };
            }

            FlowTemplate.ItemsSource = TemplateControl.GetInstance().FlowParams;
            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (FlowTemplate.SelectedValue is FlowParam flowParam)
                {
                    string fileName = SolutionManager.GetInstance().CurrentSolution.FullName + "\\Flow\\" + flowParam.FileName;
                    if (File.Exists(fileName))
                    {
                        if (View != null)
                        {
                            try
                            {
                                View.FlowEngineControl.Load(fileName);
                            }
                            catch
                            {

                            }
                        }
                    }
                }
            };
            FlowTemplate.SelectedIndex = 0;

        }


        private FlowControl flowControl;

        private void FlowControl_FlowCompleted(object? sender, EventArgs e)
        {
            flowControl.FlowCompleted -= FlowControl_FlowCompleted;
            if (sender != null)
            {
                FlowControlData FlowControlData = (FlowControlData)sender;
                ServiceManager.GetInstance().ProcResult(FlowControlData);
            }
            handler?.Close();
            if (sender != null)
            {
                FlowControlData FlowControlData = (FlowControlData)sender;
                if (FlowControlData.EventName == "Completed" || FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
                {
                    MessageBox.Show("流程计算" + FlowControlData.EventName, "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                }
            }
        }

        IPendingHandler handler { get; set; }

        private  void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {
            if (FlowTemplate.SelectedValue is FlowParam flowParam)
            {
                string startNode = View.FlowEngineControl.GetStartNodeName();
                if (!string.IsNullOrWhiteSpace(startNode))
                {
                    flowControl = new FlowControl(MQTTControl.GetInstance(), View.FlowEngineControl);

                    handler = PendingBox.Show(Application.Current.MainWindow, "TTL:" + "0", "流程运行", true);
                    handler.Cancelling += delegate
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            flowControl?.Stop();
                        });
                        handler?.Close();
                    };

                    flowControl.FlowData += (s, e) =>
                    {
                        if (s is FlowControlData msg)
                        {
                            try
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    handler?.UpdateMessage("TTL: " + msg.Params.TTL.ToString());
                                });
                            }
                            catch 
                            {

                            }
                        }
                    };
                    flowControl.FlowCompleted += FlowControl_FlowCompleted;
                    string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                    ServiceManager.GetInstance().ResultBatchSave(sn);
                    flowControl.Start(sn);
                }
                else
                {
                    MessageBox.Show(Application.Current.MainWindow, "找不到完整流程，运行失败");
                }
            }
        }

        private void Button_FlowStop_Click(object sender, RoutedEventArgs e)
        {
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
    }
}
