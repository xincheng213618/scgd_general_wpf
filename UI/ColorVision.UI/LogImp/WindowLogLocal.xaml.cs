using ColorVision.Themes;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// WindowLogLocal.xaml 的交互逻辑
    /// 用于显示外部程序生成的日志文件
    /// </summary>
    public partial class WindowLogLocal : Window
    {
        /// <summary>
        /// 日志文件路径
        /// </summary>
        public string LogFilePath { get; private set; }

        /// <summary>
        /// 配置实例
        /// </summary>
        public WindowLogLocalConfig Config => ConfigHandler.GetInstance().GetRequiredService<WindowLogLocalConfig>();

        private DispatcherTimer _refreshTimer;
        private long _lastReadPosition;
        private readonly object _fileLock = new object();

        /// <summary>
        /// 创建一个新的 WindowLogLocal 实例
        /// </summary>
        /// <param name="logFilePath">日志文件路径</param>
        public WindowLogLocal(string logFilePath)
        {
            LogFilePath = logFilePath;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            this.Title = $"Log - {Path.GetFileName(LogFilePath)}";

            SearchBar1Brush = SearchBar1.BorderBrush;

            this.Closed += (s, e) =>
            {
                _refreshTimer.Stop();
            };

            // 初始加载日志
            LoadLogFile();
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Config.RefreshIntervalMs)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;

            this.SizeChanged += (s, e) =>
            {
                ButtonAutoScrollToEnd.Visibility = this.ActualWidth > LogConstants.MinWidthForAutoScrollButton ? Visibility.Visible : Visibility.Collapsed;
                ButtonAutoRefresh.Visibility = this.ActualWidth > LogConstants.MinWidthForAutoRefreshButton ? Visibility.Visible : Visibility.Collapsed;
                SearchBar1.Visibility = this.ActualWidth > LogConstants.MinWidthForSearchBar ? Visibility.Visible : Visibility.Collapsed;
            };

            // 监听配置变化
            Config.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(WindowLogLocalConfig.RefreshIntervalMs))
                {
                    _refreshTimer.Interval = TimeSpan.FromMilliseconds(Config.RefreshIntervalMs);
                }
                else if (e.PropertyName == nameof(WindowLogLocalConfig.AutoRefresh))
                {
                    if (Config.AutoRefresh)
                        _refreshTimer.Start();
                    else
                        _refreshTimer.Stop();
                }
                else if (e.PropertyName == nameof(WindowLogLocalConfig.LogReverse))
                {
                    // 切换倒序模式时重新加载日志
                    _lastReadPosition = 0;
                    LoadLogFile();
                }
            };
            // 启动自动刷新
            if (Config.AutoRefresh)
            {
                _refreshTimer.Start();
            }
        }

        /// <summary>
        /// GB2312编码
        /// </summary>
        private static readonly Encoding GB2312Encoding = Encoding.GetEncoding("GB2312");

        /// <summary>
        /// 加载日志文件内容
        /// </summary>
        private void LoadLogFile()
        {
            if (!File.Exists(LogFilePath))
            {
                logTextBox.Text = $"File not found: {LogFilePath}";
                return;
            }

            try
            {
                lock (_fileLock)
                {
                    using var fileStream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(fileStream, GB2312Encoding);

                    var lines = new List<string>();
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }

                    // 限制最大行数
                    if (Config.MaxLines > 0 && lines.Count > Config.MaxLines)
                    {
                        lines = lines.Skip(lines.Count - Config.MaxLines).ToList();
                    }

                    // 倒序模式：最新日志在顶部
                    if (Config.LogReverse)
                    {
                        lines.Reverse();
                    }

                    logTextBox.Text = string.Join(Environment.NewLine, lines);
                    _lastReadPosition = fileStream.Position;
                }

                if (Config.AutoScrollToEnd && !Config.LogReverse)
                {
                    logTextBox.ScrollToEnd();
                }
                else if (Config.LogReverse)
                {
                    logTextBox.ScrollToHome();
                }
            }
            catch (IOException ex)
            {
                logTextBox.Text = $"Error reading log file: {ex.Message}";
            }
            catch (Exception ex)
            {
                logTextBox.Text = $"An unexpected error occurred: {ex.Message}";
            }
        }

        /// <summary>
        /// 增量读取日志文件新增内容
        /// </summary>
        private void ReadNewLogContent()
        {
            if (!File.Exists(LogFilePath))
                return;

            try
            {
                lock (_fileLock)
                {
                    using var fileStream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    
                    // 检查文件是否被截断或重写
                    if (fileStream.Length < _lastReadPosition)
                    {
                        // 文件被截断，重新从头读取
                        _lastReadPosition = 0;
                        LoadLogFile();
                        return;
                    }

                    if (fileStream.Length == _lastReadPosition)
                    {
                        // 没有新内容
                        return;
                    }

                    fileStream.Seek(_lastReadPosition, SeekOrigin.Begin);
                    using var reader = new StreamReader(fileStream, GB2312Encoding);

                    var newLines = new List<string>();
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        newLines.Add(line);
                    }

                    if (newLines.Count > 0)
                    {
                        // 倒序模式：新内容插入到顶部
                        if (Config.LogReverse)
                        {
                            newLines.Reverse();
                            var newContent = string.Join(Environment.NewLine, newLines);
                            
                            if (!string.IsNullOrEmpty(logTextBox.Text))
                            {
                                logTextBox.Text = newContent + Environment.NewLine + logTextBox.Text;
                            }
                            else
                            {
                                logTextBox.Text = newContent;
                            }

                            // 检查并限制最大行数（倒序模式从底部截断）
                            EnforceMaxLinesReverse();

                            // 滚动到顶部显示最新内容
                            logTextBox.ScrollToHome();
                            if (logTextBoxSerch.Visibility == Visibility.Visible)
                            {
                                logTextBoxSerch.ScrollToHome();
                            }
                        }
                        else
                        {
                            var newContent = string.Join(Environment.NewLine, newLines);
                            
                            // 追加新内容
                            if (!string.IsNullOrEmpty(logTextBox.Text))
                            {
                                logTextBox.AppendText(Environment.NewLine + newContent);
                            }
                            else
                            {
                                logTextBox.Text = newContent;
                            }

                            // 检查并限制最大行数
                            EnforceMaxLines();

                            if (Config.AutoScrollToEnd)
                            {
                                logTextBox.ScrollToEnd();
                                if (logTextBoxSerch.Visibility == Visibility.Visible)
                                {
                                    logTextBoxSerch.ScrollToEnd();
                                }
                            }
                        }

                        // 更新搜索结果
                        if (!string.IsNullOrEmpty(SearchBar1.Text))
                        {
                            var newContent = string.Join(Environment.NewLine, newLines);
                            UpdateSearchResults(newContent);
                        }
                    }

                    _lastReadPosition = fileStream.Position;
                }
            }
            catch (IOException)
            {
                // 文件被其他程序锁定，忽略此次读取
            }
            catch (Exception)
            {
                // 忽略其他异常，避免影响定时器
            }
        }

        /// <summary>
        /// 限制显示的最大行数
        /// </summary>
        private void EnforceMaxLines()
        {
            if (Config.MaxLines <= 0) return;

            var lines = logTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            if (lines.Length > Config.MaxLines)
            {
                var trimmedLines = lines.Skip(lines.Length - Config.MaxLines);
                logTextBox.Text = string.Join(Environment.NewLine, trimmedLines);
            }
        }

        /// <summary>
        /// 限制显示的最大行数（倒序模式，从底部截断）
        /// </summary>
        private void EnforceMaxLinesReverse()
        {
            if (Config.MaxLines <= 0) return;

            var lines = logTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            if (lines.Length > Config.MaxLines)
            {
                var trimmedLines = lines.Take(Config.MaxLines);
                logTextBox.Text = string.Join(Environment.NewLine, trimmedLines);
            }
        }

        /// <summary>
        /// 更新搜索结果（增量添加匹配的新内容）
        /// </summary>
        private void UpdateSearchResults(string newContent)
        {
            var searchText = SearchBar1.Text.ToLower(CultureInfo.CurrentCulture);
            if (string.IsNullOrEmpty(searchText)) return;

            var containsRegexSpecialChars = RegexSpecialChars.Any(searchText.Contains);
            var newLines = newContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            string[] filteredLines;

            if (containsRegexSpecialChars)
            {
                try
                {
                    var regex = new Regex(searchText, RegexOptions.IgnoreCase);
                    filteredLines = newLines.Where(line => regex.IsMatch(line)).ToArray();
                }
                catch (RegexParseException)
                {
                    return;
                }
            }
            else
            {
                var keywords = searchText.Split(Chars, StringSplitOptions.RemoveEmptyEntries);
                filteredLines = newLines.Where(line => keywords.All(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToArray();
            }

            if (filteredLines.Length > 0)
            {
                var filteredContent = string.Join(Environment.NewLine, filteredLines);
                if (!string.IsNullOrEmpty(logTextBoxSerch.Text))
                {
                    logTextBoxSerch.AppendText(Environment.NewLine + filteredContent);
                }
                else
                {
                    logTextBoxSerch.Text = filteredContent;
                }
            }
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            if (!Config.AutoRefresh) return;
            ReadNewLogContent();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            _lastReadPosition = 0;
            LoadLogFile();
            
            // 重新应用搜索过滤
            if (!string.IsNullOrEmpty(SearchBar1.Text))
            {
                ApplySearchFilter();
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            logTextBox.Text = string.Empty;
            logTextBoxSerch.Text = string.Empty;
        }

        private readonly char[] Chars = new[] { ' ' };
        private static readonly string[] RegexSpecialChars = { ".", "*", "+", "?", "^", "$", "(", ")", "[", "]", "{", "}", "|", "\\" };
        private Brush? SearchBar1Brush;

        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        /// <summary>
        /// 应用搜索过滤
        /// </summary>
        private void ApplySearchFilter()
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
