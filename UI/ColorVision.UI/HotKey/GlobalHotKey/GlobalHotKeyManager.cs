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
                registration.Dispose();
            }

            Registrations.Clear();
            CallbackRegistrations.Clear();
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
            if (hotKeys == null || hotKeys.Kinds != HotKeyKinds.Global || hotKeys.HotKeyHandler == null) return null;

            var registration = GlobalHotKey.Register(WindowHandle, hotKeys.Hotkey.Modifiers, hotKeys.Hotkey.Key, hotKeys.HotKeyHandler);
            hotKeys.Registration = registration;
            hotKeys.IsRegistered = registration?.IsRegistered == true;
            if (registration != null)
            {
                Registrations[hotKeys] = registration;
            }
            return registration;
        }

        public bool Register(Hotkey hotkey, HotKeyCallBackHanlder callBack)
        {
            if (hotkey.IsNullOrEmpty()) return false;
            var registration = GlobalHotKey.Register(WindowHandle, hotkey.Modifiers, hotkey.Key, callBack);
            if (registration == null) return false;

            CallbackRegistrations[callBack] = registration;
            return true;
        }
        public bool Register(ModifierKeys modifierKeys, Key key, HotKeyCallBackHanlder callBack)
        {
            var registration = GlobalHotKey.Register(WindowHandle, modifierKeys, key, callBack);
            if (registration == null) return false;

            CallbackRegistrations[callBack] = registration;
            return true;
        }

        public void UnRegister(HotKeys hotKeys)
        {
            if (Registrations.Remove(hotKeys, out var registration))
            {
                registration.Dispose();
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
            if (CallbackRegistrations.Remove(callBack, out var registration))
            {
                registration.Dispose();
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
