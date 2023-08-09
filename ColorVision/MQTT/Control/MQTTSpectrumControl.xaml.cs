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
using static ColorVision.MQTT.MQTTSpectrum;
using static cvColorVision.GCSDLL;

namespace ColorVision.MQTT.Control
{
    /// <summary>
    /// MQTTSpectrumControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTSpectrumControl : UserControl
    {
        public MQTTSpectrum Spectrum { get; set; }
        private WindowSpectrum? windowSpectrum;
        public MQTTSpectrumControl(MQTTSpectrum spectrum)
        {
            this.Spectrum = spectrum;
            InitializeComponent();
        }

        private MQTTSpectrum GetSpectrum()
        {
            return Spectrum;
        }

        private void StackPanelSpectrum_Initialized(object sender, EventArgs e)
        {
            Spectrum.DataHandlerEvent += (e) =>
            {
                if (windowSpectrum != null && e != null)
                {
                    windowSpectrum.spectrumResult.SpectrumDrawPlot(e);
                }
            };

            Spectrum.HeartbeatHandlerEvent += (e) =>
            {
                if (e.IsOpen)
                {
                    connectBtn.Content = "关闭";
                    if(windowSpectrum==null)
                    {
                        windowSpectrum = new WindowSpectrum();
                        windowSpectrum.Show();
                    }
                }
                else
                {
                    connectBtn.Content = "打开";
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

        #region MQTT
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Spectrum.Init();
        }

        private void Button_Click_Open(object sender, RoutedEventArgs e)
        {
            string btnTitle = connectBtn.Content.ToString();
            if (!string.IsNullOrWhiteSpace(btnTitle) && btnTitle.Equals("打开", StringComparison.Ordinal))
            {
                Spectrum.Open();
                connectBtn.Content = "关闭";
                //windowSpectrum = new WindowSpectrum();
                //windowSpectrum.Show();
            }
            else
            {
                Spectrum.Close();
                connectBtn.Content = "打开";
                if (windowSpectrum != null)
                {
                    windowSpectrum.Close();
                    windowSpectrum = null;
                }
            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            //Spectrum.SetParam();
        }

        private void Button_Click_OneTest(object sender, RoutedEventArgs e)
        {
            Spectrum.GetData((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, AutoIntTime.IsChecked??false, AutoDark.IsChecked ?? false);
        }

        private void Button_Click_Close(object sender, RoutedEventArgs e)
        {
            Spectrum.Close();
            //Spectrum.UnInit();
        }
        private void Button_Click_AutoTest(object sender, RoutedEventArgs e)
        {
            string btnTitle = autoTest.Content.ToString();
            if (!string.IsNullOrWhiteSpace(btnTitle) && btnTitle.Equals("自动测试", StringComparison.Ordinal))
            {
                Spectrum.GetDataAuto((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, AutoIntTime.IsChecked ?? false, AutoDark.IsChecked ?? false);
                autoTest.Content = "取消自动测试";
            }
            else
            {
                Spectrum.GetDataAutoStop();
                autoTest.Content = "自动测试";
            }
        }
        private void Button_Click_Init_Dark(object sender, RoutedEventArgs e)
        {
            Spectrum.InitDark((float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value);
        }
        private void Button_Click_GetParam(object sender, RoutedEventArgs e)
        {
            Spectrum.GetParam();
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
        WindowSpectrum WindowSpectrum;
        private void SpectrumSingleTest(object sender, RoutedEventArgs e)
        {
            GCSDLL.CVOneTest(SpectrumData, (float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, false, false);
            WindowSpectrum ??= new WindowSpectrum();
            WindowSpectrum.Show();
        }

        public void TestResult(ref ColorParam data, float intTime, int resultCode)
        {
            if (resultCode == 0)
            {
                WindowSpectrum.spectrumResult.SpectrumDrawPlot(new SpectumData(-1,data));
            }
            else
            {
                MessageBox.Show("测量失败");
            }
        }
        #endregion
    }
}
