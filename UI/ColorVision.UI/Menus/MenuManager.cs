#pragma warning disable CA1720,CA1822,CA1854,CS8619
using ColorVision.Common.MVVM;
using log4net;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.Menus
{
    public class MenuManager : IMenuService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuManager));
        private static MenuManager _instance;
        private static readonly object _locker = new();
        public static MenuManager GetInstance() { lock (_locker) { return _instance ??= new MenuManager(); } }

        public HashSet<string> FilteredGuids { get; } = new();
        public Dictionary<string, int> OrderOverrides { get; } = new();
        public Dictionary<string, string> OwnerGuidOverrides { get; } = new();

        private readonly List<MenuRegistration> _menuRegistrations = new();
        private readonly Dictionary<Menu, List<MenuItem>> _menuBackups = new();

        private bool _typeCacheBuilt;
        private readonly List<Type> _menuItemTypeCache = new();
        private readonly List<Type> _menuItemProviderTypeCache = new();
        /// <summary>Types decorated with <see cref="MenuItemAttribute"/> that do NOT
        /// already implement <see cref="IMenuItem"/> or <see cref="IMenuItemProvider"/>.
        /// These are handled via the lazy-loading path.</summary>
        private readonly List<Type> _menuItemAttributeTypeCache = new();
        private readonly object _typeCacheLock = new();

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
            EnsureTypeCaches();

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

            var windowSpecificItems = GetWindowSpecificItems(targetName, typeFilter);
            var rootMenuItems = windowSpecificItems
                .Where(mi => GetEffectiveOwnerGuid(mi) == MenuItemConstants.Menu)
                .OrderBy(mi => GetEffectiveOrder(mi));

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
                    .Where(mi => GetEffectiveOwnerGuid(mi) == ownerGuid && (mi.GuidId == null || !FilteredGuids.Contains(mi.GuidId)))
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
#if DEBUG
            menuItem.ToolTip = $"Class: {mi.GetType().Name}\nAssembly: {mi.GetType().Assembly.GetName().Name}";
