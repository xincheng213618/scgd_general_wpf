using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace ColorVision.UI.HotKey.GlobalHotKey
{

    /// <summary>
    /// 热键管理器
    /// </summary>
    public static class GlobalHotKey
    {
        /// <summary>
        /// 热键消息
        /// </summary>
        public const int WMHOTKEY = 0x312;

        /// <summary>
        /// 注册热键
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, ModifierKeys fsModifuers, uint vk);

        /// <summary>
        /// 注销热键
        /// </summary>
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private static readonly Dictionary<IntPtr, HwndHotkeyScope> Scopes = new();
        private static int _nextAutomaticId;

        internal static bool Matches(IHotkeyRegistration registration, Hotkey hotkey, HotKeyCallBackHanlder callback)
            => registration is GlobalHotkeyRegistration globalRegistration && globalRegistration.IsRegistered
                && globalRegistration.Hotkey == hotkey && globalRegistration.Callback == callback;

        /// <summary>
        /// 注册快捷键
        /// </summary>
        /// <param name="window">持有快捷键窗口</param>
        /// <param name="fsModifiers">组合键</param>
        /// <param name="key">快捷键</param>
        /// <param name="callBack">回调函数</param>
        public static IHotkeyRegistration? Register(IntPtr hwnd, ModifierKeys fsModifiers, Key key, HotKeyCallBackHanlder callBack)
        {
            return TryRegister(hwnd, fsModifiers, key, callBack).Registration;
        }

        internal static HotkeyRegistrationAttempt TryRegister(IntPtr hwnd, ModifierKeys modifiers, Key key, HotKeyCallBackHanlder callback)
        {
            if (key == Key.None) return new(null);
            var scope = GetOrCreateScope(hwnd);
            return scope?.Register(modifiers, key, callback) ?? new(null, "全局快捷键的窗口句柄不可用。");
        }

        /// <summary>
        /// 可以自定义id
        /// </summary>
        public static IHotkeyRegistration? Register(IntPtr hwnd, int id , ModifierKeys fsModifiers, Key key, HotKeyCallBackHanlder callBack)
        {
            if (key == Key.None) return null;

            var scope = GetOrCreateScope(hwnd);
            return scope?.Register(id, fsModifiers, key, callBack).Registration;
        }

        /// <summary>
        /// 注销快捷键
        /// </summary>
        /// <param name="hWnd">持有快捷键窗口的句柄</param>
        /// <param name="callBack">回调函数</param>
        public static void UnRegister(IntPtr hWnd, HotKeyCallBackHanlder callBack)
        {
            if (!Scopes.TryGetValue(hWnd, out var scope)) return;

            foreach (var registration in scope.FindByCallback(callBack))
            {
                registration.Dispose();
            }
        }

        public static bool UnRegister(IHotkeyRegistration registration)
        {
            if (registration is not GlobalHotkeyRegistration globalRegistration || !globalRegistration.IsRegistered)
            {
                return false;
            }

            globalRegistration.Dispose();
            return true;
        }

        private static HwndHotkeyScope? GetOrCreateScope(IntPtr hwnd)
        {
            if (Scopes.TryGetValue(hwnd, out var scope))
            {
                return scope;
            }

            HwndSource? source = HwndSource.FromHwnd(hwnd);
            if (source == null) return null;

            scope = new HwndHotkeyScope(hwnd, source, RemoveScope);
            Scopes.Add(hwnd, scope);
            return scope;
        }

        private static void RemoveScope(IntPtr hwnd)
        {
            Scopes.Remove(hwnd);
        }

        private sealed class HwndHotkeyScope
        {
            private readonly Dictionary<int, GlobalHotkeyRegistration> _registrations = new();
            private readonly HwndSource _source;
            private readonly HwndSourceHook _hook;
            private readonly Action<IntPtr> _removeScope;

            public HwndHotkeyScope(IntPtr hwnd, HwndSource source, Action<IntPtr> removeScope)
            {
                HWnd = hwnd;
                _source = source;
                _removeScope = removeScope;
                _hook = WndProc;
                _source.AddHook(_hook);
            }

            public IntPtr HWnd { get; }

            public HotkeyRegistrationAttempt Register(ModifierKeys modifiers, Key key, HotKeyCallBackHanlder callback)
            {
                // Do not immediately reuse an ID when the last scope is rebuilt after
                // capture; a previously queued WM_HOTKEY must not invoke a new binding.
                for (int attempt = 0; attempt <= 0xBFFF; attempt++)
                {
                    int id = _nextAutomaticId;
                    _nextAutomaticId = (_nextAutomaticId + 1) % 0xC000;
                    if (!_registrations.ContainsKey(id)) return Register(id, modifiers, key, callback);
                }
                return new(null, "当前窗口的全局快捷键 ID 已用尽。");
            }

            public HotkeyRegistrationAttempt Register(int id, ModifierKeys modifiers, Key key, HotKeyCallBackHanlder callback)
            {
                if (_registrations.ContainsKey(id)) return new(null, "当前窗口的全局快捷键 ID 已被使用。");

                int virtualKey = KeyInterop.VirtualKeyFromKey(key);
                const ModifierKeys noRepeat = (ModifierKeys)0x4000;
                if (!RegisterHotKey(HWnd, id, modifiers | noRepeat, (uint)virtualKey))
                {
                    int error = Marshal.GetLastWin32Error();
                    ReleaseEmptyScope();
                    return new(null, $"系统拒绝注册快捷键（{error}）：{new Win32Exception(error).Message}");
                }

                var registration = new GlobalHotkeyRegistration(this, id, new Hotkey(key, modifiers), callback);
                _registrations.Add(id, registration);
                return new(registration);
            }

            public List<GlobalHotkeyRegistration> FindByCallback(HotKeyCallBackHanlder callback)
            {
                return _registrations.Values.Where(registration => registration.Callback == callback).ToList();
            }

            public void Remove(GlobalHotkeyRegistration registration)
            {
                if (!UnregisterHotKey(HWnd, registration.Id))
                {
                    int error = Marshal.GetLastWin32Error();
                    // A destroyed HWND or an already removed registration owns no OS hotkey.
                    if (error is not (1400 or 1419)) throw new Win32Exception(error, "系统未能解除全局快捷键。");
                }
                _registrations.Remove(registration.Id);
                ReleaseEmptyScope();
            }

            private void ReleaseEmptyScope()
            {
                if (_registrations.Count == 0)
                {
                    _source.RemoveHook(_hook);
                    _removeScope(HWnd);
                }
            }

            private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
            {
                if (msg == WMHOTKEY)
                {
                    int id = wParam.ToInt32();
                    if (_registrations.TryGetValue(id, out var registration))
                    {
                        handled = true;
                        if (!HotkeyDispatchGate.ShouldSuppress(registration.Hotkey.Key)) registration.Callback();
                    }
                }

                return IntPtr.Zero;
            }
        }

        private sealed class GlobalHotkeyRegistration : IHotkeyRegistration
        {
            private HwndHotkeyScope? _scope;

            public GlobalHotkeyRegistration(HwndHotkeyScope scope, int id, Hotkey hotkey, HotKeyCallBackHanlder callback)
            {
                _scope = scope;
                Id = id;
                Hotkey = hotkey;
                Callback = callback;
                IsRegistered = true;
            }

            public int Id { get; }
            public HotKeyCallBackHanlder Callback { get; }
            public Hotkey Hotkey { get; }
            public bool IsRegistered { get; private set; }

            public void Dispose()
            {
                if (!IsRegistered) return;

                _scope?.Remove(this);
                _scope = null;
                MarkUnregistered();
            }

            internal void MarkUnregistered()
            {
                IsRegistered = false;
            }
        }

    }
}
