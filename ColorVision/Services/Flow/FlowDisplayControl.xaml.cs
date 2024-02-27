using ColorVision.MQTT;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using Panuon.WPF.UI;
using ColorVision.Themes;
using System.Windows.Media;
using ColorVision.Solution;
using ColorVision.Services.Flow;
using ColorVision.Settings;
using ColorVision.Common.Utilities;
using ColorVision.Services.Interfaces;
using System.Windows.Input;
using ColorVision.Services.Templates;

namespace ColorVision.Services.Flow
{
    /// <summary>
    /// FlowDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class FlowDisplayControl : UserControl, IDisPlayControl
    {
        public IFlowView View { get; set; }

        public FlowDisplayControl()
        {
            InitializeComponent();
        }

        public ConfigHandler ConfigHandler { get; set; }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ConfigHandler = ConfigHandler.GetInstance();
            MQTTConfig mQTTConfig = ConfigHandler.SoftwareConfig.MQTTConfig;
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


                this.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    if (ViewConfig.GetInstance().IsAutoSelect)
                    {
                        if (ViewGridManager.GetInstance().ViewMax == 1)
                        {
                            View.View.ViewIndex = 0;
                            ViewGridManager.GetInstance().SetViewIndex(control, 0);
                        }
                    }
                };

            }

            FlowTemplate.ItemsSource = TemplateControl.GetInstance().FlowParams;
            FlowTemplate.SelectionChanged += (s, e) =>
            {
                if (FlowTemplate.SelectedValue is FlowParam flowParam)
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
                                View.FlowEngineControl.LoadFromBase64(flowParam.DataBase64);
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
            };
            FlowTemplate.SelectedIndex = 0;

            this.PreviewMouseDown += UserControl_PreviewMouseDown;
        }

        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; DisPlayBorder.BorderBrush = value ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");  } }
        private bool _IsSelected;

        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (this.Parent is StackPanel stackPanel)
            {
                if (stackPanel.Tag is IDisPlayControl disPlayControl)
                    disPlayControl.IsSelected = false;
                stackPanel.Tag = this;
                IsSelected = true;
            }
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
                            handler?.Close();
                        });
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

        private void Button_Click_Refresh(object sender, RoutedEventArgs e)
        {
            TemplateControl.GetInstance().LoadFlowParam();
            FlowTemplate.ItemsSource = TemplateControl.GetInstance().FlowParams;
        }
    }
}
