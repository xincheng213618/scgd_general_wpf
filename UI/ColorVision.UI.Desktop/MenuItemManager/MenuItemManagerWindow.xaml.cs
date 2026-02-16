using ColorVision.UI.Menus;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    /// <summary>
    /// Represents an OwnerGuid option with hierarchical path display (e.g. "Menu > Help > Log")
    /// </summary>
    public class OwnerGuidOption
    {
        public string GuidId { get; set; } = string.Empty;
        public string DisplayPath { get; set; } = string.Empty;

        public override string ToString() => DisplayPath;
    }

    public partial class MenuItemManagerWindow : Window
    {
        public MenuItemManagerWindow()
        {
            InitializeComponent();
        }

        private ObservableCollection<MenuItemSetting> _allSettings = new();
        private string? _selectedOwnerGuid;

        /// <summary>
        /// Available OwnerGuid options for the ComboBox dropdown (multi-level path display)
        /// </summary>
        public List<OwnerGuidOption> AvailableOwnerGuids { get; set; } = new();

        private static string GetEffectiveOwner(MenuItemSetting s) => s.OwnerGuidOverride ?? s.OwnerGuid ?? "";

        private void Window_Initialized(object sender, EventArgs e)
        {
            var config = MenuItemManagerConfig.Instance;

            // Ensure settings are synced
            MenuItemManagerService.GetInstance().ApplySettings();

            _allSettings = config.Settings;

            RefreshHierarchyView();
            UpdateStatusText();

            // Restore last selected tree node
            RestoreLastSelectedTreeNode();
        }

        private void RefreshHierarchyView()
        {
            BuildAvailableOwnerGuids();
            BuildTreeView();
        }

        private void BuildAvailableOwnerGuids()
        {
            // Build GuidId -> Header lookup from all settings
            var headerLookup = new Dictionary<string, string>();
            foreach (var s in _allSettings)
            {
                if (!string.IsNullOrEmpty(s.GuidId) && !string.IsNullOrEmpty(s.Header))
                    headerLookup[s.GuidId] = s.Header;
            }

            // Well-known top-level names
            headerLookup[MenuItemConstants.Menu] = "Menu";

            // Build GuidId -> OwnerGuid lookup for path traversal
            var ownerLookup = new Dictionary<string, string>();
            foreach (var s in _allSettings)
            {
                if (!string.IsNullOrEmpty(s.GuidId) && !string.IsNullOrEmpty(s.OwnerGuid))
                    ownerLookup[s.GuidId] = s.OwnerGuid;
            }

            // Collect all valid GuidIds
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

            // Build hierarchical path for each guid
            var options = new List<OwnerGuidOption>();
            foreach (var guid in guids)
            {
                var path = BuildMenuPath(guid, headerLookup, ownerLookup);
                options.Add(new OwnerGuidOption { GuidId = guid, DisplayPath = path });
            }

            AvailableOwnerGuids = options.OrderBy(o => o.DisplayPath).ToList();
        }

        /// <summary>
        /// Build a hierarchical path like "Menu > Help > Log" for a given GuidId
        /// </summary>
        private static string BuildMenuPath(string guidId, Dictionary<string, string> headerLookup, Dictionary<string, string> ownerLookup)
        {
            var parts = new List<string>();
            var current = guidId;
            var visited = new HashSet<string>();

            while (!string.IsNullOrEmpty(current) && visited.Add(current))
            {
                var display = headerLookup.ContainsKey(current) ? headerLookup[current] : current;
                parts.Add(display);

                if (ownerLookup.ContainsKey(current))
                    current = ownerLookup[current];
                else
                    break;
            }

            parts.Reverse();
            return string.Join(" > ", parts);
        }

        /// <summary>
        /// Find GuidId from display path
        /// </summary>
        private string? ResolveGuidIdFromDisplayPath(string displayPath)
        {
            if (string.IsNullOrEmpty(displayPath)) return null;

            // Direct match first
            var option = AvailableOwnerGuids.FirstOrDefault(o => o.DisplayPath == displayPath);
            if (option != null) return option.GuidId;

            // Fallback: treat as raw GuidId
            var directMatch = AvailableOwnerGuids.FirstOrDefault(o => o.GuidId == displayPath);
            if (directMatch != null) return directMatch.GuidId;

            // User typed a raw GuidId that's not in the list
            return displayPath;
        }

        /// <summary>
        /// Find display path from GuidId
        /// </summary>
        private string GetDisplayPathForGuidId(string? guidId)
        {
            if (string.IsNullOrEmpty(guidId)) return "";
            var option = AvailableOwnerGuids.FirstOrDefault(o => o.GuidId == guidId);
            return option?.DisplayPath ?? guidId;
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

            // First level: items with OwnerGuid == "Menu" (File, Edit, View, Tool, Help, etc.)
            var rootGuid = MenuItemConstants.Menu;
            var rootItems = effectiveOwnerLookup.ContainsKey(rootGuid) ? effectiveOwnerLookup[rootGuid] : new List<MenuItemSetting>();

            foreach (var item in rootItems.OrderBy(s => s.OrderOverride ?? s.DefaultOrder))
            {
                var childCount = 0;
                if (!string.IsNullOrEmpty(item.GuidId) && effectiveOwnerLookup.ContainsKey(item.GuidId))
                    childCount = effectiveOwnerLookup[item.GuidId].Count;

                var displayText = item.Header ?? item.GuidId;
                if (!item.IsVisible)
                    displayText = $"[Hidden] {displayText}";
                if (childCount > 0)
                    displayText = $"{displayText} ({childCount})";

                var node = new TreeViewItem
                {
                    Header = displayText,
                    Tag = item.GuidId ?? "",
                    IsExpanded = false,
                    Foreground = item.IsVisible
                        ? (System.Windows.Media.Brush)Application.Current.FindResource("PrimaryTextBrush")
                        : (System.Windows.Media.Brush)Application.Current.FindResource("SecondaryTextBrush")
                };
                node.Selected += TreeNode_Selected;

                // Second level: direct children of this item (no deeper recursion)
                if (!string.IsNullOrEmpty(item.GuidId) && effectiveOwnerLookup.ContainsKey(item.GuidId))
                {
                    foreach (var child in effectiveOwnerLookup[item.GuidId].OrderBy(s => s.OrderOverride ?? s.DefaultOrder))
                    {
                        var childChildCount = 0;
                        if (!string.IsNullOrEmpty(child.GuidId) && effectiveOwnerLookup.ContainsKey(child.GuidId))
                            childChildCount = effectiveOwnerLookup[child.GuidId].Count;

                        var childText = child.Header ?? child.GuidId;
                        if (!child.IsVisible)
                            childText = $"[Hidden] {childText}";
                        if (childChildCount > 0)
                            childText = $"{childText} ({childChildCount})";

                        var childNode = new TreeViewItem
                        {
                            Header = childText,
                            Tag = child.GuidId ?? "",
                            Foreground = child.IsVisible
                                ? (System.Windows.Media.Brush)Application.Current.FindResource("PrimaryTextBrush")
                                : (System.Windows.Media.Brush)Application.Current.FindResource("SecondaryTextBrush")
                        };
                        childNode.Selected += TreeNode_Selected;
                        node.Items.Add(childNode);
                    }
                }

                allNode.Items.Add(node);
            }
        }

        private void RestoreLastSelectedTreeNode()
        {
            var lastNode = MenuItemManagerConfig.Instance.LastSelectedTreeNode;
            if (string.IsNullOrEmpty(lastNode))
            {
                // Default: select (All)
                if (MenuTreeView.Items.Count > 0 && MenuTreeView.Items[0] is TreeViewItem allNode)
                {
                    allNode.IsSelected = true;
                }
                return;
            }

            // Find the node with matching Tag
            if (SelectTreeNodeByTag(MenuTreeView, lastNode))
                return;

            // Fallback: select (All)
            if (MenuTreeView.Items.Count > 0 && MenuTreeView.Items[0] is TreeViewItem fallback)
            {
                fallback.IsSelected = true;
            }
        }

        private static bool SelectTreeNodeByTag(ItemsControl parent, string tag)
        {
            // Two-level iteration: (All) -> first-level -> second-level
            foreach (var item in parent.Items)
            {
                if (item is TreeViewItem tvi)
                {
                    if (tvi.Tag is string t && t == tag)
                    {
                        tvi.IsSelected = true;
                        if (parent is TreeViewItem parentTvi)
                            parentTvi.IsExpanded = true;
                        return true;
                    }
                    // Check second level
                    foreach (var child in tvi.Items)
                    {
                        if (child is TreeViewItem childTvi && childTvi.Tag is string ct && ct == tag)
                        {
                            tvi.IsExpanded = true;
                            childTvi.IsSelected = true;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private void SaveLastSelectedTreeNode()
        {
            MenuItemManagerConfig.Instance.LastSelectedTreeNode = _selectedOwnerGuid;
            ConfigHandler.GetInstance().SaveConfigs();
        }

        private void TreeNode_Selected(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is TreeViewItem tvi && tvi.Tag is string ownerGuid)
            {
                _selectedOwnerGuid = ownerGuid;
                SaveLastSelectedTreeNode();
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
            AddDetailRow("OwnerGuid (default)", GetDisplayPathForGuidId(setting.OwnerGuid));
            AddDetailRow("OwnerGuid (override)", setting.OwnerGuidOverride != null ? GetDisplayPathForGuidId(setting.OwnerGuidOverride) : "(default)");
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

            // Default to original OwnerGuid path when no override is set
            var currentOverrideGuid = setting.OwnerGuidOverride ?? setting.OwnerGuid;
            var displayText = GetDisplayPathForGuidId(currentOverrideGuid);

            var ownerCombo = new ComboBox
            {
                IsEditable = true,
                Text = displayText,
                ItemsSource = AvailableOwnerGuids,
                DisplayMemberPath = "DisplayPath",
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
                ownerCombo.Text = GetDisplayPathForGuidId(setting.OwnerGuid);
            };
            DetailPanel.Children.Add(clearBtn);
        }

        private void ApplyOwnerGuidFromCombo(MenuItemSetting setting, ComboBox combo)
        {
            string? guidId = null;
            if (combo.SelectedItem is OwnerGuidOption option)
            {
                guidId = option.GuidId;
            }
            else
            {
                var text = combo.Text?.Trim();
                guidId = ResolveGuidIdFromDisplayPath(text ?? "");
            }

            // Only set override if it differs from the original
            if (!string.IsNullOrEmpty(guidId) && guidId != setting.OwnerGuid)
                setting.OwnerGuidOverride = guidId;
            else
                setting.OwnerGuidOverride = null;
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
            RestoreLastSelectedTreeNode();
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
            RestoreLastSelectedTreeNode();
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
