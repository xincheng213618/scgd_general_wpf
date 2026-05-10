using log4net;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

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

        public LogOutput(string? pattern = null)
        {
            Pattern = pattern ?? LogConstants.DefaultLogPattern;
            InitializeComponent();
            Loaded += LogOutput_Loaded;
            Unloaded += LogOutput_Unloaded;
            this.SizeChanged += (s, e) =>
            {
                ButtonAutoScrollToEnd.Visibility = this.ActualWidth > LogConstants.MinWidthForAutoScrollButton ? Visibility.Visible : Visibility.Collapsed;
                ButtonAutoRefresh.Visibility = this.ActualWidth > LogConstants.MinWidthForAutoRefreshButton ? Visibility.Visible : Visibility.Collapsed;
                cmlog.Visibility = this.ActualWidth > LogConstants.MinWidthForLevelComboBox ? Visibility.Visible : Visibility.Collapsed;
                SearchBar1.Visibility = this.ActualWidth > LogConstants.MinWidthForSearchBar ? Visibility.Visible : Visibility.Collapsed;
            };
        }
        TextBoxAppender? TextBoxAppender { get; set; }
        Hierarchy? Hierarchy { get; set; }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            DetachAppender();
            Loaded -= LogOutput_Loaded;
            Unloaded -= LogOutput_Unloaded;
            GC.SuppressFinalize(this);
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            Hierarchy = (Hierarchy)LogManager.GetRepository();
            this.DataContext = LogConfig.Instance;
            cmlog.ItemsSource = LogConfig.GetAllLevels().Select(level => new KeyValuePair<Level, string>(level, level.Name));
            SearchBar1Brush = SearchBar1.BorderBrush;

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

            TextBoxAppender = new TextBoxAppender(logTextBox, logTextBoxSerch)
            {
                Layout = new PatternLayout(Pattern)
            };
            Hierarchy.Root.AddAppender(TextBoxAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);
            _isAppenderAttached = true;
        }

        private void DetachAppender()
        {
            if (!_isAppenderAttached || Hierarchy == null || TextBoxAppender == null)
            {
                return;
            }

            Hierarchy.Root.RemoveAppender(TextBoxAppender);
            TextBoxAppender.Dispose();
            TextBoxAppender = null;
            log4net.Config.BasicConfigurator.Configure(Hierarchy);
            _isAppenderAttached = false;
        }

        private void cmlog_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedLevel = (KeyValuePair<Level, string>)cmlog.SelectedItem;
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            if (selectedLevel.Key != hierarchy.Root.Level)
            {
                hierarchy.Root.Level = selectedLevel.Key;
                log4net.Config.BasicConfigurator.Configure(hierarchy);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            logTextBox.Text = string.Empty;
            logTextBoxSerch.Text = string.Empty;
        }


        private Brush SearchBar1Brush;
        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchBar1.Text.ToLower(CultureInfo.CurrentCulture);
            if (TextBoxAppender != null)
            {
                TextBoxAppender.SearchText = searchText;
            }

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
    }
}
