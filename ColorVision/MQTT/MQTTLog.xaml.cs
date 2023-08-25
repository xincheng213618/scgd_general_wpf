#pragma warning disable CS4014
using ColorVision.MQTT;
using ColorVision.SettingUp;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision
{
    public partial class MQTTLog : Window
    {

        MQTTControl MQTTControl { get; set; }

        public SoftwareConfig SoftwareConfig { get; set; }
        public MQTTSetting MQTTSetting { get => SoftwareConfig.MQTTSetting; }

        public MQTTLog()
        {
            InitializeComponent();
            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.MQTTMsgChanged += ShowLog;
            TopicListView.ItemsSource = MQTTControl.SubscribeTopic;
            SoftwareConfig = GlobalSetting.GetInstance().SoftwareConfig;
            this.DataContext = GlobalSetting.GetInstance();
            this.Title += $"  {MQTTControl.MQTTConfig.Host}_{MQTTControl.MQTTConfig.Port}";
        }




        private void ShowLog(MQMsg resultData_MQTT)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (MQTTSetting.IsShieldHeartbeat && !string.IsNullOrWhiteSpace(resultData_MQTT.Payload.ToString()))
                {
                    try
                    {
                        MsgReturn json = JsonConvert.DeserializeObject<MsgReturn>(resultData_MQTT.Payload.ToString() ?? string.Empty);
                        if (json != null && json.EventName == "Heartbeat")
                            return;
                    }catch 
                    {
                        
                    }

                }
                if (MQTTSetting.ShowSelect && (TopicListView.SelectedIndex<0 ||(TopicListView.SelectedIndex >-1&&resultData_MQTT.Topic.ToString()!= MQTTControl.SubscribeTopic[TopicListView.SelectedIndex])))
                {
                    return;
                }

                TextBox textBox = new TextBox() { BorderThickness = new Thickness(0),Text = resultData_MQTT.ResultMsg,Tag = resultData_MQTT,Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f5f5f5")) };

                if (!string.IsNullOrWhiteSpace(resultData_MQTT.Payload.ToString()))
                {
                    ContextMenu contextMenu = new ContextMenu();
                    MenuItem menuItem2 = new MenuItem() { Header = "复制Payload" };
                    menuItem2.Click += (s, e) => { NativeMethods.Clipboard.SetText(resultData_MQTT.Payload.ToString() ?? string.Empty); };
                    contextMenu.Items.Add(menuItem2);
                    MenuItem menuItem = new MenuItem() { Header = "复制" };
                    menuItem.Click += (s, e) => { NativeMethods.Clipboard.SetText(textBox.Text); };
                    contextMenu.Items.Add(menuItem);
                    MenuItem menuItem1 = new MenuItem() { Header = "复制Topic" };
                    menuItem1.Click += (s, e) => { NativeMethods.Clipboard.SetText(resultData_MQTT.Topic.ToString() ?? string.Empty); };
                    contextMenu.Items.Add(menuItem1);
                    MenuItem menuItem3 = new MenuItem() { Header = "SaveToFile" };

                    menuItem3.Click += (s, e) =>
                    {
                        System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog();
                        saveFileDialog.Filter = "文本文件|*.txt";
                        saveFileDialog.FileName = resultData_MQTT.Topic + DateTime.Now.ToString("MM-dd HH-mm-ss");
                        if (saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            File.WriteAllText(saveFileDialog.FileName, resultData_MQTT.Payload.ToString());
                        };
                    };
                    contextMenu.Items.Add(menuItem3);
                    textBox.ContextMenu = contextMenu;
                }
                else
                {
                    textBox.ContextMenu = null;
                }
                StackPanelText.Children.Insert(0,textBox);
            }));
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            MQTTControl.DisconnectAsyncClient();
        }

        private void Subscribe_Click(object sender, RoutedEventArgs e)
        {
            MQTTControl.SubscribeAsyncClientAsync(TextBoxSubscribe.Text);
        }

        private void UnSubscribe_Click(object sender, RoutedEventArgs e)
        {
            MQTTControl.UnsubscribeAsyncClientAsync(TextBoxSubscribe.Text);
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
    }
}
