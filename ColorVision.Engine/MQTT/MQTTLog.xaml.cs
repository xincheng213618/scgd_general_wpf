#pragma warning disable CS4014
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.Properties;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using NPOI.Util.Collections;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.MQTT
{
    public class ExportMQTTLox : IHotKey, IMenuItem
    {
        public string? OwnerGuid => "Help";

        public string? GuidId => "MQTTLog";

        public int Order => 1;
        public Visibility Visibility => Visibility.Visible;

        public string? Header => Resources.MQTTLog;

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new(A => Execute());

        public HotKeys HotKeys => new(Resources.MQTTLog, new Hotkey(Key.Q, ModifierKeys.Control), Execute);

        private void Execute()
        {
            new MQTTLog() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }


    public class ExportServiceLog : IMenuItem
    {

        public string? OwnerGuid => "Help";

        public string? GuidId => "ServiceLog";

        public int Order => 1;
        public Visibility Visibility => Visibility.Visible;

        public string? Header => Resources.ServiceLog;

        public string? InputGestureText { get; }

        public object? Icon { get; }

        public RelayCommand Command => new(A => Execute());

        private static void Execute()
        {
        }
    }


public partial class MQTTLog : Window
    {

        MQTTControl MQTTControl { get; set; }
        public MQTTLog()
        {
            InitializeComponent();
            MQTTControl = MQTTControl.GetInstance();
            MQTTControl.MQTTMsgChanged += ShowLog;
            TopicListView.ItemsSource = MQTTControl.SubscribeTopic;
            DataContext = MQTTSetting.Instance;
            Title += $"  {MQTTControl.Config.Host}_{MQTTControl.Config.Port}";
        }




        private void ShowLog(MQMsg resultData_MQTT)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (MQTTSetting.Instance.IsShieldHeartbeat && !string.IsNullOrWhiteSpace(resultData_MQTT.Payload.ToString()))
                {

                }
                if (MQTTSetting.Instance.ShowSelect && (TopicListView.SelectedIndex<0 ||(TopicListView.SelectedIndex >-1&&resultData_MQTT.Topic.ToString()!= MQTTControl.SubscribeTopic[TopicListView.SelectedIndex])))
                {
                    return;
                }

                TextBox textBox = new() { BorderThickness = new Thickness(0),Text = resultData_MQTT.ResultMsg,Tag = resultData_MQTT};

                if (!string.IsNullOrWhiteSpace(resultData_MQTT.Payload.ToString()))
                {
                    ContextMenu contextMenu = new();
                    MenuItem menuItem2 = new() { Header = "复制Payload" };
                    menuItem2.Click += (s, e) => { Common.NativeMethods.Clipboard.SetText(resultData_MQTT.Payload.ToString() ?? string.Empty); };
                    contextMenu.Items.Add(menuItem2);
                    MenuItem menuItem = new() { Header = "复制" };
                    menuItem.Click += (s, e) => { Common.NativeMethods.Clipboard.SetText(textBox.Text); };
                    contextMenu.Items.Add(menuItem);
                    MenuItem menuItem1 = new() { Header = "复制Topic" };
                    menuItem1.Click += (s, e) => { Common.NativeMethods.Clipboard.SetText(resultData_MQTT.Topic.ToString() ?? string.Empty); };
                    contextMenu.Items.Add(menuItem1);
                    MenuItem menuItem3 = new() { Header = "SaveToFile" };

                    menuItem3.Click += (s, e) =>
                    {
                        System.Windows.Forms.SaveFileDialog saveFileDialog = new();
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
