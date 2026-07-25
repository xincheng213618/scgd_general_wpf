using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    public partial class FlowMessageListWindow : Window
    {
        public ObservableCollection<FlowNodeMessage> Messages { get; set; } = new ObservableCollection<FlowNodeMessage>();
        private List<FlowNodeMessage> _allMessages = new List<FlowNodeMessage>();
        private readonly string? _nodeId;
        private readonly string? _nodeName;
        private bool IsNodeScoped => !string.IsNullOrWhiteSpace(_nodeId);

        public FlowMessageListWindow()
            : this(null, null)
        {
        }

        public FlowMessageListWindow(string? nodeId, string? nodeName)
        {
            _nodeId = nodeId;
            _nodeName = nodeName;
            InitializeComponent();

            if (IsNodeScoped)
            {
                string displayName = !string.IsNullOrWhiteSpace(_nodeName) ? _nodeName : NodeTitleText.Text;
                Title = $"{Properties.Resources.Flow_NodeExecutionDetails} - {displayName}";
                FilterNodeName.Text = displayName;
                FilterNodeName.IsReadOnly = true;
                DeleteAllSeparator.Visibility = Visibility.Collapsed;
                DeleteAllButton.Visibility = Visibility.Collapsed;
                NodeExecutionSummaryPanel.Visibility = Visibility.Visible;
                NodeTitleText.Text = displayName;
                NodeIdText.Text = _nodeId;
            }
        }

        private async void Window_Initialized(object sender, EventArgs e)
        {
            ListView1.ItemsSource = Messages;
            await LoadMessagesAsync();
        }

        private async Task LoadMessagesAsync()
        {
            int limit = 500;
            if (int.TryParse(LoadCount.Text, out int val) && val > 0)
                limit = val;

            var result = await Task.Run(() =>
            {
                FlowNodeRecordDataBaseHelper.FlushPendingWrites();
                if (IsNodeScoped)
                {
                    return (
                        Messages: FlowNodeRecordDataBaseHelper.GetMessagesByNodeId(_nodeId!, limit),
                        Record: FlowNodeRecordDataBaseHelper.GetLastByNodeId(_nodeId!));
                }

                return (
                    Messages: FlowNodeRecordDataBaseHelper.GetAllMessages(limit),
                    Record: (FlowNodeRecord?)null);
            });

            _allMessages = result.Messages;
            if (IsNodeScoped)
            {
                FlowNodeRecord? record = result.Record;
                UpdateNodeExecutionSummary(record);
            }

            ApplyFilter();
            if (IsNodeScoped && Messages.Count > 0)
                ListView1.SelectedIndex = 0;
        }

        private void ApplyFilter()
        {
            Messages.Clear();
            var filtered = _allMessages.AsEnumerable();

            string nodeName = FilterNodeName.Text?.Trim();
            if (!IsNodeScoped && !string.IsNullOrEmpty(nodeName))
                filtered = filtered.Where(m => m.NodeName != null && m.NodeName.Contains(nodeName, StringComparison.OrdinalIgnoreCase));

            string eventName = FilterEventName.Text?.Trim();
            if (!string.IsNullOrEmpty(eventName))
                filtered = filtered.Where(m => m.EventName != null && m.EventName.Contains(eventName, StringComparison.OrdinalIgnoreCase));

            if (FilterState.SelectedItem is ComboBoxItem stateItem && stateItem.Tag?.ToString() != "All")
            {
                if (Enum.TryParse<FlowMessageState>(stateItem.Content.ToString(), out var state))
                    filtered = filtered.Where(m => m.State == state);
            }

            foreach (var msg in filtered)
                Messages.Add(msg);

            TotalCountText.Text = _allMessages.Count.ToString();
            DisplayCountText.Text = Messages.Count.ToString();
        }

        private void UpdateNodeExecutionSummary(FlowNodeRecord? record)
        {
            FlowNodeExecutionPresentation presentation = FlowNodeExecutionPresentation.FromRecord(record, DateTime.Now);
            string state = presentation.State == FlowNodeExecutionState.NotStarted
                ? Properties.Resources.Flow_NotStarted
                : presentation.State.ToString();
            string batch = record == null ? string.Empty : $" · Batch {record.BatchId}";
            string elapsed = presentation.ElapsedMs.HasValue
                ? $" · {Properties.Resources.Flow_Elapsed} {presentation.ElapsedMs.Value:N0} ms"
                : string.Empty;
            NodeExecutionSummaryText.Text = $"{state}{elapsed}{batch} · MQTT {_allMessages.Count}";

            if (record != null && string.IsNullOrWhiteSpace(_nodeName))
                NodeTitleText.Text = record.NodeName;
        }

        private void QueryButton_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadMessagesAsync();
        }

        private void DeleteAllButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show(Properties.Resources.Flow_MessageList_ConfirmClearAll, "ColorVision",
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
