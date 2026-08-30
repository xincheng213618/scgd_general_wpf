using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace ColorVision.UI.HotKey.GlobalHotKey
{
    public class GlobalHotKeyManager
    {
        public IntPtr WindowHandle { get; set; }

        public static Dictionary<IntPtr, GlobalHotKeyManager> Instances { get; set; } = new Dictionary<IntPtr, GlobalHotKeyManager>();
        private Dictionary<HotKeys, IHotkeyRegistration> Registrations { get; } = new();
        private Dictionary<HotKeyCallBackHanlder, IHotkeyRegistration> CallbackRegistrations { get; } = new();


        private GlobalHotKeyManager(Window window, IntPtr intPtr)
        {
            WindowHandle = intPtr;
            Instances.Add(intPtr,this);
            window.Closed += Window_Closed;
        }

        private void Window_Closed(object? sender, EventArgs e)
        {
            foreach (var registration in Registrations.Values.Concat(CallbackRegistrations.Values).Distinct().ToList())
            {
                try { registration.Dispose(); }
                catch (Exception exception) { System.Diagnostics.Trace.TraceWarning(exception.Message); }
            }

            foreach (var (hotkeys, registration) in Registrations)
            {
                if (!ReferenceEquals(hotkeys.Registration, registration)) continue;
                hotkeys.Registration = null;
                hotkeys.IsRegistered = false;
            }
            Registrations.Clear();
            CallbackRegistrations.Clear();
            if (sender is Window window) window.Closed -= Window_Closed;
            Instances.Remove(WindowHandle);
        }
        private static readonly object locker = new();

        public static GlobalHotKeyManager GetInstance(Window window)
        {
            IntPtr intPtr = new WindowInteropHelper(window).EnsureHandle();
            lock (locker)
            {
                if (Instances.TryGetValue(intPtr, out GlobalHotKeyManager globalHotKeyManager))
                {
                    return globalHotKeyManager;
                }
                else
                {
                    return new GlobalHotKeyManager(window, intPtr);
                }
            }
        }


        public bool Register(HotKeys hotKeys)
        {
            return RegisterHandle(hotKeys)?.IsRegistered == true;
        }

        public IHotkeyRegistration? RegisterHandle(HotKeys hotKeys)
        {
            return TryRegisterHandle(hotKeys).Registration;
        }

        internal HotkeyRegistrationAttempt TryRegisterHandle(HotKeys hotKeys)
        {
            if (hotKeys == null || hotKeys.Kinds != HotKeyKinds.Global || hotKeys.HotKeyHandler == null)
                return new(null, "全局快捷键类型或操作无效。");
            if (Registrations.TryGetValue(hotKeys, out var existing))
            {
                if (GlobalHotKey.Matches(existing, hotKeys.Hotkey, hotKeys.HotKeyHandler)) return new(existing);
                existing.Dispose();
                Registrations.Remove(hotKeys);
            }

            HotkeyRegistrationAttempt attempt = GlobalHotKey.TryRegister(WindowHandle, hotKeys.Hotkey.Modifiers, hotKeys.Hotkey.Key, hotKeys.HotKeyHandler);
            var registration = attempt.Registration;
            hotKeys.Registration = registration;
            hotKeys.IsRegistered = registration?.IsRegistered == true;
            if (registration != null)
            {
                Registrations[hotKeys] = registration;
            }
            return attempt;
        }

        public bool Register(Hotkey hotkey, HotKeyCallBackHanlder callBack)
        {
            if (hotkey.IsNullOrEmpty()) return false;
            if (CallbackRegistrations.TryGetValue(callBack, out var existing))
            {
                if (GlobalHotKey.Matches(existing, hotkey, callBack)) return true;
                existing.Dispose();
                CallbackRegistrations.Remove(callBack);
            }
            var registration = GlobalHotKey.Register(WindowHandle, hotkey.Modifiers, hotkey.Key, callBack);
            if (registration == null) return false;

            CallbackRegistrations[callBack] = registration;
            return true;
        }
        public bool Register(ModifierKeys modifierKeys, Key key, HotKeyCallBackHanlder callBack)
        {
            return Register(new Hotkey(key, modifierKeys), callBack);
        }

        public void UnRegister(HotKeys hotKeys)
        {
            if (Registrations.TryGetValue(hotKeys, out var registration))
            {
                registration.Dispose();
                Registrations.Remove(hotKeys);
            }
            else
            {
                hotKeys.Registration?.Dispose();
            }
            hotKeys.Registration = null;
            hotKeys.IsRegistered = false;
        }
        public void UnRegister(HotKeyCallBackHanlder callBack)
        {
            if (CallbackRegistrations.TryGetValue(callBack, out var registration))
            {
                registration.Dispose();
                CallbackRegistrations.Remove(callBack);
            }
            else
            {
                GlobalHotKey.UnRegister(WindowHandle, callBack);
            }
        }

        public bool ModifiedHotkey(HotKeys hotkeys)
        {
            UnRegister(hotkeys);
            return Register(hotkeys);
        }

        public void ModifiedHotkey(Hotkey hotkey, HotKeyCallBackHanlder callBack)
        {
            if (callBack == null) return;
            UnRegister(callBack);
            if (!hotkey.IsNullOrEmpty()) Register(hotkey, callBack);

        }
    }
    

}
