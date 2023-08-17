using ColorVision.MQTT.Spectrum;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static cvColorVision.GCSDLL;

namespace ColorVision.MQTT.Spectrum
{
    /// <summary>
    /// MQTTSpectrumControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTSpectrumControl : UserControl
    {

        public DeviceSpectrum DeviceSpectrum { get; set; }
        public SpectrumService SpectrumService { get => DeviceSpectrum.SpectrumService; }

        public ChartView View { get => DeviceSpectrum.ChartView;}
        public MQTTSpectrumControl(DeviceSpectrum DeviceSpectrum)
        {
            this.DeviceSpectrum = DeviceSpectrum;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = SpectrumService;
            ViewGridManager.GetInstance().AddView(View);
            SpectrumService.DataHandlerEvent += e =>
            {
                if (e != null)
                    SpectrumDrawPlot(e);
            };

            SpectrumService.HeartbeatHandlerEvent += (e) =>
            {
                if (e.DeviceStatus == DeviceStatus.Open)
                {
                    connectBtn.Content = "关闭";
                }
                else if(e.DeviceStatus == DeviceStatus.Close)
                {
                    connectBtn.Content = "打开";
                }
                else if (e.DeviceStatus == DeviceStatus.Opening)
                {
                    connectBtn.Content = "打开中";
                }
                else if (e.DeviceStatus == DeviceStatus.Closing)
                {
                    connectBtn.Content = "关闭中";
                }

                if (e.IsAutoGetData)
                {
                    autoTest.Content = "取消自动测试";
                }
                else
                {
                    autoTest.Content = "自动测试";
                }
            };
        }

        public void SpectrumDrawPlot(SpectumData data)
        {
            View.SpectrumDrawPlot(data);
        }


        #region MQTT
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            SpectrumService.Init();
        }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            string btnTitle = connectBtn.Content.ToString();
            if (!string.IsNullOrWhiteSpace(btnTitle))
            {
                if (btnTitle.Equals("打开", StringComparison.Ordinal))
                {
                    connectBtn.Content = "打开中";
                    SpectrumService.Open();
                }
                else
                {
                    connectBtn.Content = "关闭中";
                    SpectrumService.Close();
                }
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            //SpectrumService.SetParam();
        }

        private void Button_Click_OneTest(object sender, RoutedEventArgs e)
        {
            SpectrumService.GetData((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, AutoIntTime.IsChecked??false, AutoDark.IsChecked ?? false);
        }

        private void Button_Click_Close(object sender, RoutedEventArgs e)
        {
            SpectrumService.Close();
            //SpectrumService.UnInit();
        }
        private void Button_Click_AutoTest(object sender, RoutedEventArgs e)
        {
            string btnTitle = autoTest.Content.ToString();
            if (!string.IsNullOrWhiteSpace(btnTitle) && btnTitle.Equals("自动测试", StringComparison.Ordinal))
            {
                SpectrumService.GetDataAuto((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, AutoIntTime.IsChecked ?? false, AutoDark.IsChecked ?? false);
                autoTest.Content = "取消自动测试";
            }
            else
            {
                SpectrumService.GetDataAutoStop();
                autoTest.Content = "自动测试";
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

        private GCSDLL.ColorParamReturn SpectrumData;

        private bool ConnectSpectrum()
        {
            if (GCSDLL.JKStartServer() != 0)
            {
                MessageBox.Show("启动光谱仪软件的服务失败");
                return false;
            }
            if (GCSDLL.CVInit() != 0)
            {
                MessageBox.Show("连接光谱仪失败");
                return false;
            }
            SpectrumData += TestResult;
            return true;
        }
        private static bool DisconnectSpectrum()
        {
            if (GCSDLL.JKEmissionClose() != 0)
            {
                MessageBox.Show("断开光谱仪失败");
                return false;
            }
            if (GCSDLL.JKCloseServer() != 0)
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
            if (GCSDLL.CVInitDark(fIntTime, iAveNum) == 0)
                MessageBox.Show("校零成功");
            else
                MessageBox.Show("校零失败");
        }
        private void SpectrumSingleTest(object sender, RoutedEventArgs e)
        {
            GCSDLL.CVOneTest(SpectrumData, (float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, false, false);
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


    }
}
