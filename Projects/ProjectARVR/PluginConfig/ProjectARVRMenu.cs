using ColorVision.UI.Menus;
using System.Windows;

namespace ProjectARVR.PluginConfig
{



    public class ProjectARVRMenu : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;

        public override int Order => 100;
        public override string Header => "ARVR Detect";

        public override void Execute()
        {
            if (ProjectWindowInstance.WindowInstance == null)
            {
                ProjectWindowInstance.WindowInstance = new ARVRWindow
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
