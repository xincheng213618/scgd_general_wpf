using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;

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

        // ---------------------- 核心变更：多窗口支持 ----------------------
        // 记录 TargetName 对应的 Menu 控件实例
        private readonly Dictionary<string, Menu> _windowMenus = new();
        // 记录每个 Menu 控件最初在 XAML 中定义的原生菜单项备份
        private readonly Dictionary<Menu, List<MenuItem>> _menuBackups = new();
        // ----------------------------------------------------------------

        // ---------------------- Type caching ----------------------
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
            EnsureTypeCaches();

            // 1. 注册该窗口的 Menu，并备份原生 XAML 菜单项（仅首次）
            _windowMenus[targetName] = targetMenuControl;
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

            // 2. 获取所有菜单项，并根据 targetName 进行过滤
            var allItems = GetIMenuItemsFiltered();
            var windowSpecificItems = allItems.Where(mi =>
                mi.TargetName == targetName ||
                mi.TargetName == MenuItemConstants.GlobalTarget).ToList();
            // 3. 构建一级菜单
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

            // 4. 恢复原生备份项 (避免覆盖已生成的同名菜单)
            foreach (var item in _menuBackups[targetMenuControl])
            {
                if (!targetMenuControl.Items.OfType<MenuItem>().Any(m => m.Header?.ToString() == item.Header?.ToString()))
                {
                    targetMenuControl.Items.Add(item);
                }
            }
        }

        private void AddChildMenuItems(MenuItem parent, string ownerGuid, List<IMenuItem> windowSpecificItems)
        {
            var children = windowSpecificItems
                .Where(mi => GetEffectiveOwnerGuid(mi) == ownerGuid)
                .OrderBy(mi => GetEffectiveOrder(mi)).ToList();

            for (int i = 0; i < children.Count; i++)
            {
                var mi = children[i];
                var menuItem = CreateMenuItem(mi);

                // 自动添加分隔符
                if (i > 0
                    && GetEffectiveOrder(mi) - GetEffectiveOrder(children[i - 1]) > 4
                    && menuItem.Visibility == Visibility.Visible)
                {
                    parent.Items.Add(new Separator());
                }

                parent.Items.Add(menuItem);

                // 递归添加子菜单
                if (mi.GuidId != null)
                    AddChildMenuItems(menuItem, mi.GuidId, windowSpecificItems);
            }
        }

        /// <summary>
        /// 刷新指定 Guid 下的子菜单 (实现 IMenuService)
        /// 现在会遍历所有已注册窗口，刷新它们包含该 Guid 的菜单节点
        /// </summary>
        public void RefreshMenuItemsByGuid(string ownerGuid)
        {
            var allItems = GetIMenuItemsFiltered();

            foreach (var kvp in _windowMenus)
            {
                var targetName = kvp.Key;
                var targetMenuControl = kvp.Value;

                var parentMenuItem = FindMenuItemByGuid(ownerGuid, targetMenuControl.Items);
                if (parentMenuItem == null) continue; // 当前窗口没有这个菜单节点，跳过

                parentMenuItem.Items.Clear();

                // 仅筛选属于该窗口的菜单项
                var windowSpecificItems = allItems.Where(mi =>
                    mi.TargetName == targetName ||
                    mi.TargetName == MenuItemConstants.GlobalTarget).ToList();

                var refreshedItems = windowSpecificItems
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
                        AddChildMenuItems(menuItem, mi.GuidId, windowSpecificItems);
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
                IsChecked = mi.IsChecked ?? false,
                Visibility = mi.Visibility,
            };
            return menuItem;
        }

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

        /// <summary>
        /// 提供一个公共方法，获取系统当前所有过滤后的 IMenuItem（包含所有窗口）
        /// 用于之前的全局搜索或快捷键绑定功能
        /// </summary>
        public List<IMenuItem> GetAllMenuItemsFiltered()
        {
            return GetIMenuItemsFiltered();
        }

        private List<IMenuItem> GetIMenuItemsFiltered()
        {
            EnsureTypeCaches();
            var allMenuItems = new List<IMenuItem>(_menuItemTypeCache.Count + 16);

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


        // 将这两个方法添加到 MenuManager 类中

        /// <summary>
        /// 获取系统中所有加载的菜单项（不应用任何过滤或隐藏规则）
        /// 主要供配置界面使用，这样即便是被隐藏的菜单也能在设置里被找回来
        /// </summary>
        public List<IMenuItem> GetAllMenuItems()
        {
            EnsureTypeCaches();
            var allMenuItems = new List<IMenuItem>(_menuItemTypeCache.Count + 16);

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

            return allMenuItems;
        }

        /// <summary>
        /// 重新构建所有已注册窗口的菜单
        /// </summary>
        public void RebuildAllMenus()
        {
            // 遍历字典中所有已注册的窗口标识和对应的 Menu 控件
            foreach (var kvp in _windowMenus)
            {
                var targetName = kvp.Key;
                var targetMenuControl = kvp.Value;

                LoadMenuForWindow(targetName, targetMenuControl);
            }
        }
    }
}