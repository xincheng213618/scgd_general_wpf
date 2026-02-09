using ColorVision.UI.Menus;

namespace ColorVision.Engine.ToolPlugins
{

    public class SSCOMTool : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => ColorVision.Engine.Properties.Resources.SSCOMTool;
        public override int Order => 101;
        public override void Execute()
        {
            Common.Utilities.PlatformHelper.Open("Assets\\Tool\\sscom5.13.1.exe");
        }
    }
}

