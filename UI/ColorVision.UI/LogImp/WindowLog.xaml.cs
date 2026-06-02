using ColorVision.Themes;
using ColorVision.UI.LogImp;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System.Diagnostics;
using System.Globalization;
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
        TextBoxAppender TextBoxAppender { get; set; }
        Hierarchy Hierarchy { get; set; }
        private LogTextViewController? _logTextView;
        private void Window_Initialized(object sender, EventArgs e)
        {
            Hierarchy = (Hierarchy)LogManager.GetRepository();
            TextBoxAppender = new TextBoxAppender(logTextBox, logTextBoxSerch);
            TextBoxAppender.Layout = new PatternLayout(LogConstants.DefaultLogPattern);
            Hierarchy.Root.AddAppender(TextBoxAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);

            this.Closed += (s, e) =>
            {
                Hierarchy.Root.RemoveAppender(TextBoxAppender);
                TextBoxAppender.Dispose();
                _logTextView?.Detach();
                log4net.Config.BasicConfigurator.Configure(Hierarchy);
            };

            this.DataContext = LogConfig.Instance;

            _logTextView = new LogTextViewController(this, RootGrid, SearchPanel, SearchBar1, logTextBox, logTextBoxSerch, CloseSearchButton);
            _logTextView.ConfigureContextMenus((contextMenu, _) =>
                LogTextViewMenuFactory.AppendRealtimeLogMenuItems(contextMenu, ClearLog, SetLogLevel));

            LoadLogHistory();
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                logTextBox.ScrollToEnd();
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
            logTextBox.Text = string.Empty;
            var logFilePath = GetLogFilePath();
            if (logFilePath != null && File.Exists(logFilePath))
            {
                try
                {
                    using (FileStream fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader reader = new StreamReader(fileStream, Encoding.Default))
                    {
                        LoadLogs(reader);
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

        private void LoadLogs(StreamReader reader)
        {
            var logLoadState = LogConfig.Instance.LogLoadState;
            var logReserve = LogConfig.Instance.LogReserve;
            DateTime today = DateTime.Today;
            DateTime startupTime = Process.GetCurrentProcess().StartTime;
            List<string> matchingEntries = new List<string>();
            StringBuilder? currentEntry = null;
            bool currentEntryIncluded = false;

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (TryParseLogTimestamp(line, out DateTime logTime))
                {
                    if (currentEntryIncluded && currentEntry != null)
                    {
                        matchingEntries.Add(currentEntry.ToString());
                    }

                    currentEntryIncluded = ShouldIncludeLogEntry(logLoadState, today, startupTime, logTime);
                    currentEntry = currentEntryIncluded ? new StringBuilder(line) : null;
                    continue;
                }

                if (currentEntryIncluded && currentEntry != null)
                {
                    currentEntry.AppendLine();
                    currentEntry.Append(line);
                }
            }

            if (currentEntryIncluded && currentEntry != null)
            {
                matchingEntries.Add(currentEntry.ToString());
            }

            if (logReserve)
            {
                matchingEntries.Reverse();
            }

            logTextBox.Text = string.Join(Environment.NewLine, matchingEntries);
        }

        private static bool TryParseLogTimestamp(string line, out DateTime logTime)
        {
            logTime = default;
            if (string.IsNullOrWhiteSpace(line) || line.Length < LogConstants.LogTimestampLength)
            {
                return false;
            }

            return DateTime.TryParseExact(
                line.Substring(0, LogConstants.LogTimestampLength),
                LogConstants.LogTimestampFormat,
                null,
                DateTimeStyles.None,
                out logTime);
        }

        private static bool ShouldIncludeLogEntry(LogLoadState logLoadState, DateTime today, DateTime startupTime, DateTime logTime)
        {
            if (logLoadState == LogLoadState.AllToday)
            {
                return logTime.Date == today;
            }

            if (logLoadState == LogLoadState.SinceStartup)
            {
                return logTime >= startupTime;
            }

            return true;
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
            logTextBox.Text = string.Empty;
            logTextBoxSerch.Text = string.Empty;
        }

        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = LogViewUiHelper.NormalizeSearchText(SearchBar1.Text);
            TextBoxAppender.SearchText = searchText;
            _logTextView?.QueueSearchFilter(searchText);
        }
    }
}
