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
                        logTextBox.Text = LogHistoryReader.ReadDisplayText(
                            reader,
                            LogConfig.Instance.LogLoadState,
                            LogConfig.Instance.LogReserve,
                            LogConfig.Instance.MaxChars);
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
