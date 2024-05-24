using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.MQTT;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Msg
{
    public class HotKeyMsgList : IHotKey, IMenuItem
    {
        public HotKeys HotKeys => new(Properties.Resources.MsgList, new Hotkey(Key.M, ModifierKeys.Control), Execute);
        private void Execute()
        {
            new MsgList() { Owner = Application.Current.GetActiveWindow() }.Show();
        }

        public string? OwnerGuid => "Help";

        public string? GuidId => "MsgList";
        public int Order => 2;
        public string? Header => "MQTTMsg";

        public string? InputGestureText { get; } = "Ctrl + M";

        public object? Icon { get; }

        public RelayCommand Command => new(a => Execute());
        public Visibility Visibility => Visibility.Visible;
    }

    /// <summary>
    /// MsgList.xaml 的交互逻辑
    /// </summary>
    public partial class MsgList : Window
    {
        public ObservableCollection<MsgRecord> MsgRecords { get; set; }


        public MsgList()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
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
    }
}
