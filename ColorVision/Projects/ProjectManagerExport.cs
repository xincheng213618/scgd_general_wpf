using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Projects
{
    public class ProjectManagerExport : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => nameof(ProjectManagerExport);
        public override int Order => 10000;
        public override string Header => "项目管理";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new ProjectManagerWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }
}
