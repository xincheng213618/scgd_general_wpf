using ColorVision.UI.Menus;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    public partial class MenuItemManagerWindow : Window
    {
        public MenuItemManagerWindow()
        {
            InitializeComponent();
        }

        private ObservableCollection<MenuItemSetting> _allSettings = new();
        private string? _selectedGroup;

        private void Window_Initialized(object sender, EventArgs e)
        {
            var config = MenuItemManagerConfig.Instance;
            var menuManager = MenuManager.GetInstance();

            // Ensure settings are synced
            MenuItemManagerService.GetInstance().ApplySettings();

            _allSettings = config.Settings;

            // Build group list (unique OwnerGuid values)
            var groups = _allSettings
                .Where(s => !string.IsNullOrEmpty(s.OwnerGuid))
                .Select(s => s.OwnerGuid!)
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            groups.Insert(0, "(All)");
            GroupListView.ItemsSource = groups;
            GroupListView.SelectedIndex = 0;

            UpdateStatusText();
        }

        private void GroupListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GroupListView.SelectedItem is string group)
            {
                _selectedGroup = group;
                RefreshMenuItemList();
            }
        }

        private void RefreshMenuItemList()
        {
            var searchText = SearchBox?.Text?.Trim()?.ToLowerInvariant() ?? "";

            IEnumerable<MenuItemSetting> filtered = _allSettings;

            if (_selectedGroup != null && _selectedGroup != "(All)")
            {
                filtered = filtered.Where(s => s.OwnerGuid == _selectedGroup);
            }

            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(s =>
                    (s.Header?.ToLowerInvariant().Contains(searchText) == true) ||
                    (s.GuidId?.ToLowerInvariant().Contains(searchText) == true));
            }

            // Use same logic as MenuManager.GetEffectiveOrder: override first, then default
            MenuItemListView.ItemsSource = filtered.OrderBy(s => s.OrderOverride ?? s.DefaultOrder).ToList();
        }

        private void MenuItemListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MenuItemListView.SelectedItem is MenuItemSetting setting)
            {
                ShowDetail(setting);
            }
        }

        private void ShowDetail(MenuItemSetting setting)
        {
            DetailTitle.Text = setting.Header ?? setting.GuidId;
            DetailPanel.Children.Clear();

            AddDetailRow("GuidId", setting.GuidId);
            AddDetailRow("OwnerGuid", setting.OwnerGuid ?? "");
            AddDetailRow("Header", setting.Header ?? "");
            AddDetailRow("Default Order", setting.DefaultOrder.ToString());
            AddDetailRow("Order Override", setting.OrderOverride?.ToString() ?? "(default)");
            AddDetailRow("Visible", setting.IsVisible ? "Yes" : "No");
            AddDetailRow("Hotkey Override", setting.HotkeyOverride ?? "(none)");
        }

        private void AddDetailRow(string label, string value)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            sp.Children.Add(new TextBlock
            {
                Text = label + ": ",
                FontWeight = FontWeights.SemiBold,
                Width = 110,
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("PrimaryTextBrush")
            });
            sp.Children.Add(new TextBlock
            {
                Text = value,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("PrimaryTextBrush")
            });
            DetailPanel.Children.Add(sp);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            RefreshMenuItemList();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            MenuItemManagerService.GetInstance().RebuildMenu();

            // Apply hotkeys to main window
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                MenuItemManagerService.GetInstance().ApplyHotkeys(mainWindow);
            }

            ConfigHandler.GetInstance().SaveConfigs();
            UpdateStatusText();
            MessageBox.Show("Settings applied and menu rebuilt.", "MenuItemManager", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Reset all menu item settings to defaults?", "MenuItemManager", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            foreach (var setting in _allSettings)
            {
                setting.IsVisible = true;
                setting.OrderOverride = null;
                setting.HotkeyOverride = null;
            }

            MenuItemManagerService.GetInstance().RebuildMenu();
            ConfigHandler.GetInstance().SaveConfigs();
            RefreshMenuItemList();
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            int total = _allSettings.Count;
            int hidden = _allSettings.Count(s => !s.IsVisible);
            int customOrder = _allSettings.Count(s => s.OrderOverride.HasValue);
            int withHotkey = _allSettings.Count(s => !string.IsNullOrEmpty(s.HotkeyOverride));
            StatusText.Text = $"Total: {total} | Hidden: {hidden} | Custom Order: {customOrder} | Custom Hotkeys: {withHotkey}";
        }
    }
}