#endif
            return menuItem;
        }

        private void EnsureTypeCaches()
        {
            if (_typeCacheBuilt) return;
            lock (_typeCacheLock)
            {
                if (_typeCacheBuilt) return;

                foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
                {
                    foreach (var t in AssemblyHandler.GetInstance().GetTypes(assembly))
                    {
                        if (!IsConcreteMenuCandidate(t)) continue;

                        if (typeof(IMenuItem).IsAssignableFrom(t))
                        {
                            AddInstantiableType(_menuItemTypeCache, t, nameof(IMenuItem));
                        }
                        else if (typeof(IMenuItemProvider).IsAssignableFrom(t))
                        {
                            AddInstantiableType(_menuItemProviderTypeCache, t, nameof(IMenuItemProvider));
                        }
                        else if (t.IsDefined(typeof(MenuItemAttribute), false))
                        {
                            AddInstantiableType(_menuItemAttributeTypeCache, t, nameof(MenuItemAttribute));
                        }
                    }
                }

                _typeCacheBuilt = true;
            }
        }

        private static bool IsConcreteMenuCandidate(Type? type)
        {
            return type != null && type.IsClass && !type.IsAbstract && !type.IsInterface && !type.ContainsGenericParameters;
        }

        private static bool HasPublicParameterlessConstructor(Type type)
        {
            return type.GetConstructor(Type.EmptyTypes) != null;
        }

        private void AddInstantiableType(List<Type> cache, Type type, string discoveryKind)
        {
            if (HasPublicParameterlessConstructor(type))
            {
                cache.Add(type);
                return;
            }

            if (log.IsDebugEnabled)
                log.Debug($"Skip {discoveryKind} discovery for {type.FullName}: no public parameterless constructor.");
        }

        /// <summary>
        /// 提供一个公共方法，获取系统当前所有过滤后的 IMenuItem（包含所有窗口）
        /// 用于之前的全局搜索或快捷键绑定功能
        /// </summary>
        public List<IMenuItem> GetAllMenuItemsFiltered()
        {
            return GetIMenuItemsFiltered(null);
        }

        private List<IMenuItem> GetWindowSpecificItems(string targetName, Func<Type, bool>? typeFilter)
        {
            var allItems = GetIMenuItemsFiltered(typeFilter);
            return allItems.Where(mi =>
                mi.TargetName == targetName ||
                mi.TargetName == MenuItemConstants.GlobalTarget).ToList();
        }

        private List<IMenuItem> GetIMenuItemsFiltered(Func<Type, bool>? typeFilter)
        {
            var allMenuItems = CreateAllMenuItems(typeFilter);
            var allFilteredGuids = GetAllFilteredGuids(allMenuItems, FilteredGuids);
            return allMenuItems.Where(mi => mi.GuidId == null || !allFilteredGuids.Contains(mi.GuidId)).ToList();
        }

        private List<IMenuItem> CreateAllMenuItems(Func<Type, bool>? typeFilter)
        {
            EnsureTypeCaches();
            var allMenuItems = new List<IMenuItem>(_menuItemTypeCache.Count + _menuItemProviderTypeCache.Count + _menuItemAttributeTypeCache.Count + 16);

            foreach (var t in _menuItemTypeCache)
            {
                if (typeFilter != null && !typeFilter(t))
                    continue;

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

            foreach (var t in _menuItemProviderTypeCache)
            {
                if (typeFilter != null && !typeFilter(t))
                    continue;

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

            foreach (var t in _menuItemAttributeTypeCache)
            {
                if (typeFilter != null && !typeFilter(t))
                    continue;

                var attr = t.GetCustomAttribute<MenuItemAttribute>(false);
                if (attr?.Header != null)
                    allMenuItems.Add(new LazyMenuItemAdapter(attr, t));
            }

            return allMenuItems;
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

        /// <summary>
        /// 获取系统中所有加载的菜单项（不应用任何过滤或隐藏规则）
        /// 主要供配置界面使用，这样即便是被隐藏的菜单也能在设置里被找回来
        /// </summary>
        public List<IMenuItem> GetAllMenuItems()
        {
            return CreateAllMenuItems(null);
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

        private sealed class MenuRegistration
        {
            private readonly MenuManager _owner;
            private readonly RoutedEventHandler _unloadedHandler;
            private RoutedEventHandler? _loadedHandler;
            private EventHandler? _closedHandler;
            private Window? _window;
            private bool _detached;

            public MenuRegistration(MenuManager owner, string targetName, Menu menu, Func<Type, bool>? typeFilter)
            {
                _owner = owner;
                TargetName = targetName;
                Menu = menu;
                TypeFilter = typeFilter;

                _unloadedHandler = OnMenuUnloaded;
                Menu.Unloaded += _unloadedHandler;
                AttachWindowIfAvailable();
                if (_window == null)
                {
                    _loadedHandler = OnMenuLoaded;
                    Menu.Loaded += _loadedHandler;
                }
            }

            public string TargetName { get; private set; }
            public Menu Menu { get; }
            public Func<Type, bool>? TypeFilter { get; private set; }

            public void Update(string targetName, Func<Type, bool>? typeFilter)
            {
                TargetName = targetName;
                TypeFilter = typeFilter;
                AttachWindowIfAvailable();
            }

            public void Detach()
            {
                if (_detached) return;

                _detached = true;
                Menu.Unloaded -= _unloadedHandler;
                if (_loadedHandler != null)
                {
                    Menu.Loaded -= _loadedHandler;
                    _loadedHandler = null;
                }

                if (_window != null && _closedHandler != null)
                {
                    _window.Closed -= _closedHandler;
                }

                _closedHandler = null;
                _window = null;
            }

            private void AttachWindowIfAvailable()
            {
                if (_window != null) return;

                var window = Window.GetWindow(Menu);
                if (window == null) return;

                _window = window;
                _closedHandler = OnWindowClosed;
                _window.Closed += _closedHandler;

                if (_loadedHandler != null)
                {
                    Menu.Loaded -= _loadedHandler;
                    _loadedHandler = null;
                }
            }

            private void OnMenuLoaded(object sender, RoutedEventArgs e)
            {
                AttachWindowIfAvailable();
            }

            private void OnMenuUnloaded(object sender, RoutedEventArgs e)
            {
                _owner.UnregisterMenu(Menu);
            }

            private void OnWindowClosed(object? sender, EventArgs e)
            {
                _owner.UnregisterMenu(Menu);
            }
        }

        /// <summary>
        /// Adapts a <see cref="MenuItemAttribute"/>-decorated class to <see cref="IMenuItem"/>.
        /// All display metadata comes from the attribute; the underlying class is instantiated
        /// only when the menu item's command is executed (lazy loading).
        /// </summary>
        private sealed class LazyMenuItemAdapter : IMenuItem
        {
            private readonly MenuItemAttribute _attr;
            private readonly Type _type;
            private ICommand? _lazyCommand;

            internal LazyMenuItemAdapter(MenuItemAttribute attr, Type type)
            {
                _attr = attr;
                _type = type;
            }

            public string TargetName => _attr.TargetName;
            public string? OwnerGuid => _attr.OwnerGuid;
            public string? GuidId => _attr.GuidId ?? _type.FullName;
            public int Order => _attr.Order;
            public string? Header => _attr.Header;
            public string? InputGestureText => _attr.InputGestureText;
            public object? Icon => null;
            public Visibility Visibility => Visibility.Visible;
            public bool? IsChecked => null;

            public ICommand? Command => _lazyCommand ??= new RelayCommand(_ => ExecuteUnderlying());

            private void ExecuteUnderlying()
            {
                try
                {
                    var instance = Activator.CreateInstance(_type);
                    if (instance is IMenuItem mi)
                    {
                        mi.Command?.Execute(null);
                    }
                    else
                    {
                        var method = _type.GetMethod("Execute",
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                            null, Type.EmptyTypes, null);
                        if (method != null)
                            method.Invoke(instance, null);
                        else
                            log.Warn($"[MenuItem] class {_type.FullName} has no parameterless Execute() method and does not implement IMenuItem.");
                    }
                }
                catch (Exception ex)
                {
                    log.Warn($"Execute [MenuItem] class failed: {_type.FullName}: {ex.Message}");
                }
            }
        }
    }
}
