using ColorVision.Themes;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace EventVWR
{

    /// <summary>
    /// EventWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EventWindow : Window
    {
        public EventWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
        }
        public ObservableCollection<EventLogEntry> logEntries { get; set; } = new ObservableCollection<EventLogEntry>();
        private void Window_Initialized(object sender, EventArgs e)
        {
            string logName = "Application";

            // 使用 using 语句确保资源释放
            using (EventLog eventLog = new EventLog(logName))
            {
                // 使用 LINQ 查询并倒序排列结果
                 logEntries = new ObservableCollection<EventLogEntry>(
                    eventLog.Entries.Cast<EventLogEntry>()
                    .Where(entry => entry.EntryType == EventLogEntryType.Error)
                    .OrderByDescending(entry => entry.TimeGenerated)
                );

                // 设置 ListView 的 ItemsSource
                ListViewEvent.ItemsSource = logEntries;
            }
        }

        private void ListViewEvent_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                MessageText.Text = logEntries[listView.SelectedIndex].Message;
            }
        }
    }
}
