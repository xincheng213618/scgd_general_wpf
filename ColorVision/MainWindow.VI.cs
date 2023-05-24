using ColorVision.MQTT;
using ColorVision.Template;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision
{
    /// <summary>
    /// 滤色轮
    /// </summary>
    public partial class MainWindow
    {
        private void ButtonSourceMeter1_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                if (!passSxSource.IsOpen)
                {
                    if (passSxSource.Open(passSxSource.IsNet, passSxSource.DevName))
                        button.Content = "关闭";
                }
                else
                {
                    passSxSource.Close();
                    button.Content = "打开";
                }
            }
        }

        private void MeasureData_Click(object sender, RoutedEventArgs e)
        {
            double V = 0, I = 0;
            passSxSource.MeasureData(passSxSource.MeasureVal, passSxSource.LmtVal, ref V, ref I);
        }
        private void StepMeasureData_Click(object sender, RoutedEventArgs e)
        {
            double V = 0, I = 0;
            passSxSource.MeasureData(passSxSource.MeasureVal, passSxSource.LmtVal, ref V, ref I);
        }

        private void MeasureDataClose_Click(object sender, RoutedEventArgs e)
        {
            passSxSource.CloseOutput();
        }

        PassSxSource passSxSource;


        private MQTTVISource MQTTVISource { get; set; }
        private void StackPanelVI_Initialized(object sender, EventArgs e)
        {
            MQTTVISource = new MQTTVISource();
            passSxSource = new PassSxSource();
            StackPanelVI.DataContext = passSxSource;
        }

        private void MQTTVIOpen(object sender, RoutedEventArgs e)
        {
            MQTTVISource.Open();
        }
        private void MQTTVIClose(object sender, RoutedEventArgs e)
        {
            MQTTVISource.Close();
        }

        private void MQTTVIGetData(object sender, RoutedEventArgs e)
        {
            MQTTVISource.GetData();
        }
    }
}
