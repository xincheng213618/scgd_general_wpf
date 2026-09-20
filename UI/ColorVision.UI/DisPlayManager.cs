using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.Views;
using log4net;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace ColorVision.UI
{


    public static class DisPlayManagerExtension
    {

        /// <summary>
        /// 注册视图控件到 DockViewManager。
        /// 控件只需是 UserControl，标题通过 title 参数传入。
        /// 双击控件时激活对应视图，单击只保留控件自身的选择和编辑行为。
        /// </summary>
 


        public static void ApplyChangedSelectedColor(this IDisPlayControl disPlayControl, Border border)
        {
            disPlayControl.SelectChanged += (s, e) => UpdateDisPlayBorder(disPlayControl, border);
            SubscribeToThemeChanges(
                new WeakReference<IDisPlayControl>(disPlayControl),
                new WeakReference<Border>(border));
            UpdateDisPlayBorder(disPlayControl, border);
            if (disPlayControl is UserControl userControl)
                RegisterSelectionInput(disPlayControl, userControl);
        }

        private static void SubscribeToThemeChanges(
            WeakReference<IDisPlayControl> displayReference,
            WeakReference<Border> borderReference)
        {
            ThemeManager themeManager = ThemeManager.Current;
            ThemeChangedHandler? themeChangedHandler = null;
            themeChangedHandler = (s) =>
            {
                if (displayReference.TryGetTarget(out IDisPlayControl? target)
                    && borderReference.TryGetTarget(out Border? targetBorder))
                {
                    UpdateDisPlayBorder(target, targetBorder);
                    return;
                }

                themeManager.CurrentUIThemeChanged -= themeChangedHandler;
            };
            themeManager.CurrentUIThemeChanged += themeChangedHandler;
        }

        private static void UpdateDisPlayBorder(IDisPlayControl target, Border targetBorder)
        {
            if (target.IsSelected)
            {
                targetBorder.BorderBrush = ImageUtils.ConvertFromString(ThemeManager.Current.CurrentUITheme switch
                {
                    Theme.Light => "#5649B0",
                    Theme.Dark => "#A79CF1",
                    _ => "#A79CF1" // 默认颜色
                });
            }
            else
            {
                targetBorder.SetResourceReference(Border.BorderBrushProperty, "CV.Border.Weak");
            }
        }

        internal static void RegisterSelectionInput(IDisPlayControl disPlayControl, UserControl userControl)
        {
            userControl.Focusable = true;
            userControl.PreviewMouseDown += (s, e) =>
            {
                DisPlayManager.GetInstance().SelectControl(disPlayControl);
            };
            userControl.MouseDown += (s, e) =>
            {
                userControl.Focus();
            };
        }

        public static void AddViewConfig(this UserControl userControl, UserControl viewControl, string title)
        {
            var manager = DockViewManager.GetInstance();
            if (!string.IsNullOrEmpty(title))
                manager.SetViewTitle(viewControl, title);
            manager.AddView(viewControl);

            userControl.MouseDoubleClick += (s, e) =>
            {
                if (e.ChangedButton != MouseButton.Left)
                    return;

                if (DisplayPinButton.IsPinInput(e.OriginalSource as DependencyObject))
                    return;

                manager.ActiveView(viewControl);
                e.Handled = true;
            };
        }
    }

    public class DisPlayGroupConfig : ViewModelBase
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name = string.Empty;

        public bool IsExpanded { get => _IsExpanded; set { _IsExpanded = value; OnPropertyChanged(); } }
        private bool _IsExpanded = true;
    }

    public class DisPlayManagerConfig : ViewModelBase,IConfig
    {
        public const string DefaultGroupId = "__default__";
        public static DisPlayManagerConfig Instance => ConfigService.Instance.GetRequiredService<DisPlayManagerConfig>();

        public Dictionary<string, int> StoreIndex { get; set; } = new Dictionary<string, int>();
        public HashSet<string> PinnedControls { get; set; } = new();
        public HashSet<string> HiddenControls { get; set; } = new();
        public Dictionary<string, string> ControlGroups { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, bool> ControlExpandedStates { get; set; } = new Dictionary<string, bool>();
        public ObservableCollection<DisPlayGroupConfig> Groups { get; set; } = new ObservableCollection<DisPlayGroupConfig>();

        public int LastSelectIndex { get => _LastSelectIndex; set { _LastSelectIndex = value; OnPropertyChanged(); } }
        private int _LastSelectIndex ;

        public string LastSelectedControlKey { get; set; } = string.Empty;
    }



    public class DisPlayManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DisPlayManager));
        private static DisPlayManager _instance;
        private static readonly object _locker = new();
        public static DisPlayManager GetInstance() { lock (_locker) { return _instance ??= new DisPlayManager(); } }
        public static ICommand CreateGroupCommand { get; } = new RelayCommand(_ => GetInstance().CreateGroup());
        public static ICommand ManageControlsCommand { get; } = new RelayCommand(_ => GetInstance().ShowManagementWindow());
        public ObservableCollection<IDisPlayControl> IDisPlayControls { get; private set; }
        private const string DragDataFormat = "ColorVision.UI.DisPlayControl";
        private static readonly TimeSpan DisplayDragPressDelay = TimeSpan.FromMilliseconds(260);
        private IDisPlayControl? _selectedControl;
        private IDisPlayControl? _dragSourceControl;
        private Point _dragStartPoint;
        private DateTime _dragStartTime;
        private bool _suppressCollectionChanged;
        private bool _isInitialized;
        private readonly Dictionary<ToggleButton, RoutedEventHandler> _expansionHandlers = new();
        private DispatcherTimer? _saveTimer;
        private Window? _ownerWindow;

        internal DisPlayManager()
        {
            IDisPlayControls = new ObservableCollection<IDisPlayControl>();
        }

        public event EventHandler? SelectedControlChanged;

        public IDisPlayControl? SelectedControl => _selectedControl;

        public StackPanel StackPanel { get; set; } = null!;

        public ObservableCollection<DisPlayGroupConfig> Groups
        {
            get
            {
                EnsureDefaultGroup();
                return DisPlayManagerConfig.Instance.Groups;
            }
        }

        public void Init(Window window, StackPanel stackPanel)
        {
            if (_ownerWindow != null)
                _ownerWindow.Closing -= OwnerWindow_Closing;
            _ownerWindow = window;
            _ownerWindow.Closing += OwnerWindow_Closing;

            StackPanel = stackPanel;
            StackPanel.AllowDrop = true;
            StackPanel.Drop -= StackPanel_Drop;
            StackPanel.Drop += StackPanel_Drop;

            IDisPlayControls.CollectionChanged -= IDisPlayControls_CollectionChanged;
            IDisPlayControls.CollectionChanged += IDisPlayControls_CollectionChanged;
            _isInitialized = true;
            RebuildPanel();

            if (IDisPlayControls.Count > 0)
                RestoreLastSelectedControl();
        }

        private void IDisPlayControls_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (_suppressCollectionChanged || !_isInitialized)
                return;

            if (e.OldItems != null)
            {
                foreach (IDisPlayControl control in e.OldItems.OfType<IDisPlayControl>())
                    DetachExpansionState(control);
            }

            bool configChanged = e.NewItems != null
                && MigrateLegacyControlSettings(e.NewItems.OfType<IDisPlayControl>());
            if (configChanged)
                ScheduleConfigSave();
            RebuildPanel();
        }

        /// <summary>
        /// Replaces the complete display-control snapshot and rebuilds the visual
        /// panel once. Startup service discovery can otherwise trigger a full panel
        /// rebuild for every device added to the observable collection.
        /// </summary>
        public void ReplaceControls(IEnumerable<IDisPlayControl> controls)
        {
            ArgumentNullException.ThrowIfNull(controls);
            _suppressCollectionChanged = true;
            try
            {
                foreach (IDisPlayControl control in IDisPlayControls)
                    DetachExpansionState(control);
                IDisPlayControls.Clear();
                foreach (IDisPlayControl control in controls)
                {
                    IDisPlayControls.Add(control);
                }
            }
            finally
            {
                _suppressCollectionChanged = false;
            }

            RestoreControl();
            RestoreLastSelectedControl();
        }

        private void RestoreLastSelectedControl()
        {
            List<IDisPlayControl> visibleControls = IDisPlayControls.Where(IsControlVisible).ToList();
            if (visibleControls.Count == 0)
            {
                ClearSelection();
                return;
            }

            EnsureConfigCollections();
            var config = DisPlayManagerConfig.Instance;
            IDisPlayControl? selected = null;
            if (!string.IsNullOrWhiteSpace(config.LastSelectedControlKey))
            {
                selected = visibleControls.FirstOrDefault(control =>
                    string.Equals(GetControlKey(control), config.LastSelectedControlKey, StringComparison.Ordinal)
                    || string.Equals(control.DisPlayName, config.LastSelectedControlKey, StringComparison.Ordinal));
            }

            if (selected == null)
            {
                int index = config.LastSelectIndex;
                if (index < 0 || index >= IDisPlayControls.Count || !IsControlVisible(IDisPlayControls[index]))
                {
                    index = IDisPlayControls.IndexOf(visibleControls[0]);
                    config.LastSelectIndex = index;
                }
                selected = IDisPlayControls[index];
            }

            SelectControl(selected);
        }

        private void ClearSelection()
        {
            bool selectionChanged = _selectedControl != null;
            if (_selectedControl != null)
                _selectedControl.IsSelected = false;
            _selectedControl = null;
            if (StackPanel != null)
                StackPanel.Tag = null;

            var config = DisPlayManagerConfig.Instance;
            config.LastSelectIndex = -1;
            config.LastSelectedControlKey = string.Empty;
            if (selectionChanged)
                SelectedControlChanged?.Invoke(this, EventArgs.Empty);
            ScheduleConfigSave();
        }

        public void SelectControl(IDisPlayControl disPlayControl)
        {
            ArgumentNullException.ThrowIfNull(disPlayControl);
            bool selectionChanged = !ReferenceEquals(_selectedControl, disPlayControl);
            if (_selectedControl != null && !ReferenceEquals(_selectedControl, disPlayControl))
                _selectedControl.IsSelected = false;

            _selectedControl = disPlayControl;
            if (!disPlayControl.IsSelected)
                disPlayControl.IsSelected = true;

            if (StackPanel != null)
                StackPanel.Tag = disPlayControl;

            int index = IDisPlayControls.IndexOf(disPlayControl);
            if (index >= 0)
            {
                var config = DisPlayManagerConfig.Instance;
                string key = GetControlKey(disPlayControl);
                bool stateChanged = config.LastSelectIndex != index
                    || !string.Equals(config.LastSelectedControlKey, key, StringComparison.Ordinal);
                config.LastSelectIndex = index;
                config.LastSelectedControlKey = key;
                if (stateChanged)
                    ScheduleConfigSave();
            }

            if (selectionChanged)
                SelectedControlChanged?.Invoke(this, EventArgs.Empty);
        }

        private static void EnsureConfigCollections()
        {
            var config = DisPlayManagerConfig.Instance;
            config.StoreIndex ??= new Dictionary<string, int>();
            config.PinnedControls ??= new HashSet<string>();
            config.HiddenControls ??= new HashSet<string>();
            config.ControlGroups ??= new Dictionary<string, string>();
            config.ControlExpandedStates ??= new Dictionary<string, bool>();
            config.Groups ??= new ObservableCollection<DisPlayGroupConfig>();
            config.LastSelectedControlKey ??= string.Empty;
        }

        private static DisPlayGroupConfig EnsureDefaultGroup()
        {
            EnsureConfigCollections();
            var config = DisPlayManagerConfig.Instance;
            var defaultGroup = config.Groups.FirstOrDefault(a => a.Id == DisPlayManagerConfig.DefaultGroupId);
            if (defaultGroup != null)
                return defaultGroup;

            defaultGroup = new DisPlayGroupConfig
            {
                Id = DisPlayManagerConfig.DefaultGroupId,
                Name = "默认分组",
                IsExpanded = true
            };
            config.Groups.Insert(0, defaultGroup);
            return defaultGroup;
        }

        internal static bool IsDefaultGroup(string groupId) => groupId == DisPlayManagerConfig.DefaultGroupId;

        private static string GetControlKey(IDisPlayControl control)
        {
            string key = control.PersistenceKey;
            return string.IsNullOrWhiteSpace(key) ? control.DisPlayName : key;
        }

        private static bool MigrateLegacyControlSettings(IEnumerable<IDisPlayControl> controls)
        {
            EnsureConfigCollections();
            var config = DisPlayManagerConfig.Instance;
            bool changed = false;
            foreach (IDisPlayControl control in controls)
            {
                string key = GetControlKey(control);
                string legacyKey = control.DisPlayName;
                if (string.Equals(key, legacyKey, StringComparison.Ordinal))
                    continue;

                if (!config.StoreIndex.ContainsKey(key)
                    && config.StoreIndex.TryGetValue(legacyKey, out int storedIndex))
                {
                    config.StoreIndex[key] = storedIndex;
                    changed = true;
                }

                if (!config.PinnedControls.Contains(key) && config.PinnedControls.Contains(legacyKey))
                {
                    config.PinnedControls.Add(key);
                    changed = true;
                }

                if (!config.HiddenControls.Contains(key) && config.HiddenControls.Contains(legacyKey))
                {
                    config.HiddenControls.Add(key);
                    changed = true;
                }

                if (!config.ControlGroups.ContainsKey(key)
                    && config.ControlGroups.TryGetValue(legacyKey, out string? groupId))
                {
                    config.ControlGroups[key] = groupId;
                    changed = true;
                }

                if (!config.ControlExpandedStates.ContainsKey(key)
                    && config.ControlExpandedStates.TryGetValue(legacyKey, out bool isExpanded))
                {
                    config.ControlExpandedStates[key] = isExpanded;
                    changed = true;
                }

                if (string.Equals(config.LastSelectedControlKey, legacyKey, StringComparison.Ordinal))
                {
                    config.LastSelectedControlKey = key;
                    changed = true;
                }
            }
            return changed;
        }

        private static List<DisPlayGroupConfig> GetGroupsInOrder()
        {
            EnsureDefaultGroup();

            var config = DisPlayManagerConfig.Instance;
            return config.Groups
                .Where(a => !string.IsNullOrWhiteSpace(a.Id))
                .GroupBy(a => a.Id)
                .Select(a => a.First())
                .OrderBy(a => IsDefaultGroup(a.Id) ? 0 : 1)
                .ThenBy(a => config.Groups.IndexOf(a))
                .ToList();
        }

        private static string GetGroupKey(IDisPlayControl disPlayControl)
        {
            EnsureDefaultGroup();
            var config = DisPlayManagerConfig.Instance;
            if (config.ControlGroups.TryGetValue(GetControlKey(disPlayControl), out string? groupId)
                && config.Groups.Any(a => a.Id == groupId))
            {
                return groupId;
            }

            return DisPlayManagerConfig.DefaultGroupId;
        }

        private static int GetGroupOrder(string groupId)
        {
            var groups = GetGroupsInOrder().ToList();
            int index = groups.FindIndex(a => a.Id == groupId);
            return index < 0 ? int.MaxValue : index;
        }

        private static int GetStoredIndex(IDisPlayControl disPlayControl)
        {
            return DisPlayManagerConfig.Instance.StoreIndex.TryGetValue(GetControlKey(disPlayControl), out int index)
                ? index
                : int.MaxValue;
        }

        private static int CompareDisplayControls(IDisPlayControl a, IDisPlayControl b)
        {
            int groupCompare = GetGroupOrder(GetGroupKey(a)).CompareTo(GetGroupOrder(GetGroupKey(b)));
            if (groupCompare != 0)
                return groupCompare;

            int pinCompare = IsPinned(b).CompareTo(IsPinned(a));
            if (pinCompare != 0)
                return pinCompare;

            int indexCompare = GetStoredIndex(a).CompareTo(GetStoredIndex(b));
            if (indexCompare != 0)
                return indexCompare;

            return string.Compare(a.DisPlayName, b.DisPlayName, StringComparison.OrdinalIgnoreCase);
        }

        private List<IDisPlayControl> GetControlsInGroup(string groupId, bool applyPins = true, bool includeHidden = false)
        {
            return IDisPlayControls
                .Where(a => GetGroupKey(a) == groupId && (includeHidden || IsControlVisible(a)))
                .OrderBy(a => applyPins && IsPinned(a) ? 0 : 1)
                .ThenBy(GetStoredIndex)
                .ThenBy(a => a.DisPlayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool IsPinned(IDisPlayControl control)
        {
            EnsureConfigCollections();
            return DisPlayManagerConfig.Instance.PinnedControls.Contains(GetControlKey(control));
        }

        public static bool IsControlVisible(IDisPlayControl control)
        {
            EnsureConfigCollections();
            return !DisPlayManagerConfig.Instance.HiddenControls.Contains(GetControlKey(control));
        }

        public void SetControlVisible(IDisPlayControl control, bool isVisible)
        {
            if (!IDisPlayControls.Contains(control))
                return;

            EnsureConfigCollections();
            string key = GetControlKey(control);
            bool changed = isVisible
                ? DisPlayManagerConfig.Instance.HiddenControls.Remove(key)
                : DisPlayManagerConfig.Instance.HiddenControls.Add(key);
            if (isVisible && !string.Equals(key, control.DisPlayName, StringComparison.Ordinal))
                changed |= DisPlayManagerConfig.Instance.HiddenControls.Remove(control.DisPlayName);
            if (!changed)
                return;

            RebuildPanel();
            if (!isVisible && ReferenceEquals(_selectedControl, control))
            {
                IDisPlayControl? replacement = IDisPlayControls.FirstOrDefault(IsControlVisible);
                if (replacement != null)
                    SelectControl(replacement);
                else
                    ClearSelection();
            }
            ScheduleConfigSave();
        }

        public bool IsControlExpanded(IDisPlayControl control)
        {
            EnsureConfigCollections();
            string key = GetControlKey(control);
            if (DisPlayManagerConfig.Instance.ControlExpandedStates.TryGetValue(key, out bool isExpanded))
                return isExpanded;

            isExpanded = true;
            DisPlayManagerConfig.Instance.ControlExpandedStates[key] = isExpanded;
            ScheduleConfigSave();
            return isExpanded;
        }

        public void SetControlExpanded(IDisPlayControl control, bool isExpanded)
        {
            if (!IDisPlayControls.Contains(control))
                return;

            EnsureConfigCollections();
            string key = GetControlKey(control);
            if (control is UserControl userControl && FindDisplayHeaderToggle(userControl) is ToggleButton header)
                header.SetCurrentValue(ToggleButton.IsCheckedProperty, isExpanded);

            if (DisPlayManagerConfig.Instance.ControlExpandedStates.TryGetValue(key, out bool stored)
                && stored == isExpanded)
            {
                return;
            }

            DisPlayManagerConfig.Instance.ControlExpandedStates[key] = isExpanded;
            ScheduleConfigSave();
        }

        public string GetControlGroupId(IDisPlayControl control) => GetGroupKey(control);

        public void SetControlGroup(IDisPlayControl control, string groupId)
        {
            if (!IDisPlayControls.Contains(control) || GetGroupKey(control) == groupId)
                return;

            MoveControlToGroup(GetControlKey(control), groupId,
                GetControlsInGroup(groupId, includeHidden: true).Count);
        }

        public void MoveControl(IDisPlayControl control, int offset)
        {
            if (!IDisPlayControls.Contains(control) || offset == 0)
                return;

            string groupId = GetGroupKey(control);
            List<IDisPlayControl> controls = GetControlsInGroup(groupId, applyPins: false, includeHidden: true);
            int oldIndex = controls.IndexOf(control);
            int newIndex = Math.Clamp(oldIndex + offset, 0, controls.Count - 1);
            if (oldIndex < 0 || oldIndex == newIndex)
                return;

            controls.RemoveAt(oldIndex);
            controls.Insert(newIndex, control);
            UpdateGroupIndexes(controls);
            ArrangeCollectionByConfig();
            RebuildPanel();
            ScheduleConfigSave();
        }

        internal void MoveGroup(DisPlayGroupConfig group, int offset)
        {
            if (IsDefaultGroup(group.Id) || offset == 0)
                return;

            ObservableCollection<DisPlayGroupConfig> groups = DisPlayManagerConfig.Instance.Groups;
            int oldIndex = groups.IndexOf(group);
            if (oldIndex < 0)
                return;

            int firstCustomIndex = groups.IndexOf(EnsureDefaultGroup()) + 1;
            int newIndex = Math.Clamp(oldIndex + offset, firstCustomIndex, groups.Count - 1);
            if (oldIndex == newIndex)
                return;

            groups.Move(oldIndex, newIndex);
            ArrangeCollectionByConfig();
            RebuildPanel();
            ScheduleConfigSave();
        }

        internal void SetGroupExpanded(DisPlayGroupConfig group, bool isExpanded)
        {
            if (group.IsExpanded == isExpanded)
                return;

            group.IsExpanded = isExpanded;
            RebuildPanel();
            ScheduleConfigSave();
        }

        public void SetPinned(IDisPlayControl control, bool pinned)
        {
            if (!IDisPlayControls.Contains(control))
                return;

            EnsureConfigCollections();
            string key = GetControlKey(control);
            bool changed = pinned
                ? DisPlayManagerConfig.Instance.PinnedControls.Add(key)
                : DisPlayManagerConfig.Instance.PinnedControls.Remove(key);
            if (!pinned && !string.Equals(key, control.DisPlayName, StringComparison.Ordinal))
                changed |= DisPlayManagerConfig.Instance.PinnedControls.Remove(control.DisPlayName);
            if (!changed)
                return;

            ArrangeCollectionByConfig();
            RebuildPanel();
            ScheduleConfigSave();
        }

        private void RebuildPanel()
        {
            if (StackPanel == null)
                return;

            EnsureDefaultGroup();
            DetachDisplayControlsFromParents();
            StackPanel.Children.Clear();

            bool showGroups = DisPlayManagerConfig.Instance.Groups.Any(a => !IsDefaultGroup(a.Id));
            if (!showGroups)
            {
                StackPanel.Tag = _selectedControl;
                StackPanel.AllowDrop = true;
                foreach (IDisPlayControl item in GetControlsInGroup(DisPlayManagerConfig.DefaultGroupId))
                    AddDisplayControl(StackPanel, item);
            }
            else
            {
                var groupSections = GetGroupsInOrder()
                    .Select(group => (Group: group, Controls: GetControlsInGroup(group.Id).ToList()))
                    .Where(section => !IsDefaultGroup(section.Group.Id) || section.Controls.Count > 0)
                    .ToList();

                foreach (var sectionData in groupSections)
                {
                    var section = CreateGroupSection(sectionData.Group, sectionData.Controls);
                    StackPanel.Children.Add(section);
                }
            }
        }

        private void DetachDisplayControlsFromParents()
        {
            foreach (var userControl in IDisPlayControls.OfType<UserControl>())
            {
                if (userControl.Parent is Panel parent)
                    parent.Children.Remove(userControl);
                else if (userControl.Parent is ContentControl contentControl && ReferenceEquals(contentControl.Content, userControl))
                    contentControl.Content = null;
            }
        }

        private StackPanel CreateGroupSection(DisPlayGroupConfig group, List<IDisPlayControl> controls)
        {
            var section = new StackPanel
            {
                Margin = new Thickness(0, 0, 3, 2),
                Tag = group.Id,
                AllowDrop = true
            };
            section.Drop += Group_Drop;

            var header = new Border
            {
                MinHeight = 22,
                Padding = new Thickness(4, 1, 2, 1),
                Margin = new Thickness(0),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Cursor = Cursors.Hand,
                Tag = group.Id,
                AllowDrop = true
            };
            header.SetResourceReference(Border.BackgroundProperty, "GlobalBackground");
            header.SetResourceReference(Border.BorderBrushProperty, "GlobalBorderBrush");
            AttachGroupDropFeedback(header);
            header.MouseLeftButtonUp += (s, e) =>
            {
                group.IsExpanded = !group.IsExpanded;
                RebuildPanel();
                ScheduleConfigSave();
                e.Handled = true;
            };
            header.Drop += Group_Drop;

            var headerPanel = new DockPanel();
            header.Child = headerPanel;

            var menuButton = new Button
            {
                Content = "⋯",
                Padding = new Thickness(4, 0, 4, 0),
                Margin = new Thickness(4, 0, 0, 0),
                MinWidth = 18,
                Height = 18,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Opacity = 0.6,
                Tag = group
            };
            menuButton.ContextMenu = CreateGroupContextMenu(group);
            menuButton.Click += (s, e) =>
            {
                if (menuButton.ContextMenu != null)
                {
                    menuButton.ContextMenu.PlacementTarget = menuButton;
                    menuButton.ContextMenu.IsOpen = true;
                }
                e.Handled = true;
            };
            DockPanel.SetDock(menuButton, Dock.Right);
            headerPanel.Children.Add(menuButton);

            var arrow = new TextBlock
            {
                Text = group.IsExpanded ? "▾" : "▸",
                Width = 14,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            arrow.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            DockPanel.SetDock(arrow, Dock.Left);
            headerPanel.Children.Add(arrow);

            var count = new TextBlock
            {
                Text = controls.Count.ToString(),
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,
                Opacity = 0.55
            };
            count.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            DockPanel.SetDock(count, Dock.Right);
            headerPanel.Children.Add(count);

            var title = new TextBlock
            {
                Text = GetGroupDisplayName(group),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            headerPanel.Children.Add(title);

            section.Children.Add(header);

            var content = new StackPanel
            {
                Tag = group.Id,
                AllowDrop = true,
                Visibility = group.IsExpanded ? Visibility.Visible : Visibility.Collapsed
            };
            content.Drop += Group_Drop;
            foreach (IDisPlayControl item in controls)
                AddDisplayControl(content, item);

            section.Children.Add(content);
            return section;
        }

        private static string GetGroupDisplayName(DisPlayGroupConfig group)
        {
            if (IsDefaultGroup(group.Id))
                return "默认";

            return string.IsNullOrWhiteSpace(group.Name) ? "未命名分组" : group.Name.Trim();
        }

        private static void AttachGroupDropFeedback(Border border)
        {
            border.MouseEnter += (s, e) => SetGroupHeaderHighlight(border, true);
            border.MouseLeave += (s, e) => SetGroupHeaderHighlight(border, false);
            border.DragEnter += GroupDropTarget_DragEnter;
            border.DragOver += GroupDropTarget_DragOver;
            border.DragLeave += GroupDropTarget_DragLeave;
            border.Drop += GroupDropTarget_Drop;
        }

        private static void GroupDropTarget_DragEnter(object sender, DragEventArgs e)
        {
            if (sender is Border border && e.Data.GetDataPresent(DragDataFormat))
            {
                SetGroupHeaderHighlight(border, true);
                e.Effects = DragDropEffects.Move;
            }
        }

        private static void GroupDropTarget_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DragDataFormat))
            {
                e.Effects = DragDropEffects.Move;
                e.Handled = true;
            }
        }

        private static void GroupDropTarget_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border)
                SetGroupHeaderHighlight(border, false);
        }

        private static void GroupDropTarget_Drop(object sender, DragEventArgs e)
        {
            if (sender is Border border)
                SetGroupHeaderHighlight(border, false);
        }

        private static void SetGroupHeaderHighlight(Border border, bool isHighlighted)
        {
            border.SetResourceReference(Border.BackgroundProperty, isHighlighted ? "GlobalBorderBrush1" : "GlobalBackground");
        }

        private ContextMenu CreateGroupContextMenu(DisPlayGroupConfig group)
        {
            var contextMenu = new ContextMenu();

            var renameItem = new MenuItem { Header = "重命名" };
            renameItem.Click += (s, e) => RenameGroup(group);
            contextMenu.Items.Add(renameItem);

            if (!IsDefaultGroup(group.Id))
            {
                var deleteItem = new MenuItem { Header = "删除分组" };
                deleteItem.Click += (s, e) => DeleteGroup(group);
                contextMenu.Items.Add(deleteItem);
            }

            return contextMenu;
        }

        private void AddDisplayControl(StackPanel panel, IDisPlayControl item)
        {
            if (item is not UserControl userControl)
                return;

            AttachExpansionState(item, userControl);
            userControl.AllowDrop = true;
            userControl.PreviewMouseLeftButtonDown -= DisplayControl_PreviewMouseLeftButtonDown;
            userControl.PreviewMouseLeftButtonDown += DisplayControl_PreviewMouseLeftButtonDown;
            userControl.PreviewMouseLeftButtonUp -= DisplayControl_PreviewMouseLeftButtonUp;
            userControl.PreviewMouseLeftButtonUp += DisplayControl_PreviewMouseLeftButtonUp;
            userControl.PreviewMouseRightButtonDown -= DisplayControl_PreviewMouseRightButtonDown;
            userControl.PreviewMouseRightButtonDown += DisplayControl_PreviewMouseRightButtonDown;
            userControl.PreviewMouseMove -= DisplayControl_PreviewMouseMove;
            userControl.PreviewMouseMove += DisplayControl_PreviewMouseMove;
            userControl.ContextMenuOpening -= DisplayControl_ContextMenuOpening;
            userControl.ContextMenuOpening += DisplayControl_ContextMenuOpening;
            userControl.Drop -= DisplayControl_Drop;
            userControl.Drop += DisplayControl_Drop;
            userControl.Margin = new Thickness(userControl.Margin.Left, 0, userControl.Margin.Right, 2);
            panel.Children.Add(userControl);
        }

        private void AttachExpansionState(IDisPlayControl control, UserControl userControl)
        {
            ToggleButton? header = FindDisplayHeaderToggle(userControl);
            if (header == null)
                return;

            if (_expansionHandlers.Remove(header, out RoutedEventHandler? existingHandler))
            {
                header.Checked -= existingHandler;
                header.Unchecked -= existingHandler;
            }

            EnsureConfigCollections();
            var config = DisPlayManagerConfig.Instance;
            string key = GetControlKey(control);
            bool isExpanded;
            bool configChanged = false;
            if (!config.ControlExpandedStates.TryGetValue(key, out isExpanded))
            {
                isExpanded = header.IsChecked != false;
                config.ControlExpandedStates[key] = isExpanded;
                configChanged = true;
            }

            header.SetCurrentValue(ToggleButton.IsCheckedProperty, isExpanded);
            RoutedEventHandler handler = (_, _) =>
            {
                bool current = header.IsChecked == true;
                if (config.ControlExpandedStates.TryGetValue(key, out bool stored) && stored == current)
                    return;

                config.ControlExpandedStates[key] = current;
                ScheduleConfigSave();
            };
            header.Checked += handler;
            header.Unchecked += handler;
            _expansionHandlers[header] = handler;

            if (configChanged)
                ScheduleConfigSave();
        }

        private void DetachExpansionState(IDisPlayControl control)
        {
            if (control is not UserControl userControl)
                return;

            ToggleButton? header = FindDisplayHeaderToggle(userControl);
            if (header != null && _expansionHandlers.Remove(header, out RoutedEventHandler? handler))
            {
                header.Checked -= handler;
                header.Unchecked -= handler;
            }
        }

        private static ToggleButton? FindDisplayHeaderToggle(DependencyObject owner)
        {
            if (owner is ToggleButton { Name: "DisplayHeaderToggle" } namedHeader)
                return namedHeader;

            if (owner is FrameworkElement element
                && element.FindName("DisplayHeaderToggle") is ToggleButton registeredHeader)
            {
                return registeredHeader;
            }

            int childCount = owner is Visual || owner is Visual3D
                ? VisualTreeHelper.GetChildrenCount(owner)
                : 0;
            for (int i = 0; i < childCount; i++)
            {
                if (FindDisplayHeaderToggle(VisualTreeHelper.GetChild(owner, i)) is ToggleButton childHeader)
                    return childHeader;
            }

            return null;
        }

        private void DisplayControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragSourceControl = null;
            if (sender is IDisPlayControl disPlayControl && sender is DependencyObject owner && CanStartDisplayDrag(e.OriginalSource as DependencyObject, owner))
            {
                _dragSourceControl = disPlayControl;
                _dragStartPoint = e.GetPosition(null);
                _dragStartTime = DateTime.UtcNow;
            }
        }

        private void DisplayControl_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => ClearDisplayDragCandidate();

        private void DisplayControl_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e) => ClearDisplayDragCandidate();

        private void DisplayControl_ContextMenuOpening(object sender, ContextMenuEventArgs e) => ClearDisplayDragCandidate();

        private void DisplayControl_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_dragSourceControl == null)
                return;

            if (e.LeftButton != MouseButtonState.Pressed)
            {
                ClearDisplayDragCandidate();
                return;
            }

            if (DateTime.UtcNow - _dragStartTime < DisplayDragPressDelay)
                return;

            Point position = e.GetPosition(null);
            if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            if (_dragSourceControl is UserControl userControl)
            {
                var data = new DataObject(DragDataFormat, GetControlKey(_dragSourceControl));
                try
                {
                    DragDrop.DoDragDrop(userControl, data, DragDropEffects.Move);
                }
                finally
                {
                    ClearDisplayDragCandidate();
                }
            }
        }

        private void ClearDisplayDragCandidate() => _dragSourceControl = null;

        private static bool CanStartDisplayDrag(DependencyObject? source, DependencyObject owner)
        {
            ToggleButton? headerToggle = null;
            while (source != null)
            {
                if (source is TextBoxBase || source is Slider || source is ComboBox || source is ScrollBar || source is Thumb)
                    return false;

                if (source is FrameworkElement { Name: "DisPlayBorder" })
                    return false;

                if (headerToggle == null && source is ToggleButton toggleButton && IsDisplayHeaderToggle(toggleButton))
                    headerToggle = toggleButton;

                if (ReferenceEquals(source, owner))
                    return headerToggle != null;

                source = GetDisplayDragParent(source);
            }
            return headerToggle != null;
        }

        private static DependencyObject? GetDisplayDragParent(DependencyObject source)
        {
            if (source is Visual || source is Visual3D)
                return VisualTreeHelper.GetParent(source);

            if (source is FrameworkContentElement contentElement)
                return contentElement.Parent;

            return LogicalTreeHelper.GetParent(source);
        }

        private static bool IsDisplayHeaderToggle(ToggleButton toggleButton)
        {
            return toggleButton.Style != null && ReferenceEquals(toggleButton.Style, toggleButton.TryFindResource("ButtonPageControl1"));
        }

        private void DisplayControl_Drop(object sender, DragEventArgs e)
        {
            if (sender is not IDisPlayControl targetControl || !TryGetDraggedControlKey(e, out string draggedKey))
                return;

            string targetGroupId = GetGroupKey(targetControl);
            var controls = GetControlsInGroup(targetGroupId).Where(a => GetControlKey(a) != draggedKey).ToList();
            int targetIndex = controls.FindIndex(a => ReferenceEquals(a, targetControl));
            if (targetIndex < 0)
                targetIndex = controls.Count;

            if (sender is FrameworkElement targetElement && e.GetPosition(targetElement).Y > targetElement.ActualHeight / 2)
                targetIndex++;

            MoveControlToGroup(draggedKey, targetGroupId, targetIndex);
            e.Handled = true;
        }

        private void Group_Drop(object sender, DragEventArgs e)
        {
            if (!TryGetDraggedControlKey(e, out string draggedKey))
                return;

            string groupId = (sender as FrameworkElement)?.Tag as string ?? DisPlayManagerConfig.DefaultGroupId;
            MoveControlToGroup(draggedKey, groupId, GetControlsInGroup(groupId).Count);
            e.Handled = true;
        }

        private void StackPanel_Drop(object sender, DragEventArgs e)
        {
            if (!TryGetDraggedControlKey(e, out string draggedKey))
                return;

            MoveControlToGroup(draggedKey, DisPlayManagerConfig.DefaultGroupId, GetControlsInGroup(DisPlayManagerConfig.DefaultGroupId).Count);
            e.Handled = true;
        }

        private static bool TryGetDraggedControlKey(DragEventArgs e, out string controlKey)
        {
            controlKey = e.Data.GetDataPresent(DragDataFormat)
                ? e.Data.GetData(DragDataFormat) as string ?? string.Empty
                : string.Empty;

            return !string.IsNullOrWhiteSpace(controlKey);
        }

        private void MoveControlToGroup(string controlKey, string groupId, int insertIndex)
        {
            EnsureDefaultGroup();
            if (!DisPlayManagerConfig.Instance.Groups.Any(a => a.Id == groupId))
                groupId = DisPlayManagerConfig.DefaultGroupId;

            var draggedControl = IDisPlayControls.FirstOrDefault(a => GetControlKey(a) == controlKey);
            if (draggedControl == null)
                return;

            var config = DisPlayManagerConfig.Instance;
            string oldGroupId = GetGroupKey(draggedControl);
            config.ControlGroups[controlKey] = groupId;
            if (!string.Equals(controlKey, draggedControl.DisPlayName, StringComparison.Ordinal))
                config.ControlGroups.Remove(draggedControl.DisPlayName);

            var visibleControls = GetControlsInGroup(groupId).Where(a => GetControlKey(a) != controlKey).ToList();
            insertIndex = Math.Clamp(insertIndex, 0, visibleControls.Count);
            // Translate the visible drop position back into the base order. Pinning
            // must never bake the pinned partition into the saved field order.
            bool pinned = IsPinned(draggedControl);
            var next = visibleControls.Skip(insertIndex).FirstOrDefault(a => IsPinned(a) == pinned);
            var previous = visibleControls.Take(insertIndex).LastOrDefault(a => IsPinned(a) == pinned);
            var targetControls = GetControlsInGroup(groupId, applyPins: false, includeHidden: true).Where(a => GetControlKey(a) != controlKey).ToList();
            int baseIndex = next != null ? targetControls.IndexOf(next)
                : previous != null ? targetControls.IndexOf(previous) + 1 : targetControls.Count;
            targetControls.Insert(baseIndex, draggedControl);
            UpdateGroupIndexes(targetControls);

            if (oldGroupId != groupId)
                UpdateGroupIndexes(GetControlsInGroup(oldGroupId, applyPins: false, includeHidden: true));

            ArrangeCollectionByConfig();
            RebuildPanel();
            ScheduleConfigSave();
        }

        private static bool UpdateGroupIndexes(List<IDisPlayControl> controls)
        {
            bool changed = false;
            for (int i = 0; i < controls.Count; i++)
            {
                string key = GetControlKey(controls[i]);
                if (!DisPlayManagerConfig.Instance.StoreIndex.TryGetValue(key, out int storedIndex) || storedIndex != i)
                {
                    DisPlayManagerConfig.Instance.StoreIndex[key] = i;
                    changed = true;
                }
            }
            return changed;
        }

        private void ArrangeCollectionByConfig()
        {
            _suppressCollectionChanged = true;
            try
            {
                IDisPlayControls.Sort(CompareDisplayControls);
            }
            finally
            {
                _suppressCollectionChanged = false;
            }
            if (_selectedControl != null && IDisPlayControls.Contains(_selectedControl))
            {
                DisPlayManagerConfig.Instance.LastSelectIndex = IDisPlayControls.IndexOf(_selectedControl);
                DisPlayManagerConfig.Instance.LastSelectedControlKey = GetControlKey(_selectedControl);
            }
        }

        internal void CreateGroup()
        {
            string defaultName = $"分组 {DisPlayManagerConfig.Instance.Groups.Count(a => !IsDefaultGroup(a.Id)) + 1}";
            string? name = ShowTextDialog("新建分组", "分组名称", defaultName);
            if (string.IsNullOrWhiteSpace(name))
                return;

            DisPlayManagerConfig.Instance.Groups.Add(new DisPlayGroupConfig
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = name.Trim(),
                IsExpanded = true
            });

            RebuildPanel();
            ScheduleConfigSave();
        }

        internal void RenameGroup(DisPlayGroupConfig group)
        {
            string? name = ShowTextDialog("重命名分组", "分组名称", group.Name);
            if (string.IsNullOrWhiteSpace(name))
                return;

            group.Name = name.Trim();
            RebuildPanel();
            ScheduleConfigSave();
        }

        internal void DeleteGroup(DisPlayGroupConfig group)
        {
            if (IsDefaultGroup(group.Id))
                return;

            MessageBoxResult result = MessageBox.Show(Application.Current.GetActiveWindow(), $"删除分组“{group.Name}”？分组内控件会移回默认分组。", "ColorVision", MessageBoxButton.OKCancel);
            if (result != MessageBoxResult.OK)
                return;

            var config = DisPlayManagerConfig.Instance;
            foreach (var key in config.ControlGroups.Where(a => a.Value == group.Id).Select(a => a.Key).ToList())
                config.ControlGroups[key] = DisPlayManagerConfig.DefaultGroupId;

            config.Groups.Remove(group);
            ArrangeCollectionByConfig();
            RebuildPanel();
            ScheduleConfigSave();
        }

        private static string? ShowTextDialog(string title, string label, string value)
        {
            var owner = Application.Current.GetActiveWindow();
            var window = new Window
            {
                Title = title,
                Width = 320,
                Height = 135,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
                Owner = owner,
                Background = Application.Current.FindResource("GlobalBackground") as Brush
            };

            var root = new Grid { Margin = new Thickness(12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelBlock = new TextBlock { Text = label, Margin = new Thickness(0, 0, 0, 6) };
            labelBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            root.Children.Add(labelBlock);

            var textBox = new TextBox { Text = value, MinHeight = 24 };
            Grid.SetRow(textBox, 1);
            root.Children.Add(textBox);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            Grid.SetRow(buttons, 2);

            var okButton = new Button { Content = "确定", Width = 70, Margin = new Thickness(0, 0, 6, 0), IsDefault = true };
            var cancelButton = new Button { Content = "取消", Width = 70, IsCancel = true };
            okButton.Click += (s, e) =>
            {
                window.DialogResult = true;
                window.Close();
            };
            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);
            root.Children.Add(buttons);

            window.Content = root;
            window.Loaded += (s, e) =>
            {
                textBox.Focus();
                textBox.SelectAll();
            };

            return window.ShowDialog() == true ? textBox.Text : null;
        }

        private void ShowManagementWindow()
        {
            if (!_isInitialized)
                return;

            Window? owner = Application.Current?.GetActiveWindow();
            var window = new DisplayControlManagerWindow(this)
            {
                Owner = owner,
                WindowStartupLocation = owner == null
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner
            };
            window.ShowDialog();
            FlushPendingConfigSave();
        }

        public void RestoreControl()
        {
            EnsureDefaultGroup();
            bool configChanged = MigrateLegacyControlSettings(IDisPlayControls);
            ArrangeCollectionByConfig();

            foreach (var group in GetGroupsInOrder())
                configChanged |= UpdateGroupIndexes(GetControlsInGroup(group.Id, applyPins: false, includeHidden: true));

            if (configChanged)
                ScheduleConfigSave();

            if (_isInitialized)
                RebuildPanel();
        }

        private void OwnerWindow_Closing(object? sender, CancelEventArgs e) => FlushPendingConfigSave();

        private void ScheduleConfigSave()
        {
            if (ConfigService.Instance is ConfigHandler { IsAutoSave: false } || StackPanel == null)
                return;

            _saveTimer ??= CreateSaveTimer();
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        private DispatcherTimer CreateSaveTimer()
        {
            var timer = new DispatcherTimer(DispatcherPriority.Background, StackPanel.Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            timer.Tick += (_, _) => FlushPendingConfigSave();
            return timer;
        }

        internal void FlushPendingConfigSave()
        {
            if (_saveTimer?.IsEnabled != true)
                return;

            _saveTimer.Stop();
            try
            {
                ConfigService.Instance?.Save<DisPlayManagerConfig>();
            }
            catch (Exception exception)
            {
                log.Warn("Failed to save display panel state.", exception);
            }
        }
    }
}
