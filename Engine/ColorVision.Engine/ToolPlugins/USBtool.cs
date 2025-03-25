using ColorVision.UI.Menus;

namespace ColorVision.Engine.ToolPlugins
{
    public class USBtool : MenuItemBase
    {

        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "UsbTreeView";
        public override int Order => 100;
        public override void Execute()
        {
            Common.Utilities.PlatformHelper.Open("Assets\\Tool\\UsbTreeView.exe");
        }
    }
}

