using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using log4net.Core;
using ColorVision.UI.LogImp.Controls;

namespace ColorVision.UI.LogImp
{
    internal static class LogViewUiHelper
    {
        public const string CtrlFInputGestureText = "Ctrl+F";

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

        public static bool IsCtrlF(KeyEventArgs e)
        {
            return e.Key == Key.F && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        }

        public static bool IsEscape(KeyEventArgs e)
        {
            return e.Key == Key.Escape;
        }

        public static void FocusSearchInput(Control searchInput)
        {
            searchInput.Focus();
            if (searchInput is TextBox textBox)
            {
                textBox.SelectAll();
            }
        }

        public static void PopulateLogLevelMenu(MenuItem levelMenuItem, IReadOnlyList<Level> levels, Level currentLevel, Action<Level> setLogLevel)
        {
            levelMenuItem.Items.Clear();

            foreach (var level in levels)
            {
                var item = new MenuItem
                {
                    Header = level.Name,
                    IsCheckable = true,
                    IsChecked = string.Equals(level.Name, currentLevel.Name, StringComparison.Ordinal)
                };
                item.Click += (_, _) => setLogLevel(level);
                levelMenuItem.Items.Add(item);
            }
        }

        public static string NormalizeSearchText(string? searchText)
        {
            return searchText?.ToLower(CultureInfo.CurrentCulture) ?? string.Empty;
        }

        public static void ApplySearchFilter(string searchText, LogViewerControl logViewer, Control searchInput, Brush? defaultBrush)
        {
            if (!logViewer.ApplySearchFilter(searchText))
            {
                searchInput.BorderBrush = Brushes.Red;
                return;
            }

            if (defaultBrush != null)
            {
                searchInput.BorderBrush = defaultBrush;
            }
        }
    }
}
