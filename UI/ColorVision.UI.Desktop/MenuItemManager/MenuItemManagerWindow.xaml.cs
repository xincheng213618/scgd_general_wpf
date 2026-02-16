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
        private string? _selectedOwnerGuid;

        /// <summary>
        /// Available OwnerGuid values for the ComboBox dropdown in the DataGrid
        /// </summary>
        public List<string> AvailableOwnerGuids { get; set; } = new();

        private static string GetEffectiveOwner(MenuItemSetting s) => s.OwnerGuidOverride ?? s.OwnerGuid ?? "";

        private void Window_Initialized(object sender, EventArgs e)
        {
            var config = MenuItemManagerConfig.Instance;

            // Ensure settings are synced
            MenuItemManagerService.GetInstance().ApplySettings();

            _allSettings = config.Settings;

            RefreshHierarchyView();
            UpdateStatusText();
        }

        private void RefreshHierarchyView()
        {
            BuildAvailableOwnerGuids();
            BuildTreeView();
        }

        private void BuildAvailableOwnerGuids()
        {
            var guids = new HashSet<string>
            {
                MenuItemConstants.Menu,
                MenuItemConstants.File,
                MenuItemConstants.Edit,
                MenuItemConstants.View,
                MenuItemConstants.Tool,
                MenuItemConstants.Help
            };

            foreach (var s in _allSettings)
            {
                if (!string.IsNullOrEmpty(s.GuidId))
                    guids.Add(s.GuidId);
                if (!string.IsNullOrEmpty(s.OwnerGuid))
                    guids.Add(s.OwnerGuid);
            }

            AvailableOwnerGuids = guids.OrderBy(g => g).ToList();
        }

        private void BuildTreeView()
        {
            MenuTreeView.Items.Clear();

            // Build lookup: effectiveOwnerGuid -> list of settings
            var effectiveOwnerLookup = new Dictionary<string, List<MenuItemSetting>>();
            foreach (var s in _allSettings)
            {
                var owner = GetEffectiveOwner(s);
                if (!effectiveOwnerLookup.ContainsKey(owner))
                    effectiveOwnerLookup[owner] = new List<MenuItemSetting>();
                effectiveOwnerLookup[owner].Add(s);
            }

            // Create (All) root node
            var allNode = new TreeViewItem
            {
                Header = $"(All) ({_allSettings.Count})",
                Tag = "(All)",
                IsExpanded = true,
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("PrimaryTextBrush")
            };
            allNode.Selected += TreeNode_Selected;
            MenuTreeView.Items.Add(allNode);

            // Root: items with OwnerGuid == "Menu"
            var rootGuid = MenuItemConstants.Menu;
            var rootItems = effectiveOwnerLookup.ContainsKey(rootGuid) ? effectiveOwnerLookup[rootGuid] : new List<MenuItemSetting>();

            foreach (var item in rootItems.OrderBy(s => s.OrderOverride ?? s.DefaultOrder))
            {
                var node = CreateTreeNode(item, effectiveOwnerLookup);
                allNode.Items.Add(node);
            }

            // Also add orphan groups not under "Menu"
            var knownOwners = new HashSet<string> { rootGuid };
            CollectKnownOwners(rootItems, effectiveOwnerLookup, knownOwners);

            foreach (var kvp in effectiveOwnerLookup.Where(k => !knownOwners.Contains(k.Key) && k.Key != rootGuid))
            {
                var orphanGroup = new TreeViewItem
                {
                    Header = $"[{kvp.Key}] ({kvp.Value.Count})",
                    Tag = kvp.Key,
                    IsExpanded = false,
                    FontStyle = FontStyles.Italic,
                    Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("SecondaryTextBrush")
                };
                orphanGroup.Selected += TreeNode_Selected;
                foreach (var item in kvp.Value.OrderBy(s => s.OrderOverride ?? s.DefaultOrder))
                {
                    var node = CreateTreeNode(item, effectiveOwnerLookup);
                    orphanGroup.Items.Add(node);
                }
                allNode.Items.Add(orphanGroup);
            }
        }

        private void CollectKnownOwners(List<MenuItemSetting> items, Dictionary<string, List<MenuItemSetting>> lookup, HashSet<string> known)
        {
            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(item.GuidId))
                {
                    known.Add(item.GuidId);
                    if (lookup.ContainsKey(item.GuidId))
                    {
                        CollectKnownOwners(lookup[item.GuidId], lookup, known);
                    }
                }
            }
        }

        private TreeViewItem CreateTreeNode(MenuItemSetting setting, Dictionary<string, List<MenuItemSetting>> lookup)
        {
            var childCount = 0;
            if (!string.IsNullOrEmpty(setting.GuidId) && lookup.ContainsKey(setting.GuidId))
                childCount = lookup[setting.GuidId].Count;

            var displayText = setting.Header ?? setting.GuidId;
            if (!setting.IsVisible)
                displayText = $"[Hidden] {displayText}";
            if (childCount > 0)
                displayText = $"{displayText} ({childCount})";

            var node = new TreeViewItem
            {
                Header = displayText,
                Tag = setting.GuidId ?? "",
                IsExpanded = childCount > 0,
                Foreground = setting.IsVisible
                    ? (System.Windows.Media.Brush)Application.Current.FindResource("PrimaryTextBrush")
                    : (System.Windows.Media.Brush)Application.Current.FindResource("SecondaryTextBrush")
            };
            node.Selected += TreeNode_Selected;

            // Recursively add children
            if (!string.IsNullOrEmpty(setting.GuidId) && lookup.ContainsKey(setting.GuidId))
            {
                foreach (var child in lookup[setting.GuidId].OrderBy(s => s.OrderOverride ?? s.DefaultOrder))
                {
                    node.Items.Add(CreateTreeNode(child, lookup));
                }
            }

            return node;
        }

        private void TreeNode_Selected(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is TreeViewItem tvi && tvi.Tag is string ownerGuid)
            {
                _selectedOwnerGuid = ownerGuid;
                RefreshMenuItemList();
            }
        }

        private void MenuTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // Handled by TreeNode_Selected
        }

        private void RefreshMenuItemList()
        {
            var searchText = SearchBox?.Text?.Trim()?.ToLowerInvariant() ?? "";

            IEnumerable<MenuItemSetting> filtered = _allSettings;

            if (_selectedOwnerGuid != null && _selectedOwnerGuid != "(All)")
            {
                filtered = filtered.Where(s => GetEffectiveOwner(s) == _selectedOwnerGuid);
            }

            if (!string.IsNullOrEmpty(searchText))
            {
                filtered = filtered.Where(s =>
                    (s.Header?.ToLowerInvariant().Contains(searchText) == true) ||
                    (s.GuidId?.ToLowerInvariant().Contains(searchText) == true) ||
                    (s.OwnerGuid?.ToLowerInvariant().Contains(searchText) == true));
            }

            // Use same logic as MenuManager.GetEffectiveOrder: override first, then default
            var items = filtered.OrderBy(s => s.OrderOverride ?? s.DefaultOrder).ToList();
            MenuItemDataGrid.ItemsSource = items;

            ListTitle.Text = _selectedOwnerGuid != null && _selectedOwnerGuid != "(All)"
                ? $"Menu Items — {_selectedOwnerGuid} ({items.Count})"
                : $"Menu Items ({items.Count})";
        }

        private void MenuItemDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MenuItemDataGrid.SelectedItem is MenuItemSetting setting)
            {
                ShowDetail(setting);
            }
        }

        private void ShowDetail(MenuItemSetting setting)
        {
            DetailTitle.Text = setting.Header ?? setting.GuidId;
            DetailPanel.Children.Clear();

            AddDetailRow("GuidId", setting.GuidId);
            AddDetailRow("OwnerGuid (default)", setting.OwnerGuid ?? "");
            AddDetailRow("OwnerGuid (override)", setting.OwnerGuidOverride ?? "(default)");
            AddDetailRow("Header", setting.Header ?? "");
            AddDetailRow("Default Order", setting.DefaultOrder.ToString());
            AddDetailRow("Order Override", setting.OrderOverride?.ToString() ?? "(default)");
            AddDetailRow("Visible", setting.IsVisible ? "Yes" : "No");
            AddDetailRow("Hotkey Override", setting.HotkeyOverride ?? "(none)");

            // Add editable OwnerGuid override section
            DetailPanel.Children.Add(new Separator { Margin = new Thickness(0, 8, 0, 8) });

            var ownerLabel = new TextBlock
            {
                Text = "Move to (OwnerGuid Override):",
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 4),
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("PrimaryTextBrush")
            };
            DetailPanel.Children.Add(ownerLabel);

            var ownerCombo = new ComboBox
            {
                IsEditable = true,
                Text = setting.OwnerGuidOverride ?? "",
                ItemsSource = AvailableOwnerGuids,
                Margin = new Thickness(0, 0, 0, 8),
                Width = double.NaN,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            ownerCombo.SelectionChanged += (s, _) => ApplyOwnerGuidFromCombo(setting, ownerCombo);
            ownerCombo.LostFocus += (s, _) => ApplyOwnerGuidFromCombo(setting, ownerCombo);
            DetailPanel.Children.Add(ownerCombo);

            var clearBtn = new Button
            {
                Content = "Clear OwnerGuid Override",
                Padding = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 4)
            };
            clearBtn.Click += (s, _) =>
            {
                setting.OwnerGuidOverride = null;
                ownerCombo.Text = "";
            };
            DetailPanel.Children.Add(clearBtn);
        }

        private static void ApplyOwnerGuidFromCombo(MenuItemSetting setting, ComboBox combo)
        {
            var text = combo.SelectedItem as string ?? combo.Text?.Trim();
            setting.OwnerGuidOverride = string.IsNullOrEmpty(text) ? null : text;
        }

        private void AddDetailRow(string label, string value)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };
            sp.Children.Add(new TextBlock
            {
                Text = label + ": ",
                FontWeight = FontWeights.SemiBold,
                Width = 140,
                TextWrapping = TextWrapping.Wrap,
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

            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                MenuItemManagerService.GetInstance().ApplyHotkeys(mainWindow);
            }

            ConfigHandler.GetInstance().SaveConfigs();
            RefreshHierarchyView();
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
                setting.OwnerGuidOverride = null;
            }

            MenuItemManagerService.GetInstance().RebuildMenu();
            ConfigHandler.GetInstance().SaveConfigs();
            RefreshHierarchyView();
            RefreshMenuItemList();
            UpdateStatusText();
        }

        private void UpdateStatusText()
        {
            int total = _allSettings.Count;
            int hidden = _allSettings.Count(s => !s.IsVisible);
            int customOrder = _allSettings.Count(s => s.OrderOverride.HasValue);
            int withHotkey = _allSettings.Count(s => !string.IsNullOrEmpty(s.HotkeyOverride));
            int movedItems = _allSettings.Count(s => !string.IsNullOrEmpty(s.OwnerGuidOverride));
            StatusText.Text = $"Total: {total} | Hidden: {hidden} | Custom Order: {customOrder} | Moved: {movedItems} | Custom Hotkeys: {withHotkey}";
        }
    }
}
