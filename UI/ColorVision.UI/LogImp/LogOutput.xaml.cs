using log4net;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using System.Globalization;
using System.Text.RegularExpressions;
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

        public LogOutput()
        {
            Pattern = LogConstants.DefaultLogPattern;
            InitializeComponent();
            this.SizeChanged += (s, e) =>
            {
                ButtonAutoScrollToEnd.Visibility = this.ActualWidth > LogConstants.MinWidthForAutoScrollButton ? Visibility.Visible : Visibility.Collapsed;
                ButtonAutoRefresh.Visibility = this.ActualWidth > LogConstants.MinWidthForAutoRefreshButton ? Visibility.Visible : Visibility.Collapsed;
                cmlog.Visibility = this.ActualWidth > LogConstants.MinWidthForLevelComboBox ? Visibility.Visible : Visibility.Collapsed;
                SearchBar1.Visibility = this.ActualWidth > LogConstants.MinWidthForSearchBar ? Visibility.Visible : Visibility.Collapsed;
            };
        }


        public LogOutput(string? pattern = null)
        {
            Pattern = pattern ?? LogConstants.DefaultLogPattern;
            InitializeComponent();
            this.SizeChanged += (s, e) =>
            {
                ButtonAutoScrollToEnd.Visibility = this.ActualWidth > LogConstants.MinWidthForAutoScrollButton ? Visibility.Visible : Visibility.Collapsed;
                ButtonAutoRefresh.Visibility = this.ActualWidth > LogConstants.MinWidthForAutoRefreshButton ? Visibility.Visible : Visibility.Collapsed;
                cmlog.Visibility = this.ActualWidth > LogConstants.MinWidthForLevelComboBox ? Visibility.Visible : Visibility.Collapsed;
                SearchBar1.Visibility = this.ActualWidth > LogConstants.MinWidthForSearchBar ? Visibility.Visible : Visibility.Collapsed;
            };
        }
        TextBoxAppender TextBoxAppender { get; set; }
        Hierarchy Hierarchy { get; set; }

        public void Dispose()
        {
            Hierarchy.Root.RemoveAppender(TextBoxAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);
            GC.SuppressFinalize(this);
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            Hierarchy = (Hierarchy)LogManager.GetRepository();
            TextBoxAppender = new TextBoxAppender(logTextBox,logTextBoxSerch);
            TextBoxAppender.Layout = new PatternLayout(Pattern);
            Hierarchy.Root.AddAppender(TextBoxAppender);
            log4net.Config.BasicConfigurator.Configure(Hierarchy);

            this.DataContext = LogConfig.Instance;

            cmlog.ItemsSource = LogConfig.GetAllLevels().Select(level => new KeyValuePair<Level, string>(level, level.Name));
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


        private readonly char[] Chars = new[] { ' ' };
        private static readonly string[] RegexSpecialChars = { ".", "*", "+", "?", "^", "$", "(", ")", "[", "]", "{", "}", "|", "\\" };
        private Brush SearchBar1Brush;
        private void SearchBar1_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchBar1.Text.ToLower(CultureInfo.CurrentCulture);
            TextBoxAppender.SearchText = searchText;

            if (!string.IsNullOrEmpty(searchText))
            {
                var containsRegexSpecialChars = RegexSpecialChars.Any(searchText.Contains);

                var keywords = searchText.Split(Chars, StringSplitOptions.RemoveEmptyEntries);

                logTextBox.Visibility = Visibility.Collapsed;
                logTextBoxSerch.Visibility = Visibility.Visible;
                var logLines = logTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                if (containsRegexSpecialChars)
                {
                    // 使用正则表达式搜索
                    try
                    {
                        var regex = new Regex(searchText, RegexOptions.IgnoreCase);
                        var filteredLines = logLines.Where(line => regex.IsMatch(line)).ToArray();
                        logTextBoxSerch.Text = string.Join(Environment.NewLine, filteredLines);
                    }
                    catch (RegexParseException)
                    {
                        SearchBar1.BorderBrush = Brushes.Red;
                        return;
                    }
                }
                else
                {
                    var filteredLines = logLines.Where(line => keywords.All(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase))).ToArray();
                    logTextBoxSerch.Text = string.Join(Environment.NewLine, filteredLines);
                }
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
