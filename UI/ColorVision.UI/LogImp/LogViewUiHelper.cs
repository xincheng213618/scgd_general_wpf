using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.UI.LogImp
{
    internal static class LogViewUiHelper
    {
        public static void UpdateToolbarVisibility(double actualWidth, UIElement autoScrollButton, UIElement autoRefreshButton, UIElement searchBar, UIElement? levelComboBox = null)
        {
            autoScrollButton.Visibility = actualWidth > LogConstants.MinWidthForAutoScrollButton ? Visibility.Visible : Visibility.Collapsed;
            autoRefreshButton.Visibility = actualWidth > LogConstants.MinWidthForAutoRefreshButton ? Visibility.Visible : Visibility.Collapsed;
            if (levelComboBox != null)
            {
                levelComboBox.Visibility = actualWidth > LogConstants.MinWidthForLevelComboBox ? Visibility.Visible : Visibility.Collapsed;
            }

            searchBar.Visibility = actualWidth > LogConstants.MinWidthForSearchBar ? Visibility.Visible : Visibility.Collapsed;
        }

        public static string NormalizeSearchText(string? searchText)
        {
            return searchText?.ToLower(CultureInfo.CurrentCulture) ?? string.Empty;
        }

        public static void ApplySearchFilter(string searchText, TextBox sourceTextBox, TextBox searchResultTextBox, Control searchInput, Brush? defaultBrush)
        {
            if (!string.IsNullOrEmpty(searchText))
            {
                sourceTextBox.Visibility = Visibility.Collapsed;
                searchResultTextBox.Visibility = Visibility.Visible;
                var logLines = sourceTextBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

                if (!LogSearchHelper.FilterLines(searchText, logLines, out var filteredLines))
                {
                    searchInput.BorderBrush = Brushes.Red;
                    return;
                }

                searchResultTextBox.Text = string.Join(Environment.NewLine, filteredLines);
                if (defaultBrush != null)
                {
                    searchInput.BorderBrush = defaultBrush;
                }
                return;
            }

            searchResultTextBox.Visibility = Visibility.Collapsed;
            sourceTextBox.Visibility = Visibility.Visible;
            if (defaultBrush != null)
            {
                searchInput.BorderBrush = defaultBrush;
            }
        }
    }
}