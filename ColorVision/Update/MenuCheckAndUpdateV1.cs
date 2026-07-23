using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Update
{
    public class MenuCheckAndUpdateV1 : MenuItemBase, IHotKey
    {
        public HotKeys HotKeys => new HotKeys(Properties.Resources.CheckForUpdates, new Hotkey(Key.U, ModifierKeys.Control), Execute);

        public override string OwnerGuid => MenuItemConstants.Help;

        public override int Order => 10003;

        public override Visibility Visibility => Visibility.Visible;

        public override string Header => Properties.Resources.CheckForUpdates;

        public override string InputGestureText => "Ctrl + U";

        public override void Execute() => _ = CombinedUpdateCoordinator.StartInteractiveAsync();
    }
}
