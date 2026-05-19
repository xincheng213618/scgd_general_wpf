using ColorVision.UI.Menus;
using System.Windows;

namespace ProjectARVRPro.SocketRelay
{
    public class SocketRelayMenu : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;

        public override int Order => 101;
        public override string Header => "Socket中转服务器";

        public override void Execute()
        {
            SocketRelayWindow.OpenWindow();
        }
    }
}
