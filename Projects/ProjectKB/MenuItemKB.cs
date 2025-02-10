using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows;

namespace ProjectKB
{
    public class ProjectWindowInstance
    {
        public static ProjectKBWindow WindowInstance { get; set; }
    }


    public class PluginKB: IProjectBase
    {
        public  override string? Header => "键盘测试";
        public override string? UpdateUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Projects/ProjectKB";

        public override void Execute()
        {
            if (ProjectWindowInstance.WindowInstance == null)
            {
                ProjectWindowInstance.WindowInstance = new ProjectKBWindow
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

    public class MenuItemKB : MenuItemBase
    {
        public override string OwnerGuid => "Tool";

        public override string GuidId => nameof(MenuItemKB);

        public override int Order => 100;
        public override string Header => "键盘测试";

        public override void Execute()
        {
            if (ProjectWindowInstance.WindowInstance == null)
            {
                ProjectWindowInstance.WindowInstance = new ProjectKBWindow
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
