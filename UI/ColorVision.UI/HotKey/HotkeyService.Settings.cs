namespace ColorVision.UI.HotKey
{
    public sealed partial class HotkeyService
    {
        private void ApplySettingsToRuntime(IEnumerable<HotkeySetting> settings)
        {
            ApplySettings(HotKeys, _hotkeysById, settings);
        }

        private static void ApplySettings(IReadOnlyList<HotKeys> hotKeysList, IReadOnlyDictionary<string, HotKeys> hotKeysById, IEnumerable<HotkeySetting> settings)
        {
            foreach (var setting in settings)
            {
                HotKeys? hotKeys = Find(hotKeysList, hotKeysById, setting);
                if (hotKeys == null) continue;

                hotKeys.SetBindings(setting.GetBindings());
                hotKeys.Kinds = setting.Kinds;
            }
        }

        private static HotKeys? Find(IReadOnlyList<HotKeys> hotKeysList, IReadOnlyDictionary<string, HotKeys> hotKeysById, HotkeySetting setting)
        {
            if (!string.IsNullOrWhiteSpace(setting.Id) && hotKeysById.TryGetValue(setting.Id, out var byId))
            {
                return byId;
            }

            if (!string.IsNullOrWhiteSpace(setting.LegacyName))
            {
                return hotKeysList.LastOrDefault(hotKeys => string.Equals(hotKeys.Name, setting.LegacyName, StringComparison.Ordinal));
            }

            return null;
        }

        private static Dictionary<string, HotKeys> CreateIdMap(IEnumerable<HotKeys> hotKeysList)
        {
            var map = new Dictionary<string, HotKeys>(StringComparer.OrdinalIgnoreCase);
            foreach (var hotKeys in hotKeysList)
            {
                if (!string.IsNullOrWhiteSpace(hotKeys.Id) && !map.ContainsKey(hotKeys.Id))
                {
                    map.Add(hotKeys.Id, hotKeys);
                }
            }

            return map;
        }

        private static HotKeys CreateEditableCopy(HotKeys source)
        {
            return new HotKeys
            {
                Id = source.Id,
                Name = source.Name,
                DisplayName = source.DisplayName,
                Description = source.Description,
                Category = source.Category,
                Source = source.Source,
                Hotkey = CloneHotkey(source.Hotkey),
                AdditionalHotkeys = CloneBindings(source.AdditionalHotkeys),
                Kinds = source.Kinds,
                DefaultHotkey = CloneHotkey(source.DefaultHotkey),
                DefaultAdditionalHotkeys = CloneBindings(source.DefaultAdditionalHotkeys),
                DefaultKinds = source.DefaultKinds,
                IsRegistered = source.IsRegistered
            };
        }

        private static Hotkey CloneHotkey(Hotkey? hotkey)
        {
            return hotkey == null ? new() : new Hotkey(hotkey.Key, hotkey.Modifiers);
        }

        private static List<Hotkey> CloneBindings(IEnumerable<Hotkey> bindings) => bindings.Select(CloneHotkey).ToList();
    }
}
