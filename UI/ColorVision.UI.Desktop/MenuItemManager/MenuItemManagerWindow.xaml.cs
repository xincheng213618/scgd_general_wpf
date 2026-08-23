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
        public string TargetName { get; set; } = string.Empty;
        public string GuidId { get; set; } = string.Empty;
        public string DisplayPath { get; set; } = string.Empty;
        public MenuItemScopeKey ScopeKey => new(TargetName, GuidId);

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
        private const string MenuItemDragFormat = "ColorVision.MenuItemManager.MenuItemScopeKey";

        private ObservableCollection<MenuItemSetting> _allSettings = new();
        private readonly HashSet<MenuItemScopeKey> _expandedKeys = new();
        private string _selectedTargetName = MenuItemConstants.MainWindowTarget;
        private MenuItemScopeKey _selectedOwnerKey = new(MenuItemConstants.MainWindowTarget, RootGuid);
        private MenuItemSetting? _selectedSetting;
        private Point _dragStartPoint;
        private MenuItemScopeKey? _dragSourceKey;
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
            _allSettings = MenuItemManagerService.CreateEditingSnapshot();
            InitializeTargetScopes();

            RefreshEditorPreview(restoreSelection: false);
            RestoreLastSelectedTreeNode();
        }

        private void InitializeTargetScopes()
        {
            var targetNames = _allSettings
                .Select(setting => setting.TargetName)
                .Where(targetName => !string.IsNullOrWhiteSpace(targetName))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(targetName => string.Equals(targetName, MenuItemConstants.GlobalTarget, StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(targetName => targetName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string? savedTarget = MenuItemManagerConfig.Instance.LastSelectedTargetName;
            _selectedTargetName = targetNames.Contains(savedTarget, StringComparer.Ordinal)
                ? savedTarget!
                : targetNames.Contains(MenuItemConstants.MainWindowTarget, StringComparer.Ordinal)
                    ? MenuItemConstants.MainWindowTarget
                    : targetNames.FirstOrDefault() ?? MenuItemConstants.GlobalTarget;

            TargetScopeComboBox.ItemsSource = targetNames;
            TargetScopeComboBox.SelectedItem = _selectedTargetName;
            _selectedOwnerKey = RootKey;
        }

        private MenuItemScopeKey RootKey => new(_selectedTargetName, RootGuid);

        private IEnumerable<MenuItemSetting> CurrentSurfaceSettings => _allSettings.Where(setting =>
            string.Equals(setting.TargetName, _selectedTargetName, StringComparison.Ordinal)
            || (!string.Equals(_selectedTargetName, MenuItemConstants.GlobalTarget, StringComparison.Ordinal)
                && string.Equals(setting.TargetName, MenuItemConstants.GlobalTarget, StringComparison.Ordinal)));

        private static MenuItemScopeKey GetScopeKey(MenuItemSetting setting) => new(setting.TargetName, setting.GuidId);

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

                if (restoreSelection)
                    SelectTreeNodeQuietly(_selectedOwnerKey);

                RefreshMenuItemList();
                ShowSelectedDetail();
                UpdateStatusText();
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        private Dictionary<MenuItemScopeKey, MenuItemSetting> CreateSettingsByKey()
        {
            return CurrentSurfaceSettings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.GuidId))
                .GroupBy(GetScopeKey)
                .ToDictionary(group => group.Key, group => group.First());
        }

        private Dictionary<MenuItemScopeKey, List<MenuItemSetting>> CreateChildrenLookup()
        {
            var lookup = new Dictionary<MenuItemScopeKey, List<MenuItemSetting>>();
            var settingsByKey = CreateSettingsByKey();
            foreach (var setting in CurrentSurfaceSettings)
            {
                MenuItemScopeKey ownerKey = ResolveOwnerKey(setting, GetEffectiveOwner(setting), settingsByKey) ?? RootKey;

                if (!lookup.TryGetValue(ownerKey, out var children))
                {
                    children = new List<MenuItemSetting>();
                    lookup.Add(ownerKey, children);
                }

                children.Add(setting);
            }

            return lookup;
        }

        private void BuildAvailableOwnerGuids()
        {
            var options = new Dictionary<MenuItemScopeKey, OwnerGuidOption>
            {
                [RootKey] = new OwnerGuidOption
                {
                    TargetName = RootKey.TargetName,
                    GuidId = RootGuid,
                    DisplayPath = $"[{_selectedTargetName}] Menu"
                }
            };

            foreach (var setting in CurrentSurfaceSettings)
            {
                if (!string.IsNullOrWhiteSpace(setting.GuidId))
                {
                    MenuItemScopeKey key = GetScopeKey(setting);
                    options[key] = new OwnerGuidOption
                    {
                        TargetName = key.TargetName,
                        GuidId = key.GuidId,
                        DisplayPath = $"[{key.TargetName}] {GetCurrentPath(setting)}"
                    };
                }
            }

            AvailableOwnerGuids = options.Values
                .OrderBy(option => option.DisplayPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void BuildTreeView()
        {
            MenuTreeView.Items.Clear();

            var rootNode = CreateTreeNode(RootKey, $"Menu ({_selectedTargetName})", null, true);
            rootNode.IsExpanded = true;
            MenuTreeView.Items.Add(rootNode);

            var childrenLookup = CreateChildrenLookup();
            AddTreeChildren(rootNode, RootKey, childrenLookup, new HashSet<MenuItemScopeKey> { RootKey });
        }

        private TreeViewItem CreateTreeNode(MenuItemScopeKey key, string header, MenuItemSetting? setting, bool isRoot = false)
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
                Tag = key,
                IsExpanded = isRoot || _expandedKeys.Contains(key)
            };
            node.Selected += TreeNode_Selected;
            return node;
        }

        private static string GetTreeNodeText(MenuItemSetting setting)
        {
            var displayName = GetDisplayName(setting);
            return setting.IsVisible ? displayName : $"{displayName} (Hidden)";
        }

        private void AddTreeChildren(TreeViewItem parent, MenuItemScopeKey ownerKey, Dictionary<MenuItemScopeKey, List<MenuItemSetting>> childrenLookup, HashSet<MenuItemScopeKey> visited)
        {
            if (!childrenLookup.TryGetValue(ownerKey, out var children)) return;

            foreach (var child in children.OrderBy(GetEffectiveOrder).ThenBy(GetDisplayName, StringComparer.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(child.GuidId)) continue;
                MenuItemScopeKey childKey = GetScopeKey(child);
                if (visited.Contains(childKey)) continue;

                var node = CreateTreeNode(childKey, GetDisplayName(child), child);
                parent.Items.Add(node);

                visited.Add(childKey);
                AddTreeChildren(node, childKey, childrenLookup, visited);
                visited.Remove(childKey);
            }
        }

        private MenuItemScopeKey? ResolveOwnerKey(
            MenuItemSetting setting,
            string ownerGuid,
            Dictionary<MenuItemScopeKey, MenuItemSetting>? settingsByKey = null)
        {
            if (string.Equals(ownerGuid, RootGuid, StringComparison.Ordinal))
                return RootKey;

            settingsByKey ??= CreateSettingsByKey();
            var sameTargetKey = new MenuItemScopeKey(setting.TargetName, ownerGuid);
            if (settingsByKey.ContainsKey(sameTargetKey))
                return sameTargetKey;

            var globalKey = new MenuItemScopeKey(MenuItemConstants.GlobalTarget, ownerGuid);
            return settingsByKey.ContainsKey(globalKey) ? globalKey : null;
        }

        private void CaptureExpandedTreeState()
        {
            _expandedKeys.Clear();
            CaptureExpandedTreeState(MenuTreeView);
        }

        private void CaptureExpandedTreeState(ItemsControl parent)
        {
            foreach (var item in parent.Items)
            {
                if (item is not TreeViewItem treeViewItem) continue;

                if (treeViewItem.IsExpanded && treeViewItem.Tag is MenuItemScopeKey key)
                    _expandedKeys.Add(key);

                CaptureExpandedTreeState(treeViewItem);
            }
        }

        private void RestoreLastSelectedTreeNode()
        {
            MenuItemManagerConfig config = MenuItemManagerConfig.Instance;
            _selectedOwnerKey = ResolveSavedSelection(config.LastSelectedTreeNodeTargetName, config.LastSelectedTreeNode);

            if (!SelectTreeNodeQuietly(_selectedOwnerKey))
            {
                _selectedOwnerKey = RootKey;
                SelectTreeNodeQuietly(RootKey);
            }

            _selectedSetting = FindSetting(_selectedOwnerKey);
        }

        private MenuItemScopeKey ResolveSavedSelection(string? targetName, string? guidId)
        {
            if (string.IsNullOrWhiteSpace(guidId) || string.Equals(guidId, RootGuid, StringComparison.Ordinal))
                return RootKey;

            if (!string.IsNullOrWhiteSpace(targetName))
            {
                var savedKey = new MenuItemScopeKey(targetName, guidId);
                if (FindSetting(savedKey) != null
                    && (string.Equals(targetName, _selectedTargetName, StringComparison.Ordinal)
                        || string.Equals(targetName, MenuItemConstants.GlobalTarget, StringComparison.Ordinal)))
                {
                    return savedKey;
                }
            }

            // Compatibility with configs written before tree-node scope was persisted.
            var sameTargetKey = new MenuItemScopeKey(_selectedTargetName, guidId);
            if (FindSetting(sameTargetKey) != null)
                return sameTargetKey;

            var globalKey = new MenuItemScopeKey(MenuItemConstants.GlobalTarget, guidId);
            return FindSetting(globalKey) != null ? globalKey : RootKey;
        }

        private bool SelectTreeNodeQuietly(MenuItemScopeKey tag)
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

        private static bool SelectTreeNodeByTag(ItemsControl parent, MenuItemScopeKey tag)
        {
            foreach (var item in parent.Items)
            {
                if (item is not TreeViewItem treeViewItem) continue;

                if (treeViewItem.Tag is MenuItemScopeKey currentTag && currentTag == tag)
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
            MenuItemManagerConfig.Instance.LastSelectedTargetName = _selectedTargetName;
            MenuItemManagerConfig.Instance.LastSelectedTreeNodeTargetName = _selectedOwnerKey.TargetName;
            MenuItemManagerConfig.Instance.LastSelectedTreeNode = _selectedOwnerKey.GuidId;
        }

        private void TreeNode_Selected(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (_isRefreshing || _isSelectingTreeNode) return;
            if (sender is not TreeViewItem treeViewItem || treeViewItem.Tag is not MenuItemScopeKey ownerKey) return;

            _selectedOwnerKey = ownerKey;
            _selectedSetting = FindSetting(ownerKey);
            SaveLastSelectedTreeNode();
            RefreshMenuItemList();
            ShowSelectedDetail();
        }

        private MenuItemSetting? FindSetting(MenuItemScopeKey key)
        {
            if (string.IsNullOrWhiteSpace(key.GuidId)) return null;
            return _allSettings.FirstOrDefault(setting => GetScopeKey(setting) == key);
        }

        private void RefreshMenuItemList()
        {
            var searchText = SearchBox?.Text?.Trim() ?? string.Empty;
            List<MenuItemSetting> items;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                items = CurrentSurfaceSettings
                    .Where(setting => IsSearchMatch(setting, searchText))
                    .OrderBy(GetCurrentPath, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                ListTitle.Text = $"Search Results ({items.Count})";
            }
            else
            {
                MenuItemScopeKey ownerKey = _selectedOwnerKey;
                var settingsByKey = CreateSettingsByKey();
                items = CurrentSurfaceSettings
                    .Where(setting => (ResolveOwnerKey(setting, GetEffectiveOwner(setting), settingsByKey) ?? RootKey) == ownerKey)
                    .OrderBy(GetEffectiveOrder)
                    .ThenBy(GetDisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                ListTitle.Text = ownerKey == RootKey ? $"Top-level Menu Items ({items.Count})" : $"Direct Children ({items.Count})";
            }

            var rows = items.Select(CreateListRow).ToList();
            MenuItemScopeKey? selectedKey = _selectedSetting == null ? null : GetScopeKey(_selectedSetting);

            try
            {
                _isReplacingMenuItemRows = true;
                MenuItemDataGrid.ItemsSource = rows;

                if (selectedKey.HasValue)
                    MenuItemDataGrid.SelectedItem = rows.FirstOrDefault(row => GetScopeKey(row.Setting) == selectedKey.Value);
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
            var settingsByKey = CreateSettingsByKey();
            string currentGuid = GetEffectiveOwner(setting);
            MenuItemSetting currentSetting = setting;
            var visited = new HashSet<MenuItemScopeKey>();

            while (!string.IsNullOrWhiteSpace(currentGuid))
            {
                if (string.Equals(currentGuid, RootGuid, StringComparison.Ordinal))
                {
                    parts.Add("Menu");
                    break;
                }

                MenuItemScopeKey? parentKey = ResolveOwnerKey(currentSetting, currentGuid, settingsByKey);
                if (!parentKey.HasValue || !visited.Add(parentKey.Value) || !settingsByKey.TryGetValue(parentKey.Value, out var parentSetting))
                {
                    parts.Add(currentGuid);
                    break;
                }

                parts.Add(GetDisplayName(parentSetting));
                currentSetting = parentSetting;
                currentGuid = GetEffectiveOwner(parentSetting);
            }

            parts.Reverse();
            return string.Join(" > ", parts);
        }

        private string GetPathForGuid(MenuItemSetting source, string? guid)
        {
            if (string.IsNullOrWhiteSpace(guid)) return string.Empty;
            if (string.Equals(guid, RootGuid, StringComparison.Ordinal)) return "Menu";

            var settingsByKey = CreateSettingsByKey();
            MenuItemScopeKey? key = ResolveOwnerKey(source, guid, settingsByKey);
            return key.HasValue && settingsByKey.TryGetValue(key.Value, out var setting) ? GetCurrentPath(setting) : guid;
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
            _dragSourceKey = null;

            if (ShouldIgnoreDragSource(e.OriginalSource as DependencyObject))
                return;

            if (sender == MenuTreeView)
                _dragSourceKey = GetTreeNodeKey(e.OriginalSource as DependencyObject);
            else if (sender == MenuItemDataGrid)
                _dragSourceKey = GetGridRowSetting(e.OriginalSource as DependencyObject) is { } rowSetting ? GetScopeKey(rowSetting) : null;

            if (_dragSourceKey == RootKey)
                _dragSourceKey = null;
        }

        private void DragSource_MouseMove(object sender, MouseEventArgs e)
        {
            if (Mouse.LeftButton != MouseButtonState.Pressed) return;
            if (!_dragSourceKey.HasValue) return;

            var currentPosition = e.GetPosition(this);
            if (Math.Abs(currentPosition.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                && Math.Abs(currentPosition.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var sourceSetting = FindSetting(_dragSourceKey.Value);
            if (sourceSetting == null) return;

            DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(MenuItemDragFormat, GetScopeKey(sourceSetting)), DragDropEffects.Move);
            _dragSourceKey = null;
        }

        private void MenuTreeView_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (TryGetDraggedSetting(e.Data, out var setting))
            {
                MenuItemScopeKey targetOwnerKey = GetTreeNodeKey(e.OriginalSource as DependencyObject) ?? RootKey;
                if (CanMoveToOwner(setting, targetOwnerKey))
                    e.Effects = DragDropEffects.Move;
            }

            e.Handled = true;
        }

        private void MenuTreeView_Drop(object sender, DragEventArgs e)
        {
            if (!TryGetDraggedSetting(e.Data, out var setting)) return;

            MenuItemScopeKey targetOwnerKey = GetTreeNodeKey(e.OriginalSource as DependencyObject) ?? RootKey;
            MoveSettingToOwner(setting, targetOwnerKey);
            e.Handled = true;
        }

        private void MenuItemDataGrid_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.None;
            if (TryGetDraggedSetting(e.Data, out var setting))
            {
                MenuItemScopeKey targetOwnerKey = GetDropTargetOwnerKey(e.OriginalSource as DependencyObject);
                if (CanMoveToOwner(setting, targetOwnerKey))
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
                MenuItemScopeKey targetOwnerKey = ResolveOwnerKey(row.Setting, GetEffectiveOwner(row.Setting)) ?? RootKey;
                var targetChildren = GetOrderedChildren(targetOwnerKey).Where(child => !ReferenceEquals(child, setting)).ToList();
                var targetIndex = targetChildren.FindIndex(child => ReferenceEquals(child, row.Setting));
                if (targetIndex < 0) targetIndex = targetChildren.Count;

                if (e.GetPosition(targetRow).Y > targetRow.ActualHeight / 2)
                    targetIndex++;

                MoveSettingToOwner(setting, targetOwnerKey, targetIndex);
            }
            else
            {
                MoveSettingToOwner(setting, _selectedOwnerKey, int.MaxValue);
            }

            e.Handled = true;
        }

        private MenuItemScopeKey GetDropTargetOwnerKey(DependencyObject? originalSource)
        {
            var rowSetting = GetGridRowSetting(originalSource);
            return rowSetting == null
                ? _selectedOwnerKey
                : ResolveOwnerKey(rowSetting, GetEffectiveOwner(rowSetting)) ?? RootKey;
        }

        private static bool ShouldIgnoreDragSource(DependencyObject? source)
        {
            return FindVisualParent<TextBox>(source) != null
                || FindVisualParent<CheckBox>(source) != null
                || FindVisualParent<ComboBox>(source) != null
                || FindVisualParent<Button>(source) != null;
        }

        private static MenuItemScopeKey? GetTreeNodeKey(DependencyObject? source)
        {
            var treeViewItem = FindVisualParent<TreeViewItem>(source);
            return treeViewItem?.Tag is MenuItemScopeKey key ? key : null;
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
            if (data.GetData(MenuItemDragFormat) is not MenuItemScopeKey key) return false;

            setting = FindSetting(key)!;
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

        private bool CanMoveToOwner(MenuItemSetting setting, MenuItemScopeKey targetOwnerKey)
        {
            if (string.Equals(targetOwnerKey.GuidId, RootGuid, StringComparison.Ordinal))
                return targetOwnerKey == RootKey
                    && (string.Equals(setting.TargetName, _selectedTargetName, StringComparison.Ordinal)
                        || string.Equals(setting.TargetName, MenuItemConstants.GlobalTarget, StringComparison.Ordinal))
                    && MenuItemManagerService.IsValidOwnerOverride(setting, RootGuid, _allSettings);

            // Owner overrides persist a Guid only. When the same Guid exists in both the
            // target and Global scopes, runtime lookup selects the target-local item first.
            // Reject a visually selected key that cannot be represented by that persisted value.
            if (ResolveOwnerKey(setting, targetOwnerKey.GuidId) != targetOwnerKey)
                return false;

            MenuItemSetting? owner = FindSetting(targetOwnerKey);
            return owner != null
                && MenuItemManagerService.IsOwnerInAllowedScope(setting, owner)
                && MenuItemManagerService.IsValidOwnerOverride(setting, targetOwnerKey.GuidId, _allSettings);
        }

        private void MoveSettingToOwner(MenuItemSetting setting, MenuItemScopeKey targetOwnerKey, int insertIndex = int.MaxValue)
        {
            if (!CanMoveToOwner(setting, targetOwnerKey)) return;

            var targetChildren = GetOrderedChildren(targetOwnerKey)
                .Where(child => !ReferenceEquals(child, setting))
                .ToList();
            insertIndex = Math.Clamp(insertIndex, 0, targetChildren.Count);

            var orderSlots = CreateOrderSlots(targetOwnerKey, targetChildren.Count + 1);
            targetChildren.Insert(insertIndex, setting);

            setting.OwnerGuidOverride = string.Equals(NormalizeOwnerGuid(setting.OwnerGuid), targetOwnerKey.GuidId, StringComparison.Ordinal)
                ? null
                : targetOwnerKey.GuidId;

            for (var i = 0; i < targetChildren.Count; i++)
                SetOrderOverride(targetChildren[i], orderSlots[i]);

            _selectedOwnerKey = string.Equals(targetOwnerKey.GuidId, RootGuid, StringComparison.Ordinal) ? RootKey : targetOwnerKey;
            _selectedSetting = setting;
            _expandedKeys.Add(targetOwnerKey);
            SaveLastSelectedTreeNode();
            RefreshEditorPreview();
        }

        private List<MenuItemSetting> GetOrderedChildren(MenuItemScopeKey ownerKey)
        {
            var settingsByKey = CreateSettingsByKey();
            return CurrentSurfaceSettings
                .Where(setting => (ResolveOwnerKey(setting, GetEffectiveOwner(setting), settingsByKey) ?? RootKey) == ownerKey)
                .OrderBy(GetEffectiveOrder)
                .ThenBy(GetDisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private List<int> CreateOrderSlots(MenuItemScopeKey ownerKey, int requiredCount)
        {
            var slots = GetOrderedChildren(ownerKey)
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
                AddInfoRow(DetailPanel, "Target", setting.TargetName);
                AddInfoRow(DetailPanel, "GuidId", setting.GuidId);
                AddInfoRow(DetailPanel, "Default owner", GetPathForGuid(setting, setting.OwnerGuid));
                AddInfoRow(DetailPanel, "Current owner", GetPathForGuid(setting, GetEffectiveOwner(setting)));
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
                List<OwnerGuidOption> ownerOptions = GetValidOwnerGuidOptions(setting);
                MenuItemScopeKey currentOwnerKey = ResolveOwnerKey(setting, GetEffectiveOwner(setting))
                    ?? new MenuItemScopeKey(setting.TargetName, RootGuid);
                var ownerCombo = new ComboBox
                {
                    ItemsSource = ownerOptions,
                    DisplayMemberPath = nameof(OwnerGuidOption.DisplayPath),
                    SelectedItem = ownerOptions.FirstOrDefault(option => option.ScopeKey == currentOwnerKey),
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
                .Where(option => CanMoveToOwner(setting, option.ScopeKey))
                .ToList();
        }

        private void ApplyOwnerGuidFromCombo(MenuItemSetting setting, ComboBox combo)
        {
            if (_isRefreshing || _isUpdatingDetail) return;
            if (combo.SelectedItem is not OwnerGuidOption option) return;

            if (TrySetOwnerGuidOverride(setting, option))
                RefreshEditorPreview();
        }

        private bool TrySetOwnerGuidOverride(MenuItemSetting setting, OwnerGuidOption option)
        {
            string guidId = option.GuidId;
            if (string.IsNullOrWhiteSpace(guidId) || string.Equals(guidId, setting.OwnerGuid, StringComparison.Ordinal))
            {
                setting.OwnerGuidOverride = null;
                return true;
            }

            if (!CanMoveToOwner(setting, option.ScopeKey))
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

        private void TargetScopeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isRefreshing || TargetScopeComboBox.SelectedItem is not string targetName)
                return;
            if (string.Equals(_selectedTargetName, targetName, StringComparison.Ordinal))
                return;

            CaptureExpandedTreeState();
            _selectedTargetName = targetName;
            _selectedOwnerKey = RootKey;
            _selectedSetting = null;
            SaveLastSelectedTreeNode();
            RefreshEditorPreview(restoreSelection: false);
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            MenuItemScopeKey selectedKey = _selectedOwnerKey;
            MenuItemScopeKey? selectedSettingKey = _selectedSetting == null ? null : GetScopeKey(_selectedSetting);
            MenuItemManagerService.CommitEditingSnapshot(_allSettings);
            _allSettings = MenuItemManagerService.CreateEditingSnapshot();
            InitializeTargetScopes();
            if (FindSetting(selectedKey) != null)
                _selectedOwnerKey = selectedKey;
            _selectedSetting = selectedSettingKey.HasValue ? FindSetting(selectedSettingKey.Value) : null;
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

            _selectedOwnerKey = RootKey;
            _selectedSetting = null;
            RefreshEditorPreview();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void UpdateStatusText()
        {
            List<MenuItemSetting> surfaceSettings = CurrentSurfaceSettings.ToList();
            int total = surfaceSettings.Count;
            int hidden = surfaceSettings.Count(s => !s.IsVisible);
            int customOrder = surfaceSettings.Count(s => s.OrderOverride.HasValue);
            int movedItems = surfaceSettings.Count(s => !string.IsNullOrEmpty(s.OwnerGuidOverride));
            StatusText.Text = $"Total: {total} | Hidden: {hidden} | Custom Order: {customOrder} | Moved: {movedItems}";
        }
    }
}
