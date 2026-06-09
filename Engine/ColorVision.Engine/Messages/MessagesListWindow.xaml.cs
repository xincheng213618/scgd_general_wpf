#pragma warning disable CS8604
using ColorVision.Engine.MQTT;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Menus;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.Text;
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
        public override string Header => ColorVision.Engine.Properties.Resources.MsgLogWin;
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

        MessagesListManager MsgRecordManager { get; set; }

        public MessagesListWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            MessagesListWindowConfig.Instance.SetWindow(this);
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            MsgRecordManager = MessagesListManager.GetInstance();
            MsgRecordManager.LoadAll(MsgRecordManager.Config.Count);
            MsgRecordManager.StartListening();

            this.DataContext = MsgRecordManager;
            MsgRecords = MsgRecordManager.MsgRecords;
            ListView1.ItemsSource = MsgRecords;
        }
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MsgRecordManager.StopListening();
            MsgRecordManager.MsgRecords.Clear();
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
            MessageBox.Show(ColorVision.Engine.Properties.Resources.Engine_Msg_MqttHistoryCleared, "ColorVision");
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView1.SelectedIndex > -1)
            {
                GridContent.DataContext = MsgRecords[ListView1.SelectedIndex];
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            JsonSerializerSettings settings = new()
            {
                Formatting = Formatting.Indented
            };
            string text = JsonConvert.SerializeObject(MsgRecords[ListView1.SelectedIndex].MsgSend, settings);
            Common.NativeMethods.Clipboard.SetText(text);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            string text = JsonConvert.SerializeObject(MsgRecords[ListView1.SelectedIndex].MsgSend);
            Common.NativeMethods.Clipboard.SetText(text);
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            JsonSerializerSettings settings = new()
            {
                Formatting = Formatting.Indented
            };
            string text = JsonConvert.SerializeObject(MsgRecords[ListView1.SelectedIndex].MsgReturn, settings);
            Common.NativeMethods.Clipboard.SetText(text);
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            string text = JsonConvert.SerializeObject(MsgRecords[ListView1.SelectedIndex].MsgReturn);
            Common.NativeMethods.Clipboard.SetText(text);
        }

        private void StateFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MsgRecordManager == null) return;
            if (StateFilterComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                if (string.IsNullOrEmpty(tag))
                    MsgRecordManager.FilterMsgRecordState = null;
                else if (Enum.TryParse<MsgRecordState>(tag, out var state))
                    MsgRecordManager.FilterMsgRecordState = state;
            }
        }

        private void Button_Click_MqttStatus(object sender, RoutedEventArgs e)
        {
            MsgRecord selectedRecord = ListView1.SelectedItem as MsgRecord;

            Window window = new()
            {
                Title = "MQTT",
                Owner = this,
                Width = 920,
                Height = 660,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Background = FindResource("GlobalBackground") as System.Windows.Media.Brush
            };

            TextBox textBox = new()
            {
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                AcceptsReturn = true,
                FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(6)
            };

            void RefreshText() => textBox.Text = BuildMqttStatusText(selectedRecord);
            RefreshText();

            Button refreshButton = new()
            {
                Content = ColorVision.Engine.Properties.Resources.Refresh,
                Margin = new Thickness(0, 0, 6, 0),
                MinWidth = 72
            };
            refreshButton.Click += (_, _) => RefreshText();

            Button copyButton = new()
            {
                Content = ColorVision.Engine.Properties.Resources.Copy,
                MinWidth = 72
            };
            copyButton.Click += (_, _) => Common.NativeMethods.Clipboard.SetText(textBox.Text);

            StackPanel buttonPanel = new()
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(6, 6, 6, 0)
            };
            buttonPanel.Children.Add(refreshButton);
            buttonPanel.Children.Add(copyButton);

            DockPanel dockPanel = new();
            DockPanel.SetDock(buttonPanel, Dock.Top);
            dockPanel.Children.Add(buttonPanel);
            dockPanel.Children.Add(textBox);

            window.Content = dockPanel;
            window.Show();
        }

        private string BuildMqttStatusText(MsgRecord selectedRecord)
        {
            MQTTControl mqttControl = MQTTControl.GetInstance();
            var subscribedTopics = mqttControl.GetSubscribeTopicSnapshot().OrderBy(topic => topic).ToList();
            var traces = mqttControl.GetMessageTraceSnapshot();
            var relatedRecords = MsgRecords
                .Where(record => IsRelatedRecord(record, selectedRecord))
                .OrderByDescending(record => record.SendTime)
                .Take(50)
                .ToList();

            string sendTopic = selectedRecord?.SendTopic;
            string subscribeTopic = selectedRecord?.SubscribeTopic;
            var relatedTraces = traces
                .Where(trace => IsRelatedTrace(trace, sendTopic, subscribeTopic))
                .OrderByDescending(trace => trace.Time)
                .Take(80)
                .OrderBy(trace => trace.Time)
                .ToList();

            StringBuilder builder = new();
            builder.AppendLine("MQTT Connection");
            builder.AppendLine($"Connected: {mqttControl.IsConnect}");
            builder.AppendLine($"Subscribed topic count: {subscribedTopics.Count}");
            builder.AppendLine($"Runtime trace count: {traces.Count}");
            builder.AppendLine();

            builder.AppendLine("Selected Record");
            if (selectedRecord == null)
            {
                builder.AppendLine("No record selected. Showing loaded records and traces without topic filtering.");
            }
            else
            {
                builder.AppendLine($"SendTopic: {selectedRecord.SendTopic}");
                builder.AppendLine($"SubscribeTopic: {selectedRecord.SubscribeTopic}");
                builder.AppendLine($"EventName: {selectedRecord.MsgSend?.EventName}");
                builder.AppendLine($"DeviceCode: {selectedRecord.MsgSend?.DeviceCode ?? selectedRecord.MsgReturn?.DeviceCode}");
                builder.AppendLine($"MsgID: {selectedRecord.MsgID}");
                builder.AppendLine($"State: {selectedRecord.MsgRecordState}");
                if (!string.IsNullOrWhiteSpace(subscribeTopic) && !subscribedTopics.Contains(subscribeTopic))
                {
                    builder.AppendLine($"Expected subscribe topic is NOT in current subscriptions: {subscribeTopic}");
                }
            }
            builder.AppendLine();

            builder.AppendLine("Subscribed Topics");
            if (subscribedTopics.Count == 0)
            {
                builder.AppendLine("(none)");
            }
            else
            {
                foreach (string topic in subscribedTopics)
                {
                    string marker = string.Equals(topic, subscribeTopic, StringComparison.Ordinal) ? "*" : " ";
                    builder.AppendLine($"{marker} {topic}");
                }
            }
            builder.AppendLine();

            builder.AppendLine("Loaded MsgRecords");
            if (relatedRecords.Count == 0)
            {
                builder.AppendLine("(none)");
            }
            else
            {
                foreach (MsgRecord record in relatedRecords)
                {
                    builder.AppendLine(FormatRecordLine(record));
                }
            }
            builder.AppendLine();

            builder.AppendLine("Runtime MQTT Trace");
            if (relatedTraces.Count == 0)
            {
                builder.AppendLine("(none)");
            }
            else
            {
                foreach (var trace in relatedTraces)
                {
                    builder.AppendLine($"[{trace.Time:HH:mm:ss.fff}] {trace.Direction} {trace.Topic} QoS:{trace.QualityOfServiceLevel} Retain:{trace.Retain}");
                    builder.AppendLine($"    {ToSingleLine(trace.Payload, 600)}");
                }
            }

            return builder.ToString();
        }

        private static bool IsRelatedRecord(MsgRecord record, MsgRecord selectedRecord)
        {
            if (record == null)
                return false;
            if (selectedRecord == null)
                return true;

            string deviceCode = selectedRecord.MsgSend?.DeviceCode ?? selectedRecord.MsgReturn?.DeviceCode;

            return string.Equals(record.SendTopic, selectedRecord.SendTopic, StringComparison.Ordinal)
                || string.Equals(record.SubscribeTopic, selectedRecord.SubscribeTopic, StringComparison.Ordinal)
                || (!string.IsNullOrWhiteSpace(deviceCode)
                    && (string.Equals(record.MsgSend?.DeviceCode, deviceCode, StringComparison.Ordinal)
                        || string.Equals(record.MsgReturn?.DeviceCode, deviceCode, StringComparison.Ordinal)));
        }

        private static bool IsRelatedTrace(MqttMessageTraceEntry trace, string sendTopic, string subscribeTopic)
        {
            if (trace == null)
                return false;
            if (string.IsNullOrWhiteSpace(sendTopic) && string.IsNullOrWhiteSpace(subscribeTopic))
                return true;

            return string.Equals(trace.Topic, sendTopic, StringComparison.Ordinal)
                || string.Equals(trace.Topic, subscribeTopic, StringComparison.Ordinal);
        }

        private static string FormatRecordLine(MsgRecord record)
        {
            string receiveTime = record.IsRecive ? record.ReciveTime.ToString("HH:mm:ss.fff") : "-";
            string returnCode = record.MsgReturn == null ? "-" : record.MsgReturn.Code.ToString();
            string eventName = record.MsgSend?.EventName ?? record.MsgReturn?.EventName ?? string.Empty;
            return $"[{record.SendTime:HH:mm:ss.fff}] recv:{receiveTime} state:{record.MsgRecordState} code:{returnCode} event:{eventName} msg:{record.MsgID}";
        }

        private static string ToSingleLine(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            string singleLine = text.Replace("\r", string.Empty).Replace("\n", " ");
            if (singleLine.Length <= maxLength)
                return singleLine;

            return singleLine[..maxLength] + "...";
        }


    }
}
