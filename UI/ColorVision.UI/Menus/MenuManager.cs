#pragma warning disable CA1720,CA1822,CA1854,CS8619
using ColorVision.UI.HotKey;
using log4net;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Menus
{
    /// <summary>
    /// Identifies a menu contribution without conflating equal GuidIds that belong to different windows.
    /// </summary>
    public readonly record struct MenuItemScopeKey(string TargetName, string GuidId);

    public partial class MenuManager : IMenuService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuManager));
        private static MenuManager _instance;
        private static readonly object _locker = new();
        public static MenuManager GetInstance() { lock (_locker) { return _instance ??= new MenuManager(); } }

        public HashSet<string> FilteredGuids { get; } = new();
        public Dictionary<string, int> OrderOverrides { get; } = new();
        public Dictionary<string, string> OwnerGuidOverrides { get; } = new();
        public HashSet<MenuItemScopeKey> ScopedFilteredItems { get; } = new();
        public Dictionary<MenuItemScopeKey, int> ScopedOrderOverrides { get; } = new();
        public Dictionary<MenuItemScopeKey, string> ScopedOwnerGuidOverrides { get; } = new();

        private readonly List<MenuRegistration> _menuRegistrations = new();
        private readonly Dictionary<Menu, List<MenuItem>> _menuBackups = new();

        /// <param name="AttributeTypes">Types decorated with <see cref="MenuItemAttribute"/> that do NOT
        /// already implement <see cref="IMenuItem"/> or <see cref="IMenuItemProvider"/>.
        /// These are handled via the lazy-loading path.</param>
        private sealed record MenuTypeSnapshot(Type[] ItemTypes, Type[] ProviderTypes, Type[] AttributeTypes);

        private volatile MenuTypeSnapshot? _typeSnapshot;
        private int _typeSnapshotGeneration;
        private readonly object _typeCacheLock = new();

        private MenuManager()
        {
            MenuService.SetInstance(this);
            AssemblyHandler.GetInstance().AssembliesChanged += (_, _) =>
            {
                Interlocked.Increment(ref _typeSnapshotGeneration);
                _typeSnapshot = null;
            };
        }

        public int GetEffectiveOrder(IMenuItem mi)
        {
            if (mi.GuidId == null)
                return mi.Order;

            var key = CreateScopeKey(mi);
            if (ScopedOrderOverrides.TryGetValue(key, out int scopedOrder))
                return scopedOrder;

            return OrderOverrides.TryGetValue(mi.GuidId, out int order) ? order : mi.Order;
        }

        public string? GetEffectiveOwnerGuid(IMenuItem mi)
        {
            if (mi.GuidId == null)
                return mi.OwnerGuid;

            var key = CreateScopeKey(mi);
            if (ScopedOwnerGuidOverrides.TryGetValue(key, out string? scopedOwnerGuid))
                return scopedOwnerGuid;

            return OwnerGuidOverrides.TryGetValue(mi.GuidId, out string? ownerGuid) ? ownerGuid : mi.OwnerGuid;
        }

        public void AddFilteredGuid(string guid) => FilteredGuids.Add(guid);
        public void AddFilteredGuids(IEnumerable<string> guids)
        {
            if (guids == null) return;
            foreach (var guid in guids) FilteredGuids.Add(guid);
        }
        public void AddFilteredGuids(params string[] guids)
        {
            if (guids == null) return;
            foreach (var guid in guids) FilteredGuids.Add(guid);
        }

        /// <summary>
        /// 核心加载方法：为指定的窗口(TargetName)和具体的 Menu 控件加载菜单
        /// </summary>
        public void LoadMenuForWindow(string targetName, Menu targetMenuControl)
        {
            LoadMenuForWindow(targetName, targetMenuControl, null);
        }

        /// <summary>
        /// 核心加载方法：为指定的窗口(TargetName)和具体的 Menu 控件加载菜单，
        /// 并允许调用方按声明类型进一步过滤菜单来源。
        /// </summary>
        public void LoadMenuForWindow(string targetName, Menu targetMenuControl, Func<Type, bool>? typeFilter)
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch phaseStopwatch = Stopwatch.StartNew();
            EnsureTypeCaches();
            long typeDiscoveryMilliseconds = phaseStopwatch.ElapsedMilliseconds;

            RegisterMenu(targetName, targetMenuControl, typeFilter);
            if (!_menuBackups.ContainsKey(targetMenuControl))
            {
                var backups = targetMenuControl.Items.OfType<MenuItem>().ToList();
                _menuBackups[targetMenuControl] = backups;
                foreach (var item in backups)
                {
                    targetMenuControl.Items.Remove(item);
                }
            }

            log.Info($"LoadMenuForWindow for target: {targetName}");
            targetMenuControl.Items.Clear();

            phaseStopwatch.Restart();
            var windowSpecificItems = GetWindowSpecificItems(targetName, typeFilter);
            long itemDiscoveryMilliseconds = phaseStopwatch.ElapsedMilliseconds;
            var rootMenuItems = windowSpecificItems
                .Where(mi => GetEffectiveOwnerGuid(mi) == MenuItemConstants.Menu)
                .OrderBy(mi => GetEffectiveOrder(mi))
                .ToList();

            phaseStopwatch.Restart();
            foreach (var mi in rootMenuItems)
            {
                var menuItem = CreateMenuItem(mi);
                targetMenuControl.Items.Add(menuItem);
                if (mi.GuidId != null)
                {
                    AddChildMenuItems(menuItem, mi.GuidId, windowSpecificItems);
                }
            }

            foreach (var item in _menuBackups[targetMenuControl])
            {
                if (!targetMenuControl.Items.OfType<MenuItem>().Any(m => m.Header?.ToString() == item.Header?.ToString()))
                {
                    targetMenuControl.Items.Add(item);
                }
            }

            totalStopwatch.Stop();
            log.Info($"LoadMenuForWindow completed for target: {targetName}. " +
                $"Items={windowSpecificItems.Count}, Roots={rootMenuItems.Count}, " +
                $"TypeDiscovery={typeDiscoveryMilliseconds}ms, ItemDiscovery={itemDiscoveryMilliseconds}ms, " +
                $"TreeBuild={phaseStopwatch.ElapsedMilliseconds}ms, Total={totalStopwatch.ElapsedMilliseconds}ms.");
        }

        public void UnregisterMenu(Menu menu)
        {
            foreach (var registration in _menuRegistrations.Where(r => ReferenceEquals(r.Menu, menu)).ToList())
            {
                registration.Detach();
                _menuRegistrations.Remove(registration);
            }

            _menuBackups.Remove(menu);
        }

        private void RegisterMenu(string targetName, Menu targetMenuControl, Func<Type, bool>? typeFilter)
        {
            var registration = _menuRegistrations.FirstOrDefault(r => ReferenceEquals(r.Menu, targetMenuControl));
            if (registration != null)
            {
                registration.Update(targetName, typeFilter);
                return;
            }

            _menuRegistrations.Add(new MenuRegistration(this, targetName, targetMenuControl, typeFilter));
        }

        private void AddChildMenuItems(MenuItem parent, string ownerGuid, List<IMenuItem> windowSpecificItems, HashSet<string>? visited = null)
        {
            visited ??= new HashSet<string>(StringComparer.Ordinal);
            if (!visited.Add(ownerGuid)) return;

            var children = windowSpecificItems
                .Where(mi => GetEffectiveOwnerGuid(mi) == ownerGuid)
                .OrderBy(mi => GetEffectiveOrder(mi)).ToList();

            for (int i = 0; i < children.Count; i++)
            {
                var mi = children[i];
                if (mi.GuidId != null && visited.Contains(mi.GuidId))
                    continue;

                var menuItem = CreateMenuItem(mi);

                // 自动添加分隔符
                if (i > 0
                    && GetEffectiveOrder(mi) - GetEffectiveOrder(children[i - 1]) > 4
                    && menuItem.Visibility == Visibility.Visible)
                {
                    parent.Items.Add(new Separator());
                }

                parent.Items.Add(menuItem);

                if (mi.GuidId != null)
                    AddChildMenuItems(menuItem, mi.GuidId, windowSpecificItems, visited);
            }

            visited.Remove(ownerGuid);
        }

        /// <summary>
        /// 刷新指定 Guid 下的子菜单 (实现 IMenuService)
        /// 现在会遍历所有已注册窗口，刷新它们包含该 Guid 的菜单节点
        /// </summary>
        public void RefreshMenuItemsByGuid(string ownerGuid)
        {
            foreach (var registration in _menuRegistrations.ToList())
            {
                var targetName = registration.TargetName;
                var targetMenuControl = registration.Menu;
                var typeFilter = registration.TypeFilter;

                var parentMenuItem = FindMenuItemByGuid(ownerGuid, targetMenuControl.Items);
                if (parentMenuItem == null) continue;

                parentMenuItem.Items.Clear();

                var windowSpecificItems = GetWindowSpecificItems(targetName, typeFilter);

                var refreshedItems = windowSpecificItems
                    .Where(mi => GetEffectiveOwnerGuid(mi) == ownerGuid)
                    .OrderBy(mi => GetEffectiveOrder(mi)).ToList();
                var visited = new HashSet<string>(StringComparer.Ordinal) { ownerGuid };

                for (int i = 0; i < refreshedItems.Count; i++)
                {
                    var mi = refreshedItems[i];
                    if (mi.GuidId != null && visited.Contains(mi.GuidId))
                        continue;

                    var menuItem = CreateMenuItem(mi);
                    if (i > 0
                        && GetEffectiveOrder(mi) - GetEffectiveOrder(refreshedItems[i - 1]) > 4
                        && menuItem.Visibility == Visibility.Visible)
                    {
                        parentMenuItem.Items.Add(new Separator());
                    }
                    parentMenuItem.Items.Add(menuItem);
                    if (mi.GuidId != null)
                        AddChildMenuItems(menuItem, mi.GuidId, windowSpecificItems, visited);
                }
            }
        }

        private static MenuItem? FindMenuItemByGuid(string guid, ItemCollection items)
        {
            foreach (var obj in items)
            {
                if (obj is MenuItem mi && mi.Tag is IMenuItem iMenuItem && iMenuItem.GuidId == guid)
                    return mi;

                if (obj is MenuItem mi2)
                {
                    var found = FindMenuItemByGuid(guid, mi2.Items);
                    if (found != null) return found;
                }
            }
            return null;
        }

        private MenuItem CreateMenuItem(IMenuItem mi)
        {
            var menuItem = new MenuItem
            {
                Header = mi.Header,
                Icon = mi.Icon,
                InputGestureText = mi.InputGestureText,
                Command = mi.Command,
                Tag = mi,
                IsCheckable = mi.IsChecked.HasValue,
                IsChecked = mi.IsChecked ?? false,
                Visibility = mi.Visibility,
            };
            if (mi is IHotKey)
                HotkeyMenuGestureBinding.Attach(menuItem, mi, HotkeyService.GetInstance().HotKeys);
#if DEBUG
            menuItem.ToolTip = $"Class: {mi.GetType().Name}\nAssembly: {mi.GetType().Assembly.GetName().Name}";
#endif
            return menuItem;
        }

        /// <summary>
        /// 重新构建所有已注册窗口的菜单
        /// </summary>
        public void RebuildAllMenus()
        {
            foreach (var registration in _menuRegistrations.ToList())
            {
                LoadMenuForWindow(registration.TargetName, registration.Menu, registration.TypeFilter);
            }
        }
    }
}
