using ColorVision.Services.Devices.Spectrum.Configs;
using ColorVision.Services.Devices.Spectrum.Views;
using ColorVision.SettingUp;
using MQTTMessageLib;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static cvColorVision.GCSDLL;

namespace ColorVision.Services.Devices.Spectrum
{
    /// <summary>
    /// DisplaySpectrumControl.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySpectrumControl : UserControl
    {
        public DeviceSpectrum DeviceSpectrum { get; set; }
        public MQTTSpectrum SpectrumService { get => DeviceSpectrum.DeviceService; }

        public ViewSpectrum View { get => DeviceSpectrum.View;}
        public DisplaySpectrumControl(DeviceSpectrum DeviceSpectrum)
        {
            this.DeviceSpectrum = DeviceSpectrum;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = DeviceSpectrum;
            ViewGridManager.GetInstance().AddView(View);

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
                    ViewGridManager.GetInstance().SetViewIndex(View, KeyValue.Value);
                }
            };
            View.View.ViewIndex = -1;

            this.PreviewMouseLeftButtonUp += (s, e) =>
            {
                if (ViewConfig.GetInstance().IsAutoSelect)
                {
                    if (ViewGridManager.GetInstance().ViewMax == 1)
                    {
                        View.View.ViewIndex = 0;
                        ViewGridManager.GetInstance().SetViewIndex(View, 0);
                    }
                }
            };


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
        }
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
        public void SpectrumDrawPlot(SpectumData data)
        {
            View.SpectrumDrawPlot(data);
        }


        #region MQTT

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            string btnTitle = btn_connect.Content.ToString();
            if (!string.IsNullOrWhiteSpace(btnTitle))
            {
                if (btnTitle.Equals("打开", StringComparison.Ordinal))
                {
                    btn_connect.Content = "打开中";
                    SpectrumService.Open();
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
            //DeviceService.SetParam();
        }

        private void Button_Click_OneTest(object sender, RoutedEventArgs e)
        {
            SpectrumService.GetData((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, AutoIntTime.IsChecked??false, AutoDark.IsChecked ?? false, AutoShutterDark.IsChecked ?? false);
        }

        private void Button_Click_Close(object sender, RoutedEventArgs e)
        {
            SpectrumService.Close();
            //DeviceService.UnInit();
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
                View.SpectrumDrawPlot(new SpectumData(-1,data));
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

    }
}
