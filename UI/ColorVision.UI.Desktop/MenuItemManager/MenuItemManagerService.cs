using ColorVision.UI.Menus;
using log4net;
using System.Collections.ObjectModel;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    public static class MenuItemManagerService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MenuItemManagerService));

        public static ObservableCollection<MenuItemSetting> CreateEditingSnapshot()
        {
            return CreateEditingSnapshot(MenuManager.GetInstance().GetAllMenuItems(), MenuItemManagerConfig.Instance);
        }

        public static ObservableCollection<MenuItemSetting> CreateEditingSnapshot(
            IEnumerable<IMenuItem> menuItems,
            MenuItemManagerConfig config)
        {
            ArgumentNullException.ThrowIfNull(menuItems);
            ArgumentNullException.ThrowIfNull(config);

            MigrateLegacySettings(config);

            var overrides = config.Overrides ?? new ObservableCollection<MenuItemOverride>();
            var scopedOverrides = overrides
                .Where(item => !string.IsNullOrWhiteSpace(item.TargetName) && !string.IsNullOrWhiteSpace(item.GuidId))
                .GroupBy(CreateScopeKey)
                .ToDictionary(group => group.Key, group => group.Last());
            var legacyOverrides = overrides
                .Where(item => string.IsNullOrWhiteSpace(item.TargetName) && !string.IsNullOrWhiteSpace(item.GuidId))
                .GroupBy(item => item.GuidId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

            var snapshot = new ObservableCollection<MenuItemSetting>();
            var knownKeys = new HashSet<MenuItemScopeKey>();
            var knownGuids = new HashSet<string>(StringComparer.Ordinal);

            foreach (var menuItem in menuItems.Where(item => !string.IsNullOrWhiteSpace(item.GuidId)))
            {
                var key = new MenuItemScopeKey(NormalizeTargetName(menuItem.TargetName), menuItem.GuidId!);
                if (!knownKeys.Add(key))
                    continue;

                knownGuids.Add(key.GuidId);
                scopedOverrides.TryGetValue(key, out var itemOverride);
                if (itemOverride == null)
                    legacyOverrides.TryGetValue(key.GuidId, out itemOverride);

                Type sourceType = menuItem.GetType();
                snapshot.Add(new MenuItemSetting
                {
                    TargetName = key.TargetName,
                    GuidId = key.GuidId,
                    OwnerGuid = menuItem.OwnerGuid,
                    Header = menuItem.Header,
                    DefaultOrder = menuItem.Order,
                    IsVisible = itemOverride?.IsVisible ?? true,
                    OrderOverride = itemOverride?.OrderOverride,
                    OwnerGuidOverride = itemOverride?.OwnerGuidOverride,
                    SourceType = sourceType.FullName,
                    SourceAssembly = sourceType.Assembly.GetName().Name,
                });
            }

            // Preserve scoped overrides for temporarily unavailable plugin menu items.
            foreach (var itemOverride in scopedOverrides.Values.Where(item => !knownKeys.Contains(CreateScopeKey(item))))
                snapshot.Add(CreateOrphanSetting(itemOverride));

            // An unresolved legacy override remains unscoped until its menu contribution is available again.
            foreach (var itemOverride in legacyOverrides.Values.Where(item => !knownGuids.Contains(item.GuidId)))
                snapshot.Add(CreateOrphanSetting(itemOverride));

            return snapshot;
        }

        public static ObservableCollection<MenuItemOverride> CreateSparseOverrides(IEnumerable<MenuItemSetting> settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var overrides = settings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.GuidId))
                .Where(HasCustomization)
                .Where(setting => !IsRetired(CreateScopeKey(setting)))
                .GroupBy(CreateScopeKey)
                .Select(group => group.Last())
                .Select(setting => new MenuItemOverride
                {
                    TargetName = NormalizeOptionalTargetName(setting.TargetName),
                    GuidId = setting.GuidId,
                    IsVisible = setting.IsVisible,
                    OrderOverride = setting.OrderOverride,
                    OwnerGuidOverride = NormalizeOptionalGuid(setting.OwnerGuidOverride),
                })
                .OrderBy(item => NormalizeTargetName(item.TargetName), StringComparer.Ordinal)
                .ThenBy(item => item.GuidId, StringComparer.Ordinal)
                .ToList();

            return new ObservableCollection<MenuItemOverride>(overrides);
        }

        public static bool CommitEditingSnapshot(IEnumerable<MenuItemSetting> settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            var draft = settings.ToList();
            ValidateDraft(draft);

            var menuManager = MenuManager.GetInstance();
            var config = MenuItemManagerConfig.Instance;
            MigrateLegacySettings(config);

            ObservableCollection<MenuItemOverride> previousOverrides = config.Overrides;
            ObservableCollection<MenuItemOverride> nextOverrides = CreateSparseOverrides(draft);

            try
            {
                config.Overrides = nextOverrides;
                bool changed = ApplyConfigToMenuManager(menuManager, config);
                if (changed)
                    menuManager.RebuildAllMenus();

                ConfigHandler.GetInstance().Save<MenuItemManagerConfig>();
                return changed;
            }
            catch
            {
                config.Overrides = previousOverrides;
                if (ApplyConfigToMenuManager(menuManager, config))
                    menuManager.RebuildAllMenus();
                throw;
            }
        }

        public static bool ApplySettings()
        {
            var config = MenuItemManagerConfig.Instance;
            MigrateLegacySettings(config);
            return ApplyConfigToMenuManager(MenuManager.GetInstance(), config);
        }

        public static void RebuildMenu()
        {
            if (ApplySettings())
                MenuManager.GetInstance().RebuildAllMenus();
        }

        public static bool IsValidOwnerOverride(
            MenuItemSetting setting,
            string? targetOwnerGuid,
            IEnumerable<MenuItemSetting> settings)
        {
            ArgumentNullException.ThrowIfNull(setting);
            ArgumentNullException.ThrowIfNull(settings);

            if (string.IsNullOrWhiteSpace(targetOwnerGuid)
                || string.Equals(targetOwnerGuid, MenuItemConstants.Menu, StringComparison.Ordinal))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(setting.GuidId)) return false;

            var catalog = settings
                .Where(item => !string.IsNullOrWhiteSpace(item.GuidId))
                .GroupBy(CreateScopeKey)
                .ToDictionary(group => group.Key, group => group.First());
            MenuItemScopeKey sourceKey = CreateScopeKey(setting);
            MenuItemScopeKey? currentKey = ResolveOwnerKey(setting.TargetName, targetOwnerGuid, catalog);
            if (!currentKey.HasValue)
                return false;

            var visited = new HashSet<MenuItemScopeKey>();
            while (currentKey.HasValue)
            {
                if (currentKey.Value == sourceKey) return false;
                if (!visited.Add(currentKey.Value)) return false;
                if (!catalog.TryGetValue(currentKey.Value, out var parentSetting)) return false;

                string parentOwnerGuid = GetEffectiveOwner(parentSetting);
                if (string.Equals(parentOwnerGuid, MenuItemConstants.Menu, StringComparison.Ordinal))
                    break;

                currentKey = ResolveOwnerKey(parentSetting.TargetName, parentOwnerGuid, catalog);
            }

            return true;
        }

        public static bool IsOwnerInAllowedScope(MenuItemSetting setting, MenuItemSetting owner)
        {
            if (string.Equals(setting.TargetName, MenuItemConstants.GlobalTarget, StringComparison.Ordinal))
                return string.Equals(owner.TargetName, MenuItemConstants.GlobalTarget, StringComparison.Ordinal);

            return string.Equals(owner.TargetName, setting.TargetName, StringComparison.Ordinal)
                || string.Equals(owner.TargetName, MenuItemConstants.GlobalTarget, StringComparison.Ordinal);
        }

        private static bool ApplyConfigToMenuManager(MenuManager menuManager, MenuItemManagerConfig config)
        {
            List<MenuItemOverride> activeOverrides = (config.Overrides ?? new ObservableCollection<MenuItemOverride>())
                .Where(item => !string.IsNullOrWhiteSpace(item.GuidId))
                .Where(HasCustomization)
                .Where(item => !IsRetired(CreateScopeKey(item)))
                .ToList();

            // The default configuration is deliberately a no-discovery path. Most users
            // never customize menus, so startup only has to clear any stale runtime maps.
            if (activeOverrides.Count == 0)
                return ClearRuntimeOverrides(menuManager);

            List<IMenuItem> menuItems = menuManager.GetAllMenuItems();
            List<ScopedOverride> expandedOverrides = ExpandOverrides(activeOverrides, menuItems);

            var filteredItems = expandedOverrides
                .Where(item => !item.Value.IsVisible)
                .Select(item => item.Key)
                .ToHashSet();
            var orderOverrides = expandedOverrides
                .Where(item => item.Value.OrderOverride.HasValue)
                .ToDictionary(item => item.Key, item => item.Value.OrderOverride!.Value);

            var validationSettings = CreateEditingSnapshot(menuItems, config);
            var settingsByKey = validationSettings
                .Where(item => !string.IsNullOrWhiteSpace(item.GuidId))
                .GroupBy(CreateScopeKey)
                .ToDictionary(group => group.Key, group => group.First());
            var ownerGuidOverrides = new Dictionary<MenuItemScopeKey, string>();
            foreach (var item in expandedOverrides.Where(item => !string.IsNullOrWhiteSpace(item.Value.OwnerGuidOverride)))
            {
                if (settingsByKey.TryGetValue(item.Key, out var setting)
                    && IsValidOwnerOverride(setting, item.Value.OwnerGuidOverride, validationSettings))
                {
                    ownerGuidOverrides[item.Key] = item.Value.OwnerGuidOverride!;
                }
                else
                {
                    log.Warn($"Skip invalid OwnerGuid override '{item.Value.OwnerGuidOverride}' for '{item.Key.TargetName}:{item.Key.GuidId}'.");
                }
            }

            bool changed = menuManager.FilteredGuids.Count != 0
                || menuManager.OrderOverrides.Count != 0
                || menuManager.OwnerGuidOverrides.Count != 0
                || !menuManager.ScopedFilteredItems.SetEquals(filteredItems)
                || !DictionaryEquals(menuManager.ScopedOrderOverrides, orderOverrides)
                || !DictionaryEquals(menuManager.ScopedOwnerGuidOverrides, ownerGuidOverrides);
            if (!changed)
                return false;

            // MenuItemManager owns scoped customization. Clear legacy global overrides after expansion.
            menuManager.FilteredGuids.Clear();
            menuManager.OrderOverrides.Clear();
            menuManager.OwnerGuidOverrides.Clear();

            menuManager.ScopedFilteredItems.Clear();
            menuManager.ScopedFilteredItems.UnionWith(filteredItems);

            menuManager.ScopedOrderOverrides.Clear();
            foreach (var (key, order) in orderOverrides)
                menuManager.ScopedOrderOverrides[key] = order;

            menuManager.ScopedOwnerGuidOverrides.Clear();
            foreach (var (key, ownerGuid) in ownerGuidOverrides)
                menuManager.ScopedOwnerGuidOverrides[key] = ownerGuid;

            return true;
        }

        private static bool ClearRuntimeOverrides(MenuManager menuManager)
        {
            bool changed = menuManager.FilteredGuids.Count != 0
                || menuManager.OrderOverrides.Count != 0
                || menuManager.OwnerGuidOverrides.Count != 0
                || menuManager.ScopedFilteredItems.Count != 0
                || menuManager.ScopedOrderOverrides.Count != 0
                || menuManager.ScopedOwnerGuidOverrides.Count != 0;
            if (!changed)
                return false;

            menuManager.FilteredGuids.Clear();
            menuManager.OrderOverrides.Clear();
            menuManager.OwnerGuidOverrides.Clear();
            menuManager.ScopedFilteredItems.Clear();
            menuManager.ScopedOrderOverrides.Clear();
            menuManager.ScopedOwnerGuidOverrides.Clear();
            return true;
        }

        private static List<ScopedOverride> ExpandOverrides(
            IEnumerable<MenuItemOverride>? overrides,
            IReadOnlyCollection<IMenuItem> menuItems)
        {
            var expanded = new List<ScopedOverride>();
            var catalogKeysByGuid = menuItems
                .Where(item => !string.IsNullOrWhiteSpace(item.GuidId))
                .Select(item => new MenuItemScopeKey(NormalizeTargetName(item.TargetName), item.GuidId!))
                .Distinct()
                .GroupBy(key => key.GuidId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            foreach (var item in overrides ?? Enumerable.Empty<MenuItemOverride>())
            {
                if (string.IsNullOrWhiteSpace(item.GuidId))
                    continue;

                if (!string.IsNullOrWhiteSpace(item.TargetName))
                {
                    expanded.Add(new ScopedOverride(CreateScopeKey(item), item));
                    continue;
                }

                if (catalogKeysByGuid.TryGetValue(item.GuidId, out var matchingKeys))
                {
                    foreach (var key in matchingKeys)
                        expanded.Add(new ScopedOverride(key, item));
                }
            }

            return expanded
                .GroupBy(item => item.Key)
                .Select(group => group.Last())
                .ToList();
        }

        private static bool DictionaryEquals<TKey, TValue>(
            IReadOnlyDictionary<TKey, TValue> current,
            IReadOnlyDictionary<TKey, TValue> expected)
            where TKey : notnull
        {
            if (current.Count != expected.Count)
                return false;

            var comparer = EqualityComparer<TValue>.Default;
            return current.All(pair => expected.TryGetValue(pair.Key, out TValue? value)
                && comparer.Equals(pair.Value, value));
        }

        private static void MigrateLegacySettings(MenuItemManagerConfig config)
        {
            config.Overrides ??= new ObservableCollection<MenuItemOverride>();
            ObservableCollection<MenuItemOverride> migrated = config.Settings == null
                ? new ObservableCollection<MenuItemOverride>()
                : CreateSparseOverrides(config.Settings);
            config.Overrides = new ObservableCollection<MenuItemOverride>(migrated
                .Concat(config.Overrides)
                .Where(item => !string.IsNullOrWhiteSpace(item.GuidId))
                .Where(item => !IsRetired(CreateScopeKey(item)))
                .GroupBy(CreateScopeKey)
                .Select(group => group.Last())
                .Where(HasCustomization)
                .OrderBy(item => NormalizeTargetName(item.TargetName), StringComparer.Ordinal)
                .ThenBy(item => item.GuidId, StringComparer.Ordinal));
            config.Settings = null;
        }

        private static void ValidateDraft(IReadOnlyCollection<MenuItemSetting> settings)
        {
            var duplicateKey = settings
                .Where(setting => !string.IsNullOrWhiteSpace(setting.GuidId))
                .GroupBy(CreateScopeKey)
                .FirstOrDefault(group => group.Count() > 1);
            if (duplicateKey != null)
                throw new InvalidOperationException($"Duplicate menu identity '{duplicateKey.Key.TargetName}:{duplicateKey.Key.GuidId}' in the editing snapshot.");

            foreach (var setting in settings.Where(item => !IsOrphan(item) && !string.IsNullOrWhiteSpace(item.OwnerGuidOverride)))
            {
                if (!IsValidOwnerOverride(setting, setting.OwnerGuidOverride, settings))
                {
                    log.Warn($"Reject invalid OwnerGuid override '{setting.OwnerGuidOverride}' for '{setting.TargetName}:{setting.GuidId}'.");
                    throw new InvalidOperationException($"Invalid menu owner override for '{setting.TargetName}:{setting.GuidId}'.");
                }
            }
        }

        private static MenuItemSetting CreateOrphanSetting(MenuItemOverride item)
        {
            return new MenuItemSetting
            {
                TargetName = NormalizeTargetName(item.TargetName),
                GuidId = item.GuidId,
                IsVisible = item.IsVisible,
                OrderOverride = item.OrderOverride,
                OwnerGuidOverride = item.OwnerGuidOverride,
            };
        }

        private static MenuItemScopeKey? ResolveOwnerKey(
            string targetName,
            string ownerGuid,
            Dictionary<MenuItemScopeKey, MenuItemSetting> catalog)
        {
            string normalizedTarget = NormalizeTargetName(targetName);
            var sameTargetKey = new MenuItemScopeKey(normalizedTarget, ownerGuid);
            if (catalog.ContainsKey(sameTargetKey))
                return sameTargetKey;

            if (!string.Equals(normalizedTarget, MenuItemConstants.GlobalTarget, StringComparison.Ordinal))
            {
                var globalKey = new MenuItemScopeKey(MenuItemConstants.GlobalTarget, ownerGuid);
                if (catalog.ContainsKey(globalKey))
                    return globalKey;
            }

            return null;
        }

        private static MenuItemScopeKey CreateScopeKey(MenuItemSetting setting)
        {
            return new MenuItemScopeKey(NormalizeTargetName(setting.TargetName), setting.GuidId);
        }

        private static MenuItemScopeKey CreateScopeKey(MenuItemOverride item)
        {
            return new MenuItemScopeKey(NormalizeTargetName(item.TargetName), item.GuidId);
        }

        private static bool HasCustomization(MenuItemSetting setting)
        {
            return !setting.IsVisible
                || setting.OrderOverride.HasValue
                || !string.IsNullOrWhiteSpace(setting.OwnerGuidOverride);
        }

        private static bool IsOrphan(MenuItemSetting setting)
        {
            return string.IsNullOrWhiteSpace(setting.SourceType)
                && string.IsNullOrWhiteSpace(setting.SourceAssembly);
        }

        private static bool HasCustomization(MenuItemOverride item)
        {
            return !item.IsVisible
                || item.OrderOverride.HasValue
                || !string.IsNullOrWhiteSpace(item.OwnerGuidOverride);
        }

        private static string NormalizeTargetName(string? targetName)
        {
            return targetName?.Trim() ?? string.Empty;
        }

        private static string? NormalizeOptionalTargetName(string? targetName)
        {
            string normalized = NormalizeTargetName(targetName);
            return normalized.Length == 0 ? null : normalized;
        }

        private static string? NormalizeOptionalGuid(string? guid)
        {
            return string.IsNullOrWhiteSpace(guid) ? null : guid;
        }

        private static string GetEffectiveOwner(MenuItemSetting setting)
        {
            return string.IsNullOrWhiteSpace(setting.OwnerGuidOverride)
                ? NormalizeOwnerGuid(setting.OwnerGuid)
                : NormalizeOwnerGuid(setting.OwnerGuidOverride);
        }

        private static string NormalizeOwnerGuid(string? ownerGuid)
        {
            return string.IsNullOrWhiteSpace(ownerGuid) ? MenuItemConstants.Menu : ownerGuid;
        }

        private static bool IsRetired(MenuItemScopeKey key)
        {
            if (string.Equals(key.GuidId, "MenuMenuItemManager", StringComparison.Ordinal))
            {
                return string.IsNullOrWhiteSpace(key.TargetName)
                    || string.Equals(key.TargetName, MenuItemConstants.GlobalTarget, StringComparison.Ordinal);
            }

            if (string.Equals(key.GuidId, "ServiceManager", StringComparison.Ordinal))
            {
                return string.IsNullOrWhiteSpace(key.TargetName)
                    || string.Equals(key.TargetName, MenuItemConstants.MainWindowTarget, StringComparison.Ordinal);
            }

            return false;
        }

        private sealed record ScopedOverride(MenuItemScopeKey Key, MenuItemOverride Value);
    }
}
