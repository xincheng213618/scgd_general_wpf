using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.Devices.Spectrum.Views;
using ColorVision.UI;
using CVCommCore;
using System;
using System.Windows;
using System.Windows.Controls;
using static cvColorVision.GCSDLL;


namespace ColorVision.Engine.Services.Devices.Spectrum
{
    /// <summary>
    /// DisplaySpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class DisplaySpectrum : UserControl, IDisPlayControl
    {
        public DeviceSpectrum DeviceSpectrum { get; set; }
        public MQTTSpectrum SpectrumService { get => DeviceSpectrum.DService; }

        public ViewSpectrum View { get => DeviceSpectrum.View;}

        public string DisPlayName => DeviceSpectrum.Config.Name;

        public DisplaySpectrum(DeviceSpectrum DeviceSpectrum)
        {
            this.DeviceSpectrum = DeviceSpectrum;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            DataContext = DeviceSpectrum;

            this.AddViewConfig(View,ComboxView);

            SpectrumService.DataHandlerEvent += (s,e) =>
            {
                if (e != null)
                    View.SpectrumDrawPlot(e);
            };

            void UpdateUI(DeviceStatusType status)
            {
                void SetVisibility(UIElement element, Visibility visibility) { if (element.Visibility != visibility) element.Visibility = visibility; };

                void HideAllButtons()
                {
                    SetVisibility(ButtonUnauthorized, Visibility.Collapsed);
                    SetVisibility(TextBlockUnknow, Visibility.Collapsed);
                    SetVisibility(StackPanelContent, Visibility.Collapsed);
                    SetVisibility(StackPanelOpen, Visibility.Collapsed);
                    SetVisibility(TextBlockOffLine, Visibility.Collapsed);
                }

                HideAllButtons();
                btn_autoTest.Content = "自动测试";
                switch (status)
                {

                    case DeviceStatusType.Unknown:
                        SetVisibility(TextBlockUnknow, Visibility.Visible);
                        break;
                    case DeviceStatusType.Unauthorized:
                        SetVisibility(ButtonUnauthorized, Visibility.Visible);
                        break;
                    case DeviceStatusType.OffLine:
                        SetVisibility(TextBlockOffLine, Visibility.Visible);
                        break;
                    case DeviceStatusType.UnInit:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        btn_connect.Content = "打开";
                        break;
                    case DeviceStatusType.Closed:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        btn_connect.Content = "打开";
                        break;
                    case DeviceStatusType.Opened:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        SetVisibility(StackPanelOpen, Visibility.Visible);
                        btn_connect.Content = "关闭";
                        break;
                    case DeviceStatusType.SP_Continuous_Mode:
                        SetVisibility(StackPanelContent, Visibility.Visible);
                        SetVisibility(StackPanelOpen, Visibility.Visible);
                        btn_autoTest.Content = "取消自动测试";
                        break;
                    default:
                        break;
                }
            }

            UpdateUI(SpectrumService.DeviceStatus);
            SpectrumService.DeviceStatusChanged += UpdateUI;

            this.ApplyChangedSelectedColor(DisPlayBorder);
        }

        public event RoutedEventHandler Selected;
        public event RoutedEventHandler Unselected;
        public event EventHandler SelectChanged;
        private bool _IsSelected;
        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; SelectChanged?.Invoke(this, new RoutedEventArgs()); if (value) Selected?.Invoke(this, new RoutedEventArgs()); else Unselected?.Invoke(this, new RoutedEventArgs()); } }


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
        private void Button_Click_OneTest(object sender, RoutedEventArgs e)
        {
            SpectrumService.GetData((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, AutoIntTime.IsChecked??false, AutoDark.IsChecked ?? false, AutoShutterDark.IsChecked ?? false);
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
            MsgRecord  msgRecord = SpectrumService.InitDark((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value);
            msgRecord.MsgRecordStateChanged += (e) =>
            {
                if (e == MsgRecordState.Success)
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "执行结束", "ColorVision");
                }
                else
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "执行失败", "ColorVision");
                }
            };
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
    }
}
