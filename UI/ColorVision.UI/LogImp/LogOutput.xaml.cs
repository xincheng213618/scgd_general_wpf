﻿using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// LogOutput.xaml 的交互逻辑
    /// </summary>
    public partial class LogOutput : UserControl,IDisposable
    {
        public LogOutput()
        {
            InitializeComponent();
        }
        TextBoxAppender TextBoxAppender { get; set; }
        Hierarchy Hierarchy { get; set; }

        public void Dispose()
        {
            Hierarchy.Root.RemoveAppender(TextBoxAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);
            GC.SuppressFinalize(this);
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            Hierarchy = (Hierarchy)LogManager.GetRepository();
            TextBoxAppender = new TextBoxAppender(logTextBox);
            TextBoxAppender.Layout = new PatternLayout("%date [%thread] %-5level %logger %  %message%newline");
            Hierarchy.Root.AddAppender(TextBoxAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);

            this.DataContext = LogConfig.Instance;

            cmlog.ItemsSource = LogConfig.GetAllLevels().Select(level => new KeyValuePair<Level, string>(level, level.Name));

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

                if (timestampLine.Length > 23 && DateTime.TryParseExact(timestampLine.Substring(0, 23), "yyyy-MM-dd HH:mm:ss,fff", null, DateTimeStyles.None, out DateTime logTime))
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
}
