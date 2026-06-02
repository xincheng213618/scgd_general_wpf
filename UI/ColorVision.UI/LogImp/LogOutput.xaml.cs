using log4net;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.LogImp
{
    /// <summary>
    /// LogOutput.xaml 的交互逻辑
    /// </summary>
    public partial class LogOutput : UserControl,IDisposable
    {
        private string? Pattern;
        private bool _isAppenderAttached;
        private bool _isDisposed;
        private LogTextViewController? _logTextView;

        public LogOutput(string? pattern = null)
        {
            Pattern = pattern ?? LogConstants.DefaultLogPattern;
            InitializeComponent();
            Loaded += LogOutput_Loaded;
            Unloaded += LogOutput_Unloaded;
        }
        LogViewerAppender? LogViewerAppender { get; set; }
        Hierarchy? Hierarchy { get; set; }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            DetachAppender();
            _logTextView?.Detach();
            Loaded -= LogOutput_Loaded;
            Unloaded -= LogOutput_Unloaded;
            GC.SuppressFinalize(this);
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            Hierarchy = (Hierarchy)LogManager.GetRepository();
            this.DataContext = LogConfig.Instance;
            _logTextView = new LogTextViewController(this, RootGrid, SearchPanel, SearchBar1, LogViewer, CloseSearchButton);
            _logTextView.ConfigureContextMenus(contextMenu =>
                LogTextViewMenuFactory.AppendRealtimeLogMenuItems(contextMenu, ClearLog, SetLogLevel));

            AttachAppender();
        }

        private void LogOutput_Loaded(object sender, RoutedEventArgs e)
        {
            AttachAppender();
        }

        private void LogOutput_Unloaded(object sender, RoutedEventArgs e)
        {
            DetachAppender();
        }

        private void AttachAppender()
        {
            if (_isDisposed || _isAppenderAttached || Hierarchy == null)
            {
                return;
            }

            LogViewerAppender = new LogViewerAppender(LogViewer)
            {
                Layout = new PatternLayout(Pattern)
            };
            Hierarchy.Root.AddAppender(LogViewerAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);
            _isAppenderAttached = true;
        }

        private void DetachAppender()
        {
            if (!_isAppenderAttached || Hierarchy == null || LogViewerAppender == null)
            {
                return;
            }

            Hierarchy.Root.RemoveAppender(LogViewerAppender);
            LogViewerAppender.Dispose();
            LogViewerAppender = null;
            log4net.Config.BasicConfigurator.Configure(Hierarchy);
            _isAppenderAttached = false;
        }

        private void SetLogLevel(Level selectedLevel)
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            if (!string.Equals(selectedLevel.Name, hierarchy.Root.Level?.Name, StringComparison.Ordinal))
            {
                LogConfig.Instance.LogLevel = selectedLevel;
            }
        }

        private void ClearLog()
        {
            LogViewer.Clear();
        }

        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = LogViewUiHelper.NormalizeSearchText(SearchBar1.Text);
            _logTextView?.QueueSearchFilter(searchText);
        }
    }
}
