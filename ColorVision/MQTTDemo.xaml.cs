#pragma warning disable CS4014
using ColorVision.MQTT;
using ColorVision.MVVM;
using MQTTnet.Client;
using MQTTnet.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision
{
    public partial class MQTTDemo : Window
    {

        MQTTControl mQTTControl;
        public MQTTDemo()
        {
            InitializeComponent();
            mQTTControl = MQTTControl.GetInstance();

            this.DataContext = mQTTControl;
        }


        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (!mQTTControl.IsConnect)
                await mQTTControl.Connect();
            mQTTControl.MQTTMsgChanged += ShowLog;
        }

        private void ShowLog(ResultDataMQTT resultData_MQTT)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                TextBoxResult.Text = $"\r\n返回结果：{resultData_MQTT.ResultCode}，返回信息：{resultData_MQTT.ResultMsg}" + TextBoxResult.Text;
            }));
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            mQTTControl.DisconnectAsyncClient();
        }

        private void Subscribe_Click(object sender, RoutedEventArgs e)
        {
            mQTTControl.SubscribeAsyncClient(TextBoxSubscribe.Text);
        }

        private void UnSubscribe_Click(object sender, RoutedEventArgs e)
        {
            mQTTControl.UnsubscribeAsyncClient(TextBoxSubscribe.Text);
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            mQTTControl.PublishAsyncClient(TextBoxSubscribe1.Text, TextBoxSend.Text, CheckBoxRetained.IsChecked==true);
        }

    }
}
