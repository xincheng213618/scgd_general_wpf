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

    public class SSCOMTool : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "SSCOM串口调试助手";
        public override int Order => 100;
        public override void Execute()
        {
            Common.Utilities.PlatformHelper.Open("Assets\\Tool\\sscom5.13.1.exe");
        }
    }

    public class SpectrAdjtool : MenuItemBase
    {

        public override string OwnerGuid => MenuItemConstants.View;
        public override string Header => "校正文件修正工具";
        public override int Order => 100;
        public override void Execute()
        {
            Common.Utilities.PlatformHelper.Open("Assets\\Tool\\SpectrAdj.exe");
        }
    }
}

