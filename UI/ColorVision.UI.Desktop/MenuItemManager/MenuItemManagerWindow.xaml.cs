using ColorVision.UI.Menus;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    public class OwnerGuidOption
    {
        public string GuidId { get; set; } = string.Empty;
        public string DisplayPath { get; set; } = string.Empty;

        public override string ToString() => DisplayPath;
    }

    public sealed class MenuItemListRow
    {
        public MenuItemListRow(MenuItemSetting setting, string currentPath)
        {
            Setting = setting;
            CurrentPath = currentPath;
        }

        public MenuItemSetting Setting { get; }
        public bool IsVisible { get => Setting.IsVisible; set => Setting.IsVisible = value; }
        public string Header => GetDisplayName(Setting);
        public string CurrentPath { get; }
        public int? OrderOverride { get => Setting.OrderOverride; set => Setting.OrderOverride = value; }

        private static string GetDisplayName(MenuItemSetting setting)
        {
            return string.IsNullOrWhiteSpace(setting.Header) ? setting.GuidId : setting.Header!;
        }
    }

    public partial class MenuItemManagerWindow : Window
    {
        private const string RootGuid = MenuItemConstants.Menu;
        private const string MenuItemDragFormat = "ColorVision.MenuItemManager.MenuItemGuid";

        private ObservableCollection<MenuItemSetting> _allSettings = new();
        private readonly HashSet<string> _expandedGuids = new(StringComparer.Ordinal);
        private string? _selectedOwnerGuid = RootGuid;
        private MenuItemSetting? _selectedSetting;
        private Point _dragStartPoint;
        private string? _dragSourceGuid;
        private bool _isRefreshing;
        private bool _isReplacingMenuItemRows;
        private bool _isSelectingTreeNode;
        private bool _isUpdatingDetail;

        public MenuItemManagerWindow()
        {
            InitializeComponent();
        }

        public List<OwnerGuidOption> AvailableOwnerGuids { get; set; } = new();

        private static string GetEffectiveOwner(MenuItemSetting setting)
        {
            return NormalizeOwnerGuid(setting.OwnerGuidOverride ?? setting.OwnerGuid);
        }

        private static int GetEffectiveOrder(MenuItemSetting setting)
        {
            return setting.OrderOverride ?? setting.DefaultOrder;
        }

        private static string NormalizeOwnerGuid(string? ownerGuid)
        {
            return string.IsNullOrWhiteSpace(ownerGuid) ? RootGuid : ownerGuid;
        }

        private static string GetDisplayName(MenuItemSetting setting)
        {
            return string.IsNullOrWhiteSpace(setting.Header) ? setting.GuidId : setting.Header!;
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            MenuItemManagerService.ApplySettings();
            _allSettings = MenuItemManagerConfig.Instance.Settings;

            RefreshEditorPreview(restoreSelection: false);
            RestoreLastSelectedTreeNode();
        }

        private void RefreshEditorPreview(bool restoreSelection = true)
        {
            if (_isRefreshing) return;

            try
            {
                _isRefreshing = true;

                if (restoreSelection)
                    CaptureExpandedTreeState();

                BuildAvailableOwnerGuids();
                BuildTreeView();

                if (restoreSelection && !string.IsNullOrEmpty(_selectedOwnerGuid))
                    SelectTreeNodeQuietly(_selectedOwnerGuid);

                RefreshMenuItemList();
                ShowSelectedDetail();
                UpdateStatusText();
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private Dictionary<string, MenuItemSetting> CreateSettingsByGuid()
        {
            return _allSettings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.GuidId))
                .GroupBy(setting => setting.GuidId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        }

        private Dictionary<string, List<MenuItemSetting>> CreateChildrenLookup()
        {
            var lookup = new Dictionary<string, List<MenuItemSetting>>(StringComparer.Ordinal);
            foreach (var setting in _allSettings)
            {
                var ownerGuid = GetEffectiveOwner(setting);
                if (string.IsNullOrWhiteSpace(ownerGuid))
                    ownerGuid = RootGuid;

                if (!lookup.TryGetValue(ownerGuid, out var children))
                {
                    children = new List<MenuItemSetting>();
                    lookup.Add(ownerGuid, children);
                }

                children.Add(setting);
            }

            return lookup;
        }

        private void BuildAvailableOwnerGuids()
        {
            var guids = new HashSet<string>(StringComparer.Ordinal) { RootGuid };
            foreach (var setting in _allSettings)
            {
                if (!string.IsNullOrWhiteSpace(setting.GuidId))
                    guids.Add(setting.GuidId);
                if (!string.IsNullOrWhiteSpace(setting.OwnerGuid))
                    guids.Add(setting.OwnerGuid);
                if (!string.IsNullOrWhiteSpace(setting.OwnerGuidOverride))
                    guids.Add(setting.OwnerGuidOverride);
            }

            AvailableOwnerGuids = guids
                .Select(guid => new OwnerGuidOption { GuidId = guid, DisplayPath = GetPathForGuid(guid) })
                .OrderBy(option => option.DisplayPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void BuildTreeView()
        {
            MenuTreeView.Items.Clear();

            var rootNode = CreateTreeNode(RootGuid, "Menu", null, true);
            rootNode.IsExpanded = true;
            MenuTreeView.Items.Add(rootNode);

            var childrenLookup = CreateChildrenLookup();
            AddTreeChildren(rootNode, RootGuid, childrenLookup, new HashSet<string>(StringComparer.Ordinal) { RootGuid });
        }

        private TreeViewItem CreateTreeNode(string guid, string header, MenuItemSetting? setting, bool isRoot = false)
        {
            var displayText = setting == null ? header : GetTreeNodeText(setting);
            var textBlock = new TextBlock
            {
                Text = displayText,
                TextWrapping = TextWrapping.NoWrap,
                Opacity = setting is { IsVisible: false } ? 0.68 : 1,
                FontStyle = setting is { IsVisible: false } ? FontStyles.Italic : FontStyles.Normal,
                ToolTip = setting is { IsVisible: false } ? "Hidden" : null
            };
            SetForegroundResource(textBlock, setting == null || setting.IsVisible ? "PrimaryTextBrush" : "SecondaryTextBrush");

            var node = new TreeViewItem
            {
                Header = textBlock,
                Tag = guid,
                IsExpanded = isRoot || _expandedGuids.Contains(guid)
            };
            node.Selected += TreeNode_Selected;
            return node;
        }

        private static string GetTreeNodeText(MenuItemSetting setting)
        {
            var displayName = GetDisplayName(setting);
            return setting.IsVisible ? displayName : $"{displayName} (Hidden)";
        }

        private void AddTreeChildren(TreeViewItem parent, string ownerGuid, Dictionary<string, List<MenuItemSetting>> childrenLookup, HashSet<string> visited)
        {
            if (!childrenLookup.TryGetValue(ownerGuid, out var children)) return;

            foreach (var child in children.OrderBy(GetEffectiveOrder).ThenBy(GetDisplayName, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(child.GuidId)) continue;
                if (visited.Contains(child.GuidId)) continue;

                var node = CreateTreeNode(child.GuidId, GetDisplayName(child), child);
                parent.Items.Add(node);

                visited.Add(child.GuidId);
                AddTreeChildren(node, child.GuidId, childrenLookup, visited);
                visited.Remove(child.GuidId);
            }
        }

        private void CaptureExpandedTreeState()
        {
            _expandedGuids.Clear();
            CaptureExpandedTreeState(MenuTreeView);
        }

        private void CaptureExpandedTreeState(ItemsControl parent)
        {
            foreach (var item in parent.Items)
            {
                if (item is not TreeViewItem treeViewItem) continue;

                if (treeViewItem.IsExpanded && treeViewItem.Tag is string guid)
                    _expandedGuids.Add(guid);

                CaptureExpandedTreeState(treeViewItem);
            }
        }

        private void RestoreLastSelectedTreeNode()
        {
            var lastNode = MenuItemManagerConfig.Instance.LastSelectedTreeNode;
            _selectedOwnerGuid = string.IsNullOrWhiteSpace(lastNode) ? RootGuid : lastNode;

            if (!SelectTreeNodeQuietly(_selectedOwnerGuid))
            {
                _selectedOwnerGuid = RootGuid;
                SelectTreeNodeQuietly(RootGuid);
            }

            _selectedSetting = FindSetting(_selectedOwnerGuid);
        }

        private bool SelectTreeNodeQuietly(string tag)
        {
            try
            {
                _isSelectingTreeNode = true;
                return SelectTreeNodeByTag(MenuTreeView, tag);
            }
            finally
            {
                _isSelectingTreeNode = false;
            }
        }

        private static bool SelectTreeNodeByTag(ItemsControl parent, string tag)
        {
            foreach (var item in parent.Items)
            {
                if (item is not TreeViewItem treeViewItem) continue;

                if (treeViewItem.Tag is string currentTag && string.Equals(currentTag, tag, StringComparison.Ordinal))
                {
                    treeViewItem.IsSelected = true;
                    return true;
                }

                if (SelectTreeNodeByTag(treeViewItem, tag))
                {
                    treeViewItem.IsExpanded = true;
                    return true;
                }
            }

            return false;
        }

        private void SaveLastSelectedTreeNode()
        {
            MenuItemManagerConfig.Instance.LastSelectedTreeNode = _selectedOwnerGuid;
        }

        private void TreeNode_Selected(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (_isRefreshing || _isSelectingTreeNode) return;
            if (sender is not TreeViewItem treeViewItem || treeViewItem.Tag is not string ownerGuid) return;

            _selectedOwnerGuid = ownerGuid;
            _selectedSetting = FindSetting(ownerGuid);
            SaveLastSelectedTreeNode();
            RefreshMenuItemList();
            ShowSelectedDetail();
        }

        private MenuItemSetting? FindSetting(string? guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return null;
            return _allSettings.FirstOrDefault(setting => string.Equals(setting.GuidId, guid, StringComparison.Ordinal));
        }

        private void RefreshMenuItemList()
        {
            var searchText = SearchBox?.Text?.Trim() ?? string.Empty;
            List<MenuItemSetting> items;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                items = _allSettings
                    .Where(setting => IsSearchMatch(setting, searchText))
                    .OrderBy(GetCurrentPath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                ListTitle.Text = $"Search Results ({items.Count})";
            }
            else
            {
                var ownerGuid = string.IsNullOrWhiteSpace(_selectedOwnerGuid) ? RootGuid : _selectedOwnerGuid;
                items = _allSettings
                    .Where(setting => string.Equals(GetEffectiveOwner(setting), ownerGuid, StringComparison.Ordinal))
                    .OrderBy(GetEffectiveOrder)
                    .ThenBy(GetDisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                ListTitle.Text = ownerGuid == RootGuid ? $"Top-level Menu Items ({items.Count})" : $"Direct Children ({items.Count})";
            }

            var rows = items.Select(CreateListRow).ToList();
            var selectedGuid = _selectedSetting?.GuidId;

            try
            {
                _isReplacingMenuItemRows = true;
                MenuItemDataGrid.ItemsSource = rows;

                if (!string.IsNullOrWhiteSpace(selectedGuid))
                    MenuItemDataGrid.SelectedItem = rows.FirstOrDefault(row => string.Equals(row.Setting.GuidId, selectedGuid, StringComparison.Ordinal));
            }
            finally
            {
                _isReplacingMenuItemRows = false;
            }
        }

        private bool IsSearchMatch(MenuItemSetting setting, string searchText)
        {
            return GetDisplayName(setting).Contains(searchText, StringComparison.OrdinalIgnoreCase)
                || GetCurrentPath(setting).Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }

        private MenuItemListRow CreateListRow(MenuItemSetting setting)
        {
            return new MenuItemListRow(setting, GetCurrentPath(setting));
        }

        private string GetCurrentPath(MenuItemSetting setting)
        {
            var parts = new List<string> { GetDisplayName(setting) };
            var settingsByGuid = CreateSettingsByGuid();
            var currentGuid = GetEffectiveOwner(setting);
            var visited = new HashSet<string>(StringComparer.Ordinal);

            while (!string.IsNullOrWhiteSpace(currentGuid) && visited.Add(currentGuid))
            {
                if (string.Equals(currentGuid, RootGuid, StringComparison.Ordinal))
                {
                    parts.Add("Menu");
                    break;
                }

                if (!settingsByGuid.TryGetValue(currentGuid, out var parentSetting))
                {
                    parts.Add(currentGuid);
                    break;
                }

                parts.Add(GetDisplayName(parentSetting));
                currentGuid = GetEffectiveOwner(parentSetting);
            }

            parts.Reverse();
            return string.Join(" > ", parts);
        }

        private string GetPathForGuid(string? guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return string.Empty;
            if (string.Equals(guid, RootGuid, StringComparison.Ordinal)) return "Menu";

            var setting = FindSetting(guid);
            return setting == null ? guid : GetCurrentPath(setting);
        }

        private string? ResolveGuidIdFromDisplayPath(string displayPath)
        {
            if (string.IsNullOrWhiteSpace(displayPath)) return null;

            var option = AvailableOwnerGuids.FirstOrDefault(o => string.Equals(o.DisplayPath, displayPath, StringComparison.Ordinal));
            if (option != null) return option.GuidId;

            var directMatch = AvailableOwnerGuids.FirstOrDefault(o => string.Equals(o.GuidId, displayPath, StringComparison.Ordinal));
            return directMatch?.GuidId ?? displayPath;
        }

        private void MenuItemDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing || _isReplacingMenuItemRows) return;
            if (MenuItemDataGrid.SelectedItem is not MenuItemListRow row) return;

            _selectedSetting = row.Setting;
            ShowSelectedDetail();
        }

        private void MenuItemDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (_isRefreshing || _isUpdatingDetail || _isReplacingMenuItemRows) return;
            Dispatcher.InvokeAsync(() =>
            {
                if (!_isRefreshing && !_isUpdatingDetail && !_isReplacingMenuItemRows)
                    RefreshEditorPreview();
            });
        }

        private void VisibleCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (_isRefreshing || _isUpdatingDetail || _isReplacingMenuItemRows) return;
            if (sender is CheckBox { DataContext: MenuItemListRow row } checkBox)
                row.Setting.IsVisible = checkBox.IsChecked == true;

            RefreshEditorPreview();
        }

        private void DragSource_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
            _dragSourceGuid = null;

            if (ShouldIgnoreDragSource(e.OriginalSource as DependencyObject))
                return;

            if (sender == MenuTreeView)
                _dragSourceGuid = GetTreeNodeGuid(e.OriginalSource as DependencyObject);
            else if (sender == MenuItemDataGrid)
                _dragSourceGuid = GetGridRowSetting(e.OriginalSource as DependencyObject)?.GuidId;

            if (string.Equals(_dragSourceGuid, RootGuid, StringComparison.Ordinal))
                _dragSourceGuid = null;
        }

        private void DragSource_MouseMove(object sender, MouseEventArgs e)
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed) return;
            if (string.IsNullOrWhiteSpace(_dragSourceGuid)) return;

            var currentPosition = e.GetPosition(this);
            if (Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var sourceSetting = FindSetting(_dragSourceGuid);
            if (sourceSetting == null) return;

            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(MenuItemDragFormat, sourceSetting.GuidId), DragDropEffects.Move);
            _dragSourceGuid = null;
        }

        private void MenuTreeView_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (TryGetDraggedSetting(e.Data, out var setting))
            {
                var targetOwnerGuid = GetTreeNodeGuid(e.OriginalSource as DependencyObject) ?? RootGuid;
                if (MenuItemManagerService.IsValidOwnerOverride(setting, targetOwnerGuid))
                    e.Effects = DragDropEffects.Move;
            }

            e.Handled = true;
        }

        private void MenuTreeView_Drop(object sender, DragEventArgs e)
        {
            if (!TryGetDraggedSetting(e.Data, out var setting)) return;

            var targetOwnerGuid = GetTreeNodeGuid(e.OriginalSource as DependencyObject) ?? RootGuid;
            MoveSettingToOwner(setting, targetOwnerGuid);
            e.Handled = true;
        }

        private void MenuItemDataGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (TryGetDraggedSetting(e.Data, out var setting))
            {
                var targetOwnerGuid = GetDropTargetOwnerGuid(e.OriginalSource as DependencyObject);
                if (MenuItemManagerService.IsValidOwnerOverride(setting, targetOwnerGuid))
                    e.Effects = DragDropEffects.Move;
            }

            e.Handled = true;
        }

        private void MenuItemDataGrid_Drop(object sender, DragEventArgs e)
        {
            if (!TryGetDraggedSetting(e.Data, out var setting)) return;

            var targetRow = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
            if (targetRow?.DataContext is MenuItemListRow row && !ReferenceEquals(row.Setting, setting))
            {
                var targetOwnerGuid = GetEffectiveOwner(row.Setting);
                var targetChildren = GetOrderedChildren(targetOwnerGuid).Where(child => !ReferenceEquals(child, setting)).ToList();
                var targetIndex = targetChildren.FindIndex(child => ReferenceEquals(child, row.Setting));
                if (targetIndex < 0) targetIndex = targetChildren.Count;

                if (e.GetPosition(targetRow).Y > targetRow.ActualHeight / 2)
                    targetIndex++;

                MoveSettingToOwner(setting, targetOwnerGuid, targetIndex);
            }
            else
            {
                MoveSettingToOwner(setting, NormalizeOwnerGuid(_selectedOwnerGuid), int.MaxValue);
            }

            e.Handled = true;
        }

        private string GetDropTargetOwnerGuid(DependencyObject? originalSource)
        {
            var rowSetting = GetGridRowSetting(originalSource);
            return rowSetting == null ? NormalizeOwnerGuid(_selectedOwnerGuid) : GetEffectiveOwner(rowSetting);
        }

        private static bool ShouldIgnoreDragSource(DependencyObject? source)
        {
            return FindVisualParent<TextBox>(source) != null
                || FindVisualParent<CheckBox>(source) != null
                || FindVisualParent<ComboBox>(source) != null
                || FindVisualParent<Button>(source) != null;
        }

        private static string? GetTreeNodeGuid(DependencyObject? source)
        {
            var treeViewItem = FindVisualParent<TreeViewItem>(source);
            return treeViewItem?.Tag as string;
        }

        private static MenuItemSetting? GetGridRowSetting(DependencyObject? source)
        {
            var row = FindVisualParent<DataGridRow>(source);
            return row?.DataContext is MenuItemListRow listRow ? listRow.Setting : null;
        }

        private bool TryGetDraggedSetting(IDataObject data, out MenuItemSetting setting)
        {
            setting = null!;
            if (!data.GetDataPresent(MenuItemDragFormat)) return false;
            if (data.GetData(MenuItemDragFormat) is not string guid) return false;

            setting = FindSetting(guid)!;
            return setting != null;
        }

        private static T? FindVisualParent<T>(DependencyObject? source) where T : DependencyObject
        {
            while (source != null)
            {
                if (source is T target)
                    return target;

                try
                {
                    source = VisualTreeHelper.GetParent(source);
                }
                catch (InvalidOperationException)
                {
                    source = source switch
                    {
                        FrameworkElement frameworkElement => frameworkElement.Parent,
                        FrameworkContentElement contentElement => contentElement.Parent,
                        _ => null
                    };
                }
            }

            return null;
        }

        private void MoveSettingToOwner(MenuItemSetting setting, string targetOwnerGuid, int insertIndex = int.MaxValue)
        {
            targetOwnerGuid = NormalizeOwnerGuid(targetOwnerGuid);
            if (!MenuItemManagerService.IsValidOwnerOverride(setting, targetOwnerGuid)) return;

            var targetChildren = GetOrderedChildren(targetOwnerGuid)
                .Where(child => !ReferenceEquals(child, setting))
                .ToList();
            insertIndex = Math.Clamp(insertIndex, 0, targetChildren.Count);

            var orderSlots = CreateOrderSlots(targetOwnerGuid, targetChildren.Count + 1);
            targetChildren.Insert(insertIndex, setting);

            setting.OwnerGuidOverride = string.Equals(NormalizeOwnerGuid(setting.OwnerGuid), targetOwnerGuid, StringComparison.Ordinal)
                ? null
                : targetOwnerGuid;

            for (var i = 0; i < targetChildren.Count; i++)
                SetOrderOverride(targetChildren[i], orderSlots[i]);

            _selectedOwnerGuid = targetOwnerGuid;
            _selectedSetting = setting;
            _expandedGuids.Add(targetOwnerGuid);
            SaveLastSelectedTreeNode();
            RefreshEditorPreview();
        }

        private List<MenuItemSetting> GetOrderedChildren(string ownerGuid)
        {
            ownerGuid = NormalizeOwnerGuid(ownerGuid);
            return _allSettings
                .Where(setting => string.Equals(GetEffectiveOwner(setting), ownerGuid, StringComparison.Ordinal))
                .OrderBy(GetEffectiveOrder)
                .ThenBy(GetDisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<int> CreateOrderSlots(string ownerGuid, int requiredCount)
        {
            var slots = GetOrderedChildren(ownerGuid)
                .Select(GetEffectiveOrder)
                .OrderBy(order => order)
                .ToList();

            while (slots.Count < requiredCount)
                slots.Add(slots.Count == 0 ? 0 : slots[^1] + 1);

            for (var i = 1; i < slots.Count; i++)
            {
                if (slots[i] <= slots[i - 1])
                    slots[i] = slots[i - 1] + 1;
            }

            return slots;
        }

        private static void SetOrderOverride(MenuItemSetting setting, int order)
        {
            setting.OrderOverride = setting.DefaultOrder == order ? null : order;
        }

        private void ShowSelectedDetail()
        {
            if (_selectedSetting == null)
            {
                ShowRootDetail();
                return;
            }

            ShowDetail(_selectedSetting);
        }

        private void ShowRootDetail()
        {
            DetailTitle.Text = "Menu";
            DetailPanel.Children.Clear();
            var textBlock = new TextBlock
            {
                Text = "Select a menu item from the tree or list to edit visibility, position, and order.",
                TextWrapping = TextWrapping.Wrap
            };
            SetForegroundResource(textBlock, "SecondaryTextBrush");
            DetailPanel.Children.Add(textBlock);
        }

        private void ShowDetail(MenuItemSetting setting)
        {
            _isUpdatingDetail = true;
            try
            {
                DetailTitle.Text = GetDisplayName(setting);
                DetailPanel.Children.Clear();

                AddInfoRow(DetailPanel, "Current path", GetCurrentPath(setting));
                AddInfoRow(DetailPanel, "GuidId", setting.GuidId);
                AddInfoRow(DetailPanel, "Default owner", GetPathForGuid(setting.OwnerGuid));
                AddInfoRow(DetailPanel, "Current owner", GetPathForGuid(GetEffectiveOwner(setting)));
                AddInfoRow(DetailPanel, "Source assembly", setting.SourceAssembly ?? "Unknown");
                AddInfoRow(DetailPanel, "Source type", setting.SourceType ?? "Unknown");

                var visibleCheckBox = new CheckBox
                {
                    Content = "Visible",
                    IsChecked = setting.IsVisible,
                    Margin = new Thickness(0, 10, 0, 10)
                };
                SetForegroundResource(visibleCheckBox, "PrimaryTextBrush");
                visibleCheckBox.Checked += (_, _) => SetVisibility(setting, true);
                visibleCheckBox.Unchecked += (_, _) => SetVisibility(setting, false);
                DetailPanel.Children.Add(visibleCheckBox);

                DetailPanel.Children.Add(CreateSectionLabel("Move to"));
                var ownerCombo = new ComboBox
                {
                    ItemsSource = GetValidOwnerGuidOptions(setting),
                    DisplayMemberPath = nameof(OwnerGuidOption.DisplayPath),
                    SelectedValuePath = nameof(OwnerGuidOption.GuidId),
                    SelectedValue = setting.OwnerGuidOverride ?? setting.OwnerGuid ?? RootGuid,
                    Margin = new Thickness(0, 0, 0, 8),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                ownerCombo.SelectionChanged += (_, _) => ApplyOwnerGuidFromCombo(setting, ownerCombo);
                DetailPanel.Children.Add(ownerCombo);

                var resetOwnerButton = new Button
                {
                    Content = "Restore Default Position",
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 0, 12),
                    IsEnabled = !string.IsNullOrWhiteSpace(setting.OwnerGuidOverride),
                    Opacity = string.IsNullOrWhiteSpace(setting.OwnerGuidOverride) ? 0.64 : 1
                };
                resetOwnerButton.Click += (_, _) => RestoreDefaultPosition(setting);
                DetailPanel.Children.Add(resetOwnerButton);

                DetailPanel.Children.Add(CreateSectionLabel("Order"));
                AddInfoRow(DetailPanel, "Default order", setting.DefaultOrder.ToString());
                DetailPanel.Children.Add(CreateSectionLabel("OrderOverride"));
                var orderPanel = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 0, 0, 12) };
                var resetOrderButton = new Button
                {
                    Content = "Restore Default Order",
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(8, 0, 0, 0),
                    IsEnabled = setting.OrderOverride.HasValue,
                    Opacity = setting.OrderOverride.HasValue ? 1 : 0.64
                };
                resetOrderButton.Click += (_, _) => RestoreDefaultOrder(setting);
                DockPanel.SetDock(resetOrderButton, Dock.Right);
                orderPanel.Children.Add(resetOrderButton);

                var orderTextBox = new TextBox
                {
                    Text = setting.OrderOverride?.ToString() ?? string.Empty,
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                orderTextBox.LostFocus += (_, _) => ApplyOrderOverride(setting, orderTextBox);
                orderTextBox.KeyDown += (_, e) =>
                {
                    if (e.Key != Key.Enter) return;
                    ApplyOrderOverride(setting, orderTextBox);
                    e.Handled = true;
                };
                orderPanel.Children.Add(orderTextBox);
                DetailPanel.Children.Add(orderPanel);
            }
            finally
            {
                _isUpdatingDetail = false;
            }
        }

        private static TextBlock CreateSectionLabel(string text)
        {
            var textBlock = new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 8, 0, 4)
            };
            SetForegroundResource(textBlock, "PrimaryTextBrush");
            return textBlock;
        }

        private static void AddInfoRow(Panel panel, string label, string value)
        {
            var row = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(112) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelText = new TextBlock
            {
                Text = label + ": ",
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            SetForegroundResource(labelText, "PrimaryTextBrush");
            Grid.SetColumn(labelText, 0);
            row.Children.Add(labelText);

            var valueText = new TextBlock
            {
                Text = value,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            SetForegroundResource(valueText, "PrimaryTextBrush");
            Grid.SetColumn(valueText, 1);
            row.Children.Add(valueText);
            panel.Children.Add(row);
        }

        private static void SetForegroundResource(FrameworkElement element, string resourceKey)
        {
            switch (element)
            {
                case TextBlock textBlock:
                    textBlock.SetResourceReference(TextBlock.ForegroundProperty, resourceKey);
                    break;
                case Control control:
                    control.SetResourceReference(Control.ForegroundProperty, resourceKey);
                    break;
            }
        }

        private void SetVisibility(MenuItemSetting setting, bool isVisible)
        {
            if (_isRefreshing || _isUpdatingDetail) return;
            if (setting.IsVisible == isVisible) return;

            setting.IsVisible = isVisible;
            RefreshEditorPreview();
        }

        private void RestoreDefaultPosition(MenuItemSetting setting)
        {
            if (_isRefreshing || _isUpdatingDetail) return;
            if (string.IsNullOrWhiteSpace(setting.OwnerGuidOverride)) return;

            setting.OwnerGuidOverride = null;
            RefreshEditorPreview();
        }

        private void RestoreDefaultOrder(MenuItemSetting setting)
        {
            if (_isRefreshing || _isUpdatingDetail) return;
            if (!setting.OrderOverride.HasValue) return;

            setting.OrderOverride = null;
            RefreshEditorPreview();
        }

        private List<OwnerGuidOption> GetValidOwnerGuidOptions(MenuItemSetting setting)
        {
            return AvailableOwnerGuids
                .Where(option => MenuItemManagerService.IsValidOwnerOverride(setting, option.GuidId))
                .ToList();
        }

        private void ApplyOwnerGuidFromCombo(MenuItemSetting setting, ComboBox combo)
        {
            if (_isRefreshing || _isUpdatingDetail) return;
            if (combo.SelectedValue is not string guidId) return;

            if (TrySetOwnerGuidOverride(setting, guidId))
                RefreshEditorPreview();
        }

        private bool TrySetOwnerGuidOverride(MenuItemSetting setting, string? selectedValue)
        {
            var guidId = ResolveGuidIdFromDisplayPath(selectedValue ?? string.Empty);
            if (string.IsNullOrWhiteSpace(guidId) || string.Equals(guidId, setting.OwnerGuid, StringComparison.Ordinal))
            {
                setting.OwnerGuidOverride = null;
                return true;
            }

            if (!MenuItemManagerService.IsValidOwnerOverride(setting, guidId))
                return false;

            setting.OwnerGuidOverride = guidId;
            return true;
        }

        private void ApplyOrderOverride(MenuItemSetting setting, TextBox textBox)
        {
            if (_isRefreshing || _isUpdatingDetail) return;

            var text = textBox.Text.Trim();
            if (string.IsNullOrEmpty(text))
            {
                if (!setting.OrderOverride.HasValue) return;
                setting.OrderOverride = null;
                RefreshEditorPreview();
                return;
            }

            if (int.TryParse(text, out var orderOverride))
            {
                if (setting.OrderOverride == orderOverride) return;
                setting.OrderOverride = orderOverride;
                RefreshEditorPreview();
                return;
            }

            textBox.Text = setting.OrderOverride?.ToString() ?? string.Empty;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isRefreshing) return;
            RefreshMenuItemList();
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            MenuItemManagerService.RebuildMenu();
            ConfigHandler.GetInstance().SaveConfigs();
            RefreshEditorPreview();
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
                setting.OwnerGuidOverride = null;
            }

            _selectedOwnerGuid = RootGuid;
            _selectedSetting = null;
            MenuItemManagerService.RebuildMenu();
            ConfigHandler.GetInstance().SaveConfigs();
            RefreshEditorPreview();
        }

        private void UpdateStatusText()
        {
            int total = _allSettings.Count;
            int hidden = _allSettings.Count(s => !s.IsVisible);
            int customOrder = _allSettings.Count(s => s.OrderOverride.HasValue);
            int movedItems = _allSettings.Count(s => !string.IsNullOrEmpty(s.OwnerGuidOverride));
            StatusText.Text = $"Total: {total} | Hidden: {hidden} | Custom Order: {customOrder} | Moved: {movedItems}";
        }
    }
}
