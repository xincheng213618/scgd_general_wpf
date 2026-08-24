using ColorVision.Common.ThirdPartyApps;
using ColorVision.UI.Authorizations;
using System.Windows;

namespace ColorVision.UI.Desktop.ThirdPartyApps.Treemap
{
    public class TreemapAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new[]
            {
                new ThirdPartyAppInfo
                {
                    Name = "Treemap",
                    Group = "ColorVision",
                    Category = ThirdPartyAppCategory.Internal,
                    RequiredPermission = PermissionMode.Guest,
                    Order = -1000,
                    IconGlyph = ThirdPartyAppIconGlyphs.Treemap,
                    LaunchAction = () =>
                    {
                        new TreemapWindow
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
