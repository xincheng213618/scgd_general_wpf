using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.LogImp
{
    public class MenuLogWindow : GlobalMenuBase, IHotKey
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 10005;
        public override string Header => Properties.Resources.Log;
        public static Hotkey Hotkey { get; set; } = new(Key.L, ModifierKeys.Control | ModifierKeys.Alt);
        public HotKeys HotKeys => new HotKeys(Properties.Resources.Log, Hotkey, Execute) { Description = BuiltInHotkeyDescriptions.OpenLog };
        public override void Execute() => new WindowLog() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
    }
}
