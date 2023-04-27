#pragma warning disable CS4014
using ColorVision.Util;
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

namespace ColorVision
{
    /// <summary>
    /// MQTTDemo.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTDemo : Window
    {
        public MQTTDemo()
        {
            InitializeComponent();
        }
        MQTTHelper mQTTHelper = new MQTTHelper();


        private void Start_Click(object sender, RoutedEventArgs e)
        {
            string iPStr = TextBoxIP.Text.Trim();
            string portStr = TextBoxPort.Text.Trim();
            string uName = "";
            string uPwd = "";

            int port = Convert.ToInt32(portStr);

            mQTTHelper.CreateMQTTClientAndStart(iPStr, port, uName, uPwd, ShowLog);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
             mQTTHelper.DisconnectAsync_Client();
        }

        private void ShowLog(ResultData_MQTT resultData_MQTT)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                TextBoxResult.Text = $"\r\n返回结果：{resultData_MQTT.ResultCode}，返回信息：{resultData_MQTT.ResultMsg}" + TextBoxResult.Text;

            }));
        }

        private void Subscribe_Click(object sender, RoutedEventArgs e)
        {
            mQTTHelper.SubscribeAsync_Client(TextBoxSubscribe.Text);
        }

        private void UnSubscribe_Click(object sender, RoutedEventArgs e)
        {
            mQTTHelper.UnsubscribeAsync_Client(TextBoxSubscribe.Text);
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            mQTTHelper.PublishAsync_Client(TextBoxSubscribe1.Text, TextBoxSend.Text, CheckBoxRetained.IsChecked==true);
        }

        private void SendDemo_Click(object sender, RoutedEventArgs e)
        {
            mQTTHelper.PublishAsync_Client(TextBoxSubscribe1.Text, "CM_Open", CheckBoxRetained.IsChecked == true);


        }
        private void SendDemo1_Click(object sender, RoutedEventArgs e)
        {
            mQTTHelper.PublishAsync_Client(TextBoxSubscribe1.Text, "CM_GetFrame", CheckBoxRetained.IsChecked == true);


        }
    }
}
