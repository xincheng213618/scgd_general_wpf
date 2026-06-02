using ColorVision.Common.MVVM;
using ColorVision.UI.LogImp;
using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI
{
    public class TextAppender : AppenderSkeleton
    {
        private readonly LogStatusBarProvider _logStatusBarProvider;
        private readonly object _syncLock = new object();
        private string _latestMessage = string.Empty;
        private bool _updateQueued;

        public TextAppender(LogStatusBarProvider logStatusBarProvider)
        {
            _logStatusBarProvider = logStatusBarProvider;
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            var renderedMessage = RenderLoggingEvent(loggingEvent);
            string messageToShow = renderedMessage.Length > 10 ? string.Concat(renderedMessage.AsSpan(0, 10), "...") : renderedMessage;

            bool shouldQueueUpdate = false;
            lock (_syncLock)
            {
                _latestMessage = messageToShow;
                if (!_updateQueued)
                {
                    _updateQueued = true;
                    shouldQueueUpdate = true;
                }
            }

            if (shouldQueueUpdate)
            {
                QueueStatusUpdate();
            }
        }

        private void QueueStatusUpdate()
        {
            Application.Current?.Dispatcher.BeginInvoke(ProcessPendingStatusUpdate, DispatcherPriority.Background);
        }

        private void ProcessPendingStatusUpdate()
        {
            string messageToShow;
            bool shouldQueueUpdate = false;

            lock (_syncLock)
            {
                messageToShow = _latestMessage;
                _updateQueued = false;
            }

            _logStatusBarProvider.Log = messageToShow;

            lock (_syncLock)
            {
                if (!_updateQueued && !string.Equals(messageToShow, _latestMessage, StringComparison.Ordinal))
                {
                    _updateQueued = true;
                    shouldQueueUpdate = true;
                }
            }

            if (shouldQueueUpdate)
            {
                QueueStatusUpdate();
            }
        }
    }

    public class LogStatusBarProvider : ViewModelBase, IConfig
    {
        public static LogStatusBarProvider Instance => ConfigService.Instance.GetRequiredService<LogStatusBarProvider>();
        private TextAppender _textAppender;
        private Hierarchy _hierarchy;

        public LogStatusBarProvider()
        {
            _hierarchy = (Hierarchy)LogManager.GetRepository();
            _textAppender = new TextAppender(this)
            {
                Layout = new PatternLayout("%message")
            };

            if (IsShowLog)
            {
                _hierarchy.Root.AddAppender(_textAppender);
                log4net.Config.BasicConfigurator.Configure(_hierarchy);
            }
        }

        [JsonIgnore]
        public string Log { get => _Log; set { _Log = value; OnPropertyChanged(); } }
        private string _Log;

        public bool IsShowLog
        {
            get => _IsShowLog;
            set
            {
                if (_IsShowLog != value)
                {
                    _IsShowLog = value;
                    OnPropertyChanged();

                    if (_hierarchy == null)
                        _hierarchy = (Hierarchy)LogManager.GetRepository();

                    if (_textAppender == null)
                    {
                        _textAppender = new TextAppender(this)
                        {
                            Layout = new PatternLayout("%message")
                        };
                    }

                    if (_IsShowLog)
                    {
                        if (!_hierarchy.Root.Appenders.Cast<IAppender>().Contains(_textAppender))
                        {
                            _hierarchy.Root.AddAppender(_textAppender);
                        }
                        log4net.Config.BasicConfigurator.Configure(_hierarchy);
                    }
                    else
                    {
                        _hierarchy.Root.RemoveAppender(_textAppender);
                    }
                }
            }
        }
        private bool _IsShowLog;
    }
}
