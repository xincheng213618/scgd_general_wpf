using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Flow
{
    public partial class FlowMessageListWindow : Window
    {
        public ObservableCollection<FlowNodeMessage> Messages { get; set; } = new ObservableCollection<FlowNodeMessage>();
        private List<FlowNodeMessage> _allMessages = new List<FlowNodeMessage>();

        public FlowMessageListWindow()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ListView1.ItemsSource = Messages;
            LoadMessages();
        }

        private void LoadMessages()
        {
            int limit = 500;
            if (int.TryParse(LoadCount.Text, out int val) && val > 0)
                limit = val;

            _allMessages = FlowNodeRecordDataBaseHelper.GetAllMessages(limit);
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            Messages.Clear();
            var filtered = _allMessages.AsEnumerable();

            string nodeName = FilterNodeName.Text?.Trim();
            if (!string.IsNullOrEmpty(nodeName))
                filtered = filtered.Where(m => m.NodeName != null && m.NodeName.Contains(nodeName, StringComparison.OrdinalIgnoreCase));

            string eventName = FilterEventName.Text?.Trim();
            if (!string.IsNullOrEmpty(eventName))
                filtered = filtered.Where(m => m.EventName != null && m.EventName.Contains(eventName, StringComparison.OrdinalIgnoreCase));

            if (FilterState.SelectedItem is ComboBoxItem stateItem && stateItem.Content?.ToString() != "全部")
            {
                if (Enum.TryParse<FlowMessageState>(stateItem.Content.ToString(), out var state))
                    filtered = filtered.Where(m => m.State == state);
            }

            foreach (var msg in filtered)
                Messages.Add(msg);

            TotalCountText.Text = _allMessages.Count.ToString();
            DisplayCountText.Text = Messages.Count.ToString();
        }

        private void QueryButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadMessages();
        }

        private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("确定要清空所有流程MQTT消息记录吗？", "ColorVision",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                FlowNodeRecordDataBaseHelper.DeleteAllMessages();
                _allMessages.Clear();
                Messages.Clear();
                TotalCountText.Text = "0";
                DisplayCountText.Text = "0";
                SendPayloadBox.Text = string.Empty;
                RecvPayloadBox.Text = string.Empty;
                SendTopicText.Text = string.Empty;
                RecvTopicText.Text = string.Empty;
            }
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListView1.SelectedItem is FlowNodeMessage msg)
            {
                SendPayloadBox.Text = FormatJsonSafe(msg.SendPayload);
                RecvPayloadBox.Text = FormatJsonSafe(msg.RecvPayload);
                SendTopicText.Text = msg.SendTopic ?? string.Empty;
                RecvTopicText.Text = msg.RecvTopic ?? string.Empty;
            }
            else
            {
                SendPayloadBox.Text = string.Empty;
                RecvPayloadBox.Text = string.Empty;
                SendTopicText.Text = string.Empty;
                RecvTopicText.Text = string.Empty;
            }
        }

        private static string FormatJsonSafe(string json)
        {
            if (string.IsNullOrEmpty(json)) return string.Empty;
            try
            {
                var obj = JsonConvert.DeserializeObject(json);
                return JsonConvert.SerializeObject(obj, Formatting.Indented);
            }
            catch
            {
                return json;
            }
        }
    }
}
