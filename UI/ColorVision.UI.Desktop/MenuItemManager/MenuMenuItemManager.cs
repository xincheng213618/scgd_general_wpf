using ColorVision.Common.Utilities;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    public class MenuMenuItemManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;
        public override string Header => "MenuItemManager";
        public override int Order => 10000;

        public override void Execute()
        {
            new MenuItemManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
