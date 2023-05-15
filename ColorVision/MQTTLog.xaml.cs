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
using System.Windows.Controls;

namespace ColorVision
{
    public partial class MQTTLog : Window
    {

        MQTTControl MQTTControl;
        public MQTTLog()
        {
            InitializeComponent();
            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.MQTTMsgChanged += ShowLog;
            TopicListView.ItemsSource = MQTTControl.SubscribeTopic;
            this.DataContext = MQTTControl;
        }


        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (!MQTTControl.IsConnect)
                await MQTTControl.Connect();
        }

        private void ShowLog(ResultDataMQTT resultData_MQTT)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                TextBoxResult.Text = $"{resultData_MQTT.ResultMsg}\r\n" + TextBoxResult.Text;
            }));
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            MQTTControl.DisconnectAsyncClient();
        }

        private void Subscribe_Click(object sender, RoutedEventArgs e)
        {
            MQTTControl.SubscribeAsyncClient(TextBoxSubscribe.Text);
        }

        private void UnSubscribe_Click(object sender, RoutedEventArgs e)
        {
            MQTTControl.UnsubscribeAsyncClient(TextBoxSubscribe.Text);
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            MQTTControl.PublishAsyncClient(TextBoxSubscribe1.Text, TextBoxSend.Text, CheckBoxRetained.IsChecked==true);
        }

        private void TopicListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                TextBoxSubscribe1.Text = MQTTControl.SubscribeTopic[listView.SelectedIndex];
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string json = "{\"Code\":0,\"EventName\":\"InitCamera\",\"Msg\":\"\",\"data\":{\"CameraId\":\"{\\n\\t\\\"ID\\\" : \\n\\t[\\n\\t\\t\\\"566b2242984bc686b\\\"\\n\\t],\\n\\t\\\"number\\\" : 1\\n}\\n\"}}";
            MQTTControl.PublishAsyncClient("CameraService", json, false);
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            string json = "{\"Code\":0,\"EventName\":\"OpenCamera\",\"Msg\":\"\",\"data\":null}";
            MQTTControl.PublishAsyncClient("CameraService", json, false);
        }

        private void Button2_Click(object sender, RoutedEventArgs e)
        {
            string json = "{\"Version\":\"1.0\",\"EventName\":\"GetData\",\"Code\":0,\"Msg\":\"\",\"data\":{\"FilePath\":\"D:\\\\1.tif\"}}";
            MQTTControl.PublishAsyncClient("CameraService", json, false);
        }
    }
}
