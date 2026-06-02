#pragma warning disable CA1822
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// Embeddable UserControl for displaying external log files with auto-refresh.
    /// This is the core log-file-monitoring component that can be hosted in DockingManager panels
    /// or any other container. WindowLogLocal uses this control as its content.
    /// </summary>
    public partial class LogLocalOutput : UserControl, IDisposable
    {
        /// <summary>
        /// Log file path being monitored.
        /// </summary>
        public string LogFilePath { get; private set; }

        /// <summary>
        /// Configuration instance (shared with WindowLogLocal).
        /// </summary>
        public WindowLogLocalConfig Config => ConfigHandler.GetInstance().GetRequiredService<WindowLogLocalConfig>();

        private DispatcherTimer? _refreshTimer;
        private long _lastReadPosition;
        private readonly object _fileLock = new object();
        private FileSystemWatcher? _fileWatcher;
        private int _fileChangePending;
        private bool _isDisposed;
        private LogTextViewController? _logTextView;
        private readonly List<string> _displayLines = new();

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
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;

            _logTextView = new LogTextViewController(this, RootGrid, SearchPanel, SearchBar1, logTextBox, logTextBoxSerch, CloseSearchButton);
            _logTextView.ConfigureContextMenus((contextMenu, _) =>
                LogTextViewMenuFactory.AppendLocalLogMenuItems(contextMenu, Config, RefreshLog, OpenLogFolder, ClearLog));

            this.Loaded += UserControl_Loaded;
            this.Unloaded += UserControl_Unloaded;

            // Initial load
            LoadLogFile();
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Config.RefreshIntervalMs)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;

            // Setup FileSystemWatcher for immediate file change detection
            SetupFileWatcher();

            // Listen for config changes
            Config.PropertyChanged += Config_PropertyChanged;

            EnableFileWatcher(false);
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isDisposed)
                return;

            if (Config.AutoRefresh)
            {
                _refreshTimer?.Start();
                EnableFileWatcher(true);
                // Catch up on any changes missed while unloaded
                ReadNewLogContent();
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer?.Stop();
            EnableFileWatcher(false);
        }

        private void SetupFileWatcher()
        {
            try
            {
                var dir = Path.GetDirectoryName(LogFilePath);
                var fileName = Path.GetFileName(LogFilePath);
                if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(fileName)) return;
                if (!Directory.Exists(dir)) return;

                _fileWatcher = new FileSystemWatcher(dir, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = false
                };
                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.Created += OnFileChanged;
            }
            catch
            {
                // FileSystemWatcher may fail on network paths; fall back to timer-only
            }
        }

        private void EnableFileWatcher(bool enable)
        {
            if (_fileWatcher != null)
            {
                try { _fileWatcher.EnableRaisingEvents = enable; }
                catch { /* path may no longer be valid */ }
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (Interlocked.Exchange(ref _fileChangePending, 1) == 1) return;

            Dispatcher.BeginInvoke(() =>
            {
                Interlocked.Exchange(ref _fileChangePending, 0);
                if (Config.AutoRefresh)
                {
                    ReadNewLogContent();
                }
            }, DispatcherPriority.Background);
        }

        private void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(WindowLogLocalConfig.RefreshIntervalMs))
            {
                if (_refreshTimer != null)
                    _refreshTimer.Interval = TimeSpan.FromMilliseconds(Config.RefreshIntervalMs);
            }
            else if (e.PropertyName == nameof(WindowLogLocalConfig.AutoRefresh))
            {
                if (Config.AutoRefresh)
                {
                    _refreshTimer?.Start();
                    EnableFileWatcher(true);
                }
                else
                {
                    _refreshTimer?.Stop();
                    EnableFileWatcher(false);
                }
            }
            else if (e.PropertyName == nameof(WindowLogLocalConfig.LogReverse))
            {
                ReloadLogFile(refreshSearch: true);
            }
            else if (e.PropertyName == nameof(WindowLogLocalConfig.MaxLines))
            {
                ReloadLogFile(refreshSearch: true);
            }
        }

        private void ReloadLogFile(bool refreshSearch)
        {
            _lastReadPosition = 0;
            LoadLogFile();
            if (refreshSearch && !string.IsNullOrEmpty(SearchBar1.Text))
            {
                ApplySearchFilter();
            }
        }

        /// <summary>
        /// Load the full log file content.
        /// </summary>
        private void LoadLogFile()
        {
            if (!File.Exists(LogFilePath))
            {
                _displayLines.Clear();
                logTextBox.Text = $"File not found: {LogFilePath}";
                return;
            }

            try
            {
                lock (_fileLock)
                {
                    using var fileStream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(fileStream, Encoding);

                    var lines = ReadDisplayLines(reader);
                    if (Config.LogReverse)
                    {
                        lines.Reverse();
                    }

                    SetDisplayLines(lines);
                    logTextBox.Text = RenderDisplayLines();
                    _lastReadPosition = fileStream.Length;
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
                _displayLines.Clear();
                logTextBox.Text = $"Error reading log file: {ex.Message}";
            }
            catch (Exception ex)
            {
                _displayLines.Clear();
                logTextBox.Text = $"An unexpected error occurred: {ex.Message}";
            }
        }

        private List<string> ReadDisplayLines(StreamReader reader)
        {
            if (Config.MaxLines <= 0)
            {
                var allLines = new List<string>();
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    allLines.Add(line);
                }

                return allLines;
            }

            var tailLines = new Queue<string>(Config.MaxLines);
            string? tailLine;
            while ((tailLine = reader.ReadLine()) != null)
            {
                if (tailLines.Count == Config.MaxLines)
                {
                    tailLines.Dequeue();
                }

                tailLines.Enqueue(tailLine);
            }

            return tailLines.ToList();
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
                    using var reader = new StreamReader(fileStream, Encoding, detectEncodingFromByteOrderMarks: false);

                    var newLines = new List<string>();
                    string? line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        newLines.Add(line);
                    }

                    if (newLines.Count > 0)
                    {
                        var displayTrimmed = false;
                        if (Config.LogReverse)
                        {
                            newLines.Reverse();
                            displayTrimmed = PrependDisplayLines(newLines);
                            logTextBox.ScrollToHome();
                            if (logTextBoxSerch.Visibility == Visibility.Visible)
                            {
                                logTextBoxSerch.ScrollToHome();
                            }
                        }
                        else
                        {
                            displayTrimmed = AppendDisplayLines(newLines);

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
                            if (displayTrimmed)
                            {
                                ApplySearchFilter();
                            }
                            else
                            {
                                UpdateSearchResults(newLines);
                            }
                        }
                    }

                    _lastReadPosition = fileStream.Length;
                }
            }
            catch (IOException)
            {
            }
            catch (Exception)
            {
            }
        }

        private void SetDisplayLines(IEnumerable<string> lines)
        {
            _displayLines.Clear();
            _displayLines.AddRange(lines);
        }

        private bool AppendDisplayLines(IReadOnlyList<string> newLines)
        {
            var hadContent = _displayLines.Count > 0;
            _displayLines.AddRange(newLines);

            if (TrimDisplayLines(keepHead: false))
            {
                logTextBox.Text = RenderDisplayLines();
                return true;
            }

            var newContent = string.Join(Environment.NewLine, newLines);
            if (hadContent)
            {
                logTextBox.AppendText(Environment.NewLine + newContent);
            }
            else
            {
                logTextBox.Text = newContent;
            }

            return false;
        }

        private bool PrependDisplayLines(IReadOnlyList<string> newLines)
        {
            _displayLines.InsertRange(0, newLines);
            var trimmed = TrimDisplayLines(keepHead: true);
            logTextBox.Text = RenderDisplayLines();
            return trimmed;
        }

        private bool TrimDisplayLines(bool keepHead)
        {
            if (Config.MaxLines <= 0 || _displayLines.Count <= Config.MaxLines)
            {
                return false;
            }

            var removeCount = _displayLines.Count - Config.MaxLines;
            if (keepHead)
            {
                _displayLines.RemoveRange(Config.MaxLines, removeCount);
            }
            else
            {
                _displayLines.RemoveRange(0, removeCount);
            }

            return true;
        }

        private string RenderDisplayLines()
        {
            return string.Join(Environment.NewLine, _displayLines);
        }

        private void UpdateSearchResults(IReadOnlyList<string> newLines)
        {
            var searchText = LogViewUiHelper.NormalizeSearchText(SearchBar1.Text);
            if (string.IsNullOrEmpty(searchText)) return;

            if (!LogSearchHelper.FilterLines(searchText, newLines, out var filteredLines))
                return;

            if (filteredLines.Length > 0)
            {
                var filteredContent = string.Join(Environment.NewLine, filteredLines);
                if (Config.LogReverse)
                {
                    logTextBoxSerch.Text = string.IsNullOrEmpty(logTextBoxSerch.Text)
                        ? filteredContent
                        : filteredContent + Environment.NewLine + logTextBoxSerch.Text;
                    logTextBoxSerch.ScrollToHome();
                }
                else if (!string.IsNullOrEmpty(logTextBoxSerch.Text))
                {
                    logTextBoxSerch.AppendText(Environment.NewLine + filteredContent);
                    if (Config.AutoScrollToEnd)
                    {
                        logTextBoxSerch.ScrollToEnd();
                    }
                }
                else
                {
                    logTextBoxSerch.Text = filteredContent;
                    if (Config.AutoScrollToEnd)
                    {
                        logTextBoxSerch.ScrollToEnd();
                    }
                }
            }
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            if (!Config.AutoRefresh) return;
            ReadNewLogContent();
        }

        private void RefreshLog()
        {
            _lastReadPosition = 0;
            LoadLogFile();

            if (!string.IsNullOrEmpty(SearchBar1.Text))
            {
                ApplySearchFilter();
            }
        }

        private void ClearLog()
        {
            _displayLines.Clear();
            logTextBox.Text = string.Empty;
            logTextBoxSerch.Text = string.Empty;
        }

        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            QueueSearchFilter();
        }

        private void ApplySearchFilter()
        {
            var searchText = LogViewUiHelper.NormalizeSearchText(SearchBar1.Text);
            _logTextView?.ApplySearchFilter(searchText);
        }

        private void QueueSearchFilter()
        {
            var searchText = LogViewUiHelper.NormalizeSearchText(SearchBar1.Text);
            _logTextView?.QueueSearchFilter(searchText);
        }

        private void OpenLogFolder()
        {
            Common.Utilities.PlatformHelper.OpenFolderAndSelectFile(LogFilePath);
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            _logTextView?.Detach();
            Loaded -= UserControl_Loaded;
            Unloaded -= UserControl_Unloaded;
            Config.PropertyChanged -= Config_PropertyChanged;

            if (_refreshTimer != null)
            {
                _refreshTimer.Stop();
                _refreshTimer.Tick -= RefreshTimer_Tick;
                _refreshTimer = null;
            }

            if (_fileWatcher != null)
            {
                try
                {
                    _fileWatcher.EnableRaisingEvents = false;
                }
                catch
                {
                }

                _fileWatcher.Changed -= OnFileChanged;
                _fileWatcher.Created -= OnFileChanged;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
