using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// Embeddable UserControl for displaying external log files with auto-refresh.
    /// This is the core log-file-monitoring component that can be hosted in DockingManager panels
    /// or any other container. WindowLogLocal uses this control as its content.
    /// </summary>
    public partial class LogLocalOutput : UserControl
    {
        /// <summary>
        /// Log file path being monitored.
        /// </summary>
        public string LogFilePath { get; private set; }

        /// <summary>
        /// Configuration instance (shared with WindowLogLocal).
        /// </summary>
        public WindowLogLocalConfig Config => ConfigHandler.GetInstance().GetRequiredService<WindowLogLocalConfig>();

        private DispatcherTimer _refreshTimer;
        private long _lastReadPosition;
        private readonly object _fileLock = new object();

        /// <summary>
        /// File encoding (defaults to system default; use GB2312 for C++ logs on Chinese Windows).
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.Default;

        /// <summary>
        /// Create a new LogLocalOutput instance for the specified log file.
        /// </summary>
        /// <param name="logFilePath">Path to the log file to monitor</param>
        /// <param name="encoding">Optional encoding override</param>
        public LogLocalOutput(string logFilePath, Encoding? encoding = null)
        {
            LogFilePath = logFilePath;
            if (encoding != null)
            {
                Encoding = encoding;
            }
            InitializeComponent();
            this.SizeChanged += (s, e) =>
            {
                ButtonAutoScrollToEnd.Visibility = this.ActualWidth > LogConstants.MinWidthForAutoScrollButton ? Visibility.Visible : Visibility.Collapsed;
                ButtonAutoRefresh.Visibility = this.ActualWidth > LogConstants.MinWidthForAutoRefreshButton ? Visibility.Visible : Visibility.Collapsed;
                SearchBar1.Visibility = this.ActualWidth > LogConstants.MinWidthForSearchBar ? Visibility.Visible : Visibility.Collapsed;
            };
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;

            SearchBar1Brush = SearchBar1.BorderBrush;

            this.Unloaded += (s, e) =>
            {
                _refreshTimer?.Stop();
            };

            // Initial load
            LoadLogFile();
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Config.RefreshIntervalMs)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;

            // Listen for config changes
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
                    _lastReadPosition = 0;
                    LoadLogFile();
                }
            };

            if (Config.AutoRefresh)
            {
                _refreshTimer.Start();
            }
        }

        /// <summary>
        /// Load the full log file content.
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
                    using var reader = new StreamReader(fileStream, Encoding);

                    var lines = new List<string>();
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }

                    if (Config.MaxLines > 0 && lines.Count > Config.MaxLines)
                    {
                        lines = lines.Skip(lines.Count - Config.MaxLines).ToList();
                    }

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
        /// Incrementally read new content appended to the log file.
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

                    if (fileStream.Length < _lastReadPosition)
                    {
                        _lastReadPosition = 0;
                        LoadLogFile();
                        return;
                    }

                    if (fileStream.Length == _lastReadPosition)
                    {
                        return;
                    }

                    fileStream.Seek(_lastReadPosition, SeekOrigin.Begin);
                    using var reader = new StreamReader(fileStream, Encoding);

                    var newLines = new List<string>();
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        newLines.Add(line);
                    }

                    if (newLines.Count > 0)
                    {
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

                            EnforceMaxLinesReverse();
                            logTextBox.ScrollToHome();
                            if (logTextBoxSerch.Visibility == Visibility.Visible)
                            {
                                logTextBoxSerch.ScrollToHome();
                            }
                        }
                        else
                        {
                            var newContent = string.Join(Environment.NewLine, newLines);

                            if (!string.IsNullOrEmpty(logTextBox.Text))
                            {
                                logTextBox.AppendText(Environment.NewLine + newContent);
                            }
                            else
                            {
                                logTextBox.Text = newContent;
                            }

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
            }
            catch (Exception)
            {
            }
        }

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

        private void UpdateSearchResults(string newContent)
        {
            var searchText = SearchBar1.Text.ToLower(CultureInfo.CurrentCulture);
            if (string.IsNullOrEmpty(searchText)) return;

            var newLines = newContent.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            if (!LogSearchHelper.FilterLines(searchText, newLines, out var filteredLines))
                return;

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

        private Brush? SearchBar1Brush;

        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            var searchText = SearchBar1.Text.ToLower(CultureInfo.CurrentCulture);
            if (!string.IsNullOrEmpty(searchText))
            {
                logTextBox.Visibility = Visibility.Collapsed;
                logTextBoxSerch.Visibility = Visibility.Visible;
                var logLines = logTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                if (!LogSearchHelper.FilterLines(searchText, logLines, out var filteredLines))
                {
                    SearchBar1.BorderBrush = Brushes.Red;
                    return;
                }
                logTextBoxSerch.Text = string.Join(Environment.NewLine, filteredLines);
                SearchBar1.BorderBrush = SearchBar1Brush;
            }
            else
            {
                logTextBoxSerch.Visibility = Visibility.Collapsed;
                logTextBox.Visibility = Visibility.Visible;
            }
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            Common.Utilities.PlatformHelper.OpenFolderAndSelectFile(LogFilePath);
        }
    }
}
