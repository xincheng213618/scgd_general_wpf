#pragma warning disable CA1822
using ColorVision.UI.LogImp.Models;
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
        private readonly object _fileLock = new();
        private FileSystemWatcher? _fileWatcher;
        private int _fileChangePending;
        private bool _isDisposed;
        private LogTextViewController? _logTextView;

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
            LogViewer.MaxEntries = Config.MaxLines;

            _logTextView = new LogTextViewController(this, RootGrid, SearchPanel, SearchBar1, LogViewer, CloseSearchButton);
            _logTextView.ConfigureContextMenus(contextMenu =>
                LogTextViewMenuFactory.AppendLocalLogMenuItems(contextMenu, Config, RefreshLog, OpenLogFolder, ClearLog));

            this.Loaded += UserControl_Loaded;
            this.Unloaded += UserControl_Unloaded;

            LoadLogFile();
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(Config.RefreshIntervalMs)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;

            SetupFileWatcher();
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
                // FileSystemWatcher may fail on network paths; fall back to timer-only.
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
                LogViewer.MaxEntries = Config.MaxLines;
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
                LogViewer.SetMessage($"File not found: {LogFilePath}", LogEntryLevel.Warning);
                _lastReadPosition = 0;
                return;
            }

            try
            {
                lock (_fileLock)
                {
                    using var fileStream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(fileStream, Encoding);

                    LogViewer.MaxEntries = Config.MaxLines;
                    LogViewer.SetEntries(ReadDisplayEntries(reader), Config.LogReverse);
                    _lastReadPosition = fileStream.Length;
                }

                if (Config.AutoScrollToEnd || Config.LogReverse)
                {
                    LogViewer.ScrollToLatest(Config.LogReverse);
                }
            }
            catch (IOException ex)
            {
                LogViewer.SetMessage($"Error reading log file: {ex.Message}", LogEntryLevel.Error);
            }
            catch (Exception ex)
            {
                LogViewer.SetMessage($"An unexpected error occurred: {ex.Message}", LogEntryLevel.Error);
            }
        }

        private List<LogEntry> ReadDisplayEntries(StreamReader reader)
        {
            if (Config.MaxLines <= 0)
            {
                return LogEntryParser.FromLines(ReadAllLines(reader));
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

            return LogEntryParser.FromLines(tailLines);
        }

        private static IEnumerable<string> ReadAllLines(TextReader reader)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }

        /// <summary>
        /// Incrementally read new content appended to the log file.
        /// </summary>
        private void ReadNewLogContent()
        {
            if (!File.Exists(LogFilePath))
                return;

            List<LogEntry>? newEntries = null;
            var shouldReload = false;

            try
            {
                lock (_fileLock)
                {
                    using var fileStream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

                    if (fileStream.Length < _lastReadPosition)
                    {
                        _lastReadPosition = 0;
                        shouldReload = true;
                    }
                    else if (fileStream.Length > _lastReadPosition)
                    {
                        fileStream.Seek(_lastReadPosition, SeekOrigin.Begin);
                        using var reader = new StreamReader(fileStream, Encoding, detectEncodingFromByteOrderMarks: false);

                        newEntries = LogEntryParser.FromLines(ReadAllLines(reader));
                        _lastReadPosition = fileStream.Length;
                    }
                }

                if (shouldReload)
                {
                    LoadLogFile();
                    return;
                }

                if (newEntries is { Count: > 0 })
                {
                    LogViewer.MaxEntries = Config.MaxLines;
                    LogViewer.AppendEntries(newEntries, Config.LogReverse, Config.AutoScrollToEnd);
                }
            }
            catch (IOException)
            {
            }
            catch (Exception)
            {
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
            LogViewer.Clear();
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
