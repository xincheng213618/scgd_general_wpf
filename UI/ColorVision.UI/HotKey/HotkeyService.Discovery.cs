namespace ColorVision.UI.HotKey
{
    public sealed partial class HotkeyService
    {
        private IEnumerable<HotkeyDefinition> DiscoverDefinitions()
        {
            foreach (Type type in _getProviderTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(IHotkeyProvider).IsAssignableFrom(type) && !typeof(IHotKey).IsAssignableFrom(type)) continue;
                foreach (HotkeyDefinition definition in TryCreateDefinitions(type)) yield return definition;
            }
        }

        private static IEnumerable<Type> DiscoverProviderTypes()
            => AssemblyHandler.GetInstance().GetAssemblies().SelectMany(assembly => AssemblyHandler.GetInstance().GetTypes(assembly));

        private static IReadOnlyList<HotkeyDefinition> TryCreateDefinitions(Type type)
        {
            try
            {
                object? instance = Activator.CreateInstance(type);
                var definitions = new List<HotkeyDefinition>();
                if (instance is IHotkeyProvider provider)
                {
                    foreach (HotkeyDefinition definition in provider.GetHotkeyDefinitions())
                        if (definition != null && !string.IsNullOrWhiteSpace(definition.Id))
                            definitions.Add(HotkeyPresentation.Enrich(definition, instance));
                    return definitions;
                }

                if (instance is IHotKey legacyProvider)
                {
                    HotKeys hotKeys = legacyProvider.HotKeys;
                    if (hotKeys.HotKeyHandler == null) return definitions;
                    string id = string.IsNullOrWhiteSpace(hotKeys.Id) ? CreateLegacyProviderId(type) : hotKeys.Id;
                    IReadOnlyList<Hotkey> defaultBindings = hotKeys.GetDefaultBindings();
                    if (defaultBindings.Count == 0) defaultBindings = hotKeys.GetBindings();
                    Hotkey defaultHotkey = defaultBindings.FirstOrDefault() ?? new();
                    definitions.Add(HotkeyPresentation.Enrich(new HotkeyDefinition(id, hotKeys.Name, defaultHotkey, hotKeys.HotKeyHandler, hotKeys.Kinds)
                    {
                        AdditionalDefaultHotkeys = CloneBindings(defaultBindings.Skip(1)),
                        DisplayName = hotKeys.DisplayName,
                        Description = hotKeys.Description,
                        Category = hotKeys.Category,
                        Source = hotKeys.Source
                    }, instance));
                }
                return definitions;
            }
            catch (Exception exception)
            {
                Log.Warn($"Read hotkey provider failed: {type.FullName}: {exception.GetBaseException().Message}");
                return [];
            }
        }

        private static string CreateLegacyProviderId(Type providerType)
        {
            return providerType.FullName ?? providerType.Name;
        }

        private static string CreateCallbackId(HotKeyCallBackHanlder? callback)
        {
            if (callback?.Method == null)
            {
                return Guid.NewGuid().ToString("N");
            }

            return $"{callback.Method.DeclaringType?.FullName}.{callback.Method.Name}";
        }
    }
}
