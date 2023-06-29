using ColorVision.MVVM;
using ColorVision.SettingUp;
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
using System.Windows.Shapes;

namespace ColorVision.MQTT
{
    /// <summary>
    /// MQTTConnect.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTConnect : Window
    {
        public MQTTConnect()
        {
            InitializeComponent();
        }

        public void NumberValidationTextBox(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Back || e.Key == Key.Left || e.Key == Key.Right)
            {
                e.Handled = false;
                return;
            }
            if ((e.Key >= Key.D0 && e.Key <= Key.D9) || (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(MQTTConfig.Name))
            {
                MQTTConfig.Name = MQTTConfig.Host +"_" +MQTTConfig.Port;
            }

            GlobalSetting.GetInstance().SaveSoftwareConfig();
            Task.Run(() => MQTTControl.GetInstance().Connect());
            this.Close();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            MQTTConfigBackUp.CopyTo(MQTTConfig);
            this.Close();
        }


        public MQTTConfig MQTTConfig { get;set;}

        private MQTTConfig MQTTConfigBackUp { get; set; }


        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTConfig= GlobalSetting.GetInstance().SoftwareConfig.MQTTConfig;
            GridMQTT.DataContext = MQTTConfig;
            MQTTConfigBackUp = new MQTTConfig();
            MQTTConfig.CopyTo(MQTTConfigBackUp);
        }

        private async void Button_Click_Test(object sender, RoutedEventArgs e)
        {
            bool IsConnect = await MQTTControl.TestConnect(MQTTConfig);
            MessageBox.Show($"连接{(IsConnect ? "成功" : "失败")}");

        }
    }
}
