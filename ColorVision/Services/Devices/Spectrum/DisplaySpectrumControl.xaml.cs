using ColorVision.Common.Utilities;
using ColorVision.MySql;
using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.Services.Devices.Spectrum.Views;
using ColorVision.Services.Templates;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Views;
using CVCommCore;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static cvColorVision.GCSDLL;


namespace ColorVision.Services.Devices.Spectrum
{
    /// <summary>
    /// DisplaySpectrumControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySpectrumControl : UserControl, IDisPlayControl
    {
        public DeviceSpectrum DeviceSpectrum { get; set; }
        public MQTTSpectrum SpectrumService { get => DeviceSpectrum.DeviceService; }

        public ViewSpectrum View { get => DeviceSpectrum.View;}

        public string DisPlayName => DeviceSpectrum.Config.Name;

        public DisplaySpectrumControl(DeviceSpectrum DeviceSpectrum)
        {
            this.DeviceSpectrum = DeviceSpectrum;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = DeviceSpectrum;

            this.AddViewConfig(View,ComboxView);

            SpectrumService.DataHandlerEvent += e =>
            {
                if (e != null)
                    View.SpectrumDrawPlot(e);
            };

            SpectrumService.HeartbeatEvent += e =>
            {
                doHeartbeat(e);
            };

            SpectrumService.HeartbeatHandlerEvent += (e) =>
            {
                doSpectrumHeartbeat(e);
            };


            SpectrumResourceParams = new ObservableCollection<TemplateModel<SpectrumResourceParam>>();
            SpectrumResourceParams.Insert(0, new TemplateModel<SpectrumResourceParam>("Empty", new SpectrumResourceParam() { Id = -1 }));

            foreach (var item in DeviceSpectrum.SpectrumResourceParams)
                SpectrumResourceParams.Add(item);

            DeviceSpectrum.SpectrumResourceParams.CollectionChanged += (s, e) =>
            {
                switch (e.Action)
                {
                    case NotifyCollectionChangedAction.Add:
                        // 处理添加项
                        if (e.NewItems != null)
                            foreach (TemplateModel<SpectrumResourceParam> newItem in e.NewItems)
                                SpectrumResourceParams.Add(newItem);
                        break;
                    case NotifyCollectionChangedAction.Remove:
                        // 处理移除项
                        if (e.OldItems != null)
                            foreach (TemplateModel<SpectrumResourceParam> newItem in e.OldItems)
                                SpectrumResourceParams.Remove(newItem);
                        break;
                    case NotifyCollectionChangedAction.Replace:
                        // 处理替换项
                        // ...
                        break;
                    case NotifyCollectionChangedAction.Move:
                        // 处理移动项
                        // ...
                        break;
                    case NotifyCollectionChangedAction.Reset:
                        // 处理清空集合
                        SpectrumResourceParams.Clear();
                        SpectrumResourceParams.Insert(0, new TemplateModel<SpectrumResourceParam>("Empty", new SpectrumResourceParam()) { Id = -1 });
                        break;
                }
            };

            ComboxResourceTemplate.ItemsSource = SpectrumResourceParams;
            ComboxResourceTemplate.SelectedIndex = 0;

            PreviewMouseDown += UserControl_PreviewMouseDown;
            SelectChanged += (s, e) =>
            {
                DisPlayBorder.BorderBrush = IsSelected ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");
            };
            ThemeManager.Current.CurrentUIThemeChanged += (s) =>
            {
                DisPlayBorder.BorderBrush = IsSelected ? ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#5649B0" : "#A79CF1") : ImageUtil.ConvertFromString(ThemeManager.Current.CurrentUITheme == Theme.Light ? "#EAEAEA" : "#151515");
            };
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


        public ObservableCollection<TemplateModel<SpectrumResourceParam>> SpectrumResourceParams { get; set; }

        private void doHeartbeat(HeartbeatParam e)
        {
            SpectrumService.Config.DeviceStatus = e.DeviceStatus;
            if (e.DeviceStatus == DeviceStatusType.Opened)
            {
                btn_connect.Content = "关闭";
            }
            else if (e.DeviceStatus == DeviceStatusType.Closed)
            {
                btn_connect.Content = "打开";
            }
            else if (e.DeviceStatus == DeviceStatusType.Opening)
            {
                btn_connect.Content = "打开中";
            }
            else if (e.DeviceStatus == DeviceStatusType.Closing)
            {
                btn_connect.Content = "关闭中";
            }
            else if (e.DeviceStatus == DeviceStatusType.Busy)
            {
                enableBtn(false);
            }
            else if (e.DeviceStatus == DeviceStatusType.Free)
            {
                enableBtn(true);
            }
        }
        private void doSpectrumHeartbeat(SpectrumHeartbeatParam e)
        {
            doHeartbeat(e);
            if (e.IsAutoGetData)
            {
                btn_autoTest.Content = "取消自动测试";
            }
            else
            {
                btn_autoTest.Content = "自动测试";
            }
        }

        private void enableBtn(bool enable)
        {
            btn_connect.IsEnabled = enable;
            btn_autoTest.IsEnabled = enable;
            btn_oneTest.IsEnabled = enable;
            btn_oneInitDark.IsEnabled = enable;
            //btn_getPatam.IsEnabled = enable;
        }

        public void SpectrumClear()
        {
            View.Clear();
        }
        public void SpectrumDrawPlot(SpectrumData data)
        {
            View.SpectrumDrawPlot(data);
        }


        #region MQTT

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            string btnTitle = btn_connect.Content.ToString();
            if (!string.IsNullOrWhiteSpace(btnTitle))
            {
                if (!btnTitle.Equals("关闭", StringComparison.Ordinal))
                {
                     if (ComboxResourceTemplate.SelectedValue is SpectrumResourceParam param)
                    {
                        SpectrumService.Open(param);
                    }
                    else
                    {
                        MessageBox.Show("请先选择校正文件");
                    }
                }
                else
                {
                    btn_connect.Content = "关闭中";
                    SpectrumService.Close();
                }
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            //MQTTFileServer.SetParam();
        }

        private void Button_Click_OneTest(object sender, RoutedEventArgs e)
        {
            SpectrumService.GetData((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, AutoIntTime.IsChecked??false, AutoDark.IsChecked ?? false, AutoShutterDark.IsChecked ?? false);
        }

        private void Button_Click_Close(object sender, RoutedEventArgs e)
        {
            SpectrumService.Close();
            //MQTTFileServer.UnInit();
        }
        private void Button_Click_AutoTest(object sender, RoutedEventArgs e)
        {
            string btnTitle = btn_autoTest.Content.ToString();
            if (!string.IsNullOrWhiteSpace(btnTitle) && btnTitle.Equals("自动测试", StringComparison.Ordinal))
            {
                SpectrumService.GetDataAuto((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, AutoIntTime.IsChecked ?? false, AutoDark.IsChecked ?? false);
                btn_autoTest.Content = "取消自动测试";
            }
            else
            {
                SpectrumService.GetDataAutoStop();
                btn_autoTest.Content = "自动测试";
            }
        }
        private void Button_Click_Init_Dark(object sender, RoutedEventArgs e)
        {
            SpectrumService.InitDark((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value);
        }
        private void Button_Click_GetParam(object sender, RoutedEventArgs e)
        {
            SpectrumService.GetParam();
        }
        #endregion
        #region Spectrum

        private ColorParamReturn SpectrumData;

        private bool ConnectSpectrum()
        {
            if (JKStartServer() != 0)
            {
                MessageBox.Show("启动光谱仪软件的服务失败");
                return false;
            }
            if (CVInit() != 0)
            {
                MessageBox.Show("连接光谱仪失败");
                return false;
            }
            SpectrumData += TestResult;
            return true;
        }
        private static bool DisconnectSpectrum()
        {
            if (JKEmissionClose() != 0)
            {
                MessageBox.Show("断开光谱仪失败");
                return false;
            }
            if (JKCloseServer() != 0)
            {
                MessageBox.Show("断开光谱仪软件的服务失败");
                return false;
            }
            return true;
        }



        private async void SpectrumIni(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.Content.ToString() == "连接光谱仪")
                {
                    if (await Task.Run(() => ConnectSpectrum()))
                        button.Content = "断开光谱仪";
                }
                else
                {
                    if (await Task.Run(() => DisconnectSpectrum()))
                        button.Content = "连接光谱仪";
                }
            }
        }
        private void Spectrum0(object sender, RoutedEventArgs e)
        {
            float fIntTime = 100;
            int iAveNum = 1;
            if (CVInitDark(fIntTime, iAveNum) == 0)
                MessageBox.Show("校零成功");
            else
                MessageBox.Show("校零失败");
        }
        private void SpectrumSingleTest(object sender, RoutedEventArgs e)
        {
            CVOneTest(SpectrumData, (float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, false, false);
        }

        public void TestResult(ref ColorParam data, float intTime, int resultCode)
        {
            if (resultCode == 0)
            {
                View.SpectrumDrawPlot(new SpectrumData(-1,data));
            }
            else
            {
                MessageBox.Show("测量失败");
            }
        }

        #endregion

        private void Button_Click_Shutter_Connect(object sender, RoutedEventArgs e)
        {
            SpectrumService.ShutterConnect();
        }

        private void Button_Click_Shutter_Doopen(object sender, RoutedEventArgs e)
        {
            SpectrumService.ShutterDoopen();
        }

        private void Button_Click_Shutter_Doclose(object sender, RoutedEventArgs e)
        {
            SpectrumService.ShutterDoclose();
        }

        private void MenuItem_Template(object sender, RoutedEventArgs e)
        {
            if (sender is Control menuItem)
            {
                WindowTemplate windowTemplate;
                if (MySqlSetting.Instance.IsUseMySql && !MySqlSetting.IsConnect)
                {
                    MessageBox.Show("数据库连接失败，请先连接数据库在操作", "ColorVision");
                    return;
                }
                switch (menuItem.Tag?.ToString() ?? string.Empty)
                {
                    case "SpectrumResourceParam":
                        SpectrumResourceControl calibration = DeviceSpectrum.SpectrumResourceParams.Count == 0 ? new SpectrumResourceControl(DeviceSpectrum) : new SpectrumResourceControl(DeviceSpectrum, DeviceSpectrum.SpectrumResourceParams[0].Value);
                        var  ITemplate = new TemplateSpectrumResourceParam() { Device = DeviceSpectrum, TemplateParams = DeviceSpectrum.SpectrumResourceParams, SpectrumResourceControl = calibration, Title = "SpectrumResourceParams" };


                        windowTemplate = new WindowTemplate(ITemplate, false);
                        windowTemplate.Owner = Window.GetWindow(this);
                        windowTemplate.ShowDialog();
                        break;
                }
            }
        }
    }
}
