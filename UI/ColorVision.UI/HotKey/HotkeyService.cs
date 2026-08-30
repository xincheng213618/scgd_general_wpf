using ColorVision.UI.HotKey.GlobalHotKey;
using ColorVision.UI.HotKey.WindowHotKey;
using log4net;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.HotKey
{
    public sealed class HotkeyService
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
            ApplySettings(HotKeys.Select(hotKeys => new HotkeySetting { Id = hotKeys.Id, Hotkey = CloneHotkey(hotKeys.DefaultHotkey), Kinds = hotKeys.DefaultKinds }).ToList());
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
                hotKeys.Hotkey = CloneHotkey(hotKeys.DefaultHotkey);
            }

            return editableHotKeys;
        }

        public void ApplySettings(IEnumerable<HotkeySetting> settings)
        {
            // Legacy callers may pass the full saved list, including absent plugins and Name-only entries.
            var known = settings.Select(setting => (Setting: setting, Runtime: Find(HotKeys, _hotkeysById, setting)))
                .Where(item => item.Runtime != null)
                .Select(item => new HotkeySetting { Id = item.Runtime!.Id, Hotkey = CloneHotkey(item.Setting.Hotkey), Kinds = item.Setting.Kinds }).ToList();
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
                            existing.Hotkey = CloneHotkey(hotKeys.Hotkey);
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
                        if (runtime.Hotkey.IsEmpty || attempt.Registration?.IsRegistered == true)
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
            target.DefaultHotkey = CloneHotkey(source.DefaultHotkey);
            target.DefaultKinds = source.DefaultKinds;
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
                DisplayName = source.DisplayName,
                Description = source.Description,
                Category = source.Category,
                Source = source.Source,
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

        private HotkeyRegistrationAttempt TryRegister(HotKeys hotKeys)
        {
            if (hotKeys.Hotkey.IsEmpty)
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

        private HotkeyApplyResult ApplyCore(IEnumerable<HotkeySetting> settings, bool save)
        {
            lock (_operationLock)
            {
                if (_captureDepth > 0 || _isApplying)
                    return new([new("", "正在录入或应用快捷键，请稍后重试。")]);
                _isApplying = true;
                var oldRuntime = new List<RuntimeSnapshot>();
                try
                {
                    var errors = new List<HotkeyOperationError>();
                    List<HotkeySetting> requested = ValidateSettings(settings, errors);
                    if (errors.Count > 0) return new(errors);
                    if (requested.Count == 0) return new();
                    HotKeyConfig? oldConfig = save ? CloneConfiguration(_getConfig()) : null;
                    HotKeyConfig? candidate = save ? BuildCandidateConfiguration(requested) : null;
                    foreach (HotkeySetting setting in requested)
                    {
                        HotKeys runtime = _hotkeysById[setting.Id];
                        if (runtime.Hotkey != setting.Hotkey || runtime.Kinds != setting.Kinds
                            || (!setting.Hotkey.IsEmpty && runtime.Registration?.IsRegistered != true))
                            oldRuntime.Add(Snapshot(runtime));
                    }

                    // Release the changed set together, allowing two explicitly edited bindings to swap.
                    foreach (RuntimeSnapshot snapshot in oldRuntime) Unregister(snapshot.Entry);
                    foreach (RuntimeSnapshot snapshot in oldRuntime)
                    {
                        HotkeySetting setting = requested.First(item => string.Equals(item.Id, snapshot.Entry.Id, StringComparison.OrdinalIgnoreCase));
                        snapshot.Entry.Hotkey = CloneHotkey(setting.Hotkey);
                        snapshot.Entry.Kinds = setting.Kinds;
                        HotkeyRegistrationAttempt attempt = TryRegister(snapshot.Entry);
                        if (!setting.Hotkey.IsEmpty && attempt.Registration?.IsRegistered != true)
                            errors.Add(new(setting.Id, attempt.Error ?? "快捷键注册失败。"));
                    }
                    if (errors.Count > 0) return new(errors, RestoreRuntime(oldRuntime));
                    if (!save) return new();

                    HotkeyPersistenceAttempt persistence = _persist(candidate!, () => PublishConfiguration(candidate!));
                    if (persistence.Status == ConfigSavePublicationStatus.PersistedAndPublished) return new();
                    errors.Add(new("", persistence.Error ?? "快捷键设置未能完整保存。"));
                    if (persistence.Status == ConfigSavePublicationStatus.NotPersisted)
                        return new(errors, RestoreRuntime(oldRuntime));

                    // A publication failure has already committed new bytes. Compensate the disk
                    // before restoring old runtime bindings, and expose compensation failures.
                    var restoreErrors = new List<HotkeyOperationError>();
                    HotkeyPersistenceAttempt compensation;
                    try { compensation = _persist(oldConfig!, () => PublishConfiguration(oldConfig!)); }
                    catch (Exception exception)
                    {
                        compensation = new(ConfigSavePublicationStatus.NotPersisted, exception.GetBaseException().Message);
                    }
                    if (compensation.Status == ConfigSavePublicationStatus.NotPersisted)
                    {
                        restoreErrors.Add(new("", "旧配置恢复失败；已保存的新键位继续保留。" + compensation.Error));
                        try { PublishConfiguration(candidate!); }
                        catch (Exception exception) { restoreErrors.Add(new("", exception.GetBaseException().Message)); }
                    }
                    else
                    {
                        restoreErrors.AddRange(RestoreRuntime(oldRuntime));
                        if (compensation.Status == ConfigSavePublicationStatus.PersistedButPublishFailed)
                            restoreErrors.Add(new("", "旧配置已恢复到磁盘，但内存发布失败：" + compensation.Error));
                    }
                    return new(errors, restoreErrors);
                }
                catch (Exception exception)
                {
                    return new([new("", exception.GetBaseException().Message)], RestoreRuntime(oldRuntime));
                }
                finally
                {
                    _isApplying = false;
                }
            }
        }

        private List<HotkeySetting> ValidateSettings(IEnumerable<HotkeySetting> settings, List<HotkeyOperationError> errors)
        {
            ArgumentNullException.ThrowIfNull(settings);
            var requested = new List<HotkeySetting>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (HotkeySetting setting in settings)
            {
                if (setting == null || string.IsNullOrWhiteSpace(setting.Id) || setting.Id != setting.Id.Trim()
                    || setting.Id.Length > 1024 || setting.Id.Any(char.IsControl))
                {
                    errors.Add(new(setting?.Id ?? "", "快捷键 ID 为空或不合法。"));
                    continue;
                }
                if (!ids.Add(setting.Id))
                    errors.Add(new(setting.Id, "提交中包含重复的快捷键 ID。"));
                if (!_hotkeysById.ContainsKey(setting.Id))
                    errors.Add(new(setting.Id, "快捷键尚未加载或已经移除，请刷新列表。"));
                string? validation = ValidateCombination(setting);
                if (validation != null) errors.Add(new(setting.Id, validation));
                requested.Add(CloneSetting(setting));
            }
            if (errors.Count > 0) return requested;
            var proposed = HotKeys.Select(runtime =>
            {
                HotkeySetting? setting = requested.FirstOrDefault(item => string.Equals(item.Id, runtime.Id, StringComparison.OrdinalIgnoreCase));
                return (Runtime: runtime, Setting: setting ?? HotkeySetting.FromHotKeys(runtime));
            }).Where(item => !item.Setting.Hotkey.IsEmpty).ToList();
            for (int first = 0; first < proposed.Count; first++)
            {
                for (int second = first + 1; second < proposed.Count; second++)
                {
                    var left = proposed[first];
                    var right = proposed[second];
                    if (left.Setting.Hotkey != right.Setting.Hotkey || (!ids.Contains(left.Runtime.Id) && !ids.Contains(right.Runtime.Id))) continue;
                    Control? leftOwner = left.Runtime.Control ?? _hostWindow;
                    Control? rightOwner = right.Runtime.Control ?? _hostWindow;
                    if (left.Setting.Kinds == HotKeyKinds.Global || right.Setting.Kinds == HotKeyKinds.Global
                        || ReferenceEquals(leftOwner, rightOwner)
                        || (leftOwner != null && rightOwner != null && (leftOwner.IsAncestorOf(rightOwner) || rightOwner.IsAncestorOf(leftOwner))))
                    {
                        var edited = ids.Contains(left.Runtime.Id) ? left : right;
                        var conflict = ids.Contains(left.Runtime.Id) ? right : left;
                        errors.Add(new(edited.Runtime.Id, $"{edited.Setting.Hotkey} 与“{conflict.Runtime.Name}”冲突。"));
                    }
                }
            }
            return requested;
        }

        private static string? ValidateCombination(HotkeySetting setting)
        {
            if (!Enum.IsDefined(setting.Kinds) || setting.Hotkey == null) return "快捷键类型或组合无效。";
            return HotkeyInput.IsValid(setting.Hotkey) ? null : "无效的按键组合；普通字符键需搭配 Ctrl、Alt 或 Win，不能只使用修饰键。";
        }

        private HotKeyConfig BuildCandidateConfiguration(IReadOnlyList<HotkeySetting> requested)
        {
            var changedIds = requested.Select(item => item.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var items = _getConfig().Hotkeys.Where(setting =>
            {
                if (!string.IsNullOrWhiteSpace(setting.Id)) return !changedIds.Contains(setting.Id);
                HotKeys? matched = Find(HotKeys, _hotkeysById, setting);
                return matched == null || !changedIds.Contains(matched.Id);
            }).Select(CloneSetting).ToList();
            items.AddRange(requested.Select(CloneSetting));
            return new HotKeyConfig { Hotkeys = new ObservableCollection<HotkeySetting>(items) };
        }

        private static HotkeySetting CloneSetting(HotkeySetting setting) => new()
        {
            Id = setting.Id,
            LegacyName = setting.LegacyName,
            Hotkey = CloneHotkey(setting.Hotkey),
            Kinds = setting.Kinds
        };

        private static HotKeyConfig CloneConfiguration(HotKeyConfig config) => new()
        {
            Hotkeys = new ObservableCollection<HotkeySetting>(config.Hotkeys.Select(CloneSetting))
        };

        private void PublishConfiguration(HotKeyConfig candidate) => _getConfig().Hotkeys = CloneConfiguration(candidate).Hotkeys;

        private static HotkeyPersistenceAttempt PersistConfiguration(HotKeyConfig candidate, Action publish)
        {
            if (ConfigService.Instance is not ConfigHandler handler)
                return new(ConfigSavePublicationStatus.NotPersisted, "当前配置服务不支持安全保存快捷键设置。");
            ConfigSavePublicationStatus status = handler.TrySaveAndPublish(candidate, publish, out string error);
            return new(status, error);
        }

        private static RuntimeSnapshot Snapshot(HotKeys entry) => new(entry, CloneHotkey(entry.Hotkey), entry.Kinds, entry.Control, entry.Registration?.IsRegistered == true);

        private List<HotkeyOperationError> RestoreRuntime(IReadOnlyList<RuntimeSnapshot> snapshots)
        {
            var errors = new List<HotkeyOperationError>();
            var blocked = new HashSet<HotKeys>();
            foreach (RuntimeSnapshot snapshot in snapshots)
            {
                if (snapshot.Entry.Registration?.IsRegistered == true && snapshot.Entry.Hotkey == snapshot.Hotkey
                    && snapshot.Entry.Kinds == snapshot.Kinds && ReferenceEquals(snapshot.Entry.Control, snapshot.Owner)) continue;
                try { Unregister(snapshot.Entry); }
                catch (Exception exception)
                {
                    errors.Add(new(snapshot.Entry.Id, "撤销新绑定失败：" + exception.GetBaseException().Message));
                    blocked.Add(snapshot.Entry);
                    continue;
                }
                snapshot.Entry.Hotkey = CloneHotkey(snapshot.Hotkey);
                snapshot.Entry.Kinds = snapshot.Kinds;
                snapshot.Entry.Control = snapshot.Owner;
            }
            foreach (RuntimeSnapshot snapshot in snapshots.Where(item => item.WasRegistered))
            {
                if (blocked.Contains(snapshot.Entry) || snapshot.Entry.Registration?.IsRegistered == true) continue;
                HotkeyRegistrationAttempt attempt = TryRegister(snapshot.Entry);
                if (attempt.Registration?.IsRegistered != true)
                    errors.Add(new(snapshot.Entry.Id, "恢复原快捷键失败：" + attempt.Error));
            }
            return errors;
        }

        public HotkeyCaptureLease BeginCapture()
        {
            lock (_operationLock)
            {
                if (_isApplying) throw new InvalidOperationException("正在应用快捷键，不能开始录入。");
                if (_captureDepth > 0)
                {
                    _captureDepth++;
                    return new HotkeyCaptureLease(EndCapture);
                }
                HotkeyDispatchGate.Enter();
                _capturedGlobals = HotKeys.Where(item => item.IsGlobal && item.Registration?.IsRegistered == true).Select(Snapshot).ToList();
                try
                {
                    foreach (RuntimeSnapshot snapshot in _capturedGlobals) Unregister(snapshot.Entry);
                    _captureDepth = 1;
                    LastCaptureRestoreResult = null;
                    return new HotkeyCaptureLease(EndCapture);
                }
                catch (Exception exception)
                {
                    LastCaptureRestoreResult = new(restoreErrors: RestoreRuntime(_capturedGlobals));
                    _capturedGlobals.Clear();
                    HotkeyDispatchGate.Exit();
                    if (!LastCaptureRestoreResult.Success)
                        throw new InvalidOperationException(exception.Message + Environment.NewLine + LastCaptureRestoreResult.Message, exception);
                    throw;
                }
            }
        }

        private HotkeyApplyResult EndCapture()
        {
            lock (_operationLock)
            {
                if (--_captureDepth > 0) return new();
                try
                {
                    LastCaptureRestoreResult = new(restoreErrors: RestoreRuntime(_capturedGlobals));
                    return LastCaptureRestoreResult;
                }
                finally
                {
                    _capturedGlobals.Clear();
                    HotkeyDispatchGate.Exit();
                }
            }
        }

        private sealed record RuntimeSnapshot(HotKeys Entry, Hotkey Hotkey, HotKeyKinds Kinds, Control? Owner, bool WasRegistered);

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
                    Hotkey defaultHotkey = hotKeys.DefaultHotkey.IsEmpty ? hotKeys.Hotkey : hotKeys.DefaultHotkey;
                    definitions.Add(HotkeyPresentation.Enrich(new HotkeyDefinition(id, hotKeys.Name, defaultHotkey, hotKeys.HotKeyHandler, hotKeys.Kinds)
                    {
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
