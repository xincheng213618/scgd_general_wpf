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

namespace ColorVision.MQTT.Control
{
    /// <summary>
    /// MQTTSpectrumControl.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTSpectrumControl : UserControl
    {
        public MQTTSpectrum Spectrum { get; set; }
        public MQTTSpectrumControl(MQTTSpectrum spectrum)
        {
            this.Spectrum = spectrum;
            InitializeComponent();
        }
        private void StackPanelSpectrum_Initialized(object sender, EventArgs e)
        {
            Spectrum.DataHandlerEvent += (e) =>
            {
                WindowSpectrum WindowSpectrum = new WindowSpectrum();
                WindowSpectrum.Show();
                WindowSpectrum.spectrumResult.SpectrumDrawPlot(e);
            };
        }

        #region MQTT
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Spectrum.Init();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            Spectrum.Open();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            Spectrum.SetParam();
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            Spectrum.GetData(100, 1);
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            Spectrum.Close();
            Spectrum.UnInit();
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
                WindowSpectrum.spectrumResult.SpectrumDrawPlot(data);
            }
            else
            {
                MessageBox.Show("测量失败");
            }
        }



        #endregion




    }
}
