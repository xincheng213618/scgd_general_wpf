using ColorVision.Common.MVVM;
using ColorVision.Themes;
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
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI
{

    public class TextAppender : AppenderSkeleton
    {
        private LogStatusBarProvider LogStatusBarProvider;
        public TextAppender(LogStatusBarProvider logStatusBarProvider)
        {
            LogStatusBarProvider = logStatusBarProvider;
        }
        protected override void Append(LoggingEvent loggingEvent)
        {
            var renderedMessage = RenderLoggingEvent(loggingEvent);
            string messageToShow = renderedMessage.Length > 10 ? string.Concat(renderedMessage.AsSpan(0, 10), "...") : renderedMessage;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                LogStatusBarProvider.Log = messageToShow;
            });
        }
    }

    public class LogStatusBarProvider : ViewModelBase, IConfig,IStatusBarProvider
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
        public string Log { get => _Log; set { _Log = value; NotifyPropertyChanged(); } }
        private string _Log;

        public bool IsShowLog
        {
            get => _IsShowLog;
            set
            {
                if (_IsShowLog != value)
                {
                    _IsShowLog = value;
                    NotifyPropertyChanged();

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


        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            return new List<StatusBarMeta>
            {
                new StatusBarMeta()
                {
                    Name = "Log",
                    Description = "Log",
                    Order = 11,
                    Type = StatusBarType.Text,
                    BindingName = nameof(Log),
                    VisibilityBindingName = nameof(IsShowLog),
                    Source = Instance
                }
            };
        }
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
        private void Window_Initialized(object sender, EventArgs e)
        {
            Hierarchy = (Hierarchy)LogManager.GetRepository();
            TextBoxAppender = new TextBoxAppender(logTextBox);
            TextBoxAppender.Layout = new PatternLayout("%date [%thread] %-5level %logger %  %message%newline");
            Hierarchy.Root.AddAppender(TextBoxAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);

            this.Closed += (s, e) =>
            {
                Hierarchy.Root.RemoveAppender(TextBoxAppender);
                log4net.Config.BasicConfigurator.Configure(Hierarchy);
            };
            this.DataContext = LogConfig.Instance;

            cmlog.ItemsSource = LogConfig.GetAllLevels().Select(level => new KeyValuePair<Level, string>(level, level.Name));
            SearchBar1Brush = SearchBar1.BorderBrush;

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
            StringBuilder logBuilder = new StringBuilder();

            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                string timestampLine = line;
                string logContentLine = reader.ReadLine(); // 读取日志内容行

                if (timestampLine.Length>23 && DateTime.TryParseExact(timestampLine.Substring(0, 23), "yyyy-MM-dd HH:mm:ss,fff", null, DateTimeStyles.None, out DateTime logTime))
                {
                    if (logLoadState == LogLoadState.AllToday && logTime.Date != today)
                    {
                        continue;
                    }
                    else if (logLoadState == LogLoadState.SinceStartup && logTime < startupTime)
                    {
                        continue;
                    }
                }
                else
                {
                    // 如果时间解析失败，跳过当前日志条目
                    continue;
                }

                // 找到符合条件的日志条目后，读取并添加后续所有日志条目
                logBuilder.AppendLine(timestampLine);
                logBuilder.AppendLine(logContentLine);

                while ((line = reader.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    logBuilder.AppendLine(line);
                    logContentLine = reader.ReadLine(); // 读取日志内容行
                    if (!string.IsNullOrWhiteSpace(logContentLine))
                    {
                        logBuilder.AppendLine(logContentLine);
                    }
                }

                break; // 退出外层循环
            }

            // 将日志内容添加到日志文 框中
            if (logReserve)
            {
                logTextBox.Text = logBuilder.ToString() + logTextBox.Text;
            }
            else
            {
                logTextBox.AppendText(logBuilder.ToString());
            }
        }

        private void cmlog_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedLevel = (KeyValuePair<Level, string>)cmlog.SelectedItem;
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            if (selectedLevel.Key != hierarchy.Root.Level)
            {
                hierarchy.Root.Level = selectedLevel.Key;
                log4net.Config.BasicConfigurator.Configure(hierarchy);
                log.Info("更新Log4Net 日志级别：" + selectedLevel.Value);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            logTextBox.Text = string.Empty;
            logTextBoxSerch.Text = string.Empty;
        }


        private readonly char[] Chars = new[] { ' ' };
        private static readonly string[] RegexSpecialChars = { ".", "*", "+", "?", "^", "$", "(", ")", "[", "]", "{", "}", "|", "\\" };
        private Brush SearchBar1Brush;
        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchBar1.Text.ToLower(CultureInfo.CurrentCulture);
            if (!string.IsNullOrEmpty(searchText))
            {
                var containsRegexSpecialChars = RegexSpecialChars.Any(searchText.Contains);

                var keywords = searchText.Split(Chars, StringSplitOptions.RemoveEmptyEntries);

                logTextBox.Visibility = Visibility.Collapsed;
                logTextBoxSerch.Visibility = Visibility.Visible;
                var logLines = logTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                if (containsRegexSpecialChars)
                {
                    // 使用正则表达式搜索
                    try
                    {
                        var regex = new Regex(searchText, RegexOptions.IgnoreCase);
                        var filteredLines = logLines.Where(line => regex.IsMatch(line)).ToArray();
                        logTextBoxSerch.Text = string.Join(Environment.NewLine, filteredLines);
                    }
                    catch (RegexParseException)
                    {
                        SearchBar1.BorderBrush = Brushes.Red;
                        return;
                    }
                }
                else
                {
                    var filteredLines = logLines.Where(line => keywords.All(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToArray();
                    logTextBoxSerch.Text = string.Join(Environment.NewLine, filteredLines);
                }
                SearchBar1.BorderBrush = SearchBar1Brush;
            }
            else
            {
                logTextBoxSerch.Visibility = Visibility.Collapsed;
                logTextBox.Visibility = Visibility.Visible;
            }
        }


    }
    public class TextBoxAppender : AppenderSkeleton
    {
        public TextBoxAppender(TextBox textBox)
        {
            _textBox = textBox;
        }

        private TextBox _textBox;
        protected override void Append(LoggingEvent loggingEvent)
        {
            if (!LogConfig.Instance.AutoRefresh) return;
            var renderedMessage = RenderLoggingEvent(loggingEvent);
            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                if (LogConfig.Instance.LogReserve)
                {
                    _textBox.Text = renderedMessage + _textBox.Text;
                }
                else
                {
                    _textBox.AppendText(renderedMessage);
                    if (LogConfig.Instance.AutoScrollToEnd)  
                        _textBox.ScrollToEnd();
                }
            });
        }
    }
}
