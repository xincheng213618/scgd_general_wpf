using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms.Integration;
using System.Windows.Markup;
using ColorVision.MQTT;
using ColorVision.Template;
using ColorVision.Util;
using cvColorVision;
using static cvColorVision.GCSDLL;

namespace ColorVision
{
    public partial class MainWindow
    {
        private MQTTSpectrum MQTTSpectrum { get; set; }

        private void StackPanelSpectrum_Initialized(object sender, EventArgs e)
        {
            MQTTSpectrum = new MQTTSpectrum();
            MQTTSpectrum.DataHandlerEvent += WindowSpectrum.spectrumResult.SpectrumDrawPlot;
        }

        #region MQTT
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            MQTTSpectrum.Init();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            MQTTSpectrum.Open();
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            MQTTSpectrum.SetParam();
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            MQTTSpectrum.GetData(100,1);
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            MQTTSpectrum.Close();
        }
        #endregion
        #region Spectrum

        private GCSDLL.DllcallBack SpectrumData;

        private void ConnectSpectrum()
        {
            if (GCSDLL.JKStartServer() != 0)
            {
                MessageBox.Show("启动光谱仪软件的服务失败");
                return;
            }
            if (GCSDLL.CVInit() != 0)
            {
                MessageBox.Show("连接光谱仪失败");
                return;
            }
            SpectrumData += TestResult;
        }
        private static void DisconnectSpectrum()
        {
            if (GCSDLL.JKEmissionClose() != 0)
            {
                MessageBox.Show("断开光谱仪失败");
                return;
            }
            if (GCSDLL.JKCloseServer() != 0)
            {
                MessageBox.Show("断开光谱仪软件的服务失败");
                return;
            }
        }



        private async void SpectrumIni(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (button.Content.ToString() == "连接光谱仪")
                {
                    await Task.Run(() => ConnectSpectrum());
                    button.Content = "断开光谱仪";
                }
                else
                {
                    await Task.Run(() => DisconnectSpectrum());
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
        WindowSpectrum WindowSpectrum = new WindowSpectrum();
        private void SpectrumSingleTest(object sender, RoutedEventArgs e)
        {
            GCSDLL.CVOneTest(SpectrumData, (float)SpectrumSliderIntTime.Value, (int)SpectrumSliderAveNum.Value, false, false);
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
