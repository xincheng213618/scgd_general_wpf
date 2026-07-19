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

        public static bool ApplySettings()
        {
            var menuManager = MenuManager.GetInstance();
            var config = MenuItemManagerConfig.Instance;

            SyncSettingsFromMenuItems(menuManager, config);
            return ApplyConfigToMenuManager(menuManager, config);
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

        private static bool ApplyConfigToMenuManager(MenuManager menuManager, MenuItemManagerConfig config)
        {
            var filteredGuids = config.Settings
                .Where(setting => !setting.IsVisible && !string.IsNullOrEmpty(setting.GuidId))
                .Select(setting => setting.GuidId!)
                .ToHashSet(StringComparer.Ordinal);

            var orderOverrides = config.Settings
                .Where(setting => setting.OrderOverride.HasValue && !string.IsNullOrEmpty(setting.GuidId))
                .GroupBy(setting => setting.GuidId!, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last().OrderOverride!.Value, StringComparer.Ordinal);

            var ownerGuidOverrides = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var setting in config.Settings.Where(setting => !string.IsNullOrEmpty(setting.OwnerGuidOverride) && !string.IsNullOrEmpty(setting.GuidId)))
            {
                if (IsValidOwnerOverride(setting, setting.OwnerGuidOverride))
                {
                    ownerGuidOverrides[setting.GuidId!] = setting.OwnerGuidOverride!;
                }
                else
                {
                    log.Warn($"Skip invalid OwnerGuid override '{setting.OwnerGuidOverride}' for '{setting.GuidId}'.");
                }
            }

            bool changed = !menuManager.FilteredGuids.SetEquals(filteredGuids)
                || !DictionaryEquals(menuManager.OrderOverrides, orderOverrides)
                || !DictionaryEquals(menuManager.OwnerGuidOverrides, ownerGuidOverrides);

            if (!changed)
                return false;

            menuManager.FilteredGuids.Clear();
            menuManager.FilteredGuids.UnionWith(filteredGuids);

            menuManager.OrderOverrides.Clear();
            foreach (var (guid, order) in orderOverrides)
            {
                menuManager.OrderOverrides[guid] = order;
            }

            menuManager.OwnerGuidOverrides.Clear();
            foreach (var (guid, ownerGuid) in ownerGuidOverrides)
            {
                menuManager.OwnerGuidOverrides[guid] = ownerGuid;
            }

            return true;
        }

        private static bool DictionaryEquals<TValue>(
            IReadOnlyDictionary<string, TValue> current,
            IReadOnlyDictionary<string, TValue> expected)
        {
            if (current.Count != expected.Count)
                return false;

            var comparer = EqualityComparer<TValue>.Default;
            return current.All(pair => expected.TryGetValue(pair.Key, out TValue? value)
                && comparer.Equals(pair.Value, value));
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
