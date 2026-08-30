using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.HotKey.WindowHotKey
{
    public class WindowHotKeyManager
    {
        public static Dictionary<Control, WindowHotKeyManager> Instances { get; set; } = new Dictionary<Control, WindowHotKeyManager>();

        private static readonly object locker = new();

        public Control control { get; set; }

        private WindowHotKeyManager(Control window)
        {
            control = window;
            Instances.Add(window, this);
            Registrations = new Dictionary<HotKeys, IHotkeyRegistration>();
            CallbackRegistrations = new Dictionary<HotKeyCallBackHanlder, IHotkeyRegistration>();
            if (window is Window win)
            {
                win.Closed += Window_Closed;
            }
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            foreach (var registration in Registrations.Values.Concat(CallbackRegistrations.Values).Distinct().ToList())
            {
                registration.Dispose();
            }
            foreach (var (hotkeys, registration) in Registrations)
            {
                if (!ReferenceEquals(hotkeys.Registration, registration)) continue;
                hotkeys.Registration = null;
                hotkeys.IsRegistered = false;
            }
            Registrations.Clear();
            CallbackRegistrations.Clear();
            if (control is Window window) window.Closed -= Window_Closed;
            Instances.Remove(control);
        }


        public static WindowHotKeyManager GetInstance(Control control)
        {
            lock (locker)
            {
                if (Instances.TryGetValue(control,out WindowHotKeyManager window))
                {
                    return window;
                }
                else
                {
                    return new WindowHotKeyManager(control);
                }
            }
        }

        private Dictionary<HotKeys, IHotkeyRegistration> Registrations { get; }
        private Dictionary<HotKeyCallBackHanlder, IHotkeyRegistration> CallbackRegistrations { get; }


        public bool Register(HotKeys hotkeys)
        {
            return RegisterHandle(hotkeys)?.IsRegistered == true;
        }

        public IHotkeyRegistration? RegisterHandle(HotKeys hotkeys)
        {
            return TryRegisterHandle(hotkeys).Registration;
        }

        internal HotkeyRegistrationAttempt TryRegisterHandle(HotKeys hotkeys)
        {
            if (hotkeys.HotKeyHandler == null) return new(null, "快捷键没有可执行的操作。");
            if (Registrations.TryGetValue(hotkeys, out var existing))
            {
                if (WindowHotKey.Matches(existing, hotkeys.Hotkey, hotkeys.HotKeyHandler))
                    return new(existing);
                existing.Dispose();
                Registrations.Remove(hotkeys);
            }

            hotkeys.Control = control;
            var registration = WindowHotKey.Register(control, hotkeys.Hotkey, hotkeys.HotKeyHandler);
            hotkeys.Registration = registration;
            hotkeys.IsRegistered = registration?.IsRegistered == true;
            if (registration != null)
            {
                Registrations[hotkeys] = registration;
            }

            return new(registration, registration == null && !hotkeys.Hotkey.IsEmpty ? "此控件内已经注册了相同快捷键。" : null);
        }

        public bool Register(Hotkey hotkey, HotKeyCallBackHanlder callBack)
        {
            if (CallbackRegistrations.TryGetValue(callBack, out var existing))
            {
                if (WindowHotKey.Matches(existing, hotkey, callBack)) return true;
                existing.Dispose();
                CallbackRegistrations.Remove(callBack);
            }
            var registration = WindowHotKey.Register(control, hotkey, callBack);
            if (registration == null)
                return false;
            CallbackRegistrations[callBack] = registration;
            return true;
        }

        public bool UnRegister(HotKeys hotkeys)
        {
            if (Registrations.TryGetValue(hotkeys, out var registration))
            {
                registration.Dispose();
                Registrations.Remove(hotkeys);
            }
            else
            {
                hotkeys.Registration?.Dispose();
            }
            hotkeys.Registration = null;
            hotkeys.IsRegistered = false;
            return true;
        }

        public bool UnRegister(HotKeyCallBackHanlder callBack)
        {
            if (CallbackRegistrations.TryGetValue(callBack, out var registration))
            {
                registration.Dispose();
                CallbackRegistrations.Remove(callBack);
            }
            return true;
        }


        public bool ModifiedHotkey(HotKeys hotkeys)
        {
            UnRegister(hotkeys);
            return Register(hotkeys);
        }

        public void ModifiedHotkey(Hotkey hotkey, HotKeyCallBackHanlder callBack)
        {
            UnRegister(callBack);
            Register(hotkey, callBack);
        }

    }
}
