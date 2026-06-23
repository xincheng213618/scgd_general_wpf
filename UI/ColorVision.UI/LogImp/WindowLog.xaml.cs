using ColorVision.Themes;
using ColorVision.UI.LogImp;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI
{
    /// <summary>
    /// WindowLog.xaml 的交互逻辑
    /// </summary>
    public partial class WindowLog : Window
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(WindowLog));

        public WindowLog()
        {
            InitializeComponent();
            this.ApplyCaption();
        }
        LogViewerAppender LogViewerAppender { get; set; }
        Hierarchy Hierarchy { get; set; }
        private LogTextViewController? _logTextView;
        private bool _autoRefresh = true;
        private void Window_Initialized(object sender, EventArgs e)
        {
            Hierarchy = (Hierarchy)LogManager.GetRepository();
            LogViewerAppender = new LogViewerAppender(LogViewer)
            {
                Layout = new PatternLayout(LogConstants.DefaultLogPattern),
                IgnoreAutoRefresh = true,
                AutoRefresh = _autoRefresh
            };
            Hierarchy.Root.AddAppender(LogViewerAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);

            this.Closed += (s, e) =>
            {
                Hierarchy.Root.RemoveAppender(LogViewerAppender);
                LogViewerAppender.Dispose();
                _logTextView?.Detach();
                log4net.Config.BasicConfigurator.Configure(Hierarchy);
            };

            this.DataContext = LogConfig.Instance;

            _logTextView = new LogTextViewController(this, RootGrid, SearchPanel, SearchBar1, LogViewer, CloseSearchButton);
            _logTextView.ConfigureContextMenus(contextMenu => LogTextViewMenuFactory.AppendRealtimeLogMenuItems(contextMenu, ClearLog, SetLogLevel, GetAutoRefresh, SetAutoRefresh));

            LoadLogHistory();
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                LogViewer.ScrollToLatest(LogConfig.Instance.LogReserve);
            });

        }
        private static string? GetLogFilePath()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            var fileAppender = hierarchy.Root.Appenders.OfType<RollingFileAppender>().FirstOrDefault();
            return fileAppender?.File;
        }


        private void LoadLogHistory()
        {
            if (LogConfig.Instance.LogLoadState == LogLoadState.None) return;
            LogViewer.Clear();
            LogViewer.MaxEntries = LogConfig.Instance.MaxEntries;
            var logFilePath = GetLogFilePath();
            if (logFilePath != null && File.Exists(logFilePath))
            {
                try
                {
                    using (FileStream fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader reader = new StreamReader(fileStream, Encoding.Default))
                    {
                        if (LogViewer.UsesVirtualizedRendering)
                        {
                            var entries = LogHistoryReader.ReadEntries(
                                reader,
                                LogConfig.Instance.LogLoadState,
                                reverse: false,
                                LogConfig.Instance.MaxChars);
                            LogViewer.SetEntries(entries, LogConfig.Instance.LogReserve);
                        }
                        else
                        {
                            var displayText = LogHistoryReader.ReadDisplayText(
                                reader,
                                LogConfig.Instance.LogLoadState,
                                LogConfig.Instance.LogReserve,
                                LogConfig.Instance.MaxChars);
                            LogViewer.SetText(displayText, LogConfig.Instance.LogReserve);
                        }
                    }
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Error reading log file: {ex.Message}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An unexpected error occurred: {ex.Message}");
                }
            }
        }

        private void SetLogLevel(Level selectedLevel)
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            if (!string.Equals(selectedLevel.Name, hierarchy.Root.Level?.Name, StringComparison.Ordinal))
            {
                LogConfig.Instance.LogLevel = selectedLevel;
                log.Info(Properties.Resources.UpdateLog4NetLevel + selectedLevel.Name);
            }
        }

        private void ClearLog()
        {
            LogViewer.Clear();
        }

        private bool GetAutoRefresh()
        {
            return LogViewerAppender.AutoRefresh;
        }

        private void SetAutoRefresh(bool autoRefresh)
        {
            _autoRefresh = autoRefresh;
            LogViewerAppender.AutoRefresh = autoRefresh;
        }

        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = LogViewUiHelper.NormalizeSearchText(SearchBar1.Text);
            _logTextView?.QueueSearchFilter(searchText);
        }
    }
}
