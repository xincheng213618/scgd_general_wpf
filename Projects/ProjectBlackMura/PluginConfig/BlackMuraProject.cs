using ColorVision.UI;
using System.Windows;

namespace ProjectBlackMura.PluginConfig
{
    public class BlackMuraProject : IFeatureLauncherBase
    {
        public override string? Header => "BlackMura检测";

        public override string? UpdateUrl => "http://xc213618.ddns.me:9999/D%3A/ColorVision/Projects/ProjectBlackMura";

        public override void Execute()
        {
            if (ProjectWindowInstance.WindowInstance == null)
            {
                ProjectWindowInstance.WindowInstance = new MainWindow
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
