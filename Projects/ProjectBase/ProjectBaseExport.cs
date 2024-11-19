using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ProjectBase
{

    public class ProjectBasePlugin : IProjectBase
    {
        public override string Header => "基础项目";
        public override string? UpdateUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Projects/ProjectBase";

        public override void Execute()
        {
            new MainWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.Show();
        }
    }

    public class ProjectBaseExport : MenuItemBase
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
