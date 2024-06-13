using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Services.DAO;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Configs;
using ColorVision.UI.Menus;
using ColorVision.UI.Views;
using ColorVision.Util.Interfaces;
using Mysqlx.Crud;
using NPOI.Util.Collections;
using Panuon.WPF.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;


namespace ColorVision.Engine.Services.Flow
{
    public class FlowDisplayControlConfig : ViewModelBase,IConfig
    {
        public static FlowDisplayControlConfig Instance =>ConfigHandler.GetInstance().GetRequiredService<FlowDisplayControlConfig>();

        public bool ForceDisableDwayneNeed { get => _ForceDisableDwayneNeed; set { _ForceDisableDwayneNeed = value; NotifyPropertyChanged(); } }
        private bool _ForceDisableDwayneNeed = true;
    }

    public class FlowDisplayControlConfigProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new List<ConfigSettingMetadata> {
                            new ConfigSettingMetadata
                            {
                                Name = "ForceDisableDwayneNeed",
                                Description = "重启生效",
                                Type = ConfigSettingType.Bool,
                                BindingName = nameof(FlowDisplayControlConfig.ForceDisableDwayneNeed),
                                Source = FlowDisplayControlConfig.Instance,
                                Order = 800
                            }
            };
        }
    }


        /// <summary>
        /// FlowDisplayControl.xaml 的交互逻辑
        /// </summary>
        public partial class FlowDisplayControl : UserControl, IDisPlayControl, IIcon
    {

        private static FlowDisplayControl _instance;
        private static readonly object _locker = new();
        public static FlowDisplayControl GetInstance() { lock (_locker) { return _instance ??= new FlowDisplayControl(); } }

        public IFlowView View { get; set; }
        public string DisPlayName => "Flow";

        public FlowDisplayControl()
        {
            InitializeComponent();
        }

        MenuItem menuItem { get; set; }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;
            FlowEngineLib.MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);

            if (FlowDisplayControlConfig.Instance.ForceDisableDwayneNeed)
            {
                View = new CVFlowView1();
            }
            else
            {
                View = new CVFlowView();
            }
            View.View.Title = $"流程窗口 ";
            this.SetIconResource("DrawingImageFlow", View.View);

            this.AddViewConfig(View, ComboxView);
            View.View.ViewIndex = 0;

            FlowTemplate.ItemsSource = FlowParam.Params;
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
                                var tokens = ServiceManager.GetInstance().ServiceTokens;
                                View.FlowEngineControl.LoadFromBase64(flowParam.DataBase64, tokens);
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
            DataContext = flowControl;
            PreviewMouseDown += UserControl_PreviewMouseDown;

            menuItem = new MenuItem() { Header = ColorVision.Engine.Properties.Resources.MenuFlow };
            MenuItem menuItem1 = new() { Header = ColorVision.Engine.Properties.Resources.ExecutionProcess };
            menuItem1.Click +=(s,e)=> Button_FlowRun_Click(s, e);
            menuItem.Items.Add(menuItem1);

            MenuItem menuItem2 = new() { Header = ColorVision.Engine.Properties.Resources.StopProcess };
            menuItem2.Click += (s, e) => Button_FlowStop_Click(s, e);
            menuItem.Items.Add(menuItem2);

            Selected += (s, e) =>
            {
                MenuManager.GetInstance().AddMenuItem(menuItem, 1);
            };
            Unselected += (s, e) =>
            {
                MenuManager.GetInstance().RemoveMenuItem(menuItem);
            };
            this.ApplyChangedSelectedColor(DisPlayBorder);
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


        private void UserControl_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (Parent is StackPanel stackPanel)
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
            handler?.Close();
            if (sender is FlowControlData FlowControlData)
            {
                ButtonRun.Visibility = Visibility.Visible;
                ButtonStop.Visibility = Visibility.Collapsed;

                if (FlowControlData.EventName == "Completed" || FlowControlData.EventName == "Canceled" || FlowControlData.EventName == "OverTime" || FlowControlData.EventName == "Failed")
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "流程计算" + FlowControlData.EventName, "ColorVision");
                    });
                }
            }
        }

        IPendingHandler handler { get; set; }
        public ImageSource Icon { get => _Icon; set { _Icon = value; } }
        private ImageSource _Icon;


        private  void Button_FlowRun_Click(object sender, RoutedEventArgs e)
        {
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
            FlowTemplate.SelectedIndex = -1;
            FlowTemplate.ItemsSource = FlowParam.Params;
            FlowTemplate.SelectedIndex = 0;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            new WindowTemplate(new TemplateFlow(), FlowTemplate.SelectedIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }
}
