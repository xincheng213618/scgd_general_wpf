using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ColorVision.Projects.ProjectHeyuan
{
    public class ProjectWindowInstance
    {
        public static ProjectHeyuanWindow WindowInstance { get; set; }
    }

    public class PluginHeyuan : IFeatureLauncherBase
    {
        public override string? Header => "HeYuan";
        public override string? UpdateUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Projects/ProjectHeyuan";

        public override void Execute()
        {
            if (ProjectWindowInstance.WindowInstance == null)
            {
                ProjectWindowInstance.WindowInstance = new ProjectHeyuanWindow
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


    public class MenuItemHeyuan : MenuItemBase
    {
        public override string OwnerGuid => "Tool";

        public override string GuidId => "HeYuan";

        public override int Order => 100;
        public override string Header => "HeYuan";


        public override void Execute()
        {
            if (ProjectWindowInstance.WindowInstance == null)
            {
                ProjectWindowInstance.WindowInstance = new ProjectHeyuanWindow
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
