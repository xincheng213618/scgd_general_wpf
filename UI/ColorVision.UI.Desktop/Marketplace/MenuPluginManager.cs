using ColorVision.UI.Authorizations;
using ColorVision.UI.Desktop.Properties;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.UI.Desktop.Marketplace
{
    public class MenuPluginManager : GlobalMenuBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 9000;
        public override string Header => LocalizedMenuAccessKey.Format(Resources.Marketplace, 'P');

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new MarketplaceWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
