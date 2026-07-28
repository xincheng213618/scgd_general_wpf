#pragma warning disable CS8625
using ColorVision.UI.Menus;

namespace ProjectARVRPro.PluginConfig
{
    public class ProjectARVRMenu : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;

        public override int Order => 100;
        public override string Header => "模组检测";

        public override void Execute()
        {
            ProjectARVRWindowHost.ShowOrActivate();
        }
    }
}
