using ColorVision.Properties;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Projects
{
    public class MenuProjectManager : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Help;
        public override int Order => 10000;
        public override string Header => Resources.ProjectManagerWindow;

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new ProjectManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
