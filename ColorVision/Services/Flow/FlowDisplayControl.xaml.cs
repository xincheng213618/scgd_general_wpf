using ColorVision.Common.Utilities;
using ColorVision.Extension;
using ColorVision.MQTT;
using ColorVision.Services.Core;
using ColorVision.Services.Devices;
using ColorVision.Services.Extension;
using ColorVision.Services.Templates;
using ColorVision.Settings;
using ColorVision.Themes;
using Panuon.WPF.UI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.Services.Flow
{
    /// <summary>
    /// FlowDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class FlowDisplayControl : UserControl, IDisPlayControl, IIcon
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
            View = graphics.DpiX > 96 ? new CVFlowView1() : new CVFlowView();

            View.View.Title = $"流程窗口 ";
            this.SetIconResource("DrawingImageFlow", View.View);

            this.AddViewConfig(View, ComboxView);
            View.View.ViewIndex = 0;

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
                                View.FlowEngineControl.LoadFromBase64(flowParam.DataBase64, ServiceManager.GetInstance().ServiceTokens);
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
            this.DataContext = flowControl;
            this.PreviewMouseDown += UserControl_PreviewMouseDown;

            menuItem = new MenuItem() { Header = ColorVision.Properties.Resource.MenuFlow };
            MenuItem menuItem1 = new MenuItem() { Header = ColorVision.Properties.Resource.ExecutionProcess };
            menuItem1.Click +=(s,e)=> Button_FlowRun_Click(s, e);
            menuItem.Items.Add(menuItem1);

            MenuItem menuItem2 = new MenuItem() { Header = ColorVision.Properties.Resource.StopProcess };
            menuItem2.Click += (s, e) => Button_FlowStop_Click(s, e);
            menuItem.Items.Add(menuItem2);
        }
        MenuItem menuItem { get; set; }

        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected;
            set 
            { 
                _IsSelected = value; 
                if (value)
                {
                    MenuManager.GetInstance().AddMenuItem(menuItem,1);
                }
                else
                {
                    MenuManager.GetInstance().RemoveMenuItem(menuItem);
                }
                DisPlayBorder.BorderBrush = value ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");
            }
        }

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

                ButtonRun.Visibility = Visibility.Visible;
                ButtonStop.Visibility = Visibility.Collapsed;

                if (FlowControlData.EventName == "Completed" || FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
                {
                    MessageBox.Show("流程计算" + FlowControlData.EventName, "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                }
            }
        }

        IPendingHandler handler { get; set; }
        public ImageSource Icon { get => _Icon; set { _Icon = value; } }
        private ImageSource _Icon;

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
                            ButtonRun.Visibility = Visibility.Visible;
                            ButtonStop.Visibility = Visibility.Collapsed;
                        });

                        flowControl?.Stop();
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
                    ButtonRun.Visibility = Visibility.Collapsed;
                    ButtonStop.Visibility = Visibility.Visible;
                    flowControl.Start(sn);
                }
                else
                {
                    MessageBox.Show(WindowHelpers.GetActiveWindow(), "找不到完整流程，运行失败","ColorVision");
                }
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
            TemplateControl.GetInstance().LoadFlowParam();
            FlowTemplate.ItemsSource = TemplateControl.GetInstance().FlowParams;
        }
        FlowControl rcflowControl;
        private void Button_RCFlowRun_Click(object sender, RoutedEventArgs e)
        {
            if (FlowTemplate.SelectedItem is TemplateModel<FlowParam> flowParam)
            {
                rcflowControl ??= new FlowControl(MQTTControl.GetInstance(), "");
                string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                ServiceManager.GetInstance().ResultBatchSave(sn);
                rcflowControl.Start(sn, flowParam.Value);

                ButtonRun.Visibility = Visibility.Collapsed;
                ButtonStop.Visibility = Visibility.Visible;
            }
            else
            {
                MessageBox.Show(WindowHelpers.GetActiveWindow(),"没有选择流程","ColorVision");
            }
        }
    }
}
