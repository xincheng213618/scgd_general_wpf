using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ProjectBase
{
    public class ProjectBaseExport : MenuItemBase, IProject
    {
        public override string OwnerGuid => "Tool";

        public override string GuidId => nameof(ProjectBaseExport);
        public override int Order => 100;
        public override string Header => "基础项目";

        public override void Execute()
        {
            new MainWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }
}
