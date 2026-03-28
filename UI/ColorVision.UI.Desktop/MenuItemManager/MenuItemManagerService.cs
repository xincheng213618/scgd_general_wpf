using ColorVision.UI.Menus;
using log4net;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    public class MenuItemManagerService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuItemManagerService));
        private static MenuItemManagerService? _instance;
        private static readonly object _locker = new();
        public static MenuItemManagerService GetInstance()
        {
            lock (_locker) { return _instance ??= new MenuItemManagerService(); }
        }

        private MenuItemManagerService() { }

        public void ApplySettings()
        {
            var menuManager = MenuManager.GetInstance();
            var config = MenuItemManagerConfig.Instance;

            // Sync settings from discovered menu items
            SyncSettingsFromMenuItems(menuManager, config);

            // Apply visibility (hidden items)
            foreach (var setting in config.Settings.Where(s => !s.IsVisible && !string.IsNullOrEmpty(s.GuidId)))
            {
                menuManager.AddFilteredGuid(setting.GuidId);
            }

            // Apply order overrides
            foreach (var setting in config.Settings.Where(s => s.OrderOverride.HasValue && !string.IsNullOrEmpty(s.GuidId)))
            {
                menuManager.OrderOverrides[setting.GuidId] = setting.OrderOverride!.Value;
            }

            // Apply OwnerGuid overrides
            foreach (var setting in config.Settings.Where(s => !string.IsNullOrEmpty(s.OwnerGuidOverride) && !string.IsNullOrEmpty(s.GuidId)))
            {
                menuManager.OwnerGuidOverrides[setting.GuidId] = setting.OwnerGuidOverride!;
            }
        }

        public void ApplyHotkeys(Window mainWindow)
        {
            if (mainWindow == null) return;
            var menuManager = MenuManager.GetInstance();
            var config = MenuItemManagerConfig.Instance;

            // 使用过滤后的有效菜单项来绑定快捷键
            var activeMenuItems = menuManager.GetAllMenuItemsFiltered();

            foreach (var setting in config.Settings.Where(s => !string.IsNullOrEmpty(s.HotkeyOverride) && !string.IsNullOrEmpty(s.GuidId)))
            {
                var menuItem = activeMenuItems.FirstOrDefault(mi => mi.GuidId == setting.GuidId);
                if (menuItem?.Command == null) continue;

                var hotkey = ParseHotkey(setting.HotkeyOverride!);
                if (hotkey == null) continue;

                try
                {
                    var binding = new KeyBinding(menuItem.Command, hotkey.Key, hotkey.ModifierKeys);
                    mainWindow.InputBindings.Add(binding);
                }
                catch (System.Exception ex)
                {
                    log.Warn($"Failed to register hotkey '{setting.HotkeyOverride}' for '{setting.GuidId}': {ex.Message}");
                }
            }
        }

        private static void SyncSettingsFromMenuItems(MenuManager menuManager, MenuItemManagerConfig config)
        {
            var existingGuids = new HashSet<string>(config.Settings.Where(s => s.GuidId != null && s.GuidId.Length > 0).Select(s => s.GuidId!));

            // 获取系统中所有加载的菜单项（不过滤），确保被隐藏的菜单也能在设置面板中被配置
            var allMenuItems = menuManager.GetAllMenuItems();

            foreach (var mi in allMenuItems)
            {
                if (string.IsNullOrEmpty(mi.GuidId)) continue;
                if (existingGuids.Contains(mi.GuidId)) continue;

                config.Settings.Add(new MenuItemSetting
                {
                    GuidId = mi.GuidId,
                    OwnerGuid = mi.OwnerGuid,
                    Header = mi.Header,
                    DefaultOrder = mi.Order,
                    IsVisible = true,
                });
            }

            // Update Header/DefaultOrder for existing settings
            var menuItemDict = allMenuItems
                .Where(mi => mi.GuidId != null && mi.GuidId.Length > 0)
                .GroupBy(mi => mi.GuidId!)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var setting in config.Settings)
            {
                if (string.IsNullOrEmpty(setting.GuidId)) continue;
                if (menuItemDict.TryGetValue(setting.GuidId, out var mi))
                {
                    setting.Header = mi.Header;
                    setting.OwnerGuid = mi.OwnerGuid;
                    setting.DefaultOrder = mi.Order;
                }
            }
        }

        public void SetMenuItemVisibility(string guidId, bool visible) { /* 保持不变 */ }
        public void SetMenuItemOrder(string guidId, int? orderOverride) { /* 保持不变 */ }
        public void SetMenuItemHotkey(string guidId, string? hotkey) { /* 保持不变 */ }

        public void RebuildMenu()
        {
            var menuManager = MenuManager.GetInstance();

            // Clear and reapply filtered guids from config
            menuManager.FilteredGuids.Clear();
            menuManager.OrderOverrides.Clear();
            menuManager.OwnerGuidOverrides.Clear();

            var config = MenuItemManagerConfig.Instance;
            foreach (var setting in config.Settings.Where(s => !s.IsVisible && !string.IsNullOrEmpty(s.GuidId)))
                menuManager.AddFilteredGuid(setting.GuidId);

            foreach (var setting in config.Settings.Where(s => s.OrderOverride.HasValue && !string.IsNullOrEmpty(s.GuidId)))
                menuManager.OrderOverrides[setting.GuidId] = setting.OrderOverride!.Value;

            foreach (var setting in config.Settings.Where(s => !string.IsNullOrEmpty(s.OwnerGuidOverride) && !string.IsNullOrEmpty(s.GuidId)))
                menuManager.OwnerGuidOverrides[setting.GuidId] = setting.OwnerGuidOverride!;

            // 替换为新的多窗口重建方法
            menuManager.RebuildAllMenus();
        }

        private static ParsedHotkey? ParseHotkey(string hotkeyStr) { /* 保持不变 */ return null; }
        private record ParsedHotkey(Key Key, ModifierKeys ModifierKeys);
    }
}