using ColorVision.Common.MVVM;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Serach
{
    public partial class SearchControl : UserControl
    {
        public SearchControl()
        {
            InitializeComponent();
        }

        public ObservableCollection<ISearch> Searches { get; set; } = new ObservableCollection<ISearch>();
        public List<ISearch> FilteredResults { get; set; } = new List<ISearch>();

        private static readonly char[] SplitChars = new[] { ' ' };

        public void FocusSearchBox()
        {
            Searchbox.Focus();
        }

        private void Searchbox_GotFocus(object sender, RoutedEventArgs e)
        {
            Searches = new ObservableCollection<ISearch>(SearchManager.GetInstance().GetISearches());
        }

        private void Searchbox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            string searchtext = textBox.Text;
            if (string.IsNullOrWhiteSpace(searchtext))
            {
                SearchPopup.IsOpen = false;
                return;
            }

            SearchPopup.IsOpen = true;
            var keywords = searchtext.Split(SplitChars, StringSplitOptions.RemoveEmptyEntries);

            FilteredResults = Searches
                .OfType<ISearch>()
                .Where(template => keywords.All(keyword =>
                    (!string.IsNullOrEmpty(template.Header) && template.Header.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ||
                    (template.GuidId != null && template.GuidId.ToString().Contains(keyword, StringComparison.OrdinalIgnoreCase))
                ))
                .ToList();

            var config = SearchConfig.Instance;

            if (config.EnableEverythingSearch && File.Exists(config.EverythingPath))
            {
                FilteredResults.Add(new SearchMeta
                {
                    Header = $"{Properties.Resources.Search} {searchtext} (Everything)",
                    Command = new RelayCommand(a => LaunchEverything(config.EverythingPath, searchtext))
                });
            }

            if (config.EnableBrowserSearch)
            {
                string engineName = config.SearchEngine.ToString();
                FilteredResults.Add(new SearchMeta
                {
                    Header = $"{Properties.Resources.Search} {searchtext} ({engineName})",
                    Command = new RelayCommand(a => SearchInBrowser(searchtext, config.SearchEngine))
                });
            }

            ListViewSearch.ItemsSource = FilteredResults;
            if (FilteredResults.Count > 0)
            {
                ListViewSearch.SelectedIndex = 0;
            }
        }

        private static void LaunchEverything(string everythingPath, string searchtext)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory,
                    FileName = everythingPath,
                    Arguments = $"-s {searchtext}"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
            }
        }

        private static void SearchInBrowser(string searchtext, SearchEngine engine)
        {
            string url = engine switch
            {
                SearchEngine.Google => $"https://www.google.com/search?q={Uri.EscapeDataString(searchtext)}",
                SearchEngine.Baidu => $"https://www.baidu.com/s?wd={Uri.EscapeDataString(searchtext)}",
                SearchEngine.Bing => $"https://www.bing.com/search?q={Uri.EscapeDataString(searchtext)}",
                _ => $"https://www.google.com/search?q={Uri.EscapeDataString(searchtext)}"
            };

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message);
            }
        }

        private void ListViewSearch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void Searchbox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (ListViewSearch.SelectedIndex > -1)
                {
                    Searchbox.Text = string.Empty;
                    FilteredResults[ListViewSearch.SelectedIndex].Command?.Execute(this);
                }
            }
            if (e.Key == Key.Up)
            {
                if (ListViewSearch.SelectedIndex > 0)
                    ListViewSearch.SelectedIndex -= 1;
            }
            if (e.Key == Key.Down)
            {
                if (ListViewSearch.SelectedIndex < FilteredResults.Count - 1)
                    ListViewSearch.SelectedIndex += 1;
            }
        }

        private void ListViewSearch_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ListViewSearch.SelectedIndex > -1)
            {
                Searchbox.Text = string.Empty;
                FilteredResults[ListViewSearch.SelectedIndex].Command?.Execute(this);
            }
        }
    }
}
