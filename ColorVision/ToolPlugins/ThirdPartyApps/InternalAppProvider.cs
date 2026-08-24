using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI.Authorizations;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ToolPlugins.ThirdPartyApps
{
    public class InternalAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new List<ThirdPartyAppInfo>
            {
                new ThirdPartyAppInfo
                {
                    Name = "上网网卡选择",
                    Group = ThirdPartyAppGroupNames.CommonTools,
                    Category = ThirdPartyAppCategory.Internal,
                    RequiredPermission = PermissionMode.Administrator,
                    Order = -897,
                    IconGlyph = ThirdPartyAppIconGlyphs.NetworkAdapter,
                    LaunchAction = () =>
                    {
                        new NetworkAdapterPriorityWindow
                        {
                            Owner = Application.Current.GetActiveWindow(),
                            WindowStartupLocation = WindowStartupLocation.CenterOwner
                        }.Show();
                    },
                }
            };
        }
    }
}
