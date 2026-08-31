using ColorVision.UI.HotKey;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Update
{
    public class MenuCheckAndUpdateV1 : MenuItemBase, IHotKey
    {
        public HotKeys HotKeys => new HotKeys(Properties.Resources.CheckForUpdates, new Hotkey(), Execute) { Description = BuiltInHotkeyDescriptions.CheckUpdates };

        public override string OwnerGuid => MenuItemConstants.Help;

        public override int Order => 10003;

        public override Visibility Visibility => Visibility.Visible;

        public override string Header => Properties.Resources.CheckForUpdates;

        public override void Execute() => _ = CombinedUpdateCoordinator.StartInteractiveAsync();
    }
}
