using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Plugins
{
    public class PluginManagerExport : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => nameof(PluginManagerExport);
        public override int Order => 10000;
        public override string Header => "插件管理";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new PluginManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
