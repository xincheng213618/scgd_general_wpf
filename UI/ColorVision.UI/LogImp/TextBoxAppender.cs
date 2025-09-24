using log4net.Appender;
using log4net.Core;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI
{


    public class TextBoxAppender : AppenderSkeleton
    {
        private readonly TextBox _textBox;
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly object _lock = new object();
        private readonly DispatcherTimer _flushTimer;
        public int FlushIntervalMs { get; set; } = 100; // 刷新频率，100ms合并一次刷新
        private bool _reverseLastState = false;


        public string SearchText { get => _SearchText; set { _SearchText = value; IsSearchEnabled = !string.IsNullOrWhiteSpace(value); } }
        private string _SearchText;

        private bool IsSearchEnabled;

        private readonly TextBox _logTextBoxSearch;

        public TextBoxAppender(TextBox textBox,TextBox logTextBoxSerch, int flushIntervalMs =100)
        {
            FlushIntervalMs = flushIntervalMs;
            _textBox = textBox ?? throw new ArgumentNullException(nameof(textBox));
            _logTextBoxSearch = logTextBoxSerch ?? throw new ArgumentNullException(nameof(logTextBoxSerch));
            _flushTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(FlushIntervalMs)
            };
            _flushTimer.Tick += (s, e) => FlushBuffer();
            _flushTimer.Start();
            textBox.Loaded += (s, e) =>
            {
                AttachScrollEventHandlers();
            };
        }

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

            // UI线程刷新
            if (_textBox.Dispatcher.CheckAccess())
            {
                UpdateTextBox(logs, reverse);
            }
            else
            {
                _textBox.Dispatcher.BeginInvoke(new Action(() => UpdateTextBox(logs, reverse)));
            }
        }

        private void UpdateTextBox(string logs, bool reverse)
        {
            if (reverse)
            {
                if (LogConfig.Instance.MaxChars > 1000 && _textBox.Text.Length > LogConfig.Instance.MaxChars)
                {
                    _textBox.Text = _textBox.Text.Substring(0,LogConfig.Instance.MaxChars);
                    _textBox.CaretIndex = _textBox.Text.Length;
                }
                if (IsSearchEnabled && logs.Contains(SearchText))
                {
                    var logLines = logs.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    var filteredLines = logLines.Where(line => line.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (filteredLines.Length == 0) return;
                    logs = Environment.NewLine + string.Join(Environment.NewLine, filteredLines);
                    _logTextBoxSearch.Text = logs + _logTextBoxSearch.Text;
                }
                else
                {
                    _textBox.Text = logs + _textBox.Text;
                }
            }
            else
            {
                if (LogConfig.Instance.MaxChars > 1000 && _textBox.Text.Length > LogConfig.Instance.MaxChars)
                {
                    _textBox.Text = _textBox.Text.Substring(_textBox.Text.Length - LogConfig.Instance.MaxChars);
                    _textBox.CaretIndex = _textBox.Text.Length;
                }
                if (IsSearchEnabled )
                {
                    var logLines = logs.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    var filteredLines = logLines.Where(line=>line.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (filteredLines.Length== 0) return;
                    logs = Environment.NewLine + string.Join(Environment.NewLine, filteredLines);
                    _logTextBoxSearch.AppendText(logs);
                    if (LogConfig.Instance.AutoScrollToEnd && !suspendAutoScroll)
                        _logTextBoxSearch.ScrollToEnd();
                }
                else
                {
                    _textBox.AppendText(logs);
                    if (LogConfig.Instance.AutoScrollToEnd && !suspendAutoScroll)
                        _textBox.ScrollToEnd();
                }
            }
        }


        private bool suspendAutoScroll;
        private DispatcherTimer resumeScrollTimer;

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

        private void PauseAutoScroll()
        {
            suspendAutoScroll = true;
            if (resumeScrollTimer != null) resumeScrollTimer.Stop();
        }

        private void ResumeAutoScrollWithDelay()
        {
            if (resumeScrollTimer == null)
            {
                resumeScrollTimer = new DispatcherTimer();
                resumeScrollTimer.Interval = TimeSpan.FromSeconds(2);
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

        // 获取TextBox的ScrollViewer
        private static ScrollViewer? GetScrollViewer(DependencyObject depObj)
        {
            if (depObj is ScrollViewer) return (ScrollViewer)depObj;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }


        protected override void OnClose()
        {
            base.OnClose();
            _flushTimer?.Stop();
            FlushBuffer(); // 关闭前最后刷新一次
        }
    }
}