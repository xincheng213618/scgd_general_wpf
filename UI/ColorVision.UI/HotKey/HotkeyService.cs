using ColorVision.UI.HotKey.GlobalHotKey;
using ColorVision.UI.HotKey.WindowHotKey;
using log4net;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.HotKey
{
    public sealed partial class HotkeyService
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(HotkeyService));
        private static readonly object Locker = new();
        private static HotkeyService? _instance;
        private readonly Dictionary<string, HotKeys> _hotkeysById = new(StringComparer.OrdinalIgnoreCase);
        private readonly ObservableCollection<HotKeys> _hotKeys = new();
        private readonly Func<Control, HotKeys, HotkeyRegistrationAttempt> _register;
        private readonly Func<HotKeyConfig, Action, HotkeyPersistenceAttempt> _persist;
        private readonly Func<HotKeyConfig> _getConfig;
        private readonly Func<IEnumerable<Type>> _getProviderTypes;
        private readonly object _operationLock = new();
        private bool _isApplying;
        private int _captureDepth;
        private List<RuntimeSnapshot> _capturedGlobals = new();

        private Window? _hostWindow;

        private HotkeyService()
            : this(RegisterWithBackend, PersistConfiguration, () => HotKeyConfig.Instance)
        {
        }

        internal HotkeyService(Func<Control, HotKeys, HotkeyRegistrationAttempt> register,
            Func<HotKeyConfig, Action, HotkeyPersistenceAttempt> persist, Func<HotKeyConfig> getConfig,
            Func<IEnumerable<Type>>? getProviderTypes = null)
        {
            _register = register ?? throw new ArgumentNullException(nameof(register));
            _persist = persist ?? throw new ArgumentNullException(nameof(persist));
            _getConfig = getConfig ?? throw new ArgumentNullException(nameof(getConfig));
            _getProviderTypes = getProviderTypes ?? DiscoverProviderTypes;
        }

        public HotkeyApplyResult? LastCaptureRestoreResult { get; private set; }
        public HotkeyApplyResult? LastApplyResult { get; private set; }

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
            lock (_operationLock)
            {
                if (_captureDepth > 0 || _isApplying) throw new InvalidOperationException("正在录入或应用快捷键，不能重新加载。");
                _isApplying = true;
                try
                {
                    // Discover and read overrides before releasing a working set. A failed provider
                    // is isolated by TryCreateDefinitions; a failure of discovery itself leaves it intact.
                    List<HotkeyDefinition> definitions = DiscoverDefinitions().ToList();
                    List<HotkeySetting> settings = _getConfig().Hotkeys.Select(CloneSetting).ToList();
                    UnregisterAll();
                    _hostWindow = hostWindow;
                    ClearDefinitions();
                    foreach (HotkeyDefinition definition in definitions) RegisterDefinition(definition);
                    ApplySettingsToRuntime(settings);
                    RegisterAll(hostWindow);
                }
                finally { _isApplying = false; }
            }
        }

        public bool AddHotKeys(Window hostWindow, HotKeys hotKeys) => AddHotKeysCore(hostWindow, hotKeys);

        public bool AddHotKeys(Control control, HotKeys hotKeys)
        {
            if (hotKeys.IsGlobal)
            {
                LastApplyResult = new([new(hotKeys.Id, "全局快捷键需要窗口宿主。")]);
                return false;
            }
            return AddHotKeysCore(control, hotKeys);
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
            if (_captureDepth > 0 || _isApplying) return;
            if (hotKeys.Control == null && _hostWindow == null) return;

            Unregister(hotKeys);
            Window? hostWindow = _hostWindow ?? (hotKeys.Control == null ? null : Window.GetWindow(hotKeys.Control));

            if (hotKeys.IsGlobal)
            {
                if (hostWindow != null)
                {
                    TryRegister(hotKeys);
                }
            }
            else if (hotKeys.Control != null)
            {
                TryRegister(hotKeys);
            }
        }

        public void SetDefault()
        {
            ApplySettings(CreateDefaultEditableHotKeys().Select(HotkeySetting.FromHotKeys).ToList());
        }

        public void ReloadSettings()
        {
            ApplySettings(_getConfig().Hotkeys);
        }

        public List<HotKeys> CreateEditableHotKeys(bool useSavedSettings = false)
        {
            var editableHotKeys = HotKeys.Select(CreateEditableCopy).ToList();
            if (useSavedSettings)
            {
                ApplySettings(editableHotKeys, CreateIdMap(editableHotKeys), _getConfig().Hotkeys);
            }

            return editableHotKeys;
        }

        public List<HotKeys> CreateDefaultEditableHotKeys()
        {
            var editableHotKeys = CreateEditableHotKeys();
            foreach (var hotKeys in editableHotKeys)
            {
                hotKeys.Kinds = hotKeys.DefaultKinds;
                hotKeys.SetBindings(hotKeys.GetDefaultBindings());
            }

            return editableHotKeys;
        }

        public void ApplySettings(IEnumerable<HotkeySetting> settings)
        {
            // Legacy callers may pass the full saved list, including absent plugins and Name-only entries.
            var known = settings.Select(setting => (Setting: setting, Runtime: Find(HotKeys, _hotkeysById, setting)))
                .Where(item => item.Runtime != null)
                .Select(item =>
                {
                    HotkeySetting setting = CloneSetting(item.Setting);
                    setting.Id = item.Runtime!.Id;
                    return setting;
                }).ToList();
            LastApplyResult = ApplyCore(known, save: false);
            if (!LastApplyResult.Success) Log.Warn(LastApplyResult.Message);
        }

        public HotkeyApplyResult ApplyAndSaveSettings(IEnumerable<HotkeySetting> settings)
        {
            LastApplyResult = ApplyCore(settings, save: true);
            return LastApplyResult;
        }

        /// <summary>Checks the final candidate set without registering, saving, or leaving capture mode.</summary>
        public HotkeyApplyResult ValidateSettings(IEnumerable<HotkeySetting> settings)
        {
            lock (_operationLock)
            {
                var errors = new List<HotkeyOperationError>();
                try { ValidateSettings(settings, errors); }
                catch (Exception exception) { errors.Add(new("", exception.GetBaseException().Message)); }
                return new(errors);
            }
        }

        public void SaveSettings()
        {
            PublishConfiguration(BuildCandidateConfiguration(HotKeys.Select(HotkeySetting.FromHotKeys).ToList()));
        }

        public void RegisterAll()
        {
            foreach (var hotKeys in HotKeys)
                if (hotKeys.Registration?.IsRegistered != true)
                    TryRegister(hotKeys);
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
                hotKeys.Control ??= hostWindow;
                if (hotKeys.Registration?.IsRegistered != true)
                    TryRegister(hotKeys);
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

        private bool AddHotKeysCore(Control owner, HotKeys hotKeys)
        {
            lock (_operationLock)
            {
                if (_captureDepth > 0 || _isApplying)
                {
                    LastApplyResult = new([new(hotKeys.Id, "正在录入或应用快捷键，请稍后重试。")]);
                    return false;
                }
                if (owner is not Window && hotKeys.IsGlobal)
                {
                    LastApplyResult = new([new(hotKeys.Id, "全局快捷键需要窗口宿主。")]);
                    return false;
                }

                _isApplying = true;
                Window? previousHost = _hostWindow;
                RuntimeSnapshot? previousRuntime = null;
                HotKeys? previousDefinition = null;
                try
                {
                    string error;
                    try
                    {
                        HotkeyPresentation.Enrich(hotKeys);
                        if (string.IsNullOrWhiteSpace(hotKeys.Id)) hotKeys.Id = CreateCallbackId(hotKeys.HotKeyHandler);
                        HotKeys runtime;
                        if (_hotkeysById.TryGetValue(hotKeys.Id, out var existing))
                        {
                            previousRuntime = Snapshot(existing);
                            previousDefinition = CreateEditableCopy(existing);
                            previousDefinition.HotKeyHandler = existing.HotKeyHandler;
                            // Do not publish the new action until its old registration is released.
                            Unregister(existing);
                            CopyDefinition(existing, hotKeys);
                            existing.SetBindings(hotKeys.GetBindings());
                            existing.Kinds = hotKeys.Kinds;
                            runtime = existing;
                        }
                        else
                        {
                            runtime = hotKeys;
                            _hotkeysById.Add(runtime.Id, runtime);
                            HotKeys.Add(runtime);
                            Unregister(runtime);
                        }
                        runtime.Control = owner;
                        _hostWindow ??= owner as Window;
                        HotkeyRegistrationAttempt attempt = TryRegister(runtime);
                        if (runtime.GetBindings().Count == 0 || attempt.Registration?.IsRegistered == true)
                        {
                            LastApplyResult = new();
                            return runtime.IsRegistered;
                        }
                        error = attempt.Error ?? "快捷键注册失败。";
                    }
                    catch (Exception exception)
                    {
                        error = exception.GetBaseException().Message;
                    }

                    var restoreErrors = new List<HotkeyOperationError>();
                    _hostWindow = previousHost;
                    if (previousRuntime != null && previousDefinition != null)
                    {
                        try
                        {
                            previousRuntime.Entry.HotKeyHandler = previousDefinition.HotKeyHandler;
                            CopyDefinition(previousRuntime.Entry, previousDefinition);
                        }
                        catch (Exception exception)
                        {
                            restoreErrors.Add(new(hotKeys.Id, "恢复原快捷键定义失败：" + exception.GetBaseException().Message));
                        }
                        try { restoreErrors.AddRange(RestoreRuntime([previousRuntime])); }
                        catch (Exception exception)
                        {
                            restoreErrors.Add(new(hotKeys.Id, "恢复原快捷键注册失败：" + exception.GetBaseException().Message));
                        }
                    }
                    LastApplyResult = new([new(hotKeys.Id, error)], restoreErrors);
                    return false;
                }
                finally { _isApplying = false; }
            }
        }

        private static void CopyDefinition(HotKeys target, HotKeys source)
        {
            target.Name = source.Name;
            target.DisplayName = source.DisplayName;
            target.Description = source.Description;
            target.Category = source.Category;
            target.Source = source.Source;
            target.HotKeyHandler = source.HotKeyHandler ?? target.HotKeyHandler;
            target.SetDefaultBindings(source.GetDefaultBindings());
            target.DefaultKinds = source.DefaultKinds;
        }



        private HotkeyRegistrationAttempt TryRegister(HotKeys hotKeys)
        {
            if (hotKeys.GetBindings().Count == 0)
            {
                hotKeys.Registration = null;
                hotKeys.IsRegistered = false;
                return new(null);
            }
            Control? owner = hotKeys.Control ?? _hostWindow;
            if (owner == null || hotKeys.HotKeyHandler == null)
                return new(null, owner == null ? "没有可用的快捷键宿主。" : "快捷键没有可执行的操作。");
            if (_captureDepth > 0)
                return new(null, "正在录入快捷键，请结束录入后重试。");
            try
            {
                var attempt = _register(owner, hotKeys);
                hotKeys.Control = owner;
                hotKeys.Registration = attempt.Registration;
                hotKeys.IsRegistered = attempt.Registration?.IsRegistered == true;
                return hotKeys.IsRegistered ? attempt : new(attempt.Registration, attempt.Error ?? "快捷键注册失败，可能已被占用。");
            }
            catch (Exception exception)
            {
                hotKeys.IsRegistered = false;
                return new(null, exception.GetBaseException().Message);
            }
        }

        private static HotkeyRegistrationAttempt RegisterWithBackend(Control owner, HotKeys hotKeys)
        {
            if (!hotKeys.IsGlobal)
                return WindowHotKeyManager.GetInstance(owner).TryRegisterHandle(hotKeys);
            Window? window = owner as Window ?? Window.GetWindow(owner);
            return window == null ? new(null, "全局快捷键需要已加载的窗口宿主。")
                : GlobalHotKeyManager.GetInstance(window).TryRegisterHandle(hotKeys);
        }

        private static void Unregister(HotKeys hotKeys)
        {
            hotKeys.Registration?.Dispose();
            hotKeys.Registration = null;
            hotKeys.IsRegistered = false;
        }

    }
}
