using System.Collections.Generic;
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
            HotKeysList = new List<HotKeys>();
        }

        private static WindowHotKeyManager instance;

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
                    instance = new WindowHotKeyManager(control);
                }
            }
            return instance;
        }




        public List<HotKeys> HotKeysList { get; set; }


        public bool Register(HotKeys hotkeys)
        {
            hotkeys.Control = control;
            hotkeys.IsRegistered = WindowHotKey.Register(control, hotkeys.Hotkey, hotkeys.HotKeyHandler);
            HotKeysList.Add(hotkeys);
            return hotkeys.IsRegistered;
        }

        public bool Register(Hotkey hotkey, HotKeyCallBackHanlder callBack)
        {
            if (!WindowHotKey.Register(control, hotkey, callBack))
                return false;
            HotKeysList.Add(new HotKeys() { Hotkey =hotkey,HotKeyHandler = callBack});
            return true;
        }

        public bool UnRegister(HotKeys hotkeys)
        {
            WindowHotKey.UnRegister(hotkeys.HotKeyHandler);
            HotKeysList.Remove(hotkeys);
            return true;
        }

        public bool UnRegister(HotKeyCallBackHanlder callBack)
        {
            WindowHotKey.UnRegister(callBack);
            foreach (var item in HotKeysList)
            {
                if (callBack == item.HotKeyHandler)
                {
                    HotKeysList.Remove(item);
                }
            }
            return true;
        }


        public bool ModifiedHotkey(HotKeys hotkeys)
        {
            WindowHotKey.UnRegister(hotkeys.HotKeyHandler);
            return WindowHotKey.Register(control, hotkeys.Hotkey, hotkeys.HotKeyHandler);
        }

        public void ModifiedHotkey(Hotkey hotkey, HotKeyCallBackHanlder callBack)
        {
            WindowHotKey.UnRegister(callBack);
            WindowHotKey.Register(control, hotkey, callBack);

            foreach (var item in HotKeysList)
            {
                if (callBack == item.HotKeyHandler)
                {
                    HotKeysList.Remove(item);
                }
            }
            HotKeysList.Add(new HotKeys() { Hotkey = hotkey, HotKeyHandler = callBack });
        }

    }
}
