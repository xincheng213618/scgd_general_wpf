using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.UI.HotKey.WindowHotKey
{
    public class WindowHotKey
    {
        /// <summary>
        /// AllKeyMap
        /// </summary>
        static Dictionary<int, HotKeyCallBackHanlder> AllKeyMap = new();
        
        static List<Control> ControlHook = new();
        static Dictionary<Control,Dictionary<int, HotKeyCallBackHanlder>> ControlHookKeyMap = new();


        public static bool Register(Control control,Hotkey hotkey, HotKeyCallBackHanlder callBack)
        {
            if (hotkey == null || hotkey == Hotkey.None) return false;
            if (!ControlHook.Contains(control))
            {
                control.PreviewKeyUp += (s,e)=> 
                {
                    //e.Handled = true;
                    ModifierKeys modifiers = Keyboard.Modifiers;
                    if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
                        modifiers |= ModifierKeys.Windows;
                    Key key = e.Key;

                    if (key == Key.System)
                        key = e.SystemKey;


                    // Pressing delete, backspace or escape without modifiers clears the current value
                    if (modifiers == ModifierKeys.None && (key == Key.Delete || key == Key.Back || key == Key.Escape))
                    {
                        return;
                    }

                    // If no actual key was pressed - retur
                    if (key == Key.LeftCtrl || key == Key.RightCtrl || key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin || key == Key.Clear || key == Key.OemClear || key == Key.Apps)
                        return;
                    // Update the value
                    if (ControlHookKeyMap[control].TryGetValue(((int)modifiers << 8) + (int)key, out var callback))
                    {
                        callback();
                    }
                };
                ControlHook.Add(control);
                ControlHookKeyMap.Add(control, new Dictionary<int, HotKeyCallBackHanlder>());
            }
            int vk = hotkey.ToInt();
            if (AllKeyMap.TryGetValue(vk,out HotKeyCallBackHanlder hotKeyCallBackHanlder))
            {
                return false;
            }
            else
            {
                ControlHookKeyMap[control].Add(vk, callBack);
                AllKeyMap.Add(vk, callBack);
            }
            return true;
        }
        public static bool UnRegister(HotKeyCallBackHanlder callBack)
        {
            var keysToRemove = AllKeyMap.Where(kv => kv.Value == callBack).Select(kv => kv.Key).ToList();
            foreach (var key in keysToRemove)
            {
                AllKeyMap.Remove(key);
            }
            
            foreach (var item in ControlHookKeyMap)
            {
                var controlKeysToRemove = item.Value.Where(kv => kv.Value == callBack).Select(kv => kv.Key).ToList();
                foreach (var key in controlKeysToRemove)
                {
                    item.Value.Remove(key);
                }
            }
            return true;
        }






    }
}
