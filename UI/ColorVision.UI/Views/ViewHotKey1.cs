using ColorVision.UI.HotKey;
using System.Windows.Input;

namespace ColorVision.UI.Views
{
    public class ViewHotKey1: IHotKey
    {
        public HotKeys HotKeys => new HotKeys(Properties.Resources.ViewMode1, new Hotkey(Key.D1, ModifierKeys.Control), Execute);

        public static void Execute()
        {
            ViewGridManager.GetInstance().SetViewGrid(1);
        }
    }

    public class ViewHotKey2 : IHotKey
    {
        public HotKeys HotKeys => new HotKeys(Properties.Resources.ViewMode2, new Hotkey(Key.D2, ModifierKeys.Control), Execute);

        public static void Execute()
        {
            ViewGridManager.GetInstance().SetViewGrid(2);
        }
    }


    public class ViewHotKey4 : IHotKey
    {
        public HotKeys HotKeys => new HotKeys(Properties.Resources.ViewMode4, new Hotkey(Key.D4, ModifierKeys.Control), Execute);

        public static void Execute()
        {
            ViewGridManager.GetInstance().SetViewGrid(4);
        }
    }

    public class ViewHotKey9 : IHotKey
    {
        public HotKeys HotKeys => new HotKeys(Properties.Resources.ViewMode9, new Hotkey(Key.D9, ModifierKeys.Control), Execute);

        public static void Execute()
        {
            ViewGridManager.GetInstance().SetViewGrid(9);
        }
    }
}
