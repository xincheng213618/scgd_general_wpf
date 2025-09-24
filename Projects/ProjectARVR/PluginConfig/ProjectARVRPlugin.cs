using ColorVision.UI;
using System.Windows;

namespace ProjectARVR.PluginConfig
{

    public class ProjectARVRPlugin : IFeatureLauncherBase
    {
        public override string? Header => "ARVR检测";

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
