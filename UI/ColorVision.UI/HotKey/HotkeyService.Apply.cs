using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.HotKey
{
    public sealed partial class HotkeyService
    {
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
                        if (!runtime.GetBindings().SequenceEqual(setting.GetBindings()) || runtime.Kinds != setting.Kinds
                            || (setting.GetBindings().Count > 0 && runtime.Registration?.IsRegistered != true))
                            oldRuntime.Add(Snapshot(runtime));
                    }

                    // Release the changed set together, allowing two explicitly edited bindings to swap.
                    foreach (RuntimeSnapshot snapshot in oldRuntime) Unregister(snapshot.Entry);
                    foreach (RuntimeSnapshot snapshot in oldRuntime)
                    {
                        HotkeySetting setting = requested.First(item => string.Equals(item.Id, snapshot.Entry.Id, StringComparison.OrdinalIgnoreCase));
                        snapshot.Entry.SetBindings(setting.GetBindings());
                        snapshot.Entry.Kinds = setting.Kinds;
                        HotkeyRegistrationAttempt attempt = TryRegister(snapshot.Entry);
                        if (setting.GetBindings().Count > 0 && attempt.Registration?.IsRegistered != true)
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
            }).SelectMany(item => item.Setting.GetBindings().Select(binding => (item.Runtime, item.Setting, Binding: binding))).ToList();
            for (int first = 0; first < proposed.Count; first++)
            {
                for (int second = first + 1; second < proposed.Count; second++)
                {
                    var left = proposed[first];
                    var right = proposed[second];
                    if (ReferenceEquals(left.Runtime, right.Runtime) || left.Binding != right.Binding
                        || (!ids.Contains(left.Runtime.Id) && !ids.Contains(right.Runtime.Id))) continue;
                    Control? leftOwner = left.Runtime.Control ?? _hostWindow;
                    Control? rightOwner = right.Runtime.Control ?? _hostWindow;
                    if (left.Setting.Kinds == HotKeyKinds.Global || right.Setting.Kinds == HotKeyKinds.Global
                        || ReferenceEquals(leftOwner, rightOwner)
                        || (leftOwner != null && rightOwner != null && (IsScopeAncestor(leftOwner, rightOwner) || IsScopeAncestor(rightOwner, leftOwner))))
                    {
                        var edited = ids.Contains(left.Runtime.Id) ? left : right;
                        var conflict = ids.Contains(left.Runtime.Id) ? right : left;
                        errors.Add(new(edited.Runtime.Id, $"{edited.Binding} 与“{conflict.Runtime.Name}”冲突。"));
                    }
                }
            }
            return requested;
        }

        private static bool IsScopeAncestor(Control ancestor, Control descendant)
        {
            if (ancestor.IsAncestorOf(descendant)) return true;
            // Content is logically attached before templates create its visual route. Treat it
            // as an overlapping scope now, rather than allowing a conflict until first layout.
            for (DependencyObject? parent = LogicalTreeHelper.GetParent(descendant); parent != null; parent = LogicalTreeHelper.GetParent(parent))
                if (ReferenceEquals(parent, ancestor)) return true;
            return false;
        }

        private static string? ValidateCombination(HotkeySetting setting)
        {
            if (!Enum.IsDefined(setting.Kinds) || setting.Hotkey == null) return "快捷键类型或组合无效。";
            if (!HotkeyInput.IsValid(setting.Hotkey) || setting.AdditionalHotkeys.Any(binding => binding == null || binding.IsEmpty || !HotkeyInput.IsValid(binding)))
                return "无效的按键组合；普通字符键需搭配 Ctrl、Alt 或 Win，不能只使用修饰键。";
            var bindings = new HashSet<Hotkey>();
            foreach (Hotkey binding in setting.GetBindings())
                if (!bindings.Add(binding)) return $"同一操作包含重复的快捷键 {binding}。";
            return null;
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
            AdditionalHotkeys = CloneBindings(setting.AdditionalHotkeys),
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

        private static RuntimeSnapshot Snapshot(HotKeys entry) => new(entry, CloneBindings(entry.GetBindings()), entry.Kinds, entry.Control, entry.Registration?.IsRegistered == true);

        private List<HotkeyOperationError> RestoreRuntime(IReadOnlyList<RuntimeSnapshot> snapshots)
        {
            var errors = new List<HotkeyOperationError>();
            var blocked = new HashSet<HotKeys>();
            foreach (RuntimeSnapshot snapshot in snapshots)
            {
                if (snapshot.Entry.Registration?.IsRegistered == true && snapshot.Entry.GetBindings().SequenceEqual(snapshot.Bindings)
                    && snapshot.Entry.Kinds == snapshot.Kinds && ReferenceEquals(snapshot.Entry.Control, snapshot.Owner)) continue;
                try { Unregister(snapshot.Entry); }
                catch (Exception exception)
                {
                    errors.Add(new(snapshot.Entry.Id, "撤销新绑定失败：" + exception.GetBaseException().Message));
                    blocked.Add(snapshot.Entry);
                    continue;
                }
                snapshot.Entry.SetBindings(snapshot.Bindings);
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
    }
}
