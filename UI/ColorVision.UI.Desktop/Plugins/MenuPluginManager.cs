using ColorVision.UI.Properties;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;
using ColorVision.UI.Plugins;

namespace ColorVision.UI.Desktop.Plugins
{
    public class MenuPluginManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 9000;
        public override string Header => Resources.PluginManagerWindow;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new PluginManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
