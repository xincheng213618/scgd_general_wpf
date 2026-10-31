using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI.Views;
using log4net;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision.UI
{
    public partial class DisPlayManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DisPlayManager));
        private static DisPlayManager _instance;
        private static readonly object _locker = new();
        public static DisPlayManager GetInstance() { lock (_locker) { return _instance ??= new DisPlayManager(); } }
        public static ICommand CreateGroupCommand { get; } = new RelayCommand(_ => GetInstance().CreateGroup());
        public static ICommand ManageControlsCommand { get; } = new RelayCommand(_ => GetInstance().ShowManagementWindow());
        public static ICommand ConfigureDevicesCommand { get; } = new RelayCommand(
            parameter => GetInstance().DeviceConfigurationCommand?.Execute(parameter),
            parameter => GetInstance().DeviceConfigurationCommand?.CanExecute(parameter) == true);
        public ICommand? DeviceConfigurationCommand { get; set; }
        public ObservableCollection<IDisPlayControl> IDisPlayControls { get; private set; }
        private IDisPlayControl? _selectedControl;
        private bool _suppressCollectionChanged;
        private bool _isInitialized;
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

        internal static string GetControlKey(IDisPlayControl control)
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

        public static bool IsControlVisible(IDisPlayControl control) => IsControlVisible(control.PersistenceKey, control.DisPlayName);

        public static bool IsControlVisible(string persistenceKey, string displayName)
        {
            EnsureConfigCollections();
            var hidden = DisPlayManagerConfig.Instance.HiddenControls;
            // Registration precedes the full display snapshot and its legacy-key migration.
            string key = string.IsNullOrWhiteSpace(persistenceKey) ? displayName : persistenceKey;
            return !hidden.Contains(key) && !hidden.Contains(displayName);
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

            DockViewManager.GetInstance().SetViewVisibility(key, isVisible);
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
            string defaultName = string.Format(Properties.Resources.DisplayControlNewGroupName, DisPlayManagerConfig.Instance.Groups.Count(a => !IsDefaultGroup(a.Id)) + 1);
            string? name = ShowTextDialog(Properties.Resources.DisplayControlCreateGroup, Properties.Resources.DisplayControlGroupName, defaultName);
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
            string? name = ShowTextDialog(Properties.Resources.DisplayControlRenameGroup, Properties.Resources.DisplayControlGroupName, group.Name);
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

            MessageBoxResult result = MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(Properties.Resources.DisplayControlDeleteGroupPrompt, group.Name), "ColorVision", MessageBoxButton.OKCancel);
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
