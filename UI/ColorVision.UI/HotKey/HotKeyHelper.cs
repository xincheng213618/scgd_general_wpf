using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.HotKey
{



    public static partial class HotKeysExtension
    {
        public static void LoadHotKeyFromAssembly(this Window This)
        {
            HotkeyService.GetInstance().LoadFromAssemblies(This);

        }

        public static bool AddHotKeys(this Window This, HotKeys hotKeys)
        {
            return HotkeyService.GetInstance().AddHotKeys(This, hotKeys);
        }

        public static bool AddHotKeys(this Control This, HotKeys hotKeys) => HotkeyService.GetInstance().AddHotKeys(This, hotKeys);

        public static bool AddHotKeys(this Window This, Hotkey hotKey, HotKeyCallBackHanlder hotKeyHandler, HotKeyKinds hotKeyKinds = HotKeyKinds.Windows) => HotkeyService.GetInstance().RegisterHotkey(This, hotKey, hotKeyHandler, hotKeyKinds);
        public static bool AddHotKeysGlobal(this Window This, Hotkey hotKey, HotKeyCallBackHanlder hotKeyHandler) => AddHotKeys(This, hotKey, hotKeyHandler, HotKeyKinds.Global);


    }
    [Obsolete("Use HotkeyService directly.")]
    public class HotKeyHelper
    {
        private static readonly HotKeyHelper Instance = new();
        public static HotKeyHelper GetInstance()
        {
            return Instance;
        }
        
        public void AddHotKeys(Window window, HotKeys hotKeys)
        {
            HotkeyService.GetInstance().AddHotKeys(window, hotKeys);
        }

        public static void RegisterHotKeysList()
        {
            HotkeyService.GetInstance().RegisterAll();
        }
        public static void UnRegisterHotKeysList()
        {
            HotkeyService.GetInstance().UnregisterAll();
        }


    }
}
