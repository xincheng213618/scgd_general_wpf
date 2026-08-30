namespace ColorVision.UI.HotKey
{
    /// <summary>One action owns all of its alternate bindings, including partially failed cleanup.</summary>
    internal sealed class HotkeyRegistrationGroup : IHotkeyRegistration
    {
        private readonly IReadOnlyList<Hotkey> _bindings;
        private readonly HotKeyCallBackHanlder _callback;
        private readonly List<IHotkeyRegistration> _registrations = new();
        private bool _complete;

        private HotkeyRegistrationGroup(IReadOnlyList<Hotkey> bindings, HotKeyCallBackHanlder callback)
        {
            _bindings = HotkeyBindingCollection.Copy(bindings);
            _callback = callback;
        }

        public Hotkey Hotkey => HotkeyBindingCollection.Copy(_bindings.FirstOrDefault());
        public bool IsRegistered => _complete && _registrations.Count == _bindings.Count && _registrations.All(item => item.IsRegistered);
        internal IReadOnlyList<IHotkeyRegistration> Registrations => _registrations;

        internal static bool Matches(IHotkeyRegistration registration, IReadOnlyList<Hotkey> bindings,
            HotKeyCallBackHanlder callback, Func<IHotkeyRegistration, Hotkey, HotKeyCallBackHanlder, bool> matchesSingle)
            => registration is HotkeyRegistrationGroup group
                ? group.IsRegistered && group._callback == callback && group._bindings.SequenceEqual(bindings)
                : bindings.Count == 1 && matchesSingle(registration, bindings[0], callback);

        internal static HotkeyRegistrationAttempt TryRegister(IReadOnlyList<Hotkey> bindings, HotKeyCallBackHanlder callback,
            Func<Hotkey, HotKeyCallBackHanlder, HotkeyRegistrationAttempt> register)
        {
            if (bindings.Count == 0) return new(null);
            if (bindings.Any(binding => binding.IsNullOrEmpty())) return new(null, "快捷键列表不能包含空绑定。");
            if (bindings.Distinct().Count() != bindings.Count) return new(null, "同一操作不能重复绑定相同快捷键。");
            if (bindings.Count == 1) return register(bindings[0], callback);

            HotkeyRegistrationGroup group = new(bindings, callback);
            try
            {
                foreach (Hotkey binding in bindings)
                {
                    HotkeyRegistrationAttempt attempt = register(binding, group.Dispatch);
                    if (attempt.Registration != null) group._registrations.Add(attempt.Registration);
                    if (attempt.Registration?.IsRegistered != true)
                        return group.RollBack(attempt.Error ?? "快捷键注册失败。");
                }
                group._complete = true;
                return new(group);
            }
            catch (Exception exception)
            {
                return group.RollBack(exception.GetBaseException().Message);
            }
        }

        private void Dispatch()
        {
            // An incomplete/partly released group must not invoke a business action.
            if (IsRegistered) _callback();
        }

        private HotkeyRegistrationAttempt RollBack(string error)
        {
            try { Dispose(); return new(null, error); }
            catch (Exception exception)
            {
                // Return ownership of unreleased children so the caller can retry disposal.
                return new(this, error + Environment.NewLine + "清理部分注册失败：" + exception.Message);
            }
        }

        public void Dispose()
        {
            _complete = false;
            List<Exception> errors = new();
            foreach (IHotkeyRegistration registration in _registrations)
            {
                if (!registration.IsRegistered) continue;
                try { registration.Dispose(); }
                catch (Exception exception) { errors.Add(exception); }
            }
            if (errors.Count > 0) throw new AggregateException("未能释放全部快捷键绑定。", errors);
        }
    }
}
