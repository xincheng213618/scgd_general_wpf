#pragma warning disable CS8625
using ColorVision.UI;

namespace ProjectARVRPro.PluginConfig
{

    public class ProjectARVRLitePlugin : IFeatureLauncherBase
    {
        public override string? Header => "ARVRPro";

        public override void Execute()
        {
            ProjectARVRWindowHost.ShowOrActivate();
        }
    }
}
