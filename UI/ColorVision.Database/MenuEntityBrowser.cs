using ColorVision.Common.ThirdPartyApps;
using ColorVision.Database.Properties;
using ColorVision.UI.Authorizations;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.Database
{
    public class DatabaseBrowserAppProvider : IThirdPartyAppProvider
    {
        public IEnumerable<ThirdPartyAppInfo> GetThirdPartyApps()
        {
            return new[]
            {
                new ThirdPartyAppInfo
                {
                    Name = Resources.MenuEntityBrowser,
                    Group = "ColorVision",
                    Category = ThirdPartyAppCategory.Internal,
                    RequiredPermission = PermissionMode.Administrator,
                    Order = 50,
                    IconGlyph = ThirdPartyAppIconGlyphs.Database,
                    LaunchAction = OpenDatabaseBrowser,
                }
            };
        }

        private static void OpenDatabaseBrowser()
        {
            Window? owner = Application.Current.GetActiveWindow();
            new DatabaseBrowserWindow
            {
                Owner = owner,
                WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner
            }.Show();
        }
    }
}
