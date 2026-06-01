using ColorVision.UI.Menus;
using log4net;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    public static class MenuItemManagerService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuItemManagerService));

        public static void SyncSettingsFromMenuItems()
        {
            SyncSettingsFromMenuItems(MenuManager.GetInstance(), MenuItemManagerConfig.Instance);
        }

        public static void ApplySettings()
        {
            var menuManager = MenuManager.GetInstance();
            var config = MenuItemManagerConfig.Instance;

            SyncSettingsFromMenuItems(menuManager, config);
            ApplyConfigToMenuManager(menuManager, config);
        }

        public static void RebuildMenu()
        {
            ApplySettings();
            MenuManager.GetInstance().RebuildAllMenus();
        }

        public static bool IsValidOwnerOverride(MenuItemSetting setting, string? targetOwnerGuid)
        {
            if (string.IsNullOrWhiteSpace(targetOwnerGuid)) return true;
            if (string.IsNullOrWhiteSpace(setting.GuidId)) return false;
            if (string.Equals(setting.GuidId, targetOwnerGuid, StringComparison.Ordinal)) return false;

            var settingsByGuid = MenuItemManagerConfig.Instance.Settings
                .Where(s => !string.IsNullOrWhiteSpace(s.GuidId))
                .GroupBy(s => s.GuidId!, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var currentGuid = targetOwnerGuid;
            var visited = new HashSet<string>(StringComparer.Ordinal);

            while (!string.IsNullOrWhiteSpace(currentGuid))
            {
                if (string.Equals(currentGuid, setting.GuidId, StringComparison.Ordinal)) return false;
                if (!visited.Add(currentGuid)) return false;

                if (!settingsByGuid.TryGetValue(currentGuid, out var parentSetting))
                    break;

                currentGuid = GetEffectiveOwner(parentSetting);
            }

            return true;
        }

        private static void ApplyConfigToMenuManager(MenuManager menuManager, MenuItemManagerConfig config)
        {
            menuManager.FilteredGuids.Clear();
            menuManager.OrderOverrides.Clear();
            menuManager.OwnerGuidOverrides.Clear();

            foreach (var setting in config.Settings.Where(s => !s.IsVisible && !string.IsNullOrEmpty(s.GuidId)))
            {
                menuManager.AddFilteredGuid(setting.GuidId);
            }

            foreach (var setting in config.Settings.Where(s => s.OrderOverride.HasValue && !string.IsNullOrEmpty(s.GuidId)))
            {
                menuManager.OrderOverrides[setting.GuidId] = setting.OrderOverride!.Value;
            }

            foreach (var setting in config.Settings.Where(s => !string.IsNullOrEmpty(s.OwnerGuidOverride) && !string.IsNullOrEmpty(s.GuidId)))
            {
                if (IsValidOwnerOverride(setting, setting.OwnerGuidOverride))
                {
                    menuManager.OwnerGuidOverrides[setting.GuidId] = setting.OwnerGuidOverride!;
                }
                else
                {
                    log.Warn($"Skip invalid OwnerGuid override '{setting.OwnerGuidOverride}' for '{setting.GuidId}'.");
                }
            }
        }

        private static void SyncSettingsFromMenuItems(MenuManager menuManager, MenuItemManagerConfig config)
        {
            var existingGuids = new HashSet<string>(config.Settings.Where(s => s.GuidId != null && s.GuidId.Length > 0).Select(s => s.GuidId!));

            var allMenuItems = menuManager.GetAllMenuItems();

            foreach (var mi in allMenuItems)
            {
                if (string.IsNullOrEmpty(mi.GuidId)) continue;
                if (existingGuids.Contains(mi.GuidId)) continue;

                var type = mi.GetType();
                existingGuids.Add(mi.GuidId);
                config.Settings.Add(new MenuItemSetting
                {
                    GuidId = mi.GuidId,
                    OwnerGuid = mi.OwnerGuid,
                    Header = mi.Header,
                    DefaultOrder = mi.Order,
                    IsVisible = true,
                    SourceType = type.FullName,
                    SourceAssembly = type.Assembly.GetName().Name,
                });
            }

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
                    setting.SourceType = mi.GetType().FullName;
                    setting.SourceAssembly = mi.GetType().Assembly.GetName().Name;
                }
            }
        }

        private static string? GetEffectiveOwner(MenuItemSetting setting)
        {
            return string.IsNullOrWhiteSpace(setting.OwnerGuidOverride) ? setting.OwnerGuid : setting.OwnerGuidOverride;
        }
    }
}