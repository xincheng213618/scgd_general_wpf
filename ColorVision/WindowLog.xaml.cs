using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision
{
    public class WindowLogExport : MenuItemBase, IHotKey
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "WindowLog";
        public override int Order => 10005;
        public override string Header => Properties.Resources.Log;
        public override string InputGestureText => "Ctrl + F2";
        public HotKeys HotKeys => new(Properties.Resources.Log, new Hotkey(Key.F2, ModifierKeys.Control), Execute);
        public override void Execute()
        {
            new WindowLog() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }

    public class ExportLogOpen : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "LogOpen";
        public override int Order => 3;
        public override string Header => "打开日志文件夹(_L)";
        public override void Execute()
        {
            var fileAppender = (log4net.Appender.FileAppender)LogManager.GetRepository().GetAppenders().FirstOrDefault(a => a is log4net.Appender.FileAppender);
            if (fileAppender != null)
            {
                Process.Start("explorer.exe", $"{Path.GetDirectoryName(fileAppender.File)}");
            }
        }
    }


    public enum LogLoadState
    {
        AllToday,
        SinceStartup,
        None
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

        private void Window_Initialized(object sender, EventArgs e)
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            //hierarchy.Root.RemoveAllAppenders();
            // 创建一个输出到TextBox的Appender
            var textBoxAppender = new TextBoxAppender(logTextBox);

            // 设置布局格式
            var layout = new PatternLayout("%date [%thread] %-5level %logger %newline %message%newline");
            textBoxAppender.Layout = layout;
            // 将Appender添加到Logger中
            hierarchy.Root.AddAppender(textBoxAppender);

            // 配置并激活log4net
            log4net.Config.BasicConfigurator.Configure(hierarchy);
            this.DataContext = MainWindowConfig.Instance;
            cmlog.ItemsSource = MainWindowConfig.GetAllLevels().Select(level => new KeyValuePair<Level, string>(level, level.Name));
            SearchBar1Brush = SearchBar1.BorderBrush;

            cmlogLoadState.ItemsSource = Enum.GetValues(typeof(LogLoadState)).Cast<LogLoadState>().Select(state => new KeyValuePair<LogLoadState, string>(state, state.ToString()));

        }
        private static string GetLogFilePath()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            var fileAppender = hierarchy.Root.Appenders.OfType<RollingFileAppender>().FirstOrDefault();
            return fileAppender?.File;
        }

        private void cmlogLoadState_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            LoadLogHistory();
        }

        private void LoadLogHistory()
        {
            if (MainWindowConfig.Instance.LogLoadState == LogLoadState.None) return;


                var logFilePath = GetLogFilePath();
            if (logFilePath != null && File.Exists(logFilePath))
            {
                try
                {
                    using (FileStream fileStream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (StreamReader reader = new StreamReader(fileStream, Encoding.Default))
                    {
                        string line;
                        DateTime today = DateTime.Today;
                        DateTime startupTime = Process.GetCurrentProcess().StartTime;

                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            string timestampLine = line;
                            string logContentLine = reader.ReadLine(); // 读取日志内容行


                            if (DateTime.TryParseExact(timestampLine.Substring(0, 23), "yyyy-MM-dd HH:mm:ss,fff", null, System.Globalization.DateTimeStyles.None, out DateTime logTime))
                            {
                                if (MainWindowConfig.Instance.LogLoadState == LogLoadState.AllToday && logTime.Date != today)
                                {
                                    continue;
                                }
                                else if (MainWindowConfig.Instance.LogLoadState == LogLoadState.SinceStartup && logTime < startupTime)
                                {
                                    continue;
                                }
                            }

                            string logEntry = timestampLine + Environment.NewLine + logContentLine + Environment.NewLine;

                            if (MainWindowConfig.Instance.LogReserve)
                            {
                                logTextBox.Text = logEntry + logTextBox.Text;
                            }
                            else
                            {
                                logTextBox.AppendText(logEntry);
                            }
                        }
                    }
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"Error reading log file: {ex.Message}");
                }
                catch 
                {
                    
                }
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
                var logLines = logTextBox.Text.Split(new[] { Environment.NewLine }, System.StringSplitOptions.None);
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
            if (!MainWindowConfig.Instance.AutoRefresh) return;
            var renderedMessage = RenderLoggingEvent(loggingEvent);
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MainWindowConfig.Instance.LogReserve)
                {
                    _textBox.Text = renderedMessage + _textBox.Text;
                }
                else
                {
                    _textBox.AppendText(renderedMessage);
                    if (MainWindowConfig.Instance.AutoScrollToEnd)
                        _textBox.ScrollToEnd();
                }
            });
        }
    }
}
