using ColorVision.Common.MVVM;
using ColorVision.Themes;
using ColorVision.UI.LogImp;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI
{
    public class TextAppender : AppenderSkeleton
    {
        private LogStatusBarProvider LogStatusBarProvider;
        private readonly object _syncLock = new object();
        private string _latestMessage = string.Empty;
        private bool _updateQueued;

        public TextAppender(LogStatusBarProvider logStatusBarProvider)
        {
            LogStatusBarProvider = logStatusBarProvider;
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            var renderedMessage = RenderLoggingEvent(loggingEvent);
            string messageToShow = renderedMessage.Length > 10 ? string.Concat(renderedMessage.AsSpan(0, 10), "...") : renderedMessage;

            bool shouldQueueUpdate = false;
            lock (_syncLock)
            {
                _latestMessage = messageToShow;
                if (!_updateQueued)
                {
                    _updateQueued = true;
                    shouldQueueUpdate = true;
                }
            }

            if (shouldQueueUpdate)
            {
                QueueStatusUpdate();
            }
        }

        private void QueueStatusUpdate()
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(ProcessPendingStatusUpdate));
        }

        private void ProcessPendingStatusUpdate()
        {
            string messageToShow;
            bool shouldQueueUpdate = false;

            lock (_syncLock)
            {
                messageToShow = _latestMessage;
                _updateQueued = false;
            }

            LogStatusBarProvider.Log = messageToShow;

            lock (_syncLock)
            {
                if (!_updateQueued && !string.Equals(messageToShow, _latestMessage, StringComparison.Ordinal))
                {
                    _updateQueued = true;
                    shouldQueueUpdate = true;
                }
            }

            if (shouldQueueUpdate)
            {
                QueueStatusUpdate();
            }
        }
    }

    public class LogStatusBarProvider : ViewModelBase, IConfig
    {
        public static LogStatusBarProvider Instance => ConfigService.Instance.GetRequiredService<LogStatusBarProvider>();
        private TextAppender _textAppender;
        private Hierarchy _hierarchy;

        public LogStatusBarProvider()
        {
            _hierarchy = (Hierarchy)LogManager.GetRepository();
            _textAppender = new TextAppender(this);
            _textAppender.Layout = new PatternLayout("%message");
            if (IsShowLog)
            {
                _hierarchy.Root.AddAppender(_textAppender);
                log4net.Config.BasicConfigurator.Configure(_hierarchy);
            }
        }
        [JsonIgnore]
        public string Log { get => _Log; set { _Log = value; OnPropertyChanged(); } }
        private string _Log;

        public bool IsShowLog
        {
            get => _IsShowLog;
            set
            {
                if (_IsShowLog != value)
                {
                    _IsShowLog = value;
                    OnPropertyChanged();

                    if (_hierarchy == null)
                        _hierarchy = (Hierarchy)LogManager.GetRepository();

                    if (_textAppender == null)
                    {
                        _textAppender = new TextAppender(this);
                        _textAppender.Layout = new PatternLayout("%message");
                    }

                    if (_IsShowLog)
                    {
                        if (!_hierarchy.Root.Appenders.Cast<IAppender>().Contains(_textAppender))
                        {
                            _hierarchy.Root.AddAppender(_textAppender);
                        }
                        log4net.Config.BasicConfigurator.Configure(_hierarchy);
                    }
                    else
                    {
                        _hierarchy.Root.RemoveAppender(_textAppender);
                    }
                }
            }
        }
        private bool _IsShowLog;
    }





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
            _logTextView?.ApplySearchFilter(searchText);
        }
    }
}
