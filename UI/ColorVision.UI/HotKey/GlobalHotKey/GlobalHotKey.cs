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

        /// <summary>
        /// 注册快捷键
        /// </summary>
        /// <param name="window">持有快捷键窗口</param>
        /// <param name="fsModifiers">组合键</param>
        /// <param name="key">快捷键</param>
        /// <param name="callBack">回调函数</param>
        public static IHotkeyRegistration? Register(IntPtr hwnd, ModifierKeys fsModifiers, Key key, HotKeyCallBackHanlder callBack)
        {
            if (key == Key.None) return null;

            var scope = GetOrCreateScope(hwnd);
            return scope?.Register(fsModifiers, key, callBack);
        }

        /// <summary>
        /// 可以自定义id
        /// </summary>
        public static IHotkeyRegistration? Register(IntPtr hwnd, int id , ModifierKeys fsModifiers, Key key, HotKeyCallBackHanlder callBack)
        {
            if (key == Key.None) return null;

            var scope = GetOrCreateScope(hwnd);
            return scope?.Register(id, fsModifiers, key, callBack);
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
            private int _nextId;

            public HwndHotkeyScope(IntPtr hwnd, HwndSource source, Action<IntPtr> removeScope)
            {
                HWnd = hwnd;
                _source = source;
                _removeScope = removeScope;
                _hook = WndProc;
                _source.AddHook(_hook);
            }

            public IntPtr HWnd { get; }

            public GlobalHotkeyRegistration? Register(ModifierKeys modifiers, Key key, HotKeyCallBackHanlder callback)
            {
                return Register(_nextId++, modifiers, key, callback);
            }

            public GlobalHotkeyRegistration? Register(int id, ModifierKeys modifiers, Key key, HotKeyCallBackHanlder callback)
            {
                if (_registrations.ContainsKey(id)) return null;

                int virtualKey = KeyInterop.VirtualKeyFromKey(key);
                if (!RegisterHotKey(HWnd, id, modifiers, (uint)virtualKey))
                {
                    UnregisterHotKey(HWnd, id);
                    return null;
                }

                var registration = new GlobalHotkeyRegistration(this, id, new Hotkey(key, modifiers), callback);
                _registrations.Add(id, registration);
                return registration;
            }

            public List<GlobalHotkeyRegistration> FindByCallback(HotKeyCallBackHanlder callback)
            {
                return _registrations.Values.Where(registration => registration.Callback == callback).ToList();
            }

            public void Remove(GlobalHotkeyRegistration registration)
            {
                UnregisterHotKey(HWnd, registration.Id);
                _registrations.Remove(registration.Id);
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
                        registration.Callback();
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
