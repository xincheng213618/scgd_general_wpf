using ColorVision.Engine.MQTT;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Messages
{
    public class ExportMsgList : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string GuidId => "MsgList";
        public override string Header => "消息日志窗口";
        public override int Order => 20;

        public override void Execute()
        {
            new MessagesListWindow() { Owner = Application.Current.GetActiveWindow() }.Show();
        }
    }

    public class MessagesListWindowConfig : WindowConfig
    {
        public static MessagesListWindowConfig Instance => ConfigService.Instance.GetRequiredService<MessagesListWindowConfig>();
    }



    /// <summary>
    /// MessagesListWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MessagesListWindow : Window
    {
        public ObservableCollection<MsgRecord> MsgRecords { get; set; }


        public MessagesListWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            MessagesListWindowConfig.Instance.SetWindow(this);
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext =
            MsgRecords = MsgConfig.Instance.MsgRecords;
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
                    JsonSerializerSettings settings = new()
                    {
                        Formatting = Formatting.Indented
                    }; 
                    string text = JsonConvert.SerializeObject(msgReturn, settings);
                    Common.NativeMethods.Clipboard.SetText(text);
                    MessageBox.Show(Application.Current.MainWindow, text, "ColorVision");
                }
                else if (stackPanel.Tag is MsgSend msgSend)
                {
                    JsonSerializerSettings settings = new()
                    {
                        Formatting = Formatting.Indented
                    };
                    string text = JsonConvert.SerializeObject(msgSend, settings);
                    Common.NativeMethods.Clipboard.SetText(text);
                    MessageBox.Show(Application.Current.MainWindow, text, "ColorVision");

                }
            }
        }

        private void MenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is MsgRecord msgRecord)
            {
                string json = JsonConvert.SerializeObject(msgRecord.MsgSend, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                Task.Run(() => MQTTControl.GetInstance().PublishAsyncClient(msgRecord.SendTopic, json, false));
            }
        }

        private void MenuItem_Click1(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is MsgRecord msgRecord)
            {
                string json = JsonConvert.SerializeObject(msgRecord.MsgReturn, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
                Task.Run(() => MQTTControl.GetInstance().PublishAsyncClient(msgRecord.SubscribeTopic, json, false));
            }
        }

        private void MenuItem_Click2(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is MsgRecord msgRecord)
            {
                JsonSerializerSettings settings = new()
                {
                    Formatting = Formatting.Indented
                };
                string text = JsonConvert.SerializeObject(msgRecord.MsgSend, settings);
                Common.NativeMethods.Clipboard.SetText(text);
                MessageBox.Show(Application.Current.MainWindow, text, "ColorVision");
            }

        }

        private void MenuItem_Click3(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is MsgRecord msgRecord)
            {
                JsonSerializerSettings settings = new()
                {
                    Formatting = Formatting.Indented
                };
                string text = JsonConvert.SerializeObject(msgRecord.MsgReturn, settings);
                Common.NativeMethods.Clipboard.SetText(text);
                MessageBox.Show(Application.Current.MainWindow, text, "ColorVision");
            }
        }

        private void MenuItem_Click4(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is MsgRecord msgRecord)
            {
                MsgRecords.Remove(msgRecord);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MsgRecords.Clear();
            MessageBox.Show("MQTT历史记录清理完毕", "ColorVision");
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                GridContent.DataContext = MsgConfig.Instance.MsgRecords[ListView1.SelectedIndex];
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            JsonSerializerSettings settings = new()
            {
                Formatting = Formatting.Indented
            };
            string text = JsonConvert.SerializeObject(MsgConfig.Instance.MsgRecords[ListView1.SelectedIndex].MsgSend, settings);
            Common.NativeMethods.Clipboard.SetText(text);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            string text = JsonConvert.SerializeObject(MsgConfig.Instance.MsgRecords[ListView1.SelectedIndex].MsgSend);
            Common.NativeMethods.Clipboard.SetText(text);
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            JsonSerializerSettings settings = new()
            {
                Formatting = Formatting.Indented
            };
            string text = JsonConvert.SerializeObject(MsgConfig.Instance.MsgRecords[ListView1.SelectedIndex].MsgReturn, settings);
            Common.NativeMethods.Clipboard.SetText(text);
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            string text = JsonConvert.SerializeObject(MsgConfig.Instance.MsgRecords[ListView1.SelectedIndex].MsgReturn);
            Common.NativeMethods.Clipboard.SetText(text);
        }

        private void SearchAdvanced_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Inquire_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
