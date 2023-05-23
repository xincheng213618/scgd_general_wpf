#pragma warning disable CS4014
using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.Util;
using MQTTnet.Client;
using MQTTnet.Server;
using ScottPlot.Drawing.Colormaps;
using System;
using System.Collections.Generic;
using System.IO;
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
                TextBox textBox = new TextBox() { BorderThickness = new Thickness(0),Text = resultData_MQTT.ResultMsg,Tag = resultData_MQTT };
                ContextMenu contextMenu = new ContextMenu();
                MenuItem menuItem2 = new MenuItem() { Header = "复制Payload" };
                menuItem2.Click += (s, e) => { NativeMethods.Clipboard.SetText(resultData_MQTT.Payload.ToString() ?? string.Empty); };
                contextMenu.Items.Add(menuItem2);
                MenuItem menuItem = new MenuItem() { Header = "复制" };
                menuItem.Click += (s, e) => { NativeMethods.Clipboard.SetText(textBox.Text); };
                contextMenu.Items.Add(menuItem);
                MenuItem menuItem1 = new MenuItem() { Header = "复制Topic" };
                menuItem1.Click += (s, e) => { NativeMethods.Clipboard.SetText(resultData_MQTT.Topic.ToString()??string.Empty); };
                contextMenu.Items.Add(menuItem1);
                MenuItem menuItem3 = new MenuItem() { Header = "SaveToFile" };
                menuItem3.Click += (s, e) => {
                    System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
                    saveFileDialog.Filter = "文本文件|*.txt";
                    saveFileDialog.FileName = resultData_MQTT.Topic + DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss");
                    if (saveFileDialog.ShowDialog()== System.Windows.Forms.DialogResult.OK)
                    {
                        File.WriteAllText(saveFileDialog.FileName, resultData_MQTT.Payload.ToString());
                    };
                };
                contextMenu.Items.Add(menuItem3);
                textBox.ContextMenu = contextMenu;
                StackPanelText.Children.Insert(0,textBox);
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
