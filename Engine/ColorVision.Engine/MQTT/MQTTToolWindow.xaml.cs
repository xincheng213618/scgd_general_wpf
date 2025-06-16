#pragma warning disable CS4014
using ColorVision.Engine.Properties;
using ColorVision.Themes;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.MQTT
{
    public class ExportMQTTTool : MenuItemBase,IHotKey
    {
        public override string OwnerGuid => nameof(ExportMQTTMenuItem);
        public override string GuidId => "MQTTLog";
        public override string Header => Resources.MQTTLog;
        public override int Order => 1;

        public HotKeys HotKeys => new(Resources.MQTTLog, new Hotkey(Key.Q, ModifierKeys.Control), Execute);

        public override void Execute()
        {
            new MQTTToolWindow() { Owner = WindowHelpers.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }

    public partial class MQTTToolWindow : Window
    {

        public static MQTTControl MQTTControl => MQTTControl.GetInstance();
        public MQTTToolWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }
        private void Window_Initialized(object sender, EventArgs e)
        {
            Title += $"  {MQTTControl.Config.Host}_{MQTTControl.Config.Port}";
            MQTTControl.MQTTLogChanged += ShowLog;
            TopicListView.ItemsSource = MQTTControl.SubscribeTopic;
            DataContext = MQTTSetting.Instance;
            this.Closed += (s,e) => MQTTControl.MQTTLogChanged -= ShowLog;
        }

        private void ShowLog(MQTTLog resultData_MQTT)
        {
            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (MQTTSetting.Instance.IsShieldHeartbeat && !string.IsNullOrWhiteSpace(resultData_MQTT.Payload.ToString()))
                {

                }
                if (MQTTSetting.Instance.ShowSelect && !resultData_MQTT.Topic.ToString().Contains(TextBoxSubscribe1.Text))
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
