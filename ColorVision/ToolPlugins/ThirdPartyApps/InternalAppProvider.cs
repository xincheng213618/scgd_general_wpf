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
                    ActionId = "tools.network-adapter.open",
                    ResourceId = "network.adapters",
                    Order = -897,
                    IconGlyph = ThirdPartyAppIconGlyphs.NetworkAdapter,
                    LaunchAction = () =>
                    {
                        AccessControl.Demand("tools.network-adapter.open", PermissionMode.Administrator, "network.adapters");
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
