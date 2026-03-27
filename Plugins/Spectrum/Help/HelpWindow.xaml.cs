using ColorVision.UI.Menus;
using Spectrum.Menus;
using System.Windows;
using System.Windows.Controls;

namespace Spectrum.Help
{
    /// <summary>
    /// Menu item that opens the help window from the Help menu.
    /// </summary>
    public class MenuSpectrumHelp : SpectrumMenuIBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 1;
        public override string Header => "帮助文档";

        public override void Execute()
        {
            new HelpWindow
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }
    }

    /// <summary>
    /// HelpWindow - Searchable help reference for spectrometer terminology and usage.
    /// </summary>
    public partial class HelpWindow : Window
    {
        private readonly List<HelpEntry> _allEntries;
        private List<HelpEntry> _filteredEntries;

        public HelpWindow()
        {
            InitializeComponent();
            _allEntries = HelpData.GetAllEntries();
            _filteredEntries = new List<HelpEntry>(_allEntries);
            HelpList.ItemsSource = _filteredEntries;
            UpdateItemCount();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
        }

        private void ApplyFilter()
        {
            if (_allEntries == null) return;

            string keyword = SearchBox.Text?.Trim() ?? string.Empty;
            HelpCategory? categoryFilter = null;

            if (FilterTerminology?.IsChecked == true)
                categoryFilter = HelpCategory.Terminology;
            else if (FilterUsage?.IsChecked == true)
                categoryFilter = HelpCategory.Usage;

            _filteredEntries = _allEntries.Where(entry =>
            {
                if (categoryFilter.HasValue && entry.Category != categoryFilter.Value)
                    return false;

                if (string.IsNullOrEmpty(keyword))
                    return true;

                return entry.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || entry.Summary.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || entry.Keywords.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                    || entry.Detail.Contains(keyword, StringComparison.OrdinalIgnoreCase);
            }).ToList();

            HelpList.ItemsSource = _filteredEntries;
            UpdateItemCount();

            // Auto-select first if search narrows to one
            if (_filteredEntries.Count == 1)
            {
                HelpList.SelectedIndex = 0;
            }
        }

        private void UpdateItemCount()
        {
            if (ItemCountText != null)
            {
                ItemCountText.Text = $"共 {_filteredEntries.Count} / {_allEntries.Count} 条";
            }
        }

        private void HelpList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HelpList.SelectedItem is HelpEntry entry)
            {
                DetailTitle.Text = entry.Title;
                DetailCategory.Text = entry.CategoryDisplay;
                DetailContent.Text = entry.Detail;
                PlaceholderText.Visibility = Visibility.Collapsed;
                DetailScroll.Visibility = Visibility.Visible;
            }
            else
            {
                DetailTitle.Text = string.Empty;
                DetailCategory.Text = string.Empty;
                DetailContent.Text = string.Empty;
                PlaceholderText.Visibility = Visibility.Visible;
                DetailScroll.Visibility = Visibility.Collapsed;
            }
        }
    }
}
