using ColorVision.UI.Menus;
using System.Windows;

namespace ProjectARVRPro.PluginConfig
{
    public class SocketRelayMenu : MenuItemBase
    {
        public override string OwnerGuid => MenuItemConstants.Tool;

        public override int Order => 101;
        public override string Header => "Socket中转服务器";

        private static SocketRelayWindow _window;

        public override void Execute()
        {
            if (_window == null)
            {
                _window = new SocketRelayWindow
                {
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                _window.Closed += (s, e) => _window = null;
                _window.Show();
            }
            else
            {
                _window.Activate();
            }
        }
    }
}
