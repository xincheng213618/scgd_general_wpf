using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI
{
    public class MenuLogWindow : MenuItemBase, IHotKey
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 10005;
        public override string Header => Properties.Resources.Log;
        public override string InputGestureText => Hotkey.ToString();

        public static Hotkey Hotkey { get; set; } = new Hotkey(Key.L , ModifierKeys.Control);
        public HotKeys HotKeys => new HotKeys(Properties.Resources.Log, Hotkey, Execute);
        public override void Execute() => new WindowLog() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
    }
}
