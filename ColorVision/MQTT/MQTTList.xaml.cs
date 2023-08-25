using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.MQTT
{
    /// <summary>
    /// MQTTList.xaml 的交互逻辑
    /// </summary>
    public partial class MQTTList : Window
    {

        public ObservableCollection<MsgRecord> MsgRecords { get; set; }
        public MQTTControl MQTTControl { get; set; }


        public MQTTList()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTControl = MQTTControl.GetInstance();
            MsgRecords = MQTTControl.MQTTSetting.MsgRecords;
            ListView1.ItemsSource = MsgRecords;
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }

        private void StackPanel_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is StackPanel stackPanel  )
            {
                if (stackPanel.Tag is MsgReturn msgReturn)
                {
                    JsonSerializerSettings settings = new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented
                    }; 
                    string text = JsonConvert.SerializeObject(msgReturn, settings);
                    NativeMethods.Clipboard.SetText(text);
                    MessageBox.Show(text, "ColorVision");
                }
                else if (stackPanel.Tag is MsgSend msgSend)
                {
                    JsonSerializerSettings settings = new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented
                    };
                    string text = JsonConvert.SerializeObject(msgSend, settings);
                    NativeMethods.Clipboard.SetText(text);
                    MessageBox.Show(text, "ColorVision");

                }
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is MsgRecord msgRecord)
            {
                string json = JsonConvert.SerializeObject(msgRecord.MsgSend, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                Task.Run(() => MQTTControl.PublishAsyncClient(msgRecord.SendTopic, json, false));
            }
        }

        private void MenuItem_Click1(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is MsgRecord msgRecord)
            {
                string json = JsonConvert.SerializeObject(msgRecord.MsgReturn, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                Task.Run(() => MQTTControl.PublishAsyncClient(msgRecord.SubscribeTopic, json, false));
            }
        }

        private void MenuItem_Click2(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is MsgRecord msgRecord)
            {
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                };
                string text = JsonConvert.SerializeObject(msgRecord.MsgSend, settings);
                NativeMethods.Clipboard.SetText(text);
                MessageBox.Show(text, "ColorVision");
            }

        }

        private void MenuItem_Click3(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is MsgRecord msgRecord)
            {
                JsonSerializerSettings settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                };
                string text = JsonConvert.SerializeObject(msgRecord.MsgReturn, settings);
                NativeMethods.Clipboard.SetText(text);
                MessageBox.Show(text, "ColorVision");
            }
        }

        private void MenuItem_Click4(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is MsgRecord msgRecord)
            {
                MsgRecords.Remove(msgRecord);
            }
        }
    }
}
