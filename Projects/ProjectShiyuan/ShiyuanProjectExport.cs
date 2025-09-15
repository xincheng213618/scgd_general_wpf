#pragma warning disable CS8625
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Projects.ProjectShiYuan
{
    public class ProjectWindowInstance
    {
        public static ShiyuanProjectWindow WindowInstance { get; set; }
    }

    public class ShiyuanProjectPlugin : IFeatureLauncherBase
    {
        public override string? Header => "ProjectShiyuan";
        public override string? UpdateUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Projects/ProjectShiyuan";

        public override string Description { get; set; } = "视源项目";
        public override void Execute()
        {
            if (ProjectWindowInstance.WindowInstance == null)
            {
                ProjectWindowInstance.WindowInstance = new ShiyuanProjectWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                ProjectWindowInstance.WindowInstance.Closed += (s, e) => ProjectWindowInstance.WindowInstance = null;
                ProjectWindowInstance.WindowInstance.Show();
            }
            else
            {
                ProjectWindowInstance.WindowInstance.Activate();
            }
        }
    }

    public class ShiyuanProjectExport : MenuItemBase
    {
        public override string OwnerGuid => "Tool";

        public override string GuidId => "ProjectShiyuan";

        public override int Order => 100;
        public override string Header => "视源项目";

        public override void Execute()
        {
            if (ProjectWindowInstance.WindowInstance == null)
            {
                ProjectWindowInstance.WindowInstance = new ShiyuanProjectWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                ProjectWindowInstance.WindowInstance.Closed += (s, e) => ProjectWindowInstance.WindowInstance = null;
                ProjectWindowInstance.WindowInstance.Show();
            }
            else
            {
                ProjectWindowInstance.WindowInstance.Activate();
            }
        }
    }
}
