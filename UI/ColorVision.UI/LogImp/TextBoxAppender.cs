using ColorVision.UI.LogImp;
using log4net.Appender;
using log4net.Core;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Timers;

namespace ColorVision.UI
{
    /// <summary>
    /// 自定义 log4net追加器，支持批量缓冲和实时搜索功能
    /// </summary>
    /// <remarks>
    /// 该追加器使用批量刷新机制（默认100ms），减少 UI 更新频率，提升性能。
    /// 支持智能滚动控制和实时搜索过滤。
    /// </remarks>
    public class TextBoxAppender : AppenderSkeleton
    {
        private readonly TextBox _textBox;
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly object _lock = new object();
        private readonly System.Timers.Timer _flushTimer;

        /// <summary>
        /// 批量刷新间隔，单位：毫秒
        /// </summary>
        /// <value>默认值为100ms</value>
        public int FlushIntervalMs { get; set; } = LogConstants.DefaultFlushIntervalMs;
        
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

            // 使用后台计时器，避免每次 Tick 在 UI线程上触发大量工作
            _flushTimer = new System.Timers.Timer(FlushIntervalMs) { AutoReset = true };
            _flushTimer.Elapsed += (s, e) => FlushBuffer();
            _flushTimer.Start();

            //仍在 Loaded 时附加滚动事件到视觉树中的 ScrollViewer
            textBox.Loaded += (s, e) =>
            {
                AttachScrollEventHandlers();
            };
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
                if (LogConfig.Instance.LogReserve)
                {
                    _buffer.Insert(0, renderedMessage);
                    _reverseLastState = true;
                }
                else
                {
                    _buffer.Append(renderedMessage);
                    _reverseLastState = false;
                }
            }
        }

        /// <summary>
        /// 刷新缓冲区内容到 UI
        /// </summary>
        private void FlushBuffer()
        {
            string logs;
            bool reverse;
            lock (_lock)
            {
                if (_buffer.Length == 0) return; // 无新内容无需处理
                logs = _buffer.ToString();
                _buffer.Clear();
                reverse = _reverseLastState;
            }

            // 始终从后台线程调用 FlushBuffer（Timer），使用 Dispatcher 异步回到 UI线程执行最小量更新
            if (_textBox.Dispatcher.CheckAccess())
            {
                // 如果在 UI线程上被调用（极少数情形），确保使用 BeginInvoke 来避免阻塞调用者上下文
                _textBox.Dispatcher.BeginInvoke(new Action(() => UpdateTextBox(logs, reverse)), DispatcherPriority.Normal);
            }
            else
            {
                _textBox.Dispatcher.BeginInvoke(new Action(() => UpdateTextBox(logs, reverse)), DispatcherPriority.Normal);
            }
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
                if (reverse)
                {
                    if (LogConfig.Instance.MaxChars > LogConstants.MinMaxCharsForTrimming && _textBox.Text.Length > LogConfig.Instance.MaxChars)
                    {
                        _textBox.Text = _textBox.Text.Substring(0,LogConfig.Instance.MaxChars);
                        _textBox.CaretIndex = _textBox.Text.Length;
                    }
                    if (IsSearchEnabled && logs.Contains(SearchText))
                    {
                        var logLines = logs.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                        var filteredLines = logLines.Where(line => line.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (filteredLines.Length ==0) return;
                        logs = Environment.NewLine + string.Join(Environment.NewLine, filteredLines);
                        _logTextBoxSearch.Text = logs + _logTextBoxSearch.Text;
                    }
                    else
                    {
                        // 倒序插入时尽量使用 StringBuilder 构建一次性字符串
                        var sb = new StringBuilder(logs.Length + _textBox.Text.Length);
                        sb.Append(logs);
                        sb.Append(_textBox.Text);
                        _textBox.Text = sb.ToString();
                    }
                }
                else
                {
                    if (LogConfig.Instance.MaxChars > LogConstants.MinMaxCharsForTrimming && _textBox.Text.Length > LogConfig.Instance.MaxChars)
                    {
                        _textBox.Text = _textBox.Text.Substring(_textBox.Text.Length - LogConfig.Instance.MaxChars);
                        _textBox.CaretIndex = _textBox.Text.Length;
                    }
                    if (IsSearchEnabled )
                    {
                        var logLines = logs.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                        var filteredLines = logLines.Where(line=>line.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (filteredLines.Length==0) return;
                        logs = Environment.NewLine + string.Join(Environment.NewLine, filteredLines);
                        _logTextBoxSearch.AppendText(logs);
                        if (LogConfig.Instance.AutoScrollToEnd && !suspendAutoScroll)
                            _logTextBoxSearch.ScrollToEnd();
                    }
                    else
                    {
                        // 使用 AppendText 比直接修改 Text 更高效
                        _textBox.AppendText(logs);
                        if (LogConfig.Instance.AutoScrollToEnd && !suspendAutoScroll)
                            _textBox.ScrollToEnd();
                    }
                }
            }
            catch (Exception)
            {
                // 忽略 UI 层异常，避免阻塞日志子系统
            }
        }


        private bool suspendAutoScroll;
        private DispatcherTimer resumeScrollTimer;

        /// <summary>
        /// 附加滚动事件处理器，实现智能滚动控制
        /// </summary>
        public void AttachScrollEventHandlers()
        {
            var scrollViewer = GetScrollViewer(_textBox);
            if (scrollViewer != null)
            {
                scrollViewer.PreviewMouseDown += ScrollViewer_PreviewMouseDown;
                scrollViewer.PreviewMouseUp += ScrollViewer_PreviewMouseUp;
                scrollViewer.PreviewMouseWheel += ScrollViewer_PreviewMouseWheel;
            }
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
            base.OnClose();
            try
            {
                _flushTimer?.Stop();
                _flushTimer?.Dispose();
            }
            catch { }
            // 在关闭前最后刷新一次（在 UI线程）
            _textBox.Dispatcher.BeginInvoke(new Action(() => FlushBuffer()));
        }
    }
}