using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.HotKey.WindowHotKey
{
    public class WindowHotKey
    {
        private static readonly Dictionary<Control, ControlHotkeyScope> Scopes = new();

        internal static bool Matches(IHotkeyRegistration registration, Hotkey hotkey, HotKeyCallBackHanlder callback)
            => registration is WindowHotkeyRegistration windowRegistration && windowRegistration.IsRegistered
                && windowRegistration.Hotkey == hotkey && windowRegistration.Callback == callback;


        public static IHotkeyRegistration? Register(Control control,Hotkey hotkey, HotKeyCallBackHanlder callBack)
        {
            if (hotkey.IsNullOrEmpty()) return null;

            var scope = GetOrCreateScope(control);
            int virtualKey = hotkey.ToInt();
            if (scope.Contains(virtualKey)) return null;

            var registration = new WindowHotkeyRegistration(scope, virtualKey, hotkey, callBack);
            scope.Add(registration);
            return registration;
        }

        public static bool UnRegister(IHotkeyRegistration registration)
        {
            if (registration is not WindowHotkeyRegistration windowRegistration || !windowRegistration.IsRegistered)
            {
                return false;
            }

            windowRegistration.Scope.Remove(windowRegistration);
            windowRegistration.MarkUnregistered();
            return true;
        }

        public static bool UnRegister(HotKeyCallBackHanlder callBack)
        {
            var registrations = Scopes.Values
                .SelectMany(scope => scope.FindByCallback(callBack))
                .ToList();

            foreach (var registration in registrations)
            {
                registration.Dispose();
            }

            return registrations.Count > 0;
        }

        private static ControlHotkeyScope GetOrCreateScope(Control control)
        {
            if (Scopes.TryGetValue(control, out var scope))
            {
                return scope;
            }

            scope = new ControlHotkeyScope(control, RemoveScope);
            Scopes.Add(control, scope);
            return scope;
        }

        private static void RemoveScope(Control control)
        {
            Scopes.Remove(control);
        }

        private sealed class ControlHotkeyScope
        {
            private readonly Dictionary<int, WindowHotkeyRegistration> _registrations = new();
            private readonly Action<Control> _removeScope;
            private readonly KeyEventHandler _keyDownHandler;
            private readonly KeyEventHandler _keyUpHandler;

            public ControlHotkeyScope(Control control, Action<Control> removeScope)
            {
                Control = control;
                _removeScope = removeScope;
                _keyDownHandler = OnPreviewKeyDown;
                _keyUpHandler = OnPreviewKeyUp;
                Control.PreviewKeyDown += _keyDownHandler;
                Control.AddHandler(Keyboard.PreviewKeyUpEvent, _keyUpHandler, handledEventsToo: true);
            }

            public Control Control { get; }

            public bool Contains(int virtualKey) => _registrations.ContainsKey(virtualKey);

            public void Add(WindowHotkeyRegistration registration)
            {
                _registrations.Add(registration.VirtualKey, registration);
            }

            public void Remove(WindowHotkeyRegistration registration)
            {
                _registrations.Remove(registration.VirtualKey);
                if (_registrations.Count == 0)
                {
                    Control.PreviewKeyDown -= _keyDownHandler;
                    Control.RemoveHandler(Keyboard.PreviewKeyUpEvent, _keyUpHandler);
                    _removeScope(Control);
                }
            }

            public List<WindowHotkeyRegistration> FindByCallback(HotKeyCallBackHanlder callback)
            {
                return _registrations.Values.Where(registration => registration.Callback == callback).ToList();
            }

            private void OnPreviewKeyUp(object sender, KeyEventArgs e)
            {
                // Releasing a gesture recorded by the editor only clears its capture gate.
                // Business dispatch belongs to PreviewKeyDown, before WPF command routing.
                HotkeyDispatchGate.ShouldSuppress(e.Key == Key.System ? e.SystemKey : e.Key, isKeyUp: true);
            }

            private void OnPreviewKeyDown(object sender, KeyEventArgs e)
            {
                ModifierKeys modifiers = Keyboard.Modifiers;
                if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
                    modifiers |= ModifierKeys.Windows;

                Key key = e.Key;
                if (key == Key.System)
                    key = e.SystemKey;

                if (modifiers == ModifierKeys.None && (key == Key.Delete || key == Key.Back || key == Key.Escape))
                    return;

                if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin || key == Key.Clear || key == Key.OemClear || key == Key.Apps)
                    return;

                int virtualKey = ((int)modifiers << 8) + (int)key;
                if (_registrations.TryGetValue(virtualKey, out var registration))
                {
                    if (HotkeyDispatchGate.ShouldSuppress(key))
                    {
                        // Let the recorder see input while open. After it closes, consume
                        // the held gesture so a native command cannot receive its repeats.
                        if (!HotkeyDispatchGate.IsSuspended)
                            e.Handled = true;
                        return;
                    }
                    e.Handled = true;
                    if (!e.IsRepeat)
                        registration.Callback();
                }
            }
        }

        private sealed class WindowHotkeyRegistration : IHotkeyRegistration
        {
            public WindowHotkeyRegistration(ControlHotkeyScope scope, int virtualKey, Hotkey hotkey, HotKeyCallBackHanlder callback)
            {
                Scope = scope;
                VirtualKey = virtualKey;
                Hotkey = new Hotkey(hotkey.Key, hotkey.Modifiers);
                Callback = callback;
                IsRegistered = true;
            }

            public ControlHotkeyScope Scope { get; }
            public int VirtualKey { get; }
            public HotKeyCallBackHanlder Callback { get; }
            public Hotkey Hotkey { get; }
            public bool IsRegistered { get; private set; }

            public void Dispose()
            {
                WindowHotKey.UnRegister(this);
            }

            internal void MarkUnregistered()
            {
                IsRegistered = false;
            }
        }






    }
}
