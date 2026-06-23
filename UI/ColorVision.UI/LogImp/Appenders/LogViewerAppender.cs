using ColorVision.UI.LogImp.Controls;
using ColorVision.UI.LogImp.Models;
using log4net.Appender;
using log4net.Core;
using System.ComponentModel;
using System.Windows.Threading;

namespace ColorVision.UI.LogImp
{
    public sealed class LogViewerAppender : AppenderSkeleton, IDisposable
    {
        private readonly LogViewerControl _logViewer;
        private readonly List<LogEntry> _buffer = new();
        private readonly object _lock = new();
        private readonly System.Timers.Timer _flushTimer;
        private readonly PropertyChangedEventHandler _configChangedHandler;
        private bool _isClosed;
        private bool _flushQueued;
        private bool _reverseLastState;

        public LogViewerAppender(LogViewerControl logViewer)
        {
            _logViewer = logViewer ?? throw new ArgumentNullException(nameof(logViewer));
            _logViewer.MaxEntries = GetMaxEntries();

            _flushTimer = new System.Timers.Timer(GetFlushIntervalMs()) { AutoReset = true };
            _flushTimer.Elapsed += (_, _) => QueueFlush(force: false);
            _flushTimer.Start();

            _configChangedHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(LogConfig.LogFlushIntervalMs))
                {
                    _flushTimer.Interval = GetFlushIntervalMs();
                }
                else if (e.PropertyName == nameof(LogConfig.MaxEntries))
                {
                    _logViewer.Dispatcher.BeginInvoke(() => _logViewer.MaxEntries = GetMaxEntries(), DispatcherPriority.Background);
                }
            };
            LogConfig.Instance.PropertyChanged += _configChangedHandler;
        }

        public bool IgnoreAutoRefresh { get; set; }
        public bool AutoRefresh { get; set; } = true;

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (!AutoRefresh || (!IgnoreAutoRefresh && !LogConfig.Instance.AutoRefresh))
            {
                return;
            }

            var renderedMessage = RenderLoggingEvent(loggingEvent);
            var entries = LogEntryParser.FromRenderedMessage(renderedMessage);
            if (entries.Count == 0)
            {
                return;
            }

            lock (_lock)
            {
                _buffer.AddRange(entries);
                _reverseLastState = LogConfig.Instance.LogReserve;
            }
        }

        private void QueueFlush(bool force)
        {
            lock (_lock)
            {
                if (_buffer.Count == 0 || _flushQueued || (!force && _isClosed))
                {
                    return;
                }

                _flushQueued = true;
            }

            try
            {
                _logViewer.Dispatcher.BeginInvoke(ProcessPendingFlush, DispatcherPriority.Background);
            }
            catch
            {
                lock (_lock)
                {
                    _flushQueued = false;
                }
            }
        }

        private void ProcessPendingFlush()
        {
            List<LogEntry> pendingEntries;
            bool reverse;
            lock (_lock)
            {
                if (_buffer.Count == 0)
                {
                    _flushQueued = false;
                    return;
                }

                pendingEntries = new List<LogEntry>(_buffer);
                _buffer.Clear();
                reverse = _reverseLastState;
            }

            try
            {
                _logViewer.AppendEntries(pendingEntries, reverse, LogConfig.Instance.AutoScrollToEnd);
            }
            catch
            {
                // Logging must never block or crash the UI surface.
            }

            var queueAgain = false;
            lock (_lock)
            {
                _flushQueued = false;
                queueAgain = _buffer.Count > 0 && !_isClosed;
            }

            if (queueAgain)
            {
                QueueFlush(force: false);
            }
        }

        protected override void OnClose()
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;
            base.OnClose();
            LogConfig.Instance.PropertyChanged -= _configChangedHandler;
            _flushTimer.Stop();
            _flushTimer.Dispose();
            QueueFlush(force: true);
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }

        private static int GetFlushIntervalMs()
        {
            return LogConfig.Instance.LogFlushIntervalMs > 0
                ? LogConfig.Instance.LogFlushIntervalMs
                : LogConstants.DefaultFlushIntervalMs;
        }

        private static int GetMaxEntries()
        {
            return LogConfig.Instance.MaxEntries;
        }
    }
}
