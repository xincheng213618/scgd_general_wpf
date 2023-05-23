using ColorVision.MQTT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision
{
    /// <summary>
    /// 滤色轮
    /// </summary>
    public partial class MainWindow
    {
        public MQTTVISource MQTTVISource { get; set; } = new MQTTVISource();

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
