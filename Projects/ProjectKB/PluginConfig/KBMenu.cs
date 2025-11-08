using ColorVision.UI.Menus;
using System.Windows;

namespace ProjectKB.PluginConfig
{



    public class KBMenu : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;

        public override int Order => 100;
        public override string Header => ProjectKB.Properties.Resources.KBDetect;

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
