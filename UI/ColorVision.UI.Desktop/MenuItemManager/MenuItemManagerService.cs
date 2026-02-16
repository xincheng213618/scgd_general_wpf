using ColorVision.UI.Menus;
using log4net;
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

        /// <summary>
        /// Apply saved settings to MenuManager (call after LoadMenuItemFromAssembly)
        /// </summary>
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
        }

        /// <summary>
        /// Apply hotkey overrides to the main window (call after menu is built)
        /// </summary>
        public void ApplyHotkeys(Window mainWindow)
        {
            if (mainWindow == null) return;
            var menuManager = MenuManager.GetInstance();
            var config = MenuItemManagerConfig.Instance;

            foreach (var setting in config.Settings.Where(s => !string.IsNullOrEmpty(s.HotkeyOverride) && !string.IsNullOrEmpty(s.GuidId)))
            {
                var menuItem = menuManager.MenuItems.FirstOrDefault(mi => mi.GuidId == setting.GuidId);
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

        /// <summary>
        /// Ensure all discovered menu items have a corresponding setting entry
        /// </summary>
        private static void SyncSettingsFromMenuItems(MenuManager menuManager, MenuItemManagerConfig config)
        {
            var existingGuids = new HashSet<string>(config.Settings.Where(s => s.GuidId != null && s.GuidId.Length > 0).Select(s => s.GuidId!));

            foreach (var mi in menuManager.MenuItems)
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
            var menuItemDict = menuManager.MenuItems
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

        /// <summary>
        /// Update a menu item's visibility setting
        /// </summary>
        public void SetMenuItemVisibility(string guidId, bool visible)
        {
            var config = MenuItemManagerConfig.Instance;
            var setting = config.Settings.FirstOrDefault(s => s.GuidId == guidId);
            if (setting != null)
                setting.IsVisible = visible;
        }

        /// <summary>
        /// Update a menu item's order override
        /// </summary>
        public void SetMenuItemOrder(string guidId, int? orderOverride)
        {
            var config = MenuItemManagerConfig.Instance;
            var setting = config.Settings.FirstOrDefault(s => s.GuidId == guidId);
            if (setting != null)
                setting.OrderOverride = orderOverride;
        }

        /// <summary>
        /// Update a menu item's hotkey override
        /// </summary>
        public void SetMenuItemHotkey(string guidId, string? hotkey)
        {
            var config = MenuItemManagerConfig.Instance;
            var setting = config.Settings.FirstOrDefault(s => s.GuidId == guidId);
            if (setting != null)
                setting.HotkeyOverride = hotkey;
        }

        /// <summary>
        /// Rebuild menu after settings change
        /// </summary>
        public void RebuildMenu()
        {
            var menuManager = MenuManager.GetInstance();

            // Clear and reapply filtered guids from config
            menuManager.FilteredGuids.Clear();
            menuManager.OrderOverrides.Clear();

            var config = MenuItemManagerConfig.Instance;
            foreach (var setting in config.Settings.Where(s => !s.IsVisible && !string.IsNullOrEmpty(s.GuidId)))
            {
                menuManager.AddFilteredGuid(setting.GuidId);
            }

            foreach (var setting in config.Settings.Where(s => s.OrderOverride.HasValue && !string.IsNullOrEmpty(s.GuidId)))
            {
                menuManager.OrderOverrides[setting.GuidId] = setting.OrderOverride!.Value;
            }

            menuManager.LoadMenuItemFromAssembly();
        }

        private static ParsedHotkey? ParseHotkey(string hotkeyStr)
        {
            if (string.IsNullOrWhiteSpace(hotkeyStr)) return null;

            var parts = hotkeyStr.Split('+').Select(p => p.Trim()).ToArray();
            if (parts.Length == 0) return null;

            var modifiers = ModifierKeys.None;
            Key key = Key.None;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i].ToUpperInvariant();
                if (i < parts.Length - 1)
                {
                    // Modifier
                    switch (part)
                    {
                        case "CTRL":
                        case "CONTROL":
                            modifiers |= ModifierKeys.Control;
                            break;
                        case "SHIFT":
                            modifiers |= ModifierKeys.Shift;
                            break;
                        case "ALT":
                            modifiers |= ModifierKeys.Alt;
                            break;
                        case "WIN":
                        case "WINDOWS":
                            modifiers |= ModifierKeys.Windows;
                            break;
                    }
                }
                else
                {
                    // Key
                    if (System.Enum.TryParse(parts[i], true, out Key parsedKey))
                        key = parsedKey;
                }
            }

            if (key == Key.None) return null;
            return new ParsedHotkey(key, modifiers);
        }

        private record ParsedHotkey(Key Key, ModifierKeys ModifierKeys);
    }
}
