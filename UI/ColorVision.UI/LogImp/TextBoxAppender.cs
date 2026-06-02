#pragma warning disable CA1805
using ColorVision.UI.LogImp;
using log4net.Appender;
using log4net.Core;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI
{
    /// <summary>
    /// 自定义 log4net追加器，支持批量缓冲和实时搜索功能
    /// </summary>
    /// <remarks>
    /// 该追加器使用批量刷新机制（默认100ms），减少 UI 更新频率，提升性能。
    /// 支持智能滚动控制和实时搜索过滤。
    /// </remarks>
    public class TextBoxAppender : AppenderSkeleton, IDisposable
    {
        private readonly TextBox _textBox;
        private readonly List<string> _buffer = new();
        private readonly object _lock = new object();
        private readonly System.Timers.Timer _flushTimer;
        private readonly PropertyChangedEventHandler _configChangedHandler;
        private readonly RoutedEventHandler _textBoxLoadedHandler;
        private ScrollViewer? _attachedScrollViewer;
        private bool _isClosed;
        private bool _flushQueued;
        private bool _scrollHandlersAttached;

        /// <summary>
        /// 批量刷新间隔，单位：毫秒
        /// </summary>
        /// <value>默认值为100ms</value>
        public int FlushIntervalMs { get; private set; } = LogConstants.DefaultFlushIntervalMs;
        
        private bool _reverseLastState = false;

        /// <summary>
        /// 搜索文本，设置后启用实时搜索过滤
        /// </summary>
        public string SearchText { get => _SearchText; set { _SearchText = value; IsSearchEnabled = !string.IsNullOrWhiteSpace(value); } }
        private string _SearchText;

        private bool IsSearchEnabled;

        private readonly TextBox _logTextBoxSearch;

        /// <summary>
        /// 初始化 TextBoxAppender 实例
        /// </summary>
        /// <param name="textBox">主日志显示文本框</param>
        /// <param name="logTextBoxSerch">搜索结果显示文本框</param>
        /// <exception cref="ArgumentNullException">当 textBox 或 logTextBoxSerch 为 null 时抛出</exception>
        public TextBoxAppender(TextBox textBox,TextBox logTextBoxSerch)
        {
            _textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
            _logTextBoxSearch = logTextBoxSerch ?? throw new ArgumentNullException(nameof(logTextBoxSerch));
            FlushIntervalMs = GetFlushIntervalMs();

            // 使用后台计时器，避免每次 Tick 在 UI线程上触发大量工作
            _flushTimer = new System.Timers.Timer(FlushIntervalMs) { AutoReset = true };
            _flushTimer.Elapsed += (s, e) => FlushBuffer();
            _flushTimer.Start();

            _configChangedHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(LogConfig.LogFlushIntervalMs))
                {
                    FlushIntervalMs = GetFlushIntervalMs();
                    _flushTimer.Interval = FlushIntervalMs;
                }
            };
            LogConfig.Instance.PropertyChanged += _configChangedHandler;

            //仍在 Loaded 时附加滚动事件到视觉树中的 ScrollViewer
            _textBoxLoadedHandler = (_, _) => AttachScrollEventHandlers();
            textBox.Loaded += _textBoxLoadedHandler;
        }

        private static int GetFlushIntervalMs()
        {
            return LogConfig.Instance.LogFlushIntervalMs > 0
                ? LogConfig.Instance.LogFlushIntervalMs
                : LogConstants.DefaultFlushIntervalMs;
        }

        /// <summary>
        /// 追加日志事件到缓冲区
        /// </summary>
        /// <param name="loggingEvent">日志事件</param>
        protected override void Append(LoggingEvent loggingEvent)
        {
            if (!LogConfig.Instance.AutoRefresh) return;
            var renderedMessage = RenderLoggingEvent(loggingEvent);

            lock (_lock)
            {
                _buffer.Add(renderedMessage);
                if (LogConfig.Instance.LogReserve)
                {
                    _reverseLastState = true;
                }
                else
                {
                    _reverseLastState = false;
                }
            }
        }

        /// <summary>
        /// 刷新缓冲区内容到 UI
        /// </summary>
        private void FlushBuffer()
        {
            QueueFlush(force: false);
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

            _textBox.Dispatcher.BeginInvoke(ProcessPendingFlush, DispatcherPriority.Background);
        }

        private void ProcessPendingFlush()
        {
            string logs;
            bool reverse;
            List<string> pendingLogs;
            lock (_lock)
            {
                if (_buffer.Count == 0)
                {
                    _flushQueued = false;
                    return;
                }

                pendingLogs = new List<string>(_buffer);
                _buffer.Clear();
                reverse = _reverseLastState;
            }

            logs = BuildBufferedLogs(pendingLogs, reverse);
            UpdateTextBox(logs, reverse);

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

        private static string BuildBufferedLogs(List<string> pendingLogs, bool reverse)
        {
            if (pendingLogs.Count == 1)
            {
                return pendingLogs[0];
            }

            var totalLength = 0;
            for (var i = 0; i < pendingLogs.Count; i++)
            {
                totalLength += pendingLogs[i].Length;
            }

            var builder = new StringBuilder(totalLength);
            if (reverse)
            {
                for (var i = pendingLogs.Count - 1; i >= 0; i--)
                {
                    builder.Append(pendingLogs[i]);
                }
            }
            else
            {
                for (var i = 0; i < pendingLogs.Count; i++)
                {
                    builder.Append(pendingLogs[i]);
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// 更新文本框内容
        /// </summary>
        /// <param name="logs">要添加的日志内容</param>
        /// <param name="reverse">是否倒序插入</param>
        private void UpdateTextBox(string logs, bool reverse)
        {
            try
            {
                UpdateMainTextBox(logs, reverse);

                if (IsSearchEnabled)
                {
                    UpdateSearchTextBox(logs, reverse);
                }
            }
            catch (Exception)
            {
                // 忽略 UI 层异常，避免阻塞日志子系统
            }
        }

        private void UpdateMainTextBox(string logs, bool reverse)
        {
            if (reverse)
            {
                // 倒序插入时尽量使用 StringBuilder 构建一次性字符串
                var sb = new StringBuilder(logs.Length + _textBox.Text.Length);
                sb.Append(logs);
                sb.Append(_textBox.Text);
                _textBox.Text = sb.ToString();
                TrimTextBox(_textBox, reverse);
                _textBox.CaretIndex = _textBox.Text.Length;
                return;
            }

            // 使用 AppendText 比直接修改 Text 更高效
            _textBox.AppendText(logs);
            TrimTextBox(_textBox, reverse);
            if (LogConfig.Instance.AutoScrollToEnd && !suspendAutoScroll)
                _textBox.ScrollToEnd();
        }

        private void UpdateSearchTextBox(string logs, bool reverse)
        {
            var logLines = logs.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            if (!LogSearchHelper.FilterLines(SearchText, logLines, out var filteredLines) || filteredLines.Length == 0)
            {
                return;
            }

            var filteredLogs = string.Join(Environment.NewLine, filteredLines);
            if (string.IsNullOrEmpty(filteredLogs))
            {
                return;
            }

            if (reverse)
            {
                _logTextBoxSearch.Text = string.IsNullOrEmpty(_logTextBoxSearch.Text)
                    ? filteredLogs
                    : filteredLogs + Environment.NewLine + _logTextBoxSearch.Text;
                TrimTextBox(_logTextBoxSearch, reverse);
                _logTextBoxSearch.ScrollToHome();
                return;
            }

            if (!string.IsNullOrEmpty(_logTextBoxSearch.Text))
            {
                _logTextBoxSearch.AppendText(Environment.NewLine + filteredLogs);
            }
            else
            {
                _logTextBoxSearch.Text = filteredLogs;
            }

            TrimTextBox(_logTextBoxSearch, reverse);
            if (LogConfig.Instance.AutoScrollToEnd && !suspendAutoScroll)
                _logTextBoxSearch.ScrollToEnd();
        }

        private static void TrimTextBox(TextBox textBox, bool reverse)
        {
            if (LogConfig.Instance.MaxChars <= LogConstants.MinMaxCharsForTrimming || textBox.Text.Length <= LogConfig.Instance.MaxChars)
            {
                return;
            }

            textBox.Text = reverse
                ? textBox.Text.Substring(0, LogConfig.Instance.MaxChars)
                : textBox.Text.Substring(textBox.Text.Length - LogConfig.Instance.MaxChars);
        }


        private bool suspendAutoScroll;
        private DispatcherTimer resumeScrollTimer;

        /// <summary>
        /// 附加滚动事件处理器，实现智能滚动控制
        /// </summary>
        public void AttachScrollEventHandlers()
        {
            if (_scrollHandlersAttached)
            {
                return;
            }

            var scrollViewer = GetScrollViewer(_textBox);
            if (scrollViewer != null)
            {
                scrollViewer.PreviewMouseDown += ScrollViewer_PreviewMouseDown;
                scrollViewer.PreviewMouseUp += ScrollViewer_PreviewMouseUp;
                scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
                _attachedScrollViewer = scrollViewer;
                _scrollHandlersAttached = true;
            }
        }

        private void DetachScrollEventHandlers()
        {
            _textBox.Loaded -= _textBoxLoadedHandler;

            if (!_scrollHandlersAttached || _attachedScrollViewer == null)
            {
                return;
            }

            _attachedScrollViewer.PreviewMouseDown -= ScrollViewer_PreviewMouseDown;
            _attachedScrollViewer.PreviewMouseUp -= ScrollViewer_PreviewMouseUp;
            _attachedScrollViewer.PreviewMouseWheel -= ScrollViewer_PreviewMouseWheel;
            _attachedScrollViewer = null;
            _scrollHandlersAttached = false;
        }

        private void ScrollViewer_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            PauseAutoScroll();
        }
        private void ScrollViewer_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            ResumeAutoScrollWithDelay();
        }
        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            PauseAutoScroll();
            ResumeAutoScrollWithDelay();
        }

        /// <summary>
        /// 暂停自动滚动
        /// </summary>
        private void PauseAutoScroll()
        {
            suspendAutoScroll = true;
            if (resumeScrollTimer != null) resumeScrollTimer.Stop();
        }

        /// <summary>
        /// 延迟恢复自动滚动
        /// </summary>
        private void ResumeAutoScrollWithDelay()
        {
            if (resumeScrollTimer == null)
            {
                resumeScrollTimer = new DispatcherTimer();
                resumeScrollTimer.Interval = TimeSpan.FromSeconds(LogConstants.AutoScrollResumeDelaySeconds);
                resumeScrollTimer.Tick += (s, e) =>
                {
                    suspendAutoScroll = false;
                    resumeScrollTimer.Stop();
                    if (LogConfig.Instance.AutoScrollToEnd)
                        _textBox.ScrollToEnd();
                };
            }
            resumeScrollTimer.Stop();
            resumeScrollTimer.Start();
        }

        /// <summary>
        /// 获取 TextBox 的 ScrollViewer 控件
        /// </summary>
        /// <param name="depObj">依赖对象</param>
        /// <returns>ScrollViewer 实例，如果未找到则返回 null</returns>
        private static ScrollViewer? GetScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer) return (ScrollViewer)depObj;
            for (int i =0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }


        /// <summary>
        /// 关闭追加器，停止定时器并刷新剩余日志
        /// </summary>
        protected override void OnClose()
        {
            if (_isClosed)
            {
                return;
            }

            _isClosed = true;
            base.OnClose();
            try
            {
                LogConfig.Instance.PropertyChanged -= _configChangedHandler;
                DetachScrollEventHandlers();
                _flushTimer?.Stop();
                _flushTimer?.Dispose();
                resumeScrollTimer?.Stop();
            }
            catch { }
            // 在关闭前最后刷新一次（在 UI线程）
            QueueFlush(force: true);
        }

        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }
    }
}
