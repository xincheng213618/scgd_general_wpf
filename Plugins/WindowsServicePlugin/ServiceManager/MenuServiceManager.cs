using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI.Authorizations;
using System.Windows;

namespace WindowsServicePlugin.ServiceManager
{
    public class ServiceManagerAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = "服务管理器",
                    Group = "内部工具",
                    Category = ThirdPartyAppCategory.Internal,
                    RequiredPermission = PermissionMode.Administrator,
                    Order = 3,
                    IconGlyph = ThirdPartyAppIconGlyphs.ServiceManager,
                    LaunchAction = () =>
                    {
                        new ServiceManagerWindow
                        {
                            Owner = Application.Current.GetActiveWindow(),
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        }.Show();
                    },
                }
            };
        }
    }

}
