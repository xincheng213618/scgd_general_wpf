#pragma warning disable CA1822
using ColorVision.UI.LogImp.Models;
using Microsoft.Win32.SafeHandles;
using System.IO;
using System.Runtime.InteropServices;
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
        private readonly Func<string, FileStream> _openFile;
        private ReadCursor _cursor;
        private Encoding _encoding = Encoding.Default;
        private CancellationTokenSource? _activeRead;
        private Task _readCompletion = Task.CompletedTask;
        private int _readVersion;
        private int _clearVersion;
        private int _requestedClearVersion;
        private bool _readRequested;
        private bool _reloadRequested;
        private bool _suspended;
        private FileSystemWatcher? _fileWatcher;
        private int _fileChangePending;
        private bool _isDisposed;
        private LogTextViewController? _logTextView;

        /// <summary>
        /// File encoding (defaults to system default; use GB2312 for C++ logs on Chinese Windows).
        /// </summary>
        public Encoding Encoding
        {
            get => _encoding;
            set
            {
                Dispatcher.VerifyAccess();
                _encoding = value;
                _cursor = default;
                if (_activeRead != null) _ = QueueRead(reload: true);
            }
        }

        internal Task ReadCompletion => _readCompletion;

        /// <summary>
        /// Create a new LogLocalOutput instance for the specified log file.
        /// </summary>
        /// <param name="logFilePath">Path to the log file to monitor</param>
        /// <param name="encoding">Optional encoding override</param>
        public LogLocalOutput(string logFilePath, Encoding? encoding = null)
            : this(logFilePath, encoding, path => new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
        }

        internal LogLocalOutput(string logFilePath, Encoding? encoding, Func<string, FileStream> openFile)
        {
            LogFilePath = logFilePath;
            _openFile = openFile;
            if (encoding != null)
            {
                _encoding = encoding;
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

            _ = QueueRead(reload: true);
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

            _suspended = false;
            if (_cursor.Encoding == null && !_readRequested && (_activeRead == null || _activeRead.IsCancellationRequested))
                _ = QueueRead(reload: true);
            if (Config.AutoRefresh)
            {
                _refreshTimer?.Start();
                EnableFileWatcher(true);
                if (_cursor.Encoding != null) _ = ReadNewLogContent();
            }
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _refreshTimer?.Stop();
            EnableFileWatcher(false);
            _suspended = true;
            CancelQueuedRead();
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
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = false
                };
                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.Created += OnFileChanged;
                _fileWatcher.Renamed += OnFileChanged;
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
                    _ = ReadNewLogContent();
                }
            }, DispatcherPriority.Background);
        }

        private void Config_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(() => Config_PropertyChanged(sender, e), DispatcherPriority.Background);
                return;
            }
            if (_isDisposed) return;

            if (e.PropertyName == nameof(WindowLogLocalConfig.RefreshIntervalMs))
            {
                if (_refreshTimer != null)
                    _refreshTimer.Interval = TimeSpan.FromMilliseconds(Config.RefreshIntervalMs);
            }
            else if (e.PropertyName == nameof(WindowLogLocalConfig.AutoRefresh))
            {
                if (Config.AutoRefresh && IsLoaded)
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
                _ = QueueRead(reload: true);
            }
            else if (e.PropertyName == nameof(WindowLogLocalConfig.MaxLines))
            {
                LogViewer.MaxEntries = Config.MaxLines;
                _ = QueueRead(reload: true);
            }
        }

        internal Task ReadNewLogContent() => QueueRead(reload: false);

        private Task QueueRead(bool reload)
        {
            Dispatcher.VerifyAccess();
            if (_isDisposed) return _readCompletion;
            if (reload)
            {
                _readVersion++;
                _activeRead?.Cancel();
                _cursor = default;
                _reloadRequested = true;
            }
            if (_suspended) return _readCompletion;
            if (reload || !_readRequested) _requestedClearVersion = _clearVersion;
            _readRequested = true;
            if (_readCompletion.IsCompleted)
                _readCompletion = Dispatcher.InvokeAsync(ReadQueuedAsync, DispatcherPriority.Background).Task.Unwrap();
            return _readCompletion;
        }

        private void CancelQueuedRead()
        {
            _readVersion++;
            _readRequested = false;
            _reloadRequested = false;
            _activeRead?.Cancel();
        }

        private async Task ReadQueuedAsync()
        {
            while (_readRequested)
            {
                bool reload = _reloadRequested;
                int readVersion = _readVersion;
                int clearVersion = _requestedClearVersion;
                ReadCursor cursor = _cursor;
                Encoding encoding = _encoding;
                int maxLines = Config.MaxLines;
                _readRequested = false;
                _reloadRequested = false;
                using var request = new CancellationTokenSource();
                _activeRead = request;
                try
                {
                    ReadResult read = await Task.Run(() => ReadFile(cursor, encoding, maxLines, reload, request.Token), request.Token);
                    if (_isDisposed || readVersion != _readVersion) continue;

                    // Clear suppresses this batch but retains its cursor for the next appended bytes.
                    _cursor = read.Cursor;
                    if (clearVersion != _clearVersion || read.Entries == null) continue;
                    LogViewer.MaxEntries = Config.MaxLines;
                    if (read.Replace)
                    {
                        LogViewer.SetEntries(read.Entries, Config.LogReverse);
                        if (!string.IsNullOrEmpty(SearchBar1.Text)) ApplySearchFilter();
                    }
                    else LogViewer.AppendEntries(read.Entries, Config.LogReverse, Config.AutoScrollToEnd);
                }
                catch (OperationCanceledException) when (request.IsCancellationRequested) { }
                finally { _activeRead = null; }
            }
        }

        private readonly record struct ReadCursor(long Position, (ulong Volume, ulong Low, ulong High)? Identity, Encoding? Encoding);
        private sealed record ReadResult(ReadCursor Cursor, IReadOnlyList<LogEntry>? Entries = null, bool Replace = false);

        private ReadResult ReadFile(ReadCursor cursor, Encoding encoding, int maxLines, bool reload, CancellationToken token)
        {
            try
            {
                if (!File.Exists(LogFilePath))
                    return reload ? new(default, [new($"File not found: {LogFilePath}", LogEntryLevel.Warning)], Replace: true) : new(default);

                using FileStream file = _openFile(LogFilePath);
                var identity = GetFileIdentity(file.SafeFileHandle);
                long length = file.Length;
                using var snapshot = new LogReadStream(file, length, token);
                if (reload || cursor.Encoding == null || length < cursor.Position || (length > 0 && cursor.Position == 0)
                    || (cursor.Identity.HasValue && identity.HasValue && cursor.Identity != identity))
                {
                    var entries = ReadDisplayEntries(file, snapshot, length, maxLines, encoding, token, out Encoding effectiveEncoding);
                    return new(new(length, identity, effectiveEncoding), entries, Replace: true);
                }

                identity ??= cursor.Identity;
                if (length == cursor.Position) return new(cursor with { Identity = identity });
                file.Position = cursor.Position;
                using var reader = new StreamReader(snapshot, cursor.Encoding, detectEncodingFromByteOrderMarks: false);
                return new(new(length, identity, cursor.Encoding), LogEntryParser.FromLines(ReadAllLines(reader)));
            }
            catch (IOException ex)
            {
                return reload ? new(cursor, [new($"Error reading log file: {ex.Message}", LogEntryLevel.Error)], Replace: true) : new(cursor);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return reload ? new(cursor, [new($"An unexpected error occurred: {ex.Message}", LogEntryLevel.Error)], Replace: true) : new(cursor);
            }
        }

        private static List<LogEntry> ReadDisplayEntries(FileStream file, Stream snapshot, long length, int maxLines,
            Encoding fallbackEncoding, CancellationToken token, out Encoding encoding)
        {
            using (var probe = new StreamReader(snapshot, fallbackEncoding, detectEncodingFromByteOrderMarks: true, bufferSize: 128, leaveOpen: true))
            {
                _ = probe.Peek();
                encoding = probe.CurrentEncoding;
            }
            long tailStart = maxLines > 0 && encoding.CodePage is not (1200 or 1201 or 12000 or 12001)
                ? FindTailStart(file, length, maxLines, token)
                : 0;
            file.Position = tailStart;
            using var reader = new StreamReader(snapshot, encoding, detectEncodingFromByteOrderMarks: tailStart == 0, leaveOpen: true);
            if (maxLines <= 0)
                return LogEntryParser.FromLines(ReadAllLines(reader));

            var tailLines = new Queue<string>(maxLines);
            string? tailLine;
            while ((tailLine = reader.ReadLine()) != null)
            {
                if (tailLines.Count == maxLines)
                {
                    tailLines.Dequeue();
                }

                tailLines.Enqueue(tailLine);
            }

            return LogEntryParser.FromLines(tailLines);
        }

        private static (ulong Volume, ulong Low, ulong High)? GetFileIdentity(SafeFileHandle handle)
        {
            const int fileIdInfo = 18;
            return GetFileInformationByHandleEx(handle, fileIdInfo, out FileIdInformation identity, (uint)Marshal.SizeOf<FileIdInformation>())
                ? (identity.VolumeSerialNumber, identity.FileIdLow, identity.FileIdHigh)
                : null;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FileIdInformation
        {
            public ulong VolumeSerialNumber;
            public ulong FileIdLow;
            public ulong FileIdHigh;
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle, int informationClass, out FileIdInformation information, uint size);

        private static long FindTailStart(FileStream fileStream, long length, int maxLines, CancellationToken token)
        {
            const int BufferSize = 64 * 1024;
            byte[] buffer = new byte[BufferSize];
            long position = length;
            int newlineCount = 0;

            while (position > 0)
            {
                token.ThrowIfCancellationRequested();
                int bytesToRead = (int)Math.Min(BufferSize, position);
                position -= bytesToRead;
                fileStream.Seek(position, SeekOrigin.Begin);
                fileStream.ReadExactly(buffer.AsSpan(0, bytesToRead));

                for (int i = bytesToRead - 1; i >= 0; i--)
                {
                    if (buffer[i] != (byte)'\n')
                    {
                        continue;
                    }

                    newlineCount++;
                    if (newlineCount > maxLines)
                    {
                        return position + i + 1;
                    }
                }
            }

            return 0;
        }

        private static IEnumerable<string> ReadAllLines(TextReader reader)
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                yield return line;
            }
        }

        private sealed class LogReadStream(FileStream file, long end, CancellationToken token) : Stream
        {
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

            public override int Read(Span<byte> buffer)
            {
                token.ThrowIfCancellationRequested();
                int count = (int)Math.Min(buffer.Length, Math.Max(0, end - file.Position));
                if (count == 0) return 0;
                int read = file.Read(buffer[..count]);
                if (read == 0) throw new EndOfStreamException("The log file was truncated during the read.");
                return read;
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override void Flush() => throw new NotSupportedException();
        }

        private void RefreshTimer_Tick(object? sender, EventArgs e)
        {
            if (!Config.AutoRefresh) return;
            _ = ReadNewLogContent();
        }

        private void RefreshLog() => _ = QueueRead(reload: true);

        internal void ClearLog()
        {
            _clearVersion++;
            if (!_reloadRequested) _readRequested = false;
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
            CancelQueuedRead();

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
                _fileWatcher.Renamed -= OnFileChanged;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            GC.SuppressFinalize(this);
        }
    }
}
