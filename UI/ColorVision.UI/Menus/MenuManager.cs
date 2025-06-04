#pragma warning disable CA1720,CS8620,CA1822,CS8602
using ColorVision.Common.MVVM;
using ColorVision.UI.Authorizations;
using log4net;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Menus
{
    public class MenuManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuManager));
        private static MenuManager _instance;
        private static readonly object _locker = new();
        public static MenuManager GetInstance() { lock (_locker) { return _instance ??= new MenuManager(); } }

        public Menu Menu { get; set; }
        public List<IMenuItem> MenuItems { get; private set; } = new();
        public HashSet<string> FilteredGuids { get; } = new();

        private bool _initialized;
        private List<MenuItem> _menuBack = new();

        private MenuManager() { }

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
            var parentMenuItem = FindMenuItemByGuid(ownerGuid, Menu.Items);
            if (parentMenuItem == null) return;
            parentMenuItem.Items.Clear();

            MenuItems = GetIMenuItemsFiltered();
            var refreshedItems = MenuItems
                .Where(mi => mi.OwnerGuid == ownerGuid && (mi.GuidId == null || !FilteredGuids.Contains(mi.GuidId)))
                .OrderBy(mi => mi.Order).ToList();

            for (int i = 0; i < refreshedItems.Count; i++)
            {
                var mi = refreshedItems[i];
                var menuItem = CreateMenuItem(mi);
                if (i > 0
                    && mi.Order - refreshedItems[i - 1].Order > 4
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
                Visibility = mi.Visibility,
            };

            // 检查类型的RequiresPermissionAttribute

            if (mi.GetType().GetCustomAttributes(typeof(RequiresPermissionAttribute), true).FirstOrDefault() is RequiresPermissionAttribute attr)
            {
                menuItem.Visibility = mi.Visibility == Visibility.Visible && Authorization.Instance.PermissionMode == attr.RequiredPermission ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (mi.Command is RelayCommand relayCommand)
            {
                menuItem.Visibility = mi.Visibility == Visibility.Visible && relayCommand.CanExecute(null) ? Visibility.Visible : Visibility.Collapsed;
            }
            return menuItem;
        }

        public void LoadMenuItemFromAssembly()
        {
            if (!_initialized)
            {
                _menuBack = Menu.Items.OfType<MenuItem>().ToList();
                foreach (var item in _menuBack)
                    Menu.Items.Remove(item);

                // 只需注册一次
                Authorizations.Authorization.Instance.PermissionModeChanged += (s, e) =>
                {
                    // 这里可以加防抖逻辑，避免重复刷新
                    LoadMenuItemFromAssembly();
                };
            }

            _initialized = true;
            log.Info("LoadMenuItemsFromAssembly");
            Menu.Items.Clear();
            MenuItems = GetIMenuItemsFiltered();

            // 构建一级菜单
            var rootMenuItems = MenuItems.Where(mi => mi.OwnerGuid == "Menu").OrderBy(mi => mi.Order);
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
            var children = MenuItems.Where(mi => mi.OwnerGuid == ownerGuid).OrderBy(mi => mi.Order).ToList();
            for (int i = 0; i < children.Count; i++)
            {
                var mi = children[i];
                var menuItem = CreateMenuItem(mi);
                if (i > 0
                    && mi.Order - children[i - 1].Order > 4
                    && menuItem.Visibility == Visibility.Visible)
                {
                    parent.Items.Add(new Separator());
                }
                parent.Items.Add(menuItem);
                if (mi.GuidId != null)
                    AddChildMenuItems(menuItem, mi.GuidId);
            }
        }

        private List<IMenuItem> GetIMenuItemsFiltered()
        {
            var allMenuItems = new List<IMenuItem>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                allMenuItems.AddRange(assembly.GetTypes()
                    .Where(t => typeof(IMenuItem).IsAssignableFrom(t) && !t.IsAbstract)
                    .Select(t => Activator.CreateInstance(t) as IMenuItem)
                    .Where(i => i != null && i.Command != null));

                allMenuItems.AddRange(assembly.GetTypes()
                    .Where(t => typeof(IMenuItemProvider).IsAssignableFrom(t) && !t.IsAbstract)
                    .SelectMany(t => ((IMenuItemProvider)Activator.CreateInstance(t)).GetMenuItems())
                    .Where(i => i.Command != null && i.Header != null));
            }
            var allFilteredGuids = GetAllFilteredGuids(allMenuItems, FilteredGuids);
            return allMenuItems.Where(mi => mi.GuidId == null || !allFilteredGuids.Contains(mi.GuidId)).ToList();
        }

        private static HashSet<string> GetAllFilteredGuids(IEnumerable<IMenuItem> allMenuItems, IEnumerable<string> initialGuids)
        {
            var result = new HashSet<string>(initialGuids ?? Enumerable.Empty<string>());
            bool added;
            do
            {
                added = false;
                foreach (var item in allMenuItems)
                {
                    if (item.OwnerGuid != null && result.Contains(item.OwnerGuid) && item.GuidId != null && result.Add(item.GuidId))
                        added = true;
                }
            } while (added);
            return result;
        }
    }
}