using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;
using log4net;
using System.Windows;
using System.Windows.Controls;
using System.Reflection; // added for ReflectionTypeLoadException

namespace ColorVision.UI.Menus
{
    public class MenuManager: IMenuService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuManager));
        private static MenuManager _instance;
        private static readonly object _locker = new();
        public static MenuManager GetInstance() { lock (_locker) { return _instance ??= new MenuManager(); } }

        public Menu Menu { get; set; }
        public List<IMenuItem> MenuItems { get; private set; } = new();
        public HashSet<string> FilteredGuids { get; } = new();
        public Dictionary<string, int> OrderOverrides { get; } = new();
        public Dictionary<string, string> OwnerGuidOverrides { get; } = new();

        private bool _initialized;
        private List<MenuItem> _menuBack = new();

        // ---------------------- New caching fields ----------------------
        private bool _typeCacheBuilt;
        private readonly List<Type> _menuItemTypeCache = new();
        private readonly List<Type> _menuItemProviderTypeCache = new();
        // ----------------------------------------------------------------

        private MenuManager() 
        {
            MenuService.SetInstance(this);
        }

        public int GetEffectiveOrder(IMenuItem mi) => mi.GuidId != null && OrderOverrides.TryGetValue(mi.GuidId, out var o) ? o : mi.Order;

        public string? GetEffectiveOwnerGuid(IMenuItem mi) => mi.GuidId != null && OwnerGuidOverrides.TryGetValue(mi.GuidId, out var g) ? g : mi.OwnerGuid;

        public void AddFilteredGuid(string guid) => FilteredGuids.Add(guid);
        public void AddFilteredGuids(IEnumerable<string> guids)
        {
            if (guids == null) return;
            foreach (var guid in guids)
                FilteredGuids.Add(guid);
        }
        public void AddFilteredGuids(params string[] guids)
        {
            if (guids == null) return;
            foreach (var guid in guids)
                FilteredGuids.Add(guid);
        }

        public void RefreshMenuItemsByGuid(string ownerGuid)
        {
            if (Menu == null) return;
            var parentMenuItem = FindMenuItemByGuid(ownerGuid, Menu.Items);
            if (parentMenuItem == null) return;
            parentMenuItem.Items.Clear();

            MenuItems = GetIMenuItemsFiltered();
            var refreshedItems = MenuItems
                .Where(mi => GetEffectiveOwnerGuid(mi) == ownerGuid && (mi.GuidId == null || !FilteredGuids.Contains(mi.GuidId)))
                .OrderBy(mi => GetEffectiveOrder(mi)).ToList();

            for (int i = 0; i < refreshedItems.Count; i++)
            {
                var mi = refreshedItems[i];
                var menuItem = CreateMenuItem(mi);
                if (i > 0
                    && GetEffectiveOrder(mi) - GetEffectiveOrder(refreshedItems[i - 1]) > 4
                    && menuItem.Visibility == Visibility.Visible)
                {
                    parentMenuItem.Items.Add(new Separator());
                }
                parentMenuItem.Items.Add(menuItem);
                if (mi.GuidId != null)
                    AddChildMenuItems(menuItem, mi.GuidId);
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
                IsChecked = mi.IsChecked ?? false,
                Visibility = mi.Visibility,
            };

            // 初始权限 / 可执行状态判定
            ApplyPermissionAndCommandVisibility(menuItem, mi);
            return menuItem;
        }

        // ---------------------- Incremental update support ----------------------
        private void ApplyPermissionAndCommandVisibility(MenuItem menuItem, IMenuItem mi)
        {
            if (mi == null || menuItem == null) return;

            if (mi.GetType().GetCustomAttributes(typeof(RequiresPermissionAttribute), true).FirstOrDefault() is RequiresPermissionAttribute attr)
            {
                menuItem.Visibility = mi.Visibility == Visibility.Visible && Authorization.Instance.PermissionMode == attr.RequiredPermission ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (mi.Command is RelayCommand relayCommand)
            {
                // 保持原逻辑：只在可执行时显示
                menuItem.Visibility = mi.Visibility == Visibility.Visible && relayCommand.CanExecute(null) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateMenuItemsVisibility()
        {
            if (Menu == null) return;
            foreach (var menuItem in EnumerateAllMenuItems(Menu.Items))
            {
                if (menuItem.Tag is IMenuItem mi)
                {
                    ApplyPermissionAndCommandVisibility(menuItem, mi);
                }
            }
        }

        private static IEnumerable<MenuItem> EnumerateAllMenuItems(ItemCollection items)
        {
            foreach (var obj in items)
            {
                if (obj is MenuItem mi)
                {
                    yield return mi;
                    foreach (var child in EnumerateAllMenuItems(mi.Items))
                        yield return child;
                }
            }
        }
        // ----------------------------------------------------------------------

        public void LoadMenuItemFromAssembly()
        {
            if (!_initialized)
            {
                _menuBack = Menu.Items.OfType<MenuItem>().ToList();
                foreach (var item in _menuBack)
                    Menu.Items.Remove(item);

                // 权限变化 => 增量更新可见性，而不是整棵重建
                Authorizations.Authorization.Instance.PermissionModeChanged += (s, e) =>
                {
                    UpdateMenuItemsVisibility();
                };
            }

            _initialized = true;
            log.Info("LoadMenuItemsFromAssembly (full rebuild)");
            Menu.Items.Clear();
            MenuItems = GetIMenuItemsFiltered();

            // 构建一级菜单
            var rootMenuItems = MenuItems.Where(mi => GetEffectiveOwnerGuid(mi) == "Menu").OrderBy(mi => GetEffectiveOrder(mi));
            foreach (var mi in rootMenuItems)
            {
                var menuItem = CreateMenuItem(mi);
                Menu.Items.Add(menuItem);
                if (mi.GuidId != null)
                    AddChildMenuItems(menuItem, mi.GuidId);
            }

            // 恢复备份项
            foreach (var item in _menuBack)
            {
                if (!Menu.Items.OfType<MenuItem>().Any(m => m.Header?.ToString() == item.Header?.ToString()))
                    Menu.Items.Add(item);
            }
        }

        private void AddChildMenuItems(MenuItem parent, string ownerGuid)
        {
            var children = MenuItems.Where(mi => GetEffectiveOwnerGuid(mi) == ownerGuid).OrderBy(mi => GetEffectiveOrder(mi)).ToList();
            for (int i = 0; i < children.Count; i++)
            {
                var mi = children[i];
                var menuItem = CreateMenuItem(mi);
                if (i > 0
                    && GetEffectiveOrder(mi) - GetEffectiveOrder(children[i - 1]) > 4
                    && menuItem.Visibility == Visibility.Visible)
                {
                    parent.Items.Add(new Separator());
                }
                parent.Items.Add(menuItem);
                if (mi.GuidId != null)
                    AddChildMenuItems(menuItem, mi.GuidId);
            }
        }

        // ---------------------- Type caching ----------------------
        private void EnsureTypeCaches()
        {
            if (_typeCacheBuilt) return;
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types.Where(t => t != null).ToArray();
                }

                foreach (var t in types)
                {
                    if (t == null || t.IsAbstract) continue;

                    if (typeof(IMenuItem).IsAssignableFrom(t))
                        _menuItemTypeCache.Add(t);
                    else if (typeof(IMenuItemProvider).IsAssignableFrom(t))
                        _menuItemProviderTypeCache.Add(t);
                }
            }
            _typeCacheBuilt = true;
        }
        // ------------------------------------------------------------

        private List<IMenuItem> GetIMenuItemsFiltered()
        {
            EnsureTypeCaches();
            var allMenuItems = new List<IMenuItem>(_menuItemTypeCache.Count + 16);

            // 实例化 IMenuItem
            foreach (var t in _menuItemTypeCache)
            {
                try
                {
                    if (Activator.CreateInstance(t) is IMenuItem item && item.Command != null)
                        allMenuItems.Add(item);
                }
                catch (Exception ex)
                {
                    log.Warn($"Create IMenuItem failed: {t.FullName}: {ex.Message}");
                }
            }

            // 实例化 IMenuItemProvider 并收集
            foreach (var t in _menuItemProviderTypeCache)
            {
                try
                {
                    if (Activator.CreateInstance(t) is IMenuItemProvider provider)
                    {
                        var provided = provider.GetMenuItems();
                        if (provided != null)
                        {
                            foreach (var p in provided)
                            {
                                if (p != null && p.Command != null && p.Header != null)
                                    allMenuItems.Add(p);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($"Create IMenuItemProvider failed: {t.FullName}: {ex.Message}");
                }
            }

            var allFilteredGuids = GetAllFilteredGuids(allMenuItems, FilteredGuids);
            return allMenuItems.Where(mi => mi.GuidId == null || !allFilteredGuids.Contains(mi.GuidId)).ToList();
        }

        private HashSet<string> GetAllFilteredGuids(IEnumerable<IMenuItem> allMenuItems, IEnumerable<string> initialGuids)
        {
            var result = new HashSet<string>(initialGuids ?? Enumerable.Empty<string>());
            bool added;
            do
            {
                added = false;
                foreach (var item in allMenuItems)
                {
                    var effectiveOwner = GetEffectiveOwnerGuid(item);
                    if (effectiveOwner != null && result.Contains(effectiveOwner) && item.GuidId != null && result.Add(item.GuidId))
                        added = true;
                }
            } while (added);
            return result;
        }
    }
}