using ColorVision.Common.ThirdPartyApps;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.SocketProtocol;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Engine.ToolPlugins.ThirdPartyApps
{
    public class InternalAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            string group = Properties.Resources.InternalTools;
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = ColorVision.SocketProtocol.Properties.Resources.SocketManagementWindow,
                    Group = group,
                    Order = 0,
                    LaunchAction = () =>
                    {
                        new SocketManagerWindow()
                        {
                            Owner = Application.Current.GetActiveWindow(),
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        }.Show();
                    },
                },
                new ThirdPartyAppInfo
                {
                    Name = Properties.Resources.MenuPhyCameraManager,
                    Group = group,
                    Order = 1,
                    LaunchAction = () =>
                    {
                        new PhyCameraManagerWindow()
                        {
                            Owner = Application.Current.GetActiveWindow(),
                            WindowStartupLocation = WindowStartupLocation.CenterScreen
                        }.ShowDialog();
                    },
                },
            };
        }
    }
}
