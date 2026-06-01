using ColorVision.UI.HotKey.GlobalHotKey;
using ColorVision.UI.HotKey.WindowHotKey;
using log4net;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.HotKey
{
    public sealed class HotkeyService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HotkeyService));
        private static readonly object Locker = new();
        private static HotkeyService? _instance;
        private readonly Dictionary<string, HotKeys> _hotkeysById = new(StringComparer.OrdinalIgnoreCase);
        private readonly ObservableCollection<HotKeys> _hotKeys = new();

        private Window? _hostWindow;

        private HotkeyService()
        {
        }

        public static HotkeyService GetInstance()
        {
            lock (Locker)
            {
                return _instance ??= new HotkeyService();
            }
        }

        public ObservableCollection<HotKeys> HotKeys => _hotKeys;

        public void LoadFromAssemblies(Window hostWindow)
        {
            UnregisterAll();
            _hostWindow = hostWindow;
            ClearDefinitions();

            foreach (var definition in DiscoverDefinitions())
            {
                RegisterDefinition(definition);
            }

            ApplySettingsToRuntime(HotKeyConfig.Instance.Hotkeys);
            RegisterAll(hostWindow);
        }

        public bool AddHotKeys(Window hostWindow, HotKeys hotKeys)
        {
            _hostWindow ??= hostWindow;
            if (string.IsNullOrWhiteSpace(hotKeys.Id))
            {
                hotKeys.Id = CreateCallbackId(hotKeys.HotKeyHandler);
            }

            var runtimeHotKeys = AddRuntimeHotKeys(hotKeys);

            Unregister(runtimeHotKeys);
            return Register(hostWindow, runtimeHotKeys);
        }

        public bool AddHotKeys(Control control, HotKeys hotKeys)
        {
            if (hotKeys.Kinds == HotKeyKinds.Global) return false;
            hotKeys.Control = control;
            var runtimeHotKeys = AddRuntimeHotKeys(hotKeys);

            Unregister(runtimeHotKeys);
            return Register(control, runtimeHotKeys);
        }

        public bool RegisterHotkey(Window hostWindow, Hotkey hotkey, HotKeyCallBackHanlder handler, HotKeyKinds kind = HotKeyKinds.Windows)
        {
            string id = CreateCallbackId(handler);
            var hotKeys = new HotKeys(id, hotkey, handler)
            {
                Id = id,
                Kinds = kind,
                DefaultKinds = kind
            };

            return AddHotKeys(hostWindow, hotKeys);
        }

        public void UpdateRegistration(HotKeys hotKeys)
        {
            if (hotKeys.Control == null && _hostWindow == null) return;

            Unregister(hotKeys);
            Window? hostWindow = _hostWindow ?? (hotKeys.Control == null ? null : Window.GetWindow(hotKeys.Control));

            if (hotKeys.IsGlobal)
            {
                if (hostWindow != null)
                {
                    Register(hostWindow, hotKeys);
                }
            }
            else if (hotKeys.Control != null)
            {
                Register(hotKeys.Control, hotKeys);
            }
        }

        public void SetDefault()
        {
            UnregisterAll();
            foreach (var hotKeys in HotKeys)
            {
                hotKeys.Hotkey = Hotkey.None;
            }

            foreach (var hotKeys in HotKeys)
            {
                hotKeys.Kinds = hotKeys.DefaultKinds;
                hotKeys.Hotkey = hotKeys.DefaultHotkey;
            }

            RegisterAll();
        }

        public void ReloadSettings()
        {
            UnregisterAll();
            ApplySettingsToRuntime(HotKeyConfig.Instance.Hotkeys);
            RegisterAll();
        }

        public List<HotKeys> CreateEditableHotKeys(bool useSavedSettings = false)
        {
            var editableHotKeys = HotKeys.Select(CreateEditableCopy).ToList();
            if (useSavedSettings)
            {
                ApplySettings(editableHotKeys, CreateIdMap(editableHotKeys), HotKeyConfig.Instance.Hotkeys);
            }

            return editableHotKeys;
        }

        public List<HotKeys> CreateDefaultEditableHotKeys()
        {
            var editableHotKeys = CreateEditableHotKeys();
            foreach (var hotKeys in editableHotKeys)
            {
                hotKeys.Kinds = hotKeys.DefaultKinds;
                hotKeys.Hotkey = CloneHotkey(hotKeys.DefaultHotkey);
            }

            return editableHotKeys;
        }

        public void ApplySettings(IEnumerable<HotkeySetting> settings)
        {
            UnregisterAll();
            ApplySettingsToRuntime(settings);
            RegisterAll();
        }

        public void SaveSettings()
        {
            HotKeyConfig.Instance.Hotkeys = new ObservableCollection<HotkeySetting>(HotKeys.Select(HotkeySetting.FromHotKeys));
        }

        public void RegisterAll()
        {
            if (_hostWindow == null) return;

            RegisterAll(_hostWindow);
        }

        public void UnregisterAll()
        {
            foreach (var hotKeys in HotKeys)
            {
                Unregister(hotKeys);
            }
        }

        private void RegisterAll(Window hostWindow)
        {
            foreach (var hotKeys in HotKeys)
            {
                Register(hostWindow, hotKeys);
            }
        }

        private void ClearDefinitions()
        {
            _hotkeysById.Clear();
            HotKeys.Clear();
        }

        private HotKeys RegisterDefinition(HotkeyDefinition definition)
        {
            if (_hotkeysById.TryGetValue(definition.Id, out var existing))
            {
                return existing;
            }

            var hotKeys = definition.CreateRuntimeHotKeys();
            _hotkeysById.Add(hotKeys.Id, hotKeys);
            HotKeys.Add(hotKeys);
            return hotKeys;
        }

        private HotKeys AddRuntimeHotKeys(HotKeys hotKeys)
        {
            if (string.IsNullOrWhiteSpace(hotKeys.Id))
            {
                hotKeys.Id = CreateCallbackId(hotKeys.HotKeyHandler);
            }

            if (_hotkeysById.TryGetValue(hotKeys.Id, out var existing))
            {
                UpdateRuntimeHotKeys(existing, hotKeys);
                return existing;
            }

            _hotkeysById.Add(hotKeys.Id, hotKeys);
            HotKeys.Add(hotKeys);
            return hotKeys;
        }

        private static void UpdateRuntimeHotKeys(HotKeys target, HotKeys source)
        {
            target.Name = source.Name;
            target.HotKeyHandler = source.HotKeyHandler ?? target.HotKeyHandler;
            target.DefaultHotkey = CloneHotkey(source.DefaultHotkey);
            target.DefaultKinds = source.DefaultKinds;
            target.Hotkey = CloneHotkey(source.Hotkey);
            target.Kinds = source.Kinds;
        }

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

                hotKeys.Hotkey = CloneHotkey(setting.Hotkey);
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
                Hotkey = CloneHotkey(source.Hotkey),
                Kinds = source.Kinds,
                DefaultHotkey = CloneHotkey(source.DefaultHotkey),
                DefaultKinds = source.DefaultKinds,
                IsRegistered = source.IsRegistered
            };
        }

        private static Hotkey CloneHotkey(Hotkey? hotkey)
        {
            return hotkey == null ? Hotkey.None : new Hotkey(hotkey.Key, hotkey.Modifiers);
        }

        private static bool Register(Window hostWindow, HotKeys hotKeys)
        {
            hotKeys.Control = hostWindow;
            hotKeys.Registration = hotKeys.IsGlobal
                ? GlobalHotKeyManager.GetInstance(hostWindow).RegisterHandle(hotKeys)
                : WindowHotKeyManager.GetInstance(hostWindow).RegisterHandle(hotKeys);
            hotKeys.IsRegistered = hotKeys.Registration?.IsRegistered == true;
            return hotKeys.IsRegistered;
        }

        private static bool Register(Control control, HotKeys hotKeys)
        {
            hotKeys.Control = control;
            hotKeys.Registration = WindowHotKeyManager.GetInstance(control).RegisterHandle(hotKeys);
            hotKeys.IsRegistered = hotKeys.Registration?.IsRegistered == true;
            return hotKeys.IsRegistered;
        }

        private static void Unregister(HotKeys hotKeys)
        {
            hotKeys.Registration?.Dispose();
            hotKeys.Registration = null;
            hotKeys.IsRegistered = false;
        }

        private static IEnumerable<HotkeyDefinition> DiscoverDefinitions()
        {
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (var type in GetLoadableTypes(assembly))
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(IHotkeyProvider).IsAssignableFrom(type) && !typeof(IHotKey).IsAssignableFrom(type)) continue;

                    foreach (var definition in TryCreateDefinitions(type))
                    {
                        yield return definition;
                    }
                }
            }
        }

        private static IEnumerable<HotkeyDefinition> TryCreateDefinitions(Type type)
        {
            object? instance;
            try
            {
                instance = Activator.CreateInstance(type);
            }
            catch (Exception ex)
            {
                Log.Warn($"Create hotkey provider failed: {type.FullName}: {ex.Message}");
                yield break;
            }

            if (instance is IHotkeyProvider provider)
            {
                foreach (var definition in provider.GetHotkeyDefinitions())
                {
                    if (!string.IsNullOrWhiteSpace(definition.Id))
                    {
                        yield return definition;
                    }
                }

                yield break;
            }

            if (instance is IHotKey legacyProvider)
            {
                HotKeys hotKeys = legacyProvider.HotKeys;
                if (hotKeys.HotKeyHandler == null) yield break;

                string id = string.IsNullOrWhiteSpace(hotKeys.Id) ? CreateLegacyProviderId(type) : hotKeys.Id;
                Hotkey defaultHotkey = hotKeys.DefaultHotkey.IsEmpty ? hotKeys.Hotkey : hotKeys.DefaultHotkey;
                yield return new HotkeyDefinition(id, hotKeys.Name, defaultHotkey, hotKeys.HotKeyHandler, hotKeys.Kinds);
            }
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(type => type != null).Cast<Type>();
            }
            catch
            {
                return Array.Empty<Type>();
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
